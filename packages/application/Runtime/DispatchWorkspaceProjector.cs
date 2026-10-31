using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;

namespace Wjss.Runtime.Core;

/// <summary>
/// Projects the read-only H3-A Cleaning Job Workspace document from ONE committed Runtime revision
/// plus the Runtime-owned countdown. The projection selects and orders values that already exist:
/// it reuses the accepted <see cref="QueueSummary"/> and <see cref="ActiveCleaningJobState"/> types
/// unchanged, so the workspace can never disagree with the Snapshot of the same revision.
///
/// <para>
/// The projection owns no state, accepts no command and adds no policy: the timeline stage is
/// derived from the already-published Job lifecycle and verified phase, and the preview is the
/// planner's own view of the CURRENT inputs. A preview never enters the frozen decision.
/// </para>
/// </summary>
public static class DispatchWorkspaceProjector
{
    private static readonly (DispatchTimelineStage Stage, string Label)[] StepTable =
    [
        (DispatchTimelineStage.PRECHECK, "Precheck"),
        (DispatchTimelineStage.MOVE_TO_P1, "Move to P1"),
        (DispatchTimelineStage.OPEN_IV, "Open IV"),
        (DispatchTimelineStage.P2, "P2"),
        (DispatchTimelineStage.P3, "P3"),
        (DispatchTimelineStage.P4, "P4"),
        (DispatchTimelineStage.P5, "P5"),
        (DispatchTimelineStage.P6, "P6"),
        (DispatchTimelineStage.CLOSE_IV, "Close IV"),
        (DispatchTimelineStage.MANDATORY_SAFE_RETURN, "Mandatory Safe Return"),
        (DispatchTimelineStage.COMPLETE, "Complete"),
        (DispatchTimelineStage.NEXT_DISPATCH_COUNTDOWN, "Next dispatch countdown"),
        (DispatchTimelineStage.DISPATCHED, "Dispatched"),
    ];

    /// <summary>The dispatch-cycle steps, in the Owner-approved order with their Operator wording.</summary>
    public static IReadOnlyList<(DispatchTimelineStage Stage, string Label)> OrderedSteps { get; } =
        Array.AsReadOnly(StepTable);

    /// <summary>Projects the workspace at the revision carried by <paramref name="state"/>.</summary>
    public static DispatchWorkspace Project(
        RuntimeState state,
        NextDispatchCountdown countdown,
        TimeDelayConfiguration? configuration,
        RhTimeDelayInput? liveRhInput)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(countdown);

        var nextSensor = HeadSensor(state);
        TimeDelaySensorIdentity? identity = null;
        if (nextSensor is not null)
        {
            TimeDelayPlanner.TryResolveSensorIdentity(nextSensor, out identity, out _);
        }

        var preview = TimeDelayPlanner.Preview(configuration, identity, liveRhInput, state.GeneratedAtUtc);
        var failed = false;
        var stage = CurrentStage(state, countdown, out failed);

        return new DispatchWorkspace
        {
            Kind = SchemaIds.DispatchWorkspaceKind,
            Schema = SchemaIds.DispatchWorkspace,
            ApiVersion = SchemaIds.ApiVersion,
            Revision = state.Revision,
            GeneratedAt = UtcTimestamps.Format(state.GeneratedAtUtc),
            DeviceProfile = state.DeviceProfile,
            StageMarker = RuntimeStage.Marker,
            Simulation = DispatchWorkspace.SimulationNote,
            Queue = state.Queue,
            ActiveJob = state.ActiveJob,
            AutoSequence = state.Sequence.AutoSequence,
            Mode = state.Sequence.Mode,
            Countdown = ProjectCountdown(countdown),
            Timeline = ProjectTimeline(state, countdown, stage, failed),
            Configuration = configuration,
            Preview = preview,
        };
    }

    /// <summary>Projects only the countdown section (used by the state machine and by tests).</summary>
    public static DispatchCountdownView ProjectCountdown(NextDispatchCountdown countdown)
    {
        ArgumentNullException.ThrowIfNull(countdown);

        return new DispatchCountdownView
        {
            Lifecycle = countdown.Lifecycle,
            Generation = countdown.Generation,
            RemainingSeconds = countdown.RemainingSeconds,
            Decision = countdown.Decision,
            Frozen = countdown.Frozen,
            DispatchAdmissible = countdown.DispatchAdmissible,
            SupersededReason = countdown.SupersededReason,
            LastSupersededReason = countdown.LastSupersededReason,
            RefusalCode = countdown.RefusalCode,
            CreatedAt = countdown.Decision?.CreatedAt,
            CompletedAt = countdown.CompletedAtUtc is { } completed ? UtcTimestamps.Format(completed) : null,
            DispatchedAt = countdown.DispatchedAtUtc is { } dispatched ? UtcTimestamps.Format(dispatched) : null,
        };
    }

    /// <summary>The timeline of the dispatch cycle, in contract order, with exactly one current step.</summary>
    public static DispatchTimelineView ProjectTimeline(
        RuntimeState state,
        NextDispatchCountdown countdown,
        DispatchTimelineStage stage,
        bool failed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(countdown);

        var currentIndex = -1;
        for (var index = 0; index < OrderedSteps.Count; index++)
        {
            if (OrderedSteps[index].Stage == stage)
            {
                currentIndex = index;
                break;
            }
        }

        var steps = new List<DispatchTimelineStep>(OrderedSteps.Count);
        for (var index = 0; index < OrderedSteps.Count; index++)
        {
            var stepState = index < currentIndex
                ? DispatchTimelineStepState.COMPLETE
                : index > currentIndex
                    ? DispatchTimelineStepState.PENDING
                    : failed
                        ? DispatchTimelineStepState.FAILED
                        : DispatchTimelineStepState.CURRENT;

            steps.Add(new DispatchTimelineStep
            {
                Stage = OrderedSteps[index].Stage,
                Ordinal = index + 1,
                Label = OrderedSteps[index].Label,
                State = stepState,
            });
        }

        var lastDispatch = state.Queue.LastDispatch;
        return new DispatchTimelineView
        {
            Stage = stage,
            Steps = steps,
            LastDispatchJobId = lastDispatch?.JobId,
            LastDispatchedAt = lastDispatch?.DispatchedAt,
        };
    }

    /// <summary>
    /// The current step of the dispatch cycle: the FIRST step the published evidence has not
    /// completed. A Job's own steps win while a Job exists; after release the cycle continues with
    /// COMPLETE, then the frozen countdown, then the single dispatch.
    /// </summary>
    public static DispatchTimelineStage CurrentStage(
        RuntimeState state,
        NextDispatchCountdown countdown,
        out bool failed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(countdown);
        failed = false;

        var job = state.ActiveJob;
        if (job is not null)
        {
            if (job.Lifecycle == JobLifecycle.SAFE_RETURN_FAILED)
            {
                failed = true;
                return DispatchTimelineStage.MANDATORY_SAFE_RETURN;
            }

            if (job.Lifecycle is JobLifecycle.ABORTING
                or JobLifecycle.SAFE_RETURN_CLOSE_VALVE
                or JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED
                or JobLifecycle.SAFE_RETURN_TO_STANDBY
                or JobLifecycle.SAFE_RETURN_VERIFY_STANDBY)
            {
                return DispatchTimelineStage.MANDATORY_SAFE_RETURN;
            }

            return job.VerifiedPhase switch
            {
                JobPhase.P1 => DispatchTimelineStage.P2,
                JobPhase.P2 => DispatchTimelineStage.P3,
                JobPhase.P3 => DispatchTimelineStage.P4,
                JobPhase.P4 => DispatchTimelineStage.P5,
                JobPhase.P5 => DispatchTimelineStage.P6,
                JobPhase.P6 => DispatchTimelineStage.CLOSE_IV,
                _ => job.ValveOpenCommanded ? DispatchTimelineStage.OPEN_IV : DispatchTimelineStage.MOVE_TO_P1,
            };
        }

        return countdown.Lifecycle switch
        {
            CountdownLifecycleState.ACTIVE_FROZEN => DispatchTimelineStage.NEXT_DISPATCH_COUNTDOWN,
            CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH => DispatchTimelineStage.NEXT_DISPATCH_COUNTDOWN,
            CountdownLifecycleState.DISPATCHED => DispatchTimelineStage.DISPATCHED,
            CountdownLifecycleState.SUPERSEDED => DispatchTimelineStage.COMPLETE,
            CountdownLifecycleState.CANCELLED => DispatchTimelineStage.COMPLETE,
            CountdownLifecycleState.FAULTED => DispatchTimelineStage.COMPLETE,
            _ => state.Queue.LastDispatch is null
                ? DispatchTimelineStage.PRECHECK
                : DispatchTimelineStage.COMPLETE,
        };
    }

    private static SensorPresentationState? HeadSensor(RuntimeState state)
    {
        if (state.Queue.Entries.Count == 0) return null;
        var sensorId = state.Queue.Entries[0].SensorId;
        foreach (var sensor in state.Sensors)
        {
            if (string.Equals(sensor.SensorId, sensorId, StringComparison.Ordinal)) return sensor;
        }

        return null;
    }
}
