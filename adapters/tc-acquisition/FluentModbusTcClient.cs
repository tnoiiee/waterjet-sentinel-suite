using System.Net;

namespace Wjss.Adapters.TcAcquisition;

/// <summary>
/// FluentModbus-backed implementation of <see cref="ITcReadOnlyClient"/>.
/// Library isolation: only FluentModbus types (ModbusTcpClient, ModbusException) appear inside
/// this file; they are never exposed through the public contract (see ITcReadOnlyClient docs).
/// Read-only discipline: only FC03 (Read Holding Registers) and FC04 (Read Input Registers) are
/// ever called, selected explicitly via <see cref="TcRegisterArea"/> with no fallback; the choice
/// remains CANDIDATE_UNVERIFIED.
/// Stale fencing: a monotonically increasing generation is bumped on Connect/Disconnect/Dispose;
/// a ReadAsync call captures the generation at entry and, after the library await, rejects the
/// result if the generation changed while the read was in flight. One outstanding read per client
/// is enforced via a non-blocking gate.
/// Endianness: the connection is explicitly established with <c>ModbusEndianness.BigEndian</c>
/// (Modbus network order, CANDIDATE_UNVERIFIED). Returned ushort values are network-raw register
/// words as decoded by the library according to that setting (SwapBytes on little-endian hosts);
/// scaling, wire-break and multi-register word ordering remain unverified. Word-order for future
/// 32-bit values remains unverified.
/// Connect lifecycle: FluentModbus <c>ModbusTcpClient.Connect</c> is synchronous and not
/// cooperatively cancellable mid-handshake; it is bounded by <c>ConnectTimeout</c> (finite, 2000 ms).
/// Cancellation is honored before the call starts and after it completes (late success is fenced
/// by generation and disposed). A cancellation that occurs during the blocking Connect wait does
/// not interrupt the underlying socket attempt — the attempt runs to timeout or completion and is
/// then fenced/disposed. See ADR-0018 §4.
/// Timeout handling: the transport is configured with fixed finite timeouts at creation
/// (<c>ConnectTimeout=2000</c>, <c>ReadTimeout=2000</c>, <c>WriteTimeout=2000</c>); per-call
/// <see cref="TcReadRequest.Timeout"/> is enforced via a linked <c>CancellationTokenSource</c>
/// rather than mutating the shared client timeout properties, so no concurrent mutation race occurs
/// and cleanup cannot hide the primary failure. Disconnect/Dispose fencing and deterministic cleanup
/// are preserved.
/// Memory ownership: FluentModbus returns transport-owned memory that may be reused; the adapter
/// copies into an application-owned <c>ushort[]</c> before leaving the boundary.
/// </summary>
public sealed class FluentModbusTcClient : ITcReadOnlyClient
{
    private const int FixedTimeoutMs = 2000;
    private const FluentModbus.ModbusEndianness SelectedWireEndianness = FluentModbus.ModbusEndianness.BigEndian;
    private const TcEndiannessCandidate SelectedEndiannessCandidate = TcEndiannessCandidate.BigEndian;

    private readonly object _gate = new();
    private readonly SemaphoreSlim _singleRead = new(1, 1);
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
        if (cancellationToken.IsCancellationRequested)
            return TcReadResult.CancelledResult(endpoint, Volatile.Read(ref _generation));
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

        IPAddress ip;
        try
        {
            ip = IPAddress.Parse(endpoint.IpAddress);
        }
        catch (Exception ex)
        {
            lock (_gate) { _endpoint = null; }
            return TcReadResult.Failure(TcReadKind.ConfigurationRejected, endpoint, newGeneration, "INVALID_IP", ex.Message);
        }

        var endPoint = new IPEndPoint(ip, endpoint.Port);
        var client = new FluentModbus.ModbusTcpClient
        {
            ConnectTimeout = FixedTimeoutMs,
            ReadTimeout = FixedTimeoutMs,
            WriteTimeout = FixedTimeoutMs,
        };

        // Connect is synchronous and not cooperatively cancellable; it is bounded by ConnectTimeout.
        // We must not claim that Task.Run with a token interrupts the underlying socket.
        // Honor cancellation only before the call and fence a late success after.
        if (cancellationToken.IsCancellationRequested)
        {
            try { client.Dispose(); } catch { }
            lock (_gate) { _endpoint = null; }
            return TcReadResult.CancelledResult(endpoint, newGeneration);
        }

        Exception? connectEx = null;
        bool connected = false;
        try
        {
            // Synchronous bounded connect with explicit endianness (BigEndian network order, CANDIDATE_UNVERIFIED).
            client.Connect(endPoint, SelectedWireEndianness);
            connected = true;
        }
        catch (Exception ex)
        {
            connectEx = ex;
        }

        // If cancellation was requested while we were in the blocking Connect, fence the result.
        // The underlying Connect cannot be interrupted; we dispose the client and report Cancelled
        // rather than publishing a late successful connection as current.
        if (cancellationToken.IsCancellationRequested)
        {
            try { client.Dispose(); } catch { }
            if (connected)
            {
                lock (_gate) { _endpoint = null; }
                return TcReadResult.CancelledResult(endpoint, newGeneration);
            }
            // If Connect threw as well, cancellation takes precedence as the caller's intent.
            lock (_gate) { _endpoint = null; }
            return TcReadResult.CancelledResult(endpoint, newGeneration);
        }

        if (connectEx is not null)
        {
            try { client.Dispose(); } catch { }
            lock (_gate) { _endpoint = null; }
            if (connectEx is FluentModbus.ModbusException mex)
                return TcReadResult.Failure(TcReadKind.ProtocolException, endpoint, newGeneration, "MODBUS_EXCEPTION", mex.Message);
            if (connectEx is TimeoutException)
                return TcReadResult.Failure(TcReadKind.Timeout, endpoint, newGeneration, "CONNECT_TIMEOUT", connectEx.Message);
            if (connectEx is System.Net.Sockets.SocketException || connectEx is System.IO.IOException)
            {
                var kind = connectEx is System.Net.Sockets.SocketException ? TcReadKind.ConnectionFailure : TcReadKind.TransportClosed;
                return TcReadResult.Failure(kind, endpoint, newGeneration, connectEx.GetType().Name.ToUpperInvariant(), connectEx.Message);
            }
            // FluentModbus Connect wraps timeout as generic Exception with Message "ModbusClient_TcpConnectTimeout"
            if (connectEx.Message.Contains("TcpConnectTimeout", StringComparison.OrdinalIgnoreCase) || connectEx.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                return TcReadResult.Failure(TcReadKind.Timeout, endpoint, newGeneration, "CONNECT_TIMEOUT", connectEx.Message);
            return TcReadResult.Failure(TcReadKind.ConnectionFailure, endpoint, newGeneration, connectEx.GetType().Name.ToUpperInvariant(), connectEx.Message);
        }

        // Fence stale generation or dispose that raced with Connect.
        // If another Connect/Disconnect/Dispose bumped generation while we were connecting, do not publish success.
        lock (_gate)
        {
            if (_disposed)
            {
                try { client.Dispose(); } catch { }
                _endpoint = null;
                return TcReadResult.Failure(TcReadKind.ConnectionFailure, endpoint, newGeneration, "DISPOSED_DURING_CONNECT", "Disposed while connecting.");
            }
            if (_generation != newGeneration)
            {
                try { client.Dispose(); } catch { }
                _endpoint = null;
                return TcReadResult.Stale(endpoint, newGeneration, _generation);
            }
            _client = client;
            // client is now owned by this instance; clear local reference to prevent double-dispose
            client = null!;
        }

        return new TcReadResult
        {
            Kind = TcReadKind.Success,
            Endpoint = endpoint,
            Generation = newGeneration,
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            RawWords = Array.Empty<ushort>(),
            DetailCode = "CONNECTED",
            Detail = $"Connected generation {newGeneration} with {SelectedWireEndianness} (CANDIDATE_UNVERIFIED).",
            RegisterArea = null,
            Endianness = SelectedEndiannessCandidate,
        };
    }

    public async Task<TcReadResult> ReadAsync(TcReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Endpoint);
        request.Endpoint.Validate();
        if (request.Quantity == 0) return TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, request.Generation, "QUANTITY_ZERO", "Quantity must be >0.", request.RegisterArea, request.Endianness);
        if (request.Quantity > 125) return TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, request.Generation, "QUANTITY_EXCEEDS_MODBUS_LIMIT", "Modbus PDU limits read to 125 registers.", request.RegisterArea, request.Endianness);
        if (!Enum.IsDefined(typeof(TcRegisterArea), request.RegisterArea))
            return TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, request.Generation, "INVALID_REGISTER_AREA", $"RegisterArea {request.RegisterArea} is not a defined candidate.", null, request.Endianness);
        if (request.Endianness != SelectedEndiannessCandidate)
            return TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, request.Generation, "ENDIANNESS_MISMATCH", $"Endianness {request.Endianness} does not match selected {SelectedEndiannessCandidate} (CANDIDATE_UNVERIFIED).", request.RegisterArea, request.Endianness);
        ThrowIfDisposed();

        // One outstanding read per client: fail fast if another read is in progress.
        if (!_singleRead.Wait(0))
            return TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, request.Generation, "READ_BUSY", "One outstanding read per client; concurrent reads are not allowed.", request.RegisterArea, request.Endianness);

        try
        {
            int currentGen;
            FluentModbus.ModbusTcpClient? client;
            lock (_gate)
            {
                currentGen = _generation;
                client = _client;
            }

            if (request.Generation != currentGen)
                return TcReadResult.Stale(request.Endpoint, request.Generation, currentGen, request.RegisterArea, request.Endianness);
            if (client is null || !client.IsConnected)
                return TcReadResult.Failure(TcReadKind.TransportClosed, request.Endpoint, request.Generation, "NOT_CONNECTED", "No connected transport.", request.RegisterArea, request.Endianness);
            if (cancellationToken.IsCancellationRequested)
                return TcReadResult.CancelledResult(request.Endpoint, request.Generation, request.RegisterArea, request.Endianness);

            // Per-call timeout via linked CTS, not via mutating shared ReadTimeout/WriteTimeout.
            // This avoids concurrent mutation races and disposed-client access in finally.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (request.Timeout != TimeSpan.Zero)
            {
                var ms = (int)request.Timeout.TotalMilliseconds;
                if (ms > 0 && ms < int.MaxValue)
                    timeoutCts.CancelAfter(request.Timeout);
                else if (ms <= 0)
                    return TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, request.Generation, "INVALID_TIMEOUT", "Timeout must be positive.", request.RegisterArea, request.Endianness);
            }
            var linkedToken = timeoutCts.Token;

            ushort[] words;
            try
            {
                // Read-only: FC04 or FC03 only, selected explicitly with no fallback.
                // Quantity and start address are CANDIDATE_UNVERIFIED (0 and 40 for the 5×8 model).
                // The returned Memory is transport-owned and may be reused; we copy to application-owned array.
                if (request.RegisterArea == TcRegisterArea.InputRegisters)
                {
                    var span = await client.ReadInputRegistersAsync<ushort>((int)request.Endpoint.UnitIdentifier, request.StartAddress, request.Quantity, linkedToken).ConfigureAwait(false);
                    words = span.ToArray();
                }
                else // HoldingRegisters
                {
                    var span = await client.ReadHoldingRegistersAsync<ushort>((int)request.Endpoint.UnitIdentifier, request.StartAddress, request.Quantity, linkedToken).ConfigureAwait(false);
                    words = span.ToArray();
                }

                // Stale fence after await: if Disconnect/Dispose/reconnect happened while we awaited, reject.
                lock (_gate) { currentGen = _generation; }
                if (request.Generation != currentGen)
                    return TcReadResult.Stale(request.Endpoint, request.Generation, currentGen, request.RegisterArea, request.Endianness);

                return TcReadResult.SuccessResult(request.Endpoint, request.Generation, words, DateTimeOffset.UtcNow, request.RegisterArea, request.Endianness);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return TcReadResult.CancelledResult(request.Endpoint, request.Generation, request.RegisterArea, request.Endianness);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return TcReadResult.Failure(TcReadKind.Timeout, request.Endpoint, request.Generation, "TIMEOUT", "Per-call timeout elapsed.", request.RegisterArea, request.Endianness);
            }
            catch (TimeoutException ex)
            {
                return TcReadResult.Failure(TcReadKind.Timeout, request.Endpoint, request.Generation, "TIMEOUT", ex.Message, request.RegisterArea, request.Endianness);
            }
            catch (FluentModbus.ModbusException ex)
            {
                return TcReadResult.Failure(TcReadKind.ProtocolException, request.Endpoint, request.Generation, "MODBUS_EXCEPTION", ex.Message, request.RegisterArea, request.Endianness);
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException || ex is System.IO.IOException)
            {
                var kind = ex is System.Net.Sockets.SocketException ? TcReadKind.ConnectionFailure : TcReadKind.TransportClosed;
                return TcReadResult.Failure(kind, request.Endpoint, request.Generation, ex.GetType().Name.ToUpperInvariant(), ex.Message, request.RegisterArea, request.Endianness);
            }
            catch (Exception ex)
            {
                return TcReadResult.Failure(TcReadKind.MalformedResponse, request.Endpoint, request.Generation, "UNEXPECTED_RESPONSE", ex.Message, request.RegisterArea, request.Endianness);
            }
        }
        finally
        {
            _singleRead.Release();
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
        _singleRead.Dispose();
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
