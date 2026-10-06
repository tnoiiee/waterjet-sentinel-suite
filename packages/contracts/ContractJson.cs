using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wjss.Contracts;

/// <summary>
/// Shared serialization settings for every WJSS structural contract. Golden
/// fixtures under <c>packages/contracts/fixtures/</c> are produced and compared
/// with these exact settings so that .NET output and the TypeScript mirror agree.
/// </summary>
public static class ContractJson
{
    /// <summary>
    /// Camel-case property names, enum-as-string, indentation for reviewability.
    /// Nulls are written explicitly: the Delta semantics use an ABSENT KEY for
    /// "unchanged", so a serialized null must mean "cleared", never "omitted".
    /// </summary>
    public static readonly JsonSerializerOptions Options = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        // Enum member names are serialized verbatim (UPPER_SNAKE), matching the
        // accepted presentation-contract vocabulary (e.g. SIMULATOR, DIRTY, SR2).
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
