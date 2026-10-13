using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wjss.Adapters.TcAcquisition;

namespace Wjss.TcBench;

public enum BenchMode { SIMULATION, PHYSICAL_TEST }

/// <summary>
/// Immutable trial configuration projected from the canonical TC Node configuration by the offline
/// exporter and embedded in the reviewed build. The tuple is a candidate representation only: it
/// carries no execution authority and no authorization digest. Physical authority comes solely from
/// the build identity plus the detached Owner authorization.
/// </summary>
public sealed record TrialConfiguration
{
    public required string TrialId { get; init; }
    public required string NodeId { get; init; }
    public required string CabinetId { get; init; }
    public required string Address { get; init; }
    public required int Port { get; init; }
    public required int FunctionCode { get; init; }
    public required byte UnitId { get; init; }
    public required ushort Start { get; init; }
    public required ushort Quantity { get; init; }
    public required string Endianness { get; init; }
    public required string ConnectionSettingBasis { get; init; }
    public required string PhysicalVerification { get; init; }

    [JsonIgnore]
    public TcNodeEndpoint Endpoint => new() { NodeId = NodeId, IpAddress = Address, Port = Port, UnitIdentifier = UnitId };
}

public sealed record CatalogEntry
{
    public required string ManifestSha256 { get; init; }
    public required TrialConfiguration Trial { get; init; }
}

public sealed record TrialCatalog
{
    public required string SchemaVersion { get; init; }
    public required string SourceSha256 { get; init; }
    public required string ConfigurationSha256 { get; init; }
    public required CatalogEntry[] Trials { get; init; }

    public static TrialCatalog LoadTrusted()
    {
        var assembly = typeof(TrialCatalog).Assembly;
        using var data = assembly.GetManifestResourceStream("TcTrialCatalog.json") ?? throw new InvalidDataException("CATALOG_MISSING");
        using var digest = assembly.GetManifestResourceStream("TcTrialCatalog.sha256") ?? throw new InvalidDataException("CATALOG_DIGEST_MISSING");
        using var reader = new StreamReader(data, Encoding.UTF8);
        using var digestReader = new StreamReader(digest, Encoding.UTF8);
        var bytes = reader.ReadToEnd();
        if (!string.Equals(Hash(bytes), digestReader.ReadToEnd().Trim(), StringComparison.Ordinal)) throw new InvalidDataException("CATALOG_HASH_MISMATCH");
        var catalog = StrictJson.Read<TrialCatalog>(bytes);
        if (catalog.SchemaVersion != "wjss.tc-bench-catalog/2" || catalog.Trials.Length != 1
            || !StrictJson.IsHash(catalog.SourceSha256) || !StrictJson.IsHash(catalog.ConfigurationSha256)) throw new InvalidDataException("CATALOG_SHAPE_REJECTED");
        catalog.Select("T01");
        return catalog;
    }

    public CatalogEntry Select(string trialId)
    {
        if (Trials.Length != 1 || trialId != "T01") throw new InvalidDataException("ONE_TRIAL_ONLY");
        var entry = Trials[0];
        var t = entry.Trial;
        if (t.TrialId != trialId || t.NodeId != "TC-NODE-01" || t.CabinetId != "CAB1"
            || t.Port != 502 || t.FunctionCode != 3 || t.UnitId != 0 || t.Start != 0 || t.Quantity != 40
            || t.Endianness != "BigEndian" || t.ConnectionSettingBasis != "OWNER_AUTHORIZED_BENCH_SETTING"
            || t.PhysicalVerification != "NOT_VERIFIED" || !StrictJson.IsHash(entry.ManifestSha256)) throw new InvalidDataException("TRIAL_POLICY_REJECTED");
        t.Endpoint.Validate();
        if (!System.Net.IPAddress.TryParse(t.Address, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) throw new InvalidDataException("ADDRESS_REJECTED");
        return entry;
    }

    public TrialConfiguration ValidateManifest(string trialId, string bytes)
    {
        var trusted = Select(trialId);
        if (!string.Equals(Hash(bytes), trusted.ManifestSha256, StringComparison.Ordinal)) throw new InvalidDataException("MANIFEST_HASH_MISMATCH");
        var supplied = StrictJson.Read<TrialConfiguration>(bytes);
        if (supplied != trusted.Trial) throw new InvalidDataException("EXACT_TUPLE_REQUIRED");
        return trusted.Trial;
    }

    public static string Hash(string bytes) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(bytes)));
}

/// <summary>
/// Frozen typed parser for the constrained command line. There is no endpoint switch, no listener,
/// no environment activation and no acceptance command. An authorization and a detached signature
/// are accepted only together and only for the physical-test selector; neither can grant authority
/// on its own and neither can be supplied for a dry run.
/// </summary>
public sealed record BenchArguments(string TrialId, BenchMode Mode, string? ManifestPath, string? AuthorizationPath, string? SignaturePath)
{
    public static BenchArguments Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var trialId = "T01";
        var mode = BenchMode.SIMULATION;
        string? manifest = null;
        string? authorization = null;
        string? signature = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Length; i++)
        {
            var key = arguments[i];
            if (!seen.Add(key)) throw new ArgumentException("DUPLICATE_ARGUMENT");
            switch (key)
            {
                case "--dry-run": mode = BenchMode.SIMULATION; break;
                case "--physical-test": mode = BenchMode.PHYSICAL_TEST; break;
                case "--trial":
                case "--manifest":
                case "--authorization":
                case "--signature":
                    if (++i >= arguments.Length || arguments[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("MISSING_ARGUMENT");
                    if (key == "--trial") trialId = arguments[i];
                    else if (key == "--manifest") manifest = arguments[i];
                    else if (key == "--authorization") authorization = arguments[i];
                    else signature = arguments[i];
                    break;
                default: throw new ArgumentException("UNAPPROVED_ARGUMENT");
            }
        }
        if (seen.Contains("--dry-run") && seen.Contains("--physical-test")) throw new ArgumentException("CONFLICTING_MODE");
        if (trialId != "T01") throw new ArgumentException("UNAPPROVED_SELECTION");
        if ((authorization is not null || signature is not null) && mode != BenchMode.PHYSICAL_TEST) throw new ArgumentException("UNAPPROVED_SELECTION");
        if (signature is not null && authorization is null) throw new ArgumentException("UNAPPROVED_SELECTION");
        return new(trialId, mode, manifest, authorization, signature);
    }
}

public static class StrictJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static T Read<T>(string bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        RejectDuplicates(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new InvalidDataException("NULL_DOCUMENT");
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("DUPLICATE_FIELD");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }

    public static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => char.IsAsciiHexDigit(c) && !char.IsAsciiLetterUpper(c));

    /// <summary>Commit identity: exactly 40 lowercase hexadecimal characters. No case folding is applied.</summary>
    public static bool IsCommit(string? value) => value is { Length: 40 } && value.All(c => char.IsAsciiHexDigit(c) && !char.IsAsciiLetterUpper(c));
}
