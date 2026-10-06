using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wjss.FixtureEmission.Tests;

/// <summary>
/// Semantic JSON equality: object keys are order-insensitive, arrays are
/// order-sensitive, numbers compare by invariant value (so "2" and "2.0" are
/// equal), strings/null/bool compare exactly. Used only by the fixture parity
/// tests; it is not part of any product contract.
/// </summary>
internal static class JsonCompare
{
    public static bool Equal(JsonNode? left, JsonNode? right) => NodeEqual(left, right);

    private static bool ObjectEqual(JsonObject l, JsonObject r)
    {
        if (l.Count != r.Count)
        {
            return false;
        }

        foreach (var (key, value) in l)
        {
            if (!r.TryGetPropertyValue(key, out var other) || !NodeEqual(value, other))
            {
                return false;
            }
        }

        return true;
    }

    private static bool NodeEqual(JsonNode? l, JsonNode? r)
    {
        if (l is null || r is null)
        {
            return l is null && r is null;
        }

        if (l is JsonObject lo)
        {
            return r is JsonObject ro && ObjectEqual(lo, ro);
        }

        if (l is JsonArray la)
        {
            if (r is not JsonArray ra || la.Count != ra.Count)
            {
                return false;
            }

            for (var i = 0; i < la.Count; i++)
            {
                if (!NodeEqual(la[i], ra[i]))
                {
                    return false;
                }
            }

            return true;
        }

        if (r is JsonObject or JsonArray)
        {
            return false;
        }

        // Both are JsonValue. Parse-backed values expose a JsonElement; fall
        // back to rendered text for hand-created values.
        if (l.AsValue().TryGetValue(out JsonElement le) && r.AsValue().TryGetValue(out JsonElement re))
        {
            return ElementEqual(le, re);
        }

        return l.ToJsonString() == r.ToJsonString();
    }

    private static bool ElementEqual(JsonElement l, JsonElement r)
    {
        if (l.ValueKind != r.ValueKind)
        {
            return false;
        }

        if (l.ValueKind != JsonValueKind.Number)
        {
            return l.GetRawText() == r.GetRawText();
        }

        if (l.TryGetInt64(out var li) && r.TryGetInt64(out var ri))
        {
            return li == ri;
        }

        var lv = double.Parse(l.GetRawText(), CultureInfo.InvariantCulture);
        var rv = double.Parse(r.GetRawText(), CultureInfo.InvariantCulture);
        return lv == rv;
    }
}
