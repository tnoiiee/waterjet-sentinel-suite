using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wjss.Adapters.TcAcquisition;

namespace Wjss.TcBench;

/// <summary>
/// Fixed refusal and observation vocabulary for the physical gate. Every value is a neutral
/// machine-readable token: no path, no exception message and no free text can reach public evidence.
/// </summary>
public static class PhysicalTrialRefusals
{
    public const string NotRefused = "NOT_REFUSED";
    public const string PhysicalCapabilityDisabled = "PHYSICAL_CAPABILITY_DISABLED";
    public const string BuildIdentityRejected = "BUILD_IDENTITY_REJECTED";
    public const string PublicKeyRejected = "BUILD_IDENTITY_PUBLIC_KEY_REJECTED";
    public const string AuthorizationMissing = "AUTHORIZATION_MISSING";
    public const string SignatureMissing = "AUTHORIZATION_SIGNATURE_MISSING";
    public const string AuthorizationUnreadable = "AUTHORIZATION_UNREADABLE";
    public const string AuthorizationInsideRepository = "AUTHORIZATION_INSIDE_REPOSITORY";
    public const string AuthorizationNotCanonical = "AUTHORIZATION_NOT_CANONICAL";
    public const string KeyIdMismatch = "AUTHORIZATION_KEY_ID_MISMATCH";
    public const string SignatureRejected = "AUTHORIZATION_SIGNATURE_REJECTED";
    public const string AuthorizationRejected = "AUTHORIZATION_REJECTED";
    public const string NotYetValid = "AUTHORIZATION_NOT_YET_VALID";
    public const string Expired = "AUTHORIZATION_EXPIRED";
    public const string ImplementationMismatch = "AUTHORIZATION_IMPLEMENTATION_MISMATCH";
    public const string BaseMismatch = "AUTHORIZATION_BASE_MISMATCH";
    public const string SourceRefMismatch = "AUTHORIZATION_SOURCE_REF_MISMATCH";
    public const string BuildIdentityMismatch = "AUTHORIZATION_BUILD_IDENTITY_MISMATCH";
    public const string ManifestMismatch = "AUTHORIZATION_MANIFEST_MISMATCH";
    public const string TupleMismatch = "AUTHORIZATION_TUPLE_MISMATCH";
    public const string AlreadyConsumed = "AUTHORIZATION_ALREADY_CONSUMED";
    public const string ConsumptionUnavailable = "AUTHORIZATION_CONSUMPTION_UNAVAILABLE";

    /// <summary>Terminal deployment state. Physical test deployment is not authorized in this Stage.</summary>
    public const string DeploymentNotAuthorized = "PHYSICAL_TEST_DEPLOYMENT_NOT_AUTHORIZED";
}

/// <summary>Observation states recorded in evidence. Absence of an observation is never a positive claim.</summary>
public static class PhysicalTrialStates
{
    public const string NotAttempted = "NOT_ATTEMPTED";
    public const string NotObserved = "NOT_OBSERVED";
    public const string NotProvided = "NOT_PROVIDED";
    public const string Verified = "VERIFIED";
    public const string Consumed = "CONSUMED";
    public const string AlreadyConsumed = "ALREADY_CONSUMED";
    public const string Unavailable = "UNAVAILABLE";
    public const string NotElapsed = "NOT_ELAPSED";
    public const string Elapsed = "ELAPSED";
    public const string Observed = "OBSERVED";
    public const string Overdue = "OVERDUE";
    public const string Unproven = "UNPROVEN";
}

/// <summary>
/// The exact signed payload of a single-use Owner authorization for one physical trial.
///
/// Property declaration order IS the canonical field order: canonical bytes are produced only by
/// <see cref="PhysicalTrialAuthorization.CanonicalPayloadBytes"/>. The signature is detached and is
/// verified over the exact supplied payload bytes; a payload whose bytes are not byte-identical to
/// the canonical form is refused, so a reserialized object can never be substituted for the
/// reviewed artifact.
///
/// The payload carries no credential, no secret, no private key, no local storage path, no
/// unreviewed prose, no second endpoint, no alternative candidate and no write/control authority.
/// </summary>
public sealed record PhysicalAuthorization
{
    /// <summary>The only accepted authorization schema for this Stage.</summary>
    public const string SchemaVersionValue = "wjss.tc-bench-authorization/1";

    /// <summary>The only accepted scope: one invocation, connect once, at most one read.</summary>
    public const string ScopeValue = "ONE_SHOT_READ_ONLY";

    [JsonRequired] public string SchemaVersion { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string AuthorizationId { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string Nonce { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string TrialId { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string ManifestSha256 { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string BuildIdentityDigest { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string AuthorizationKeyId { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string ImplementationSha { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string ApprovedBaseSha { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string SourceRef { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string OwnerId { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string OwnerReference { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string Scope { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public DateTimeOffset ValidFrom { get; init; }
    [JsonRequired] public DateTimeOffset ValidUntil { get; init; }
    [JsonRequired] public string NodeId { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string CabinetId { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string Address { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public int Port { get; init; }
    [JsonRequired] public int FunctionCode { get; init; }
    [JsonRequired] public byte UnitId { get; init; }
    [JsonRequired] public ushort Start { get; init; }
    [JsonRequired] public ushort Quantity { get; init; }
    [JsonRequired] public string Endianness { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public bool OneInvocation { get; init; }
    [JsonRequired] public bool OneConnectAttempt { get; init; }
    [JsonRequired] public bool AtMostOneRead { get; init; }
    [JsonRequired] public string IsolationPlanRef { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string WireEvidencePlanRef { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string EvidenceDestinationRef { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string StopProcedureRef { get; init; } = PhysicalTrialStates.NotProvided;
}

/// <summary>
/// Neutral record written to the external consumption marker. Allowlisted fields only: no
/// credential, no private key, no raw exception message and no local storage path.
/// </summary>
internal sealed record AuthorizationConsumptionRecord
{
    [JsonRequired] public string SchemaVersion { get; init; } = "wjss.tc-bench-consumption/1";
    [JsonRequired] public string AuthorizationId { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string AuthorizationPayloadSha256 { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string ImplementationSha { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public string TrialId { get; init; } = PhysicalTrialStates.NotProvided;
    [JsonRequired] public DateTimeOffset ConsumedAtUtc { get; init; }
    [JsonRequired] public string InvocationId { get; init; } = PhysicalTrialStates.NotProvided;
}

/// <summary>
/// A plan that exists only after every fail-closed gate has passed and the authorization has been
/// atomically consumed. The constructor is private: the only way to obtain a plan is
/// <see cref="PhysicalTrialAuthorization.Authorize"/>. No client may be constructed without one.
/// </summary>
public sealed class PhysicalTrialPlan
{
    private PhysicalTrialPlan(
        TrialCatalog catalog, TrialConfiguration trial, TcNodeEndpoint endpoint, BuildIdentity buildIdentity,
        string authorizationId, string authorizationReference, string authorizationPayloadSha256, string authorizationKeyId)
    {
        Catalog = catalog;
        Trial = trial;
        Endpoint = endpoint;
        BuildIdentity = buildIdentity;
        AuthorizationId = authorizationId;
        AuthorizationReference = authorizationReference;
        AuthorizationPayloadSha256 = authorizationPayloadSha256;
        AuthorizationKeyId = authorizationKeyId;
    }

    public TrialCatalog Catalog { get; }
    public TrialConfiguration Trial { get; }
    public TcNodeEndpoint Endpoint { get; }
    public BuildIdentity BuildIdentity { get; }
    public string AuthorizationId { get; }
    public string AuthorizationReference { get; }
    public string AuthorizationPayloadSha256 { get; }
    public string AuthorizationKeyId { get; }

    internal static PhysicalTrialPlan Create(
        TrialCatalog catalog, TrialConfiguration trial, TcNodeEndpoint endpoint, BuildIdentity buildIdentity,
        string authorizationId, string authorizationReference, string authorizationPayloadSha256, string authorizationKeyId) =>
        new(catalog, trial, endpoint, buildIdentity, authorizationId, authorizationReference, authorizationPayloadSha256, authorizationKeyId);
}

/// <summary>Gate outcome. Either a plan exists, or a fixed refusal code explains why it does not.</summary>
public sealed record PhysicalTrialDecision
{
    public required string RefusalCode { get; init; }
    public PhysicalTrialPlan? Plan { get; init; }
    public bool Authorized => Plan is not null;
}

/// <summary>
/// The fail-closed physical authorization gate.
///
/// Gate order: build identity and physical capability, then artifact/signature presence and location,
/// then exact payload bytes, then strict schema and canonical form, then the trusted key identifier,
/// then detached ECDSA P-256 / SHA-256 verification over the exact bytes, then the validity window,
/// then implementation/base/source/build-identity binding, then manifest digest, then the exact
/// candidate tuple and scope, and only then the atomic single-use consumption that precedes any
/// client construction. Paths are outside the Git working tree by construction; no credential,
/// secret or private key is read, written or derived anywhere in this path.
/// </summary>
public static class PhysicalTrialAuthorization
{
    /// <summary>Detached signature encoding: base64 of the raw 64-byte IEEE P1363 P-256 r||s value.</summary>
    internal const int DetachedSignatureLength = 64;

    private const string ConsumptionDirectoryName = "consumed";
    private const string ConsumptionMarkerSuffix = ".consumed.json";
    private const int MaximumSignatureFileLength = 1024;
    private const int MaximumPayloadLength = 16384;
    private const int MaximumRepositoryProbeDepth = 40;

    /// <summary>
    /// Verifies the prepared trust metadata and the detached Owner authorization. Returns a decision;
    /// never throws for a policy refusal and never constructs, resolves or contacts an endpoint.
    /// </summary>
    public static PhysicalTrialDecision Authorize(TrialCatalog catalog, BuildIdentity buildIdentity, string? authorizationPath, string? signaturePath)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(buildIdentity);

        var identityRefusal = BuildIdentity.ValidateForPhysicalUse(buildIdentity);
        if (identityRefusal is not null) return Refuse(identityRefusal);
        if (authorizationPath is null) return Refuse(PhysicalTrialRefusals.AuthorizationMissing);
        if (signaturePath is null) return Refuse(PhysicalTrialRefusals.SignatureMissing);
        if (IsInsideWorkingTree(authorizationPath) || IsInsideWorkingTree(signaturePath)) return Refuse(PhysicalTrialRefusals.AuthorizationInsideRepository);

        if (!TryReadArtifactBytes(authorizationPath, MaximumPayloadLength, out var payloadBytes)) return Refuse(PhysicalTrialRefusals.AuthorizationUnreadable);
        if (!TryReadArtifactBytes(signaturePath, MaximumSignatureFileLength, out var signatureFileBytes)) return Refuse(PhysicalTrialRefusals.AuthorizationUnreadable);

        // Canonical bytes only: no BOM, no carriage return, no trailing whitespace other than the single LF.
        if (payloadBytes.Length < 2 || (payloadBytes[0] == 0xEF && payloadBytes[1] == 0xBB)) return Refuse(PhysicalTrialRefusals.AuthorizationNotCanonical);
        string payloadText;
        try
        {
            payloadText = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(payloadBytes);
        }
        catch (ArgumentException)
        {
            return Refuse(PhysicalTrialRefusals.AuthorizationNotCanonical);
        }
        if (payloadText.Contains('\r', StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.AuthorizationNotCanonical);

        PhysicalAuthorization authorization;
        try
        {
            authorization = StrictJson.Read<PhysicalAuthorization>(payloadText);
        }
        catch (JsonException)
        {
            return Refuse(PhysicalTrialRefusals.AuthorizationRejected);
        }
        if (!string.Equals(CanonicalPayloadText(authorization), payloadText, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.AuthorizationNotCanonical);

        if (!string.Equals(authorization.AuthorizationKeyId, buildIdentity.AuthorizationKeyId, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.KeyIdMismatch);
        if (!TryDecodeDetachedSignature(signatureFileBytes, out var signatureBytes)) return Refuse(PhysicalTrialRefusals.SignatureRejected);
        if (!VerifyDetachedSignature(buildIdentity, payloadBytes, signatureBytes)) return Refuse(PhysicalTrialRefusals.SignatureRejected);

        if (!string.Equals(authorization.SchemaVersion, PhysicalAuthorization.SchemaVersionValue, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.AuthorizationRejected);
        if (authorization.ValidFrom.Offset != TimeSpan.Zero || authorization.ValidUntil.Offset != TimeSpan.Zero) return Refuse(PhysicalTrialRefusals.AuthorizationRejected);
        if (authorization.ValidUntil <= authorization.ValidFrom) return Refuse(PhysicalTrialRefusals.AuthorizationRejected);
        var now = DateTimeOffset.UtcNow;
        if (now < authorization.ValidFrom) return Refuse(PhysicalTrialRefusals.NotYetValid);
        if (now > authorization.ValidUntil) return Refuse(PhysicalTrialRefusals.Expired);

        if (!string.Equals(authorization.ImplementationSha, buildIdentity.ImplementationSha, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.ImplementationMismatch);
        if (!string.Equals(authorization.ApprovedBaseSha, buildIdentity.ApprovedBaseSha, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.BaseMismatch);
        if (!string.Equals(authorization.SourceRef, buildIdentity.SourceRef, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.SourceRefMismatch);
        if (!string.Equals(authorization.BuildIdentityDigest, buildIdentity.Digest, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.BuildIdentityMismatch);

        if (!string.Equals(authorization.TrialId, "T01", StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.AuthorizationRejected);
        TrialConfiguration trial;
        CatalogEntry entry;
        try
        {
            entry = catalog.Select(authorization.TrialId);
            trial = entry.Trial;
        }
        catch (InvalidDataException)
        {
            return Refuse(PhysicalTrialRefusals.AuthorizationRejected);
        }
        if (!string.Equals(authorization.ManifestSha256, entry.ManifestSha256, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.ManifestMismatch);
        if (!MatchesTrustedTuple(authorization, trial)) return Refuse(PhysicalTrialRefusals.TupleMismatch);
        if (!string.Equals(authorization.Scope, PhysicalAuthorization.ScopeValue, StringComparison.Ordinal)
            || !authorization.OneInvocation || !authorization.OneConnectAttempt || !authorization.AtMostOneRead) return Refuse(PhysicalTrialRefusals.AuthorizationRejected);
        if (!AllNeutralIdentifiersValid(authorization)) return Refuse(PhysicalTrialRefusals.AuthorizationRejected);

        var payloadSha256 = TrialCatalog.Hash(payloadText);
        TcNodeEndpoint endpoint;
        try
        {
            endpoint = trial.Endpoint;
            endpoint.Validate();
        }
        catch (ArgumentException)
        {
            return Refuse(PhysicalTrialRefusals.AuthorizationRejected);
        }

        var consumption = ConsumeAuthorization(authorizationPath, authorization.AuthorizationId, payloadSha256, buildIdentity.ImplementationSha, authorization.TrialId);
        if (string.Equals(consumption, PhysicalTrialStates.AlreadyConsumed, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.AlreadyConsumed);
        if (string.Equals(consumption, PhysicalTrialStates.Unavailable, StringComparison.Ordinal)) return Refuse(PhysicalTrialRefusals.ConsumptionUnavailable);

        return new PhysicalTrialDecision
        {
            RefusalCode = PhysicalTrialRefusals.NotRefused,
            Plan = PhysicalTrialPlan.Create(catalog, trial, endpoint, buildIdentity, authorization.AuthorizationId, authorization.OwnerReference, payloadSha256, buildIdentity.AuthorizationKeyId),
        };
    }

    /// <summary>
    /// The exact canonical payload bytes: UTF-8, no BOM, fixed property order, compact JSON, exactly
    /// one trailing LF. This is the only byte representation that may be signed or verified.
    /// </summary>
    public static byte[] CanonicalPayloadBytes(PhysicalAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(CanonicalPayloadText(authorization));
    }

    internal static string CanonicalPayloadText(PhysicalAuthorization authorization) =>
        JsonSerializer.Serialize(authorization, StrictJson.Options) + "\n";

    /// <summary>
    /// Verifies a detached signature over the exact supplied bytes using the build-bound trusted
    /// public key. Platform cryptography only: ECDSA over NIST P-256 with SHA-256 and the fixed
    /// IEEE P1363 r||s signature encoding. No third-party dependency and no algorithm fallback.
    /// </summary>
    public static bool VerifyDetachedSignature(BuildIdentity buildIdentity, byte[] payloadBytes, byte[] signatureBytes)
    {
        ArgumentNullException.ThrowIfNull(buildIdentity);
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentNullException.ThrowIfNull(signatureBytes);
        if (signatureBytes.Length != DetachedSignatureLength) return false;
        if (!BuildIdentity.TryDecodeVerificationKey(buildIdentity.OwnerPublicKey, out var subjectPublicKeyInfo)) return false;
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);
            return ecdsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Atomically consumes the authorization using create-new semantics. Exactly one caller can win;
    /// a pre-existing marker, a crash after consumption and a failed, stopped or rejected trial all
    /// leave the authorization consumed. The marker lives beside the artifact, outside the working
    /// tree, and contains allowlisted neutral fields only.
    /// </summary>
    internal static string ConsumeAuthorization(string authorizationPath, string authorizationId, string payloadSha256, string implementationSha, string trialId)
    {
        string directory;
        string markerPath;
        try
        {
            var full = Path.GetFullPath(authorizationPath);
            directory = Path.GetDirectoryName(full) ?? string.Empty;
            markerPath = Path.Combine(directory, ConsumptionDirectoryName, authorizationId + ConsumptionMarkerSuffix);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PhysicalTrialStates.Unavailable;
        }
        if (directory.Length == 0) return PhysicalTrialStates.Unavailable;
        if (File.Exists(markerPath)) return PhysicalTrialStates.AlreadyConsumed;

        FileStream stream;
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, ConsumptionDirectoryName));
            stream = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return File.Exists(markerPath) ? PhysicalTrialStates.AlreadyConsumed : PhysicalTrialStates.Unavailable;
        }

        using (stream)
        {
            var record = new AuthorizationConsumptionRecord
            {
                AuthorizationId = authorizationId,
                AuthorizationPayloadSha256 = payloadSha256,
                ImplementationSha = implementationSha,
                TrialId = trialId,
                ConsumedAtUtc = DateTimeOffset.UtcNow,
                InvocationId = Guid.NewGuid().ToString("N"),
            };
            try
            {
                stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(JsonSerializer.Serialize(record, StrictJson.Options) + "\n"));
                stream.Flush();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The marker exists, so the authorization is consumed whether or not the record is complete.
            }
        }
        return PhysicalTrialStates.Consumed;
    }

    /// <summary>
    /// True when the path resolves inside a Git working tree. Checked without invoking Git and without
    /// reading repository metadata: authorization material and consumption markers must live outside
    /// the tracked tree, so a checkout-local artifact is refused rather than used.
    /// </summary>
    internal static bool IsInsideWorkingTree(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return true;
        }
        var directory = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
        var info = string.IsNullOrEmpty(directory) ? null : new DirectoryInfo(directory);
        for (var depth = 0; depth < MaximumRepositoryProbeDepth && info is not null; depth++)
        {
            var marker = Path.Combine(info.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker)) return true;
            info = info.Parent;
        }
        return false;
    }

    private static PhysicalTrialDecision Refuse(string code) => new() { RefusalCode = code };

    private static bool MatchesTrustedTuple(PhysicalAuthorization authorization, TrialConfiguration trial) =>
        string.Equals(authorization.NodeId, trial.NodeId, StringComparison.Ordinal)
        && string.Equals(authorization.CabinetId, trial.CabinetId, StringComparison.Ordinal)
        && string.Equals(authorization.Address, trial.Address, StringComparison.Ordinal)
        && authorization.Port == trial.Port
        && authorization.FunctionCode == trial.FunctionCode
        && authorization.UnitId == trial.UnitId
        && authorization.Start == trial.Start
        && authorization.Quantity == trial.Quantity
        && string.Equals(authorization.Endianness, trial.Endianness, StringComparison.Ordinal)
        && string.Equals(trial.PhysicalVerification, "NOT_VERIFIED", StringComparison.Ordinal);

    private static bool AllNeutralIdentifiersValid(PhysicalAuthorization authorization) =>
        TrialEvidence.SafeIdentifier(authorization.AuthorizationId)
        && TrialEvidence.SafeIdentifier(authorization.Nonce)
        && TrialEvidence.SafeIdentifier(authorization.OwnerId)
        && TrialEvidence.SafeIdentifier(authorization.OwnerReference)
        && TrialEvidence.SafeIdentifier(authorization.IsolationPlanRef)
        && TrialEvidence.SafeIdentifier(authorization.WireEvidencePlanRef)
        && TrialEvidence.SafeIdentifier(authorization.EvidenceDestinationRef)
        && TrialEvidence.SafeIdentifier(authorization.StopProcedureRef)
        && SourceRefPolicy.IsValid(authorization.SourceRef)
        && StrictJson.IsHash(authorization.ManifestSha256)
        && StrictJson.IsHash(authorization.BuildIdentityDigest);

    private static bool TryReadArtifactBytes(string path, int maximumLength, out byte[] bytes)
    {
        bytes = [];
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > maximumLength) return false;
            bytes = File.ReadAllBytes(path);
            return bytes.Length > 0 && bytes.Length <= maximumLength;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Detached signature files are base64 text of exactly 64 bytes with at most one trailing newline
    /// and no other whitespace. Any other encoding, length or content is refused.
    /// </summary>
    private static bool TryDecodeDetachedSignature(byte[] fileBytes, out byte[] signatureBytes)
    {
        signatureBytes = [];
        if (fileBytes.Length == 0 || fileBytes.Length > MaximumSignatureFileLength) return false;
        var text = Encoding.ASCII.GetString(fileBytes).TrimEnd('\r', '\n');
        if (text.Length == 0 || text.Length % 4 != 0) return false;
        foreach (var character in text)
        {
            if (!char.IsAscii(character) || char.IsWhiteSpace(character)) return false;
        }
        try
        {
            signatureBytes = Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            return false;
        }
        return signatureBytes.Length == DetachedSignatureLength;
    }
}
