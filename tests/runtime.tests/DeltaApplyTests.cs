using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// The strict Delta application path: sequential application reproduces direct
/// evolution, a revision gap demands a fresh Snapshot and applies nothing, a
/// malformed or self-inconsistent Delta is refused rather than normalized, and the
/// three-state Active Job slot applies unchanged / replaced / cleared.
/// </summary>
public sealed class DeltaApplyTests
{
    [Fact]
    public void Sequential_Apply_Reconstructs_Direct_Evolution()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var direct = store.Current;
        var deltas = new List<RuntimeDelta>();

        for (var tick = 1; tick <= 4; tick++)
        {
            var (committed, delta) = RuntimeDeltaTestFixture.CommitTick(
                writer, direct, tick, RuntimeTestFixture.Instant.AddSeconds(tick));

            deltas.Add(delta);
            direct = committed;
        }

        var reconstructed = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        foreach (var delta in deltas)
        {
            var outcome = RuntimeDeltaApply.Apply(reconstructed, delta);

            Assert.True(outcome.Applied, outcome.Reason);
            Assert.False(outcome.ResnapshotRequired);
            reconstructed = outcome.State!;
        }

        Assert.Equal(direct.Revision, reconstructed.Revision);
        Assert.Equal(direct.GeneratedAtUtc, reconstructed.GeneratedAtUtc);
        Assert.Equal(direct.Sensors, reconstructed.Sensors);
        Assert.Equal(direct.Walls, reconstructed.Walls);
        Assert.Equal(direct.Trend.Points, reconstructed.Trend.Points);
        Assert.Equal(direct.ActiveJob, reconstructed.ActiveJob);
        Assert.Equal(RuntimeTestFixture.SerializeSnapshot(direct), RuntimeTestFixture.SerializeSnapshot(reconstructed));
    }

    [Fact]
    public void A_Revision_Gap_Requires_A_Fresh_Snapshot_And_Applies_Nothing()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;

        var (firstCommitted, _) = RuntimeDeltaTestFixture.CommitTick(
            writer, state, 1, RuntimeTestFixture.Instant.AddSeconds(1));
        var (_, secondDelta) = RuntimeDeltaTestFixture.CommitTick(
            writer, firstCommitted, 2, RuntimeTestFixture.Instant.AddSeconds(2));

        // The consumer still holds revision 1 and now receives the Delta of
        // revision 3: the missing step is a gap, not something to infer.
        var outcome = RuntimeDeltaApply.Apply(state, secondDelta);

        Assert.False(outcome.Applied);
        Assert.True(outcome.ResnapshotRequired);
        Assert.Equal(RuntimeRefusalCodes.DeltaOutOfOrder, outcome.Code);
        Assert.Null(outcome.State);
    }

    [Fact]
    public void A_Malformed_Delta_Is_Refused_Without_Asking_For_A_Snapshot()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;

        var (_, delta) = RuntimeDeltaTestFixture.CommitTick(
            writer, state, 1, RuntimeTestFixture.Instant.AddSeconds(1));

        var malformed = delta with { Revision = delta.Revision + 3 };
        var outcome = RuntimeDeltaApply.Apply(state, malformed);

        Assert.False(outcome.Applied);
        Assert.Equal(RuntimeRefusalCodes.RevisionNotNext, outcome.Code);
        Assert.False(outcome.ResnapshotRequired);
        Assert.Null(outcome.State);
    }

    [Fact]
    public void A_Non_Later_Delta_Instant_Is_Refused()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;

        var (_, delta) = RuntimeDeltaTestFixture.CommitTick(
            writer, state, 1, RuntimeTestFixture.Instant.AddSeconds(1));

        var stale = delta with { GeneratedAtUtc = state.GeneratedAtUtc };
        var outcome = RuntimeDeltaApply.Apply(state, stale);

        Assert.False(outcome.Applied);
        Assert.Equal(RuntimeRefusalCodes.EvolutionTickTime, outcome.Code);
        Assert.Null(outcome.State);
    }

    [Fact]
    public void Inconsistent_Delta_Content_Is_Refused_And_Never_Normalized()
    {
        var store = RuntimeTestFixture.CreateStore(SyntheticSeed.DefaultSeed);
        var writer = store.CreateWriter();
        var state = store.Current;

        var (_, delta) = RuntimeDeltaTestFixture.CommitTick(
            writer, state, 1, RuntimeTestFixture.Instant.AddSeconds(1));

        var wrongWalls = delta with
        {
            ChangedWalls = new[]
            {
                new WallSummary
                {
                    Wall = Wall.LEFT,
                    Total = 1,
                    Dirty = 0,
                    Cleaner = 1,
                    NotClassified = 0,
                    Uncertain = 0,
                    MaxScore = null,
                },
            },
        };
        var wallOutcome = RuntimeDeltaApply.Apply(state, wrongWalls);
        Assert.False(wallOutcome.Applied);
        Assert.Equal(RuntimeRefusalCodes.WallSummaryMismatch, wallOutcome.Code);
        Assert.Null(wallOutcome.State);

        var rewrittenIdentity = delta with
        {
            ChangedSensors = delta.ChangedSensors
                .Select(sensor => sensor with { ScanOrder = sensor.ScanOrder + 1 })
                .ToArray(),
        };
        var identityOutcome = RuntimeDeltaApply.Apply(state, rewrittenIdentity);
        Assert.False(identityOutcome.Applied);
        Assert.Equal(RuntimeRefusalCodes.SensorIdentity, identityOutcome.Code);
        Assert.Null(identityOutcome.State);

        var wrongPrevious = delta with
        {
            PreviousSensors = delta.PreviousSensors
                .Select(sensor => sensor with { SourceTimestamp = "1999-01-01T00:00:00.000Z" })
                .ToArray(),
        };
        var previousOutcome = RuntimeDeltaApply.Apply(state, wrongPrevious);
        Assert.False(previousOutcome.Applied);
        Assert.Equal(RuntimeRefusalCodes.StateInvalid, previousOutcome.Code);
        Assert.Null(previousOutcome.State);

        var duplicated = delta with
        {
            ChangedSensors = delta.ChangedSensors.Concat(new[] { delta.ChangedSensors[0] }).ToArray(),
            PreviousSensors = delta.PreviousSensors.Concat(new[] { delta.PreviousSensors[0] }).ToArray(),
        };
        var duplicateOutcome = RuntimeDeltaApply.Apply(state, duplicated);
        Assert.False(duplicateOutcome.Applied);
        Assert.Equal(RuntimeRefusalCodes.StateInvalid, duplicateOutcome.Code);
        Assert.Null(duplicateOutcome.State);

        // Nothing above touched the consumer revision.
        Assert.Equal(1, state.Revision);
        Assert.Equal(1, store.CurrentRevision);
    }

    [Fact]
    public void ActiveJob_Applies_The_Three_State_Encoding()
    {
        var basis = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var target = basis.Sensors[0];
        var activated = target with { IsActiveJobTarget = true, QueueState = QueueState.ACTIVE };
        var job = RuntimeDeltaTestFixture.SyntheticJob(
            target.SensorId, UtcTimestamps.Format(RuntimeTestFixture.Instant));

        // Object: the Active Job replaces the consumer's (here: absent) job.
        var activate = Delta(
            previousRevision: basis.Revision,
            revision: basis.Revision + 1,
            instant: RuntimeTestFixture.Instant.AddSeconds(1),
            changed: new[] { activated },
            previous: new[] { target },
            walls: RuntimeWallSummaries.Recalculate(Replace(basis, activated)),
            activeJob: DeltaJobState.Replaced(job));

        var activatedOutcome = RuntimeDeltaApply.Apply(basis, activate);
        Assert.True(activatedOutcome.Applied, activatedOutcome.Reason);

        var withJob = activatedOutcome.State!;
        Assert.Equal(job, withJob.ActiveJob);
        Assert.Equal(target.SensorId, withJob.Sensors.Single(sensor => sensor.IsActiveJobTarget).SensorId);
        Assert.Equal(basis.Sensors[1], withJob.Sensors[1]);

        // Absent: the Active Job is unchanged, not cleared.
        var touched = activated with { DirtyScore = 12.5 };
        var absent = Delta(
            previousRevision: withJob.Revision,
            revision: withJob.Revision + 1,
            instant: RuntimeTestFixture.Instant.AddSeconds(2),
            changed: new[] { touched },
            previous: new[] { activated },
            walls: RuntimeWallSummaries.Recalculate(Replace(withJob, touched)),
            activeJob: DeltaJobState.Unchanged());

        var absentOutcome = RuntimeDeltaApply.Apply(withJob, absent);
        Assert.True(absentOutcome.Applied, absentOutcome.Reason);

        var stillActive = absentOutcome.State!;
        Assert.Equal(job, stillActive.ActiveJob);
        Assert.Equal(12.5, stillActive.Sensors[0].DirtyScore);

        // Explicit null: the Active Job is cleared, and the target marker with it.
        var released = touched with { IsActiveJobTarget = false, QueueState = QueueState.NONE };
        var clear = Delta(
            previousRevision: stillActive.Revision,
            revision: stillActive.Revision + 1,
            instant: RuntimeTestFixture.Instant.AddSeconds(3),
            changed: new[] { released },
            previous: new[] { touched },
            walls: RuntimeWallSummaries.Recalculate(Replace(stillActive, released)),
            activeJob: DeltaJobState.Cleared());

        var clearOutcome = RuntimeDeltaApply.Apply(stillActive, clear);
        Assert.True(clearOutcome.Applied, clearOutcome.Reason);
        Assert.Null(clearOutcome.State!.ActiveJob);
        Assert.DoesNotContain(clearOutcome.State.Sensors, sensor => sensor.IsActiveJobTarget);
    }

    private static SensorPresentationState[] Replace(RuntimeState state, SensorPresentationState replacement) =>
        state.Sensors
            .Select(sensor => sensor.SensorId == replacement.SensorId ? replacement : sensor)
            .ToArray();

    private static RuntimeDelta Delta(
        int previousRevision,
        int revision,
        DateTimeOffset instant,
        IReadOnlyList<SensorPresentationState> changed,
        IReadOnlyList<SensorPresentationState> previous,
        IReadOnlyList<WallSummary> walls,
        DeltaJobState activeJob) => new()
        {
            PreviousRevision = previousRevision,
            Revision = revision,
            GeneratedAtUtc = instant,
            ChangedSensors = changed,
            PreviousSensors = previous,
            ChangedWalls = walls,
            ActiveJob = activeJob,
        };
}
