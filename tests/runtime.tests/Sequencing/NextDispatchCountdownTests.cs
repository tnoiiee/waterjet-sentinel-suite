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

    /// <summary>
    /// A countdown that already carries an arbitrary generation. It is only ever used to place the
    /// state machine at a generation boundary that a real orchestration would take a very long time to
    /// reach; it is never dispatched and never observed as Product state.
    /// </summary>
    private static NextDispatchCountdown AtGeneration(int generation, CountdownLifecycleState lifecycle) =>
        NextDispatchCountdown.None with { Generation = generation, Lifecycle = lifecycle };

    [Fact]
    public void No_countdown_exists_before_the_first_decision()
    {
        var none = NextDispatchCountdown.None;

        Assert.Equal(CountdownLifecycleState.NONE, none.Lifecycle);
        Assert.Equal(0, none.Generation);
        Assert.Null(none.Decision);
        Assert.Null(none.CreatedAtUtc);
        Assert.Null(none.LastObservedAtUtc);
        Assert.Null(none.CompletedAtUtc);
        Assert.Null(none.DispatchedAtUtc);
        Assert.False(none.Frozen);
        Assert.False(none.DispatchAdmissible);
        Assert.True(NextDispatchCountdownCoordinator.TryNextGeneration(none, out var nextGeneration, out var refusal));
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(1, nextGeneration);
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
        // The observation baseline starts at the frozen decision instant, never at "now".
        Assert.Equal(At, countdown.LastObservedAtUtc);
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
    public void Generation_never_wraps_and_never_repeats_at_the_integer_boundary()
    {
        // The successor sequence is exact everywhere it exists.
        foreach (var (current, expected) in new[]
        {
            (0, 1),
            (1, 2),
            (int.MaxValue - 1, int.MaxValue),
        })
        {
            var countdown = AtGeneration(current, CountdownLifecycleState.SUPERSEDED);

            Assert.True(NextDispatchCountdownCoordinator.TryNextGeneration(
                countdown, out var nextGeneration, out var refusal));
            Assert.Equal(CountdownRefusalCode.NONE, refusal);
            Assert.Equal(expected, nextGeneration);
        }
    }

    [Fact]
    public void The_successor_of_int_MaxValue_does_not_exist_and_is_refused()
    {
        var exhausted = AtGeneration(int.MaxValue, CountdownLifecycleState.SUPERSEDED);

        var accepted = NextDispatchCountdownCoordinator.TryNextGeneration(
            exhausted, out var nextGeneration, out var refusal);

        Assert.False(accepted);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_EXHAUSTED, refusal);
        // No wrap to int.MinValue, no saturation at int.MaxValue, no negative generation.
        Assert.Equal(0, nextGeneration);
        Assert.NotEqual(int.MinValue, nextGeneration);
        Assert.NotEqual(int.MaxValue, nextGeneration);
        Assert.True(nextGeneration >= 0);
    }

    [Fact]
    public void A_negative_current_generation_is_refused_as_invalid_rather_than_incremented()
    {
        var negative = AtGeneration(-1, CountdownLifecycleState.SUPERSEDED);

        var accepted = NextDispatchCountdownCoordinator.TryNextGeneration(negative, out var nextGeneration, out var refusal);

        Assert.False(accepted);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID, refusal);
        Assert.Equal(0, nextGeneration);
    }

    [Fact]
    public void Generation_exhaustion_refuses_the_decision_without_touching_the_frozen_state()
    {
        // A superseded generation at the last representable value: the next decision cannot exist.
        var completed = NextDispatchCountdownCoordinator.Advance(
            NextDispatchCountdown.None with
            {
                Lifecycle = CountdownLifecycleState.ACTIVE_FROZEN,
                Generation = int.MaxValue,
                RemainingSeconds = 3,
                Decision = Decision(int.MaxValue, 3),
                CreatedAtUtc = At,
                LastObservedAtUtc = At,
            },
            At.AddSeconds(1),
            out var advanceRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, advanceRefusal);
        Assert.Equal(2, completed.RemainingSeconds);

        var superseded = NextDispatchCountdownCoordinator.Supersede(
            completed, CountdownSupersessionReason.CRITICAL_PUMP_EVENT, out _);
        Assert.Equal(CountdownLifecycleState.SUPERSEDED, superseded.Lifecycle);
        var remainingBefore = superseded.RemainingSeconds;

        var refused = NextDispatchCountdownCoordinator.Begin(
            superseded, Decision(int.MaxValue, 30), out var refusal);

        // Exhaustion is reported by the SAME authority the Runtime uses, and the decision is never adopted.
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_EXHAUSTED, refusal);
        Assert.Same(superseded, refused);
        Assert.Equal(CountdownLifecycleState.SUPERSEDED, refused.Lifecycle);
        Assert.Equal(remainingBefore, refused.RemainingSeconds);
        Assert.Equal(int.MaxValue, refused.Generation);
        Assert.False(refused.DispatchAdmissible);
        Assert.False(refused.Frozen);
        // No partial frozen decision was created: the retained decision is still the OLD one.
        Assert.Equal(int.MaxValue, refused.Decision!.CountdownGeneration);
        Assert.NotEqual(30, refused.Decision!.AppliedDelaySeconds);
    }

    [Fact]
    public void An_exhausted_generation_faults_the_countdown_instead_of_reaching_the_planner()
    {
        // The Runtime never plans with a fabricated successor: it asks the ONE authority first and, on
        // refusal, faults through the existing countdown fault path. This mirrors that composition, so
        // no parallel error channel and no wrapped generation can reach TimeDelayPlanner.
        var active = Active(appliedDelaySeconds: 5) with
        {
            Generation = int.MaxValue,
            Decision = Decision(int.MaxValue, 5),
        };

        var planned = NextDispatchCountdownCoordinator.TryNextGeneration(active, out var generation, out var refusal);
        var observed = planned ? active : NextDispatchCountdownCoordinator.Fault(active, refusal);

        Assert.False(planned);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_EXHAUSTED, refusal);
        Assert.Equal(CountdownLifecycleState.FAULTED, observed.Lifecycle);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_GENERATION_EXHAUSTED, observed.RefusalCode);
        Assert.Equal(5, observed.RemainingSeconds);                       // evidence untouched
        Assert.Equal(At, observed.LastObservedAtUtc);
        Assert.Equal(int.MaxValue, observed.Generation);
        Assert.False(observed.DispatchAdmissible);
        // Nothing that could be handed to the planner exists: no successor was produced at all.
        Assert.Equal(0, generation);
    }

    [Fact]
    public void A_superseded_generation_still_restarts_at_exactly_plus_one_when_representable()
    {
        var previous = AtGeneration(int.MaxValue - 1, CountdownLifecycleState.SUPERSEDED);

        Assert.True(NextDispatchCountdownCoordinator.TryNextGeneration(
            previous, out var nextGeneration, out var refusal));
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(int.MaxValue, nextGeneration);

        var decision = Decision(nextGeneration, 30);
        var begun = NextDispatchCountdownCoordinator.Begin(previous, decision, out var beginRefusal);

        Assert.Equal(CountdownRefusalCode.NONE, beginRefusal);
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, begun.Lifecycle);
        Assert.Equal(int.MaxValue, begun.Generation);
        Assert.Equal(30, begun.RemainingSeconds);
        Assert.Equal(At, begun.CreatedAtUtc);
        Assert.Equal(At, begun.LastObservedAtUtc);
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
        Assert.Equal(At.AddSeconds(6), countdown.LastObservedAtUtc);

        // An earlier instant is refused instead of being reinterpreted as a longer countdown.
        var regressed = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(5), out var refusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, refusal);
        Assert.Same(countdown, regressed);

        // The refusal changed no accepted evidence at all.
        Assert.Equal(At.AddSeconds(6), regressed.LastObservedAtUtc);
        Assert.Equal(4, regressed.RemainingSeconds);
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, regressed.Lifecycle);

        // An accepted later instant moves the baseline with it: the countdown is now proof against
        // any instant before +7, not only against instants before the creation instant.
        var accepted = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(7), out var acceptedRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, acceptedRefusal);
        Assert.Equal(3, accepted.RemainingSeconds);
        Assert.Equal(At.AddSeconds(7), accepted.LastObservedAtUtc);

        var second = NextDispatchCountdownCoordinator.Advance(accepted, At.AddSeconds(6), out var secondRegression);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, secondRegression);
        Assert.Same(accepted, second);

        // A repeated instant keeps the remaining seconds where they are.
        var repeated = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(6), out var repeatedRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, repeatedRefusal);
        Assert.Equal(4, repeated.RemainingSeconds);
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, repeated.Lifecycle);
    }

    [Fact]
    public void Time_regression_is_detected_inside_one_whole_second_bucket()
    {
        // Both instants floor to the SAME elapsed whole second, so the remaining seconds cannot
        // reveal the reversal: only the accepted observation instant can.
        var countdown = Active(appliedDelaySeconds: 30);
        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(6.9), out _);
        Assert.Equal(24, countdown.RemainingSeconds);
        Assert.Equal(At.AddSeconds(6.9), countdown.LastObservedAtUtc);

        var regressed = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(6.1), out var refusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, refusal);
        Assert.Same(countdown, regressed);
        Assert.Equal(24, regressed.RemainingSeconds);

        // An instant later than the baseline inside the SAME bucket is accepted and records the later
        // instant, without moving the remaining seconds.
        var accepted = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(6.95), out var acceptedRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, acceptedRefusal);
        Assert.Equal(24, accepted.RemainingSeconds);
        Assert.Equal(At.AddSeconds(6.95), accepted.LastObservedAtUtc);
    }

    [Fact]
    public void A_zero_applied_delay_still_completes_at_its_own_creation_instant()
    {
        // The equal instant rule must not strand a zero delay in ACTIVE_FROZEN: the Runtime observes
        // the countdown at the very instant it was frozen.
        var countdown = Active(appliedDelaySeconds: 0);
        Assert.Equal(0, countdown.RemainingSeconds);
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, countdown.Lifecycle);

        var completed = NextDispatchCountdownCoordinator.Advance(countdown, At, out var refusal);

        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH, completed.Lifecycle);
        Assert.Equal(0, completed.RemainingSeconds);
        Assert.Equal(At, completed.CompletedAtUtc);
        Assert.Equal(At, completed.LastObservedAtUtc);

        // A zero delay dispatches at that same instant, and still exactly once.
        var dispatched = NextDispatchCountdownCoordinator.MarkDispatched(completed, At, out var dispatchRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, dispatchRefusal);
        Assert.Equal(CountdownLifecycleState.DISPATCHED, dispatched.Lifecycle);
        Assert.Equal(At, dispatched.CompletedAtUtc);
        Assert.Equal(At, dispatched.DispatchedAtUtc);
        Assert.Equal(At, dispatched.LastObservedAtUtc);
        var second = NextDispatchCountdownCoordinator.MarkDispatched(dispatched, At, out var secondRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_ALREADY_DISPATCHED, secondRefusal);
        Assert.Same(dispatched, second);
    }

    [Fact]
    public void Completion_records_the_observation_and_the_completion_instant_together()
    {
        var countdown = Active(appliedDelaySeconds: 5);

        countdown = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(4), out _);
        Assert.Equal(1, countdown.RemainingSeconds);
        Assert.Equal(At.AddSeconds(4), countdown.LastObservedAtUtc);
        Assert.Null(countdown.CompletedAtUtc);

        var completed = NextDispatchCountdownCoordinator.Advance(countdown, At.AddSeconds(5.25), out var refusal);
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH, completed.Lifecycle);
        // The completion is stamped with the OBSERVED instant itself, never with a truncated bucket.
        Assert.Equal(At.AddSeconds(5.25), completed.CompletedAtUtc);
        Assert.Equal(At.AddSeconds(5.25), completed.LastObservedAtUtc);
        Assert.True(completed.DispatchAdmissible);

        // The completed instance keeps its baseline; a later observation of a completed countdown is
        // refused as not active and changes no evidence.
        var later = NextDispatchCountdownCoordinator.Advance(completed, At.AddSeconds(6), out var notActive);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_ACTIVE, notActive);
        Assert.Same(completed, later);
        Assert.Equal(At.AddSeconds(5.25), later.LastObservedAtUtc);
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

    /// <summary>A completed countdown whose completion and observation instants both equal the given one.</summary>
    private static NextDispatchCountdown Completed(int appliedDelaySeconds = 5, int completedAtSeconds = 5)
    {
        var advanced = NextDispatchCountdownCoordinator.Advance(
            Active(appliedDelaySeconds: appliedDelaySeconds),
            At.AddSeconds(completedAtSeconds),
            out var refusal);
        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH, advanced.Lifecycle);
        return advanced;
    }

    [Fact]
    public void A_dispatch_at_the_completion_instant_is_accepted()
    {
        // The zero-delay and same-tick paths dispatch at exactly the instant the countdown completed.
        var completed = Completed(appliedDelaySeconds: 5, completedAtSeconds: 5);

        var dispatched = NextDispatchCountdownCoordinator.MarkDispatched(completed, At.AddSeconds(5), out var refusal);

        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(CountdownLifecycleState.DISPATCHED, dispatched.Lifecycle);
        Assert.Equal(At.AddSeconds(5), dispatched.DispatchedAtUtc);
        Assert.Equal(At.AddSeconds(5), dispatched.CompletedAtUtc);      // preserved
        Assert.Equal(At.AddSeconds(5), dispatched.LastObservedAtUtc);   // the dispatch IS the observation
        Assert.Equal(0, dispatched.RemainingSeconds);
        Assert.False(dispatched.Frozen);
        Assert.False(dispatched.DispatchAdmissible);
    }

    [Fact]
    public void A_dispatch_after_the_completion_instant_advances_the_observation_baseline()
    {
        var completed = Completed(appliedDelaySeconds: 5, completedAtSeconds: 5);

        var dispatched = NextDispatchCountdownCoordinator.MarkDispatched(completed, At.AddSeconds(7), out var refusal);

        Assert.Equal(CountdownRefusalCode.NONE, refusal);
        Assert.Equal(At.AddSeconds(7), dispatched.DispatchedAtUtc);
        Assert.Equal(At.AddSeconds(7), dispatched.LastObservedAtUtc);   // moved with the dispatch
        Assert.Equal(At.AddSeconds(5), dispatched.CompletedAtUtc);      // never rewritten
        // The accepted order holds on the dispatched record: the completion is never rewritten, the
        // dispatch instant is the final observation of the generation, and nothing precedes creation.
        Assert.True(dispatched.CreatedAtUtc <= dispatched.CompletedAtUtc);
        Assert.True(dispatched.CompletedAtUtc <= dispatched.DispatchedAtUtc);
        Assert.Equal(dispatched.DispatchedAtUtc, dispatched.LastObservedAtUtc);
    }

    [Fact]
    public void A_dispatch_before_the_completion_instant_is_refused_as_a_time_regression()
    {
        var completed = Completed(appliedDelaySeconds: 5, completedAtSeconds: 5);

        var refused = NextDispatchCountdownCoordinator.MarkDispatched(completed, At.AddSeconds(4), out var refusal);

        Assert.Equal(CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION, refusal);
        Assert.Same(completed, refused);
        Assert.Null(refused.DispatchedAtUtc);
        Assert.Equal(At.AddSeconds(5), refused.LastObservedAtUtc);
        Assert.Equal(At.AddSeconds(5), refused.CompletedAtUtc);
        Assert.Equal(CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH, refused.Lifecycle);
        Assert.True(refused.DispatchAdmissible);   // still exactly one dispatch remains admissible

        // Evidence is judged BEFORE the instant it is compared with: a constructed record whose last
        // observation runs ahead of its own completion is contradictory, so it fails closed as invalid
        // evidence even when the dispatch instant sits strictly between the two.
        var contradictory = completed with { LastObservedAtUtc = At.AddSeconds(9) };
        var invalid = NextDispatchCountdownCoordinator.MarkDispatched(
            contradictory, At.AddSeconds(6), out var invalidRefusal);
        Assert.Equal(CountdownRefusalCode.DECISION_TIMESTAMP_INVALID, invalidRefusal);
        Assert.Same(contradictory, invalid);
        Assert.Null(invalid.DispatchedAtUtc);
        Assert.Equal(At.AddSeconds(9), invalid.LastObservedAtUtc);
    }

    [Theory]
    [InlineData(0)]   // CompletedAtUtc missing
    [InlineData(1)]   // LastObservedAtUtc missing
    [InlineData(2)]   // CreatedAtUtc missing
    [InlineData(3)]   // RemainingSeconds not zero
    [InlineData(4)]   // observation AFTER completion: contradictory evidence
    [InlineData(5)]   // DispatchedAtUtc already present on a completed lifecycle
    public void A_completed_countdown_without_its_mandatory_evidence_fails_closed(int defect)
    {
        var completed = Completed(appliedDelaySeconds: 5, completedAtSeconds: 5);
        var malformed = defect switch
        {
            0 => completed with { CompletedAtUtc = null },
            1 => completed with { LastObservedAtUtc = null },
            2 => completed with { CreatedAtUtc = null },
            3 => completed with { RemainingSeconds = 1 },
            4 => completed with { LastObservedAtUtc = At.AddSeconds(9) },
            _ => completed with { DispatchedAtUtc = At.AddSeconds(6) },
        };

        var refused = NextDispatchCountdownCoordinator.MarkDispatched(malformed, At.AddSeconds(9), out var refusal);

        Assert.Equal(CountdownRefusalCode.DECISION_TIMESTAMP_INVALID, refusal);
        Assert.Same(malformed, refused);
        Assert.Null(refused.DispatchedAtUtc);
        Assert.Equal(CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH, refused.Lifecycle);
    }

    [Fact]
    public void A_superseded_cancelled_or_faulted_countdown_can_never_dispatch()
    {
        var completed = Completed();

        // Superseded, cancelled and faulted countdowns keep their evidence and never dispatch: the
        // lifecycle is judged before any timestamp, so no refusal can be mistaken for a dispatch.
        var superseded = NextDispatchCountdownCoordinator.Supersede(
            completed, CountdownSupersessionReason.QUEUE_HEAD_WITHDRAWN, out _);
        var supersededAttempt = NextDispatchCountdownCoordinator.MarkDispatched(
            superseded, At.AddSeconds(9), out var supersededRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_COMPLETE, supersededRefusal);
        Assert.Same(superseded, supersededAttempt);
        Assert.Null(supersededAttempt.DispatchedAtUtc);

        var cancelled = NextDispatchCountdownCoordinator.Cancel(completed, out var cancelRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, cancelRefusal);
        var cancelledAttempt = NextDispatchCountdownCoordinator.MarkDispatched(
            cancelled, At.AddSeconds(9), out var cancelledRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_COMPLETE, cancelledRefusal);
        Assert.Same(cancelled, cancelledAttempt);
        Assert.Null(cancelledAttempt.DispatchedAtUtc);

        var faulted = NextDispatchCountdownCoordinator.Fault(completed, CountdownRefusalCode.RH_STALE);
        var faultedAttempt = NextDispatchCountdownCoordinator.MarkDispatched(
            faulted, At.AddSeconds(9), out var faultedRefusal);
        Assert.Equal(CountdownRefusalCode.COUNTDOWN_NOT_COMPLETE, faultedRefusal);
        Assert.Same(faulted, faultedAttempt);
        Assert.Null(faultedAttempt.DispatchedAtUtc);
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
        Assert.Equal(At.AddSeconds(27), superseded.LastObservedAtUtc);   // observation evidence kept
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

        // The new generation starts its OWN observation baseline from its own decision instant: the
        // previous generation's accepted instant (+27) is never inherited.
        Assert.Equal(At, restarted.CreatedAtUtc);
        Assert.Equal(At, restarted.LastObservedAtUtc);
        Assert.NotEqual(superseded.LastObservedAtUtc, restarted.LastObservedAtUtc);

        // And an instant between the two generations is judged against the NEW baseline only: it is
        // later than the new decision instant, so it is accepted.
        var advanced = NextDispatchCountdownCoordinator.Advance(restarted, At.AddSeconds(1), out var advanceRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, advanceRefusal);
        Assert.Equal(29, advanced.RemainingSeconds);
        Assert.Equal(At.AddSeconds(1), advanced.LastObservedAtUtc);
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
    public void A_contradictory_frozen_state_is_refused_instead_of_repaired()
    {
        var active = Active(appliedDelaySeconds: 5);

        // A frozen countdown that already carries completion evidence contradicts its own lifecycle.
        var withCompletion = active with { CompletedAtUtc = At.AddSeconds(2) };
        var refusedCompletion = NextDispatchCountdownCoordinator.Advance(
            withCompletion, At.AddSeconds(3), out var completionRefusal);
        Assert.Equal(CountdownRefusalCode.DECISION_TIMESTAMP_INVALID, completionRefusal);
        Assert.Same(withCompletion, refusedCompletion);

        // A frozen countdown that already carries dispatch evidence contradicts its own lifecycle.
        var withDispatch = active with { DispatchedAtUtc = At.AddSeconds(2) };
        var refusedDispatch = NextDispatchCountdownCoordinator.Advance(
            withDispatch, At.AddSeconds(3), out var dispatchRefusal);
        Assert.Equal(CountdownRefusalCode.DECISION_TIMESTAMP_INVALID, dispatchRefusal);
        Assert.Same(withDispatch, refusedDispatch);

        // An observation baseline that precedes its own creation instant is impossible evidence.
        var inverted = active with { LastObservedAtUtc = At.AddSeconds(-1) };
        var refusedInverted = NextDispatchCountdownCoordinator.Advance(
            inverted, At.AddSeconds(3), out var invertedRefusal);
        Assert.Equal(CountdownRefusalCode.DECISION_TIMESTAMP_INVALID, invertedRefusal);
        Assert.Same(inverted, refusedInverted);

        // Fail closed means refused, not repaired: every piece of the contradictory evidence survives
        // untouched and no lifecycle is fabricated.
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, refusedCompletion.Lifecycle);
        Assert.Equal(At.AddSeconds(2), refusedCompletion.CompletedAtUtc);
        Assert.Equal(At.AddSeconds(2), refusedDispatch.DispatchedAtUtc);
        Assert.Equal(At.AddSeconds(-1), refusedInverted.LastObservedAtUtc);
        Assert.Equal(5, refusedCompletion.RemainingSeconds);
    }

    [Fact]
    public void Every_reachable_lifecycle_satisfies_the_countdown_invariant_matrix()
    {
        // ==== NONE: nothing exists yet.
        var none = NextDispatchCountdown.None;
        Assert.Equal(0, none.Generation);
        Assert.Equal(0, none.RemainingSeconds);
        Assert.Null(none.Decision);
        Assert.Null(none.CreatedAtUtc);
        Assert.Null(none.LastObservedAtUtc);
        Assert.Null(none.CompletedAtUtc);
        Assert.Null(none.DispatchedAtUtc);
        Assert.False(none.Frozen);
        Assert.False(none.DispatchAdmissible);

        // ==== ACTIVE_FROZEN: a frozen decision is counting down, and it has not completed.
        var active = Active(appliedDelaySeconds: 5);
        Assert.Equal(CountdownLifecycleState.ACTIVE_FROZEN, active.Lifecycle);
        Assert.True(active.Generation >= 1);
        Assert.NotNull(active.Decision);
        Assert.NotNull(active.CreatedAtUtc);
        Assert.NotNull(active.LastObservedAtUtc);
        Assert.True(active.LastObservedAtUtc >= active.CreatedAtUtc);
        Assert.Equal(active.Decision!.AppliedDelaySeconds, active.RemainingSeconds);
        Assert.True(active.RemainingSeconds >= 0);
        Assert.Null(active.CompletedAtUtc);
        Assert.Null(active.DispatchedAtUtc);
        Assert.True(active.Frozen);
        Assert.False(active.DispatchAdmissible);

        // ==== COMPLETED_AWAITING_DISPATCH: zero remaining, completion evidence present, once admissible.
        var completed = Completed(appliedDelaySeconds: 5, completedAtSeconds: 5);
        Assert.Equal(CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH, completed.Lifecycle);
        Assert.NotNull(completed.Decision);
        Assert.NotNull(completed.CreatedAtUtc);
        Assert.NotNull(completed.LastObservedAtUtc);
        Assert.NotNull(completed.CompletedAtUtc);
        Assert.True(completed.CreatedAtUtc <= completed.LastObservedAtUtc);
        Assert.Equal(completed.CompletedAtUtc, completed.LastObservedAtUtc);
        Assert.Equal(0, completed.RemainingSeconds);
        Assert.Null(completed.DispatchedAtUtc);
        Assert.True(completed.Frozen);
        Assert.True(completed.DispatchAdmissible);

        // ==== DISPATCHED: every earlier evidence retained, the dispatch instant last.
        var dispatched = NextDispatchCountdownCoordinator.MarkDispatched(
            completed, At.AddSeconds(6), out var dispatchRefusal);
        Assert.Equal(CountdownRefusalCode.NONE, dispatchRefusal);
        Assert.Equal(CountdownLifecycleState.DISPATCHED, dispatched.Lifecycle);
        Assert.NotNull(dispatched.Decision);
        Assert.Equal(completed.CompletedAtUtc, dispatched.CompletedAtUtc);
        Assert.NotNull(dispatched.DispatchedAtUtc);
        Assert.Equal(dispatched.DispatchedAtUtc, dispatched.LastObservedAtUtc);
        Assert.True(dispatched.DispatchedAtUtc >= dispatched.CompletedAtUtc);
        Assert.Equal(0, dispatched.RemainingSeconds);
        Assert.False(dispatched.Frozen);
        Assert.False(dispatched.DispatchAdmissible);

        // ==== SUPERSEDED / CANCELLED / FAULTED: evidence retained, never dispatchable.
        var superseded = NextDispatchCountdownCoordinator.Supersede(
            active, CountdownSupersessionReason.HEAD_SENSOR_DISABLED, out _);
        Assert.Equal(CountdownLifecycleState.SUPERSEDED, superseded.Lifecycle);
        Assert.NotNull(superseded.Decision);
        Assert.Equal(active.LastObservedAtUtc, superseded.LastObservedAtUtc);
        Assert.False(superseded.DispatchAdmissible);

        var cancelled = NextDispatchCountdownCoordinator.Cancel(active, out _);
        Assert.Equal(CountdownLifecycleState.CANCELLED, cancelled.Lifecycle);
        Assert.NotNull(cancelled.Decision);
        Assert.Equal(active.LastObservedAtUtc, cancelled.LastObservedAtUtc);
        Assert.False(cancelled.DispatchAdmissible);

        var faulted = NextDispatchCountdownCoordinator.Fault(active, CountdownRefusalCode.RH_STALE);
        Assert.Equal(CountdownLifecycleState.FAULTED, faulted.Lifecycle);
        Assert.NotNull(faulted.Decision);
        Assert.Equal(active.LastObservedAtUtc, faulted.LastObservedAtUtc);
        Assert.Equal(CountdownRefusalCode.RH_STALE, faulted.RefusalCode);
        Assert.False(faulted.DispatchAdmissible);

        // ==== PENDING_DECISION is reserved and never produced by any factory or transition, so it
        // owns no timestamp or generation behaviour to prove here.
        var reachable = new[]
        {
            none.Lifecycle, active.Lifecycle, completed.Lifecycle, dispatched.Lifecycle,
            superseded.Lifecycle, cancelled.Lifecycle, faulted.Lifecycle,
        };
        Assert.DoesNotContain(CountdownLifecycleState.PENDING_DECISION, reachable);
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
