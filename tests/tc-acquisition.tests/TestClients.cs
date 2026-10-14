using Wjss.Adapters.TcAcquisition;

namespace Wjss.TcAcquisition.Tests;

internal sealed class TestClient : ITcReadOnlyClient
{
    private int _generation;
    public int Generation
    {
        get { if (Disposed && ThrowObservationAfterDisposal) throw new InvalidOperationException("Synthetic observation failure"); return _generation; }
        private set => _generation = value;
    }
    public bool ThrowObservationAfterDisposal { get; init; }
    public bool IsConnected { get; private set; }
    public TcNodeEndpoint? ConnectedEndpoint { get; private set; }
    public int ConnectCalls { get; private set; }
    public int ReadCalls { get; private set; }
    public int DisconnectCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public bool Disposed { get; private set; }
    /// <summary>The exact request shape issued by the runner; null until a read is attempted.</summary>
    public TcReadRequest? LastRequest { get; private set; }
    public Func<CancellationToken, Task>? ConnectWait { get; init; }
    public Func<CancellationToken, Task>? ReadWait { get; init; }
    /// <summary>Deterministic cleanup delay. Cleanup is always awaited to completion; only its elapsed time can be overdue.</summary>
    public Func<Task>? DisconnectWait { get; init; }
    public Func<Task>? DisposeWait { get; init; }
    public TcReadKind ConnectKind { get; init; } = TcReadKind.Success;
    public TcReadKind ReadKind { get; init; } = TcReadKind.Success;
    public ushort[] Data { get; init; } = Enumerable.Range(0, 40).Select(i => (ushort)(i + 1)).ToArray();
    public bool ChangeGenerationDuringRead { get; init; }
    public bool CleanupFails { get; init; }

    public async Task<TcReadResult> ConnectAsync(TcNodeEndpoint endpoint, CancellationToken cancellationToken)
    {
        ConnectCalls++;
        Generation++;
        if (ConnectWait is not null) await ConnectWait(cancellationToken);
        ConnectedEndpoint = endpoint;
        IsConnected = ConnectKind == TcReadKind.Success;
        return new() { Kind = ConnectKind, Endpoint = endpoint, Generation = Generation, ReceivedAtUtc = DateTimeOffset.UtcNow };
    }

    public async Task<TcReadResult> ReadAsync(TcReadRequest request, CancellationToken cancellationToken)
    {
        ReadCalls++;
        LastRequest = request;
        if (ReadWait is not null) await ReadWait(cancellationToken);
        if (ChangeGenerationDuringRead) Generation++;
        return new() { Kind = ReadKind, Endpoint = request.Endpoint, Generation = request.Generation, RawWords = Data.ToArray(), ReceivedAtUtc = DateTimeOffset.UtcNow };
    }

    public async Task DisconnectAsync()
    {
        DisconnectCalls++;
        Generation++;
        IsConnected = false;
        ConnectedEndpoint = null;
        if (CleanupFails) throw new IOException("Synthetic cleanup failure");
        if (DisconnectWait is not null) await DisconnectWait();
    }

    public async ValueTask DisposeAsync()
    {
        DisposeCalls++;
        Generation++;
        Disposed = true;
        IsConnected = false;
        ConnectedEndpoint = null;
        if (DisposeWait is not null) await DisposeWait();
    }
}
