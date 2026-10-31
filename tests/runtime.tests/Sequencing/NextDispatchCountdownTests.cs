using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests.Sequencing;

/// <summary>
/// The H3-A frozen next-dispatch countdown: creation, monotonic advance, the single dispatch,
/// supersession with a full-delay restart, cancellation, fault and the ordinary-mutation guard.
/// Every transition takes an explicit instant; nothing here reads a clock.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local Release build and the Full .NET
/// matrix are the validation of record).
/// </summary>
public sealed class NextDispatchCountdownTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 8, 0, 0, 18, TimeSpan.Zero);

    /// <summary>One frozen decision with the required-argument set; MANUAL_FIXED and LOWER by default.</summary>
    private static NextDispatchDelayDecision Decision(
        int generation = 1,
        int appliedDelaySeconds = 5,
        string entryId = "SYN-QE-2",
        string sensorId = "G+202",
        DateTimeOffset? createdAt = null) =>
        new()
        {
            DecisionId = $"SYN-TD-{generation}",
            CountdownGeneration = generation,
            QueueGeneration = 7,
            QueueEntryId = entryId,
            NextSensorId = sensorId,
            NextWall = Wall.LEFT,
            NextSensorOrderWall = 12,
            CanonicalWaterJetId = "WJ1",
            EffectiveWaterJetId = "WJ1",
            EffectiveWaterJetProvenance = TimeDelayPlanner.CanonicalAssignmentProvenance,
            IsolationValveId = "IV1",
            CabinetId = "WSB1",
            ControllerId = "MC-WSB1",
            HorizontalAxisId = "A",
            VerticalAxisId = "B",
            BoilerCondition = BoilerCondition.VERY_CLEAN,
            Mode = TimeDelayMode.MANUAL_FIXED,
            ConfigurationVersion = TimeDelayConfiguration.LegacySeededSimulationVersion,
            ManualDelaySeconds = appliedDelaySeconds,
            RhTempMin = TimeDelayConfiguration.LegacySeededRhTempMin,
            RhTempMax = TimeDelayConfiguration.LegacySeededRhTempMax,
            RhInputWindowSeconds = TimeDelayConfiguration.LegacySeededRhInputWindowSeconds,
            AutoDelayMinSeconds = 600,
            AutoDelayMaxSeconds = 660,
            BaseDelaySeconds = appliedDelaySeconds,
            Zone = DelayZone.LOWER,
            ZoneFactor = TimeDelayPlanner.LowerZoneFactor,
            AppliedDelaySeconds = appliedDelaySeconds,
            CreatedAt = UtcTimestamps.Format(createdAt ?? At),
        };

    private static NextDispatchCountdown Active(
        int generation = 1,
        int appliedDelaySeconds = 5,
        DateTimeOffset? createdAt = null)
    {
        var countdown = NextDispatchCountdownCoordinator.Begin(
            NextDispatchCountdown.None,
            Decision(generation, appliedDelaySeconds, createdAt: createdAt),
            out var refusal);
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        return countdown;
    }

    [Fact]
    public void No_countdown_exists_before_the_first_decision()
    {
        var none = NextDispatchCountdown.None;

        Assert.Equal(CountdownLifecycleState.NONE, none.Lifecycle);
        Assert.Equal(0, none.Generation);
        Assert.Null(none.Decision);
        Assert.False(none.Frozen);
        Assert.False(none.DispatchAdmissible);
        Assert.Equal(1, NextDispatchCountdownCoordinator.NextGeneration(none));
    }

    [Fact]
    public void The_first_decision_freezes_the_full_applied_delay()
    {
        var countdown = Active();

        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, countdown.Lifecycle);
        Assert.Equal(1, countdown.Generation);
        Assert.Equal(5, countdown.RemainingSeconds);
        Assert.True(countdown.Frozen);
        Assert.False(countdown.DispatchAdmissible);
        Assert.Equal(At, countdown.CreatedAtUtc);
        Assert.Equal("SYN-TD-1", countdown.Decision!.DecisionId);
    }

    [Fact]
    public void A_missing_decision_is_refused_as_an_empty_queue()
    {
        var countdown = NextDispatchCountdownCoordinator.Begin(
            NextDispatchCountdown.None, null, out var refusal);

        Assert.Equal(CountdownRefusalCode.QUEUE_EMPTY, refusal);
        Assert.Equal(CountdownLifecycleState.NONE, countdown.Lifecycle);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void A_repeated_skipped_or_negative_generation_is_refused(int generation)
    {
        var current = NextDispatchCountdown.None;
        var countdown = NextDispatchCountdownCoordinator.Begin(
            current, Decision(generation), out var refusal);

        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID, refusal);
        Assert.Same(current, countdown);
    }

    [Fact]
    public void A_second_decision_while_one_is_frozen_is_refused()
    {
        var active = Active();
        var countdown = NextDispatchCountdownCoordinator.Begin(active, Decision(2), out var refusal);

        Assert.Equal(CountdownRefusalCode.COUNTDOWN_ALREADY_ACTIVE, refusal);
        Assert.Same(active, countdown);
    }

    [Fact]
    public void A_negative_applied_delay_is_refused()
    {
        var countdown = NextDispatchCountdownCoordinator.Begin(
            NextDispatchCountdown.None, Decision(1, -1), out var refusal);

        Assert.Equal(CountdownRefusalCode.APPLIED_DELAY_INVALID, refusal);
        Assert.Equal(CountdownLifecycleState.NONE, countdown.Lifecycle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-10-08T00:00:00Z")]
    [InlineData("not a timestamp")]
    public void A_decision_with_a_malformed_creation_timestamp_is_refused(string createdAt)
    {
        var decision = Decision() with { CreatedAt = createdAt };

        var countdown = NextDispatchCountdownCoordinator.Begin(
            NextDispatchCountdown.None, decision, out var refusal);

        Assert.Equal(CountdownRefusalCode.DECISION_TIMESTAMP_INVALID, refusal);
        Assert.Equal(CountdownLifecycleState.NONE, countdown.Lifecycle);
    }

    [Fact]
    public void The_remaining_seconds_follow_the_elapsed_whole_seconds()
    {
        var countdown = Active(appliedDelaySeconds: 5);

        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(1), out var first);
        Assert.Equal(CountdownRefusalCode.NONE, first);
        Assert.Equal(4, countdown.RemainingSeconds);
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, countdown.Lifecycle);

        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(2.999), out _);
        Assert.Equal(3, countdown.RemainingSeconds);

        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(4), out _);
        Assert.Equal(1, countdown.RemainingSeconds);
        Assert.False(countdown.DispatchAdmissible);
    }

    [Fact]
    public void The_remaining_seconds_never_increase_and_time_never_runs_backwards()
    {
        var countdown = Active(appliedDelaySeconds: 10);
        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(6), out _);
        Assert.Equal(4, countdown.RemainingSeconds);

        // An earlier instant is refused instead of being reinterpreted as a longer countdown.
        var regressed = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(5), out var refusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, refusal);
        Assert.Same(countdown, regressed);

        // A repeated instant keeps the remaining seconds where they are.
        var repeated = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(6), out _);
        Assert.Equal(4, repeated.RemainingSeconds);
    }

    [Fact]
    public void Reaching_zero_completes_the_countdown_exactly_once()
    {
        var countdown = Active(appliedDelaySeconds: 5);

        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(5), out _);
        Assert.Equal(CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH, countdown.Lifecycle);
        Assert.Equal(0, countdown.RemainingSeconds);
        Assert.True(countdown.Frozen);
        Assert.True(countdown.DispatchAdmissible);
        Assert.Equal(At.AddSeconds(5), countdown.CompletedAtUtc);

        // Advancing a completed countdown is refused; it never re-enters ACTIVE_FROZEN.
        var again = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(6), out var refusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_ACTIVE, refusal);
        Assert.Same(countdown, again);

        // A late observation also cannot push the remaining seconds below zero.
        var late = Active(appliedDelaySeconds: 5);
        late = NextDispatchCountdownCoordinator.Advance(late, At.AddSeconds(60), out _);
        Assert.Equal(0, late.RemainingSeconds);
        Assert.Equal(CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH, late.Lifecycle);
    }

    [Fact]
    public void Dispatch_is_admissible_exactly_once()
    {
        var countdown = Active(appliedDelaySeconds: 1);

        var early = NextDispatchCountdownCoordinator.MarkDispatched(countdown, At, out var earlyRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_COMPLETE, earlyRefusal);
        Assert.Same(countdown, early);

        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(1), out _);
        countdown = NextDispatchCountdownCoordinator.MarkDispatched(countdown, At.AddSeconds(1), out var dispatched);
        Assert.Equal(CountdownRefusalCode.NONE, dispatched);
        Assert.Equal(CountdownLifecycleState.DISPATCHED, countdown.Lifecycle);
        Assert.Equal(At.AddSeconds(1), countdown.DispatchedAtUtc);
        Assert.False(countdown.Frozen);
        Assert.False(countdown.DispatchAdmissible);

        var second = NextDispatchCountdownCoordinator.MarkDispatched(countdown, At.AddSeconds(2), out var secondRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_ALREADY_DISPATCHED, secondRefusal);
        Assert.Same(countdown, second);
    }

    [Fact]
    public void Supersession_keeps_the_evidence_and_never_dispatches()
    {
        var countdown = Active(appliedDelaySeconds: 30);
        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(27), out _);
        Assert.Equal(3, countdown.RemainingSeconds);

        var superseded = NextDispatchCountdownCoordinator.Supersede(
            countdown, CountdownSupersessionReason.QUEUE_HEAD_IDENTITY_CHANGED, out var refusal);

        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(CountdownLifecycleState.SUPERSEDED, superseded.Lifecycle);
        Assert.Equal(CountdownSupersessionReason.QUEUE_HEAD_IDENTITY_CHANGED, superseded.SupersededReason);
        Assert.NotNull(superseded.Decision);          // retained as evidence
        Assert.False(superseded.Frozen);
        Assert.False(superseded.DispatchAdmissible);

        // A superseded countdown can never be dispatched.
        var attempt = NextDispatchCountdownCoordinator.MarkDispatched(
            superseded, At.AddSeconds(28), out var dispatchRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_COMPLETE, dispatchRefusal);
        Assert.Equal(CountdownLifecycleState.SUPERSEDED, attempt.Lifecycle);
    }

    [Fact]
    public void A_superseded_countdown_requires_a_reason()
    {
        var countdown = Active();

        var unchanged = NextDispatchCountdownCoordinator.Supersede(
            countdown, CountdownSupersessionReason.NONE, out var refusal);

        Assert.Equal(CountdownRefusalCode.SUPERSESSION_REASON_REQUIRED, refusal);
        Assert.Same(countdown, unchanged);
    }

    [Fact]
    public void Only_a_frozen_countdown_can_be_superseded_or_cancelled()
    {
        var superseded = NextDispatchCountdownCoordinator.Supersede(
            NextDispatchCountdown.None, CountdownSupersessionReason.CRITICAL_PUMP_EVENT, out var supersedeRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_ACTIVE, supersedeRefusal);
        Assert.Equal(CountdownLifecycleState.NONE, superseded.Lifecycle);

        var cancelled = NextDispatchCountdownCoordinator.Cancel(NextDispatchCountdown.None, out var cancelRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_ACTIVE, cancelRefusal);
        Assert.Equal(CountdownLifecycleState.NONE, cancelled.Lifecycle);
    }

    [Fact]
    public void A_restart_uses_a_new_generation_and_the_full_delay_again()
    {
        var superseded = NextDispatchCountdownCoordinator.Supersede(
            NextDispatchCountdownCoordinator.Advance(Active(appliedDelaySeconds: 30), At.AddSeconds(27), out _),
            CountdownSupersessionReason.CRITICAL_PUMP_EVENT,
            out _);
        Assert.Equal(1, superseded.Generation);
        Assert.Equal(3, superseded.RemainingSeconds);

        // A decision of the SAME generation can never be reused: the stale countdown is not resumed.
        var stale = NextDispatchCountdownCoordinator.Begin(superseded, Decision(1), out var staleRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID, staleRefusal);
        Assert.Equal(CountdownLifecycleState.SUPERSEDED, stale.Lifecycle);

        var restarted = NextDispatchCountdownCoordinator.Begin(superseded, Decision(2, 30), out var restartRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, restartRefusal);
        Assert.Equal(2, restarted.Generation);
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, restarted.Lifecycle);
        Assert.Equal(30, restarted.RemainingSeconds);   // the FULL applied delay, not the 3 s that were left
        Assert.Equal(CountdownSupersessionReason.CRITICAL_PUMP_EVENT, restarted.LastSupersededReason);
    }

    [Fact]
    public void A_cancelled_countdown_never_dispatches_and_may_start_again()
    {
        var cancelled = NextDispatchCountdownCoordinator.Cancel(Active(appliedDelaySeconds: 5), out var refusal);
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(CountdownLifecycleState.CANCELLED, cancelled.Lifecycle);
        Assert.False(cancelled.Frozen);

        var dispatch = NextDispatchCountdownCoordinator.MarkDispatched(cancelled, At.AddSeconds(5), out var dispatchRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_COMPLETE, dispatchRefusal);
        Assert.Equal(CountdownLifecycleState.CANCELLED, dispatch.Lifecycle);

        var restarted = NextDispatchCountdownCoordinator.Begin(cancelled, Decision(2, 5), out _);
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, restarted.Lifecycle);
        Assert.Equal(5, restarted.RemainingSeconds);
    }

    [Fact]
    public void A_faulted_countdown_fails_closed()
    {
        var faulted = NextDispatchCountdownCoordinator.Fault(Active(), CountdownRefusalCode.RH_STALE);

        Assert.Equal(CountdownLifecycleState.FAULTED, faulted.Lifecycle);
        Assert.Equal(CountdownRefusalCode.RH_STALE, faulted.RefusalCode);
        Assert.False(faulted.Frozen);
        Assert.False(faulted.DispatchAdmissible);
        var attempt = NextDispatchCountdownCoordinator.MarkDispatched(
            faulted, At.AddSeconds(5), out var refusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_COMPLETE, refusal);
        Assert.Equal(CountdownLifecycleState.FAULTED, attempt.Lifecycle);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NextDispatchCountdownCoordinator.Fault(Active(), CountdownRefusalCode.NONE));
    }

    [Fact]
    public void Ordinary_mutation_is_refused_only_while_a_countdown_is_frozen()
    {
        Assert.False(NextDispatchCountdownCoordinator.RefusesMutation(NextDispatchCountdown.None, out _));

        var active = Active();
        Assert.True(NextDispatchCountdownCoordinator.RefusesMutation(active, out var refusal));
        Assert.Equal(CountdownRefusalCode.MUTATION_REFUSED_DURING_COUNTDOWN, refusal);

        var completed = NextDispatchCountdownCoordinator.Advance(active, At.AddSeconds(5), out _);
        Assert.True(NextDispatchCountdownCoordinator.RefusesMutation(completed, out _));

        var dispatched = NextDispatchCountdownCoordinator.MarkDispatched(completed, At.AddSeconds(5), out _);
        Assert.False(NextDispatchCountdownCoordinator.RefusesMutation(dispatched, out _));
    }

    [Fact]
    public void A_premise_break_is_detected_only_for_the_frozen_head()
    {
        var active = Active();

        Assert.False(NextDispatchCountdownCoordinator.TryDetectPremiseBreak(
            active, "SYN-QE-2", "G+202", true, false, true, out var noBreak));
        Assert.Equal(CountdownSupersessionReason.NONE, noBreak);

        Assert.True(NextDispatchCountdownCoordinator.TryDetectPremiseBreak(
            active, null, null, false, false, true, out var withdrawn));
        Assert.Equal(CountdownSupersessionReason.QUEUE_HEAD_WITHDRAWN, withdrawn);

        Assert.True(NextDispatchCountdownCoordinator.TryDetectPremiseBreak(
            active, "SYN-QE-9", "G+202", true, false, true, out var changedEntry));
        Assert.Equal(CountdownSupersessionReason.QUEUE_HEAD_IDENTITY_CHANGED, changedEntry);

        Assert.True(NextDispatchCountdownCoordinator.TryDetectPremiseBreak(
            active, "SYN-QE-2", "G+999", true, false, true, out var changedSensor));
        Assert.Equal(CountdownSupersessionReason.QUEUE_HEAD_IDENTITY_CHANGED, changedSensor);

        Assert.True(NextDispatchCountdownCoordinator.TryDetectPremiseBreak(
            active, "SYN-QE-2", "G+202", true, true, true, out var disabled));
        Assert.Equal(CountdownSupersessionReason.HEAD_SENSOR_DISABLED, disabled);

        Assert.True(NextDispatchCountdownCoordinator.TryDetectPremiseBreak(
            active, "SYN-QE-2", "G+202", true, false, false, out var structural));
        Assert.Equal(CountdownSupersessionReason.HEAD_STRUCTURALLY_INVALID, structural);

        // A countdown that is no longer frozen has no premise to break.
        Assert.False(NextDispatchCountdownCoordinator.TryDetectPremiseBreak(
            NextDispatchCountdown.None, null, null, false, false, false, out var none));
        Assert.Equal(CountdownSupersessionReason.NONE, none);
    }
}
