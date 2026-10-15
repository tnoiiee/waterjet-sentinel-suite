namespace Wjss.Adapters.TcAcquisition;

/// <summary>
/// One-use, read-only implementation of the existing Product client seam. The endpoint marker
/// is deliberately not an address: no transport, socket, device, retry or reconnect exists here.
/// Register-area and start-address fields are interface-shaped placeholders only and provide no
/// evidence of a physical process image. Time in the result is a synthetic marker; the owning
/// Application session stamps the actual acquisition with its injected clock.
/// </summary>
public sealed class DeterministicTcReadOnlyClient : ITcReadOnlyClient
{
    public const string EndpointMarker = "DETERMINISTIC_ONLY_NOT_AN_ADDRESS";
    public const ushort WordCount = 40;

    private readonly object _gate = new();
    private int _generation;
    private bool _connected;
    private bool _disposed;
    private bool _readAttempted;
    private TcNodeEndpoint? _endpoint;

    public int Generation { get { lock (_gate) return _generation; } }
    public bool IsConnected { get { lock (_gate) return _connected && !_disposed; } }
    public TcNodeEndpoint? ConnectedEndpoint { get { lock (_gate) return _endpoint; } }

    public Task<TcReadResult> ConnectAsync(TcNodeEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        endpoint.Validate();
        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(TcReadResult.CancelledResult(endpoint, _generation));
            if (_disposed || _generation != 0 || endpoint.NodeId != "TC-NODE-01"
                || endpoint.IpAddress != EndpointMarker)
                return Task.FromResult(TcReadResult.Failure(TcReadKind.ConfigurationRejected, endpoint, _generation,
                    "DETERMINISTIC_ENDPOINT_REQUIRED", "This one-use client accepts only its non-address marker."));

            _generation++;
            _connected = true;
            _endpoint = endpoint;
            return Task.FromResult(new TcReadResult
            {
                Kind = TcReadKind.Success,
                Endpoint = endpoint,
                Generation = _generation,
                ReceivedAtUtc = DateTimeOffset.UnixEpoch,
                RawWords = Array.Empty<ushort>(),
                DetailCode = "DETERMINISTIC_ONLY",
            });
        }
    }

    public Task<TcReadResult> ReadAsync(TcReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(TcReadResult.CancelledResult(request.Endpoint, request.Generation,
                    request.RegisterArea, request.Endianness));
            if (request.Generation != _generation)
                return Task.FromResult(TcReadResult.Stale(request.Endpoint, request.Generation, _generation,
                    request.RegisterArea, request.Endianness));
            if (_disposed || !_connected)
                return Task.FromResult(TcReadResult.Failure(TcReadKind.TransportClosed, request.Endpoint, _generation,
                    "DETERMINISTIC_CLOSED", "The deterministic client is closed."));
            if (_readAttempted)
                return Task.FromResult(TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, _generation,
                    "DETERMINISTIC_ALREADY_READ", "The one-use client never retries a read."));
            // Even a malformed first request consumes the one allowed attempt. No corrective retry.
            _readAttempted = true;
            if (request.Endpoint != _endpoint || request.Quantity != WordCount
                || request.StartAddress != 0 || request.RegisterArea != TcRegisterArea.HoldingRegisters
                || request.Endianness != TcEndiannessCandidate.BigEndian)
                return Task.FromResult(TcReadResult.Failure(TcReadKind.ConfigurationRejected, request.Endpoint, _generation,
                    "DETERMINISTIC_REQUEST_REQUIRED", "Exactly one fixed-shape read is supported."));

            var words = new ushort[WordCount];
            for (var index = 0; index < words.Length; index++) words[index] = checked((ushort)(100 + index));
            return Task.FromResult(TcReadResult.SuccessResult(
                request.Endpoint, _generation, words, DateTimeOffset.UnixEpoch,
                request.RegisterArea, request.Endianness));
        }
    }

    public Task DisconnectAsync()
    {
        lock (_gate)
        {
            _generation++;
            _connected = false;
            _endpoint = null;
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return ValueTask.CompletedTask;
            _generation++;
            _disposed = true;
            _connected = false;
            _endpoint = null;
        }
        return ValueTask.CompletedTask;
    }
}
