namespace Wjss.Contracts;

/// <summary>
/// One ordered evidence entry of the Mandatory Safe Return ledger. Every Job,
/// whatever the outcome, produces this ledger. Ordering is enforced by
/// increasing <see cref="Seq"/> (close command &lt; axis command; feedback may arrive in either order before
/// outcome &lt; release). Transient evidence for presentation/diagnostics;
/// Production audit content is a later stage and an open decision.
/// </summary>
public sealed record SafeReturnEvent
{
    /// <summary>Global monotonic evidence index (ordering proof).</summary>
    public required int Seq { get; init; }

    public SafeReturnStep? Step { get; init; }

    public required string Event { get; init; }
    public required string At { get; init; }
}

/// <summary>
/// Valve leg of the Safe Return ledger. The Isolation Valve close is COMMANDED
/// before the axis return is commanded. Confirmation may arrive after axis feedback.
/// </summary>
public sealed record SafeReturnValveLeg
{
    public required string ValveId { get; init; }

    public required string Command { get; init; }
    public int? CommandSeq { get; init; }

    public required string Feedback { get; init; }
    public bool? UpperLimitDetected { get; init; }
    public bool? LowerLimitDetected { get; init; }
    public double? PressureBar { get; init; }
    public string? PressureQuality { get; init; }
    public double? LowPressureThresholdBar { get; init; }
    public double? HighPressureThresholdBar { get; init; }
    public bool PressureInputValid { get; init; }
    public string? Resolution { get; init; }
    public string? Diagnosis { get; init; }
    public int? FeedbackSeq { get; init; }
}

/// <summary>
/// Axis leg of the Safe Return ledger. The Job remains Active until Standby is
/// confirmed (SR5) and the job is released (SR7).
/// </summary>
public sealed record SafeReturnAxisLeg
{
    public required string Command { get; init; }
    public int? CommandSeq { get; init; }

    public required string Standby { get; init; }
    public int? StandbySeq { get; init; }
}

/// <summary>
/// Safe Return failure information. The outcome POLICY (names, retries, manual
/// recovery) is an open Owner decision (Safe Return Failure Matrix); the
/// structure below carries only what the accepted baseline fixes: a failure
/// retains the Active Job with no outcome, no release, no dispatch.
/// </summary>
public sealed record SafeReturnFailure
{
    public required string Reason { get; init; }
    public required JobLifecycle AtLifecycle { get; init; }
    public required string At { get; init; }
    public required int Seq { get; init; }
}

/// <summary>Safe Return state attached to the Active Job; present from SR1 until release.</summary>
public sealed record SafeReturnState
{
    public SafeReturnStep? Step { get; init; }

    /// <summary>
    /// Opaque trigger identity. Production trigger NAMES are not approved
    /// (critical Pump decision matrix) - deliberately not an enum.
    /// </summary>
    public required string Trigger { get; init; }

    /// <summary>Outcome to be finalized at SR6 when Safe Return completes. Opaque string: production outcome names are pending.</summary>
    public required string PendingOutcome { get; init; }

    public required JobPhase PhaseAtTrigger { get; init; }
    public required string StartedAt { get; init; }

    public required SafeReturnValveLeg Valve { get; init; }
    public required SafeReturnAxisLeg Axis { get; init; }

    public SafeReturnFailure? Failure { get; init; }

    /// <summary>Bounded evidence tail for presentation (full retention/audit policy is a later stage).</summary>
    public required IReadOnlyList<SafeReturnEvent> Events { get; init; }
}
