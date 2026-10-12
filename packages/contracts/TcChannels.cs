using System.Text.Json.Serialization;

namespace Wjss.Contracts;

/// <summary>
/// Structured shape of one sensor-map configuration EXAMPLE slot (the public-safe
/// example file at <c>config/examples/sensor-map.example.json</c>). This is an
/// example-contract record, not a runtime configuration type: the publication
/// pipeline is Stage 0.3A-F content.
/// Thermocouple channels are a structured array — exactly two entries per slot, and every
/// slot is a SENSOR slot (Owner critical topology correction: 108 Sensors, I7 and I16
/// included; no NON_SENSOR_GAP slot exists). A comma-delimited scalar string is not
/// a valid encoding and is rejected by deserialization itself (System.Text.Json
/// cannot bind a JSON string to <see cref="IReadOnlyList{T}"/>) and by every rule
/// in <see cref="TcChannelRules"/>.
/// </summary>
public sealed record SensorMapSlotExample
{
    public required string SlotId { get; init; }

    /// <summary>Always SENSOR (all 108 logical positions are Product Sensors).</summary>
    public required LogicalPositionKind PositionKind { get; init; }
    public required Wall Wall { get; init; }
    public required int LogicalColumn { get; init; }
    public required int LogicalRow { get; init; }
    public required int WallColumn { get; init; }
    public required int WallRow { get; init; }
    public string? SensorId { get; init; }
    public string? LogicalLabel { get; init; }
    public int? ScanOrderSynthetic { get; init; }

    /// <summary>
    /// The two thermocouple channels of the Sensor slot, as a JSON array of exactly
    /// two strings. Deterministic order: index 0 is the pair's lower channel index
    /// (front), index 1 the higher (rear). Never absent on a valid slot; the
    /// null-omission attribute is retained only so that a malformed slot cannot smuggle
    /// in an explicit <c>null</c> token.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? TcChannels { get; init; }
}

/// <summary>
/// The single encoding contract for <c>tcChannels</c> in sensor-map examples and
/// generated fixtures (Stage 0.3A-1 correction). Snapshot/delta presentation keeps
/// its existing separate <c>tcFrontChannel</c>/<c>tcRearChannel</c> fields; these
/// rules apply to the slot-array shape only.
/// </summary>
public static class TcChannelRules
{
    public const int ChannelsPerSensor = CanonicalSensorMap.ChannelsPerSensor;       // 2
    public const int SensorSlotCount = CanonicalSensorMap.SensorLocations;          // 108
    public const int SlotCount = CanonicalSensorMap.MatrixSlots;                    // 108
    public const int TotalChannelCount = CanonicalSensorMap.ThermocoupleChannelCount; // 216

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

    /// <summary>
    /// Full mapping check: slot composition, per-slot channel shape, global channel
    /// uniqueness, and the canonical totals (108 slots, 108 Sensors, exactly 2 channels per
    /// Sensor, 216 distinct channels), followed by the central
    /// <see cref="TopologyInvariants"/> agreement check (sequence order, I7 / I16 present
    /// once each with one Front and one Rear channel, 240 physical / 24 unassigned).
    /// </summary>
    public static void RequireValidMapping(IReadOnlyList<SensorMapSlotExample> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        if (slots.Count != SlotCount)
        {
            throw new ArgumentException($"Expected {SlotCount} matrix slots; got {slots.Count}.", nameof(slots));
        }

        var seenChannels = new HashSet<string>(StringComparer.Ordinal);
        var sequence = new List<string>(SlotCount);
        var front = new List<string>(SlotCount);
        var rear = new List<string>(SlotCount);
        foreach (var slot in slots)
        {
            if (slot.PositionKind != LogicalPositionKind.SENSOR || string.IsNullOrWhiteSpace(slot.SensorId))
            {
                throw new ArgumentException(
                    $"Slot {slot.SlotId} must be a SENSOR slot with a Sensor identity; got kind '{slot.PositionKind}' and Sensor '{slot.SensorId ?? "<none>"}'.",
                    nameof(slots));
            }

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

            sequence.Add(slot.SensorId);
            front.Add(slot.SensorId);
            rear.Add(slot.SensorId);
        }

        if (sequence.Count != SensorSlotCount)
        {
            throw new ArgumentException($"Expected {SensorSlotCount} sensor slots; got {sequence.Count}.", nameof(slots));
        }
        if (seenChannels.Count != TotalChannelCount)
        {
            throw new ArgumentException(
                $"Expected {TotalChannelCount} distinct thermocouple channels; got {seenChannels.Count}.",
                nameof(slots));
        }

        var violation = TopologyInvariants.FirstViolation(new TopologyObservation
        {
            SensorSequence = sequence,
            FrontMeasurementSensorIds = front,
            RearMeasurementSensorIds = rear,
            AssignedMeasurementCount = seenChannels.Count,
            PhysicalChannelCount = CanonicalSensorMap.PhysicalThermocoupleChannelCount,
            UnassignedChannelCount = CanonicalSensorMap.PhysicalThermocoupleChannelCount - seenChannels.Count,
            NonSensorGapCount = 0,
            UiMapSensorCount = sequence.Count,
        });
        if (violation is not null)
        {
            throw new ArgumentException($"{TopologyInvariants.ViolationMessagePrefix}: {violation.Code}: {violation.Detail}", nameof(slots));
        }
    }
}
