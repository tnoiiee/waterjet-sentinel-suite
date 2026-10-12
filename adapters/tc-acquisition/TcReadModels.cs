namespace Wjss.Adapters.TcAcquisition;

/// <summary>
/// Read-only TC node endpoint. The IP address must be one of the six approved TEST_CONFIGURATION
/// addresses (TEST-NET-1 documentation range, as defined in tcNodeConfiguration.mjs) and the port
/// is the Owner-confirmed factory setting 502 (CANDIDATE_UNVERIFIED until physical evidence
/// establishes the as-installed value). The Unit
/// Identifier default 0x00/0xFF is a Modbus TCP convention (directly connected device) and remains
/// CANDIDATE_UNVERIFIED — the actual Unit ID behavior is NOT VERIFIED.
/// </summary>
public sealed record TcNodeEndpoint
{
    public required string NodeId { get; init; }
    public required string IpAddress { get; init; }

    /// <summary>Owner-confirmed factory setting 502; CANDIDATE_UNVERIFIED until physical evidence.</summary>
    public int Port { get; init; } = 502;

    /// <summary>Unit identifier. 0x00/0xFF are defaults for directly connected TCP devices; CANDIDATE_UNVERIFIED.</summary>
    public byte UnitIdentifier { get; init; } = 0x00;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(NodeId)) throw new ArgumentException("NodeId is required.", nameof(NodeId));
        if (string.IsNullOrWhiteSpace(IpAddress)) throw new ArgumentException("IpAddress is required.", nameof(IpAddress));
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port), Port, "Port must be 1..65535.");
    }
}

/// <summary>
/// Explicit register-area / function-code candidate for a read-only acquisition.
/// No automatic fallback: the requested area is the only function code attempted.
/// Both values remain CANDIDATE_UNVERIFIED until one-Node physical evidence establishes
/// whether the field device exposes the 40-word image at FC03 or FC04, at which start address.
/// The numeric values correspond to the Modbus function codes (3 and 4) for traceability,
/// but the candidate state is not verified.
/// </summary>
public enum TcRegisterArea
{
    /// <summary>FC03 Read Holding Registers — CANDIDATE_UNVERIFIED.</summary>
    HoldingRegisters = 3,

    /// <summary>FC04 Read Input Registers — CANDIDATE_UNVERIFIED.</summary>
    InputRegisters = 4,
}

/// <summary>
/// Endianness candidate for the Modbus TCP wire. Modbus network byte order is BigEndian
/// (MSB first). FluentModbus exposes a per-connection <c>ModbusEndianness</c> that controls
/// whether the library swaps bytes on little-endian hosts. The selected endianness is
/// explicit (BigEndian) but remains CANDIDATE_UNVERIFIED until bench evidence confirms
/// the field I/O module mapping and the host's SwapBytes behavior. Word ordering for any
/// future multi-register value, scaling, and wire-break decoding remain unverified.
/// </summary>
public enum TcEndiannessCandidate
{
    /// <summary>BigEndian — Modbus network order, CANDIDATE_UNVERIFIED.</summary>
    BigEndian = 0,

    /// <summary>LittleEndian — alternative wire order, CANDIDATE_UNVERIFIED.</summary>
    LittleEndian = 1,
}

/// <summary>
/// Read-only register block request. Only read function codes are ever issued (FC03 or FC04
/// as the explicit <see cref="RegisterArea"/> candidate, both CANDIDATE_UNVERIFIED). Quantity 40
/// at start address 0 is the DERIVED model-level candidate (5 modules × 8 channels) and remains
/// CANDIDATE_UNVERIFIED until one-Node physical evidence is obtained.
/// </summary>
public sealed record TcReadRequest
{
    public required TcNodeEndpoint Endpoint { get; init; }

    /// <summary>Start address, zero-based Modbus addressing. Candidate 0 (CANDIDATE_UNVERIFIED).</summary>
    public required ushort StartAddress { get; init; }

    /// <summary>Number of 16-bit words to read. Candidate 40 (CANDIDATE_UNVERIFIED).</summary>
    public required ushort Quantity { get; init; }

    /// <summary>Connection generation captured at call time for stale-result fencing.</summary>
    public required int Generation { get; init; }

    /// <summary>Per-call timeout; 0 means use client default (2000 ms). Implemented via linked CancellationToken, not via mutable ReadTimeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(2000);

    /// <summary>Explicit register area / function-code candidate. No fallback.</summary>
    public required TcRegisterArea RegisterArea { get; init; }

    /// <summary>Explicit endianness candidate for this read. Currently fixed to BigEndian (network order) as candidate.</summary>
    public TcEndiannessCandidate Endianness { get; init; } = TcEndiannessCandidate.BigEndian;
}

/// <summary>
/// Discriminant for a read attempt. No hardware verification is claimed: Success means the transport
/// returned 16-bit words, not that the process image, scaling or wire-break decoding is verified.
/// </summary>
public enum TcReadKind
{
    Success,
    Cancelled,
    Timeout,
    ConnectionFailure,
    TransportClosed,
    ProtocolException,
    MalformedResponse,
    ConfigurationRejected,
    StaleGenerationRejected,
}

/// <summary>
/// Application-owned result of a read-only register block read. Raw words are unsigned 16-bit as
/// received on the wire; no scaling, signedness, temperature decoding, wire-break or overrange logic
/// is applied here (all remain CANDIDATE_UNVERIFIED / NOT_IMPLEMENTED). Library types are not exposed.
/// The <see cref="RegisterArea"/> echoes the candidate that was actually attempted; no silent fallback occurred.
/// The buffer is application-owned (copied from transport-owned memory) and safe to retain.
/// </summary>
public sealed record TcReadResult
{
    public required TcReadKind Kind { get; init; }
    public required TcNodeEndpoint Endpoint { get; init; }
    public required int Generation { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; init; }
    public ushort[]? RawWords { get; init; }
    public string? DetailCode { get; init; }
    public string? Detail { get; init; }

    /// <summary>Which register area / function-code candidate was attempted, if any (null for Connect or pre-validation failures).</summary>
    public TcRegisterArea? RegisterArea { get; init; }

    /// <summary>Endianness mode that was configured for the attempt (null if not applicable).</summary>
    public TcEndiannessCandidate? Endianness { get; init; }

    public static TcReadResult SuccessResult(TcNodeEndpoint endpoint, int generation, ushort[] words, DateTimeOffset receivedAt, TcRegisterArea area, TcEndiannessCandidate endianness) =>
        new() { Kind = TcReadKind.Success, Endpoint = endpoint, Generation = generation, RawWords = words, ReceivedAtUtc = receivedAt, RegisterArea = area, Endianness = endianness };

    public static TcReadResult Failure(TcReadKind kind, TcNodeEndpoint endpoint, int generation, string code, string detail, TcRegisterArea? area = null, TcEndiannessCandidate? endianness = null) =>
        new() { Kind = kind, Endpoint = endpoint, Generation = generation, DetailCode = code, Detail = detail, ReceivedAtUtc = DateTimeOffset.UtcNow, RegisterArea = area, Endianness = endianness };

    public static TcReadResult CancelledResult(TcNodeEndpoint endpoint, int generation, TcRegisterArea? area = null, TcEndiannessCandidate? endianness = null) =>
        Failure(TcReadKind.Cancelled, endpoint, generation, "CANCELLED", "The read was cancelled.", area, endianness);

    public static TcReadResult Stale(TcNodeEndpoint endpoint, int generation, int currentGeneration, TcRegisterArea? area = null, TcEndiannessCandidate? endianness = null) =>
        Failure(TcReadKind.StaleGenerationRejected, endpoint, generation, "STALE_GENERATION_REJECTED",
            $"Result generation {generation} is stale; current generation is {currentGeneration}.", area, endianness);
}
