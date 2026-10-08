namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// Derived Isolation Valve feedback state (accepted matrix, CLEANING_SEQUENCE.md §5.2).
/// Kernel-internal: the derivation has no wire meaning in CP-2.
/// </summary>
public enum ValveFeedbackState
{
    CLOSED,
    OPEN,
    TRANSIT_OR_FAULT,
    INVALID_LIMIT_STATE,
}

/// <summary>
/// Abstract Axis Standby feedback. Synthetic input only in CP-2: no coordinate,
/// distance, speed, homing or motion value exists anywhere in the kernel.
/// </summary>
public enum AxisFeedbackState
{
    AT_STANDBY,
    NOT_AT_STANDBY,
    UNKNOWN,
    FAULT,
}

/// <summary>
/// Synthetic Main Pump classification observed by the kernel. READY and
/// EXPECTED_STOP are non-critical. UNEXPECTED_STOP and TRIP are the two accepted
/// critical classes (<see cref="Wjss.Contracts.CriticalPumpKind"/>).
/// </summary>
public enum PumpObservation
{
    READY,
    EXPECTED_STOP,
    UNEXPECTED_STOP,
    TRIP,
}

/// <summary>Names the feedback wait that a timeout input expires.</summary>
public enum FeedbackTarget
{
    VALVE_CLOSED,
    AXIS_STANDBY,
}

/// <summary>
/// Kernel-internal sub-stage of an active Job whose lifecycle is RUNNING. The
/// accepted lifecycle names are reused for every other state; these sub-stages
/// add no wire vocabulary.
/// </summary>
public enum CleaningStage
{
    PREPARING,
    READY_TO_CLEAN,
    CLEANING,
}

/// <summary>
/// Internal Cleaning Job outcome. The wire field stays an opaque string
/// (contract comment, O-13). RECOVERY_REQUIRED is evidence only: it is never
/// stored as a pending outcome and never finalises a release.
/// </summary>
public enum CleaningJobOutcome
{
    COMPLETED,
    FAILED,
    ABORTED,
    RECOVERY_REQUIRED,
}

/// <summary>Pure derivation of the valve state from the two limit observations.</summary>
public static class ValveFeedbackDerivation
{
    /// <summary>Upper 0 and lower 1 is CLOSED; upper 1 and lower 0 is OPEN; 0/0 is TRANSIT_OR_FAULT; 1/1 is INVALID_LIMIT_STATE.</summary>
    public static ValveFeedbackState Derive(bool upperLimit, bool lowerLimit) => (upperLimit, lowerLimit) switch
    {
        (false, true) => ValveFeedbackState.CLOSED,
        (true, false) => ValveFeedbackState.OPEN,
        (false, false) => ValveFeedbackState.TRANSIT_OR_FAULT,
        _ => ValveFeedbackState.INVALID_LIMIT_STATE,
    };
}
