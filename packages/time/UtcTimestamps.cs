using System.Globalization;

namespace Wjss.Time;

/// <summary>
/// The single wire encoding for presentation timestamps: UTC, millisecond
/// precision, literal <c>Z</c> suffix. Both the Equipment Runtime and the
/// SIMULATOR adapter stamp contract fields through this helper, so a synthetic
/// value and the revision it belongs to are written by one rule (and the output
/// matches the accepted fixture encoding, e.g. <c>2026-10-07T00:00:00.000Z</c>).
/// A non-UTC input is converted before formatting; the encoding never carries a
/// local offset.
/// </summary>
public static class UtcTimestamps
{
    /// <summary>Round-trip-stable ISO-8601 shape with millisecond precision and a literal Z.</summary>
    public const string MillisecondFormat = "yyyy-MM-ddTHH:mm:ss.fff'Z'";

    /// <summary>Formats one instant in the wire encoding. Culture-invariant and deterministic.</summary>
    public static string Format(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString(MillisecondFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses the wire encoding back to an instant. Only the exact millisecond
    /// shape is accepted: anything else (another shape, a local offset, a missing
    /// literal Z) is rejected rather than reinterpreted, so a malformed payload
    /// cannot silently become a plausible timestamp. Culture-invariant and
    /// deterministic; the result is always UTC.
    /// </summary>
    public static bool TryParse(string? text, out DateTimeOffset instant)
    {
        if (!string.IsNullOrWhiteSpace(text)
            && DateTimeOffset.TryParseExact(
                text,
                MillisecondFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out instant))
        {
            return true;
        }

        instant = default;
        return false;
    }
}
