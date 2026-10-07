using System.Text.Json.Serialization;

namespace Wjss.Contracts;

/// <summary>
/// Structured shape of one sensor-map configuration EXAMPLE slot (the public-safe
/// example file at <c>config/examples/sensor-map.example.json</c>). This is an
/// example-contract record, not a runtime configuration type: the publication
/// pipeline is Stage 0.3A-F content.
/// Thermocouple channels are a structured array — exactly two entries per SENSOR
/// slot and never present on CANNON slots. A comma-delimited scalar string is not
/// a valid encoding and is rejected by deserialization itself (System.Text.Json
/// cannot bind a JSON string to <see cref="IReadOnlyList{T}"/>) and by every rule
/// in <see cref="TcChannelRules"/>.
/// </summary>
public sealed record SensorMapSlotExample
{
    public required string SlotId { get; init; }
    public required SlotType SlotType { get; init; }
    public required Wall Wall { get; init; }
    public required int LogicalColumn { get; init; }
    public required int LogicalRow { get; init; }
    public required int WallColumn { get; init; }
    public required int WallRow { get; init; }
    public string? SensorId { get; init; }
    public string? EquipmentId { get; init; }
    public string? LogicalLabel { get; init; }
    public int? ScanOrderSynthetic { get; init; }

    /// <summary>
    /// The two thermocouple channels for a SENSOR slot, as a JSON array of exactly
    /// two strings. Deterministic order: index 0 is the pair's lower channel index
    /// (front), index 1 the higher (rear). Absent on CANNON slots: the key is
    /// omitted (null is never written for this example-shape property), so a
    /// cannon can never smuggle in an empty array either.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? TcChannels { get; init; }

    public bool IsCannon => SlotType == SlotType.CANNON;
}

/// <summary>
/// The single encoding contract for <c>tcChannels</c> in sensor-map examples and
/// generated fixtures (Stage 0.3A-1 correction). Snapshot/delta presentation keeps
/// its existing separate <c>tcFrontChannel</c>/<c>tcRearChannel</c> fields; these
/// rules apply to the slot-array shape only.
/// </summary>
public static class TcChannelRules
{
    public const int ChannelsPerSensor = 2;
    public const int SensorSlotCount = CanonicalSensorMap.SensorLocations;          // 106
    public const int CannonSlotCount = CanonicalSensorMap.CannonSlotCount;          // 2
    public const int SlotCount = CanonicalSensorMap.MatrixSlots;                    // 108
    public const int TotalChannelCount = CanonicalSensorMap.ThermocoupleChannelCount; // 212

    /// <summary>A SENSOR slot carries exactly two distinct, non-blank channel strings.</summary>
    public static void RequireValidSensorChannels(IReadOnlyList<string>? channels, string slotId)
    {
        if (channels is null)
        {
            throw new ArgumentException($"Sensor slot {slotId} must carry a tcChannels array.", nameof(channels));
        }
        if (channels.Count != ChannelsPerSensor)
        {
            throw new ArgumentException(
                $"Sensor slot {slotId} must carry exactly {ChannelsPerSensor} thermocouple channels; got {channels.Count}.",
                nameof(channels));
        }
        var first = channels[0];
        var second = channels[1];
        if (first is null || second is null || first.Trim().Length == 0 || second.Trim().Length == 0)
        {
            throw new ArgumentException($"Sensor slot {slotId} has an empty thermocouple channel entry.", nameof(channels));
        }
        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Sensor slot {slotId} duplicates thermocouple channel {first}.", nameof(channels));
        }
    }

    /// <summary>A CANNON slot carries no thermocouple channels (absent or empty only).</summary>
    public static void RequireCannonCarriesNoChannels(IReadOnlyList<string>? channels, string slotId)
    {
        if (channels is { Count: > 0 })
        {
            throw new ArgumentException($"Cannon slot {slotId} must not carry thermocouple channels.", nameof(channels));
        }
    }

    /// <summary>
    /// Full mapping check: slot composition, per-slot channel shape, global channel
    /// uniqueness, and the canonical totals (108 slots, 106 sensors, 2 cannons,
    /// exactly 2 channels per sensor, 212 distinct channels).
    /// </summary>
    public static void RequireValidMapping(IReadOnlyList<SensorMapSlotExample> slots)
    {
        if (slots.Count != SlotCount)
        {
            throw new ArgumentException($"Expected {SlotCount} matrix slots; got {slots.Count}.", nameof(slots));
        }

        var seenChannels = new HashSet<string>(StringComparer.Ordinal);
        var sensors = 0;
        var cannons = 0;
        foreach (var slot in slots)
        {
            if (slot.IsCannon)
            {
                cannons++;
                RequireCannonCarriesNoChannels(slot.TcChannels, slot.SlotId);
            }
            else
            {
                sensors++;
                RequireValidSensorChannels(slot.TcChannels, slot.SlotId);
                foreach (var channel in slot.TcChannels!)
                {
                    if (!seenChannels.Add(channel))
                    {
                        throw new ArgumentException(
                            $"Thermocouple channel {channel} is assigned to more than one sensor slot.",
                            nameof(slots));
                    }
                }
            }
        }

        if (sensors != SensorSlotCount || cannons != CannonSlotCount)
        {
            throw new ArgumentException(
                $"Expected {SensorSlotCount} sensor and {CannonSlotCount} cannon slots; got {sensors} and {cannons}.",
                nameof(slots));
        }
        if (seenChannels.Count != TotalChannelCount)
        {
            throw new ArgumentException(
                $"Expected {TotalChannelCount} distinct thermocouple channels; got {seenChannels.Count}.",
                nameof(slots));
        }
    }
}
