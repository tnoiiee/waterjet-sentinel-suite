using System.Globalization;
using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime;

/// <summary>
/// TRIAL B: the isolated, single-use Isolation Valve Hardware trial.
///
/// It commands exactly ONE canonical Isolation Valve OPEN and then CLOSED through the existing
/// <see cref="ProcessIoSession"/> admission and dispatch lifecycle, and it accepts the result only
/// from the existing field-feedback contract. Nothing else is composed: no motion command, no axis
/// positioning, no Cleaning Path point, no queue, no inter-job delay and no Pump command.
///
/// The Pump-ready input of THIS trial is an explicit software simulation, because the physical Pump
/// is stopped by design. The simulated sample is trial-local, is selected by an exact literal, is
/// never persisted, and is consumed only by this one process. It is never presented as physical
/// evidence, and it can never reach the normal Runtime path: the integrated TEST_HARDWARE_DRY trial
/// and every read-only or Production path are untouched by this file.
///
/// Evidence separation is the point of the trial. The IV command, the FC05 coil write, the
/// Upper/open and Lower/closed discrete inputs and the IV outlet pressure are physical. The
/// Pump-ready input is simulated and is reported as simulated. A passing run therefore proves the
/// valve command and feedback path, and it proves NOTHING about the Pump, the AI-003 transmitter or
/// water-on behaviour.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA. The Owner-local build and the separately
/// authorized physical trial are the validation of record.
///
/// Exit codes: 0 trial completed and accepted; 2 composition or sequence refusal after the trial
/// selector was accepted; 3 supervision timeout before a terminal result; 4 invocation, selector or
/// configuration refusal before anything was constructed.
/// </summary>
public static class DryIvTrial
{
    /// <summary>The exact command identity of this trial. Nothing else selects it.</summary>
    internal const string Token = "--dry-iv-trial";

    /// <summary>The dedicated trial selector. The integrated dry selector never selects this trial.</summary>
    public const string ModeVariable = "WJSS_DRY_IV_TRIAL_MODE";

    /// <summary>The only value of <see cref="ModeVariable"/> that selects the trial.</summary>
    public const string ModeSoftwareSimulatedAi003 = "SOFTWARE_SIMULATED_AI003";

    /// <summary>The explicit simulated Pump-ready pressure, in bar. Never defaulted.</summary>
    public const string SimulatedPressureVariable = "WJSS_DRY_IV_TRIAL_SIMULATED_AI003_BAR";

    /// <summary>The Owner attestation that the physical Pump is stopped for this trial.</summary>
    public const string PumpStoppedVariable = "WJSS_DRY_IV_TRIAL_PUMP_STOPPED";

    /// <summary>The one canonical Isolation Valve this trial actuates.</summary>
    public const string ValveVariable = "WJSS_DRY_IV_TRIAL_VALVE";

    /// <summary>The only accepted attestation value. It is a statement by the Owner, not a measurement.</summary>
    public const string PumpStoppedAttestation = "PUMP_STOPPED_OWNER_ATTESTED";

    /// <summary>Evidence kind reported for the Pump-ready input of this trial.</summary>
    public const string EvidenceKindSimulated = "SOFTWARE_SIMULATED";

    /// <summary>Trial provenance carried by the simulated sample.</summary>
    public const string SimulatedProvenance = "SOFTWARE_SIMULATED_AI003_FOR_IV_TRIAL";

    /// <summary>The single purpose this simulated evidence may be used for.</summary>
    public const string TrialPurpose = "IV_OPEN_CLOSE_HARDWARE_TRIAL_ONLY";

    /// <summary>Bounded pause between two supervision observations. Never zero when used.</summary>
    private const int MaximumPollMilliseconds = 25;

    // ------------------------------------------------------------------ typed refusal codes

    public const string RefusedModeNotSelected = "IV_TRIAL_REFUSED_MODE_NOT_SELECTED";
    public const string RefusedModeInvalid = "IV_TRIAL_REFUSED_MODE_INVALID";
    public const string RefusedIntegratedDrySelected = "IV_TRIAL_REFUSED_INTEGRATED_DRY_SELECTED";
    public const string RefusedProfileNotDry = "IV_TRIAL_REFUSED_PROFILE_NOT_TEST_HARDWARE_DRY";
    public const string RefusedProduction = "IV_TRIAL_REFUSED_PRODUCTION_NOT_AUTHORIZED";
    public const string RefusedEndpoint = "IV_TRIAL_REFUSED_ENDPOINT";
    public const string RefusedConsentMissing = "IV_TRIAL_REFUSED_CONSENT_MISSING";
    public const string RefusedPumpStoppedMissing = "IV_TRIAL_REFUSED_PUMP_STOPPED_ATTESTATION_MISSING";
    public const string RefusedSimulatedPressureMissing = "IV_TRIAL_REFUSED_SIMULATED_PRESSURE_MISSING";
    public const string RefusedSimulatedPressureNotFinite = "IV_TRIAL_REFUSED_SIMULATED_PRESSURE_NOT_FINITE";
    public const string RefusedSimulatedPressureAtOrBelowSetpoint = "IV_TRIAL_REFUSED_SIMULATED_PRESSURE_AT_OR_BELOW_SETPOINT";
    public const string RefusedSimulatedPressureOutOfRange = "IV_TRIAL_REFUSED_SIMULATED_PRESSURE_OUT_OF_RANGE";
    public const string RefusedThresholds = "IV_TRIAL_REFUSED_THRESHOLDS";
    public const string RefusedSupervision = "IV_TRIAL_REFUSED_SUPERVISION_WINDOW";
    public const string RefusedValveIdentity = "IV_TRIAL_REFUSED_VALVE_IDENTITY_INVALID";
    public const string RefusedCatalogue = "IV_TRIAL_REFUSED_CATALOGUE";
    public const string RefusedSessionNotOpen = "IV_TRIAL_REFUSED_SESSION_NOT_OPEN";
    public const string RefusedGenerationChanged = "IV_TRIAL_REFUSED_GENERATION_CHANGED";
    public const string RefusedInitialStateUnsafe = "IV_TRIAL_REFUSED_INITIAL_STATE_UNSAFE";
    public const string RefusedInitialStateContradictory = "IV_TRIAL_REFUSED_INITIAL_STATE_CONTRADICTORY";
    public const string RefusedWriteNotAuthorized = "IV_TRIAL_REFUSED_WRITE_NOT_AUTHORIZED";
    public const string RefusedOpenNotDispatched = "IV_TRIAL_REFUSED_OPEN_NOT_DISPATCHED";
    public const string RefusedOpenNotConfirmed = "IV_TRIAL_REFUSED_OPEN_NOT_FIELD_CONFIRMED";
    public const string RefusedOpenContradictory = "IV_TRIAL_REFUSED_OPEN_FEEDBACK_CONTRADICTORY";
    public const string RefusedCloseNotDispatched = "IV_TRIAL_REFUSED_CLOSE_NOT_DISPATCHED";
    public const string RefusedCloseNotConfirmed = "IV_TRIAL_REFUSED_CLOSE_NOT_FIELD_CONFIRMED";
    public const string RefusedCloseContradictory = "IV_TRIAL_REFUSED_CLOSE_FEEDBACK_CONTRADICTORY";
    public const string RefusedPumpReadyNotAccepted = "IV_TRIAL_REFUSED_PUMP_READY_NOT_ACCEPTED";
    public const string AbortedUnexpectedFault = "IV_TRIAL_ABORTED_UNEXPECTED_FAULT";
    public const string AbortedOperator = "IV_TRIAL_ABORTED_BY_OPERATOR";

    /// <summary>The existing contradiction assessment code, reused rather than duplicated.</summary>
    private const string FeedbackContradiction = "IV_FEEDBACK_CONTRADICTORY_BOTH_LIMITS";

    // ------------------------------------------------------------------ outcome codes

    public const string PumpReadyAccepted = "IV_TRIAL_PUMP_READY_ACCEPTED_SOFTWARE_SIMULATED";
    public const string OpenConfirmed = "IV_TRIAL_OPEN_FIELD_CONFIRMED";
    public const string TrialAccepted = "IV_TRIAL_ACCEPTED";
    public const string CleanupIncomplete = "IV_TRIAL_CLEANUP_INCOMPLETE";

    /// <summary>
    /// The trial-only Pump-ready input. It validates the configured bar against the very rule the
    /// Product uses, and it stamps the sample with its simulated provenance so no consumer can
    /// mistake it for a physical observation.
    /// </summary>
    public static bool TryCreateSimulatedPumpReady(
        double bar,
        DateTimeOffset at,
        SequencingPressureThresholds thresholds,
        int trialGeneration,
        out DryIvTrialSimulatedPumpReady? sample,
        out string code)
    {
        sample = null;
        code = string.Empty;

        if (!double.IsFinite(bar))
        {
            code = RefusedSimulatedPressureNotFinite;
            return false;
        }

        // The accepted AI-003 engineering domain is the existing conversion range, derived here from
        // the adopted raw full scale rather than restated as a new constant.
        double domainMaximum = ProcessIoEngineering.PumpOutletPressureBar(ProcessIoRepresentation.RawFullScale);
        if (bar < 0 || bar > domainMaximum)
        {
            code = RefusedSimulatedPressureOutOfRange;
            return false;
        }

        if (bar <= thresholds.PumpReadySetpointBar)
        {
            code = RefusedSimulatedPressureAtOrBelowSetpoint;
            return false;
        }

        // The Pump-ready rule requires this exact source identity; the tag is reported beside it so
        // the evidence names AI-003 explicitly without renaming the rule's own source constant.
        var pressure = new PressureSample(
            bar,
            PressureQuality.GOOD,
            false,
            at.ToUniversalTime(),
            PressureSample.PumpOutletSource);

        // The sample must satisfy the existing Product rule before it may be offered to the trial.
        if (!SequencingPressure.Valid(pressure) || !SequencingPressure.PumpReady(pressure, thresholds))
        {
            code = RefusedPumpReadyNotAccepted;
            return false;
        }

        sample = new DryIvTrialSimulatedPumpReady
        {
            Sample = pressure,
            SourceTag = ProcessIoIsolationValves.PumpOutletPressureTag,
            SimulatedBar = bar,
            EvidenceKind = EvidenceKindSimulated,
            Provenance = SimulatedProvenance,
            TrialPurpose = TrialPurpose,
            TrialGeneration = trialGeneration,
        };
        code = PumpReadyAccepted;
        return true;
    }

    /// <summary>
    /// Reads the trial selector only. Absent means the trial was not chosen and the host continues
    /// down its existing path; any present value other than the exact literal is a refusal.
    /// </summary>
    public static bool TryReadMode(
        Func<string, string?> readEnvironment,
        out bool trialSelected,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        trialSelected = false;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(ModeVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!string.Equals(raw.Trim(), ModeSoftwareSimulatedAi003, StringComparison.Ordinal))
        {
            refusalCode = RefusedModeInvalid;
            refusalDetail =
                $"{ModeVariable} must be exactly '{ModeSoftwareSimulatedAi003}' when present. No other value "
                + "selects the IV trial and nothing is substituted.";
            return false;
        }

        trialSelected = true;
        return true;
    }

    /// <summary>
    /// Validates the complete trial configuration without contacting anything. Every value is Owner
    /// local, nothing is defaulted, and the integrated dry selector is refused rather than composed
    /// on top of this trial.
    /// </summary>
    public static bool TryReadConfiguration(
        DeviceProfile profile,
        Func<string, string?> readEnvironment,
        out DryIvTrialConfiguration? configuration,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        configuration = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        // Production is refused before any other value is read.
        if (profile == DeviceProfile.PRODUCTION)
        {
            refusalCode = RefusedProduction;
            refusalDetail = "PRODUCTION is never startable by this trial. Nothing was read and nothing was contacted.";
            return false;
        }

        if (!TryReadMode(readEnvironment, out bool trialSelected, out refusalCode, out refusalDetail))
        {
            return false;
        }

        if (!trialSelected)
        {
            refusalCode = RefusedModeNotSelected;
            refusalDetail =
                $"The IV trial requires {ModeVariable}='{ModeSoftwareSimulatedAi003}'. The integrated dry "
                + "command mode never selects this trial and no mode is inferred.";
            return false;
        }

        // The integrated dry composition is a different trial with a different evidence class. When its
        // selector is present this trial refuses instead of composing beside it.
        if (!ProcessIoEndpointConfiguration.TryReadDryMode(
                readEnvironment, out bool integratedDrySelected, out string dryCode, out string dryDetail))
        {
            refusalCode = dryCode;
            refusalDetail = dryDetail;
            return false;
        }

        if (integratedDrySelected)
        {
            refusalCode = RefusedIntegratedDrySelected;
            refusalDetail =
                $"{ProcessIoEndpointConfiguration.DryModeVariable} selects the integrated dry path. That path "
                + "and this IV trial are separate evidence classes and are never composed together.";
            return false;
        }

        if (profile != DeviceProfile.TEST_HARDWARE_DRY)
        {
            refusalCode = RefusedProfileNotDry;
            refusalDetail =
                $"The IV trial composes only for TEST_HARDWARE_DRY; the profile is '{profile}'. No profile is "
                + "substituted and no simulated runtime is used as a stand-in.";
            return false;
        }

        if (!ProcessIoEndpointConfiguration.TryResolveEndpoint(
                readEnvironment, out ProcessIoEndpointHandle? endpoint, out refusalCode, out refusalDetail)
            || endpoint is null)
        {
            refusalCode = refusalCode.Length == 0 ? RefusedEndpoint : refusalCode;
            return false;
        }

        // The consent channel is the existing published one: the trial consumes the same variable and
        // the same phrase as the integrated dry composition. A second consent phrase or variable would
        // be a second authority for the same physical act.
        if (!ProcessIoEndpointConfiguration.TryReadConsent(
                readEnvironment, out string? consent, out refusalCode, out refusalDetail)
            || string.IsNullOrEmpty(consent))
        {
            refusalCode = refusalCode.Length == 0 ? RefusedConsentMissing : refusalCode;
            return false;
        }

        string? attestation = readEnvironment(PumpStoppedVariable);
        if (string.IsNullOrWhiteSpace(attestation)
            || !string.Equals(attestation.Trim(), PumpStoppedAttestation, StringComparison.Ordinal))
        {
            refusalCode = RefusedPumpStoppedMissing;
            refusalDetail =
                $"This trial requires the Owner attestation {PumpStoppedVariable}='{PumpStoppedAttestation}'. "
                + "The software never infers that the Pump is stopped and never claims to have verified it.";
            return false;
        }

        string? rawValve = readEnvironment(ValveVariable);
        if (string.IsNullOrWhiteSpace(rawValve)
            || !ProcessIoIsolationValves.TryParseValveId(rawValve, out int ordinal))
        {
            refusalCode = RefusedValveIdentity;
            refusalDetail =
                $"{ValveVariable} must name exactly one canonical Isolation Valve identity. No valve is "
                + "selected by default and no alias is accepted.";
            return false;
        }

        if (!TryReadThresholds(readEnvironment, out SequencingPressureThresholds? thresholds, out refusalCode, out refusalDetail)
            || thresholds is null)
        {
            return false;
        }

        if (!TryReadSupervision(readEnvironment, out TimeSpan supervision, out refusalCode, out refusalDetail))
        {
            return false;
        }

        string? rawPressure = readEnvironment(SimulatedPressureVariable);
        if (string.IsNullOrWhiteSpace(rawPressure))
        {
            refusalCode = RefusedSimulatedPressureMissing;
            refusalDetail =
                $"{SimulatedPressureVariable} must carry an explicit simulated Pump-ready pressure. It is "
                + "never defaulted and no previous or stale value is reused.";
            return false;
        }

        if (!double.TryParse(rawPressure.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double bar)
            || !double.IsFinite(bar))
        {
            refusalCode = RefusedSimulatedPressureNotFinite;
            refusalDetail = $"{SimulatedPressureVariable} must be one finite number of bar.";
            return false;
        }

        configuration = new DryIvTrialConfiguration
        {
            Profile = profile,
            ModeLiteral = readEnvironment(ModeVariable)!.Trim(),
            Endpoint = endpoint,
            Consent = consent,
            PumpStoppedAttestation = attestation.Trim(),
            ValveId = ProcessIoIsolationValves.ValveId(ordinal),
            ValveOrdinal = ordinal,
            Thresholds = thresholds,
            Supervision = supervision,
            SimulatedBar = bar,
        };
        return true;
    }

    /// <summary>
    /// Runs one bounded IV trial and returns the process exit code. Nothing is retried and nothing is
    /// substituted on refusal.
    /// </summary>
    public static int Run(
        DeviceProfile profile,
        Func<string, string?> readEnvironment,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryReadConfiguration(profile, readEnvironment, out DryIvTrialConfiguration? configuration, out string code, out string detail)
            || configuration is null)
        {
            error.WriteLine($"WJSS IV trial refused [{code}]: {detail} Nothing was bound and nothing was contacted.");
            return 4;
        }

        DryIvTrialConfiguration trial = configuration;

        if (!TryLoadCatalogue(readEnvironment, out IReadOnlyDictionary<string, ProcessIoCatalogueRow>? catalogue, out code, out detail)
            || catalogue is null)
        {
            error.WriteLine($"WJSS IV trial refused [{code}]: {detail} Nothing was contacted.");
            return 4;
        }

        if (!ProcessIoEndpointConfiguration.TryValidateAdoptedBindings(catalogue, out code, out detail))
        {
            error.WriteLine($"WJSS IV trial refused [{code}]: {detail} Nothing was contacted.");
            return 4;
        }

        if (!ProcessIoDryCommandGate.TryAuthorizeIvTrial(
                trial.Profile,
                string.Equals(trial.ModeLiteral, ModeSoftwareSimulatedAi003, StringComparison.Ordinal),
                trial.Endpoint.ToString(),
                trial.Consent,
                string.Equals(trial.PumpStoppedAttestation, PumpStoppedAttestation, StringComparison.Ordinal),
                out ProcessIoWriteAuthorization? authorization,
                out string authorizationCode)
            || authorization is null)
        {
            error.WriteLine($"WJSS IV trial refused [{authorizationCode}]: the trial write gate refused. Nothing was contacted.");
            return 4;
        }

        output.WriteLine("WJSS TEST_HARDWARE_DRY IV command trial");
        output.WriteLine("  trial=" + Token);
        output.WriteLine("  surface=CONSOLE_ONLY_NO_PORT_NO_ROUTE_NO_KIOSK_CONTROL");
        output.WriteLine("  endpoint=" + ProcessIoDryCommandGate.EndpointFingerprint(trial.Endpoint.ToString()));
        output.WriteLine("  valve=" + trial.ValveId);
        output.WriteLine("  pump_ready_source=" + ProcessIoIsolationValves.PumpOutletPressureTag);
        output.WriteLine("  PUMP_READY_EVIDENCE = " + EvidenceKindSimulated);
        output.WriteLine("  PHYSICAL_PUMP_EVIDENCE = NOT_ESTABLISHED_BY_THIS_TRIAL");
        output.WriteLine("  PUMP_STOPPED: " + PumpStoppedAttestation + " / NOT SOFTWARE_VERIFIED");
        output.WriteLine("  motion=NONE_NO_HOME_NO_MOVE");
        output.WriteLine("  queue=NONE_NO_TIMEDELAY_NO_NEXT_JOB");

        var endpoints = new Dictionary<string, ProcessIoEndpointConfig>(StringComparer.Ordinal)
        {
            [ProcessIoDevices.RackT8] = new ProcessIoEndpointConfig
            {
                TargetAddress = trial.Endpoint.Host,
                Port = trial.Endpoint.Port,
                UnitId = trial.Endpoint.UnitId,
                TimeoutMs = ProcessIoTransportShape.ReadTimeoutMs,
            },
        };

        ProcessIoPhysicalTransport transport = new(endpoints);
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        var equipment = new ProcessIoEquipmentBoundary(session, catalogue, authorization);

        // Operator abort: the handler only ever de-energises. It is registered for the lifetime of the
        // one sequence and unregistered before the session closes.
        using TrialAbortScope abort = new(equipment, session);

        try
        {
            int generation = session.Open(DateTimeOffset.UtcNow);

            if (!TryCreateSimulatedPumpReady(
                    trial.SimulatedBar, DateTimeOffset.UtcNow, trial.Thresholds, generation,
                    out DryIvTrialSimulatedPumpReady? pumpReady, out code)
                || pumpReady is null)
            {
                error.WriteLine($"WJSS IV trial refused [{code}]: the simulated Pump-ready sample was not accepted.");
                return 2;
            }

            DryIvTrialResult result;
            try
            {
                result = Execute(
                    new DryIvTrialRequest
                    {
                        ValveId = trial.ValveId,
                        Thresholds = trial.Thresholds,
                        Supervision = trial.Supervision,
                        PumpReady = pumpReady,
                        OnOpenDispatched = abort.Arm,
                    },
                    session,
                    equipment);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                // The cleanup inside Execute has already run: it closes only the valves this trial
                // dispatched OPEN, and the primary fault is reported rather than masked.
                error.WriteLine($"WJSS IV trial aborted [{AbortedUnexpectedFault}]: {ex.Message}");
                error.WriteLine("  cleanup ran before this report; no other valve was commanded and no retry follows.");
                return 2;
            }

            ReportTrial(result, output);

            return result.Accepted ? 0 : (result.TimedOut ? 3 : 2);
        }
        finally
        {
            // The session closes LAST, after cleanup, and the transport is released afterwards.
            if (!string.Equals(session.State, "CLOSED", StringComparison.Ordinal))
            {
                session.Close(DateTimeOffset.UtcNow);
            }

            transport.Dispose();
        }
    }

    /// <summary>
    /// The bounded IV sequence over an already composed session and equipment boundary. It writes
    /// nothing directly: every coil write is admitted and dispatched by the session, and every
    /// conclusion comes from the existing valve observation contract.
    /// </summary>
    public static DryIvTrialResult Execute(
        DryIvTrialRequest request,
        ProcessIoSession session,
        ProcessIoEquipmentBoundary equipment)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(equipment);

        if (request.PumpReady is null || !SequencingPressure.PumpReady(request.PumpReady.Sample, request.Thresholds))
        {
            return Refused(RefusedPumpReadyNotAccepted);
        }

        if (!string.Equals(session.State, "OPEN", StringComparison.Ordinal))
        {
            return Refused(RefusedSessionNotOpen);
        }

        // The simulated sample is valid only for the generation it was minted in. A reopened session is
        // a different generation and the old sample is never re-used or re-stamped.
        if (request.PumpReady.TrialGeneration != session.Generation)
        {
            return Refused(RefusedGenerationChanged);
        }

        // Initial state is read before any command exists for this valve, so the observation can only
        // report field state. A contradictory or already-open state refuses the trial untouched.
        ProcessIoValveObservation initial = equipment.ObserveValve(request.ValveId);
        if (initial.UpperLimitDetected && initial.LowerLimitDetected)
        {
            return Refused(RefusedInitialStateContradictory) with { Initial = initial };
        }

        if (initial.UpperLimitDetected)
        {
            return Refused(RefusedInitialStateUnsafe) with { Initial = initial };
        }

        int generation = session.Generation;
        ProcessIoCleanupResult? cleanup = null;
        DryIvTrialResult result;

        try
        {
            result = RunSequence(request, equipment, initial, generation);
        }
        finally
        {
            // Cleanup runs for every outcome, including an exception, and it closes only the valves
            // this boundary actually dispatched OPEN. The session is closed by the caller, last.
            cleanup = equipment.SafeCleanupCloseAll();
        }

        return result with { Cleanup = cleanup };
    }

    /// <summary>
    /// The OPEN and CLOSE half of the sequence. It returns its own outcome; the caller attaches the
    /// cleanup record afterwards so a cleanup can never overwrite a primary failure.
    /// </summary>
    private static DryIvTrialResult RunSequence(
        DryIvTrialRequest request,
        ProcessIoEquipmentBoundary equipment,
        ProcessIoValveObservation initial,
        int generation)
    {
        equipment.RequestValve(new CleaningPathValveCommandRequest(request.ValveId, CleaningPathValveCommandKind.Open));
        ProcessIoCommandOutcome open = LastOutcome(equipment, request.ValveId);
        if (!open.Dispatched)
        {
            return Refused(AdmissionRefusal(open, RefusedOpenNotDispatched)) with { Initial = initial, OpenCommand = open };
        }

        if (open.SessionGeneration != generation)
        {
            return Refused(RefusedGenerationChanged) with { Initial = initial, OpenCommand = open };
        }

        // The valve is now energised: the caller may arm its operator-abort guard.
        request.OnOpenDispatched?.Invoke();

        ProcessIoValveObservation openObservation = Supervise(equipment, request.ValveId, request.Supervision);
        if (openObservation.AssessmentCode == FeedbackContradiction)
        {
            return Refused(RefusedOpenContradictory) with { Initial = initial, OpenCommand = open, Open = openObservation };
        }

        if (!openObservation.FieldConfirmed)
        {
            return Refused(RefusedOpenNotConfirmed) with
            {
                Initial = initial,
                OpenCommand = open,
                Open = openObservation,
                TimedOut = true,
            };
        }

        var opened = new DryIvTrialResult
        {
            Accepted = false,
            Code = OpenConfirmed,
            Initial = initial,
            OpenCommand = open,
            Open = openObservation,
        };

        equipment.RequestValve(new CleaningPathValveCommandRequest(request.ValveId, CleaningPathValveCommandKind.Close));
        ProcessIoCommandOutcome close = LastOutcome(equipment, request.ValveId);
        if (!close.Dispatched)
        {
            return opened with { Code = AdmissionRefusal(close, RefusedCloseNotDispatched), CloseCommand = close };
        }

        ProcessIoValveObservation closeObservation = Supervise(equipment, request.ValveId, request.Supervision);
        if (closeObservation.AssessmentCode == FeedbackContradiction)
        {
            return opened with { Code = RefusedCloseContradictory, CloseCommand = close, Close = closeObservation };
        }

        if (!closeObservation.FieldConfirmed)
        {
            return opened with
            {
                Code = RefusedCloseNotConfirmed,
                CloseCommand = close,
                Close = closeObservation,
                TimedOut = true,
            };
        }

        return opened with
        {
            Accepted = true,
            Code = TrialAccepted,
            CloseCommand = close,
            Close = closeObservation,
        };
    }

    /// <summary>
    /// Observes the valve until the existing contract confirms it, reports a contradiction, or the
    /// supervision window expires. The last observation is returned either way: nothing is
    /// synthesized after a timeout.
    /// </summary>
    private static ProcessIoValveObservation Supervise(
        ProcessIoEquipmentBoundary equipment,
        string valveId,
        TimeSpan supervision)
    {
        ProcessIoValveObservation observation = equipment.ObserveValve(valveId);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + supervision;
        int pollMilliseconds = (int)Math.Clamp(supervision.TotalMilliseconds / 10, 0, MaximumPollMilliseconds);

        while (!observation.FieldConfirmed
               && observation.AssessmentCode != FeedbackContradiction
               && DateTimeOffset.UtcNow < deadline)
        {
            if (pollMilliseconds > 0)
            {
                Thread.Sleep(pollMilliseconds);
            }

            observation = equipment.ObserveValve(valveId);
        }

        return observation;
    }

    /// <summary>
    /// The last command outcome recorded for exactly this valve. An outcome belonging to any other
    /// valve is never adopted: that would attribute another valve's result to this one.
    /// </summary>
    private static ProcessIoCommandOutcome LastOutcome(ProcessIoEquipmentBoundary equipment, string valveId)
    {
        IReadOnlyList<ProcessIoCommandOutcome> log = equipment.CommandLog;
        for (int index = log.Count - 1; index >= 0; index -= 1)
        {
            if (string.Equals(log[index].ValveId, valveId, StringComparison.Ordinal))
            {
                return log[index];
            }
        }

        return new ProcessIoCommandOutcome
        {
            ValveId = valveId,
            Tag = string.Empty,
            CoilAddress = -1,
            CoilValue = false,
            Dispatched = false,
            AdmissionCode = ProcessIoRefusals.SessionNotOpen,
            DispatchCode = "NOT_DISPATCHED",
            SessionGeneration = -1,
            At = DateTimeOffset.UtcNow,
        };
    }

    private static string AdmissionRefusal(ProcessIoCommandOutcome outcome, string fallback) =>
        outcome.AdmissionCode switch
        {
            ProcessIoRefusals.WriteRefusedNotAuthorized => RefusedWriteNotAuthorized,
            ProcessIoRefusals.SessionNotOpen => RefusedSessionNotOpen,
            _ => fallback,
        };

    private static DryIvTrialResult Refused(string code) => new()
    {
        Accepted = false,
        Code = code,
    };

    private static void ReportTrial(DryIvTrialResult result, TextWriter output)
    {
        output.WriteLine("  trial_code=" + result.Code);
        output.WriteLine("  accepted=" + result.Accepted.ToString());
        output.WriteLine("  evidence_kind=" + EvidenceKindSimulated);
        output.WriteLine("  trial_purpose=" + TrialPurpose);
        output.WriteLine("  physical_pump_verified=FALSE");
        output.WriteLine("  physical_ai003_verified=FALSE");
        output.WriteLine("  physical_iv_command=" + (result.OpenCommand?.Dispatched == true).ToString().ToUpperInvariant());
        output.WriteLine("  physical_upper_feedback=" + (result.Open?.UpperLimitDetected == true).ToString().ToUpperInvariant());
        output.WriteLine("  physical_lower_feedback=" + (result.Close?.LowerLimitDetected == true).ToString().ToUpperInvariant());

        if (result.OpenCommand is not null)
        {
            output.WriteLine(
                "  open_command=" + result.OpenCommand.Tag
                + " coil=" + result.OpenCommand.CoilAddress.ToString(CultureInfo.InvariantCulture)
                + " value=" + result.OpenCommand.CoilValue.ToString()
                + " admission=" + result.OpenCommand.AdmissionCode
                + " dispatch=" + result.OpenCommand.DispatchCode);
        }

        if (result.CloseCommand is not null)
        {
            output.WriteLine(
                "  close_command=" + result.CloseCommand.Tag
                + " coil=" + result.CloseCommand.CoilAddress.ToString(CultureInfo.InvariantCulture)
                + " value=" + result.CloseCommand.CoilValue.ToString()
                + " admission=" + result.CloseCommand.AdmissionCode
                + " dispatch=" + result.CloseCommand.DispatchCode);
        }

        ReportObservation("open", result.Open, output);
        ReportObservation("closed", result.Close, output);

        ProcessIoCleanupResult? cleanup = result.Cleanup;
        if (cleanup is null)
        {
            output.WriteLine("  cleanup=" + CleanupIncomplete);
            return;
        }

        output.WriteLine("  cleanup.complete=" + cleanup.Complete.ToString());
        output.WriteLine("  cleanup.attempted=" + cleanup.AttemptedValveIds.Count.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  cleanup.closed=" + cleanup.ClosedValveIds.Count.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  cleanup.refused=" + cleanup.RefusedValves.Count.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  cleanup.transport_failures=" + cleanup.TransportFailures.Count.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  cleanup.unknown=" + cleanup.RemainingUnknownState.Count.ToString(CultureInfo.InvariantCulture));
    }

    private static void ReportObservation(string stage, ProcessIoValveObservation? observation, TextWriter output)
    {
        if (observation is null)
        {
            return;
        }

        output.WriteLine(
            "  " + stage + "_observation=" + observation.AssessmentCode
            + " upper=" + observation.UpperLimitDetected.ToString()
            + " lower=" + observation.LowerLimitDetected.ToString()
            + " field_confirmed=" + observation.FieldConfirmed.ToString()
            + " fc01=" + (observation.ProcessImageReadBack?.ToString() ?? "UNAVAILABLE")
            + " pressure_bar=" + (observation.OutletPressure?.Bar?.ToString("0.####", CultureInfo.InvariantCulture) ?? "UNAVAILABLE"));
    }

    private static bool TryReadThresholds(
        Func<string, string?> readEnvironment,
        out SequencingPressureThresholds? thresholds,
        out string refusalCode,
        out string refusalDetail)
    {
        thresholds = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        if (!TryReadDouble(readEnvironment, CleaningPathDryComposition.PressureLowVariable, out double low)
            || !TryReadDouble(readEnvironment, CleaningPathDryComposition.PressureHighVariable, out double high)
            || !TryReadDouble(readEnvironment, CleaningPathDryComposition.PumpReadySetpointVariable, out double setpoint))
        {
            refusalCode = RefusedThresholds;
            refusalDetail =
                $"The IV trial requires finite Owner-local values in {CleaningPathDryComposition.PressureLowVariable}, "
                + $"{CleaningPathDryComposition.PressureHighVariable} and "
                + $"{CleaningPathDryComposition.PumpReadySetpointVariable}. They are never defaulted.";
            return false;
        }

        var candidate = new SequencingPressureThresholds(low, high, setpoint);
        if (!candidate.Valid)
        {
            refusalCode = SequencingPressureThresholds.InvalidCode;
            refusalDetail =
                "The Owner-local pressure thresholds are not valid: Low must be at least zero, High must "
                + "exceed Low, and the Pump-ready setpoint must be at least zero.";
            return false;
        }

        thresholds = candidate;
        return true;
    }

    private static bool TryReadSupervision(
        Func<string, string?> readEnvironment,
        out TimeSpan supervision,
        out string refusalCode,
        out string refusalDetail)
    {
        supervision = TimeSpan.Zero;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(CleaningPathDryComposition.ValveSupervisionVariable);
        if (string.IsNullOrWhiteSpace(raw)
            || !int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int milliseconds)
            || milliseconds is < 1 or > 600000)
        {
            refusalCode = RefusedSupervision;
            refusalDetail = $"{CleaningPathDryComposition.ValveSupervisionVariable} must be an integer between "
                + "1 and 600000 milliseconds.";
            return false;
        }

        supervision = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    private static bool TryReadDouble(Func<string, string?> readEnvironment, string variable, out double value)
    {
        value = 0;
        string? raw = readEnvironment(variable);
        return !string.IsNullOrWhiteSpace(raw)
               && double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value);
    }

    private static bool TryLoadCatalogue(
        Func<string, string?> readEnvironment,
        out IReadOnlyDictionary<string, ProcessIoCatalogueRow>? catalogue,
        out string refusalCode,
        out string refusalDetail)
    {
        catalogue = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? location = readEnvironment(CleaningPathDryComposition.CatalogueVariable);
        if (string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location))
        {
            refusalCode = RefusedCatalogue;
            refusalDetail =
                $"{CleaningPathDryComposition.CatalogueVariable} must name one fully qualified Owner-local "
                + "catalogue file.";
            return false;
        }

        try
        {
            var info = new FileInfo(location);
            if (!info.Exists || info.Length > 1048576)
            {
                refusalCode = RefusedCatalogue;
                refusalDetail = "The Owner-local catalogue file is missing or larger than the accepted one megabyte.";
                return false;
            }

            IReadOnlyList<ProcessIoCatalogueRow> rows = ProcessIoCatalogueLoader.Load(File.ReadAllText(location));
            var index = new Dictionary<string, ProcessIoCatalogueRow>(StringComparer.Ordinal);
            foreach (ProcessIoCatalogueRow row in rows)
            {
                index[row.Tag] = row;
            }

            catalogue = index;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            refusalCode = RefusedCatalogue;
            refusalDetail = "The Owner-local catalogue was refused: " + ex.Message;
            return false;
        }
    }
}

/// <summary>
/// The bounded operator-abort guard for one IV trial.
///
/// A maintained command coil stays energised while the process lives, so an operator abort must
/// de-energise exactly the valve this trial opened. The guard is armed only after the OPEN dispatch is
/// observed to have happened, and it never opens anything: a duplicate CLOSE of the same coil is a
/// safe-direction repeat, never a new command. It is unregistered before the session closes.
/// </summary>
internal sealed class TrialAbortScope : IDisposable
{
    private readonly ProcessIoEquipmentBoundary _equipment;
    private readonly ProcessIoSession _session;
    private readonly ConsoleCancelEventHandler _handler;
    private int _armed;
    private int _disposed;

    public TrialAbortScope(ProcessIoEquipmentBoundary equipment, ProcessIoSession session)
    {
        _equipment = equipment;
        _session = session;
        _handler = (_, eventArgs) =>
        {
            // The operator's abort is honoured, never suppressed by the runtime.
            eventArgs.Cancel = true;
            Abort();
            Environment.Exit(2);
        };

        Console.CancelKeyPress += _handler;
    }

    /// <summary>Arms the guard. Called only once the trial has actually opened its valve.</summary>
    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    private void Abort()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        Console.Error.WriteLine("WJSS IV trial abort: " + DryIvTrial.AbortedOperator);

        if (Interlocked.CompareExchange(ref _armed, 0, 1) == 1)
        {
            ProcessIoCleanupResult cleanup = _equipment.SafeCleanupCloseAll();
            Console.Error.WriteLine(
                "  cleanup.complete=" + cleanup.Complete.ToString()
                + " attempted=" + cleanup.ClosedValveIds.Count.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.Equals(_session.State, "CLOSED", StringComparison.Ordinal))
        {
            _session.Close(DateTimeOffset.UtcNow);
        }
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= _handler;
        Interlocked.Exchange(ref _disposed, 1);
    }
}

/// <summary>
/// One validated IV-trial configuration. Every member came from an Owner-local value; nothing is
/// defaulted and nothing is inferred.
/// </summary>
public sealed record DryIvTrialConfiguration
{
    public required DeviceProfile Profile { get; init; }

    /// <summary>The validated trial-mode literal, carried so the write gate re-proves it.</summary>
    public required string ModeLiteral { get; init; }

    public required ProcessIoEndpointHandle Endpoint { get; init; }

    public required string Consent { get; init; }

    public required string PumpStoppedAttestation { get; init; }

    public required string ValveId { get; init; }

    public required int ValveOrdinal { get; init; }

    public required SequencingPressureThresholds Thresholds { get; init; }

    public required TimeSpan Supervision { get; init; }

    public required double SimulatedBar { get; init; }
}

/// <summary>
/// The trial-only Pump-ready sample. It carries the exact Product source identity the Pump-ready rule
/// requires, plus the provenance that marks it simulated. It is never serialized and never leaves
/// this process.
/// </summary>
public sealed record DryIvTrialSimulatedPumpReady
{
    public required PressureSample Sample { get; init; }

    public required string SourceTag { get; init; }

    public required double SimulatedBar { get; init; }

    public required string EvidenceKind { get; init; }

    public required string Provenance { get; init; }

    public required string TrialPurpose { get; init; }

    public required int TrialGeneration { get; init; }
}

/// <summary>One bounded IV-trial request over an already composed session and equipment boundary.</summary>
public sealed record DryIvTrialRequest
{
    public required string ValveId { get; init; }

    public required SequencingPressureThresholds Thresholds { get; init; }

    public required TimeSpan Supervision { get; init; }

    public required DryIvTrialSimulatedPumpReady PumpReady { get; init; }

    /// <summary>
    /// Invoked once, immediately after the OPEN command is observed to be dispatched. The console trial
    /// uses it to arm the operator-abort guard; tests leave it null.
    /// </summary>
    public Action? OnOpenDispatched { get; init; }
}

/// <summary>
/// The typed trial result. Every evidence class stays in its own member so a reader can never
/// collapse a simulated Pump-ready input into a physical one.
/// </summary>
public sealed record DryIvTrialResult
{
    public required bool Accepted { get; init; }

    public required string Code { get; init; }

    /// <summary>True only when a supervision window expired without a decision.</summary>
    public bool TimedOut { get; init; }

    public ProcessIoValveObservation? Initial { get; init; }

    public ProcessIoCommandOutcome? OpenCommand { get; init; }

    public ProcessIoValveObservation? Open { get; init; }

    public ProcessIoCommandOutcome? CloseCommand { get; init; }

    public ProcessIoValveObservation? Close { get; init; }

    public ProcessIoCleanupResult? Cleanup { get; init; }
}
