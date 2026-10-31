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
/// must use exactly generation + 1; the remaining seconds never increase; a frozen countdown
/// dispatches at most once; a superseded or cancelled countdown never dispatches; and an ordinary
/// mutation is refused while a countdown is frozen.
/// </para>
/// </summary>
public static class NextDispatchCountdownCoordinator
{
    /// <summary>The generation a NEW decision must carry, given the current countdown.</summary>
    public static int NextGeneration(NextDispatchCountdown current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return current.Generation + 1;
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

        if (decision.CountdownGeneration != current.Generation + 1)
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
            // The evidence of the restart survives the new decision.
            LastSupersededReason = current.SupersededReason ?? current.LastSupersededReason,
        };
    }

    /// <summary>
    /// Advances the countdown to the observing instant. The remaining seconds are derived from the
    /// frozen applied delay and the elapsed whole seconds, and are never allowed to grow; a time that
    /// runs backwards is refused rather than reinterpreted.
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

        if (atUtc < current.CreatedAtUtc.Value)
        {
            refusal = CountdownRefusalCode.COUNTDOWN_TIME_REGRESSION;
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
            };
        }

        return current with
        {
            Lifecycle = CountdownLifecycleState.COMPLETED_AWAITING_DISPATCH,
            RemainingSeconds = 0,
            CompletedAtUtc = atUtc,
        };
    }

    /// <summary>Marks the single admissible dispatch. A second dispatch is refused.</summary>
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

        return current with
        {
            Lifecycle = CountdownLifecycleState.DISPATCHED,
            RemainingSeconds = 0,
            DispatchedAtUtc = atUtc,
        };
    }

    /// <summary>
    /// Supersedes a frozen countdown. The frozen decision is retained as evidence, nothing is
    /// dispatched, and the next decision must start a new generation with the FULL applied delay.
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

    /// <summary>Cancels a frozen countdown. A cancelled countdown never dispatches.</summary>
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

    /// <summary>Faults the countdown. Fail closed: a faulted countdown never dispatches.</summary>
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
