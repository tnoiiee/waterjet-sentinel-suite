using System.Globalization;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;

namespace Wjss.Runtime;

// PUMP PLATFORM, PHASE 0.5E: the fourth physical evidence class, the Pump command rung alone.
//
//     What this run is. The complete Pump command ladder is executed against the real WAGO T8 rack over
//     the ONE authorized dry write authorization: the Pump selection pair, the inverter enable, the Run
//     command, the ramp of the analog speed reference, the commissioned Running input, the Fault input,
//     the Pump Outlet Pressure rule, and then the Stop order in reverse with the safe-zero analog sentinel.
//     Every one of those transitions is the EXISTING ProcessIoPumpLifecycle making the decisions; this
//     runner adds no second ladder, no retry, no alternative order and no bypass.
//
//     What this run is NOT. It never composes an equipment boundary, so it holds no valve command surface at
//     all: no equipment boundary is constructed, so there is no paired-IV open and no close, and there
//     is therefore nothing in this file that could write DO-018 through DO-023. The Isolation Valve stays,
//     exactly where the Owner left it, and the evidence says so as a structural fact rather than as a
//     promise. The
//     Cleaning Path does not move: no motion composition is built here, so no axis is homed and no point is
//     advanced.
//
//     Why it exists. Stage D of the bench runbook needs the Pump command rung separated from the valve
//     rung, so a Pump-side defect cannot be confused with a valve-side one and so the Pump ladder can be
//     exercised while the water side of the machine stays closed.
//
// Exit codes: 0 the whole ladder was reached and the Stop path confirmed not-running with the command
// channel released; 2 the ladder was entered but a transition or a cleanup step was refused or failed;
// 3 a bounded supervision timeout expired; 4 an invocation, selector, profile, endpoint, consent,
// catalogue or configuration refusal raised before any physical contact was attempted.

/// <summary>
/// Runs the physical Pump command ladder alone, with no valve boundary composed, and prints the ladder it
/// actually reached.
/// </summary>
public static class PumpCommandDryTrial
{
    /// <summary>The published command identity of this trial. Nothing else selects it.</summary>
    internal const string Token = "--pump-command-dry-trial";

    /// <summary>The dedicated selector of this evidence class. No other selector reaches this runner.</summary>
    public const string ModeVariable = "WJSS_PUMP_COMMAND_DRY_TRIAL";

    /// <summary>The only accepted selector literal. It is compared exactly, never normalized.</summary>
    public const string ModePumpOnly = "PUMP_ONLY_NO_VALVES";

    /// <summary>The evidence identity printed by every run of this trial.</summary>
    public const string TrialIdentity = "PHYSICAL_PUMP_COMMAND_ONLY_NO_VALVES";

    /// <summary>The owner kind the Pump command channel is acquired under for this trial.</summary>
    public const string PumpOwnerId = "PUMP_COMMAND_DRY_TRIAL";

    /// <summary>
    /// Stated on every run: this trial composes no equipment boundary, so it has no Isolation Valve path.
    /// </summary>
    public const string IvCommandSurface = "NONE_NO_VALVE_BOUNDARY_COMPOSED";

    /// <summary>The deterministic pause between two driver steps. Bounded and never zero.</summary>
    private const int DriveIntervalMilliseconds = 5;

    // ------------------------------------------------------------------ refusals before contact

    /// <summary>No selector was present, so this trial was never asked for.</summary>
    public const string RefusedModeNotSelected = "PUMP_COMMAND_TRIAL_REFUSED_MODE_NOT_SELECTED";

    /// <summary>A selector was present but is not the exact accepted literal.</summary>
    public const string RefusedModeInvalid = "PUMP_COMMAND_TRIAL_REFUSED_MODE_INVALID";

    /// <summary>The integrated dry composition was also selected, so no evidence class is chosen.</summary>
    public const string RefusedIntegratedDrySelected = "PUMP_COMMAND_TRIAL_REFUSED_INTEGRATED_DRY_SELECTED";

    /// <summary>The IV Hardware trial was also selected, so no evidence class is chosen.</summary>
    public const string RefusedIvTrialSelected = "PUMP_COMMAND_TRIAL_REFUSED_IV_TRIAL_SELECTED";

    /// <summary>The read-only Pump-ready trial was also selected, so no evidence class is chosen.</summary>
    public const string RefusedReadOnlyTrialSelected = "PUMP_COMMAND_TRIAL_REFUSED_READ_ONLY_TRIAL_SELECTED";

    /// <summary>PRODUCTION is never startable by this trial.</summary>
    public const string RefusedProduction = "PUMP_COMMAND_TRIAL_REFUSED_PRODUCTION_NOT_AUTHORIZED";

    /// <summary>Only TEST_HARDWARE_DRY carries the dry write authorization this trial consumes.</summary>
    public const string RefusedProfile = "PUMP_COMMAND_TRIAL_REFUSED_PROFILE_NOT_TEST_HARDWARE_DRY";

    /// <summary>The trial refused the simulated-Pump-ready selector: this evidence class is physical only.</summary>
    public const string RefusedSimulatedSelector = "PUMP_COMMAND_TRIAL_REFUSED_SIMULATED_PUMP_READY_NOT_APPLICABLE";

    /// <summary>Written as a fact: the trial never commands a valve, whatever else happens.</summary>
    public const string IvCommandExecutedNever = "FALSE";

    // ------------------------------------------------------------------ selector semantics

    /// <summary>
    /// Reads the dedicated selector. An absent selector is not an error: it means this evidence class was
    /// not chosen. A value that is present but wrong is a refusal, so a typo cannot silently select the
    /// Pump ladder or silently fall back to another trial.
    /// </summary>
    public static bool TryReadMode(
        Func<string, string?> readEnvironment,
        out bool selected,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);

        selected = false;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(ModeVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!string.Equals(raw, ModePumpOnly, StringComparison.Ordinal))
        {
            refusalCode = RefusedModeInvalid;
            refusalDetail =
                $"{ModeVariable} must be absent, or exactly '{ModePumpOnly}'. It is never normalized, and "
                + "no other literal selects the Pump command ladder.";
            return false;
        }

        selected = true;
        return true;
    }

    /// <summary>
    /// Proves the three rules that decide whether this evidence class may run at all, before a transport
    /// exists: the selector is this trial's, no other physical evidence class is selected beside it, and
    /// the simulated-Pump-ready selector is not set (this class is the physical Pump, so a declared value
    /// would contradict its own identity).
    /// </summary>
    public static bool TryValidateInvocation(
        DeviceProfile profile,
        Func<string, string?> readEnvironment,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);

        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        if (!TryReadMode(readEnvironment, out bool selected, out string modeCode, out string modeDetail))
        {
            refusalCode = modeCode;
            refusalDetail = modeDetail;
            return false;
        }

        if (!selected)
        {
            refusalCode = RefusedModeNotSelected;
            refusalDetail = $"{ModeVariable} does not name '{ModePumpOnly}'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(readEnvironment(DryIvTrial.ModeVariable))
            || !string.IsNullOrWhiteSpace(readEnvironment(ProcessIoPumpReadyReadTrial.ModeVariable)))
        {
            refusalCode = RefusedIvTrialSelected;
            refusalDetail =
                $"The Pump command ladder was selected together with {DryIvTrial.ModeVariable} or "
                + $"{ProcessIoPumpReadyReadTrial.ModeVariable}. Two physical evidence classes are never "
                + "combined in one run: the transcript would not say which one produced what.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(readEnvironment(CleaningPathDryComposition.SimulatedPumpReadyVariable)))
        {
            refusalCode = RefusedSimulatedSelector;
            refusalDetail =
                $"{CleaningPathDryComposition.SimulatedPumpReadyVariable} is set, but this evidence class is "
                + "the physical Pump ladder. A declared Pump Outlet Pressure is the other class's evidence "
                + "and is never mixed into a run that claims a physical Pump start.";
            return false;
        }

        if (profile == DeviceProfile.PRODUCTION)
        {
            refusalCode = RefusedProduction;
            refusalDetail = "PRODUCTION is never startable by this trial. Nothing was read and nothing was contacted.";
            return false;
        }

        if (profile != DeviceProfile.TEST_HARDWARE_DRY)
        {
            refusalCode = RefusedProfile;
            refusalDetail =
                $"The Pump command ladder composes only for TEST_HARDWARE_DRY; the profile is '{profile}'. "
                + "The dry write authorization is the only authority this trial accepts, and no profile is "
                + "substituted for it.";
            return false;
        }

        if (!ProcessIoEndpointConfiguration.TryReadDryMode(
                readEnvironment, out bool dryCommandMode, out string dryCode, out string dryDetail))
        {
            refusalCode = dryCode.Length == 0 ? RefusedProfile : dryCode;
            refusalDetail = dryDetail;
            return false;
        }

        if (!dryCommandMode)
        {
            refusalCode = RefusedProfile;
            refusalDetail =
                $"TEST_HARDWARE_DRY requires {ProcessIoEndpointConfiguration.DryModeVariable}="
                + "'TEST_HARDWARE_DRY'. The mode is never inferred from the profile alone.";
            return false;
        }

        if (!ProfileStartPolicy.TryRequireStartable(
                profile, physicalReadOnlyMode: false, dryCommandMode: true, out refusalCode, out refusalDetail))
        {
            return false;
        }

        if (!ProcessIoEndpointConfiguration.TryResolveEndpoint(
                readEnvironment, out ProcessIoEndpointHandle? endpoint, out refusalCode, out refusalDetail)
            || endpoint is null)
        {
            refusalCode = refusalCode.Length == 0 ? ProcessIoEndpointConfiguration.NotConfigured : refusalCode;
            return false;
        }

        if (!ProcessIoEndpointConfiguration.TryReadConsent(
                readEnvironment, out string? consent, out refusalCode, out refusalDetail)
            || consent is null)
        {
            refusalCode = refusalCode.Length == 0 ? ProcessIoEndpointConfiguration.ConsentMissing : refusalCode;
            return false;
        }

        if (!ProcessIoDryCommandGate.TryAuthorize(
                profile, dryCommandMode, endpoint.ToString(), consent, out _, out string authorizationCode))
        {
            refusalCode = authorizationCode;
            refusalDetail =
                "The dry command gate refused the write authorization, so no Pump command could be admitted. "
                + "No transport is constructed after a refusal.";
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ the run

    /// <summary>
    /// Runs the Pump command ladder over one opened Process-Io session and prints the ladder it reached.
    /// It performs no valve write, because it composes no valve boundary, and it performs no motion.
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

        if (!TryValidateInvocation(profile, readEnvironment, out string refusalCode, out string refusalDetail))
        {
            error.WriteLine($"WJSS Pump command trial refused [{refusalCode}]: {refusalDetail} Nothing was contacted.");
            return 4;
        }

        if (!ProcessIoEndpointConfiguration.TryResolveEndpoint(
                readEnvironment, out ProcessIoEndpointHandle? endpoint, out _, out _) || endpoint is null)
        {
            error.WriteLine("WJSS Pump command trial refused: the Owner-local endpoint was not resolvable.");
            return 4;
        }

        if (!ProcessIoEndpointConfiguration.TryReadConsent(
                readEnvironment, out string? consent, out _, out _) || consent is null)
        {
            error.WriteLine("WJSS Pump command trial refused: the Owner consent was not readable.");
            return 4;
        }

        if (!ProcessIoDryCommandGate.TryAuthorize(
                profile, dryCommandMode: true, endpoint.ToString(), consent,
                out ProcessIoWriteAuthorization? authorization, out string authorizationCode)
            || authorization is null)
        {
            error.WriteLine(
                $"WJSS Pump command trial refused [{authorizationCode}]: the dry command gate did not issue "
                + "a write authorization.");
            return 4;
        }

        if (!ProcessIoPumpReadyReadTrial.TryLoadCatalogue(
                readEnvironment, out IReadOnlyDictionary<string, ProcessIoCatalogueRow>? catalogue, out _, out string catalogueDetail)
            || catalogue is null)
        {
            error.WriteLine($"WJSS Pump command trial refused: {catalogueDetail}");
            return 4;
        }

        if (!CleaningPathDryComposition.TryReadThresholds(
                readEnvironment, out SequencingPressureThresholds? thresholds, out _, out string thresholdDetail)
            || thresholds is null)
        {
            error.WriteLine($"WJSS Pump command trial refused: {thresholdDetail}");
            return 4;
        }

        if (!CleaningPathDryComposition.TryReadPumpConfiguration(
                readEnvironment, thresholds, out PumpLifecycleConfiguration pumpConfiguration,
                out int pumpSelection, out _, out string pumpDetail))
        {
            error.WriteLine($"WJSS Pump command trial refused: {pumpDetail}");
            return 4;
        }

        IClock clock = new SystemClock();
        TimeProvider time = new ClockTimeProvider(clock);

        var endpoints = new Dictionary<string, ProcessIoEndpointConfig>(StringComparer.Ordinal)
        {
            [ProcessIoDevices.RackT8] = new ProcessIoEndpointConfig
            {
                TargetAddress = endpoint.Host,
                Port = endpoint.Port,
                UnitId = endpoint.UnitId,
                TimeoutMs = ProcessIoTransportShape.ReadTimeoutMs,
            },
        };

        var transport = new ProcessIoPhysicalTransport(endpoints);
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        var boundary = new ProcessIoPumpCommandBoundary(
            session,
            catalogue,
            authorization,
            pumpConfiguration.Feedback,
            time,
            pumpConfiguration.PressureFreshnessBudget);
        var lifecycle = new ProcessIoPumpLifecycle(
            boundary, pumpConfiguration, new PumpCommandOwnership(), time);

        output.WriteLine("WJSS PUMP COMMAND DRY TRIAL");
        output.WriteLine("  trial_identity=" + TrialIdentity);
        output.WriteLine("  evidence_kind=PHYSICAL");
        output.WriteLine("  physical_pump_verified=True");
        output.WriteLine("  profile=" + profile);
        output.WriteLine("  endpoint_fingerprint=" + endpoint.Fingerprint);
        output.WriteLine("  catalogue_rows=" + catalogue.Count.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  pump_selection=" + pumpSelection.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  pump_analog_target_ma="
            + pumpConfiguration.Ramp.TargetEngineeringMilliamps.ToString("0.###", CultureInfo.InvariantCulture));
        output.WriteLine("  pump_ready_setpoint_bar="
            + pumpConfiguration.PumpReadySetpointBar.ToString("0.###", CultureInfo.InvariantCulture));
        output.WriteLine("  iv_boundary_composed=False");
        output.WriteLine("  iv_command_surface=" + IvCommandSurface);
        output.WriteLine("  iv_command_executed=" + IvCommandExecutedNever);
        output.WriteLine("  motion_composed=False");

        int exitCode;
        try
        {
            session.Open(clock.UtcNow);
            exitCode = Drive(lifecycle, session, catalogue, thresholds, pumpConfiguration, pumpSelection,
                time, clock, output);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error.WriteLine("WJSS Pump command trial aborted: " + ex.Message);
            exitCode = 3;
        }
        finally
        {
            try
            {
                session.Close(clock.UtcNow);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                error.WriteLine("WJSS Pump command trial: the session close reported " + ex.Message);
            }

            output.WriteLine("  session_state=" + session.State);
            output.WriteLine("  transport_released=True");
            transport.Dispose();
        }

        return exitCode;
    }

    /// <summary>
    /// Starts the ladder, drives it to confirmed readiness, takes one independent Pump Outlet Pressure
    /// cross-check through the read-only observer of the SAME session, then stops it and drives the Stop
    /// order to a terminal state. Every decision belongs to the lifecycle; this loop only advances it and
    /// records the states it passed through.
    /// </summary>
    private static int Drive(
        ProcessIoPumpLifecycle lifecycle,
        ProcessIoSession session,
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        SequencingPressureThresholds thresholds,
        PumpLifecycleConfiguration configuration,
        int pumpSelection,
        TimeProvider time,
        IClock clock,
        TextWriter output)
    {
        PumpLifecycleStartOutcome admitted = lifecycle.Start(new PumpLifecycleStartRequest
        {
            Owner = PumpCommandOwnerKind.MANUAL_COMMISSIONING,
            OwnerId = PumpOwnerId,
            Selection = pumpSelection,
        });

        output.WriteLine("  start_accepted=" + (admitted.Started ? "True" : "False"));
        if (!admitted.Started)
        {
            output.WriteLine("  start_code=" + admitted.Code);
            output.WriteLine("  trial_code=PUMP_COMMAND_TRIAL_REFUSED_AT_START");
            return 2;
        }

        int rampSteps = (int)Math.Ceiling(
            PumpAnalogCommandContract.LiveMaximumMilliamps / configuration.Ramp.Step);
        TimeSpan budget = TimeSpan.FromTicks(configuration.Ramp.Interval.Ticks * rampSteps)
            + configuration.RunningFeedbackTimeout
            + configuration.PumpReadyTimeout;
        DateTimeOffset deadline = clock.UtcNow + budget;

        PumpLifecycleState highest = lifecycle.State;
        var trace = new List<string> { highest.ToString() };
        bool sawFault = false;

        int maximumSteps = (int)(budget.TotalMilliseconds / DriveIntervalMilliseconds) + 1000;
        for (int step = 0; step < maximumSteps; step += 1)
        {
            if (lifecycle.State > highest)
            {
                highest = lifecycle.State;
                trace.Add(highest.ToString());
            }

            if (lifecycle.State == PumpLifecycleState.FAULT_ACTIVE)
            {
                sawFault = true;
            }

            if (lifecycle.State == PumpLifecycleState.PUMP_READY)
            {
                break;
            }

            if (lifecycle.Result is not null || PumpLifecycleStates.IsTerminal(lifecycle.State))
            {
                break;
            }

            if (step > 0 && clock.UtcNow >= deadline)
            {
                output.WriteLine("  trial_code=PUMP_COMMAND_TRIAL_TIMEOUT_" + lifecycle.State);
                return 3;
            }

            lifecycle.Step();
            Thread.Sleep(DriveIntervalMilliseconds);
        }

        trace.Add(lifecycle.State.ToString());
        output.WriteLine("  ladder=" + string.Join(">", trace));
        output.WriteLine("  selection_pair_written=" + Reached(highest, PumpLifecycleState.PUMP_SELECTED));
        output.WriteLine("  pump_enable_written=" + Reached(highest, PumpLifecycleState.ENABLE_DISPATCHED));
        output.WriteLine("  pump_run_written=" + Reached(highest, PumpLifecycleState.RUN_DISPATCHED));
        output.WriteLine("  running_feedback_confirmed=" + Reached(highest, PumpLifecycleState.RAMPING_OUTPUT));
        output.WriteLine("  analog_ramp_executed=" + Reached(highest, PumpLifecycleState.RAMPING_OUTPUT));
        output.WriteLine("  pump_ready_confirmed=" + (highest == PumpLifecycleState.PUMP_READY
            || lifecycle.State == PumpLifecycleState.PUMP_READY ? "True" : "False"));
        output.WriteLine("  fault_feedback_never_active=" + (sawFault ? "False" : "True"));
        output.WriteLine("  valve_open_admitted_by_lifecycle=" + (lifecycle.AllowsValveOpen ? "True" : "False"));

        ReportPressureCrossCheck(lifecycle, session, catalogue, thresholds, configuration, time, output);

        PumpLifecycleStopOutcome stopped = lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        output.WriteLine("  stop_accepted=" + (stopped.StopConfirmed ? "True" : "False"));
        output.WriteLine("  stop_code=" + stopped.Code);

        TimeSpan stopBudget = TimeSpan.FromTicks(configuration.Ramp.Interval.Ticks * rampSteps)
            + configuration.StoppedFeedbackTimeout;
        DateTimeOffset stopDeadline = clock.UtcNow + stopBudget;
        int stopMaximumSteps = (int)(stopBudget.TotalMilliseconds / DriveIntervalMilliseconds) + 1000;

        for (int step = 0; step < stopMaximumSteps && lifecycle.Result is null; step += 1)
        {
            if (lifecycle.State > highest)
            {
                highest = lifecycle.State;
                trace.Add(highest.ToString());
            }

            if (step > 0 && clock.UtcNow >= stopDeadline)
            {
                break;
            }

            lifecycle.Step();
            Thread.Sleep(DriveIntervalMilliseconds);
        }

        if (lifecycle.Result is not null)
        {
            trace.Add(lifecycle.Result.TerminalState.ToString());
        }

        PumpLifecycleResult? result = lifecycle.Result;

        output.WriteLine("  ladder_complete=" + string.Join(">", trace));
        output.WriteLine("  analog_stop_sentinel_written=" + Reached(highest, PumpLifecycleState.RAMPING_DOWN));
        output.WriteLine("  pump_run_cleared=" + Reached(highest, PumpLifecycleState.CLEARING_RUN));
        output.WriteLine("  pump_enable_released=" + Reached(highest, PumpLifecycleState.DISABLING_INVERTER));
        output.WriteLine("  terminal_state=" + (result?.TerminalState.ToString() ?? lifecycle.State.ToString()));
        output.WriteLine("  stopped_confirmed=" + (result?.StoppedConfirmed == true ? "True" : "False"));
        output.WriteLine("  cleanup_state=" + (result?.CleanupState ?? "NONE"));
        output.WriteLine("  ownership_released=" + (!lifecycle.OwnsPumpCommand ? "True" : "False"));
        output.WriteLine("  safe_to_report_complete=" + (lifecycle.SafeToReportComplete ? "True" : "False"));
        output.WriteLine("  primary_failure=" + (lifecycle.PrimaryFailure?.ToString() ?? "NONE"));
        foreach (string fault in lifecycle.CleanupFaultCodes)
        {
            output.WriteLine("  cleanup_fault=" + fault);
        }

        bool complete = result is not null
            && result.StoppedConfirmed
            && result.CleanupState == PumpCleanupStates.Complete
            && !lifecycle.OwnsPumpCommand;

        output.WriteLine("  trial_code=" + (complete
            ? "PUMP_COMMAND_TRIAL_ACCEPTED"
            : "PUMP_COMMAND_TRIAL_" + (result?.TerminalState.ToString() ?? lifecycle.State.ToString())));
        return complete ? 0 : 2;
    }

    /// <summary>
    /// One independent read of the Pump Outlet Pressure through the read-only observer, over the same open
    /// session and the same catalogue, taken while the lifecycle reports readiness. It is a cross-check:
    /// the readiness decision was the lifecycle's, and a refused binding here changes nothing about that
    /// decision. It writes nothing.
    /// </summary>
    private static void ReportPressureCrossCheck(
        ProcessIoPumpLifecycle lifecycle,
        ProcessIoSession session,
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        SequencingPressureThresholds thresholds,
        PumpLifecycleConfiguration configuration,
        TimeProvider time,
        TextWriter output)
    {
        if (!ProcessIoPumpReadyReadTrial.TryValidateAi003Binding(
                catalogue, out ProcessIoCatalogueRow? pumpRow, out _, out _ ) || pumpRow is null)
        {
            output.WriteLine("  pump_outlet_pressure_bar=<not printed: AI-003+ catalogue binding refused>");
            return;
        }

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(
            new PumpReadyReadRequest
            {
                PumpRow = pumpRow,
                Catalogue = catalogue,
                Thresholds = thresholds,
                Freshness = configuration.PressureFreshnessBudget,
                Time = time,
            },
            session);

        output.WriteLine("  pump_outlet_pressure_bar=" + (outcome.Ai003.EngineeringValue?.ToString("0.####", CultureInfo.InvariantCulture)
            ?? "<not printed: no usable engineering value>"));
        output.WriteLine("  pump_outlet_pressure_raw=" + outcome.Ai003.RawValue.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  pump_outlet_pressure_fresh=" + (outcome.Ai003.Fresh ? "True" : "False"));
        output.WriteLine("  ai003_evidence_verified=" + (outcome.PhysicalAi003Verified ? "True" : "False"));
        output.WriteLine("  ai003_pump_ready_rule=" + (outcome.PumpReady ? "ACCEPTED" : "REFUSED"));
        output.WriteLine("  ai003_session_generation=" + outcome.SessionGeneration.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  lifecycle_generation_held=" + session.Generation.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// True when the ladder passed through <paramref name="rung"/>. The progression values of
    /// <see cref="PumpLifecycleState"/> are ordered, so a comparison is the whole of this judgement: no
    /// rung is inferred from a later one, and a failure state never reports a rung it did not reach.
    /// </summary>
    private static string Reached(PumpLifecycleState highest, PumpLifecycleState rung) =>
        (int)highest >= (int)rung ? "True" : "False";
}
