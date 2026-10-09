using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core;
using Wjss.Time;

namespace Wjss.Runtime;

/// <summary>
/// Structured 503 body of the read-only endpoints when the runtime cannot answer
/// (for example before the state store exists). Machine-readable code, no silent
/// fallback and no partial payload.
/// </summary>
public sealed record RuntimeUnavailablePayload
{
    public required string Code { get; init; }
    public required string Detail { get; init; }
    public required string StageMarker { get; init; }
}

/// <summary>
/// One NON_SENSOR_GAP position as the read-only API presents it: the Owner-facing
/// logical reference (I7, I16) together with the Water Jet it physically anchors,
/// which is never replaced or re-derived. The logical label is a display
/// projection of the slot's logical column (the accepted labelling
/// <c>I&lt;logicalColumn&gt;</c>). A gap position is a location anchor only — it is
/// never a Sensor and never a Water Jet identity.
/// </summary>
public sealed record RuntimeGapReference
{
    /// <summary>Owner-facing logical reference: I7 (Rear, anchors WJ3) and I16 (Front, anchors WJ1).</summary>
    public required string LogicalLabel { get; init; }

    /// <summary>The Water Jet physically anchored at this gap position (I7 → WJ3, I16 → WJ1).</summary>
    public required string GapAnchorForWaterJetId { get; init; }

    /// <summary>Logical matrix row of the position (5 for both gaps).</summary>
    public required int LogicalRow { get; init; }

    /// <summary>Logical matrix column of the position (7 Rear, 16 Front).</summary>
    public required int LogicalColumn { get; init; }
}

/// <summary>One approved Water Jet as the read-only status presents it: a configuration topology reference, never a control handle.</summary>
public sealed record RuntimeWaterJetView
{
    public required string WaterJetId { get; init; }
    public required Wall InstalledWall { get; init; }
    public required Region InstalledRegion { get; init; }
    public required WaterJetPlacementKind PlacementKind { get; init; }
    public required IReadOnlyList<string> PlacementAnchors { get; init; }
    public required Wall TargetWall { get; init; }
    public required Region TargetRegion { get; init; }

    /// <summary>The Isolation Valve dedicated to this Water Jet by the WJn ↔ IVn pairing.</summary>
    public required string DedicatedIsolationValveId { get; init; }

    public static RuntimeWaterJetView From(WaterJetConfiguration waterJet) => new()
    {
        WaterJetId = waterJet.WaterJetId,
        InstalledWall = waterJet.InstalledWall,
        InstalledRegion = waterJet.InstalledRegion,
        PlacementKind = waterJet.PlacementKind,
        PlacementAnchors = waterJet.PlacementAnchors,
        TargetWall = waterJet.TargetWall,
        TargetRegion = waterJet.TargetRegion,
        DedicatedIsolationValveId = waterJet.DedicatedIsolationValveId,
    };
}

/// <summary>One approved Isolation Valve as the read-only status presents it.</summary>
public sealed record RuntimeIsolationValveView
{
    public required string ValveId { get; init; }

    /// <summary>The Water Jet this valve serves (the reverse half of the WJn ↔ IVn pairing).</summary>
    public required string ServedWaterJetId { get; init; }

    public static RuntimeIsolationValveView From(IsolationValveConfiguration valve) => new()
    {
        ValveId = valve.ValveId,
        ServedWaterJetId = valve.ServedWaterJetId,
    };
}

/// <summary>
/// The approved equipment topology as the read-only status presents it: static
/// configuration references (which device is paired with which, where each is
/// installed, which wall it covers) — observation only, never control. Device
/// acquisition is deferred and no acquisition binding exists in this checkpoint.
/// </summary>
public sealed record RuntimeEquipmentTopology
{
    /// <summary>Fixed acquisition status of this checkpoint: no Water Jet or Isolation Valve is bound to any acquisition path.</summary>
    public const string AcquisitionStatus = "DEFERRED_NO_ACQUISITION_BINDING";

    public required string Acquisition { get; init; }
    public required IReadOnlyList<RuntimeWaterJetView> WaterJets { get; init; }
    public required IReadOnlyList<RuntimeIsolationValveView> IsolationValves { get; init; }

    public static RuntimeEquipmentTopology Canonical() => new()
    {
        Acquisition = AcquisitionStatus,
        WaterJets = [.. WaterJetTopologyCatalog.WaterJets.Select(RuntimeWaterJetView.From)],
        IsolationValves = [.. WaterJetTopologyCatalog.IsolationValves.Select(RuntimeIsolationValveView.From)],
    };
}

/// <summary>One protected-baseline count of the synthetic map, as observed by the host.</summary>
public sealed record RuntimeSensorCount
{
    public required string Label { get; init; }
    public required int Count { get; init; }
}

/// <summary>Read-only wall summary for the Runtime status payload.</summary>
public sealed record RuntimeWallSummaryView
{
    public required Wall Wall { get; init; }
    public required int Total { get; init; }
    public required int Dirty { get; init; }
    public required int Cleaner { get; init; }
    public required int NotClassified { get; init; }
    public required int Uncertain { get; init; }
    public double? MaxScore { get; init; }

    /// <summary>Projects one contract wall summary onto the status view.</summary>
    public static RuntimeWallSummaryView From(WallSummary summary) => new()
    {
        Wall = summary.Wall,
        Total = summary.Total,
        Dirty = summary.Dirty,
        Cleaner = summary.Cleaner,
        NotClassified = summary.NotClassified,
        Uncertain = summary.Uncertain,
        MaxScore = summary.MaxScore,
    };
}

/// <summary>Protected-baseline counts carried by the Runtime status payload.</summary>
public sealed record RuntimeStatusSensorCounts
{
    /// <summary>108 logical slots (106 Sensors + 2 NON_SENSOR_GAP positions).</summary>
    public required RuntimeSensorCount LogicalSlots { get; init; }

    /// <summary>106 Sensor locations (the adapter's own count, so drift is visible).</summary>
    public required RuntimeSensorCount SensorLocations { get; init; }

    /// <summary>212 Thermocouple channels.</summary>
    public required RuntimeSensorCount ThermocoupleChannels { get; init; }

    /// <summary>2 NON_SENSOR_GAP positions (not Sensors, not equipment).</summary>
    public required RuntimeSensorCount NonSensorGaps { get; init; }

    /// <summary>
    /// The two NON_SENSOR_GAP positions in canonical orderTotal order (I7 Rear, I16 Front), each
    /// carrying its Owner-facing logical reference and the Water Jet it anchors.
    /// </summary>
    public required IReadOnlyList<RuntimeGapReference> NonSensorGapSlots { get; init; }

    /// <summary>8 approved Water Jets (paired one-to-one with the Isolation Valves).</summary>
    public required RuntimeSensorCount WaterJets { get; init; }

    /// <summary>8 approved Isolation Valves.</summary>
    public required RuntimeSensorCount IsolationValves { get; init; }

    /// <summary>The approved equipment topology (read-only configuration references; acquisition deferred).</summary>
    public required RuntimeEquipmentTopology EquipmentTopology { get; init; }

    /// <summary>The counts the adapter publishes for this build.</summary>
    public static RuntimeStatusSensorCounts Canonical() => new()
    {
        LogicalSlots = new RuntimeSensorCount
        {
            Label = nameof(CanonicalSensorMap.MatrixSlots),
            Count = CanonicalSensorMap.MatrixSlots,
        },
        SensorLocations = new RuntimeSensorCount
        {
            Label = nameof(SyntheticSensorMap.SensorCount),
            Count = SyntheticSensorMap.SensorCount,
        },
        ThermocoupleChannels = new RuntimeSensorCount
        {
            Label = nameof(CanonicalSensorMap.ThermocoupleChannelCount),
            Count = CanonicalSensorMap.ThermocoupleChannelCount,
        },
        NonSensorGaps = new RuntimeSensorCount
        {
            Label = nameof(CanonicalSensorMap.NonSensorGapCount),
            Count = CanonicalSensorMap.NonSensorGapCount,
        },
        NonSensorGapSlots = [.. CanonicalSensorMap.NonSensorGapSlots
            .Select(slot => new RuntimeGapReference
            {
                LogicalLabel = slot.LogicalId,
                GapAnchorForWaterJetId = slot.AnchorWaterJetId,
                LogicalRow = slot.Row,
                LogicalColumn = slot.Column,
            })],
        WaterJets = new RuntimeSensorCount
        {
            Label = nameof(WaterJetTopologyCatalog.WaterJets),
            Count = WaterJetTopologyCatalog.WaterJets.Count,
        },
        IsolationValves = new RuntimeSensorCount
        {
            Label = nameof(WaterJetTopologyCatalog.IsolationValves),
            Count = WaterJetTopologyCatalog.IsolationValves.Count,
        },
        EquipmentTopology = RuntimeEquipmentTopology.Canonical(),
    };
}

/// <summary>
/// Read-only Runtime status (the <c>GET /api/v1/runtime</c> body). It reports
/// observation, never control: no command, no setpoint write, no dispatch. The
/// Queue and Active Job report SIMULATOR sequencing observations. The Pump
/// command block remains a disabled foundation placeholder; nothing is actuated.
/// </summary>
public sealed record RuntimeStatus
{
    /// <summary>Read-only SIMULATOR sequencing status label.</summary>
    public const string QueuePlaceholderLabel = "SIMULATOR sequencing state is available for read-only observation";

    /// <summary>Placeholder label of the Pump block: no pump command path exists in this checkpoint.</summary>
    public const string PumpPlaceholderLabel = "foundation placeholder - no pump command path in this checkpoint";

    /// <summary>True only when every readiness condition holds; the codes are <see cref="RuntimeReadinessCodes"/>.</summary>
    public required bool Ready { get; init; }
    public required string Scenario { get; init; }

    public required string ReadinessCode { get; init; }
    public required string ReadinessDetail { get; init; }

    public required DeviceProfile Profile { get; init; }

    /// <summary>Authoritative-state owner stage marker (diagnostics identity, not a claim of completeness).</summary>
    public required string StageMarker { get; init; }

    /// <summary>Current committed revision, or null when the store was never initialized.</summary>
    public required int? CurrentRevision { get; init; }

    /// <summary>Instant of the current committed revision, when one exists.</summary>
    public required string? GeneratedAt { get; init; }

    /// <summary>Instant of the last committed revision observed by the host (same as <see cref="GeneratedAt"/> when initialized).</summary>
    public required string? LastUpdateAt { get; init; }

    public required ulong SyntheticSeed { get; init; }
    public required int TickIntervalMilliseconds { get; init; }

    public required bool EvolutionRunning { get; init; }

    public required RuntimeStatusSensorCounts SensorCounts { get; init; }

    /// <summary>Wall summaries of the current revision; empty before initialization.</summary>
    public required IReadOnlyList<RuntimeWallSummaryView> Walls { get; init; }

    /// <summary>Queued entries of the bounded GlobalQueue (head-only dispatch is not implemented).</summary>
    public required int QueueCount { get; init; }

    public required int QueueCapacity { get; init; }

    /// <summary>Read-only SIMULATOR sequencing status label.</summary>
    public required string QueuePlaceholder { get; init; }

    public required bool ActiveJobPresent { get; init; }
    public required string? ActiveJobId { get; init; }

    public required PumpRunState PumpState { get; init; }

    /// <summary>Placeholder label: the pump command path is not implemented in this checkpoint.</summary>
    public required string PumpPlaceholder { get; init; }

    public required int ActiveAlarmCount { get; init; }
    public required int ClearedAlarmCount { get; init; }

    public required long AcceptedTicks { get; init; }
    public required long RejectedTransitions { get; init; }

    public required int StateHistoryDepth { get; init; }
    public required int StateHistoryCapacity { get; init; }
    public required int DeltaHistoryDepth { get; init; }
    public required int DeltaHistoryCapacity { get; init; }
    public required int? NewestDeltaRevision { get; init; }

    public required double UptimeSeconds { get; init; }

    public required string ServerTimeUtc { get; init; }

    public required string? LastFaultCode { get; init; }
    public required string? LastFaultDetail { get; init; }
    public required string? StartupFaultCode { get; init; }
}

/// <summary>One Sensor as the read-only API presents it (a projection, never a control handle).</summary>
public sealed record RuntimeSensorView
{
    public required string SensorId { get; init; }
    public required Wall Wall { get; init; }
    public required int LogicalRow { get; init; }
    public required int LogicalColumn { get; init; }
    public required int WallRow { get; init; }
    public required int WallColumn { get; init; }
    public required int ScanOrder { get; init; }
    public required string DeviceId { get; init; }
    public required string TcFrontChannel { get; init; }
    public required string TcRearChannel { get; init; }
    public double? DirtyScore { get; init; }
    public double? LastValidatedScore { get; init; }
    public required Classification Classification { get; init; }
    public required ClassificationBasis ClassificationBasis { get; init; }
    public required Quality Quality { get; init; }
    public string? QualityReason { get; init; }
    public string? SourceTimestamp { get; init; }
    public required QueueState QueueState { get; init; }
    public required bool IsActiveJobTarget { get; init; }

    /// <summary>The cleaning device assigned to this Sensor (targets this Sensor's wall; may be installed on the opposite wall).</summary>
    public required string AssignedWaterJetId { get; init; }

    /// <summary>Derived only through the WJn ↔ IVn pairing of the assigned Water Jet.</summary>
    public required string AssignedIsolationValveId { get; init; }

    /// <summary>Projects one contract Sensor record onto the read-only API view.</summary>
    public static RuntimeSensorView From(SensorPresentationState sensor) => new()
    {
        SensorId = sensor.SensorId,
        Wall = sensor.Wall,
        LogicalRow = sensor.LogicalRow,
        LogicalColumn = sensor.LogicalColumn,
        WallRow = sensor.WallRow,
        WallColumn = sensor.WallColumn,
        ScanOrder = sensor.ScanOrder,
        DeviceId = sensor.DeviceId,
        TcFrontChannel = sensor.TcFrontChannel,
        TcRearChannel = sensor.TcRearChannel,
        DirtyScore = sensor.DirtyScore,
        LastValidatedScore = sensor.LastValidatedScore,
        Classification = sensor.Classification,
        ClassificationBasis = sensor.ClassificationBasis,
        Quality = sensor.Quality,
        QualityReason = sensor.QualityReason,
        SourceTimestamp = sensor.SourceTimestamp,
        QueueState = sensor.QueueState,
        IsActiveJobTarget = sensor.IsActiveJobTarget,
        AssignedWaterJetId = sensor.AssignedWaterJetId,
        AssignedIsolationValveId = sensor.AssignedIsolationValveId,
    };
}

/// <summary>
/// One retained Delta as the development feed presents it: what changed, how much
/// changed, and whether the feed itself has a gap at this point. It carries no
/// control handle and no unbounded payload.
/// </summary>
public sealed record RuntimeDeltaFeedItem
{
    public required int Revision { get; init; }
    public required int PreviousRevision { get; init; }
    public required string GeneratedAt { get; init; }
    public required int ChangedSensorCount { get; init; }
    public required int ChangedWallCount { get; init; }

    /// <summary>Section names this Delta carries (wallMap is never one of them).</summary>
    public required IReadOnlyList<string> ChangedSections { get; init; }

    /// <summary>Three-state Active Job encoding of this Delta: absent, present or cleared.</summary>
    public required DeltaJobEncoding ActiveJobEncoding { get; init; }

    /// <summary>True when this entry continues its retained neighbour: a consumer holding <see cref="PreviousRevision"/> steps cleanly onto <see cref="Revision"/>.</summary>
    public required bool AppliesCleanly { get; init; }

    /// <summary>
    /// True only when this entry provably does NOT continue its newer retained
    /// neighbour: the neighbour declared this entry's predecessor revision, and a
    /// mismatch means a step is missing between them. The newest retained entry has
    /// no newer neighbour and is therefore never flagged, and the oldest retained
    /// entry is judged only against its retained neighbour - the window boundary
    /// never fabricates a gap for history the bounded feed does not hold.
    /// </summary>
    public required bool HasRevisionGap { get; init; }
}

/// <summary>
/// Bounded recent Delta activity (the <c>GET /api/v1/deltas</c> body). The feed is
/// capped by <see cref="Capacity"/> and never exposes unbounded history.
///
/// Chain continuity is a property of the RETENTION ORDER, not of the whole
/// history: the run is newest first, so an entry continues the chain when its own
/// <c>revision</c> equals the <c>previousRevision</c> its newer neighbour declared
/// (equivalently, when its <c>previousRevision</c> equals the next older retained
/// entry's <c>revision</c>). A provable mismatch is reported as data
/// (<see cref="RuntimeDeltaFeedItem.HasRevisionGap"/>), never silently closed.
/// <see cref="CurrentRevision"/> is reported as context only and is deliberately
/// NOT compared against every row: a bounded window must not look like a gap.
/// </summary>
public sealed record RuntimeDeltaFeed
{
    public required int Capacity { get; init; }
    public required int Count { get; init; }
    public required int? CurrentRevision { get; init; }
    public required int? NewestDeltaRevision { get; init; }

    /// <summary>Newest entry first (the history's own documented order).</summary>
    public required bool NewestFirst { get; init; }

    public required IReadOnlyList<RuntimeDeltaFeedItem> Items { get; init; }

    /// <summary>Metadata from one atomic publication; not a full Delta wire body.</summary>
    public static RuntimeDeltaFeed From(RuntimePublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        var items = new List<RuntimeDeltaFeedItem>(publication.Deltas.Count);
        int? expectedRevision = null;
        foreach (var delta in publication.Deltas)
        {
            var gap = expectedRevision is not null && delta.Revision != expectedRevision;
            items.Add(ToItem(delta, gap));
            expectedRevision = delta.PreviousRevision;
        }
        return new RuntimeDeltaFeed
        {
            Capacity = publication.HistoryCapacity,
            Count = items.Count,
            CurrentRevision = publication.Current.Revision,
            NewestDeltaRevision = publication.NewestDeltaRevision,
            NewestFirst = true,
            Items = items,
        };
    }

    /// <summary>Projects the bounded history into the feed view.</summary>
    public static RuntimeDeltaFeed From(RuntimeDeltaHistory history, int? currentRevision)
    {
        ArgumentNullException.ThrowIfNull(history);

        var items = new List<RuntimeDeltaFeedItem>(history.Count);

        // The revision this entry must have to be the immediate predecessor step
        // of the entry walked before it (newest first), or null for the newest
        // retained entry, which has no newer neighbour.
        int? expectedRevision = null;

        foreach (var delta in history.NewestFirst)
        {
            var gap = expectedRevision is not null && delta.Revision != expectedRevision;
            items.Add(ToItem(delta, gap));
            expectedRevision = delta.PreviousRevision;
        }

        return new RuntimeDeltaFeed
        {
            Capacity = history.Capacity,
            Count = items.Count,
            CurrentRevision = currentRevision,
            NewestDeltaRevision = history.NewestRevision,
            NewestFirst = true,
            Items = items,
        };
    }

    private static RuntimeDeltaFeedItem ToItem(RuntimeDelta delta, bool hasRevisionGap)
    {
        var sections = new List<string>();
        if (delta.ChangedSensors.Count > 0)
        {
            sections.Add("sensors");
        }

        if (delta.ChangedWalls.Count > 0)
        {
            sections.Add("walls");
        }

        if (delta.Pump is not null)
        {
            sections.Add("pump");
        }

        if (delta.Queue is not null)
        {
            sections.Add("queue");
        }

        if (delta.Sequence is not null)
        {
            sections.Add("sequence");
        }

        if (delta.Alarms is not null)
        {
            sections.Add("alarms");
        }

        if (delta.Communication is not null)
        {
            sections.Add("communication");
        }

        if (delta.TrendPoint is not null)
        {
            sections.Add("trendPoint");
        }

        if (delta.ActiveJob.Encoding != DeltaJobEncoding.Absent)
        {
            sections.Add("activeJob");
        }

        return new RuntimeDeltaFeedItem
        {
            Revision = delta.Revision,
            PreviousRevision = delta.PreviousRevision,
            GeneratedAt = UtcTimestamps.Format(delta.GeneratedAtUtc),
            ChangedSensorCount = delta.ChangedSensors.Count,
            ChangedWallCount = delta.ChangedWalls.Count,
            ChangedSections = sections,
            ActiveJobEncoding = delta.ActiveJob.Encoding,
            AppliesCleanly = !hasRevisionGap,
            HasRevisionGap = hasRevisionGap,
        };
    }
}
