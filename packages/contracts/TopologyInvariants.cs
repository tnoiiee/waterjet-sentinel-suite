namespace Wjss.Contracts;

/// <summary>
/// One layer's view of the Sensor topology, handed to <see cref="TopologyInvariants"/>
/// for the central cross-layer agreement check (Owner critical topology correction,
/// Stage 0.4C-K3). Every collection is the layer's OWN enumeration — nothing here is
/// derived from the expected values, otherwise the check would be circular.
/// Optional counts (<c>null</c>) mean "this layer does not publish that figure"; a
/// supplied figure must agree exactly.
/// </summary>
public sealed record TopologyObservation
{
    /// <summary>The layer's Sensor identities in its own order (catalogue, wall map, runtime projection, fixture …).</summary>
    public required IReadOnlyList<string> SensorSequence { get; init; }

    /// <summary>Sensor identity of every Front measurement the layer carries (one entry per measurement).</summary>
    public required IReadOnlyList<string> FrontMeasurementSensorIds { get; init; }

    /// <summary>Sensor identity of every Rear measurement the layer carries (one entry per measurement).</summary>
    public required IReadOnlyList<string> RearMeasurementSensorIds { get; init; }

    /// <summary>Assigned Product Thermocouple measurements the layer counts (expected 216).</summary>
    public required int AssignedMeasurementCount { get; init; }

    /// <summary>Physical Thermocouple channel capacity the layer counts (expected 240).</summary>
    public required int PhysicalChannelCount { get; init; }

    /// <summary>Physical Thermocouple channels without a Product assignment (expected 24).</summary>
    public required int UnassignedChannelCount { get; init; }

    /// <summary>Positions the layer still classifies as a non-Sensor gap (expected 0).</summary>
    public int NonSensorGapCount { get; init; }

    /// <summary>Sensors per wall as the layer counts them; null when the layer has no wall view.</summary>
    public IReadOnlyDictionary<Wall, int>? SensorsPerWall { get; init; }

    /// <summary>Sensor count of the UI wall map; null when not published by this layer.</summary>
    public int? UiMapSensorCount { get; init; }

    /// <summary>Sensor count of the runtime Sensor catalogue; null when not published by this layer.</summary>
    public int? RuntimeSensorCount { get; init; }

    /// <summary>Sensor count of a serialized fixture; null when not published by this layer.</summary>
    public int? SerializedFixtureSensorCount { get; init; }
}

/// <summary>One violated invariant: a stable machine code and a deterministic detail.</summary>
public sealed record TopologyInvariantViolation(string Code, string Detail);

/// <summary>
/// Central .NET topology invariant check — the counterpart of the accepted Node check
/// (<c>packages/commissioning/src/topologyInvariants.mjs</c>). It rejects any disagreement
/// among the authoritative Sensor catalogue, the generated Sensor sequence, the unique
/// Sensor identities, the Front and Rear measurement counts, the assigned Product
/// measurement count, the physical assignment count, the unassigned physical channel count,
/// the UI wall map, the runtime Sensor catalogue and serialized fixtures.
///
/// Required values (Owner ruling, final): Sensors = 108, unique IDs = 108, Front = 108,
/// Rear = 108, assigned measurements = 216, physical capacity = 240, unassigned = 24,
/// I7 exactly once, I16 exactly once, I7 and I16 each with exactly one Front and one Rear
/// measurement, NON_SENSOR_GAP positions = 0. The check fails closed: an unknown or
/// contradictory identity is a violation, never a tolerated difference.
/// </summary>
public static class TopologyInvariants
{
    public const string ArithmeticIdentity = "TOPOLOGY_ARITHMETIC_IDENTITY";
    public const string SequenceLength = "TOPOLOGY_SEQUENCE_LENGTH";
    public const string SequenceOrder = "TOPOLOGY_SEQUENCE_ORDER";
    public const string DuplicateSensorId = "TOPOLOGY_DUPLICATE_SENSOR_ID";
    public const string SensorMissing = "TOPOLOGY_SENSOR_MISSING";
    public const string SensorUnknown = "TOPOLOGY_SENSOR_UNKNOWN";
    public const string NonSensorGapPresent = "TOPOLOGY_NON_SENSOR_GAP_PRESENT";
    public const string CatalogueWallCount = "TOPOLOGY_CATALOGUE_WALL_COUNT";
    public const string SchedulePhysicalCount = "TOPOLOGY_SCHEDULE_PHYSICAL_COUNT";
    public const string ScheduleAssignedCount = "TOPOLOGY_SCHEDULE_ASSIGNED_COUNT";
    public const string ScheduleUnassignedCount = "TOPOLOGY_SCHEDULE_UNASSIGNED_COUNT";
    public const string FrontRearPairing = "TOPOLOGY_FRONT_REAR_PAIRING";
    public const string UiMapCount = "TOPOLOGY_UI_MAP_COUNT";
    public const string RuntimeCount = "TOPOLOGY_RUNTIME_COUNT";
    public const string FixtureCount = "TOPOLOGY_FIXTURE_COUNT";
    public const string LayerDisagreement = "TOPOLOGY_LAYER_DISAGREEMENT";

    /// <summary>Prefix of every exception thrown by <see cref="Require"/>.</summary>
    public const string ViolationMessagePrefix = "topology invariant violation";

    /// <summary>The expected figures, exposed for presentation and tests (never used as an input of the check).</summary>
    public static int ExpectedSensors => CanonicalSensorMap.SensorLocations;
    public static int ExpectedAssignedMeasurements => CanonicalSensorMap.ThermocoupleChannelCount;
    public static int ExpectedPhysicalChannels => CanonicalSensorMap.PhysicalThermocoupleChannelCount;
    public static int ExpectedUnassignedChannels => CanonicalSensorMap.UnassignedThermocoupleChannelCount;

    /// <summary>Evaluates every invariant and returns all violations in a fixed order (empty when the layers agree).</summary>
    public static IReadOnlyList<TopologyInvariantViolation> Check(TopologyObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(observation.SensorSequence);
        ArgumentNullException.ThrowIfNull(observation.FrontMeasurementSensorIds);
        ArgumentNullException.ThrowIfNull(observation.RearMeasurementSensorIds);

        var violations = new List<TopologyInvariantViolation>();
        var expected = CanonicalSensorMap.SensorSequence;

        // The arithmetic identity itself (108 × 2 = 216; 240 − 216 = 24) is pinned on the
        // canonical constants so that no edit to one constant can pass silently.
        if (ExpectedSensors * CanonicalSensorMap.ChannelsPerSensor != ExpectedAssignedMeasurements
            || ExpectedPhysicalChannels - ExpectedAssignedMeasurements != ExpectedUnassignedChannels
            || expected.Count != ExpectedSensors)
        {
            violations.Add(new(ArithmeticIdentity,
                $"canonical constants disagree: {ExpectedSensors} Sensors × {CanonicalSensorMap.ChannelsPerSensor} = {ExpectedAssignedMeasurements}; "
                + $"{ExpectedPhysicalChannels} − {ExpectedAssignedMeasurements} = {ExpectedUnassignedChannels}; sequence {expected.Count}."));
        }

        var sequence = observation.SensorSequence;
        if (sequence.Count != ExpectedSensors)
        {
            violations.Add(new(SequenceLength, $"the Sensor sequence must hold exactly {ExpectedSensors} identities; got {sequence.Count}."));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sensorId in sequence)
        {
            if (sensorId is null || !seen.Add(sensorId))
            {
                violations.Add(new(DuplicateSensorId, $"Sensor identity '{sensorId ?? "<null>"}' is null or appears more than once in the sequence."));
                break;
            }
        }

        foreach (var sensorId in seen)
        {
            if (!CanonicalSensorMap.IsProductSensorId(sensorId))
            {
                violations.Add(new(SensorUnknown, $"'{sensorId}' is not one of the {ExpectedSensors} authoritative Product Sensors."));
                break;
            }
        }

        foreach (var sensorId in expected)
        {
            if (!seen.Contains(sensorId))
            {
                violations.Add(new(SensorMissing, $"Sensor '{sensorId}' is missing from the sequence."));
                break;
            }
        }

        if (sequence.Count == expected.Count && seen.Count == expected.Count)
        {
            for (var index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(sequence[index], expected[index], StringComparison.Ordinal))
                {
                    violations.Add(new(SequenceOrder,
                        $"sequence position {index + 1} must be '{expected[index]}' (normal ordinal position); got '{sequence[index]}'."));
                    break;
                }
            }
        }

        foreach (var sensorId in CanonicalSensorMap.CorrectedSensorIds)
        {
            var occurrences = sequence.Count(id => string.Equals(id, sensorId, StringComparison.Ordinal));
            if (occurrences != 1)
            {
                violations.Add(new(SensorMissing, $"Sensor '{sensorId}' must occur exactly once in the sequence; got {occurrences}."));
            }
        }

        if (observation.NonSensorGapCount != 0)
        {
            violations.Add(new(NonSensorGapPresent,
                $"no logical position is a non-Sensor gap; the layer still classifies {observation.NonSensorGapCount} position(s) that way."));
        }

        CheckMeasurements(observation.FrontMeasurementSensorIds, "Front", violations);
        CheckMeasurements(observation.RearMeasurementSensorIds, "Rear", violations);

        if (observation.AssignedMeasurementCount != ExpectedAssignedMeasurements)
        {
            violations.Add(new(ScheduleAssignedCount,
                $"assigned Product measurements must be {ExpectedAssignedMeasurements}; got {observation.AssignedMeasurementCount}."));
        }

        if (observation.PhysicalChannelCount != ExpectedPhysicalChannels)
        {
            violations.Add(new(SchedulePhysicalCount,
                $"physical Thermocouple channels must be {ExpectedPhysicalChannels}; got {observation.PhysicalChannelCount}."));
        }

        if (observation.UnassignedChannelCount != ExpectedUnassignedChannels
            || observation.PhysicalChannelCount - observation.AssignedMeasurementCount != observation.UnassignedChannelCount)
        {
            violations.Add(new(ScheduleUnassignedCount,
                $"unassigned physical channels must be {ExpectedUnassignedChannels} = {observation.PhysicalChannelCount} − {observation.AssignedMeasurementCount}; got {observation.UnassignedChannelCount}."));
        }

        if (observation.FrontMeasurementSensorIds.Count + observation.RearMeasurementSensorIds.Count != observation.AssignedMeasurementCount)
        {
            violations.Add(new(LayerDisagreement,
                $"Front ({observation.FrontMeasurementSensorIds.Count}) + Rear ({observation.RearMeasurementSensorIds.Count}) measurements disagree with the assigned count {observation.AssignedMeasurementCount}."));
        }

        if (observation.SensorsPerWall is { } walls)
        {
            foreach (var (wall, expectedCount) in CanonicalSensorMap.SensorsPerWall)
            {
                var actual = walls.TryGetValue(wall, out var count) ? count : -1;
                if (actual != expectedCount)
                {
                    violations.Add(new(CatalogueWallCount, $"wall {wall} must hold {expectedCount} Sensors; got {actual}."));
                }
            }

            if (walls.Values.Sum() != sequence.Count)
            {
                violations.Add(new(LayerDisagreement, $"wall totals ({walls.Values.Sum()}) disagree with the Sensor sequence ({sequence.Count})."));
            }
        }

        if (observation.UiMapSensorCount is { } uiCount && uiCount != ExpectedSensors)
        {
            violations.Add(new(UiMapCount, $"the UI wall map must present {ExpectedSensors} Sensors; got {uiCount}."));
        }

        if (observation.RuntimeSensorCount is { } runtimeCount && runtimeCount != ExpectedSensors)
        {
            violations.Add(new(RuntimeCount, $"the runtime Sensor catalogue must hold {ExpectedSensors} Sensors; got {runtimeCount}."));
        }

        if (observation.SerializedFixtureSensorCount is { } fixtureCount && fixtureCount != ExpectedSensors)
        {
            violations.Add(new(FixtureCount, $"the serialized fixture must hold {ExpectedSensors} Sensors; got {fixtureCount}."));
        }

        var published = new[] { observation.UiMapSensorCount, observation.RuntimeSensorCount, observation.SerializedFixtureSensorCount }
            .Where(count => count.HasValue)
            .Select(count => count!.Value)
            .ToArray();
        if (published.Any(count => count != sequence.Count))
        {
            violations.Add(new(LayerDisagreement,
                $"layer counts disagree: sequence {sequence.Count}, published [{string.Join(", ", published)}]."));
        }

        return violations.AsReadOnly();
    }

    /// <summary>The first violation, or null when every invariant holds.</summary>
    public static TopologyInvariantViolation? FirstViolation(TopologyObservation observation)
    {
        var violations = Check(observation);
        return violations.Count == 0 ? null : violations[0];
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> (message prefixed with <see cref="ViolationMessagePrefix"/>) on the first violation.</summary>
    public static void Require(TopologyObservation observation)
    {
        var violations = Check(observation);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                $"{ViolationMessagePrefix}: {string.Join(" | ", violations.Select(v => $"{v.Code}: {v.Detail}"))}");
        }
    }

    private static void CheckMeasurements(IReadOnlyList<string> measurementSensorIds, string side, List<TopologyInvariantViolation> violations)
    {
        if (measurementSensorIds.Count != ExpectedSensors)
        {
            violations.Add(new(FrontRearPairing, $"{side} measurements must number {ExpectedSensors} (one per Sensor); got {measurementSensorIds.Count}."));
        }

        var perSensor = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var sensorId in measurementSensorIds)
        {
            if (sensorId is null)
            {
                violations.Add(new(FrontRearPairing, $"a {side} measurement carries no Sensor identity."));
                return;
            }

            perSensor[sensorId] = perSensor.TryGetValue(sensorId, out var count) ? count + 1 : 1;
        }

        foreach (var sensorId in CanonicalSensorMap.SensorSequence)
        {
            var count = perSensor.TryGetValue(sensorId, out var found) ? found : 0;
            if (count != 1)
            {
                violations.Add(new(FrontRearPairing, $"Sensor '{sensorId}' must carry exactly one {side} measurement; got {count}."));
                return;
            }
        }

        foreach (var sensorId in perSensor.Keys)
        {
            if (!CanonicalSensorMap.IsProductSensorId(sensorId))
            {
                violations.Add(new(FrontRearPairing, $"a {side} measurement belongs to '{sensorId}', which is not a Product Sensor."));
                return;
            }
        }
    }
}
