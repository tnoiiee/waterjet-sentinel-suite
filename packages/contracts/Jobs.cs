namespace Wjss.Contracts;

/// <summary>
/// The single Active Cleaning Job projection, or null when no Job is Active.
/// At most one Active Job exists at any time (standing constraint; enforced by
/// the Runtime in later checkpoints). The Job remains the Active Job until
/// Safe Return confirms Standby and releases it.
/// </summary>
public sealed record ActiveCleaningJobState
{
    public required string JobId { get; init; }
    public required string TargetSensorId { get; init; }

    /// <summary>Equipment reference identities (1:1 Water Jet / Isolation Valve relationship; identities only, never assignment data).</summary>
    public required string JetId { get; init; }
    public required string ValveId { get; init; }

    public required JobPhase Phase { get; init; }
    public required string PhaseLabel { get; init; }
    public required int PhaseIndex { get; init; }

    public required string StartedAt { get; init; }
    public required string PhaseStartedAt { get; init; }

    /// <summary>0..1 progress within the current phase; presentation-only.</summary>
    public required double PhaseProgress { get; init; }

    public required JobLifecycle Lifecycle { get; init; }

    /// <summary>IN_PROGRESS while running; frozen at the Safe Return trigger. Not the Job outcome.</summary>
    public required string CleaningPhase { get; init; }

    /// <summary>The dispatch evidence this Job was created from (atomic head removal).</summary>
    public required DispatchRecord Dispatch { get; init; }

    /// <summary>Dedicated measured Pump outlet; never inferred from the active Valve outlet.</summary>
    public double? PumpOutletPressureBar { get; init; }
    public string? PumpPressureQuality { get; init; }
    public string? PumpPressureSourceId { get; init; }
    public bool PumpPressureInputValid { get; init; }
    public double PumpReadySetpointBar { get; init; }
    /// <summary>Measured outlet of this Job's paired IVn only; never the Pump outlet.</summary>
    public double? ValveOutletPressureBar { get; init; }
    public string? ValvePressureQuality { get; init; }
    public string? ValvePressureSourceId { get; init; }
    public bool ValvePressureInputValid { get; init; }
    public string? ValveOpenResolution { get; init; }
    public string? ValveDiagnosis { get; init; }

    /// <summary>Present from SR1 until the Job is released.</summary>
    public SafeReturnState? SafeReturn { get; init; }
}

/// <summary>
/// Frozen record of the last released Job (transient presentation evidence,
/// not an audit record). Ordered evidence indices make the Safe Return ordering
/// externally checkable.
/// </summary>
public sealed record JobOutcomeRecord
{
    public required string JobId { get; init; }
    public required string TargetSensorId { get; init; }
    public required string DispatchId { get; init; }

    public required int QueueRevisionBefore { get; init; }
    public required int QueueRevisionAfter { get; init; }
    public required string QueueEntryId { get; init; }

    public required JobPhase PhaseAtTrigger { get; init; }
    public required string Trigger { get; init; }
    public required bool CleaningPhasesComplete { get; init; }

    /// <summary>Outcome name pending Owner decision; opaque string in the contract.</summary>
    public required string Outcome { get; init; }

    public required string ValveId { get; init; }
    public required int ValveCloseCommandSeq { get; init; }
    /// <summary>Null when closure is pressure-inferred without a Lower limit observation.</summary>
    public int? ValveClosedConfirmedSeq { get; init; }
    public string? ValveCloseResolution { get; init; }
    public string? ValveDiagnosis { get; init; }
    public string? QualifiedCompletion { get; init; }
    public IReadOnlyList<string>? QualifiedRemarks { get; init; }
    public IReadOnlyList<EquipmentFaultState>? EquipmentFaults { get; init; }
    public required int AxisReturnCommandSeq { get; init; }
    public required int StandbyConfirmedSeq { get; init; }
    public required int OutcomeSeq { get; init; }
    public required int ReleaseSeq { get; init; }

    public required AutoSequenceState AutoSequenceAtRelease { get; init; }
    public required string FinalizedAt { get; init; }
    public required IReadOnlyList<SafeReturnEvent> Events { get; init; }
}
