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
/// Read-only register block request. Only read function codes are ever issued (FC03 Read Holding
/// Registers or FC04 Read Input Registers as a test parameter, both CANDIDATE_UNVERIFIED). Quantity 40
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

    /// <summary>Per-call timeout; 0 means use client default.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(2000);

    /// <summary>True = FC04 Read Input Registers, false = FC03 Read Holding Registers. Both CANDIDATE_UNVERIFIED.</summary>
    public bool UseInputRegisters { get; init; } = false;
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

    public static TcReadResult SuccessResult(TcNodeEndpoint endpoint, int generation, ushort[] words, DateTimeOffset receivedAt) =>
        new() { Kind = TcReadKind.Success, Endpoint = endpoint, Generation = generation, RawWords = words, ReceivedAtUtc = receivedAt };

    public static TcReadResult Failure(TcReadKind kind, TcNodeEndpoint endpoint, int generation, string code, string detail) =>
        new() { Kind = kind, Endpoint = endpoint, Generation = generation, DetailCode = code, Detail = detail, ReceivedAtUtc = DateTimeOffset.UtcNow };

    public static TcReadResult CancelledResult(TcNodeEndpoint endpoint, int generation) =>
        Failure(TcReadKind.Cancelled, endpoint, generation, "CANCELLED", "The read was cancelled.");

    public static TcReadResult Stale(TcNodeEndpoint endpoint, int generation, int currentGeneration) =>
        Failure(TcReadKind.StaleGenerationRejected, endpoint, generation, "STALE_GENERATION_REJECTED",
            $"Result generation {generation} is stale; current generation is {currentGeneration}.");
}
