using Wjss.Adapters.Simulator;
using Wjss.Contracts;
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
    /// <summary>108 logical slots (106 Sensors + 2 Cannons).</summary>
    public required RuntimeSensorCount LogicalSlots { get; init; }

    /// <summary>106 Sensor locations (the adapter's own count, so drift is visible).</summary>
    public required RuntimeSensorCount SensorLocations { get; init; }

    /// <summary>212 Thermocouple channels.</summary>
    public required RuntimeSensorCount ThermocoupleChannels { get; init; }

    /// <summary>2 Cannon slots.</summary>
    public required RuntimeSensorCount Cannons { get; init; }

    /// <summary>Canonical Cannon equipment labels: I7 and I16.</summary>
    public required IReadOnlyList<RuntimeSensorCount> CannonLabels { get; init; }

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
        Cannons = new RuntimeSensorCount
        {
            Label = nameof(CanonicalSensorMap.CannonSlotCount),
            Count = CanonicalSensorMap.CannonSlotCount,
        },
        CannonLabels = [.. CanonicalSensorMap.CannonSlots
            .Select(slot => new RuntimeSensorCount { Label = slot.EquipmentId, Count = 1 })],
    };
}

/// <summary>
/// Read-only Runtime status (the <c>GET /api/v1/runtime</c> body). It reports
/// observation, never control: no command, no setpoint write, no dispatch. The
/// queue, Pump and Active Job blocks are placeholders of the current foundation
/// (no dispatch path and no pump command path exists yet) and are labelled as
/// such so no review mistakes them for implemented Product behaviour.
/// </summary>
public sealed record RuntimeStatus
{
    /// <summary>Placeholder label of the queue block: no dispatch path exists in this checkpoint.</summary>
    public const string QueuePlaceholderLabel = "foundation placeholder - no queue dispatch path in this checkpoint";

    /// <summary>Placeholder label of the Pump block: no pump command path exists in this checkpoint.</summary>
    public const string PumpPlaceholderLabel = "foundation placeholder - no pump command path in this checkpoint";

    /// <summary>True only when every readiness condition holds; the codes are <see cref="RuntimeReadinessCodes"/>.</summary>
    public required bool Ready { get; init; }

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

    /// <summary>Placeholder label: queue dispatch is not implemented in this checkpoint.</summary>
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

    /// <summary>True when applying this Delta to a consumer holding <see cref="PreviousRevision"/> performs a clean step.</summary>
    public required bool AppliesCleanly { get; init; }

    /// <summary>True when this entry sits after a missing predecessor: the feed reports the gap instead of hiding it.</summary>
    public required bool HasRevisionGap { get; init; }
}

/// <summary>
/// Bounded recent Delta activity (the <c>GET /api/v1/deltas</c> body). The feed is
/// capped by <see cref="Capacity"/> and never exposes unbounded history; a gap in
/// the retained run is reported as data (<see cref="RuntimeDeltaFeedItem.HasRevisionGap"/>),
/// never silently closed.
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

    /// <summary>Projects the bounded history into the feed view.</summary>
    public static RuntimeDeltaFeed From(RuntimeDeltaHistory history, int? currentRevision)
    {
        ArgumentNullException.ThrowIfNull(history);

        var items = new List<RuntimeDeltaFeedItem>(history.Count);
        int? expectedNext = null;

        foreach (var delta in history.NewestFirst)
        {
            var gap = expectedNext is not null && delta.PreviousRevision != expectedNext;
            items.Add(ToItem(delta, gap));
            expectedNext = delta.PreviousRevision;
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
