using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Wjss.TcBench;

/// <summary>
/// Immutable build-time deployment identity for the isolated one-shot TC bench.
///
/// The identity is generated into ignored build output at build time (see Wjss.TcBench.csproj) and
/// embedded as the fixed resource <c>TcBenchBuildIdentity.txt</c>. It is the ONLY place the
/// deployment identity comes from: the executable never reads Git metadata at run time, never
/// invokes Git, never infers identity from a branch name and never accepts identity from an
/// authorization artifact.
///
/// Default repository builds deliberately carry placeholders and
/// <see cref="PhysicalCapabilityEnabled"/> false, so a normal build cannot construct a physical
/// client. A physical-capable deployment build must be produced with explicit trust metadata; the
/// metadata generator, the loader and <see cref="ValidateForPhysicalUse"/> all fail closed on
/// missing, malformed, placeholder or inconsistent values.
///
/// Hashes are not signatures: this identity proves what was built, not who authorized it.
/// </summary>
public sealed record BuildIdentity
{
    /// <summary>The single placeholder used to express "not provided" in the generated resource.</summary>
    public const string NotProvided = "NOT_PROVIDED";

    /// <summary>The only accepted generated-metadata schema for this Stage.</summary>
    public const string SchemaVersionValue = "wjss.tc-bench-build/1";

    /// <summary>The only accepted deployment-proof value; any other value is refused.</summary>
    public const string DeploymentProofClean = "CLEAN_OWNER_VERIFIED";

    /// <summary>OID of NIST P-256 (secp256r1). Used only to confirm the agreed signature profile.</summary>
    private const string NistP256Oid = "1.2.840.10045.3.1.7";

    /// <summary>Field order of the canonical identity text. Fixed; changing it changes the digest.</summary>
    private static readonly string[] RequiredKeys =
    [
        "schemaVersion", "physicalCapabilityEnabled", "deploymentProof", "approvedBaseSha",
        "implementationSha", "sourceRef", "authorizationKeyId", "ownerPublicKey",
    ];

    public required string SchemaVersion { get; init; }

    /// <summary>True only for an explicitly provisioned physical-capable deployment build.</summary>
    public required bool PhysicalCapabilityEnabled { get; init; }

    /// <summary>Deployment-proof statement asserted by the Owner at build time. Not a signature.</summary>
    public required string DeploymentProof { get; init; }

    /// <summary>Approved base commit this build is derived from. Must be 40 lowercase hex characters.</summary>
    public required string ApprovedBaseSha { get; init; }

    /// <summary>Reviewed implementation commit. Must be 40 lowercase hex characters for physical use.</summary>
    public required string ImplementationSha { get; init; }

    /// <summary>Reviewed source reference (branch or tag), validated by <see cref="SourceRefPolicy"/>.</summary>
    public required string SourceRef { get; init; }

    /// <summary>Identifier of the Owner authorization key trusted by this build.</summary>
    public required string AuthorizationKeyId { get; init; }

    /// <summary>Base64 SubjectPublicKeyInfo of the trusted NIST P-256 Owner verification key.</summary>
    public required string OwnerPublicKey { get; init; }

    /// <summary>Deterministic identity digest over the canonical text. Independent of line endings.</summary>
    public string Digest => TrialCatalog.Hash(CanonicalText());

    /// <summary>
    /// The exact canonical identity text: fixed field order, UTF-8, LF-separated, one trailing LF,
    /// no BOM. The digest is taken over exactly these bytes, so generated-file line endings, BOM and
    /// whitespace cannot alter the recorded deployment identity.
    /// </summary>
    public string CanonicalText()
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in Fields(this)) builder.Append(key).Append('=').Append(value).Append('\n');
        return builder.ToString();
    }

    private static (string Key, string Value)[] Fields(BuildIdentity identity) =>
    [
        ("schemaVersion", identity.SchemaVersion),
        ("physicalCapabilityEnabled", identity.PhysicalCapabilityEnabled ? "true" : "false"),
        ("deploymentProof", identity.DeploymentProof),
        ("approvedBaseSha", identity.ApprovedBaseSha),
        ("implementationSha", identity.ImplementationSha),
        ("sourceRef", identity.SourceRef),
        ("authorizationKeyId", identity.AuthorizationKeyId),
        ("ownerPublicKey", identity.OwnerPublicKey),
    ];

    /// <summary>
    /// Loads and validates the embedded build identity. Throws <see cref="InvalidDataException"/> when
    /// the resource is missing, malformed, contains unknown or duplicate fields, or carries
    /// inconsistent physical trust metadata. Failure is the safe outcome: no client can be built.
    /// </summary>
    public static BuildIdentity LoadTrusted()
    {
        var assembly = typeof(BuildIdentity).Assembly;
        using var stream = assembly.GetManifestResourceStream("TcBenchBuildIdentity.txt")
            ?? throw new InvalidDataException("BUILD_IDENTITY_MISSING");
        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        var identity = Parse(reader.ReadToEnd());
        identity.RequireValid();
        return identity;
    }

    /// <summary>Strict <c>key=value</c> parser. Unknown, duplicate, missing or malformed lines are refused.</summary>
    internal static BuildIdentity Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        // A byte-order mark written by a build tool is stripped before parsing. It carries no meaning
        // here: the identity digest is taken over CanonicalText(), never over the generated file bytes.
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0) continue;
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == line.Length - 1) throw new InvalidDataException("BUILD_IDENTITY_MALFORMED");
            if (!values.TryAdd(line[..separator], line[(separator + 1)..])) throw new InvalidDataException("BUILD_IDENTITY_DUPLICATE");
        }
        foreach (var key in values.Keys)
        {
            if (Array.IndexOf(RequiredKeys, key) < 0) throw new InvalidDataException("BUILD_IDENTITY_UNKNOWN_FIELD");
        }
        foreach (var key in RequiredKeys)
        {
            if (!values.ContainsKey(key)) throw new InvalidDataException("BUILD_IDENTITY_MISSING_FIELD");
        }
        var capability = values["physicalCapabilityEnabled"];
        if (capability != "true" && capability != "false") throw new InvalidDataException("BUILD_IDENTITY_MALFORMED");
        foreach (var value in values.Values)
        {
            if (value.Any(char.IsControl)) throw new InvalidDataException("BUILD_IDENTITY_MALFORMED");
        }
        return new BuildIdentity
        {
            SchemaVersion = values["schemaVersion"],
            PhysicalCapabilityEnabled = capability == "true",
            DeploymentProof = values["deploymentProof"],
            ApprovedBaseSha = values["approvedBaseSha"],
            ImplementationSha = values["implementationSha"],
            SourceRef = values["sourceRef"],
            AuthorizationKeyId = values["authorizationKeyId"],
            OwnerPublicKey = values["ownerPublicKey"],
        };
    }

    /// <summary>Shape validation applied to every loaded identity, physical or not.</summary>
    internal void RequireValid()
    {
        if (SchemaVersion != SchemaVersionValue) throw new InvalidDataException("BUILD_IDENTITY_SCHEMA_REJECTED");
        if (!StrictJson.IsCommit(ApprovedBaseSha)) throw new InvalidDataException("BUILD_IDENTITY_BASE_REJECTED");
        if (PhysicalCapabilityEnabled)
        {
            if (ValidateForPhysicalUse(this) is not null) throw new InvalidDataException("BUILD_IDENTITY_PHYSICAL_METADATA_REJECTED");
            return;
        }
        if (!IsPlaceholder(ImplementationSha) && !StrictJson.IsCommit(ImplementationSha)) throw new InvalidDataException("BUILD_IDENTITY_IMPLEMENTATION_REJECTED");
        if (!IsPlaceholder(DeploymentProof) && DeploymentProof != DeploymentProofClean) throw new InvalidDataException("BUILD_IDENTITY_PROOF_REJECTED");
        if (!IsPlaceholder(SourceRef) && !SourceRefPolicy.IsValid(SourceRef)) throw new InvalidDataException("BUILD_IDENTITY_SOURCE_REJECTED");
        if (!IsPlaceholder(AuthorizationKeyId) && !TrialEvidence.SafeIdentifier(AuthorizationKeyId)) throw new InvalidDataException("BUILD_IDENTITY_KEY_ID_REJECTED");
        if (!IsPlaceholder(OwnerPublicKey) && !TryDecodeVerificationKey(OwnerPublicKey, out _)) throw new InvalidDataException("BUILD_IDENTITY_PUBLIC_KEY_REJECTED");
    }

    /// <summary>
    /// Returns null when the identity may support a physical-capable deployment; otherwise the fixed
    /// refusal code. Placeholders, a non-clean deployment proof and a missing or malformed
    /// verification key are all refused before any client construction.
    /// </summary>
    internal static string? ValidateForPhysicalUse(BuildIdentity? identity)
    {
        if (identity is null) return PhysicalTrialRefusals.BuildIdentityRejected;
        if (!identity.PhysicalCapabilityEnabled) return PhysicalTrialRefusals.PhysicalCapabilityDisabled;
        if (identity.SchemaVersion != SchemaVersionValue) return PhysicalTrialRefusals.BuildIdentityRejected;
        if (!StrictJson.IsCommit(identity.ApprovedBaseSha) || !StrictJson.IsCommit(identity.ImplementationSha)) return PhysicalTrialRefusals.BuildIdentityRejected;
        if (identity.DeploymentProof != DeploymentProofClean) return PhysicalTrialRefusals.BuildIdentityRejected;
        if (!SourceRefPolicy.IsValid(identity.SourceRef) || identity.SourceRef == NotProvided) return PhysicalTrialRefusals.BuildIdentityRejected;
        if (!TrialEvidence.SafeIdentifier(identity.AuthorizationKeyId) || identity.AuthorizationKeyId == NotProvided) return PhysicalTrialRefusals.BuildIdentityRejected;
        if (identity.OwnerPublicKey == NotProvided) return PhysicalTrialRefusals.PublicKeyRejected;
        if (!TryDecodeVerificationKey(identity.OwnerPublicKey, out _)) return PhysicalTrialRefusals.PublicKeyRejected;
        return null;
    }

    internal static bool IsPlaceholder(string? value) => string.Equals(value, NotProvided, StringComparison.Ordinal);

    /// <summary>
    /// Decodes and structurally validates a base64 SubjectPublicKeyInfo as a NIST P-256 key. Platform
    /// cryptography only; no third-party dependency. The curve is confirmed by key size and, where the
    /// platform reports it, by the P-256 object identifier.
    /// </summary>
    internal static bool TryDecodeVerificationKey(string? value, out byte[] subjectPublicKeyInfo)
    {
        subjectPublicKeyInfo = [];
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512) return false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character) || !char.IsAscii(character)) return false;
        }
        try
        {
            subjectPublicKeyInfo = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return false;
        }
        try
        {
            using var ecdsa = ECDsa.Create();
            var consumed = 0;
            ecdsa.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out consumed);
            if (consumed != subjectPublicKeyInfo.Length || ecdsa.KeySize != 256) return false;
            var oid = ecdsa.ExportParameters(includePrivateParameters: false).Curve.Oid.Value;
            return string.IsNullOrEmpty(oid) || string.Equals(oid, NistP256Oid, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}

/// <summary>
/// The single accepted form of a source reference. Narrow, length-bounded and free of path
/// separators, so an arbitrary branch string, a URL, a drive letter or a local path cannot be
/// carried into build metadata, an authorization payload or public evidence.
/// </summary>
public static class SourceRefPolicy
{
    /// <summary>The fixed placeholder meaning "no source reference is provided".</summary>
    public const string NotProvided = BuildIdentity.NotProvided;

    public static bool IsValid(string? value)
    {
        if (value is null) return false;
        if (!Regex.IsMatch(value, "\\A[A-Za-z0-9][A-Za-z0-9._/_-]{0,119}\\z", RegexOptions.CultureInvariant)) return false;
        if (value.Contains("..", StringComparison.Ordinal)) return false;
        if (value.Contains("//", StringComparison.Ordinal)) return false;
        if (value.EndsWith('/')) return false;
        return true;
    }
}
