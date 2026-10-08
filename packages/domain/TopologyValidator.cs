using Wjss.Contracts;

namespace Wjss.Domain;

/// <summary>
/// Independent structural validator for a <see cref="SensorParameterMigrationResult"/>
/// (Stage 0.3A-3 Checkpoint B). Re-checks every protected invariant of the approved
/// topology on the assembled whole — counts (108 / 106 / 212 / 24-29-24-29), orderTotal
/// and scanOrder ranges, I7/I16 NON_SENSOR_GAP identity and anchoring, the WJn ↔ IVn
/// one-to-one pairing, per-Sensor assignment and target-wall agreement, and namespace
/// disjointness between equipment identities and Sensor identities.
///
/// Returns null when every invariant holds; otherwise a fail-closed refusal. The
/// importer runs this on its own output before accepting (no accepted result can violate
/// an invariant), and tests run it on mutated results to pin each refusal code.
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public static class TopologyValidator
{
    private const int ExpectedPositions = CanonicalSensorMap.MatrixSlots;            // 108
    private const int ExpectedSensors = CanonicalSensorMap.SensorLocations;          // 106
    private const int ExpectedChannels = CanonicalSensorMap.ThermocoupleChannelCount; // 212
    private const int ExpectedWaterJets = 8;
    private const int ExpectedIsolationValves = 8;

    /// <summary>
/// Validates the complete result; null means every structural invariant holds. Check
/// order is fixed and deliberate: the Thermocouple-side total is checked before the
/// Sensor count so that a Sensor-count corruption reports the channel total first and
/// both structural refusal codes remain independently observable.
/// </summary>
    public static MigrationRefusalRecord? Validate(SensorParameterMigrationResult result)
    {
        if (result.LogicalPositions.Count != ExpectedPositions)
        {
            return Refuse(
                MigrationRefusalCodes.TopoPositionCount,
                $"expected {ExpectedPositions} logical positions, found {result.LogicalPositions.Count}.");
        }


        // Structural Thermocouple total: two expected sides per Sensor.
        if (result.Sensors.Count * 2 != ExpectedChannels)
        {
            return Refuse(
                MigrationRefusalCodes.TopoChannelCount,
                $"structural Thermocouple sides must total {ExpectedChannels} (two per Sensor), found {result.Sensors.Count * 2}.");
        }

        if (result.Sensors.Count != ExpectedSensors)
        {
            return Refuse(
                MigrationRefusalCodes.TopoSensorCount,
                $"expected {ExpectedSensors} Sensor configurations, found {result.Sensors.Count}.");
        }

        if (result.WaterJets.Count != ExpectedWaterJets)
        {
            return Refuse(
                MigrationRefusalCodes.TopoWaterJetCount,
                $"exactly {ExpectedWaterJets} Water Jets are required, found {result.WaterJets.Count}.");
        }

        if (result.IsolationValves.Count != ExpectedIsolationValves)
        {
            return Refuse(
                MigrationRefusalCodes.TopoValveCount,
                $"exactly {ExpectedIsolationValves} Isolation Valves are required, found {result.IsolationValves.Count}.");
        }

        // order_total: exactly the dense set 0..107, unique, strictly ascending.
        for (var i = 0; i < result.LogicalPositions.Count; i++)
        {
            var position = result.LogicalPositions[i];
            if (position.OrderTotal != i)
            {
                return Refuse(
                    MigrationRefusalCodes.TopoDuplicateOrder,
                    $"logical position {position.LogicalId} has order_total {position.OrderTotal} at sequence slot {i}; the ordering must be unique and strictly ascending over 0-107.");
            }
        }

        // I7 / I16: the only NON_SENSOR_GAP positions, anchoring exactly WJ3 / WJ1.
        var gapPositions = result.LogicalPositions
            .Where(p => p.PositionKind == LogicalPositionKind.NON_SENSOR_GAP)
            .ToArray();
        if (gapPositions.Length != 2)
        {
            return Refuse(
                MigrationRefusalCodes.TopoGapIdentity,
                $"exactly 2 NON_SENSOR_GAP positions are required (I7, I16), found {gapPositions.Length}.");
        }

        foreach (var gap in gapPositions)
        {
            if (!WaterJetTopologyCatalog.GapAnchors.TryGetValue(gap.LogicalId, out var expectedAnchor)
                || !string.Equals(gap.GapAnchorForWaterJetId, expectedAnchor, StringComparison.Ordinal))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoGapIdentity,
                    $"NON_SENSOR_GAP position '{gap.LogicalId}' must anchor '{(WaterJetTopologyCatalog.GapAnchors.TryGetValue(gap.LogicalId, out var anchor) ? anchor : "no Water Jet")}'.");
            }
        }

        var sensorPositions = result.LogicalPositions
            .Where(p => p.PositionKind == LogicalPositionKind.SENSOR)
            .ToArray();
        if (sensorPositions.Length != ExpectedSensors)
        {
            return Refuse(
                MigrationRefusalCodes.TopoGapIdentity,
                $"exactly {ExpectedSensors} SENSOR-kind logical positions are required, found {sensorPositions.Length}.");
        }

        // Sensor identity set: unique, agreeing with the SENSOR-kind positions, and
        // disjoint from every equipment identity (no Water Jet or Valve id may appear as
        // a Sensor id).
        var sensorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var position in sensorPositions)
        {
            if (!sensorIds.Add(position.LogicalId))
            {
                return Refuse(
                    MigrationRefusalCodes.MigrationLogicalLabelMismatch,
                    $"logical id '{position.LogicalId}' appears more than once among SENSOR positions.");
            }
        }

        foreach (var sensor in result.Sensors)
        {
            if (!sensorIds.Contains(sensor.SensorId))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoGapIdentity,
                    $"Sensor configuration '{sensor.SensorId}' has no SENSOR-kind logical position.");
            }
        }

        foreach (var equipmentId in result.WaterJets.Select(w => w.WaterJetId)
                     .Concat(result.IsolationValves.Select(v => v.ValveId)))
        {
            if (sensorIds.Contains(equipmentId))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoGapIdentity,
                    $"equipment identity '{equipmentId}' must never appear as a Sensor identity.");
            }
        }

        // scanOrder: dense one-based 1..106 over the Sensors only, strictly ascending,
        // skipping the NON_SENSOR_GAP positions.
        for (var i = 0; i < result.Sensors.Count; i++)
        {
            if (result.Sensors[i].ScanOrder != i + 1)
            {
                return Refuse(
                    MigrationRefusalCodes.TopoSensorCount,
                    $"scanOrder must be the dense sequence 1-{ExpectedSensors} over the Sensors only (gapless, no duplicates, skipping I7 and I16); entry {i + 1} has scanOrder {result.Sensors[i].ScanOrder}.");
            }
        }

        // Wall totals.
        foreach (var wall in new[] { Wall.LEFT, Wall.REAR, Wall.RIGHT, Wall.FRONT })
        {
            var actual = result.Sensors.Count(s => s.Wall == wall);
            if (actual != CanonicalSensorMap.SensorsPerWall[wall])
            {
                return Refuse(
                    MigrationRefusalCodes.TopoWallCounts,
                    $"wall '{wall}' must have {CanonicalSensorMap.SensorsPerWall[wall]} Sensors, found {actual}.");
            }
        }

        // WJn ↔ IVn: one-to-one, ordinals matching, both directions.
        var waterJetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var waterJet in result.WaterJets)
        {
            if (!waterJetIds.Add(waterJet.WaterJetId))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoPairingMismatch,
                    $"Water Jet identity '{waterJet.WaterJetId}' appears more than once.");
            }

            var expectedValve = $"IV{waterJet.WaterJetId[2..]}";
            if (!string.Equals(waterJet.DedicatedIsolationValveId, expectedValve, StringComparison.Ordinal))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoPairingMismatch,
                    $"Water Jet '{waterJet.WaterJetId}' must pair with '{expectedValve}', found '{waterJet.DedicatedIsolationValveId}'.");
            }
        }

        var valveIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var valve in result.IsolationValves)
        {
            if (!valveIds.Add(valve.ValveId))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoPairingMismatch,
                    $"Isolation Valve identity '{valve.ValveId}' appears more than once.");
            }

            var expectedWaterJet = $"WJ{valve.ValveId[2..]}";
            if (!string.Equals(valve.ServedWaterJetId, expectedWaterJet, StringComparison.Ordinal))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoPairingMismatch,
                    $"Isolation Valve '{valve.ValveId}' must pair with '{expectedWaterJet}', found '{valve.ServedWaterJetId}'.");
            }

            if (!waterJetIds.Contains(valve.ServedWaterJetId))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoPairingMismatch,
                    $"Isolation Valve '{valve.ValveId}' references Water Jet '{valve.ServedWaterJetId}', which does not exist.");
            }
        }

        // Per-Sensor: exactly one assigned Water Jet that exists; the Isolation Valve is
        // derived through the pairing; the Sensor's wall and region equal the assigned
        // Water Jet's target wall and region.
        var waterJetsById = result.WaterJets.ToDictionary(w => w.WaterJetId, StringComparer.Ordinal);
        foreach (var sensor in result.Sensors)
        {
            if (!waterJetsById.TryGetValue(sensor.AssignedWaterJetId, out var waterJet))
            {
                return Refuse(
                    MigrationRefusalCodes.MigrationMissingAssignment,
                    $"Sensor '{sensor.SensorId}' references Water Jet '{sensor.AssignedWaterJetId}', which does not exist.");
            }

            if (!string.Equals(sensor.AssignedIsolationValveId, waterJet.DedicatedIsolationValveId, StringComparison.Ordinal))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoPairingMismatch,
                    $"Sensor '{sensor.SensorId}' carries Isolation Valve '{sensor.AssignedIsolationValveId}' but its Water Jet '{waterJet.WaterJetId}' pairs with '{waterJet.DedicatedIsolationValveId}'; the valve must be derived through the pairing.");
            }

            var region = WaterJetTopologyCatalog.RegionForLogicalRow(sensor.LogicalRow);
            if (waterJet.TargetWall != sensor.Wall || waterJet.TargetRegion != region)
            {
                return Refuse(
                    MigrationRefusalCodes.TopoTargetWallContradiction,
                    $"Sensor '{sensor.SensorId}' ({sensor.Wall} {region}) is assigned '{waterJet.WaterJetId}' whose target is {waterJet.TargetWall} {waterJet.TargetRegion}.");
            }
        }

        // The approved target table itself (installed wall ≠ target wall, opposite wall,
        // matching region) is catalog data; validate it against the independent expectation.
        var targetRefusal = ValidateApprovedTargets(result.WaterJets);
        if (targetRefusal is not null)
        {
            return targetRefusal;
        }

        return null;
    }

    private static MigrationRefusalRecord? ValidateApprovedTargets(IReadOnlyList<WaterJetConfiguration> waterJets)
    {
        foreach (var waterJet in waterJets)
        {
            var expectedTarget = waterJet.InstalledWall switch
            {
                Wall.LEFT => Wall.RIGHT,
                Wall.RIGHT => Wall.LEFT,
                Wall.FRONT => Wall.REAR,
                Wall.REAR => Wall.FRONT,
                _ => waterJet.InstalledWall,
            };

            if (waterJet.TargetWall != expectedTarget || waterJet.TargetRegion != waterJet.InstalledRegion)
            {
                return Refuse(
                    MigrationRefusalCodes.TopoTargetWallContradiction,
                    $"Water Jet '{waterJet.WaterJetId}' is installed on {waterJet.InstalledWall} {waterJet.InstalledRegion} and must target the opposite wall {expectedTarget} in the same region; found {waterJet.TargetWall} {waterJet.TargetRegion}.");
            }
        }

        return null;
    }

    private static MigrationRefusalRecord Refuse(string code, string detail) =>
        new() { Code = code, Detail = detail };
}
