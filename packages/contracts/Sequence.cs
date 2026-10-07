namespace Wjss.Contracts;

/// <summary>
/// Runtime-computed availability of one sequence control, with a machine
/// reason code when disabled. Availability STRUCTURE only — the Production
/// Pause / Resume / Reset authority policy is an open Owner decision and the
/// 0.2.1A control set was SYNTHETIC REVIEW TOOLING, not a Production model.
/// </summary>
public sealed record SequenceControlAvailability
{
    public required bool Enabled { get; init; }

    /// <summary>Machine reason code when disabled (e.g. ACTIVE_JOB_PRESENT); null when enabled.</summary>
    public string? Reason { get; init; }
}

/// <summary>Availability projection for the sequence controls of one Runtime revision.</summary>
public sealed record SequenceControls
{
    public required SequenceControlAvailability Start { get; init; }
    public required SequenceControlAvailability PauseAfterCurrentJob { get; init; }
    public required SequenceControlAvailability Resume { get; init; }
    public required SequenceControlAvailability AbortActiveJob { get; init; }
    public required SequenceControlAvailability ResetCritical { get; init; }
    public required SequenceControlAvailability PumpStart { get; init; }
}

/// <summary>
/// Latched Main Pump critical event (accepted baseline: unexpected stop / trip
/// are High Critical; suspension persists; condition-clear and Acknowledge do
/// NOT resume; no automatic Resume; queue frozen unchanged while suspended).
/// Acknowledge closes only when the condition is cleared AND Safe Return is
/// complete (if required). Awareness only otherwise — not a clear, not a Resume.
/// </summary>
public sealed record CriticalPumpEvent
{
    public required string EventId { get; init; }
    public required CriticalPumpKind Kind { get; init; }
    public required AlarmSeverity Severity { get; init; }
    public required string RaisedAt { get; init; }
    public required int EvidenceSeq { get; init; }
    public required bool ConditionActive { get; init; }
    public string? ClearedAt { get; init; }
    public required bool Acknowledged { get; init; }
    public string? AcknowledgedAt { get; init; }
    public required string AlarmId { get; init; }
    public string? JobId { get; init; }
    public string? TargetSensorId { get; init; }
    public JobPhase? PhaseAtEvent { get; init; }
    public required bool SafeReturnRequired { get; init; }
    public required bool SafeReturnComplete { get; init; }
    public required bool SafeReturnFailed { get; init; }

    /// <summary>Blocking modal presentation flag (presentation state; the modal is the UI projection of this latch).</summary>
    public required bool ModalOpen { get; init; }

    public string? ModalClosedAt { get; init; }
}

/// <summary>AutoSequence / critical / last-outcome projection for one revision.</summary>
public sealed record SequenceState
{
    public required AutoSequenceState AutoSequence { get; init; }
    public required AutoSequenceMode Mode { get; init; }
    public required SequenceControls Controls { get; init; }

    /// <summary>The latched critical event, or null when none is active.</summary>
    public CriticalPumpEvent? Critical { get; init; }

    /// <summary>Frozen record of the last released Job, or null.</summary>
    public JobOutcomeRecord? LastJobOutcome { get; init; }
}
