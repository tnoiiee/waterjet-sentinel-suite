using Wjss.Contracts;
using Wjss.Time;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// The single next-dispatch countdown of one AutoSequence session. It is AutoSequence lifecycle
/// state, never a <see cref="QueueEntry"/> status: no GlobalQueue entry carries a counting, held or
/// dispatched state, and the queue stays ready-only.
///
/// <para>
/// The state is immutable. Every transition returns a new instance through
/// <see cref="NextDispatchCountdownCoordinator"/>; nothing mutates a frozen countdown in place.
/// The type carries no clock: every transition takes the observing instant explicitly.
/// </para>
/// </summary>
public sealed record NextDispatchCountdown
{
    /// <summary>No countdown exists for this session; nothing is frozen and nothing counts down.</summary>
    public static NextDispatchCountdown None { get; } = new()
    {
        Lifecycle = CountdownLifecycleState.NONE,
        Generation = 0,
        RemainingSeconds = 0,
    };

    public required CountdownLifecycleState Lifecycle { get; init; }

    /// <summary>The countdown generation. Every new decision increments it by exactly one.</summary>
    public required int Generation { get; init; }

    /// <summary>Remaining whole seconds. Monotonic non-increasing while the countdown is frozen.</summary>
    public required int RemainingSeconds { get; init; }

    /// <summary>The frozen decision, or null when no decision was created.</summary>
    public NextDispatchDelayDecision? Decision { get; init; }

    public CountdownSupersessionReason? SupersededReason { get; init; }

    /// <summary>The reason the previous generation was superseded, kept across a restart.</summary>
    public CountdownSupersessionReason? LastSupersededReason { get; init; }

    public CountdownRefusalCode? RefusalCode { get; init; }

    /// <summary>The parsed decision instant, or null when no decision exists.</summary>
    public DateTimeOffset? CreatedAtUtc { get; init; }

    /// <summary>
    /// The latest observation instant this countdown has ACCEPTED, or null when no decision exists. It
    /// starts at <see cref="CreatedAtUtc"/> and only ever moves forward, so an observation earlier than
    /// it is refused as <see cref="CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION"/> instead of being
    /// reinterpreted. Comparing whole second buckets is not enough: two instants inside ONE bucket can
    /// still run backwards, so this evidence is the strict premise of every advance.
    /// </summary>
    public DateTimeOffset? LastObservedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? DispatchedAtUtc { get; init; }

    /// <summary>
    /// True only while the decision is immutable: ACTIVE_FROZEN or COMPLETED_AWAITING_DISPATCH.
    /// Every ordinary mutation is refused for as long as this is true.
    /// </summary>
    public bool Frozen => Lifecycle
        is CountdownLifecycleState.ACTIVE_FROZEN
        or CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH;

    /// <summary>True only when exactly one dispatch is admissible for the frozen decision.</summary>
    public bool DispatchAdmissible => Lifecycle == CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH;
}

/// <summary>
/// The pure next-dispatch countdown coordinator. It creates, advances, supersedes, cancels, faults
/// and dispatches the ONE countdown of a session, deterministically and without a clock.
///
/// <para>
/// Rules of the state machine: a decision is created once (generation 1) and every later decision
/// must use exactly generation + 1, which is the ONLY place a generation is derived; the remaining
/// seconds never increase; the observation instant never runs backwards, and an earlier instant is
/// refused rather than reinterpreted; a frozen countdown dispatches at most once, and only at or
/// after the instant it completed; a superseded or cancelled countdown never dispatches; and an
/// ordinary mutation is refused while a countdown is frozen.
/// </para>
/// </summary>
public static class NextDispatchCountdownCoordinator
{
    /// <summary>
    /// The ONE generation authority. It derives the generation a NEW decision must carry, or refuses
    /// with the typed reason when no further generation exists. Generation identity is monotonic and
    /// never wraps, saturates or restarts: the successor of <see cref="int.MaxValue"/> does not exist,
    /// so it is refused as <see cref="CountdownRefusalCode.COUNTDOWN_GENERATION_EXHAUSTED"/> rather
    /// than being computed. A negative current generation is refused as invalid, because no accepted
    /// countdown can carry one. Nothing is mutated on refusal.
    /// </summary>
    public static bool TryNextGeneration(
        NextDispatchCountdown current,
        out int nextGeneration,
        out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(current);
        nextGeneration = 0;
        refusal = CountdownRefusalCode.NONE;

        if (current.Generation < 0)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID;
            return false;
        }

        if (current.Generation == int.MaxValue)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_GENERATION_EXHAUSTED;
            return false;
        }

        // Every case that reaches this line is 0 .. int.MaxValue - 1, so the successor is exact and
        // can never overflow.
        nextGeneration = current.Generation + 1;
        return true;
    }

    /// <summary>
    /// Freezes one decision and starts its full applied delay. The decision must carry exactly the
    /// next generation; a repeated or skipped generation is refused, so a stale decision can never
    /// be resumed and a renewed delay can never be shortened.
    /// </summary>
    public static NextDispatchCountdown Begin(
        NextDispatchCountdown current,
        NextDispatchDelayDecision? decision,
        out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(current);
        refusal = CountdownRefusalCode.NONE;

        if (decision is null)
        {
            refusal = CountdownRefusalCode.QUEUE_EMPTY;
            return current;
        }

        if (current.Lifecycle is CountdownLifecycleState.ACTIVE_FROZEN
            or CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH
            or CountdownLifecycleState.PENDING_DECISION)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_ALREADY_ACTIVE;
            return current;
        }

        // One authority, one derivation: the decision must carry exactly the successor this countdown
        // publishes, and an exhausted or invalid generation is refused before the decision is adopted.
        if (!TryNextGeneration(current, out var nextGeneration, out var generationRefusal))
        {
            refusal = generationRefusal;
            return current;
        }

        if (decision.CountdownGeneration != nextGeneration)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_GENERATION_INVALID;
            return current;
        }

        if (decision.AppliedDelaySeconds < 0)
        {
            refusal = CountdownRefusalCode.APPLIED_DELAY_INVALID;
            return current;
        }

        if (!UtcTimestamps.TryParse(decision.CreatedAt, out var createdAtUtc))
        {
            refusal = CountdownRefusalCode.DECISION_TIMESTAMP_INVALID;
            return current;
        }

        return new NextDispatchCountdown
        {
            Lifecycle = CountdownLifecycleState.ACTIVE_FROZEN,
            Generation = decision.CountdownGeneration,
            RemainingSeconds = decision.AppliedDelaySeconds,
            Decision = decision,
            CreatedAtUtc = createdAtUtc,
            // A new generation starts its OWN observation baseline: the previous generation's
            // LastObservedAtUtc is never carried into the new active countdown.
            LastObservedAtUtc = createdAtUtc,
            // The evidence of the restart survives the new decision.
            LastSupersededReason = current.SupersededReason ?? current.LastSupersededReason,
        };
    }

    /// <summary>
    /// Advances the countdown to the observing instant under the strict observation contract. The
    /// remaining seconds are derived from the frozen applied delay and the elapsed whole seconds, and
    /// are never allowed to grow.
    ///
    /// <para>
    /// An instant EARLIER than <see cref="NextDispatchCountdown.LastObservedAtUtc"/> is refused as
    /// <see cref="CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION"/> and returns the same instance, so no
    /// accepted evidence changes. An instant EQUAL to it is accepted and leaves the remaining seconds and
    /// the lifecycle where they are: the derivation reproduces the same whole seconds, and a zero applied
    /// delay still completes at its own creation instant. A later instant sets the new observation
    /// baseline on the accepted instance. Time is never inferred from the remaining seconds alone,
    /// because a reversal inside one whole second bucket leaves them unchanged.
    /// </para>
    /// </summary>
    public static NextDispatchCountdown Advance(
        NextDispatchCountdown current,
        DateTimeOffset atUtc,
        out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(current);
        refusal = CountdownRefusalCode.NONE;

        if (current.Lifecycle != CountdownLifecycleState.ACTIVE_FROZEN || current.Decision is null)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_NOT_ACTIVE;
            return current;
        }

        if (current.CreatedAtUtc is null)
        {
            refusal = CountdownRefusalCode.DECISION_TIMESTAMP_INVALID;
            return current;
        }

        // Every accepted countdown carries its observation baseline: Begin sets it from the same
        // parsed decision instant, so a missing one is an invalid record, never a silent fallback.
        if (current.LastObservedAtUtc is null)
        {
            refusal = CountdownRefusalCode.DECISION_TIMESTAMP_INVALID;
            return current;
        }

        // A frozen countdown has not completed and has not dispatched. A constructed record that
        // carries either evidence contradicts its own lifecycle, so it is refused instead of repaired.
        if (current.CompletedAtUtc is not null || current.DispatchedAtUtc is not null)
        {
            refusal = CountdownRefusalCode.DECISION_TIMESTAMP_INVALID;
            return current;
        }

        // Strict premise: the observation instant may never run behind the latest accepted one. This
        // also covers an instant before the creation instant, because the baseline never precedes it.
        if (atUtc < current.LastObservedAtUtc.Value)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION;
            return current;
        }

        if (current.LastObservedAtUtc.Value < current.CreatedAtUtc.Value)
        {
            refusal = CountdownRefusalCode.DECISION_TIMESTAMP_INVALID;
            return current;
        }

        var elapsed = Math.Floor((atUtc - current.CreatedAtUtc.Value).TotalSeconds);
        if (elapsed < 0.0) elapsed = 0.0;
        var remaining = current.Decision.AppliedDelaySeconds - (long)elapsed;
        if (remaining < 0L) remaining = 0L;
        if (remaining > current.RemainingSeconds) remaining = current.RemainingSeconds;

        var remainingSeconds = (int)remaining;
        if (remainingSeconds > 0)
        {
            return current with
            {
                Lifecycle = CountdownLifecycleState.ACTIVE_FROZEN,
                RemainingSeconds = remainingSeconds,
                LastObservedAtUtc = atUtc,
            };
        }

        return current with
        {
            Lifecycle = CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH,
            RemainingSeconds = 0,
            CompletedAtUtc = atUtc,
            LastObservedAtUtc = atUtc,
        };
    }

    /// <summary>
    /// Marks the single admissible dispatch. A second dispatch is refused. The accepted observation
    /// evidence is preserved and the dispatch instant becomes the new latest observation, because a
    /// dispatch IS a lifecycle observation.
    ///
    /// <para>
    /// The accepted order of a finished generation is
    /// <c>CreatedAtUtc &lt;= CompletedAtUtc &lt;= DispatchedAtUtc</c> with
    /// <c>LastObservedAtUtc == DispatchedAtUtc</c>: while the countdown is running or merely completed,
    /// the last observation can never run past the completion instant
    /// (<c>CreatedAtUtc &lt;= LastObservedAtUtc &lt;= CompletedAtUtc</c>), and the dispatch instant then
    /// becomes the final observation of that generation. The last accepted observation therefore never
    /// moves backwards and never disappears, and the dispatch instant is never earlier than either
    /// earlier evidence.
    /// </para>
    ///
    /// <para>
    /// A dispatch instant earlier than the accepted completion or observation evidence is refused as
    /// <see cref="CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION"/> instead of writing contradictory
    /// audit evidence. Missing or mutually contradictory timestamp evidence fails closed as
    /// <see cref="CountdownRefusalCode.DECISION_TIMESTAMP_INVALID"/> before the instant is judged, so a
    /// record whose own observation runs ahead of its completion can never be reinterpreted as a mere
    /// regression. Every refusal returns the SAME instance, so no lifecycle, remaining time or timestamp
    /// changes and no second dispatch becomes admissible.
    /// </para>
    /// </summary>
    public static NextDispatchCountdown MarkDispatched(
        NextDispatchCountdown current,
        DateTimeOffset atUtc,
        out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(current);
        refusal = CountdownRefusalCode.NONE;

        if (current.Lifecycle == CountdownLifecycleState.DISPATCHED)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_ALREADY_DISPATCHED;
            return current;
        }

        if (current.Lifecycle != CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_NOT_COMPLETE;
            return current;
        }

        // Mandatory evidence of a completed countdown: a dispatch is never stamped onto a record that
        // cannot prove when it completed or when it was last observed.
        if (current.Decision is null
            || current.CreatedAtUtc is not { } createdAtUtc
            || current.LastObservedAtUtc is not { } lastObservedAtUtc
            || current.CompletedAtUtc is not { } completedAtUtc
            || current.DispatchedAtUtc is not null
            || current.RemainingSeconds != 0)
        {
            refusal = CountdownRefusalCode.DECISION_TIMESTAMP_INVALID;
            return current;
        }

        // The mandatory order of a completed countdown, refused rather than silently repaired when a
        // constructed record contradicts it.
        if (lastObservedAtUtc < createdAtUtc || completedAtUtc < lastObservedAtUtc)
        {
            refusal = CountdownRefusalCode.DECISION_TIMESTAMP_INVALID;
            return current;
        }

        // The dispatch is the LAST lifecycle observation, so it may never precede the evidence it
        // succeeds: a completion at 10:00:10 can never be dispatched at 10:00:05.
        if (atUtc < completedAtUtc || atUtc < lastObservedAtUtc)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION;
            return current;
        }

        return current with
        {
            Lifecycle = CountdownLifecycleState.DISPATCHED,
            RemainingSeconds = 0,
            DispatchedAtUtc = atUtc,
            LastObservedAtUtc = atUtc,
        };
    }

    /// <summary>
    /// Supersedes a frozen countdown. The frozen decision is retained as evidence, nothing is
    /// dispatched, and the next decision must start a new generation with the FULL applied delay. The
    /// accepted observation evidence is preserved on the superseded instance, and the NEXT generation
    /// starts from its own decision instant instead of inheriting it.
    /// </summary>
    public static NextDispatchCountdown Supersede(
        NextDispatchCountdown current,
        CountdownSupersessionReason reason,
        out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(current);
        refusal = CountdownRefusalCode.NONE;

        if (reason == CountdownSupersessionReason.NONE)
        {
            refusal = CountdownRefusalCode.SUPERSESSION_REASON_REQUIRED;
            return current;
        }

        if (!current.Frozen)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_NOT_ACTIVE;
            return current;
        }

        return current with
        {
            Lifecycle = CountdownLifecycleState.SUPERSEDED,
            SupersededReason = reason,
            LastSupersededReason = reason,
        };
    }

    /// <summary>
    /// Cancels a frozen countdown. A cancelled countdown never dispatches. The accepted observation
    /// evidence is preserved.
    /// </summary>
    public static NextDispatchCountdown Cancel(
        NextDispatchCountdown current,
        out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(current);
        refusal = CountdownRefusalCode.NONE;

        if (!current.Frozen)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_NOT_ACTIVE;
            return current;
        }

        return current with { Lifecycle = CountdownLifecycleState.CANCELLED };
    }

    /// <summary>
    /// Faults the countdown. Fail closed: a faulted countdown never dispatches. The accepted observation
    /// evidence is preserved, so the last instant the Runtime actually observed survives the fault.
    /// </summary>
    public static NextDispatchCountdown Fault(
        NextDispatchCountdown current,
        CountdownRefusalCode code)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (code == CountdownRefusalCode.NONE) throw new ArgumentOutOfRangeException(nameof(code));

        if (current.Lifecycle is CountdownLifecycleState.DISPATCHED or CountdownLifecycleState.CANCELLED)
        {
            return current;
        }

        return current with
        {
            Lifecycle = CountdownLifecycleState.FAULTED,
            RefusalCode = code,
        };
    }

    /// <summary>
    /// The ordinary-mutation guard. While a countdown is frozen, every ordinary mutation (mode,
    /// configuration, assignment, Effective Water Jet, Isolation Valve, P1 to P6 path) is refused
    /// with <see cref="CountdownRefusalCode.MUTATION_REFUSED_DURING_COUNTDOWN"/>.
    /// </summary>
    public static bool RefusesMutation(NextDispatchCountdown current, out CountdownRefusalCode refusal)
    {
        ArgumentNullException.ThrowIfNull(current);
        refusal = CountdownRefusalCode.NONE;
        if (!current.Frozen) return false;
        refusal = CountdownRefusalCode.MUTATION_REFUSED_DURING_COUNTDOWN;
        return true;
    }

    /// <summary>
    /// Detects that the frozen premise no longer holds. Only the NEXT (head) Sensor matters: an
    /// admission behind the head, or an unrelated queue change, is not a premise break.
    /// </summary>
    public static bool TryDetectPremiseBreak(
        NextDispatchCountdown current,
        string? headEntryId,
        string? headSensorId,
        bool headPresent,
        bool headDisabled,
        bool headStructurallyValid,
        out CountdownSupersessionReason reason)
    {
        ArgumentNullException.ThrowIfNull(current);
        reason = CountdownSupersessionReason.NONE;

        if (!current.Frozen || current.Decision is null) return false;

        if (!headPresent || string.IsNullOrWhiteSpace(headEntryId))
        {
            reason = CountdownSupersessionReason.QUEUE_HEAD_WITHDRAWN;
            return true;
        }

        if (!string.Equals(headEntryId, current.Decision.QueueEntryId, StringComparison.Ordinal)
            || !string.Equals(headSensorId, current.Decision.NextSensorId, StringComparison.Ordinal))
        {
            reason = CountdownSupersessionReason.QUEUE_HEAD_IDENTITY_CHANGED;
            return true;
        }

        if (headDisabled)
        {
            reason = CountdownSupersessionReason.HEAD_SENSOR_DISABLED;
            return true;
        }

        if (!headStructurallyValid)
        {
            reason = CountdownSupersessionReason.HEAD_STRUCTURALLY_INVALID;
            return true;
        }

        return false;
    }
}
