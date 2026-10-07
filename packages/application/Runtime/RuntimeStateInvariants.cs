using Wjss.Contracts;

namespace Wjss.Runtime.Core;

/// <summary>
/// Structural invariants of one Runtime revision, enforced at the single ingest
/// point of the Runtime State Store. Anything that fails validation is refused
/// before it is published, so a reader never observes a state that breaks the
/// protected domain baseline: 108 logical slots, 106 Sensor locations, 212
/// Thermocouple channels, Cannon slots at logical I7 and I16 only, wall counts
/// 24 / 29 / 24 / 29, one bounded queue, one bounded trend window, and at most
/// one Active Cleaning Job.
///
/// Validation is the runtime-side mirror of the accepted contract, not a new
/// policy: it adds no eligibility rule, no alarm rule, no pump rule and no
/// Production value.
/// </summary>
public static class RuntimeStateInvariants
{
    private const string AlarmStateActiveUnack = "ACTIVE_UNACK";
    private const string AlarmStateActiveAck = "ACTIVE_ACK";
    private const string AlarmStateClearedUnack = "CLEARED_UNACK";

    /// <summary>
    /// Validates one state. On refusal the machine reason code and a
    /// human-readable detail are returned; nothing is mutated.
    /// </summary>
    public static bool TryValidate(RuntimeState? state, out string reasonCode, out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (state is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The Runtime state is null.", out reasonCode, out detail);
        }

        if (state.Revision < 1)
        {
            return Refuse(
                RuntimeRefusalCodes.RevisionOutOfRange,
                $"Revision {state.Revision} is outside the contract range; revisions are 1-based.",
                out reasonCode,
                out detail);
        }

        if (state.DeviceProfile != DeviceProfile.SIMULATOR)
        {
            return Refuse(
                RuntimeRefusalCodes.ProfileNotSimulator,
                $"Device profile {state.DeviceProfile} must never reach a committed Runtime state in Stage 0.3A; "
                + "only SIMULATOR may start (ADR-0012), and no fallback to SIMULATOR is permitted.",
                out reasonCode,
                out detail);
        }

        if (state.Config is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The Published Configuration identity is null.", out reasonCode, out detail);
        }

        if (!TryValidateWallMap(state.WallMap, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidateSensors(state.Sensors, state.WallMap, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidateWalls(state.Walls, state.Sensors, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidateActiveJob(state.ActiveJob, state.Sensors, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidateQueue(state.Queue, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidateTrend(state.Trend, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidateAlarms(state.Alarms, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidateCommunication(state.Communication, state.Sensors, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidatePump(state.Pump, out reasonCode, out detail))
        {
            return false;
        }

        if (!TryValidateSequence(state.Sequence, out reasonCode, out detail))
        {
            return false;
        }

        reasonCode = string.Empty;
        detail = string.Empty;
        return true;
    }

    /// <summary>Throwing form of <see cref="TryValidate"/> for call sites that must fail closed.</summary>
    public static void RequireValid(RuntimeState? state)
    {
        if (!TryValidate(state, out var reasonCode, out var detail))
        {
            throw new InvalidOperationException($"[{reasonCode}] {detail}");
        }
    }

    private static bool Refuse(string reasonCode, string detail, out string refusedCode, out string refusedDetail)
    {
        refusedCode = reasonCode;
        refusedDetail = detail;
        return false;
    }

    private static bool TryValidateWallMap(IReadOnlyList<WallMapSlot>? wallMap, out string reasonCode, out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (wallMap is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The wall map is null.", out reasonCode, out detail);
        }

        if (wallMap.Count != CanonicalSensorMap.MatrixSlots)
        {
            return Refuse(
                RuntimeRefusalCodes.WallMapSlotCount,
                $"The wall map must hold exactly {CanonicalSensorMap.MatrixSlots} logical slots; got {wallMap.Count}.",
                out reasonCode,
                out detail);
        }

        var seenSlotIds = new HashSet<string>(StringComparer.Ordinal);
        var seenSensorIds = new HashSet<string>(StringComparer.Ordinal);
        var sensorSlots = 0;
        var cannonSlots = 0;

        for (var index = 0; index < wallMap.Count; index++)
        {
            var slot = wallMap[index];
            if (slot is null)
            {
                return Refuse(
                    RuntimeRefusalCodes.WallMapSlotIdentity,
                    $"The wall-map slot at index {index} is null.",
                    out reasonCode,
                    out detail);
            }

            var expectedRow = (index / CanonicalSensorMap.LogicalColumnCount) + 1;
            var expectedColumn = (index % CanonicalSensorMap.LogicalColumnCount) + 1;
            if (slot.LogicalRow != expectedRow || slot.LogicalColumn != expectedColumn)
            {
                return Refuse(
                    RuntimeRefusalCodes.WallMapSlotOrder,
                    $"The wall map must be in canonical row-major order: index {index} must be row {expectedRow}, "
                    + $"column {expectedColumn}; got row {slot.LogicalRow}, column {slot.LogicalColumn}.",
                    out reasonCode,
                    out detail);
            }

            var expectedSlotId = $"SLOT-R{expectedRow}-C{expectedColumn:D2}";
            if (!string.Equals(slot.SlotId, expectedSlotId, StringComparison.Ordinal) || !seenSlotIds.Add(slot.SlotId))
            {
                return Refuse(
                    RuntimeRefusalCodes.WallMapSlotIdentity,
                    $"Slot identity '{slot.SlotId}' at row {expectedRow}, column {expectedColumn} is not the unique "
                    + $"canonical identity '{expectedSlotId}'.",
                    out reasonCode,
                    out detail);
            }

            var wall = CanonicalSensorMap.WallForColumn(expectedColumn);
            var (firstColumn, _) = CanonicalSensorMap.WallColumns[wall];
            if (slot.Wall != wall || slot.WallRow != expectedRow || slot.WallColumn != expectedColumn - firstColumn + 1)
            {
                return Refuse(
                    RuntimeRefusalCodes.WallMapSlotIdentity,
                    $"Slot '{slot.SlotId}' does not match its canonical wall geometry "
                    + $"({wall}, wall row {expectedRow}, wall column {expectedColumn - firstColumn + 1}).",
                    out reasonCode,
                    out detail);
            }

            if (slot.SlotType == SlotType.SENSOR)
            {
                sensorSlots++;
                var expectedSensorId = CanonicalSensorMap.SensorIdFor(expectedRow, expectedColumn);
                if (slot.EquipmentId is not null
                    || !string.Equals(slot.SensorId, expectedSensorId, StringComparison.Ordinal)
                    || !seenSensorIds.Add(expectedSensorId))
                {
                    return Refuse(
                        RuntimeRefusalCodes.WallMapSensorBinding,
                        $"Sensor slot '{slot.SlotId}' must carry the unique canonical Sensor identity "
                        + $"'{expectedSensorId}' and no equipment identity; got Sensor '{slot.SensorId ?? "<none>"}' "
                        + $"and equipment '{slot.EquipmentId ?? "<none>"}'.",
                        out reasonCode,
                        out detail);
                }
            }
            else if (slot.SlotType == SlotType.CANNON)
            {
                cannonSlots++;
                var expectedEquipmentId = ExpectedCannonEquipmentId(expectedRow, expectedColumn);
                if (expectedEquipmentId is null
                    || !string.Equals(slot.EquipmentId, expectedEquipmentId, StringComparison.Ordinal)
                    || slot.SensorId is not null)
                {
                    return Refuse(
                        RuntimeRefusalCodes.WallMapCannonSlots,
                        $"A Cannon equipment slot is valid only at logical I7 (row 5, column 7) and I16 "
                        + $"(row 5, column 16); slot '{slot.SlotId}' is at row {expectedRow}, column {expectedColumn} "
                        + $"and carries equipment '{slot.EquipmentId ?? "<none>"}'.",
                        out reasonCode,
                        out detail);
                }
            }
            else
            {
                return Refuse(
                    RuntimeRefusalCodes.WallMapSlotIdentity,
                    $"Slot '{slot.SlotId}' declares unsupported slot type '{slot.SlotType}'.",
                    out reasonCode,
                    out detail);
            }
        }

        if (sensorSlots != CanonicalSensorMap.SensorLocations)
        {
            return Refuse(
                RuntimeRefusalCodes.WallMapSlotCount,
                $"The wall map must hold exactly {CanonicalSensorMap.SensorLocations} Sensor slots; got {sensorSlots}.",
                out reasonCode,
                out detail);
        }

        if (cannonSlots != CanonicalSensorMap.CannonSlotCount)
        {
            return Refuse(
                RuntimeRefusalCodes.WallMapCannonSlots,
                $"The wall map must hold exactly {CanonicalSensorMap.CannonSlotCount} Cannon slots; got {cannonSlots}.",
                out reasonCode,
                out detail);
        }

        return true;
    }

    private static string? ExpectedCannonEquipmentId(int row, int column)
    {
        foreach (var (equipmentId, cannonRow, cannonColumn) in CanonicalSensorMap.CannonSlots)
        {
            if (cannonRow == row && cannonColumn == column)
            {
                return equipmentId;
            }
        }

        return null;
    }

    private static bool TryValidateSensors(
        IReadOnlyList<SensorPresentationState>? sensors,
        IReadOnlyList<WallMapSlot> wallMap,
        out string reasonCode,
        out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (sensors is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The Sensor projection list is null.", out reasonCode, out detail);
        }

        if (sensors.Count != CanonicalSensorMap.SensorLocations)
        {
            return Refuse(
                RuntimeRefusalCodes.SensorCount,
                $"Exactly {CanonicalSensorMap.SensorLocations} Sensor projections are required; got {sensors.Count}.",
                out reasonCode,
                out detail);
        }

        var scanSlots = new List<WallMapSlot>(CanonicalSensorMap.SensorLocations);
        for (var index = 0; index < wallMap.Count; index++)
        {
            if (wallMap[index].SlotType == SlotType.SENSOR)
            {
                scanSlots.Add(wallMap[index]);
            }
        }

        if (scanSlots.Count != CanonicalSensorMap.SensorLocations)
        {
            return Refuse(
                RuntimeRefusalCodes.SensorCount,
                $"The wall map supplies {scanSlots.Count} Sensor slots; exactly {CanonicalSensorMap.SensorLocations} are required.",
                out reasonCode,
                out detail);
        }

        var channels = new HashSet<string>(StringComparer.Ordinal);
        var wallCounts = new Dictionary<Wall, int>(RuntimeWallSummaries.CanonicalWallOrder.Count);
        foreach (var wall in RuntimeWallSummaries.CanonicalWallOrder)
        {
            wallCounts[wall] = 0;
        }

        for (var index = 0; index < sensors.Count; index++)
        {
            var sensor = sensors[index];
            if (sensor is null)
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorIdentity,
                    $"The Sensor projection at index {index} is null.",
                    out reasonCode,
                    out detail);
            }

            if (sensor.ScanOrder != index + 1)
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorOrder,
                    $"Sensor projections must be ordered by ScanOrder 1..{CanonicalSensorMap.SensorLocations} without "
                    + $"gaps: index {index} must carry ScanOrder {index + 1}; got {sensor.ScanOrder}.",
                    out reasonCode,
                    out detail);
            }

            var slot = scanSlots[index];
            if (sensor.SlotType != SlotType.SENSOR
                || !string.Equals(sensor.SensorId, slot.SensorId, StringComparison.Ordinal)
                || sensor.Wall != slot.Wall
                || sensor.LogicalRow != slot.LogicalRow
                || sensor.LogicalColumn != slot.LogicalColumn
                || sensor.WallRow != slot.WallRow
                || sensor.WallColumn != slot.WallColumn
                || string.IsNullOrWhiteSpace(sensor.DeviceId))
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorIdentity,
                    $"Sensor '{sensor.SensorId}' (ScanOrder {sensor.ScanOrder}) does not match its canonical wall-map "
                    + $"slot '{slot.SlotId}' and device identity.",
                    out reasonCode,
                    out detail);
            }

            var front = sensor.TcFrontChannel;
            var rear = sensor.TcRearChannel;
            if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(rear))
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorChannels,
                    $"Sensor '{sensor.SensorId}' must carry two non-blank Thermocouple channel identities.",
                    out reasonCode,
                    out detail);
            }

            if (string.Equals(front, rear, StringComparison.Ordinal))
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorChannels,
                    $"Sensor '{sensor.SensorId}' assigns Thermocouple channel '{front}' to both sides.",
                    out reasonCode,
                    out detail);
            }

            if (!channels.Add(front) || !channels.Add(rear))
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorChannels,
                    $"A Thermocouple channel of Sensor '{sensor.SensorId}' ({front}, {rear}) is assigned to more than one Sensor.",
                    out reasonCode,
                    out detail);
            }

            if (sensor.IsActiveJobTarget != (sensor.QueueState == QueueState.ACTIVE))
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorActiveTarget,
                    $"Sensor '{sensor.SensorId}' has IsActiveJobTarget {sensor.IsActiveJobTarget} with QueueState "
                    + $"{sensor.QueueState}; the Active-Job target and the single ACTIVE Sensor are the same Sensor.",
                    out reasonCode,
                    out detail);
            }

            if (!wallCounts.TryGetValue(sensor.Wall, out var wallCount))
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorWallCounts,
                    $"Sensor '{sensor.SensorId}' declares unknown wall '{sensor.Wall}'.",
                    out reasonCode,
                    out detail);
            }

            wallCounts[sensor.Wall] = wallCount + 1;
        }

        if (channels.Count != CanonicalSensorMap.ThermocoupleChannelCount)
        {
            return Refuse(
                RuntimeRefusalCodes.SensorChannelTotal,
                $"The mapping must supply exactly {CanonicalSensorMap.ThermocoupleChannelCount} distinct Thermocouple "
                + $"channels; got {channels.Count}.",
                out reasonCode,
                out detail);
        }

        foreach (var wall in RuntimeWallSummaries.CanonicalWallOrder)
        {
            var expected = CanonicalSensorMap.SensorsPerWall[wall];
            if (wallCounts[wall] != expected)
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorWallCounts,
                    $"Wall {wall} must hold {expected} Sensor locations; got {wallCounts[wall]}.",
                    out reasonCode,
                    out detail);
            }
        }

        return true;
    }

    private static bool TryValidateWalls(
        IReadOnlyList<WallSummary>? walls,
        IReadOnlyList<SensorPresentationState> sensors,
        out string reasonCode,
        out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (walls is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The wall summary list is null.", out reasonCode, out detail);
        }

        if (walls.Count != RuntimeWallSummaries.CanonicalWallOrder.Count)
        {
            return Refuse(
                RuntimeRefusalCodes.WallSummaryShape,
                $"Exactly {RuntimeWallSummaries.CanonicalWallOrder.Count} wall summaries are required; got {walls.Count}.",
                out reasonCode,
                out detail);
        }

        var expectedSummaries = RuntimeWallSummaries.Recalculate(sensors);
        for (var index = 0; index < expectedSummaries.Count; index++)
        {
            if (walls[index] is null || !walls[index].Equals(expectedSummaries[index]))
            {
                return Refuse(
                    RuntimeRefusalCodes.WallSummaryMismatch,
                    $"The wall summary for {expectedSummaries[index].Wall} does not match the Sensor composition "
                    + "(expected totals: "
                    + $"{expectedSummaries[index].Total} total, {expectedSummaries[index].Dirty} dirty, "
                    + $"{expectedSummaries[index].Cleaner} cleaner, {expectedSummaries[index].NotClassified} not classified, "
                    + $"{expectedSummaries[index].Uncertain} uncertain).",
                    out reasonCode,
                    out detail);
            }
        }

        return true;
    }

    private static bool TryValidateActiveJob(
        ActiveCleaningJobState? activeJob,
        IReadOnlyList<SensorPresentationState> sensors,
        out string reasonCode,
        out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        var targets = 0;
        SensorPresentationState? target = null;
        for (var index = 0; index < sensors.Count; index++)
        {
            if (sensors[index].IsActiveJobTarget)
            {
                targets++;
                target = sensors[index];
            }
        }

        if (activeJob is null)
        {
            if (targets != 0)
            {
                return Refuse(
                    RuntimeRefusalCodes.SensorActiveTarget,
                    $"No Active Job exists, so no Sensor may be the Active-Job target; got {targets}.",
                    out reasonCode,
                    out detail);
            }

            return true;
        }

        if (targets != 1 || target is null)
        {
            return Refuse(
                RuntimeRefusalCodes.SensorActiveTarget,
                $"Exactly one Sensor must be the Active-Job target while a Cleaning Job is active (single Active Job "
                + $"architecture); got {targets}.",
                out reasonCode,
                out detail);
        }

        if (!string.Equals(target.SensorId, activeJob.TargetSensorId, StringComparison.Ordinal))
        {
            return Refuse(
                RuntimeRefusalCodes.SensorActiveTarget,
                $"The Active Job targets Sensor '{activeJob.TargetSensorId}', but the Sensor marked as the target is "
                + $"'{target.SensorId}'.",
                out reasonCode,
                out detail);
        }

        return true;
    }

    private static bool TryValidateQueue(QueueSummary? queue, out string reasonCode, out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (queue is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The queue summary is null.", out reasonCode, out detail);
        }

        if (queue.Entries is null)
        {
            return Refuse(RuntimeRefusalCodes.QueueShape, "The queue entry list is null.", out reasonCode, out detail);
        }

        if (queue.Capacity != QueueSummary.MaxEntries
            || queue.Revision < 0
            || queue.TotalQueued < 0
            || queue.TotalQueued != queue.Entries.Count
            || queue.Entries.Count > QueueSummary.MaxEntries)
        {
            return Refuse(
                RuntimeRefusalCodes.QueueShape,
                $"The queue must be bounded to {QueueSummary.MaxEntries} entries with a contiguous membership count; "
                + $"got capacity {queue.Capacity}, revision {queue.Revision}, totalQueued {queue.TotalQueued}, "
                + $"{queue.Entries.Count} entries.",
                out reasonCode,
                out detail);
        }

        var seenEntryIds = new HashSet<string>(StringComparer.Ordinal);
        var seenSensorIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < queue.Entries.Count; index++)
        {
            var entry = queue.Entries[index];
            if (entry is null
                || entry.Position != index + 1
                || string.IsNullOrWhiteSpace(entry.EntryId)
                || string.IsNullOrWhiteSpace(entry.SensorId)
                || !seenEntryIds.Add(entry.EntryId)
                || !seenSensorIds.Add(entry.SensorId))
            {
                return Refuse(
                    RuntimeRefusalCodes.QueueShape,
                    $"Queue entry at index {index} must carry the contiguous position {index + 1} and a unique entry "
                    + "and Sensor identity.",
                    out reasonCode,
                    out detail);
            }
        }

        return true;
    }

    private static bool TryValidateTrend(TrendWindow? trend, out string reasonCode, out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (trend is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The trend window is null.", out reasonCode, out detail);
        }

        if (trend.SeriesNames is null || trend.Points is null)
        {
            return Refuse(RuntimeRefusalCodes.TrendBounds, "The trend window carries a null series-name or point list.", out reasonCode, out detail);
        }

        if (trend.Capacity < 1 || trend.Capacity > RuntimeLimits.MaximumTrendCapacity)
        {
            return Refuse(
                RuntimeRefusalCodes.TrendBounds,
                $"The trend capacity must be 1-{RuntimeLimits.MaximumTrendCapacity}; got {trend.Capacity}.",
                out reasonCode,
                out detail);
        }

        if (trend.SeriesNames.Count != RuntimeLimits.TrendSeriesCount)
        {
            return Refuse(
                RuntimeRefusalCodes.TrendBounds,
                $"The trend window carries exactly {RuntimeLimits.TrendSeriesCount} series; got {trend.SeriesNames.Count}.",
                out reasonCode,
                out detail);
        }

        if (trend.Points.Count > trend.Capacity)
        {
            return Refuse(
                RuntimeRefusalCodes.TrendBounds,
                $"The trend window holds {trend.Points.Count} points but its capacity is {trend.Capacity}.",
                out reasonCode,
                out detail);
        }

        for (var index = 0; index < trend.Points.Count; index++)
        {
            var point = trend.Points[index];
            if (point is null || point.Series is null || point.Series.Length != trend.SeriesNames.Count)
            {
                return Refuse(
                    RuntimeRefusalCodes.TrendBounds,
                    $"Trend point at index {index} must carry a fixed-width series of {trend.SeriesNames.Count} values.",
                    out reasonCode,
                    out detail);
            }
        }

        return true;
    }

    private static bool TryValidateAlarms(AlarmSummary? alarms, out string reasonCode, out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (alarms is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The alarm summary is null.", out reasonCode, out detail);
        }

        if (alarms.Items is null)
        {
            return Refuse(RuntimeRefusalCodes.AlarmCounters, "The alarm item list is null.", out reasonCode, out detail);
        }

        var activeUnack = 0;
        var activeAck = 0;
        var clearedUnack = 0;
        for (var index = 0; index < alarms.Items.Count; index++)
        {
            var item = alarms.Items[index];
            if (item is null)
            {
                return Refuse(
                    RuntimeRefusalCodes.AlarmCounters,
                    $"The alarm item at index {index} is null.",
                    out reasonCode,
                    out detail);
            }

            switch (item.State)
            {
                case AlarmStateActiveUnack:
                    activeUnack++;
                    break;
                case AlarmStateActiveAck:
                    activeAck++;
                    break;
                case AlarmStateClearedUnack:
                    clearedUnack++;
                    break;
                default:
                    return Refuse(
                        RuntimeRefusalCodes.AlarmCounters,
                        $"Alarm '{item.AlarmId}' declares unsupported state '{item.State}'.",
                        out reasonCode,
                        out detail);
            }
        }

        if (alarms.ActiveUnack < 0
            || alarms.ActiveAck < 0
            || alarms.ClearedUnack < 0
            || alarms.ActiveUnack != activeUnack
            || alarms.ActiveAck != activeAck
            || alarms.ClearedUnack != clearedUnack)
        {
            return Refuse(
                RuntimeRefusalCodes.AlarmCounters,
                "Alarm counters must equal the presented alarm items "
                + $"(counters: {alarms.ActiveUnack} active unacknowledged, {alarms.ActiveAck} active acknowledged, "
                + $"{alarms.ClearedUnack} cleared unacknowledged; items: {activeUnack}, {activeAck}, {clearedUnack}).",
                out reasonCode,
                out detail);
        }

        return true;
    }

    private static bool TryValidateCommunication(
        CommunicationHealth? communication,
        IReadOnlyList<SensorPresentationState> sensors,
        out string reasonCode,
        out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (communication is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The communication health block is null.", out reasonCode, out detail);
        }

        if (communication.Devices is null)
        {
            return Refuse(RuntimeRefusalCodes.DeviceHealthSet, "The device health list is null.", out reasonCode, out detail);
        }

        var configuredDevices = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sensors.Count; index++)
        {
            configuredDevices.Add(sensors[index].DeviceId);
        }

        var seenDevices = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < communication.Devices.Count; index++)
        {
            var device = communication.Devices[index];
            if (device is null
                || string.IsNullOrWhiteSpace(device.DeviceId)
                || !seenDevices.Add(device.DeviceId)
                || !configuredDevices.Contains(device.DeviceId))
            {
                return Refuse(
                    RuntimeRefusalCodes.DeviceHealthSet,
                    $"Device health entry at index {index} must carry a unique identity of a configured device; "
                    + "Stage 0.3A-2A publishes no acquisition evidence, so a partial or empty set is valid and a "
                    + "fabricated device identity is not.",
                    out reasonCode,
                    out detail);
            }
        }

        return true;
    }

    private static bool TryValidatePump(PumpState? pump, out string reasonCode, out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (pump is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The pump projection is null.", out reasonCode, out detail);
        }

        if (pump.Setpoint < 0.0 || pump.ReadyBandLow < 0.0 || pump.ReadyBandLow > pump.ReadyBandHigh)
        {
            return Refuse(
                RuntimeRefusalCodes.PumpBand,
                $"The pump band must satisfy 0 <= readyBandLow <= readyBandHigh (got setpoint {pump.Setpoint}, "
                + $"band {pump.ReadyBandLow}-{pump.ReadyBandHigh}); no pump policy is encoded by this check.",
                out reasonCode,
                out detail);
        }

        if (pump.Pressure is { } pressure && pressure < 0.0)
        {
            return Refuse(
                RuntimeRefusalCodes.PumpBand,
                $"Pump pressure must not be negative; got {pressure}.",
                out reasonCode,
                out detail);
        }

        return true;
    }

    private static bool TryValidateSequence(SequenceState? sequence, out string reasonCode, out string detail)
    {
        reasonCode = string.Empty;
        detail = string.Empty;

        if (sequence is null)
        {
            return Refuse(RuntimeRefusalCodes.StateInvalid, "The sequence projection is null.", out reasonCode, out detail);
        }

        if (sequence.AutoSequence == AutoSequenceState.CRITICAL_SUSPENDED && sequence.Critical is null)
        {
            return Refuse(
                RuntimeRefusalCodes.SequenceCriticalLatch,
                "AutoSequence CRITICAL_SUSPENDED requires the latched critical Pump event; suspension persists until "
                + "the latch is cleared and the sequence is resumed by a later authorized control path.",
                out reasonCode,
                out detail);
        }

        return true;
    }
}
