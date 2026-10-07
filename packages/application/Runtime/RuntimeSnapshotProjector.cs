using Wjss.Contracts;
using Wjss.Time;

namespace Wjss.Runtime.Core;

/// <summary>
/// Identity of the authoritative-state implementation currently running. The
/// marker moves with the substage that actually exists: it is reported in the
/// snapshot's runtime block so no consumer can mistake the foundation for the
/// finished Runtime.
/// </summary>
public static class RuntimeStage
{
    /// <summary>Stage 0.3A-2A: state store, deterministic initial state and snapshot projection.</summary>
    public const string Marker = "STAGE_03A2A_STATE_STORE";
}

/// <summary>
/// Projects one Runtime revision onto the accepted <c>wjss.snapshot/1</c>
/// contract. The projection is the only place the Runtime produces the
/// presentation envelope, so envelope identity (kind, schema, API version) and
/// the diagnostics block cannot drift between callers.
///
/// The projector expects a state that the Runtime State Store has already
/// validated; it adds no second policy. Diagnostics that are not implemented in
/// Stage 0.3A-2A are projected as explicitly unwired or zero rather than
/// omitted: no SSE client exists (<c>sseClients = 0</c>), no Historian exists
/// (<c>wired = false</c>), and no job may exist at all - the accepted Single
/// Active Job architecture means the refused-second-job counters stay zero until
/// a later substage introduces job commands.
/// </summary>
public static class RuntimeSnapshotProjector
{
    /// <summary>Envelope discriminator of the snapshot contract.</summary>
    public const string SnapshotKind = "snapshot";

    /// <summary>Projects the state onto the snapshot contract.</summary>
    public static OperationalSnapshot Project(
        RuntimeState state,
        RuntimeStoreCounters counters,
        double uptimeSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(counters);

        if (uptimeSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(uptimeSeconds), uptimeSeconds, "Uptime cannot be negative.");
        }

        return new OperationalSnapshot
        {
            Kind = SnapshotKind,
            Schema = SchemaIds.Snapshot,
            ApiVersion = SchemaIds.ApiVersion,
            Revision = state.Revision,
            GeneratedAt = UtcTimestamps.Format(state.GeneratedAtUtc),
            Config = state.Config,
            DeviceProfile = state.DeviceProfile,
            WallMap = state.WallMap,
            Sensors = state.Sensors,
            Walls = state.Walls,
            ActiveJob = state.ActiveJob,
            Pump = state.Pump,
            Queue = state.Queue,
            Sequence = state.Sequence,
            Alarms = state.Alarms,
            Communication = state.Communication,
            Runtime = new RuntimeHealth
            {
                UptimeSeconds = uptimeSeconds,
                SseClients = 0,
                InvariantViolations = counters.InvalidStateRefusals,
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
                CurrentRevision = state.Revision,
                StageMarker = RuntimeStage.Marker,
            },
            Trend = state.Trend,
        };
    }
}
