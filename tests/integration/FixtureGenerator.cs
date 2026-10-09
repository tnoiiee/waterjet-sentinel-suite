using System.Text.Json;
using System.Text.Json.Nodes;
using Wjss.Contracts;
using Wjss.Domain;

namespace Wjss.FixtureEmission.Tests;

/// <summary>
/// Deterministic construction of the structural fixtures. Every synthetic value
/// is derived from fixed formulas over ScanOrder so regeneration is
/// byte-stable and the TypeScript/Node reference generator can reproduce the
/// same numbers. No Production value, address, register, coordinate or timing
/// constant appears here.
/// </summary>
internal static class FixtureGenerator
{
    public const string Timestamp = "2026-10-07T00:00:00.000Z";
    public const int SnapshotRevision = 10;
    public const string FixtureStatus = "PROVISIONAL STRUCTURAL FIXTURE - OWNER-LOCAL .NET GENERATION REQUIRED";
    public const double DirtyThreshold = 50.5;

    public static double ScoreFor(int scanOrder) => ((scanOrder * 37) % 101) + 0.5;

    public static string DeviceIdFor(int scanOrder)
    {
        int[] counts = [14, 14, 13, 13, 13, 13, 13, 13];
        var remaining = scanOrder;
        for (var d = 0; d < counts.Length; d++)
        {
            if (remaining <= counts[d])
            {
                return $"SYN-TC-{(d + 1):D2}";
            }

            remaining -= counts[d];
        }

        throw new InvalidOperationException("scan order outside device distribution");
    }

    public static int IndexOnDevice(int scanOrder)
    {
        int[] counts = [14, 14, 13, 13, 13, 13, 13, 13];
        var remaining = scanOrder;
        for (var d = 0; d < counts.Length; d++)
        {
            if (remaining <= counts[d])
            {
                return remaining - 1;
            }

            remaining -= counts[d];
        }

        throw new InvalidOperationException("scan order outside device distribution");
    }

    public static (int Row, int Column)[] SensorPositions()
    {
        var list = new List<(int, int)>();
        for (var row = 1; row <= CanonicalSensorMap.LogicalRowCount; row++)
        {
            for (var col = 1; col <= CanonicalSensorMap.LogicalColumnCount; col++)
            {
                if (CanonicalSensorMap.NonSensorGapSlots.Any(c => c.Row == row && c.Column == col))
                {
                    continue;
                }

                list.Add((row, col));
            }
        }

        return list.ToArray();
    }

    public static string SensorIdForScan(int scanOrder)
    {
        var (row, col) = SensorPositions()[scanOrder - 1];
        return CanonicalSensorMap.SensorIdFor(row, col);
    }

    public static WallMapSlot[] BuildWallMap()
    {
        var slots = new List<WallMapSlot>();
        for (var row = 1; row <= CanonicalSensorMap.LogicalRowCount; row++)
        {
            for (var col = 1; col <= CanonicalSensorMap.LogicalColumnCount; col++)
            {
                var wall = CanonicalSensorMap.WallForColumn(col);
                var (first, _) = CanonicalSensorMap.WallColumns[wall];
                var gap = CanonicalSensorMap.NonSensorGapSlots.FirstOrDefault(c => c.Row == row && c.Column == col);
                slots.Add(new WallMapSlot
                {
                    SlotId = $"SLOT-R{row}-C{col:D2}",
                    PositionKind = gap.Item1 is null ? LogicalPositionKind.SENSOR : LogicalPositionKind.NON_SENSOR_GAP,
                    Wall = wall,
                    LogicalColumn = col,
                    LogicalRow = row,
                    WallColumn = col - first + 1,
                    WallRow = row,
                    SensorId = gap.Item1 is null ? CanonicalSensorMap.SensorIdFor(row, col) : null,
                    GapAnchorForWaterJetId = gap.Item2,
                });
            }
        }

        return slots.ToArray();
    }

    public static SensorPresentationState BuildSensor(int scanOrder, bool inQueue, bool isJobTarget)
    {
        var (row, col) = SensorPositions()[scanOrder - 1];
        var wall = CanonicalSensorMap.WallForColumn(col);
        var (first, _) = CanonicalSensorMap.WallColumns[wall];
        var score = ScoreFor(scanOrder);
        var uncertain = scanOrder % 7 == 0;
        var deviceId = DeviceIdFor(scanOrder);
        var idx = IndexOnDevice(scanOrder);
        var region = WaterJetTopologyCatalog.RegionForLogicalRow(row);
        var assigned = WaterJetTopologyCatalog.WaterJets.Single(w =>
            w.TargetWall == wall && w.TargetRegion == region);

        return new SensorPresentationState
        {
            SensorId = CanonicalSensorMap.SensorIdFor(row, col),
            PositionKind = LogicalPositionKind.SENSOR,
            Wall = wall,
            LogicalColumn = col,
            LogicalRow = row,
            WallColumn = col - first + 1,
            WallRow = row,
            ScanOrder = scanOrder,
            DeviceId = deviceId,
            AssignedWaterJetId = assigned.WaterJetId,
            AssignedIsolationValveId = assigned.DedicatedIsolationValveId,
            TcFrontChannel = $"{deviceId}:CH{2 * idx:D2}",
            TcRearChannel = $"{deviceId}:CH{2 * idx + 1:D2}",
            DirtyScore = score,
            LastValidatedScore = uncertain ? score : null,
            LastValidatedAt = uncertain ? Timestamp : null,
            Classification = score > DirtyThreshold ? Classification.DIRTY : Classification.CLEANER,
            ClassificationBasis = uncertain ? ClassificationBasis.LAST_VALIDATED : ClassificationBasis.CURRENT,
            Quality = uncertain ? Quality.UNCERTAIN : Quality.GOOD,
            QualityReason = uncertain ? "FIXTURE_FORCED" : null,
            SourceTimestamp = Timestamp,
            QueueState = isJobTarget ? QueueState.ACTIVE : inQueue ? QueueState.QUEUED : QueueState.NONE,
            IsActiveJobTarget = isJobTarget,
            AlarmState = AlarmState.NONE,
            AlarmSeverity = null,
        };
    }

    public static QueueEntry[] BuildQueueEntries() =>
    [
        new()
        {
            Position = 1,
            EntryId = "Q-201",
            SensorId = "G+210",
            SourceReason = "DIRTY_SCORE",
            DirtyScore = 60.5,
            SecondsSinceLastClean = 3600,
        },
        new()
        {
            Position = 2,
            EntryId = "Q-202",
            SensorId = "H12",
            SourceReason = "TIME_INTERVAL",
            DirtyScore = 52.5,
            SecondsSinceLastClean = 7200,
        },
        new()
        {
            Position = 3,
            EntryId = "Q-203",
            SensorId = "J5",
            SourceReason = "OPERATOR_REQUEST",
            DirtyScore = null,
            SecondsSinceLastClean = 10800,
        },
    ];

    public static DispatchRecord BuildDispatch() => new()
    {
        DispatchId = "D-100",
        QueueRevisionBefore = 4,
        QueueRevisionAfter = 5,
        QueueEntryId = "Q-200",
        PositionBefore = 1,
        SensorId = "H7",
        SourceReason = "DIRTY_SCORE",
        JobId = "J-300",
        Origin = "FIXTURE",
        DispatchedAt = Timestamp,
    };

    public static ActiveCleaningJobState BuildActiveJob() => new()
    {
        JobId = "J-300",
        TargetSensorId = "H7",
        JetId = "SYN-JET-05",
        ValveId = "SYN-VLV-05",
        Phase = JobPhase.P4,
        PhaseLabel = "PHASE-4 (synthetic fixture)",
        PhaseIndex = 4,
        StartedAt = Timestamp,
        PhaseStartedAt = Timestamp,
        PhaseProgress = 0.5,
        Lifecycle = JobLifecycle.SAFE_RETURN_TO_STANDBY,
        CleaningPhase = "CLEANING_STOPPED",
        PumpOutletPressureBar = 16.0,
        PumpPressureQuality = "GOOD",
        PumpPressureSourceId = "PUMP_OUTLET",
        PumpPressureInputValid = true,
        PumpReadySetpointBar = 15.0,
        ValveOutletPressureBar = 0.5,
        ValvePressureQuality = "GOOD",
        ValvePressureSourceId = "SYN-VLV-05_OUTLET",
        ValvePressureInputValid = true,
        ValveOpenResolution = "OPEN_CONFIRMED",
        ValveDiagnosis = "NONE",
        Dispatch = BuildDispatch(),
        SafeReturn = new SafeReturnState
        {
            Step = SafeReturnStep.SR4,
            Trigger = "FIXTURE_TRIGGER",
            PendingOutcome = "ABORTED",
            PhaseAtTrigger = JobPhase.P4,
            StartedAt = Timestamp,
            Valve = new SafeReturnValveLeg
            {
                ValveId = "SYN-VLV-05",
                Command = "CLOSE_COMMANDED",
                CommandSeq = 11,
                Feedback = "CLOSED_CONFIRMED",
                FeedbackSeq = 13,
                UpperLimitDetected = false,
                LowerLimitDetected = true,
                PressureBar = 0.5,
                PressureQuality = "GOOD",
                LowPressureThresholdBar = 1.0,
                HighPressureThresholdBar = 15.0,
                PressureInputValid = true,
                Resolution = "LOWER_LIMIT_CONFIRMED",
                Diagnosis = "NONE",
            },
            Axis = new SafeReturnAxisLeg
            {
                Command = "RETURN_COMMANDED",
                CommandSeq = 12,
                Standby = "NOT_CONFIRMED",
                StandbySeq = null,
            },
            Failure = null,
            Events =
            [
                new SafeReturnEvent { Seq = 9, Step = SafeReturnStep.SR1, Event = "WATER_STOPPED", At = Timestamp },
                new SafeReturnEvent { Seq = 10, Step = SafeReturnStep.SR2, Event = "VALVE_CLOSE_QUEUED", At = Timestamp },
                new SafeReturnEvent { Seq = 11, Step = SafeReturnStep.SR2, Event = "VALVE_CLOSE_COMMANDED", At = Timestamp },
                new SafeReturnEvent { Seq = 12, Step = SafeReturnStep.SR4, Event = "AXIS_RETURN_COMMANDED", At = Timestamp },
                new SafeReturnEvent { Seq = 13, Step = SafeReturnStep.SR3, Event = "VALVE_CLOSED_CONFIRMED", At = Timestamp },
            ],
        },
    };

    public static OperationalSnapshot BuildSnapshot()
    {
        var queueSensorIds = BuildQueueEntries().Select(e => e.SensorId).ToHashSet();
        var sensors = Enumerable.Range(1, CanonicalSensorMap.SensorLocations)
            .Select(scan => BuildSensor(scan, queueSensorIds.Contains(SensorIdForScan(scan)), SensorIdForScan(scan) == "H7"))
            .ToArray();

        var walls = new[] { Wall.LEFT, Wall.REAR, Wall.RIGHT, Wall.FRONT }.Select(w =>
        {
            var own = sensors.Where(s => s.Wall == w).ToArray();
            return new WallSummary
            {
                Wall = w,
                Total = own.Length,
                Dirty = own.Count(s => s.Classification == Classification.DIRTY),
                Cleaner = own.Count(s => s.Classification == Classification.CLEANER),
                NotClassified = own.Count(s => s.Classification == Classification.NOT_CLASSIFIED),
                Uncertain = own.Count(s => s.Quality == Quality.UNCERTAIN),
                MaxScore = own.Max(s => s.DirtyScore ?? 0.0),
            };
        }).ToArray();

        return new OperationalSnapshot
        {
            Kind = "snapshot",
            Schema = SchemaIds.Snapshot,
            ApiVersion = SchemaIds.ApiVersion,
            Revision = SnapshotRevision,
            GeneratedAt = Timestamp,
            Config = new PublishedConfigurationRevision
            {
                Revision = 1,
                PublishedAt = Timestamp,
                Label = "SYNTHETIC EXAMPLE - NOT A PRODUCTION VALUE",
                DirtyThreshold = DirtyThreshold,
                StaleThresholdMs = 5000,
            },
            DeviceProfile = DeviceProfile.SIMULATOR,
            WallMap = BuildWallMap(),
            Sensors = sensors,
            WaterJets = WaterJetTopologyCatalog.WaterJets,
            IsolationValves = WaterJetTopologyCatalog.IsolationValves,
            Walls = walls,
            ActiveJob = BuildActiveJob(),
            Pump = new PumpState
            {
                State = PumpRunState.RUNNING,
                Pressure = 37.5,
                Setpoint = 40.5,
                ReadyBandLow = 36.5,
                ReadyBandHigh = 42.5,
                Ready = true,
                StopRequestedAt = null,
            },
            Queue = new QueueSummary
            {
                Label = "GlobalQueue (ready-to-dispatch, bounded)",
                Capacity = QueueSummary.MaxEntries,
                TotalQueued = 3,
                Revision = 5,
                Entries = BuildQueueEntries(),
                AutoSequence = AutoSequenceState.JOB_ACTIVE,
                LastDispatch = BuildDispatch(),
            },
            Sequence = new SequenceState
            {
                AutoSequence = AutoSequenceState.JOB_ACTIVE,
                Mode = AutoSequenceMode.RUNNING,
                Controls = new SequenceControls
                {
                    Start = new SequenceControlAvailability { Enabled = false, Reason = "ALREADY_RUNNING" },
                    PauseAfterCurrentJob = new SequenceControlAvailability { Enabled = true, Reason = null },
                    Resume = new SequenceControlAvailability { Enabled = false, Reason = "NOT_PAUSED" },
                    AbortActiveJob = new SequenceControlAvailability { Enabled = false, Reason = "SAFE_RETURN_IN_PROGRESS" },
                    ResetCritical = new SequenceControlAvailability { Enabled = false, Reason = "CRITICAL_NOT_LATCHED" },
                    PumpStart = new SequenceControlAvailability { Enabled = false, Reason = "PUMP_RUNNING" },
                },
                Critical = null,
                LastJobOutcome = null,
                EquipmentFaults = null,
            },
            Alarms = new AlarmSummary
            {
                ActiveUnack = 1,
                ActiveAck = 0,
                ClearedUnack = 0,
                Items =
                [
                    new AlarmItem
                    {
                        AlarmId = "A-1",
                        Code = "SYN_DEVICE_TIMEOUT_EXAMPLE",
                        Text = "Synthetic timeout example (fixture presentation; not a Production alarm definition)",
                        Severity = AlarmSeverity.LOW,
                        State = "ACTIVE_UNACK",
                        SensorId = null,
                        DeviceId = "SYN-TC-08",
                        RaisedAt = Timestamp,
                    },
                ],
            },
            Communication = new CommunicationHealth
            {
                Devices = Enumerable.Range(1, 8).Select(i => new DeviceHealth
                {
                    DeviceId = $"SYN-TC-{i:D2}",
                    State = DeviceLinkState.ONLINE,
                    ConsecutiveTimeouts = 0,
                    LastSuccessAt = Timestamp,
                    LastLatencyMs = 5 + i,
                    PollsOk = 60 + i,
                    PollsFailed = 0,
                }).ToArray(),
            },
            Runtime = new RuntimeHealth
            {
                UptimeSeconds = 42,
                SseClients = 1,
                InvariantViolations = 0,
                AcceptedSecondJobs = 0,
                RefusedSecondJobs = 0,
                Historian = new HistorianHealth
                {
                    Wired = false,
                    Depth = 0,
                    Capacity = 0,
                    NearOverflow = false,
                    Rejected = 0,
                    LastBatchLatencyMs = null,
                    GapMarkers = 0,
                },
                CurrentRevision = SnapshotRevision,
                StageMarker = Stage03A1.Marker,
            },
            Trend = new TrendWindow
            {
                Capacity = 600,
                SeriesNames = ["fixture-1", "fixture-2", "fixture-3", "fixture-4"],
                Points =
                [
                    new TrendPoint
                    {
                        T = 1791331200,
                        Series = [1.5, null, 2.5, 2.0],
                        Setpoint = 40.5,
                        JobActive = true,
                        AlarmActive = false,
                    },
                    new TrendPoint
                    {
                        T = 1791331201,
                        Series = [2.0, 2.0, 2.5, 3.0],
                        Setpoint = 40.5,
                        JobActive = true,
                        AlarmActive = false,
                    },
                ],
            },
        };
    }

    public static OperationalDelta BuildBasicDelta() => new()
    {
        Kind = "delta",
        Schema = SchemaIds.Delta,
        ApiVersion = SchemaIds.ApiVersion,
        PreviousRevision = 10,
        Revision = 11,
        GeneratedAt = Timestamp,
        Sensors = [BuildSensor(1, false, false), BuildSensor(2, false, false)],
        // Exercises the accepted three-state encoding: an explicit-null
        // activeJob key CLEARS the Active Job (absent would mean unchanged).
        ActiveJob = Optional<ActiveCleaningJobState>.Cleared,
        Pump = new PumpState
        {
            State = PumpRunState.STOPPING,
            Pressure = 20.5,
            Setpoint = 40.5,
            ReadyBandLow = 36.5,
            ReadyBandHigh = 42.5,
            Ready = false,
            StopRequestedAt = Timestamp,
        },
        TrendPoint = new TrendPoint
        {
            T = 1791331202,
            Series = [2.5, 2.0, 1.5, 3.5],
            Setpoint = 40.5,
            JobActive = true,
            AlarmActive = false,
        },
    };

    public static OperationalDelta BuildGapDelta() => new()
    {
        Kind = "delta",
        Schema = SchemaIds.Delta,
        ApiVersion = SchemaIds.ApiVersion,
        PreviousRevision = 7,
        Revision = 12,
        GeneratedAt = Timestamp,
        Runtime = BuildSnapshot().Runtime with { CurrentRevision = 12 },
    };

    public static JsonObject WithStatus(JsonNode payload)
    {
        var obj = payload.AsObject();
        obj.Insert(0, "fixtureStatus", JsonValue.Create(FixtureStatus));
        return obj;
    }

    public static string SerializeWithStatus<T>(T value) =>
        WithStatus(JsonNode.Parse(JsonSerializer.Serialize(value, ContractJson.Options))!).ToJsonString(ContractJson.Options);

    public static string SerializeRaw<T>(T value) =>
        JsonSerializer.Serialize(value, ContractJson.Options);
}
