using System.Net;

namespace Wjss.Adapters.TcAcquisition;

/// <summary>
/// FluentModbus-backed implementation of <see cref="ITcReadOnlyClient"/>.
/// Library isolation: only FluentModbus types (ModbusTcpClient, ModbusException) appear inside
/// this file; they are never exposed through the public contract (see ITcReadOnlyClient docs).
/// Read-only discipline: only FC03 (Read Holding Registers) and FC04 (Read Input Registers) are
/// ever called; the choice is a test parameter (UseInputRegisters) and remains CANDIDATE_UNVERIFIED.
/// Stale fencing: a monotonically increasing generation is bumped on Connect/Disconnect; a ReadAsync
/// call captures the generation at entry and, after the library await, rejects the result if the
/// generation changed while the read was in flight.
/// </summary>
public sealed class FluentModbusTcClient : ITcReadOnlyClient
{
    private readonly object _gate = new();
    private int _generation;
    private TcNodeEndpoint? _endpoint;
    // The FluentModbus type is isolated to this field; no caller sees it.
    private FluentModbus.ModbusTcpClient? _client;
    private bool _disposed;

    public int Generation
    {
        get { lock (_gate) return _generation; }
    }

    public bool IsConnected
    {
        get { lock (_gate) return _client?.IsConnected == true && !_disposed; }
    }

    public TcNodeEndpoint? ConnectedEndpoint
    {
        get { lock (_gate) return _endpoint; }
    }

    public async Task<TcReadResult> ConnectAsync(TcNodeEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        endpoint.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        FluentModbus.ModbusTcpClient? clientToDispose = null;
        int newGeneration;
        lock (_gate)
        {
            newGeneration = ++_generation;
            clientToDispose = _client;
            _client = null;
            _endpoint = endpoint;
        }
        if (clientToDispose is not null)
        {
            try { clientToDispose.Dispose(); } catch { /* dispose never throws */ }
        }

        var ip = IPAddress.Parse(endpoint.IpAddress);
        var endPoint = new IPEndPoint(ip, endpoint.Port);
        var client = new FluentModbus.ModbusTcpClient
        {
            // Bounded timeout; default Infinite would hang forever on a dead peer.
            ReadTimeout = 2000,
            WriteTimeout = 2000,
        };

        try
        {
            // FluentModbus Connect is synchronous; honor cancellation by racing it.
            // If cancelled, Dispose the client and report Cancelled.
            var connectTask = Task.Run(() => client.Connect(endPoint), cancellationToken);
            await connectTask.ConfigureAwait(false);

            lock (_gate)
            {
                if (_disposed)
                {
                    clientToDispose = client;
                    client = null!;
                }
                else
                {
                    _client = client;
                    client = null!;
                }
            }
            if (clientToDispose is not null)
            {
                try { clientToDispose.Dispose(); } catch { }
                return TcReadResult.Failure(TcReadKind.ConnectionFailure, endpoint, newGeneration, "DISPOSED_DURING_CONNECT", "Disposed while connecting.");
            }

            return new TcReadResult
            {
                Kind = TcReadKind.Success,
                Endpoint = endpoint,
                Generation = newGeneration,
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                RawWords = Array.Empty<ushort>(),
                DetailCode = "CONNECTED",
                Detail = $"Connected generation {newGeneration}.",
            };
        }
        catch (OperationCanceledException)
        {
            try { client.Dispose(); } catch { }
            return TcReadResult.CancelledResult(endpoint, newGeneration);
        }
        catch (FluentModbus.ModbusException ex)
        {
            try { client.Dispose(); } catch { }
            lock (_gate) { _endpoint = null; }
            return TcReadResult.Failure(TcReadKind.ProtocolException, endpoint, newGeneration, "MODBUS_EXCEPTION", ex.Message);
        }
        catch (Exception ex) when (ex is TimeoutException || ex is System.Net.Sockets.SocketException || ex is System.IO.IOException)
        {
            try { client.Dispose(); } catch { }
            lock (_gate) { _endpoint = null; }
            var kind = ex is TimeoutException ? TcReadKind.Timeout : TcReadKind.ConnectionFailure;
            return TcReadResult.Failure(kind, endpoint, newGeneration, ex.GetType().Name.ToUpperInvariant(), ex.Message);
        }
    }

    public async Task<TcReadResult> ReadAsync(TcReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Endpoint);
        request.Endpoint.Validate();
        if (request.Quantity == 0) return TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, request.Generation, "QUANTITY_ZERO", "Quantity must be >0.");
        if (request.Quantity > 125) return TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, request.Generation, "QUANTITY_EXCEEDS_MODBUS_LIMIT", "Modbus PDU limits read to 125 registers.");
        ThrowIfDisposed();

        int currentGen;
        FluentModbus.ModbusTcpClient? client;
        lock (_gate)
        {
            currentGen = _generation;
            client = _client;
        }

        if (request.Generation != currentGen)
            return TcReadResult.Stale(request.Endpoint, request.Generation, currentGen);
        if (client is null || !client.IsConnected)
            return TcReadResult.Failure(TcReadKind.TransportClosed, request.Endpoint, request.Generation, "NOT_CONNECTED", "No connected transport.");
        if (cancellationToken.IsCancellationRequested)
            return TcReadResult.CancelledResult(request.Endpoint, request.Generation);

        // Apply per-call timeout if supplied
        var previousReadTimeout = client.ReadTimeout;
        var previousWriteTimeout = client.WriteTimeout;
        if (request.Timeout != TimeSpan.Zero)
        {
            var ms = (int)request.Timeout.TotalMilliseconds;
            if (ms > 0 && ms < int.MaxValue)
            {
                client.ReadTimeout = ms;
                client.WriteTimeout = ms;
            }
        }

        try
        {
            // Read-only: FC04 or FC03 only. Never call write/coil APIs.
            // Quantity and start address are CANDIDATE_UNVERIFIED (0 and 40 for the 5×8 model).
            ushort[] words;
            if (request.UseInputRegisters)
            {
                var span = await client.ReadInputRegistersAsync<ushort>((int)request.Endpoint.UnitIdentifier, request.StartAddress, request.Quantity, cancellationToken).ConfigureAwait(false);
                words = span.ToArray();
            }
            else
            {
                var span = await client.ReadHoldingRegistersAsync<ushort>((int)request.Endpoint.UnitIdentifier, request.StartAddress, request.Quantity, cancellationToken).ConfigureAwait(false);
                words = span.ToArray();
            }

            // Stale fence after await: if Disconnect happened while we awaited, reject.
            lock (_gate) { currentGen = _generation; }
            if (request.Generation != currentGen)
                return TcReadResult.Stale(request.Endpoint, request.Generation, currentGen);

            return TcReadResult.SuccessResult(request.Endpoint, request.Generation, words, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            return TcReadResult.CancelledResult(request.Endpoint, request.Generation);
        }
        catch (FluentModbus.ModbusException ex)
        {
            return TcReadResult.Failure(TcReadKind.ProtocolException, request.Endpoint, request.Generation, "MODBUS_EXCEPTION", ex.Message);
        }
        catch (TimeoutException ex)
        {
            return TcReadResult.Failure(TcReadKind.Timeout, request.Endpoint, request.Generation, "TIMEOUT", ex.Message);
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException || ex is System.IO.IOException)
        {
            var kind = ex is System.Net.Sockets.SocketException ? TcReadKind.ConnectionFailure : TcReadKind.TransportClosed;
            return TcReadResult.Failure(kind, request.Endpoint, request.Generation, ex.GetType().Name.ToUpperInvariant(), ex.Message);
        }
        catch (Exception ex)
        {
            return TcReadResult.Failure(TcReadKind.MalformedResponse, request.Endpoint, request.Generation, "UNEXPECTED_RESPONSE", ex.Message);
        }
        finally
        {
            client.ReadTimeout = previousReadTimeout;
            client.WriteTimeout = previousWriteTimeout;
        }
    }

    public Task DisconnectAsync()
    {
        FluentModbus.ModbusTcpClient? toDispose = null;
        lock (_gate)
        {
            _generation++;
            toDispose = _client;
            _client = null;
            _endpoint = null;
        }
        if (toDispose is not null)
        {
            try { toDispose.Dispose(); } catch { }
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        FluentModbus.ModbusTcpClient? toDispose = null;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            toDispose = _client;
            _client = null;
            _endpoint = null;
        }
        if (toDispose is not null)
        {
            try { toDispose.Dispose(); } catch { }
        }
        await Task.CompletedTask.ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FluentModbusTcClient));
        }
    }
}
