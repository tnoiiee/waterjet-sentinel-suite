using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.Simulator;

/// <summary>
/// Pure Stage 0.4A CP-3b projection of the sequencing kernel onto two wire areas that the repository fully
/// defines: the GlobalQueue summary and the Sensor queue-state fields (<c>QueueState</c> and
/// <c>IsActiveJobTarget</c>). It projects no Sensor eligibility and no DIRTY-score admission.
///
/// <para>
/// NOT projected here (recorded [OPEN] in docs/MASTER_PLAN.md section 3.3.4): the Active Job section, the Sequence
/// section (controls, critical event and last outcome), the Pump section, and the Safe Return legs. Their
/// presentation fields (PhaseIndex, PhaseProgress and PhaseLabel for P2 to P6, the Pump run-state mapping, and
/// the Command and Feedback strings) are not defined in the repository, so they must not be guessed.
/// </para>
/// </summary>
public static class SequencingRuntimeProjection
{
    /// <summary>
    /// Projects the GlobalQueue summary. Entries are in FIFO order with position 1 at the head. DirtyScore is never
    /// set. <paramref name="retention"/> supplies the last dispatch.
    /// </summary>
    public static QueueSummary ProjectQueue(SequencingState state, SequencingRetention retention)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(retention);

        var entries = SequencingKernel.ProjectQueueEntries(state);
        return new QueueSummary
        {
            Label = RuntimeStateComposer.QueueLabel,
            Capacity = QueueSummary.MaxEntries,
            TotalQueued = entries.Count,
            Revision = state.QueueRevision,
            Entries = entries,
            AutoSequence = SequencingKernel.ProjectAutoSequenceState(state),
            LastDispatch = retention.LastDispatch,
        };
    }

    /// <summary>
    /// Returns the Sensors with <c>QueueState</c> and <c>IsActiveJobTarget</c> set from the kernel state: ACTIVE and
    /// true for the Active Job's target, QUEUED for a Sensor in the queue, and NONE otherwise. The order and every
    /// other field of each Sensor are kept. A queued or active Sensor that is not in <paramref name="sensors"/> is
    /// not added.
    /// </summary>
    public static IReadOnlyList<SensorPresentationState> ProjectSensorQueueStates(
        SequencingState state,
        IReadOnlyList<SensorPresentationState> sensors)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sensors);
        SequencingStateValidator.RequireValid(state);

        var queued = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in state.Queue)
        {
            queued.Add(entry.SensorId);
        }

        var target = state.ActiveJob?.TargetSensorId;
        var projected = new List<SensorPresentationState>(sensors.Count);
        foreach (var sensor in sensors)
        {
            var isTarget = target is not null && string.Equals(sensor.SensorId, target, StringComparison.Ordinal);
            var queueState = isTarget
                ? QueueState.ACTIVE
                : queued.Contains(sensor.SensorId) ? QueueState.QUEUED : QueueState.NONE;
            projected.Add(sensor with { QueueState = queueState, IsActiveJobTarget = isTarget });
        }

        return projected.AsReadOnly();
    }
}
