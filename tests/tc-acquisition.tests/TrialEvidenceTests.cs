using System.Text.Json;
using System.Text.Json.Nodes;
using Wjss.Adapters.TcAcquisition;
using Wjss.TcBench;
using Xunit;

namespace Wjss.TcAcquisition.Tests;

public sealed class TrialEvidenceTests
{
    private static TrialEvidence DryRun() => new TrialRunner().DryRun(TrialCatalog.LoadTrusted(), "T01");

    [Fact]
    public void Complete_DryRun_Represents_Unobserved_Fields_Without_Fabricated_Zeros()
    {
        var c = TrialCatalog.LoadTrusted();
        var e = DryRun();
        TrialEvidence.Validate(e, c);
        Assert.Null(e.Connect.Generation);
        Assert.Null(e.Read.ElapsedMilliseconds);
        Assert.Null(e.CopiedWordCount);
        Assert.Null(e.WireEvidence.DataLength);
        Assert.Equal(WireEvidenceOutcome.NOT_PROVIDED, e.WireEvidence.Outcome);
        Assert.Equal(TrialAcceptance.QUARANTINED, e.TrialAcceptance);
        // A dry run never claims an authorization was verified or consumed.
        Assert.Equal("NOT_ATTEMPTED", e.AuthorizationVerification);
        Assert.Equal("NOT_ATTEMPTED", e.AuthorizationConsumption);
        Assert.Equal("NOT_PROVIDED", e.AuthorizationPayloadSha256);
        Assert.Null(e.AcquisitionEpochAfter);
        var bytes = TrialEvidence.Serialize(e);
        Assert.Equal(bytes, TrialEvidence.ExportPublic(bytes, c, TrialCatalog.Hash(bytes)));
    }

    [Fact]
    public void Missing_Unknown_Duplicate_And_Binary_Content_Are_Rejected()
    {
        var c = TrialCatalog.LoadTrusted();
        var json = JsonNode.Parse(TrialEvidence.Serialize(DryRun()))!.AsObject();
        json["unapproved"] = 1;
        var extra = json.ToJsonString();
        Assert.Throws<JsonException>(() => TrialEvidence.ExportPublic(extra, c, TrialCatalog.Hash(extra)));
        json.Remove("unapproved");
        json.Remove("connect");
        var missing = json.ToJsonString();
        Assert.Throws<JsonException>(() => TrialEvidence.ExportPublic(missing, c, TrialCatalog.Hash(missing)));
        Assert.Throws<InvalidDataException>(() => StrictJson.Read<TrialEvidence>("{\"trialId\":\"T01\",\"trialId\":\"T01\"}"));
        Assert.ThrowsAny<JsonException>(() => StrictJson.Read<TrialEvidence>("\0"));
    }

    [Theory]
    [InlineData("C:\\Users\\USER\\capture")]
    [InlineData("\\\\MACHINE\\share\\capture")]
    [InlineData("/users/USER/capture")]
    [InlineData("user/capture")]
    [InlineData("unreviewed operator prose")]
    [InlineData("arena/bf844c54-waterjet-sentinel-suite")]
    [InlineData("wjss.tc-bench-trial/1")]
    [InlineData("credential-shaped")]
    [InlineData("secret-shaped")]
    [InlineData("access-token-shaped")]
    [InlineData("Bearer value")]
    public void Public_Export_Rejects_Paths_Secrets_And_Free_Text(string unsafeText)
    {
        var e = DryRun() with { EnvironmentSummary = unsafeText };
        var bytes = TrialEvidence.Serialize(e);
        Assert.Throws<InvalidDataException>(() => TrialEvidence.ExportPublic(bytes, TrialCatalog.LoadTrusted(), TrialCatalog.Hash(bytes)));
    }

    [Fact]
    public void Public_Export_Rejects_Raw_Details_And_Storage_Fields()
    {
        foreach (var key in new[] { "rawExceptionMessage", "storagePath", "credentials", "binary" })
        {
            var json = JsonNode.Parse(TrialEvidence.Serialize(DryRun()))!.AsObject();
            json[key] = "UNAPPROVED";
            var bytes = json.ToJsonString();
            Assert.Throws<JsonException>(() => TrialEvidence.ExportPublic(bytes, TrialCatalog.LoadTrusted(), TrialCatalog.Hash(bytes)));
        }
    }

    [Fact]
    public void Independently_Reviewed_Hash_Required_And_Original_Unmodified()
    {
        var original = TrialEvidence.Serialize(DryRun());
        Assert.Throws<InvalidDataException>(() => TrialEvidence.ExportPublic(original, TrialCatalog.LoadTrusted(), new string('a', 64)));
        Assert.Equal(original, TrialEvidence.ExportPublic(original, TrialCatalog.LoadTrusted(), TrialCatalog.Hash(original)));
    }

    [Fact]
    public void Adapter_Success_Alone_Cannot_Accept()
    {
        var e = DryRun() with { TrialAcceptance = TrialAcceptance.ACCEPTED };
        Assert.Throws<InvalidDataException>(() => TrialEvidence.Validate(e, TrialCatalog.LoadTrusted()));
        Assert.Throws<InvalidDataException>(() => TrialEvidence.Validate(DryRun() with { WireEvidence = new() { Outcome = WireEvidenceOutcome.MATCHED } }, TrialCatalog.LoadTrusted()));
    }

    [Fact]
    public void Counts_And_Indexes_Are_Not_Padded_Or_Truncated()
    {
        var e = DryRun() with { CopiedWordCount = 40, Words = Enumerable.Range(1, 40).Select(i => new IndexedWord(i, (ushort)i)).ToArray() };
        Assert.Throws<InvalidDataException>(() => TrialEvidence.Validate(e, TrialCatalog.LoadTrusted()));
    }

    [Fact]
    public void Physical_Records_Require_Verified_And_Consumed_Authorization_And_Build_Derived_Identity()
    {
        var c = TrialCatalog.LoadTrusted();
        var e = PhysicalExample();
        TrialEvidence.Validate(e, c);
        foreach (var broken in new[]
        {
            e with { AuthorizationVerification = "NOT_ATTEMPTED" },
            e with { AuthorizationConsumption = "NOT_ATTEMPTED" },
            e with { AuthorizationId = "NOT_PROVIDED" },
            e with { AuthorizationKeyId = "NOT_PROVIDED" },
            e with { AuthorizationPayloadSha256 = "NOT_PROVIDED" },
            e with { PhysicalCapabilityEnabled = false },
            e with { CleanStateProof = "NOT_PROVIDED" },
            e with { SourceRef = "NOT_PROVIDED" },
            e with { CleanupState = PhysicalTrialStates.Unproven },
            e with { WorkerObservationState = PhysicalTrialStates.Overdue },
            e with { LifecycleBudgetState = PhysicalTrialStates.Elapsed },
        }) Assert.Throws<InvalidDataException>(() => TrialEvidence.Validate(broken, c));
    }

    [Fact]
    public void Simulation_Records_Cannot_Claim_Authorization_Use_Or_Physical_Identity()
    {
        var c = TrialCatalog.LoadTrusted();
        foreach (var broken in new[]
        {
            DryRun() with { AuthorizationVerification = PhysicalTrialStates.Verified },
            DryRun() with { AuthorizationConsumption = PhysicalTrialStates.Consumed },
            DryRun() with { AuthorizationId = "AUTH-01" },
            DryRun() with { AuthorizationKeyId = "OWNER-KEY-01" },
            DryRun() with { AuthorizationPayloadSha256 = new string('d', 64) },
        }) Assert.Throws<InvalidDataException>(() => TrialEvidence.Validate(broken, c));
    }

    private static TrialEvidence PhysicalExample()
    {
        // Offline synthetic record ONLY. Never executed and never claimed as physical evidence.
        var t = DateTimeOffset.UtcNow;
        return DryRun() with
        {
            Provenance = "PHYSICAL_DIAGNOSTIC", PhysicalCapabilityEnabled = true,
            ImplementationSha = new string('a', 40), SourceRef = "wjss-k6-physical",
            CleanStateProof = "CLEAN_OWNER_VERIFIED",
            AuthorizationId = "AUTH-SYNTHETIC-01", AuthorizationReference = "SYNTHETIC-REVIEW",
            AuthorizationPayloadSha256 = new string('d', 64), AuthorizationKeyId = "OWNER-KEY-01",
            AuthorizationVerification = PhysicalTrialStates.Verified, AuthorizationConsumption = PhysicalTrialStates.Consumed,
            OperatorId = "NEUTRAL-01", EnvironmentSummary = "SYNTHETIC_OFFLINE_RECORD", CreatedAt = t,
            Connect = new() { State = "OBSERVED", Kind = TcReadKind.Success, StartedAt = t, CompletedAt = t.AddMilliseconds(1), ElapsedMilliseconds = 1, Generation = 1 },
            Read = new() { State = "OBSERVED", Kind = TcReadKind.Success, StartedAt = t.AddMilliseconds(2), CompletedAt = t.AddMilliseconds(10), ElapsedMilliseconds = 8, Generation = 1 },
            CopiedWordCount = 40, Words = Enumerable.Range(0, 40).Select(i => new IndexedWord(i, (ushort)i)).ToArray(),
            AcquisitionEpoch = 1, AcquisitionEpochAfter = 1, GenerationBefore = 1, GenerationBeforeCleanup = 1, GenerationAfterCleanup = 3,
            CancellationState = "NOT_REQUESTED", StopState = "NOT_REQUESTED", TimeoutState = "NOT_ELAPSED",
            LifecycleBudgetState = PhysicalTrialStates.NotElapsed, CleanupState = PhysicalTrialStates.Observed,
            WorkerObservationState = PhysicalTrialStates.Observed,
            StaleDecision = "CURRENT_AT_DIAGNOSTIC_HANDOFF", FinalHandoffDecision = "RETAINED_QUARANTINED",
            TransportBefore = "DISCONNECTED", TransportBeforeCleanup = "CONNECTED", TransportAfter = "DISCONNECTED",
            DisconnectResult = "COMPLETE", DisposalResult = "COMPLETE", TaskCompletion = "COMPLETE",
        };
    }

    private static WireReview Matched(TrialEvidence e) => new()
    {
        TrialId = e.TrialId, SessionId = e.SessionId, Endpoint = e.Endpoint, Port = e.Port,
        WindowStart = e.Read.StartedAt, WindowEnd = e.Read.CompletedAt, RequestAt = e.Read.StartedAt, ResponseAt = e.Read.CompletedAt,
        RequestTransactionId = 10, ResponseTransactionId = 10, RequestUnitId = 0, ResponseUnitId = 0,
        RequestFc = 3, ResponseFc = 3, RequestedStart = 0, RequestedQuantity = 40, RequestAduLength = 12,
        MbapLength = 83, ByteCountField = 80, PduLength = 82, AduLength = 89, DataLength = 80,
        CaptureCompleteness = "COMPLETE", IndexedWordsSha256 = TrialEvidence.WordDigest(e.Words),
        Artifacts = [new("CAPTURE-01", 200, new string('b', 64))], Outcome = WireEvidenceOutcome.MATCHED,
    };

    [Fact]
    public void Offline_Owner_Decision_And_Matching_Wire_Are_Both_Required()
    {
        var e = PhysicalExample();
        var bytes = TrialEvidence.Serialize(e);
        var owner = new OwnerReview
        {
            TrialId = e.TrialId, SessionId = e.SessionId, AcquisitionManifestSha256 = TrialCatalog.Hash(bytes),
            OwnerId = "NEUTRAL-OWNER", Reference = "SYNTHETIC-REVIEW", ReviewedAt = e.CreatedAt.AddSeconds(1),
            Decision = TrialAcceptance.ACCEPTED, ApprovedWordExcerpt = true,
        };
        var reviewed = TrialEvidence.ApplyOfflineReview(bytes, TrialCatalog.LoadTrusted(), Matched(e), owner);
        Assert.Equal(TrialAcceptance.ACCEPTED, reviewed.TrialAcceptance);
        Assert.Equal(TrialAcceptance.QUARANTINED, e.TrialAcceptance);
        var publicBytes = TrialEvidence.Serialize(reviewed);
        Assert.Equal(publicBytes, TrialEvidence.ExportPublic(publicBytes, TrialCatalog.LoadTrusted(), TrialCatalog.Hash(publicBytes)));
        Assert.Throws<InvalidDataException>(() => TrialEvidence.ApplyOfflineReview(bytes, TrialCatalog.LoadTrusted(), new WireReview(), owner));
        Assert.Throws<InvalidDataException>(() => TrialEvidence.ApplyOfflineReview(bytes, TrialCatalog.LoadTrusted(), Matched(e), owner with { AcquisitionManifestSha256 = new string('c', 64) }));
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("transaction")]
    [InlineData("fc")]
    [InlineData("start")]
    [InlineData("quantity")]
    [InlineData("length")]
    [InlineData("time")]
    [InlineData("trial")]
    [InlineData("words")]
    public void Matched_Wire_Must_Correlate_Exact_Identity_Framing_Time_And_Data(string field)
    {
        var e = PhysicalExample();
        var w = Matched(e);
        var bad = field switch
        {
            "unit" => w with { ResponseUnitId = 255 },
            "transaction" => w with { ResponseTransactionId = 11 },
            "fc" => w with { ResponseFc = 4 },
            "start" => w with { RequestedStart = 1 },
            "quantity" => w with { RequestedQuantity = 39 },
            "length" => w with { AduLength = 88 },
            "time" => w with { ResponseAt = e.Read.CompletedAt!.Value.AddSeconds(1) },
            "trial" => w with { TrialId = "OTHER" },
            _ => w with { IndexedWordsSha256 = new string('c', 64) },
        };
        Assert.Throws<InvalidDataException>(() => TrialEvidence.ValidateWire(e, bad));
    }

    [Fact]
    public void Excerpts_Need_Explicit_Owner_Review()
    {
        var e = PhysicalExample();
        var bytes = TrialEvidence.Serialize(e);
        Assert.Throws<InvalidDataException>(() => TrialEvidence.ExportPublic(bytes, TrialCatalog.LoadTrusted(), TrialCatalog.Hash(bytes)));
    }
}
