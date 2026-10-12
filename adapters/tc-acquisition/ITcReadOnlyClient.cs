namespace Wjss.Adapters.TcAcquisition;

/// <summary>
/// Application-owned boundary for future read-only physical TC acquisition.
///
/// Implementations:
/// - connect to one configured TC Node (one connection per instance, single session owner);
/// - read a specified read-only register block and return raw unsigned 16-bit words plus
///   communication metadata;
/// - accept cancellation (read supports per-operation CancellationToken; connect timeout is bounded by
///   ConnectTimeout and is not cooperatively cancellable mid-handshake — see FluentModbusTcClient docs);
/// - expose no third-party library types to Domain, Runtime contracts, Kiosk UI or Mapping contracts;
/// - stop and dispose cleanly;
/// - reject or fence stale results from an earlier connection generation;
/// - enforce one outstanding read per client and explicit register-area selection (no fallback).
///
/// Physical scope: read-only only. No write/coil path (FC05/FC06/FC15/FC16) is ever exposed or
/// called. TEST_HARDWARE and PRODUCTION remain NOT_AUTHORIZED; this interface is disabled by
/// default and requires an explicit future Scope Gate to be used against the six
/// TEST_CONFIGURATION TC Node addresses (as defined in tcNodeConfiguration.mjs, TEST-NET-1
/// documentation range) on the Owner-confirmed port (CANDIDATE_UNVERIFIED).
/// Endianness is explicit (BigEndian network order) but remains CANDIDATE_UNVERIFIED until bench evidence.
/// </summary>
public interface ITcReadOnlyClient : IAsyncDisposable
{
    /// <summary>Current connection generation; incremented on every Connect/Disconnect.</summary>
    int Generation { get; }

    /// <summary>True while a transport is established to the configured endpoint.</summary>
    bool IsConnected { get; }

    /// <summary>The endpoint this instance was created for (or null before Connect).</summary>
    TcNodeEndpoint? ConnectedEndpoint { get; }

    /// <summary>
    /// Establish a transport to the endpoint. Cancellation is honored; timeout is bounded.
    /// Stale generations are not applicable to Connect — it establishes the current generation.
    /// </summary>
    Task<TcReadResult> ConnectAsync(TcNodeEndpoint endpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Read a read-only register block. The implementation must honor cancellation and timeout,
    /// fence stale generations (request.Generation != current Generation → StaleGenerationRejected),
    /// and never issue a write/coil function code.
    /// </summary>
    Task<TcReadResult> ReadAsync(TcReadRequest request, CancellationToken cancellationToken);

    /// <summary>Disconnect and invalidate the current generation so outstanding reads become stale.</summary>
    Task DisconnectAsync();
}
