using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Wjss.Adapters.TcAcquisition;

namespace Wjss.TcBench;

public enum WireEvidenceOutcome { NOT_PROVIDED, REVIEW_PENDING, MATCHED, MISMATCHED, MALFORMED, INCOMPLETE }
public enum TrialAcceptance { PENDING_OWNER_REVIEW, QUARANTINED, ACCEPTED, REJECTED }

public sealed record AdapterOutcome
{
    [JsonRequired]
    public string State { get; init; } = "NOT_ISSUED";
    [JsonRequired]
    public TcReadKind? Kind { get; init; }
    [JsonRequired]
    public string DetailCode { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string ExceptionType { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public DateTimeOffset? StartedAt { get; init; }
    [JsonRequired]
    public DateTimeOffset? CompletedAt { get; init; }
    [JsonRequired]
    public double? ElapsedMilliseconds { get; init; }
    [JsonRequired]
    public int? Generation { get; init; }

    public static AdapterOutcome From(TcReadResult result, DateTimeOffset start, double elapsed) => new()
    {
        State = "OBSERVED", Kind = result.Kind, DetailCode = result.DetailCode ?? "NOT_PROVIDED",
        ExceptionType = result.ExceptionType ?? "NOT_PROVIDED", StartedAt = start,
        CompletedAt = DateTimeOffset.UtcNow, ElapsedMilliseconds = elapsed, Generation = result.Generation,
    };
}

public sealed record IndexedWord([property: JsonRequired] int Index, [property: JsonRequired] ushort Value)
{
    [JsonIgnore]
    public string Hex => $"0x{Value:X4}";
}

public sealed record ExternalArtifact([property: JsonRequired] string Id, [property: JsonRequired] long Bytes, [property: JsonRequired] string Sha256);

/// <summary>Offline review observations only, never inferred from copied word count. No capture is implemented here.</summary>
public sealed record WireReview
{
    [JsonRequired]
    public string TrialId { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string SessionId { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string Endpoint { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public int? Port { get; init; }
    [JsonRequired]
    public DateTimeOffset? WindowStart { get; init; }
    [JsonRequired]
    public DateTimeOffset? WindowEnd { get; init; }
    [JsonRequired]
    public DateTimeOffset? RequestAt { get; init; }
    [JsonRequired]
    public DateTimeOffset? ResponseAt { get; init; }
    [JsonRequired]
    public ushort? RequestTransactionId { get; init; }
    [JsonRequired]
    public ushort? ResponseTransactionId { get; init; }
    [JsonRequired]
    public byte? RequestUnitId { get; init; }
    [JsonRequired]
    public byte? ResponseUnitId { get; init; }
    [JsonRequired]
    public int? RequestFc { get; init; }
    [JsonRequired]
    public int? ResponseFc { get; init; }
    [JsonRequired]
    public ushort? RequestedStart { get; init; }
    [JsonRequired]
    public ushort? RequestedQuantity { get; init; }
    [JsonRequired]
    public int? RequestAduLength { get; init; }
    [JsonRequired]
    public int? MbapLength { get; init; }
    [JsonRequired]
    public int? ByteCountField { get; init; }
    [JsonRequired]
    public int? PduLength { get; init; }
    [JsonRequired]
    public int? AduLength { get; init; }
    [JsonRequired]
    public int? DataLength { get; init; }
    [JsonRequired]
    public string CaptureCompleteness { get; init; } = "NOT_PROVIDED";
    // Digest of indexed words independently decoded from captured bytes, same documented encoding.
    [JsonRequired]
    public string IndexedWordsSha256 { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public ExternalArtifact[] Artifacts { get; init; } = [];
    [JsonRequired]
    public WireEvidenceOutcome Outcome { get; init; } = WireEvidenceOutcome.NOT_PROVIDED;
}

public sealed record OwnerReview
{
    public required string TrialId { get; init; }
    public required string SessionId { get; init; }
    public required string AcquisitionManifestSha256 { get; init; }
    public required string OwnerId { get; init; }
    public required string Reference { get; init; }
    public required DateTimeOffset ReviewedAt { get; init; }
    public required TrialAcceptance Decision { get; init; }
    public required bool ApprovedWordExcerpt { get; init; }
}

/// <summary>
/// Typed diagnostic record for one bench invocation.
///
/// Identity is carried from the build, not from a branch name and not from the authorization
/// artifact: ApprovedBaseSha, ImplementationSha, SourceRef, DeploymentBuildIdentity and
/// PhysicalCapabilityEnabled all come from the immutable build identity. Authorization state,
/// lifecycle-budget state, cleanup state and worker-observation state are recorded separately, so a
/// requested cancellation, an elapsed timeout, an overdue cleanup, an overdue worker observation, an
/// observed completion and an unproven cleanup can never be confused with one another.
///
/// Deterministic dry-run and synthetic records keep explicit NOT_PROVIDED / NOT_ISSUED /
/// NOT_OBSERVED values: no unavailable physical fact is ever fabricated as a zero.
/// </summary>
public sealed record TrialEvidence
{
    [JsonRequired]
    public string SchemaVersion { get; init; } = "wjss.tc-bench-trial/2";
    [JsonRequired]
    public string ApprovedBaseSha { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string ImplementationSha { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string SourceRef { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string DeploymentBuildIdentity { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public bool PhysicalCapabilityEnabled { get; init; }
    [JsonRequired]
    public string CleanStateProof { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string TrialId { get; init; } = "T01";
    [JsonRequired]
    public string AuthorizationId { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string AuthorizationReference { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string AuthorizationPayloadSha256 { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string AuthorizationKeyId { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string AuthorizationVerification { get; init; } = "NOT_ATTEMPTED";
    [JsonRequired]
    public string AuthorizationConsumption { get; init; } = "NOT_ATTEMPTED";
    [JsonRequired]
    public string OperatorId { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    [JsonRequired]
    public string Timezone { get; init; } = "UTC";
    [JsonRequired]
    public string EnvironmentSummary { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string Provenance { get; init; } = "SIMULATION";
    [JsonRequired]
    public string NodeId { get; init; } = "TC-NODE-01";
    [JsonRequired]
    public string CabinetId { get; init; } = "CAB1";
    [JsonRequired]
    public string Endpoint { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public int? Port { get; init; }
    [JsonRequired]
    public string SourceSha256 { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string ConfigurationSha256 { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public string TrialManifestSha256 { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public int FunctionCode { get; init; } = 3;
    [JsonRequired]
    public byte UnitId { get; init; }
    [JsonRequired]
    public ushort Start { get; init; }
    [JsonRequired]
    public ushort Quantity { get; init; } = 40;
    [JsonRequired]
    public string Endianness { get; init; } = "BigEndian";
    [JsonRequired]
    public string ConnectionSettingBasis { get; init; } = "OWNER_AUTHORIZED_BENCH_SETTING";
    [JsonRequired]
    public string PhysicalVerification { get; init; } = "NOT_VERIFIED";
    [JsonRequired]
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
    [JsonRequired]
    public long AcquisitionEpoch { get; init; }
    [JsonRequired]
    public long? AcquisitionEpochAfter { get; init; }
    [JsonRequired]
    public int? GenerationBefore { get; init; }
    [JsonRequired]
    public int? GenerationBeforeCleanup { get; init; }
    [JsonRequired]
    public int? GenerationAfterCleanup { get; init; }
    [JsonRequired]
    public AdapterOutcome Connect { get; init; } = new();
    [JsonRequired]
    public AdapterOutcome Read { get; init; } = new();
    [JsonRequired]
    public WireReview WireEvidence { get; init; } = new();
    [JsonRequired]
    public TrialAcceptance TrialAcceptance { get; init; } = TrialAcceptance.QUARANTINED;
    [JsonRequired]
    public int? CopiedWordCount { get; init; }
    [JsonRequired]
    public IndexedWord[] Words { get; init; } = [];
    [JsonRequired]
    public string CancellationState { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string TimeoutState { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string LifecycleBudgetState { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string StopState { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string StaleDecision { get; init; } = "NOT_ISSUED";
    [JsonRequired]
    public string FinalHandoffDecision { get; init; } = "NOT_ISSUED";
    [JsonRequired]
    public string TransportBefore { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string TransportBeforeCleanup { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string TransportAfter { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string DisconnectResult { get; init; } = "NOT_ISSUED";
    [JsonRequired]
    public string DisposalResult { get; init; } = "NOT_ISSUED";
    [JsonRequired]
    public string TaskCompletion { get; init; } = "NOT_ISSUED";
    [JsonRequired]
    public string CleanupState { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string WorkerObservationState { get; init; } = "NOT_OBSERVED";
    [JsonRequired]
    public string LifecycleFailureType { get; init; } = "NOT_PROVIDED";
    [JsonRequired]
    public bool NoWrite { get; init; } = true;
    [JsonRequired]
    public bool NoFallback { get; init; } = true;
    [JsonRequired]
    public OwnerReview? OwnerReview { get; init; }
    [JsonRequired]
    public string[] Limitations { get; init; } = ["RAW_WORDS_NOT_TEMPERATURES", "FRAMING_NOT_PROVEN_BY_ADAPTER", "PHYSICAL_EXECUTION_NOT_AUTHORIZED"];
    [JsonRequired]
    public string[] Changed { get; init; } = ["DIAGNOSTIC_EVIDENCE_ONLY"];
    [JsonRequired]
    public string[] Unchanged { get; init; } = ["RUNTIME", "KIOSK", "TOPOLOGY", "CONTROL_AUTHORITY"];
    [JsonRequired]
    public string[] NotVerified { get; init; } = ["PHYSICAL_REACHABILITY", "IMAGE_MAPPING", "SCALING", "WIRE_BREAK", "FIELD_TIMING"];

    public static TrialEvidence Create(TrialCatalog catalog, TrialConfiguration trial, string provenance, long epoch) => new()
    {
        TrialId = trial.TrialId, NodeId = trial.NodeId, CabinetId = trial.CabinetId, Endpoint = trial.Address,
        Port = trial.Port, SourceSha256 = catalog.SourceSha256, ConfigurationSha256 = catalog.ConfigurationSha256,
        TrialManifestSha256 = catalog.Select(trial.TrialId).ManifestSha256, Provenance = provenance, AcquisitionEpoch = epoch,
    };

    public static string Serialize(TrialEvidence evidence) => JsonSerializer.Serialize(evidence, StrictJson.Options);
    public static string WordDigest(IndexedWord[] words) => TrialCatalog.Hash(JsonSerializer.Serialize(words, StrictJson.Options));

    public static void Validate(TrialEvidence evidence, TrialCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(catalog);
        var entry = catalog.Select(evidence.TrialId);
        var t = entry.Trial;
        if (evidence.SchemaVersion != "wjss.tc-bench-trial/2"
            || !StrictJson.IsCommit(evidence.ApprovedBaseSha)
            || (!StrictJson.IsCommit(evidence.ImplementationSha) && !string.Equals(evidence.ImplementationSha, BuildIdentity.NotProvided, StringComparison.Ordinal))
            || !SourceRefPolicy.IsValid(evidence.SourceRef)
            || !StrictJson.IsHash(evidence.DeploymentBuildIdentity)
            || evidence.NodeId != t.NodeId || evidence.CabinetId != t.CabinetId
            || evidence.FunctionCode != t.FunctionCode || evidence.UnitId != t.UnitId || evidence.Start != t.Start || evidence.Quantity != 40
            || evidence.Endianness != t.Endianness || evidence.PhysicalVerification != "NOT_VERIFIED"
            || evidence.ConnectionSettingBasis != t.ConnectionSettingBasis || evidence.TrialManifestSha256 != entry.ManifestSha256
            || evidence.SourceSha256 != catalog.SourceSha256 || evidence.ConfigurationSha256 != catalog.ConfigurationSha256
            || !evidence.NoWrite || !evidence.NoFallback) throw new InvalidDataException("EVIDENCE_POLICY_MISMATCH");
        if (evidence.Provenance == "SYNTHETIC_TEST_ONLY")
        {
            if (evidence.Endpoint != "127.0.0.1" || evidence.Port is null or < 1 or > 65535 or 502) throw new InvalidDataException("SYNTHETIC_ENDPOINT_REJECTED");
        }
        else if (evidence.Provenance is not ("SIMULATION" or "PHYSICAL_DIAGNOSTIC") || evidence.Endpoint != t.Address || evidence.Port != t.Port) throw new InvalidDataException("EVIDENCE_ENDPOINT_REJECTED");

        if (evidence.Provenance == "PHYSICAL_DIAGNOSTIC")
        {
            // A physical record must be internally complete: build-derived identity, verified and
            // consumed Owner authorization, and no placeholder where a physical fact is required.
            if (!evidence.PhysicalCapabilityEnabled
                || !string.Equals(evidence.CleanStateProof, BuildIdentity.DeploymentProofClean, StringComparison.Ordinal)
                || !StrictJson.IsCommit(evidence.ImplementationSha)
                || !SourceRefPolicy.IsValid(evidence.SourceRef) || string.Equals(evidence.SourceRef, BuildIdentity.NotProvided, StringComparison.Ordinal)
                || !SafeIdentifier(evidence.AuthorizationId) || string.Equals(evidence.AuthorizationId, PhysicalTrialStates.NotProvided, StringComparison.Ordinal)
                || !SafeIdentifier(evidence.AuthorizationKeyId) || string.Equals(evidence.AuthorizationKeyId, PhysicalTrialStates.NotProvided, StringComparison.Ordinal)
                || !StrictJson.IsHash(evidence.AuthorizationPayloadSha256)
                || !string.Equals(evidence.AuthorizationVerification, PhysicalTrialStates.Verified, StringComparison.Ordinal)
                || !string.Equals(evidence.AuthorizationConsumption, PhysicalTrialStates.Consumed, StringComparison.Ordinal))
                throw new InvalidDataException("PHYSICAL_EVIDENCE_POLICY_MISMATCH");
        }
        else if (!string.Equals(evidence.AuthorizationVerification, PhysicalTrialStates.NotAttempted, StringComparison.Ordinal)
            || !string.Equals(evidence.AuthorizationConsumption, PhysicalTrialStates.NotAttempted, StringComparison.Ordinal)
            || !string.Equals(evidence.AuthorizationId, PhysicalTrialStates.NotProvided, StringComparison.Ordinal)
            || !string.Equals(evidence.AuthorizationPayloadSha256, PhysicalTrialStates.NotProvided, StringComparison.Ordinal)
            || !string.Equals(evidence.AuthorizationKeyId, PhysicalTrialStates.NotProvided, StringComparison.Ordinal))
        {
            // Simulation and synthetic records must never claim an authorization was used.
            throw new InvalidDataException("AUTHORIZATION_STATE_REJECTED");
        }

        if (evidence.Words.Length != 0 && (evidence.Words.Length != 40 || evidence.CopiedWordCount != 40
            || evidence.Read.Kind != TcReadKind.Success || evidence.FinalHandoffDecision != "RETAINED_QUARANTINED"
            || evidence.StaleDecision != "CURRENT_AT_DIAGNOSTIC_HANDOFF" || evidence.AcquisitionEpoch != evidence.AcquisitionEpochAfter
            || evidence.Read.Generation != evidence.GenerationBeforeCleanup || evidence.CancellationState != "NOT_REQUESTED"
            || evidence.DisconnectResult != "COMPLETE" || evidence.DisposalResult != "COMPLETE" || evidence.TaskCompletion != "COMPLETE"
            || evidence.TransportAfter != "DISCONNECTED" || evidence.Words.Where((word, i) => word.Index != i).Any()
            || !string.Equals(evidence.LifecycleBudgetState, PhysicalTrialStates.NotElapsed, StringComparison.Ordinal)
            || !string.Equals(evidence.CleanupState, PhysicalTrialStates.Observed, StringComparison.Ordinal)
            || !string.Equals(evidence.WorkerObservationState, PhysicalTrialStates.Observed, StringComparison.Ordinal))) throw new InvalidDataException("INDEXED_WORDS_REJECTED");
        if (!Enum.IsDefined(evidence.TrialAcceptance) || !Enum.IsDefined(evidence.WireEvidence.Outcome)) throw new InvalidDataException("UNKNOWN_RESULT_LAYER");
        foreach (var artifact in evidence.WireEvidence.Artifacts)
            if (!SafeIdentifier(artifact.Id) || artifact.Bytes <= 0 || !StrictJson.IsHash(artifact.Sha256)) throw new InvalidDataException("ARTIFACT_REJECTED");
        ValidateOutcome(evidence.Connect);
        ValidateOutcome(evidence.Read);
        if (evidence.WireEvidence.Outcome == WireEvidenceOutcome.MATCHED) ValidateWire(evidence, evidence.WireEvidence);
        if (evidence.TrialAcceptance == TrialAcceptance.ACCEPTED)
        {
            if (evidence.OwnerReview is null) throw new InvalidDataException("OWNER_REVIEW_REQUIRED");
            var original = evidence with { WireEvidence = new(), OwnerReview = null, TrialAcceptance = TrialAcceptance.QUARANTINED };
            if (evidence.OwnerReview.AcquisitionManifestSha256 != TrialCatalog.Hash(Serialize(original))) throw new InvalidDataException("ACCEPTED_RECORD_HASH_MISMATCH");
            RequireAcceptable(evidence);
            ValidateOwner(evidence, evidence.OwnerReview);
        }
    }

    private static void ValidateOutcome(AdapterOutcome outcome)
    {
        if (!SafeIdentifier(outcome.DetailCode) || !SafeIdentifier(outcome.ExceptionType)) throw new InvalidDataException("RAW_DETAIL_REFUSED");
        if (outcome.State == "NOT_ISSUED")
        {
            if (outcome.Kind is not null || outcome.StartedAt is not null || outcome.CompletedAt is not null || outcome.ElapsedMilliseconds is not null || outcome.Generation is not null) throw new InvalidDataException("UNISSUED_OUTCOME_HAS_DATA");
        }
        else if ((outcome.State != "OBSERVED" && outcome.State != "THREW")
            || (outcome.State == "OBSERVED" && (outcome.Kind is null || !Enum.IsDefined(outcome.Kind.Value)))
            || (outcome.State == "THREW" && (outcome.Kind is not null || outcome.ExceptionType == "NOT_PROVIDED" || outcome.DetailCode != "OPERATION_THROWN"))
            || outcome.StartedAt is null || outcome.CompletedAt < outcome.StartedAt || outcome.CompletedAt is null
            || outcome.ElapsedMilliseconds is null || !double.IsFinite(outcome.ElapsedMilliseconds.Value) || outcome.ElapsedMilliseconds < 0
            || (outcome.State == "OBSERVED" && outcome.Generation is null)) throw new InvalidDataException("OUTCOME_INCOMPLETE");
    }

    /// <summary>
    /// Wire review validation. K6 implements no capture: these values can only be supplied offline
    /// from an independently produced capture. A TCP packet size is never an ADU size.
    /// </summary>
    public static void ValidateWire(TrialEvidence evidence, WireReview wire)
    {
        if (wire.Outcome != WireEvidenceOutcome.MATCHED) return;
        if (wire.TrialId != evidence.TrialId || wire.SessionId != evidence.SessionId || wire.Endpoint != evidence.Endpoint || wire.Port != evidence.Port
            || wire.CaptureCompleteness != "COMPLETE" || wire.RequestTransactionId is null || wire.RequestTransactionId != wire.ResponseTransactionId
            || wire.RequestUnitId != evidence.UnitId || wire.ResponseUnitId != evidence.UnitId
            || wire.RequestFc != evidence.FunctionCode || wire.ResponseFc != evidence.FunctionCode
            || wire.RequestedStart != evidence.Start || wire.RequestedQuantity != 40 || wire.RequestAduLength != 12
            || wire.MbapLength != 83 || wire.ByteCountField != 80 || wire.PduLength != 82 || wire.AduLength != 89 || wire.DataLength != 80
            || wire.WindowStart is null || wire.WindowEnd is null || wire.RequestAt is null || wire.ResponseAt is null
            || evidence.Read.StartedAt is null || evidence.Read.CompletedAt is null
            || wire.RequestAt < wire.WindowStart || wire.ResponseAt > wire.WindowEnd || wire.RequestAt > wire.ResponseAt
            || wire.RequestAt < evidence.Read.StartedAt || wire.ResponseAt > evidence.Read.CompletedAt
            || wire.IndexedWordsSha256 != WordDigest(evidence.Words) || wire.Artifacts.Length == 0) throw new InvalidDataException("WIRE_REVIEW_MISMATCH");
        foreach (var artifact in wire.Artifacts)
            if (!SafeIdentifier(artifact.Id) || artifact.Bytes <= 0 || !StrictJson.IsHash(artifact.Sha256)) throw new InvalidDataException("ARTIFACT_REJECTED");
    }

    private static void RequireAcceptable(TrialEvidence evidence)
    {
        if (evidence.Provenance != "PHYSICAL_DIAGNOSTIC" || evidence.Connect.Kind != TcReadKind.Success || evidence.Read.Kind != TcReadKind.Success
            || evidence.Words.Length != 40 || evidence.WireEvidence.Outcome != WireEvidenceOutcome.MATCHED
            || evidence.CleanStateProof != "CLEAN_OWNER_VERIFIED" || !StrictJson.IsCommit(evidence.ImplementationSha)
            || !SafeIdentifier(evidence.AuthorizationReference) || evidence.AuthorizationReference == "NOT_PROVIDED"
            || !SafeIdentifier(evidence.AuthorizationId) || evidence.AuthorizationId == "NOT_PROVIDED"
            || !SafeIdentifier(evidence.AuthorizationKeyId) || evidence.AuthorizationKeyId == "NOT_PROVIDED"
            || !StrictJson.IsHash(evidence.AuthorizationPayloadSha256)
            || evidence.AuthorizationVerification != "VERIFIED" || evidence.AuthorizationConsumption != "CONSUMED"
            || !SafeIdentifier(evidence.OperatorId) || evidence.OperatorId == "NOT_PROVIDED"
            || !SafeIdentifier(evidence.EnvironmentSummary) || evidence.EnvironmentSummary == "NOT_PROVIDED"
            || evidence.StopState != "NOT_REQUESTED" || evidence.TimeoutState != "NOT_ELAPSED"
            || evidence.LifecycleBudgetState != "NOT_ELAPSED" || evidence.CleanupState != "OBSERVED" || evidence.WorkerObservationState != "OBSERVED"
            || evidence.CopiedWordCount != 40 || evidence.GenerationAfterCleanup is null || evidence.GenerationAfterCleanup <= evidence.GenerationBeforeCleanup) throw new InvalidDataException("TRIAL_NOT_ACCEPTABLE");
    }

    private static void ValidateOwner(TrialEvidence evidence, OwnerReview owner)
    {
        if (owner.TrialId != evidence.TrialId || owner.SessionId != evidence.SessionId
            || !SafeIdentifier(owner.OwnerId) || owner.OwnerId == "NOT_PROVIDED" || !SafeIdentifier(owner.Reference)
            || owner.ReviewedAt < evidence.CreatedAt || owner.ReviewedAt < evidence.Read.CompletedAt || owner.Decision != evidence.TrialAcceptance
            || !StrictJson.IsHash(owner.AcquisitionManifestSha256)) throw new InvalidDataException("OWNER_REVIEW_MISMATCH");
    }

    // Offline API only: caller supplies original bytes and an explicit independent Owner decision.
    // This is not called by acquisition or Program. Original evidence is never overwritten.
    public static TrialEvidence ApplyOfflineReview(string originalBytes, TrialCatalog catalog, WireReview wire, OwnerReview owner)
    {
        var evidence = StrictJson.Read<TrialEvidence>(originalBytes);
        Validate(evidence, catalog);
        if (originalBytes != Serialize(evidence) || evidence.WireEvidence.Outcome != WireEvidenceOutcome.NOT_PROVIDED
            || evidence.OwnerReview is not null || evidence.TrialAcceptance == TrialAcceptance.ACCEPTED
            || owner.AcquisitionManifestSha256 != TrialCatalog.Hash(originalBytes)) throw new InvalidDataException("ORIGINAL_REVIEW_HASH_MISMATCH");
        var reviewed = evidence with { WireEvidence = wire, OwnerReview = owner, TrialAcceptance = owner.Decision };
        ValidateOwner(reviewed, owner);
        Validate(reviewed, catalog);
        return reviewed;
    }

    public static bool SafeIdentifier(string value) => Regex.IsMatch(value, "\\A[A-Za-z0-9_-]{1,80}\\z", RegexOptions.CultureInvariant);

    // Exact typed allowlist plus lexical rejection. Arbitrary prose/binary/path fields are absent.
    public static string ExportPublic(string bytes, TrialCatalog catalog, string independentlyReviewedSha256)
    {
        if (!StrictJson.IsHash(independentlyReviewedSha256) || TrialCatalog.Hash(bytes) != independentlyReviewedSha256) throw new InvalidDataException("PUBLIC_EXPORT_REVIEW_HASH_REQUIRED");
        var evidence = StrictJson.Read<TrialEvidence>(bytes);
        Validate(evidence, catalog);
        using var document = JsonDocument.Parse(bytes);
        RejectUnsafeStrings(document.RootElement);
        if (evidence.Words.Length != 0 && evidence.OwnerReview?.ApprovedWordExcerpt != true) throw new InvalidDataException("WORD_EXCERPT_REVIEW_REQUIRED");
        // Do not redact/drop/change any supplied fact; rejection leaves originals untouched.
        return bytes;
    }

    private static void RejectUnsafeStrings(JsonElement element, string? field = null)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()!;
            if (field == "sourceRef" && SourceRefPolicy.IsValid(value)) return;
            if (field == "schemaVersion" && value == "wjss.tc-bench-trial/2") return;
            var forbidden = "pass" + "word|credential|secret|token|connection" + "string|Bearer|BEGIN.*KEY";
            if (value.Any(char.IsControl) || value.Contains('/') || value.Contains('\\') || (value.Contains(':') || value.Contains('=')) && !DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out _)
                || Regex.IsMatch(value, forbidden, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                || value.Any(c => !char.IsAscii(c)) || value.Length > 160) throw new InvalidDataException("UNSAFE_PUBLIC_TEXT");
            // Only typed timestamps, hashes, neutral identifiers, a validated source reference, hex words, and IPv4 text.
            if (!SafeIdentifier(value) && !System.Net.IPAddress.TryParse(value, out _) && !DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out _)) throw new InvalidDataException("UNREVIEWED_FREE_TEXT");
        }
        else if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject()) RejectUnsafeStrings(property.Value, property.Name);
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) RejectUnsafeStrings(item);
    }
}
