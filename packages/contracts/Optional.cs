using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wjss.Contracts;

/// <summary>
/// A three-state presence wrapper: ABSENT (the JSON key is omitted),
/// CLEARED (the JSON key is written as an explicit null), or PRESENT
/// (the JSON key carries the value).
///
/// This is a STRUCTURAL CONTRACT TECHNIQUE, not a production policy. It
/// exists because the accepted Delta baseline requires all three states to
/// be distinguishable for <c>activeJob</c> (absent = unchanged, object =
/// replace, explicit null = clear), and a bare nullable property cannot
/// express "not set" separately from "set to null". In this checkpoint the
/// wrapper is applied to <see cref="OperationalDelta.ActiveJob"/> ONLY;
/// no other field gains clear semantics here.
/// </summary>
public readonly struct Optional<T> : IEquatable<Optional<T>>
    where T : class
{
    private enum Presence : byte
    {
        /// <summary>Key omitted: unchanged.</summary>
        Absent = 0,

        /// <summary>Key present with an explicit null: cleared.</summary>
        Cleared = 1,

        /// <summary>Key present with a value: replaced.</summary>
        Present = 2,
    }

    private readonly T? _value;
    private readonly Presence _presence;

    private Optional(T? value, Presence presence)
    {
        _value = value;
        _presence = presence;
    }

    /// <summary>The default state: the key is omitted from the payload.</summary>
    public static Optional<T> Absent => default;

    /// <summary>Serializes to an explicit <c>null</c> (the value is cleared).</summary>
    public static Optional<T> Cleared => new(null, Presence.Cleared);

    /// <summary>Serializes to the object (the value replaces whatever was there).</summary>
    public static Optional<T> Present(T value) =>
        value is null ? throw new ArgumentNullException(nameof(value)) : new(value, Presence.Present);

    public bool IsAbsent => _presence == Presence.Absent;

    public bool IsCleared => _presence == Presence.Cleared;

    public bool IsPresent => _presence == Presence.Present;

    /// <summary>The value; only valid when <see cref="IsPresent"/>.</summary>
    public T Value => _presence == Presence.Present
        ? _value!
        : throw new InvalidOperationException($"Optional is {_presence}; no value is available.");

    // Present payloads delegate to EqualityComparer<T>.Default (Owner-local
    // round 5, CA-corrected semantics): value equality for record types,
    // reference equality for types that do not override equality. A plain
    // `_value == other._value` would compile to reference comparison for the
    // unconstrained T and wrongly split equal records. No recursive custom
    // deep equality is invented here - each payload keeps its own .NET
    // equality semantics.
    public bool Equals(Optional<T> other) =>
        _presence == other._presence &&
        (_presence != Presence.Present || EqualityComparer<T>.Default.Equals(_value, other._value));

    public override bool Equals(object? obj) => obj is Optional<T> other && Equals(other);

    // CA2231 (Owner-local build 2026-10-07): the operators are exactly the
    // Equals semantics - no independent behaviour, no wire or hash change.
    public static bool operator ==(Optional<T> left, Optional<T> right) =>
        left.Equals(right);

    public static bool operator !=(Optional<T> left, Optional<T> right) =>
        !left.Equals(right);

    // Hash follows the same comparer so equal values hash equal:
    // EqualityComparer<T>.Default.GetHashCode honours the payload's own
    // GetHashcode override (records) or its reference hash (others).
    public override int GetHashCode() =>
        _presence switch
        {
            Presence.Absent => 0,
            Presence.Cleared => 1,
            _ => HashCode.Combine(Presence.Present, EqualityComparer<T>.Default.GetHashCode(_value!)),
        };

    public override string ToString() => _presence switch
    {
        Presence.Absent => "Absent",
        Presence.Cleared => "Cleared(null)",
        _ => $"Present({_value})",
    };
}

/// <summary>
/// Scoped converter implementing the <see cref="Optional{T}"/> wire encoding
/// for the property it is attributed to (never registered globally — other
/// fields keep their existing semantics). <see cref="HandleNull"/> ensures
/// the explicit-null (clear) token reaches the converter instead of being
/// short-circuited by the serializer.
/// </summary>
public sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    where T : class
{
    public override bool HandleNull => true;

    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            // The reader is already positioned on the single Null token;
            // returning here leaves it at the last token of the value.
            return Optional<T>.Cleared;
        }

        var value = JsonSerializer.Deserialize<T>(ref reader, options);
        return value is null
            ? throw new JsonException("A present Optional must deserialize to an object, not null.")
            : Optional<T>.Present(value);
    }

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        if (value.IsCleared)
        {
            writer.WriteNullValue();
            return;
        }

        if (!value.IsPresent)
        {
            throw new JsonException("An Absent Optional must be omitted by the serializer and never written.");
        }

        JsonSerializer.Serialize(writer, value.Value, options);
    }
}
