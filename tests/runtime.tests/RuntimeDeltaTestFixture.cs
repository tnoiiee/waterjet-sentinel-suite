using Wjss.Adapters.Simulator;
using Wjss.Contracts;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Scenario helpers for the Delta tests: the accepted chain of a store, and one
/// synthetic Active Cleaning Job used only to exercise the three-state Active Job
/// encoding. Every value is synthetic; no device, threshold or Production value
/// appears, and nothing here commands or dispatches anything.
/// </summary>
internal static class RuntimeDeltaTestFixture
{
    /// <summary>The deterministic test seed (the adapter's default seed identity).</summary>
    internal static ulong Seed => SyntheticSeed.DefaultSeed.Value;

    /// <summary>
    /// One synthetic Active Cleaning Job bound to a Sensor. Used as encoded test
    /// data only: it is never dispatched, never queued and never actuated.
    /// </summary>
    internal static ActiveCleaningJobState SyntheticJob(string targetSensorId, string at) => new()
    {
        JobId = "J-SYN-01",
        TargetSensorId = targetSensorId,
        JetId = "SYN-JET-01",
        ValveId = "SYN-VLV-01",
        Phase = JobPhase.P1,
        PhaseLabel = "PHASE-1 (synthetic test value)",
        PhaseIndex = 1,
        StartedAt = at,
        PhaseStartedAt = at,
        PhaseProgress = 0.0,
        Lifecycle = JobLifecycle.RUNNING,
        CleaningPhase = "SYNTHETIC",
        Dispatch = new DispatchRecord
        {
            DispatchId = "D-SYN-01",
            QueueRevisionBefore = 0,
            QueueRevisionAfter = 1,
            QueueEntryId = "Q-SYN-01",
            PositionBefore = 1,
            SensorId = targetSensorId,
            SourceReason = "SYNTHETIC",
            JobId = "J-SYN-01",
            Origin = "SYNTHETIC_TEST",
            DispatchedAt = at,
        },
        SafeReturn = null,
    };

    /// <summary>
    /// Evolves one accepted tick, projects its Delta and commits the candidate
    /// through the store's single writer, returning the committed revision and the
    /// Delta that describes the transition.
    /// </summary>
    internal static (RuntimeState State, RuntimeDelta Delta) CommitTick(
        RuntimeStateWriter writer,
        RuntimeState previous,
        long tick,
        DateTimeOffset instant,
        SyntheticEvolutionRules? rules = null)
    {
        var outcome = RuntimeSyntheticEvolution.Tick(
            previous, Seed, tick, instant, rules ?? RuntimeSyntheticEvolution.DefaultRules);

        if (!outcome.Accepted)
        {
            throw new InvalidOperationException(
                $"[TEST SETUP] The synthetic tick was refused: {outcome.RefusalCode} {outcome.RefusalReason}");
        }

        var delta = RuntimeDeltaProjector.Project(previous, outcome.Result!);
        var committed = writer.Commit(outcome.Result!.State);
        return (committed, delta);
    }
}
