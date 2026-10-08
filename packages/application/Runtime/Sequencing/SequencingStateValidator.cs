using Wjss.Contracts;

namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// The single Stage 0.4A state-integrity guard, extended for CP-2. It is pure and
/// deterministic: it returns the first violation in a fixed order, so identical
/// malformed input always yields the same violation identity. It never normalizes
/// a state.
///
/// <para>
/// Used by <see cref="SequencingKernel.Apply"/> (refusal with SEQUENCING_STATE_INVALID)
/// and by every projection (InvalidOperationException). Equipment topology is an
/// event input, not state, so it is not checked here.
/// </para>
///
/// <para>
/// CP-2 combinations checked: lifecycle against sub-stage, cleaning and water flags;
/// the Safe Return ledger against lifecycle and step; trigger against pending
/// outcome; the critical latch against a Pump trigger; the failure code against the
/// wait that failed; the ledger order against the evidence sequence; and (CP-3a, FU-1) a
/// critical latch with a RUNNING Job.
/// </para>
/// </summary>
internal static class SequencingStateValidator
{
    internal const string QueueNull = "QUEUE_NULL";
    internal const string QueueOverCapacity = "QUEUE_OVER_CAPACITY";
    internal const string QueueItemNull = "QUEUE_ITEM_NULL";
    internal const string QueueEntryFieldBlank = "QUEUE_ENTRY_FIELD_BLANK";
    internal const string QueueEntrySecondsNegative = "QUEUE_ENTRY_SECONDS_NEGATIVE";
    internal const string QueueEntryIdDuplicate = "QUEUE_ENTRY_ID_DUPLICATE";
    internal const string QueueSensorDuplicate = "QUEUE_SENSOR_DUPLICATE";
    internal const string QueueRevisionNegative = "QUEUE_REVISION_NEGATIVE";
    internal const string EvidenceSeqNegative = "EVIDENCE_SEQ_NEGATIVE";
    internal const string NextEntrySeqNotUsable = "NEXT_ENTRY_SEQ_NOT_USABLE";
    internal const string NextJobSeqNotUsable = "NEXT_JOB_SEQ_NOT_USABLE";
    internal const string ModeUndefined = "MODE_UNDEFINED";
    internal const string ModeCriticalNotCanonical = "MODE_CRITICAL_NOT_CANONICAL";
    internal const string ModeOffWithJob = "MODE_OFF_WITH_JOB";
    internal const string ModePausedWithJob = "MODE_PAUSED_WITH_JOB";
    internal const string ModePauseRequestedWithoutJob = "MODE_PAUSE_REQUESTED_WITHOUT_JOB";
    internal const string JobFieldBlank = "JOB_FIELD_BLANK";
    internal const string JobPairingInvalid = "JOB_PAIRING_INVALID";
    internal const string JobRevisionInvalid = "JOB_REVISION_INVALID";
    internal const string JobDispatchSeqInvalid = "JOB_DISPATCH_SEQ_INVALID";
    internal const string JobTargetStillQueued = "JOB_TARGET_STILL_QUEUED";

    // CP-2 Job violations.
    internal const string JobLifecycleUndefined = "JOB_LIFECYCLE_UNDEFINED";
    internal const string JobStageUndefined = "JOB_STAGE_UNDEFINED";
    internal const string JobPhaseUndefined = "JOB_PHASE_UNDEFINED";
    internal const string JobValveFeedbackUndefined = "JOB_VALVE_FEEDBACK_UNDEFINED";
    internal const string JobOutcomeUndefined = "JOB_OUTCOME_UNDEFINED";
    internal const string JobStepUndefined = "JOB_STEP_UNDEFINED";
    internal const string JobTriggerUnknown = "JOB_TRIGGER_UNKNOWN";
    internal const string JobTriggerReasonBlank = "JOB_TRIGGER_REASON_BLANK";
    internal const string JobFailureCodeBlank = "JOB_FAILURE_CODE_BLANK";
    internal const string JobRunningHasSafeReturn = "JOB_RUNNING_HAS_SAFE_RETURN";
    internal const string JobCleaningStageMismatch = "JOB_CLEANING_STAGE_MISMATCH";
    internal const string JobWaterOutputMismatch = "JOB_WATER_OUTPUT_MISMATCH";
    internal const string JobPhaseStageMismatch = "JOB_PHASE_STAGE_MISMATCH";
    internal const string JobValveGateInvalid = "JOB_VALVE_GATE_INVALID";
    internal const string JobPumpGateInvalid = "JOB_PUMP_GATE_INVALID";
    internal const string JobLifecycleTransient = "JOB_LIFECYCLE_TRANSIENT";
    internal const string JobSafeReturnWaterOn = "JOB_SAFE_RETURN_WATER_ON";
    internal const string JobSafeReturnIncomplete = "JOB_SAFE_RETURN_INCOMPLETE";
    internal const string JobTriggerOutcomeMismatch = "JOB_TRIGGER_OUTCOME_MISMATCH";
    internal const string JobTriggerReasonMismatch = "JOB_TRIGGER_REASON_MISMATCH";
    internal const string JobCriticalLatchMissing = "JOB_CRITICAL_LATCH_MISSING";
    internal const string JobLedgerIncomplete = "JOB_LEDGER_INCOMPLETE";
    internal const string JobLedgerInvalid = "JOB_LEDGER_INVALID";
    internal const string JobStepMismatch = "JOB_STEP_MISMATCH";
    internal const string JobFailureInvalid = "JOB_FAILURE_INVALID";
    internal const string JobRunningUnderLatch = "JOB_RUNNING_UNDER_LATCH";

    /// <summary>Returns the first violation identity, or null when the state is consistent.</summary>
    internal static string? FirstViolation(SequencingState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var queue = state.RawQueue;
        if (queue is null)
        {
            return QueueNull;
        }

        if (queue.Count > QueueSummary.MaxEntries)
        {
            return QueueOverCapacity;
        }

        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        var sensorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in queue)
        {
            if (entry is null)
            {
                return QueueItemNull;
            }

            if (IsBlank(entry.EntryId) || IsBlank(entry.SensorId) || IsBlank(entry.SourceReason))
            {
                return QueueEntryFieldBlank;
            }

            if (entry.SecondsSinceLastClean < 0)
            {
                return QueueEntrySecondsNegative;
            }

            if (!entryIds.Add(entry.EntryId))
            {
                return QueueEntryIdDuplicate;
            }

            if (!sensorIds.Add(entry.SensorId))
            {
                return QueueSensorDuplicate;
            }
        }

        if (state.QueueRevision < 0)
        {
            return QueueRevisionNegative;
        }

        if (state.EvidenceSeq < 0)
        {
            return EvidenceSeqNegative;
        }

        if (state.NextEntrySeq < 1)
        {
            return NextEntrySeqNotUsable;
        }

        if (state.NextJobSeq < 1)
        {
            return NextJobSeqNotUsable;
        }

        if (!Enum.IsDefined(state.Mode))
        {
            return ModeUndefined;
        }

        if (state.Mode == AutoSequenceMode.CRITICAL_SUSPENDED)
        {
            return ModeCriticalNotCanonical;
        }

        if (state.ActiveJob is { } job)
        {
            return JobViolation(state, job, queue);
        }

        return state.Mode == AutoSequenceMode.PAUSE_REQUESTED ? ModePauseRequestedWithoutJob : null;
    }

    /// <summary>Throws InvalidOperationException carrying SEQUENCING_STATE_INVALID when the state is inconsistent.</summary>
    internal static void RequireValid(SequencingState state)
    {
        var violation = FirstViolation(state);
        if (violation is not null)
        {
            throw new InvalidOperationException(SequencingCodes.StateInvalid + ": " + violation);
        }
    }

    private static string? JobViolation(SequencingState state, SequencingActiveJob job, IReadOnlyList<SequencingEntry> queue)
    {
        if (state.Mode == AutoSequenceMode.OFF)
        {
            return ModeOffWithJob;
        }

        if (state.Mode == AutoSequenceMode.PAUSED)
        {
            return ModePausedWithJob;
        }

        if (IsBlank(job.JobId)
            || IsBlank(job.DispatchId)
            || IsBlank(job.TargetSensorId)
            || IsBlank(job.JetId)
            || IsBlank(job.ValveId)
            || IsBlank(job.SourceEntryId))
        {
            return JobFieldBlank;
        }

        if (!SequencingTopology.IsPairedOrdinal(job.JetId, job.ValveId))
        {
            return JobPairingInvalid;
        }

        // Computed in 64-bit so that the consecutive-revision check cannot itself overflow.
        var consecutive = (long)job.QueueRevisionBefore + 1 == job.QueueRevisionAfter;
        if (job.QueueRevisionBefore < 0 || !consecutive || job.QueueRevisionAfter > state.QueueRevision)
        {
            return JobRevisionInvalid;
        }

        if (job.DispatchEvidenceSeq < 1 || job.DispatchEvidenceSeq > state.EvidenceSeq)
        {
            return JobDispatchSeqInvalid;
        }

        foreach (var entry in queue)
        {
            if (string.Equals(entry.SensorId, job.TargetSensorId, StringComparison.Ordinal)
                || string.Equals(entry.EntryId, job.SourceEntryId, StringComparison.Ordinal))
            {
                return JobTargetStillQueued;
            }
        }

        return CleaningJobViolation(state, job);
    }

    private static string? CleaningJobViolation(SequencingState state, SequencingActiveJob job)
    {
        if (!Enum.IsDefined(job.Lifecycle))
        {
            return JobLifecycleUndefined;
        }

        if (!Enum.IsDefined(job.Stage))
        {
            return JobStageUndefined;
        }

        if (job.VerifiedPhase is { } phase && !Enum.IsDefined(phase))
        {
            return JobPhaseUndefined;
        }

        if (job.LastValveFeedback is { } valve && !Enum.IsDefined(valve))
        {
            return JobValveFeedbackUndefined;
        }

        if (job.PendingOutcome is { } pending && !Enum.IsDefined(pending))
        {
            return JobOutcomeUndefined;
        }

        if (job.Step is { } step && !Enum.IsDefined(step))
        {
            return JobStepUndefined;
        }

        if (job.Trigger is not null && !SequencingCodes.IsKnownTrigger(job.Trigger))
        {
            return JobTriggerUnknown;
        }

        if (job.TriggerReason is not null && IsBlank(job.TriggerReason))
        {
            return JobTriggerReasonBlank;
        }

        if (job.FailureCode is not null && IsBlank(job.FailureCode))
        {
            return JobFailureCodeBlank;
        }

        return job.Lifecycle == JobLifecycle.RUNNING
            ? RunningJobViolation(state, job)
            : SafeReturnJobViolation(state, job);
    }

    private static string? RunningJobViolation(SequencingState state, SequencingActiveJob job)
    {
        if (job.Trigger is not null
            || job.TriggerReason is not null
            || job.PendingOutcome is not null
            || job.Step is not null
            || job.FailureCode is not null
            || !job.Ledger.IsEmpty)
        {
            return JobRunningHasSafeReturn;
        }

        var cleaning = job.Stage == CleaningStage.CLEANING;
        if (job.CleaningActive != cleaning)
        {
            return JobCleaningStageMismatch;
        }

        if (job.WaterOutputOn != job.CleaningActive)
        {
            return JobWaterOutputMismatch;
        }

        if (job.VerifiedPhase is not null && !cleaning)
        {
            return JobPhaseStageMismatch;
        }

        if (job.LastValveFeedback == ValveFeedbackState.INVALID_LIMIT_STATE)
        {
            // An invalid limit state always leaves RUNNING through Safe Return.
            return JobValveGateInvalid;
        }

        if (job.Stage == CleaningStage.READY_TO_CLEAN && job.LastValveFeedback != ValveFeedbackState.CLOSED)
        {
            return JobValveGateInvalid;
        }

        if (cleaning && job.LastValveFeedback is null)
        {
            return JobValveGateInvalid;
        }

        if (cleaning && !job.PumpReady)
        {
            return JobPumpGateInvalid;
        }

        // CP-3a (FU-1): a RUNNING Job never coexists with the critical latch. This check runs last, so every
        // earlier RUNNING identity keeps its code.
        return state.CriticalSuspended ? JobRunningUnderLatch : null;
    }

    private static string? SafeReturnJobViolation(SequencingState state, SequencingActiveJob job)
    {
        if (job.CleaningActive || job.WaterOutputOn)
        {
            return JobSafeReturnWaterOn;
        }

        if (job.Trigger is null || job.PendingOutcome is null || job.Step is null)
        {
            return JobSafeReturnIncomplete;
        }

        if (!TriggerMatchesOutcome(job.Trigger, job.PendingOutcome.Value))
        {
            return JobTriggerOutcomeMismatch;
        }

        if (job.Trigger == SequencingCodes.TriggerExecutionFailure)
        {
            if (job.TriggerReason is null)
            {
                return JobTriggerReasonMismatch;
            }
        }
        else if (job.TriggerReason is not null)
        {
            return JobTriggerReasonMismatch;
        }

        if (SequencingCodes.IsPumpTrigger(job.Trigger) && !state.CriticalSuspended)
        {
            return JobCriticalLatchMissing;
        }

        var ledger = job.Ledger;
        if (ledger.WaterOffSeq is null || ledger.ValveCloseRequestSeq is null)
        {
            return JobLedgerIncomplete;
        }

        if (!LedgerOrdered(ledger, job.DispatchEvidenceSeq, state.EvidenceSeq))
        {
            return JobLedgerInvalid;
        }

        return job.Lifecycle switch
        {
            JobLifecycle.SAFE_RETURN_VERIFY_VALVE_CLOSED => ValveWaitViolation(job),
            JobLifecycle.SAFE_RETURN_VERIFY_STANDBY => AxisWaitViolation(job),
            JobLifecycle.SAFE_RETURN_FAILED => FailedViolation(job),
            _ => JobLifecycleTransient,
        };
    }

    private static string? ValveWaitViolation(SequencingActiveJob job)
    {
        var ledger = job.Ledger;
        if (job.Step != SafeReturnStep.SR2)
        {
            return JobStepMismatch;
        }

        if (ledger.ValveClosedSeq is not null
            || ledger.AxisReturnRequestSeq is not null
            || ledger.AxisStandbySeq is not null
            || ledger.FailureSeq is not null
            || job.FailureCode is not null)
        {
            return JobLedgerInvalid;
        }

        return null;
    }

    private static string? AxisWaitViolation(SequencingActiveJob job)
    {
        var ledger = job.Ledger;
        if (job.Step != SafeReturnStep.SR4)
        {
            return JobStepMismatch;
        }

        if (ledger.ValveClosedSeq is null
            || ledger.AxisReturnRequestSeq is null
            || ledger.AxisStandbySeq is not null
            || ledger.FailureSeq is not null
            || job.FailureCode is not null)
        {
            return JobLedgerInvalid;
        }

        return job.LastValveFeedback == ValveFeedbackState.CLOSED ? null : JobValveGateInvalid;
    }

    private static string? FailedViolation(SequencingActiveJob job)
    {
        var ledger = job.Ledger;
        if (job.Step != SafeReturnStep.SR_FAILED)
        {
            return JobStepMismatch;
        }

        if (job.FailureCode is null)
        {
            return JobFailureInvalid;
        }

        if (ledger.FailureSeq is null || ledger.AxisStandbySeq is not null)
        {
            return JobLedgerInvalid;
        }

        var valveFailure = job.FailureCode is SequencingCodes.ValveInvalidLimitState or SequencingCodes.ValveCloseNotConfirmed;
        var axisFailure = job.FailureCode is SequencingCodes.AxisFault or SequencingCodes.AxisStandbyNotConfirmedFailure;
        if (!valveFailure && !axisFailure)
        {
            return JobFailureInvalid;
        }

        if (valveFailure)
        {
            return ledger.ValveClosedSeq is null && ledger.AxisReturnRequestSeq is null ? null : JobFailureInvalid;
        }

        return ledger.ValveClosedSeq is not null && ledger.AxisReturnRequestSeq is not null ? null : JobFailureInvalid;
    }

    private static bool LedgerOrdered(SafeReturnLedger ledger, int dispatchSeq, int evidenceSeq)
    {
        int? previous = null;
        foreach (var seq in new int?[]
                 {
                     ledger.WaterOffSeq,
                     ledger.ValveCloseRequestSeq,
                     ledger.ValveClosedSeq,
                     ledger.AxisReturnRequestSeq,
                     ledger.AxisStandbySeq,
                     ledger.FailureSeq,
                 })
        {
            if (seq is not { } value)
            {
                continue;
            }

            if (value <= dispatchSeq || value > evidenceSeq)
            {
                return false;
            }

            if (previous is { } before && value <= before)
            {
                return false;
            }

            previous = value;
        }

        return true;
    }

    private static bool TriggerMatchesOutcome(string trigger, CleaningJobOutcome outcome) => trigger switch
    {
        SequencingCodes.TriggerNormalCompletion => outcome == CleaningJobOutcome.COMPLETED,
        SequencingCodes.TriggerAbort => outcome == CleaningJobOutcome.ABORTED,
        SequencingCodes.TriggerExecutionFailure => outcome == CleaningJobOutcome.FAILED,
        SequencingCodes.TriggerPumpUnexpectedStop => outcome == CleaningJobOutcome.ABORTED,
        SequencingCodes.TriggerPumpTrip => outcome == CleaningJobOutcome.ABORTED,
        _ => false,
    };

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}
