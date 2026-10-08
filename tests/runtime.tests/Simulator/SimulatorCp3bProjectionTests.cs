using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Runtime.Core.Simulator;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Stage 0.4A CP-3b tests for bounded retention, the queue and Sensor queue-state projection, and the pure candidate
/// Delta path. These tests were written in Arena and are NOT EXECUTED IN ARENA; they are planned for Owner-local
/// execution. Synthetic SYN-* identities only; no clock and no randomness.
/// </summary>
public sealed class SimulatorCp3bProjectionTests
{
    [Fact]
    public void Last_Dispatch_Carries_The_Head_Source_Reason_And_Scenario_Origin()
    {
        var (_, retention) = RunRetained(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.NORMAL_COMPLETION).Events);

        var dispatch = retention.LastDispatch ?? throw new InvalidOperationException("A dispatch was expected.");
        Assert.Equal("SYN-QE-1", dispatch.QueueEntryId);
        Assert.Equal("SYN-S01", dispatch.SensorId);
        Assert.Equal("SCENARIO_PREPARED", dispatch.SourceReason);
        Assert.Equal("SCENARIO_PREPARED", dispatch.Origin);
        Assert.Equal("SYN-JOB-1", dispatch.JobId);
        Assert.Equal(1, dispatch.PositionBefore);
        Assert.Equal(2, dispatch.QueueRevisionBefore);
        Assert.Equal(3, dispatch.QueueRevisionAfter);
    }

    [Fact]
    public void Safe_Return_Tail_Keeps_The_Ordered_Safe_Return_Records()
    {
        var (_, retention) = RunRetained(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.NORMAL_COMPLETION).Events);

        Assert.Equal(
            new[] { "SR1", "SR2", "SR3", "SR4", "SR5", "SR6", "JOB_RELEASED" },
            retention.SafeReturnTail.Select(record => record.Code).ToArray());
    }

    [Fact]
    public void First_Critical_Record_Is_Retained()
    {
        var (_, retention) = RunRetained(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.PUMP_TRIP).Events);

        var first = retention.FirstCriticalEvidence ?? throw new InvalidOperationException("A critical record was expected.");
        Assert.Equal(SequencingCodes.CriticalSuspensionRaised, first.Code);
        Assert.Equal(SequencingCodes.KindObservePumpState, first.EventKind);
    }

    [Fact]
    public void Evidence_Log_Is_Bounded_And_Counts_The_Records_It_Dropped()
    {
        var events = new List<SequencingEvent>(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO).Events.Take(4));
        for (var index = 0; index < 300; index++)
        {
            // A repeated admission of a queued Sensor is a no-op with one evidence record each time.
            events.Add(new AdmitQueueEntry(
                SimulatorScenarioCatalogue.AtTick(5),
                SequencingAdmissionSource.SCENARIO_PREPARED,
                "SYN-S01",
                SimulatorScenarioCatalogue.AdmissionReason,
                0,
                SimulatorScenarioCatalogue.Topology));
        }

        var (_, retention) = RunRetained(events);

        Assert.Equal(SequencingRetention.EvidenceLogCapacity, retention.EvidenceLog.Count);
        Assert.Equal(304 - SequencingRetention.EvidenceLogCapacity, retention.EvidenceDropped);
        Assert.Equal(304 - SequencingRetention.EvidenceLogCapacity + 1, retention.EvidenceLog[0].Seq);
        Assert.Equal(304, retention.EvidenceLog[retention.EvidenceLog.Count - 1].Seq);
    }

    [Fact]
    public void Queue_Projection_Lists_The_Fifo_Entries_With_Capacity_And_Revision()
    {
        var (state, retention) = RunRetained(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO).Events);

        var queue = SequencingRuntimeProjection.ProjectQueue(state, retention);

        Assert.Equal("GlobalQueue", queue.Label);
        Assert.Equal(8, queue.Capacity);
        Assert.Equal(7, queue.TotalQueued);
        Assert.Equal(9, queue.Revision);
        Assert.Equal(1, queue.Entries[0].Position);
        Assert.Equal("SYN-S02", queue.Entries[0].SensorId);
        Assert.Equal(7, queue.Entries[6].Position);
        Assert.Equal(AutoSequenceState.JOB_ACTIVE, queue.AutoSequence);
        Assert.Equal("SYN-S01", queue.LastDispatch?.SensorId);
    }

    [Fact]
    public void Sensor_Queue_States_Mark_The_Target_Active_And_Queued_Entries_Queued()
    {
        var (state, _) = RunRetained(SimulatorScenarioCatalogue.Get(SimulatorScenarioId.QUEUE_CAPACITY_AND_FIFO).Events);
        var template = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed).Sensors[0];
        var sensors = new List<SensorPresentationState>
        {
            template with { SensorId = "SYN-S01" },
            template with { SensorId = "SYN-S02" },
            template with { SensorId = "SYN-S09" },
        };

        var projected = SequencingRuntimeProjection.ProjectSensorQueueStates(state, sensors);

        Assert.Equal(QueueState.ACTIVE, projected[0].QueueState);
        Assert.True(projected[0].IsActiveJobTarget);
        Assert.Equal(QueueState.QUEUED, projected[1].QueueState);
        Assert.False(projected[1].IsActiveJobTarget);
        Assert.Equal(QueueState.NONE, projected[2].QueueState);
        Assert.False(projected[2].IsActiveJobTarget);
    }

    [Fact]
    public void Candidate_Delta_Emits_No_Section_For_A_Rebuilt_Identical_Queue()
    {
        var previous = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var candidate = previous with
        {
            Revision = previous.Revision + 1,
            GeneratedAtUtc = previous.GeneratedAtUtc.AddSeconds(1),
            Queue = previous.Queue with { Entries = new List<QueueEntry>(previous.Queue.Entries) },
        };

        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);

        Assert.Null(delta.Queue);
        Assert.Null(delta.Sequence);
        Assert.Null(delta.Pump);
        Assert.Empty(delta.ChangedSensors);
        Assert.Equal(previous.Revision, delta.PreviousRevision);
        Assert.Equal(candidate.Revision, delta.Revision);
    }

    [Fact]
    public void Candidate_Delta_Emits_The_Queue_Section_When_Its_Content_Changes()
    {
        var previous = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var candidate = previous with
        {
            Revision = previous.Revision + 1,
            GeneratedAtUtc = previous.GeneratedAtUtc.AddSeconds(1),
            Queue = previous.Queue with { Revision = previous.Queue.Revision + 1 },
        };

        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);

        Assert.Equal(candidate.Queue.Revision, delta.Queue?.Revision);
    }

    [Fact]
    public void Candidate_Delta_Carries_Only_The_Changed_Sensor_With_Its_Previous_Record()
    {
        var previous = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var sensors = previous.Sensors.ToList();
        sensors[0] = sensors[0] with { DirtyScore = (sensors[0].DirtyScore ?? 0.0) + 1.0 };
        var candidate = previous with
        {
            Revision = previous.Revision + 1,
            GeneratedAtUtc = previous.GeneratedAtUtc.AddSeconds(1),
            Sensors = sensors,
        };

        var delta = RuntimeDeltaProjector.ProjectCandidate(previous, candidate);

        var changed = Assert.Single(delta.ChangedSensors);
        Assert.Equal(sensors[0].SensorId, changed.SensorId);
        var before = Assert.Single(delta.PreviousSensors);
        Assert.Equal(previous.Sensors[0].DirtyScore, before.DirtyScore);
    }

    [Fact]
    public void Candidate_Delta_Refuses_A_Revision_That_Is_Not_The_Next_Step()
    {
        var previous = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var skipped = previous with
        {
            Revision = previous.Revision + 2,
            GeneratedAtUtc = previous.GeneratedAtUtc.AddSeconds(1),
        };

        var error = Assert.Throws<InvalidOperationException>(() => RuntimeDeltaProjector.ProjectCandidate(previous, skipped));
        Assert.Contains("exactly one revision step", error.Message, StringComparison.Ordinal);
    }

    private static (SequencingState State, SequencingRetention Retention) RunRetained(IEnumerable<SequencingEvent> events)
    {
        var state = SequencingKernel.Initial();
        var retention = SequencingRetention.Empty;
        foreach (var sequencingEvent in events)
        {
            var transition = SequencingKernel.Apply(state, sequencingEvent);
            retention = SequencingRetention.Retain(retention, state, transition);
            state = transition.State;
        }

        return (state, retention);
    }
}
