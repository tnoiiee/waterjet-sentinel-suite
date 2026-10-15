using System.Text.Json.Serialization;

namespace Wjss.Contracts;

/// <summary>
/// Canonical vocabulary for the deterministic one-Node, non-Hardware Product slice.
/// These are presentation identities, not physical addressing or decoded measurements.
/// </summary>
public static class DeterministicTcVocabulary
{
    public const string NodeId = "TC-NODE-01";
    public const string CabinetId = "CAB1";
    public const string Provenance = "DETERMINISTIC_SIMULATION";
    public const string Complete = "COMPLETE";
    public const string Quality = "SIMULATED";
    public const string NoDiagnostic = "NONE";
    public const string MappingNotVerified = "MAPPING_NOT_VERIFIED";
    public const string DecodeNotVerified = "DECODING_NOT_VERIFIED";
    public const int WordCount = 40;

    public static IReadOnlyList<string> NotVerified { get; } = Array.AsReadOnly(new[]
    {
        "PHYSICAL_REACHABILITY", "REGISTER_MAPPING", "SCALING", "WIRE_BREAK", "FIELD_TIMING",
    });

    public static ushort WordAt(int index) => checked((ushort)(100 + index));
}

/// <summary>
/// Canonical vocabulary of the one-Node PHYSICAL read-only Product slice.
///
/// Every value here is CANDIDATE evidence. The read profile is the Owner-supplied shape, and the
/// drawing mapping, scaling, signed representation, byte order, wire-break sentinel meaning,
/// reverse-polarity sentinel meaning and cold-junction compensation all remain NOT VERIFIED until
/// physical evidence is recorded against a separately authorized execution gate. Nothing in this
/// vocabulary is a commissioning, calibration or acceptance claim, and nothing here grants control
/// authority.
///
/// Public-repository boundary: this vocabulary deliberately contains NO network address. The
/// Owner-local physical endpoint is confidential deployment information and is never committed,
/// serialized, logged or displayed; only the redacted logical identity
/// <see cref="EndpointConfigured"/> may cross a boundary.
/// </summary>
public static class PhysicalTcVocabulary
{
    /// <summary>Canonical one-Node logical identity of this slice (not an address).</summary>
    public const string NodeId = "TC-NODE-01";

    /// <summary>Canonical cabinet identity from the committed Drawing rev1 schedule.</summary>
    public const string CabinetId = "CAB1";

    /// <summary>Provenance of a record produced by the physical read-only adapter path.</summary>
    public const string Provenance = "PHYSICAL_HOLDING_REGISTER_READ_ONLY";

    public const string Complete = "COMPLETE";

    /// <summary>Physical evidence is quarantined by construction; there is no other quality value.</summary>
    public const string Quality = "PHYSICAL_QUARANTINED";

    public const string NoDiagnostic = "NONE";

    /// <summary>Mapping is DECLARED from Drawing rev1; physical parity is not established by a read.</summary>
    public const string MappingStatus = "DRAWING_REV1_MAPPING_DECLARED_NOT_VERIFIED";

    /// <summary>Decode is a CANDIDATE rule; no physical evidence establishes it.</summary>
    public const string DecodeStatus = "DECODE_CANDIDATE_NOT_VERIFIED";

    /// <summary>No physical read can commission anything.</summary>
    public const string CommissioningState = "NOT_COMMISSIONED";

    /// <summary>Node-level mapping evidence summary: every assigned row is DRAWING_VERIFIED.</summary>
    public const string MappingEvidenceDrawingVerified = "DRAWING_VERIFIED";

    /// <summary>Node-level mapping evidence summary: at least one assigned row carries a Drawing conflict.</summary>
    public const string MappingEvidenceDrawingInternalConflict = "DRAWING_INTERNAL_CONFLICT";

    /// <summary>Node-level mapping evidence summary: the schedule carries no assigned rows for the Node.</summary>
    public const string MappingEvidenceSpareUnmapped = "SPARE_UNMAPPED";

    /// <summary>
    /// The ONLY endpoint statement any committed contract may carry: a redacted logical identity.
    /// The numeric Owner-local address never appears in a contract, response, log or UI state.
    /// </summary>
    public const string EndpointConfigured = "ENDPOINT_CONFIGURED";

    /// <summary>The redacted statement used when no Owner-local endpoint is configured.</summary>
    public const string EndpointNotConfigured = "ENDPOINT_NOT_CONFIGURED";

    /// <summary>The single authorized read function code: FC03 Read Holding Registers.</summary>
    public const string FunctionCodeHoldingRegisters = "FC03";

    /// <summary>The register area the authorized function code addresses.</summary>
    public const string RegisterAreaHoldingRegisters = "HOLDING_REGISTERS";

    /// <summary>Owner-confirmed factory port; CANDIDATE_UNVERIFIED until physical evidence exists.</summary>
    public const int RegisterPort = 502;

    /// <summary>Candidate Unit Identifier for the directly connected coupler; CANDIDATE_UNVERIFIED.</summary>
    public const int UnitIdentifier = 1;

    /// <summary>Candidate zero-based start address; CANDIDATE_UNVERIFIED.</summary>
    public const int StartAddress = 0;

    /// <summary>Candidate 16-bit register quantity: 5 modules x 8 channels. CANDIDATE_UNVERIFIED.</summary>
    public const int WordCount = 40;

    /// <summary>Assigned measurement offsets of the canonical CAB1 image (0-35).</summary>
    public const int AssignedOffsetCount = 36;

    /// <summary>Spare, unmapped offsets of the canonical CAB1 image (36-39).</summary>
    public const int SpareOffsetCount = 4;

    /// <summary>
    /// The complete, ordered, immutable Not-Verified set of a physical read. A physical record that
    /// drops or reorders an entry is refused before publication.
    /// </summary>
    public static IReadOnlyList<string> NotVerified { get; } = Array.AsReadOnly(new[]
    {
        "PHYSICAL_REACHABILITY", "DRAWING_MAPPING_PARITY", "SCALING", "SIGNED_REPRESENTATION",
        "BYTE_ORDER", "WORD_ORDER", "WIRE_BREAK_SENTINEL_MEANING", "REVERSE_POLARITY_SENTINEL_MEANING",
        "COLD_JUNCTION_COMPENSATION", "FIELD_TIMING", "MBAP_TRANSACTION_ID", "RESPONSE_UNIT_ID",
    });
}

/// <summary>
/// The logical read-profile identity actually used for a physical read-only acquisition.
///
/// It carries no network address: only the function code, register area, port and the unit /
/// start / quantity tuple. This is the profile a retry, a fallback or a silent profile change
/// cannot alter — the Runtime refuses a physical record whose profile differs from the authorized
/// one.
/// </summary>
public sealed record TcReadProfileIdentity
{
    /// <summary>Always <see cref="PhysicalTcVocabulary.FunctionCodeHoldingRegisters"/>.</summary>
    public required string FunctionCode { get; init; }

    /// <summary>Always <see cref="PhysicalTcVocabulary.RegisterAreaHoldingRegisters"/>.</summary>
    public required string RegisterArea { get; init; }

    public required int Port { get; init; }

    public required int UnitIdentifier { get; init; }

    public required int StartAddress { get; init; }

    public required int Quantity { get; init; }
}

/// <summary>
/// The physical read-only evidence block of one quarantined acquisition.
///
/// It answers only logical questions (which profile was used, what the mapping evidence state is,
/// what the commissioning state is, and whether an endpoint was configured). It NEVER carries the
/// endpoint value, and it has no field that could express acceptance, commissioning, validation or
/// approval.
/// </summary>
public sealed record TcPhysicalReadOnlyEvidence
{
    /// <summary>
    /// Redacted logical identity only: <see cref="PhysicalTcVocabulary.EndpointConfigured"/> means
    /// the Owner-local endpoint was resolved and used; the address itself is never recorded here.
    /// </summary>
    public required string EndpointIdentity { get; init; }

    public required TcReadProfileIdentity ReadProfile { get; init; }

    /// <summary>
    /// Node-level mapping evidence summary derived from the committed Drawing rev1 schedule.
    /// One of the <see cref="PhysicalTcVocabulary"/> MappingEvidence* values.
    /// </summary>
    public required string MappingEvidenceState { get; init; }

    /// <summary>Always <see cref="PhysicalTcVocabulary.CommissioningState"/>: a read commissions nothing.</summary>
    public required string CommissioningState { get; init; }
}

/// <summary>
/// Honest Runtime-facing state for one read-only TC acquisition.
///
/// Two mutually exclusive variants share this envelope so the Snapshot and Delta contracts keep a
/// single <c>tcAcquisition</c> slot:
/// <list type="bullet">
///   <item>the DETERMINISTIC non-Hardware record (<see cref="Physical"/> false, no
///   <see cref="PhysicalEvidence"/> block);</item>
///   <item>the PHYSICAL quarantined record (<see cref="Physical"/> true, with the
///   <see cref="PhysicalEvidence"/> block).</item>
/// </list>
/// The deterministic variant serializes byte-identically to the previously committed contract: the
/// added block is omitted when absent. Neither variant carries a temperature, a mapping claim, a
/// scaling claim, a wire-break claim or a network address.
/// </summary>
public sealed record TcAcquisitionState
{
    public required string NodeId { get; init; }
    public required string CabinetId { get; init; }
    public required string Provenance { get; init; }
    public required bool Physical { get; init; }
    public required bool Quarantined { get; init; }
    public required string AcquisitionState { get; init; }
    public required DateTimeOffset AcquiredAtUtc { get; init; }
    public required int CopiedWordCount { get; init; }
    public required IReadOnlyList<ushort> RawWords { get; init; }
    public required string Quality { get; init; }
    public required string DiagnosticState { get; init; }
    public required string MappingStatus { get; init; }
    public required string DecodeStatus { get; init; }
    public required IReadOnlyList<string> NotVerified { get; init; }

    /// <summary>
    /// Physical read-only evidence block. ABSENT for the deterministic record, so the committed
    /// deterministic serialization and its strict validator are unchanged. Present for a physical
    /// record, where the Runtime requires it to carry the exact authorized read profile.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TcPhysicalReadOnlyEvidence? PhysicalEvidence { get; init; }
}
