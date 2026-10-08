namespace Wjss.Contracts;

/// <summary>The four boiler walls (accepted domain baseline).</summary>
public enum Wall { LEFT, REAR, RIGHT, FRONT }

/// <summary>
/// Canonical logical-position kind (ADR-0017, Owner-approved Stage 0.3A-3 Checkpoint A;
/// atomic Runtime migration delivered in Stage 0.3A-3 Checkpoint C). NON_SENSOR_GAP
/// positions are location anchors only: they are never Sensors, never equipment entities,
/// and never appear in queues, selections, Cleaning Jobs, alarms, or coverage sets. The
/// formerly transitional <c>CANNON</c> slot vocabulary was migrated away atomically in
/// Checkpoint C and no Cannon entity or alias exists anywhere in the Runtime surface.
/// </summary>
public enum LogicalPositionKind { SENSOR, NON_SENSOR_GAP }

/// <summary>
/// Data-quality vocabulary for a Sensor presentation record. Quality SEMANTICS are
/// accepted (quality-aware pipeline); quality-derived QUEUE behaviour is NOT a
/// contract concern (queue eligibility policy is an open Owner decision).
/// </summary>
public enum Quality { GOOD, UNCERTAIN, BAD, STALE, DISABLED }

/// <summary>Dirty-score classification produced by the Runtime (accepted spike rule).</summary>
public enum Classification { DIRTY, CLEANER, NOT_CLASSIFIED }

/// <summary>Basis for the current classification (UNCERTAIN keeps the last validated value).</summary>
public enum ClassificationBasis { CURRENT, LAST_VALIDATED, NONE }

/// <summary>
/// Per-Sensor GlobalQueue membership. Presence in the queue means READY
/// (Owner domain correction). There are deliberately NO BLOCKED / HELD /
/// WAITING_* / EXCLUDED entry states.
/// </summary>
public enum QueueState { NONE, QUEUED, ACTIVE }

public enum AlarmState { NONE, ACTIVE_UNACK, ACTIVE_ACK, CLEARED_UNACK }

public enum AlarmSeverity { LOW, MEDIUM, HIGH }

/// <summary>Cleaning phases P1-P6 are the accepted logical phase identity for presentation.</summary>
public enum JobPhase { P1, P2, P3, P4, P5, P6 }

/// <summary>
/// Job lifecycle. SR steps are part of the accepted Mandatory Safe Return
/// baseline (SR1-SR5 + failure); final outcome NAMES remain an open Owner
/// decision, which is why <c>Outcome</c> fields are opaque strings, not enums.
/// </summary>
public enum JobLifecycle
{
    RUNNING,
    ABORTING,
    SAFE_RETURN_CLOSE_VALVE,
    SAFE_RETURN_VERIFY_VALVE_CLOSED,
    SAFE_RETURN_TO_STANDBY,
    SAFE_RETURN_VERIFY_STANDBY,
    SAFE_RETURN_FAILED,
}

/// <summary>Mandatory Safe Return steps (accepted ordering SR1-SR8; SR8 is "consider later sequencing").</summary>
public enum SafeReturnStep { SR1, SR2, SR3, SR4, SR5, SR6, SR7, SR8, SR_FAILED }

/// <summary>AutoSequence presentation states (accepted development baseline).</summary>
public enum AutoSequenceState
{
    CRITICAL_SUSPENDED,
    OFF,
    PAUSE_REQUESTED,
    PAUSED,
    JOB_ACTIVE,
    PUMP_NOT_READY,
    QUEUE_EMPTY,
    READY_TO_DISPATCH,
}

/// <summary>AutoSequence lifecycle mode (accepted development baseline).</summary>
public enum AutoSequenceMode { OFF, RUNNING, PAUSE_REQUESTED, PAUSED, CRITICAL_SUSPENDED }

/// <summary>Pump presentation states. TRIPPED is a synthetic-safe class from the Owner critical Pump decision.</summary>
public enum PumpRunState { STOPPED, STARTING, RUNNING, STOPPING, TRIPPED }

/// <summary>Kind of Main Pump critical event (accepted: unexpected stop and trip are High Critical).</summary>
public enum CriticalPumpKind { MAIN_PUMP_UNEXPECTED_STOP, MAIN_PUMP_TRIP }

/// <summary>Per-device link state for communication health (transport evidence, never value change).</summary>
public enum DeviceLinkState { ONLINE, TIMEOUT, RECOVERING, STALE_SOURCE }

/// <summary>
/// Device profiles (ADR-0012). Only SIMULATOR may START in Stage 0.3A; the other
/// values exist for contract stability and startup refusal. See Wjss.Domain.ProfileStartPolicy.
/// </summary>
public enum DeviceProfile { SIMULATOR, TEST_HARDWARE, PRODUCTION }
