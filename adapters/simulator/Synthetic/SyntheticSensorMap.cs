using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Time;

namespace Wjss.Adapters.Simulator;

/// <summary>
/// The canonical synthetic Sensor map of the SIMULATOR profile, derived from the
/// accepted logical matrix (<see cref="CanonicalSensorMap"/>): 108 logical slots
/// arranged row-major, every one of them a Sensor location (Owner critical topology
/// correction, Stage 0.4C-K3: I7 and I16 are normal Product Sensors; no NON_SENSOR_GAP
/// position exists). Every Sensor carries its assigned Water Jet and derived Isolation Valve per
/// the approved topology (<see cref="WaterJetTopologyCatalog"/>): the legacy
/// cleaning-device ordinal maps directly to WJn, and the assigned device may be
/// installed on the opposite wall.
///
/// The map composition is the one the committed
/// <c>config/examples/sensor-map.example.json</c> documents: the same scan order
/// (row-major over the Sensor positions), the same synthetic device distribution
/// and the same Thermocouple channel identities
/// (<c>SYN-TC-nn:CHmm</c>, front = lower channel index). A parity test pins that
/// agreement, so the simulator cannot silently drift from the committed
/// synthetic map.
///
/// Determinism: every value is a pure function of the explicit seed, the slot
/// identity and the supplied clock instant. No value is read from wall time, no
/// global state exists, and no Production value, address, register, coordinate,
/// threshold or timeout is invented here.
/// </summary>
public static class SyntheticSensorMap
{
    /// <summary>108 logical matrix slots (protected baseline).</summary>
    public const int SlotCount = CanonicalSensorMap.MatrixSlots;

    /// <summary>108 Sensor locations (protected baseline).</summary>
    public const int SensorCount = CanonicalSensorMap.SensorLocations;

    /// <summary>216 Thermocouple channels: two per Sensor location (protected baseline).</summary>
    public const int ThermocoupleChannelCount = CanonicalSensorMap.ThermocoupleChannelCount;

    /// <summary>Every Sensor location folds exactly two Thermocouple channels.</summary>
    public const int ChannelsPerSensor = 2;

    /// <summary>Lower bound of the synthetic initial Dirty Score range (presentation value, not a process value).</summary>
    public const double MinimumInitialScore = 20.0;

    /// <summary>Upper bound of the synthetic initial Dirty Score range (presentation value, not a process value).</summary>
    public const double MaximumInitialScore = 80.0;

    /// <summary>Decimal places of a synthetic Dirty Score; rounding is fixed so the value is byte-stable.</summary>
    public const int ScoreDecimals = 1;

    /// <summary>
    /// Synthetic device distribution over scan order: eight devices, four of them
    /// carrying 14 Sensors and four carrying 13 (14 x 4 + 13 x 4 = 108). This is
    /// the distribution the committed synthetic sensor map documents
    /// (<c>tools/sensor-map-example/generate.mjs</c> is its Node mirror).
    /// </summary>
    private static readonly int[] DeviceScanCounts = [14, 14, 14, 14, 13, 13, 13, 13];

    /// <summary>
    /// Builds the 108-slot wall map in canonical row-major order; every slot is a
    /// Sensor slot carrying its canonical identity (I7 and I16 included).
    /// </summary>
    public static IReadOnlyList<WallMapSlot> BuildWallMap()
    {
        var slots = new List<WallMapSlot>(SlotCount);
        for (var row = 1; row <= CanonicalSensorMap.LogicalRowCount; row++)
        {
            for (var column = 1; column <= CanonicalSensorMap.LogicalColumnCount; column++)
            {
                var wall = CanonicalSensorMap.WallForColumn(column);
                var (firstColumn, _) = CanonicalSensorMap.WallColumns[wall];

                slots.Add(new WallMapSlot
                {
                    SlotId = $"SLOT-R{row}-C{column:D2}",
                    PositionKind = LogicalPositionKind.SENSOR,
                    Wall = wall,
                    LogicalColumn = column,
                    LogicalRow = row,
                    WallColumn = column - firstColumn + 1,
                    WallRow = row,
                    SensorId = CanonicalSensorMap.SensorIdFor(row, column),
                });
            }
        }

        return Array.AsReadOnly(slots.ToArray());
    }

    /// <summary>
    /// Builds the 108 initial Sensor projections for one seed and clock instant.
    /// Every Sensor starts at a synthetic Dirty Score derived from the seed and
    /// classified against the supplied Published Configuration threshold; quality
    /// starts GOOD with a CURRENT basis, no fault injection exists in Stage
    /// 0.3A-2A, and no Sensor is queued or targeted.
    /// </summary>
    public static IReadOnlyList<SensorPresentationState> BuildInitialSensors(
        PublishedConfigurationRevision config,
        SyntheticSeed seed,
        DateTimeOffset atUtc)
    {
        ArgumentNullException.ThrowIfNull(config);

        var timestamp = UtcTimestamps.Format(atUtc);
        var positions = SensorPositions();
        var sensors = new List<SensorPresentationState>(SensorCount);

        for (var scanOrder = 1; scanOrder <= SensorCount; scanOrder++)
        {
            var (row, column) = positions[scanOrder - 1];
            var wall = CanonicalSensorMap.WallForColumn(column);
            var (firstColumn, _) = CanonicalSensorMap.WallColumns[wall];
            var deviceId = DeviceIdFor(scanOrder);
            var indexOnDevice = IndexOnDevice(scanOrder);
            var score = InitialScoreFor(seed, scanOrder);

            var region = WaterJetTopologyCatalog.RegionForLogicalRow(row);
            var assigned = WaterJetTopologyCatalog.WaterJets.Single(w =>
                w.TargetWall == wall && w.TargetRegion == region);

            sensors.Add(new SensorPresentationState
            {
                SensorId = CanonicalSensorMap.SensorIdFor(row, column),
                PositionKind = LogicalPositionKind.SENSOR,
                Wall = wall,
                LogicalColumn = column,
                LogicalRow = row,
                WallColumn = column - firstColumn + 1,
                WallRow = row,
                ScanOrder = scanOrder,
                DeviceId = deviceId,
                AssignedWaterJetId = assigned.WaterJetId,
                AssignedIsolationValveId = assigned.DedicatedIsolationValveId,
                TcFrontChannel = $"{deviceId}:CH{2 * indexOnDevice:D2}",
                TcRearChannel = $"{deviceId}:CH{2 * indexOnDevice + 1:D2}",
                DirtyScore = score,
                LastValidatedScore = score,
                LastValidatedAt = timestamp,
                Classification = score > config.DirtyThreshold ? Classification.DIRTY : Classification.CLEANER,
                ClassificationBasis = ClassificationBasis.CURRENT,
                Quality = Quality.GOOD,
                QualityReason = null,
                SourceTimestamp = timestamp,
                QueueState = QueueState.NONE,
                IsActiveJobTarget = false,
                AlarmState = AlarmState.NONE,
                AlarmSeverity = null,
            });
        }

        return Array.AsReadOnly(sensors.ToArray());
    }

    /// <summary>Initial synthetic Dirty Score of one scan-order slot for a seed.</summary>
    public static double InitialScoreFor(SyntheticSeed seed, int scanOrder)
    {
        if (scanOrder < 1 || scanOrder > SensorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scanOrder),
                scanOrder,
                $"Scan order must be 1-{SensorCount}.");
        }

        var unit = DeterministicValueSource.UnitFor(seed, scanOrder);
        var score = MinimumInitialScore + ((MaximumInitialScore - MinimumInitialScore) * unit);
        return Math.Round(score, ScoreDecimals, MidpointRounding.ToZero);
    }

    private static (int Row, int Column)[] SensorPositions()
    {
        var positions = new List<(int Row, int Column)>(SensorCount);
        for (var row = 1; row <= CanonicalSensorMap.LogicalRowCount; row++)
        {
            for (var column = 1; column <= CanonicalSensorMap.LogicalColumnCount; column++)
            {
                positions.Add((row, column));
            }
        }

        return positions.ToArray();
    }

    private static string DeviceIdFor(int scanOrder) => $"SYN-TC-{DeviceIndexFor(scanOrder) + 1:D2}";

    private static int DeviceIndexFor(int scanOrder)
    {
        var remaining = scanOrder;
        for (var deviceIndex = 0; deviceIndex < DeviceScanCounts.Length; deviceIndex++)
        {
            if (remaining <= DeviceScanCounts[deviceIndex])
            {
                return deviceIndex;
            }

            remaining -= DeviceScanCounts[deviceIndex];
        }

        throw new ArgumentOutOfRangeException(
            nameof(scanOrder),
            scanOrder,
            "Scan order is outside the synthetic device distribution.");
    }

    private static int IndexOnDevice(int scanOrder)
    {
        var remaining = scanOrder;
        for (var deviceIndex = 0; deviceIndex < DeviceScanCounts.Length; deviceIndex++)
        {
            if (remaining <= DeviceScanCounts[deviceIndex])
            {
                return remaining - 1;
            }

            remaining -= DeviceScanCounts[deviceIndex];
        }

        throw new ArgumentOutOfRangeException(
            nameof(scanOrder),
            scanOrder,
            "Scan order is outside the synthetic device distribution.");
    }
}
