using Wjss.Contracts;

namespace Wjss.Domain;

/// <summary>
/// Independent structural validator for a <see cref="SensorParameterMigrationResult"/>
/// (Stage 0.3A-3 Checkpoint B; Owner critical topology correction Stage 0.4C-K3). Re-checks
/// every protected invariant of the approved topology on the assembled whole — counts
/// (108 positions / 108 Sensors / 216 measurement sides / 24-30-24-30), orderTotal and
/// scanOrder ranges, the authoritative 108-identity Sensor sequence (I7 and I16 once each in
/// ordinal position; no gap position), the WJn ↔ IVn one-to-one pairing, per-Sensor
/// assignment and target-wall agreement, namespace disjointness between equipment
/// identities and Sensor identities, and finally the central cross-layer
/// <see cref="TopologyInvariants"/> agreement check.
///
/// Returns null when every invariant holds; otherwise a fail-closed refusal. The
/// importer runs this on its own output before accepting (no accepted result can violate
/// an invariant), and tests run it on mutated results to pin each refusal code.
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public static class TopologyValidator
{
    private const int ExpectedPositions = CanonicalSensorMap.MatrixSlots;            // 108
    private const int ExpectedSensors = CanonicalSensorMap.SensorLocations;          // 108
    private const int ExpectedChannels = CanonicalSensorMap.ThermocoupleChannelCount; // 216
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

        // Every logical position is a SENSOR position carrying its canonical identity in
        // ordinal position (Owner critical topology correction: I7 and I16 included; no
        // NON_SENSOR_GAP position exists).
        var sensorPositions = result.LogicalPositions
            .Where(p => p.PositionKind == LogicalPositionKind.SENSOR)
            .ToArray();
        if (sensorPositions.Length != ExpectedPositions)
        {
            return Refuse(
                MigrationRefusalCodes.TopoSensorIdentity,
                $"all {ExpectedPositions} logical positions must be SENSOR positions (I7 and I16 are Product Sensors), found {sensorPositions.Length}.");
        }

        for (var i = 0; i < sensorPositions.Length; i++)
        {
            var expectedId = CanonicalSensorMap.SensorSequence[i];
            if (!string.Equals(sensorPositions[i].LogicalId, expectedId, StringComparison.Ordinal))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoSensorIdentity,
                    $"logical position {i} must be Sensor '{expectedId}' (normal ordinal position), found '{sensorPositions[i].LogicalId}'.");
            }
        }

        // Sensor identity set: unique, agreeing with the SENSOR positions, and disjoint
        // from every equipment identity (no Water Jet or Valve id may appear as a Sensor id).
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
                    MigrationRefusalCodes.TopoSensorIdentity,
                    $"Sensor configuration '{sensor.SensorId}' has no SENSOR-kind logical position.");
            }
        }

        foreach (var equipmentId in result.WaterJets.Select(w => w.WaterJetId)
                     .Concat(result.IsolationValves.Select(v => v.ValveId)))
        {
            if (sensorIds.Contains(equipmentId))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoSensorIdentity,
                    $"equipment identity '{equipmentId}' must never appear as a Sensor identity.");
            }
        }

        // scanOrder: dense one-based 1..108 over all Sensors in OrderTotal order (I7 and I16
        // in their ordinal position; nothing skipped), strictly ascending and agreeing with
        // the authoritative sequence.
        for (var i = 0; i < result.Sensors.Count; i++)
        {
            if (result.Sensors[i].ScanOrder != i + 1
                || !string.Equals(result.Sensors[i].SensorId, CanonicalSensorMap.SensorSequence[i], StringComparison.Ordinal))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoSensorCount,
                    $"scanOrder must be the dense sequence 1-{ExpectedSensors} over all Sensors in ordinal order (gapless, no duplicates, I7 and I16 included); entry {i + 1} has scanOrder {result.Sensors[i].ScanOrder} for '{result.Sensors[i].SensorId}'.");
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
        // An UNRESOLVED placement must carry no anchors (nothing inferred from a former gap).
        var targetRefusal = ValidateApprovedTargets(result.WaterJets);
        if (targetRefusal is not null)
        {
            return targetRefusal;
        }

        // Central cross-layer agreement (catalogue, sequence, unique ids, Front / Rear sides,
        // assigned / physical / unassigned counts, wall totals). Fails closed on any
        // disagreement, including a Sensor missing or duplicated anywhere.
        var invariant = TopologyInvariants.FirstViolation(new TopologyObservation
        {
            SensorSequence = result.Sensors.Select(s => s.SensorId).ToArray(),
            FrontMeasurementSensorIds = result.Sensors.Select(s => s.SensorId).ToArray(),
            RearMeasurementSensorIds = result.Sensors.Select(s => s.SensorId).ToArray(),
            AssignedMeasurementCount = result.Sensors.Count * 2,
            PhysicalChannelCount = CanonicalSensorMap.PhysicalThermocoupleChannelCount,
            UnassignedChannelCount = CanonicalSensorMap.PhysicalThermocoupleChannelCount - (result.Sensors.Count * 2),
            NonSensorGapCount = result.LogicalPositions.Count(p => p.PositionKind != LogicalPositionKind.SENSOR),
            SensorsPerWall = result.Sensors.GroupBy(s => s.Wall).ToDictionary(g => g.Key, g => g.Count()),
            RuntimeSensorCount = result.LogicalPositions.Count,
        });
        if (invariant is not null)
        {
            return Refuse(MigrationRefusalCodes.TopoInvariantViolation, $"{invariant.Code}: {invariant.Detail}");
        }

        return null;
    }

    private static MigrationRefusalRecord? ValidateApprovedTargets(IReadOnlyList<WaterJetConfiguration> waterJets)
    {
        foreach (var waterJet in waterJets)
        {
            if (waterJet.PlacementKind == WaterJetPlacementKind.UNRESOLVED && waterJet.PlacementAnchors.Count != 0)
            {
                return Refuse(
                    MigrationRefusalCodes.TopoTargetWallContradiction,
                    $"Water Jet '{waterJet.WaterJetId}' has an UNRESOLVED installed position and must carry no placement anchors; found [{string.Join(", ", waterJet.PlacementAnchors)}].");
            }

            if (waterJet.PlacementKind != WaterJetPlacementKind.UNRESOLVED
                && waterJet.PlacementAnchors.Any(anchor => !CanonicalSensorMap.IsProductSensorId(anchor)))
            {
                return Refuse(
                    MigrationRefusalCodes.TopoTargetWallContradiction,
                    $"Water Jet '{waterJet.WaterJetId}' anchors on a position that is not a Product Sensor: [{string.Join(", ", waterJet.PlacementAnchors)}].");
            }

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
