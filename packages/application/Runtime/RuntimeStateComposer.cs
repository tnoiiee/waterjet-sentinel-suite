using Wjss.Contracts;
using Wjss.Domain;

namespace Wjss.Runtime.Core;

/// <summary>
/// Deterministic composition of the initial Runtime revision for Stage
/// 0.3A-2A. The composition is a pure function of its inputs: the same
/// Published Configuration identity, the same Sensor projections (which the
/// SIMULATOR source derives from an explicit seed and clock instant) and the
/// same instant produce the same state, byte-stable through the Snapshot
/// projection.
///
/// SIMULATOR-only boundary: composition runs the same domain gate startup uses
/// (<see cref="ProfileStartPolicy"/>), so TEST_HARDWARE or PRODUCTION can never
/// be composed into a state - there is no fallback to SIMULATOR and no partial
/// composition.
///
/// Scope honesty of the initial revision: Stage 0.3A-2A has no acquisition, no
/// queue dispatch, no job execution and no pump command path, so the
/// non-process blocks are composed as explicitly unavailable:
/// <list type="bullet">
///   <item>no Active Job exists (null),</item>
///   <item>the GlobalQueue is empty at revision 0,</item>
///   <item>every sequence control is disabled with reason
///   <see cref="ControlNotImplementedReason"/>,</item>
///   <item>the Pump is STOPPED with <c>Ready = false</c>. No pump setpoint is
///   published in 0.3A-2A (the value belongs to Published Configuration, a later
///   substage), so the setpoint and band are written as zero and must be shown
///   as not implemented - never as an established pressure band,</item>
///   <item>communication health publishes no device entry: there is no
///   acquisition evidence yet, and a fabricated ONLINE state is refused by the
///   invariants,</item>
///   <item>the bounded trend window is empty.</item>
/// </list>
/// </summary>
public static class RuntimeStateComposer
{
    /// <summary>Revision of the first published Runtime state.</summary>
    public const int InitialRevision = 1;

    /// <summary>Reason code carried by every sequence control that has no implementation path yet.</summary>
    public const string ControlNotImplementedReason = "CONTROL_NOT_IMPLEMENTED_STAGE_03A_2A";

    /// <summary>Presentation label of the bounded GlobalQueue.</summary>
    public const string QueueLabel = "GlobalQueue";

    private static readonly string[] TrendSeriesNameSet =
    [
        "pressure",
        "setpoint",
        "ready-band-low",
        "ready-band-high",
    ];

    /// <summary>The fixed-width trend series identity (four series, accepted 0.2.1A baseline).</summary>
    public static IReadOnlyList<string> TrendSeriesNames => Array.AsReadOnly(TrendSeriesNameSet);

    /// <summary>
    /// Composes the initial revision. The instant is supplied by the caller
    /// (read once from the injected clock) so the composed revision and every
    /// timestamp inside it belong to one deterministic instant.
    /// </summary>
    public static RuntimeState ComposeInitial(
        DeviceProfile profile,
        PublishedConfigurationRevision config,
        IReadOnlyList<WallMapSlot> wallMap,
        IReadOnlyList<SensorPresentationState> sensors,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(wallMap);
        ArgumentNullException.ThrowIfNull(sensors);

        if (!ProfileStartPolicy.TryRequireStartable(profile, out var refusalCode, out var refusalReason))
        {
            throw new InvalidOperationException($"[{refusalCode}] {refusalReason}");
        }

        var state = new RuntimeState
        {
            Revision = InitialRevision,
            GeneratedAtUtc = generatedAtUtc.ToUniversalTime(),
            DeviceProfile = profile,
            Config = config,
            WallMap = RuntimeCollections.Freeze(wallMap),
            Sensors = RuntimeCollections.Freeze(sensors),
            Walls = RuntimeWallSummaries.Recalculate(sensors),
            ActiveJob = null,
            Pump = IdlePump(),
            Queue = IdleQueue(),
            Sequence = IdleSequence(),
            Alarms = EmptyAlarms(),
            Communication = NoEvidenceCommunication(),
            Trend = EmptyTrend(),
        };

        RuntimeStateInvariants.RequireValid(state);
        return state;
    }

    private static PumpState IdlePump() => new()
    {
        State = PumpRunState.STOPPED,
        Pressure = null,
        Setpoint = 0.0,
        ReadyBandLow = 0.0,
        ReadyBandHigh = 0.0,
        Ready = false,
        StopRequestedAt = null,
    };

    private static QueueSummary IdleQueue() => new()
    {
        Label = QueueLabel,
        Capacity = QueueSummary.MaxEntries,
        TotalQueued = 0,
        Revision = 0,
        Entries = Array.AsReadOnly(Array.Empty<QueueEntry>()),
        AutoSequence = AutoSequenceState.OFF,
        LastDispatch = null,
    };

    private static SequenceState IdleSequence()
    {
        var unavailable = new SequenceControlAvailability
        {
            Enabled = false,
            Reason = ControlNotImplementedReason,
        };

        return new SequenceState
        {
            AutoSequence = AutoSequenceState.OFF,
            Mode = AutoSequenceMode.OFF,
            Controls = new SequenceControls
            {
                Start = unavailable,
                PauseAfterCurrentJob = unavailable,
                Resume = unavailable,
                AbortActiveJob = unavailable,
                ResetCritical = unavailable,
                PumpStart = unavailable,
            },
            Critical = null,
            LastJobOutcome = null,
        };
    }

    private static AlarmSummary EmptyAlarms() => new()
    {
        ActiveUnack = 0,
        ActiveAck = 0,
        ClearedUnack = 0,
        Items = Array.AsReadOnly(Array.Empty<AlarmItem>()),
    };

    private static CommunicationHealth NoEvidenceCommunication() => new()
    {
        Devices = Array.AsReadOnly(Array.Empty<DeviceHealth>()),
    };

    private static TrendWindow EmptyTrend() => new()
    {
        Capacity = RuntimeLimits.DefaultTrendCapacity,
        SeriesNames = Array.AsReadOnly(TrendSeriesNameSet),
        Points = Array.AsReadOnly(Array.Empty<TrendPoint>()),
    };
}
