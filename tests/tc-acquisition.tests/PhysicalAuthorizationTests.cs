using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wjss.Adapters.TcAcquisition;
using Wjss.TcBench;
using Xunit;

namespace Wjss.TcAcquisition.Tests;

/// <summary>
/// Focused tests for the fail-closed physical authorization gate. Everything here is offline:
/// synthetic in-memory P-256 keys, synthetic signed payloads in a temporary directory outside the
/// repository, and a fake client. No endpoint is resolved or contacted, no real Owner key is used and
/// no real T01 authorization artifact is created.
/// </summary>
public sealed class PhysicalAuthorizationTests
{
    private sealed class ArtifactDirectory : IDisposable
    {
        public ArtifactDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wjss-k6-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { /* Temporary test artifacts only; the OS reclaims them. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Exact allowlisted property names of the external consumption marker, in canonical order.</summary>
    private static readonly string[] ExpectedConsumptionMarkerPropertyNames =
        ["authorizationId", "authorizationPayloadSha256", "consumedAtUtc", "implementationSha", "invocationId", "schemaVersion", "trialId"];

    private static BuildIdentity NewIdentity(ECDsa key) => new()
    {
        SchemaVersion = BuildIdentity.SchemaVersionValue,
        PhysicalCapabilityEnabled = true,
        DeploymentProof = BuildIdentity.DeploymentProofClean,
        ApprovedBaseSha = "193aba764f8934df3dc3b0fd1841e8b64e808cc2",
        ImplementationSha = new string('a', 40),
        SourceRef = "wjss-k6-physical",
        AuthorizationKeyId = "OWNER-KEY-01",
        OwnerPublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
    };

    // The signed candidate tuple is taken from the trusted catalog, never typed here: a synthetic
    // payload must not become a second source of endpoint facts.
    private static PhysicalAuthorization NewAuthorization(BuildIdentity identity, TrialCatalog catalog)
    {
        var trial = catalog.Select("T01").Trial;
        return NewAuthorization(identity, trial, catalog.Select("T01").ManifestSha256);
    }

    private static PhysicalAuthorization NewAuthorization(BuildIdentity identity, TrialConfiguration trial, string manifestSha256) => new()
    {
        SchemaVersion = PhysicalAuthorization.SchemaVersionValue,
        AuthorizationId = "AUTH-T01-0001",
        Nonce = "NONCE-0001",
        TrialId = "T01",
        ManifestSha256 = manifestSha256,
        BuildIdentityDigest = identity.Digest,
        AuthorizationKeyId = identity.AuthorizationKeyId,
        ImplementationSha = identity.ImplementationSha,
        ApprovedBaseSha = identity.ApprovedBaseSha,
        SourceRef = identity.SourceRef,
        OwnerId = "NEUTRAL-OWNER",
        OwnerReference = "OWNER-REF-01",
        Scope = PhysicalAuthorization.ScopeValue,
        ValidFrom = DateTimeOffset.UtcNow.AddMinutes(-1),
        ValidUntil = DateTimeOffset.UtcNow.AddMinutes(10),
        NodeId = trial.NodeId,
        CabinetId = trial.CabinetId,
        Address = trial.Address,
        Port = trial.Port,
        FunctionCode = trial.FunctionCode,
        UnitId = trial.UnitId,
        Start = trial.Start,
        Quantity = trial.Quantity,
        Endianness = trial.Endianness,
        OneInvocation = true,
        OneConnectAttempt = true,
        AtMostOneRead = true,
        IsolationPlanRef = "ISO-PLAN-01",
        WireEvidencePlanRef = "WIRE-PLAN-01",
        EvidenceDestinationRef = "EVIDENCE-DEST-01",
        StopProcedureRef = "STOP-PROC-01",
    };

    private static string WriteAndAuthorize(
        string directory, BuildIdentity identity, ECDsa key, PhysicalAuthorization authorization,
        bool derSignature = false, string? rawText = null, bool omitSignatureFile = false)
    {
        var payloadText = rawText ?? PhysicalTrialAuthorization.CanonicalPayloadText(authorization);
        var payloadBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(payloadText);
        var payloadPath = System.IO.Path.Combine(directory, "authorization.json");
        File.WriteAllBytes(payloadPath, payloadBytes);
        var signaturePath = System.IO.Path.Combine(directory, "authorization.sig");
        if (omitSignatureFile) return PhysicalTrialAuthorization.Authorize(TrialCatalog.LoadTrusted(), identity, payloadPath, signaturePath).RefusalCode;
        var signature = key.SignData(payloadBytes, HashAlgorithmName.SHA256,
            derSignature ? DSASignatureFormat.Rfc3279DerSequence : DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        File.WriteAllBytes(signaturePath, Encoding.ASCII.GetBytes(Convert.ToBase64String(signature) + "\n"));
        return PhysicalTrialAuthorization.Authorize(TrialCatalog.LoadTrusted(), identity, payloadPath, signaturePath).RefusalCode;
    }

    [Fact]
    public void Repository_Build_Is_Physically_Disabled_And_Carries_No_Placeholder_Authority()
    {
        var identity = BuildIdentity.LoadTrusted();
        Assert.False(identity.PhysicalCapabilityEnabled);
        Assert.Equal(BuildIdentity.NotProvided, identity.ImplementationSha);
        Assert.Equal(BuildIdentity.NotProvided, identity.DeploymentProof);
        Assert.Equal(BuildIdentity.NotProvided, identity.SourceRef);
        Assert.Equal(BuildIdentity.NotProvided, identity.AuthorizationKeyId);
        Assert.Equal(BuildIdentity.NotProvided, identity.OwnerPublicKey);
        Assert.Equal("193aba764f8934df3dc3b0fd1841e8b64e808cc2", identity.ApprovedBaseSha);
        Assert.Matches("\\A[0-9a-f]{64}\\z", identity.Digest);
        Assert.Equal(PhysicalTrialRefusals.PhysicalCapabilityDisabled, BuildIdentity.ValidateForPhysicalUse(identity));
        Assert.Equal(PhysicalTrialRefusals.BuildIdentityRejected, BuildIdentity.ValidateForPhysicalUse(null));
        // A disabled build refuses before any artifact path is touched.
        using var artifacts = new ArtifactDirectory();
        var decision = PhysicalTrialAuthorization.Authorize(TrialCatalog.LoadTrusted(), identity,
            System.IO.Path.Combine(artifacts.Path, "absent.json"), System.IO.Path.Combine(artifacts.Path, "absent.sig"));
        Assert.False(decision.Authorized);
        Assert.Null(decision.Plan);
        Assert.Equal(PhysicalTrialRefusals.PhysicalCapabilityDisabled, decision.RefusalCode);
    }

    [Fact]
    public void Build_Identity_Parsing_Is_Strict_About_Unknown_Duplicate_Missing_And_Empty_Fields()
    {
        var identity = BuildIdentity.LoadTrusted();
        var text = identity.CanonicalText();
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        Assert.Equal(identity, BuildIdentity.Parse(text));
        // A byte-order mark written by a build tool carries no meaning and is stripped.
        Assert.Equal(identity, BuildIdentity.Parse("\uFEFF" + text));
        Assert.Throws<InvalidDataException>(() => BuildIdentity.Parse(text + "unapproved=1\n"));
        Assert.Throws<InvalidDataException>(() => BuildIdentity.Parse(text + "implementationSha=duplicate\n"));
        Assert.Throws<InvalidDataException>(() => BuildIdentity.Parse(text.Replace("schemaVersion=wjss.tc-bench-build/1\n", string.Empty, StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => BuildIdentity.Parse(text.Replace("sourceRef=NOT_PROVIDED", "sourceRef=", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => BuildIdentity.Parse(text.Replace("physicalCapabilityEnabled=false", "physicalCapabilityEnabled=maybe", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => BuildIdentity.Parse(text.Replace("approvedBaseSha=193aba764f8934df3dc3b0fd1841e8b64e808cc2", "approvedBaseSha=NOT_PROVIDED", StringComparison.Ordinal)).RequireValid());
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var physical = NewIdentity(key);
        Assert.Null(BuildIdentity.ValidateForPhysicalUse(physical));
        Assert.Equal(PhysicalTrialRefusals.BuildIdentityRejected, BuildIdentity.ValidateForPhysicalUse(physical with { DeploymentProof = "NOT_PROVIDED" }));
        Assert.Equal(PhysicalTrialRefusals.BuildIdentityRejected, BuildIdentity.ValidateForPhysicalUse(physical with { ImplementationSha = "NOT_PROVIDED" }));
        Assert.Equal(PhysicalTrialRefusals.BuildIdentityRejected, BuildIdentity.ValidateForPhysicalUse(physical with { SourceRef = "NOT_PROVIDED" }));
        Assert.Equal(PhysicalTrialRefusals.BuildIdentityRejected, BuildIdentity.ValidateForPhysicalUse(physical with { AuthorizationKeyId = "NOT_PROVIDED" }));
        Assert.Equal(PhysicalTrialRefusals.PublicKeyRejected, BuildIdentity.ValidateForPhysicalUse(physical with { OwnerPublicKey = "NOT_PROVIDED" }));
        Assert.Equal(PhysicalTrialRefusals.PublicKeyRejected, BuildIdentity.ValidateForPhysicalUse(physical with { OwnerPublicKey = Convert.ToBase64String(new byte[] { 1, 2, 3 }) }));
    }

    [Fact]
    public void Canonical_Payload_Is_Exact_Utf8_Without_Bom_And_With_One_Trailing_Lf()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var catalog = TrialCatalog.LoadTrusted();
        var identity = NewIdentity(key);
        var authorization = NewAuthorization(identity, catalog);
        var bytes = PhysicalTrialAuthorization.CanonicalPayloadBytes(authorization);
        var text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.Equal(PhysicalTrialAuthorization.CanonicalPayloadText(authorization), text);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB);
        Assert.Equal(Encoding.UTF8.GetBytes(text), bytes);
        Assert.StartsWith("{\"schemaVersion\":\"wjss.tc-bench-authorization/1\",", text, StringComparison.Ordinal);
        // Re-serializing the same values reproduces the signed bytes exactly, so a reserialized object
        // can never be substituted for the reviewed artifact.
        Assert.Equal(bytes, PhysicalTrialAuthorization.CanonicalPayloadBytes(StrictJson.Read<PhysicalAuthorization>(text)));
    }

    [Fact]
    public void Detached_Signature_Verifies_Only_The_Exact_Canonical_Bytes()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var catalog = TrialCatalog.LoadTrusted();
        var identity = NewIdentity(key);
        var authorization = NewAuthorization(identity, catalog);
        var bytes = PhysicalTrialAuthorization.CanonicalPayloadBytes(authorization);
        var signature = key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        Assert.True(PhysicalTrialAuthorization.VerifyDetachedSignature(identity, bytes, signature));
        var tampered = bytes.ToArray();
        tampered[^2] ^= 0x01;
        Assert.False(PhysicalTrialAuthorization.VerifyDetachedSignature(identity, tampered, signature));
        Assert.False(PhysicalTrialAuthorization.VerifyDetachedSignature(identity,
            new UTF8Encoding(false).GetBytes(PhysicalTrialAuthorization.CanonicalPayloadText(authorization) + " "), signature));
        Assert.False(PhysicalTrialAuthorization.VerifyDetachedSignature(identity, bytes,
            other.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        // The fixed signature encoding is IEEE P1363 r||s; a DER sequence is refused, not converted.
        Assert.False(PhysicalTrialAuthorization.VerifyDetachedSignature(identity, bytes,
            key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)));
        Assert.False(PhysicalTrialAuthorization.VerifyDetachedSignature(identity, bytes, signature[..63]));
        Assert.False(PhysicalTrialAuthorization.VerifyDetachedSignature(identity with { OwnerPublicKey = "NOT_PROVIDED" }, bytes, signature));
    }

    [Fact]
    public void Gate_Refuses_Every_Binding_Mismatch_With_A_Distinct_Typed_Code()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var catalog = TrialCatalog.LoadTrusted();
        var identity = NewIdentity(key);
        var baseline = NewAuthorization(identity, catalog);
        // Derived from the trusted address, never typed: a mismatch case must not introduce a second
        // endpoint literal anywhere in the Product tree.
        var wrongAddress = catalog.Select("T01").Trial.Address[..^1] + (catalog.Select("T01").Trial.Address[^1] == '1' ? '9' : '1');
        Assert.Equal(PhysicalTrialRefusals.AuthorizationMissing, PhysicalTrialAuthorization.Authorize(catalog, identity, null, null).RefusalCode);
        using var missingSignature = new ArtifactDirectory();
        Assert.Equal(PhysicalTrialRefusals.SignatureMissing, PhysicalTrialAuthorization.Authorize(catalog, identity,
            System.IO.Path.Combine(missingSignature.Path, "authorization.json"), null).RefusalCode);
        using var unreadable = new ArtifactDirectory();
        Assert.Equal(PhysicalTrialRefusals.AuthorizationUnreadable, WriteAndAuthorize(unreadable.Path, identity, key, baseline, omitSignatureFile: true));
        using var insideTree = new ArtifactDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(insideTree.Path, ".git"));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationInsideRepository, PhysicalTrialAuthorization.Authorize(catalog, identity,
            System.IO.Path.Combine(insideTree.Path, "authorization.json"), System.IO.Path.Combine(insideTree.Path, "authorization.sig")).RefusalCode);
        using var cases = new ArtifactDirectory();
        Assert.Equal(PhysicalTrialRefusals.KeyIdMismatch, WriteAndAuthorize(cases.Path, identity, key, baseline with { AuthorizationKeyId = "OTHER-KEY-01" }));
        Assert.Equal(PhysicalTrialRefusals.SignatureRejected, WriteAndAuthorize(cases.Path, identity, key, baseline, derSignature: true));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationNotCanonical, WriteAndAuthorize(cases.Path, identity, key, baseline,
            rawText: PhysicalTrialAuthorization.CanonicalPayloadText(baseline).Replace("\n", "\r\n", StringComparison.Ordinal)));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationNotCanonical, WriteAndAuthorize(cases.Path, identity, key, baseline,
            rawText: "\uFEFF" + PhysicalTrialAuthorization.CanonicalPayloadText(baseline)));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationRejected, WriteAndAuthorize(cases.Path, identity, key, baseline,
            rawText: "{\"schemaVersion\":\"wjss.tc-bench-authorization/1\"}\n"));
        Assert.Equal(PhysicalTrialRefusals.NotYetValid, WriteAndAuthorize(cases.Path, identity, key,
            baseline with { ValidFrom = DateTimeOffset.UtcNow.AddMinutes(5), ValidUntil = DateTimeOffset.UtcNow.AddMinutes(10) }));
        Assert.Equal(PhysicalTrialRefusals.Expired, WriteAndAuthorize(cases.Path, identity, key,
            baseline with { ValidFrom = DateTimeOffset.UtcNow.AddMinutes(-10), ValidUntil = DateTimeOffset.UtcNow.AddMinutes(-5) }));
        // UTC only: an authorization window carrying a local offset is refused outright.
        Assert.Equal(PhysicalTrialRefusals.AuthorizationRejected, WriteAndAuthorize(cases.Path, identity, key,
            baseline with { ValidUntil = DateTimeOffset.Now.AddMinutes(5) }));
        Assert.Equal(PhysicalTrialRefusals.ImplementationMismatch, WriteAndAuthorize(cases.Path, identity, key, baseline with { ImplementationSha = new string('b', 40) }));
        Assert.Equal(PhysicalTrialRefusals.BaseMismatch, WriteAndAuthorize(cases.Path, identity, key, baseline with { ApprovedBaseSha = new string('c', 40) }));
        Assert.Equal(PhysicalTrialRefusals.SourceRefMismatch, WriteAndAuthorize(cases.Path, identity, key, baseline with { SourceRef = "wjss-k6-other" }));
        Assert.Equal(PhysicalTrialRefusals.BuildIdentityMismatch, WriteAndAuthorize(cases.Path, identity, key, baseline with { BuildIdentityDigest = new string('d', 64) }));
        Assert.Equal(PhysicalTrialRefusals.ManifestMismatch, WriteAndAuthorize(cases.Path, identity, key, baseline with { ManifestSha256 = new string('e', 64) }));
        Assert.Equal(PhysicalTrialRefusals.TupleMismatch, WriteAndAuthorize(cases.Path, identity, key, baseline with { Quantity = 39 }));
        Assert.Equal(PhysicalTrialRefusals.TupleMismatch, WriteAndAuthorize(cases.Path, identity, key, baseline with { Address = wrongAddress }));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationRejected, WriteAndAuthorize(cases.Path, identity, key, baseline with { TrialId = "T02" }));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationRejected, WriteAndAuthorize(cases.Path, identity, key, baseline with { Scope = "READ_ONLY" }));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationRejected, WriteAndAuthorize(cases.Path, identity, key, baseline with { AtMostOneRead = false }));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationRejected, WriteAndAuthorize(cases.Path, identity, key, baseline with { OwnerReference = "owner reference with spaces" }));
        Assert.Equal(PhysicalTrialRefusals.AuthorizationRejected, WriteAndAuthorize(cases.Path, identity, key, baseline with { SchemaVersion = "wjss.tc-bench-authorization/2" }));
        // Repeated refusal never leaves a consumption marker behind.
        Assert.False(Directory.Exists(System.IO.Path.Combine(cases.Path, "consumed")));
    }

    [Fact]
    public async Task Authorize_Returns_A_Plan_Only_After_Atomic_Single_Use_Consumption_And_The_Plan_Runs_One_Shot()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var catalog = TrialCatalog.LoadTrusted();
        var identity = NewIdentity(key);
        var authorization = NewAuthorization(identity, catalog);
        using var artifacts = new ArtifactDirectory();
        var payloadPath = System.IO.Path.Combine(artifacts.Path, "authorization.json");
        var signaturePath = System.IO.Path.Combine(artifacts.Path, "authorization.sig");
        var payloadBytes = PhysicalTrialAuthorization.CanonicalPayloadBytes(authorization);
        File.WriteAllBytes(payloadPath, payloadBytes);
        File.WriteAllBytes(signaturePath, Encoding.ASCII.GetBytes(Convert.ToBase64String(
            key.SignData(payloadBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) + "\n"));

        var decision = PhysicalTrialAuthorization.Authorize(catalog, identity, payloadPath, signaturePath);
        Assert.True(decision.Authorized);
        Assert.Equal(PhysicalTrialRefusals.NotRefused, decision.RefusalCode);
        var plan = Assert.IsType<PhysicalTrialPlan>(decision.Plan);
        Assert.Equal("AUTH-T01-0001", plan.AuthorizationId);
        Assert.Equal("OWNER-REF-01", plan.AuthorizationReference);
        Assert.Equal(identity.AuthorizationKeyId, plan.AuthorizationKeyId);
        Assert.Equal(identity.Digest, plan.BuildIdentity.Digest);
        Assert.Equal(catalog.Select("T01").Trial.Address, plan.Endpoint.IpAddress);
        Assert.Equal(502, plan.Endpoint.Port);
        Assert.Equal(TrialCatalog.Hash(PhysicalTrialAuthorization.CanonicalPayloadText(authorization)), plan.AuthorizationPayloadSha256);
        Assert.Equal(catalog.Select("T01").Trial, plan.Trial);

        // The marker is written outside the repository, contains only allowlisted neutral fields, and
        // the authorization can never be used twice.
        var markerPath = System.IO.Path.Combine(artifacts.Path, "consumed", "AUTH-T01-0001.consumed.json");
        Assert.True(File.Exists(markerPath));
        var markerText = File.ReadAllText(markerPath);
        var marker = JsonDocument.Parse(markerText).RootElement;
        var names = marker.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(ExpectedConsumptionMarkerPropertyNames, names);
        Assert.Equal("AUTH-T01-0001", marker.GetProperty("authorizationId").GetString());
        Assert.Equal(identity.ImplementationSha, marker.GetProperty("implementationSha").GetString());
        Assert.DoesNotContain("\\", markerText, StringComparison.Ordinal);
        Assert.DoesNotContain(artifacts.Path, markerText, StringComparison.Ordinal);
        Assert.Equal(PhysicalTrialRefusals.AlreadyConsumed,
            PhysicalTrialAuthorization.Authorize(catalog, identity, payloadPath, signaturePath).RefusalCode);

        // The production entry point accepts only the plan, so no client exists before consumption.
        var fake = new TestClient();
        var evidence = await new TrialRunner().RunPhysicalTrialAsync(plan, () => fake, CancellationToken.None);
        Assert.Equal("PHYSICAL_DIAGNOSTIC", evidence.Provenance);
        Assert.True(evidence.PhysicalCapabilityEnabled);
        Assert.Equal(BuildIdentity.DeploymentProofClean, evidence.CleanStateProof);
        Assert.Equal(identity.ImplementationSha, evidence.ImplementationSha);
        Assert.Equal(identity.Digest, evidence.DeploymentBuildIdentity);
        Assert.Equal(PhysicalTrialStates.Verified, evidence.AuthorizationVerification);
        Assert.Equal(PhysicalTrialStates.Consumed, evidence.AuthorizationConsumption);
        Assert.Equal(plan.AuthorizationPayloadSha256, evidence.AuthorizationPayloadSha256);
        Assert.Equal(1, fake.ConnectCalls);
        Assert.Equal(1, fake.ReadCalls);
        Assert.Equal(40, evidence.Words.Length);
        Assert.Equal("RETAINED_QUARANTINED", evidence.FinalHandoffDecision);
        Assert.Equal(TrialAcceptance.QUARANTINED, evidence.TrialAcceptance);
        Assert.NotEqual(TrialAcceptance.ACCEPTED, evidence.TrialAcceptance);
        TrialEvidence.Validate(evidence, catalog);
    }

    [Fact]
    public void Plan_Cannot_Be_Constructed_And_No_Public_API_Returns_A_Client_Or_Paths()
    {
        Assert.Empty(typeof(PhysicalTrialPlan).GetConstructors());
        Assert.DoesNotContain(typeof(PhysicalTrialAuthorization).GetMethods(), m => m.ReturnType == typeof(ITcReadOnlyClient));
        Assert.All(typeof(TrialRunner).GetMethods().Where(m => m.Name.StartsWith("Run", StringComparison.Ordinal)),
            m => Assert.All(m.GetParameters(), p => Assert.NotEqual(typeof(string), p.ParameterType)));
    }
}
