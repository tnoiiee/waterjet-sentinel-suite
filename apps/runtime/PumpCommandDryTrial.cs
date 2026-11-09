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

    /// <summary>
    /// The deterministic pause between two driver steps. Bounded and never zero. One hundred ms is ten
    /// times finer than the only thing this loop waits on - the ramp is gated by wall clock inside the
    /// lifecycle at PumpSpeedRampConfiguration.OwnerInitialIntervalMilliseconds = 1000 ms
    /// (apps/runtime/CleaningPathDryComposition.cs:1227), and StepWaitingForRunning re-reads the Running
    /// input on its own schedule - so a faster poll adds nothing to the judgement and every extra read is
    /// one more request on the same coupler the writes use. Nothing can be missed at this pace either: the
    /// ladder only moves forward, and PUMP_READY and the terminal states are sticky once entered.
    /// </summary>
    private const int DriveIntervalMilliseconds = 100;

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

        // Declared with var on purpose: the clock and the time provider are the concrete types the trials
        // always use, and naming an interface here only adds a devirtualisation cost the analyzers report as
        // CA1859 under this repo's warnings-as-errors build.
        var clock = new SystemClock();
        var time = new ClockTimeProvider(clock);

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
            exitCode = Drive(lifecycle, boundary, transport, session, catalogue, thresholds,
                pumpConfiguration, pumpSelection, time, clock, output);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error.WriteLine("WJSS Pump command trial aborted: " + ex.Message);
            exitCode = 3;
        }
        finally
        {
            // The close is guarded the same way the read-only trial guards it: a session that never opened
            // is not "closed" by this trial, and the claim is printed only after the release has actually
            // happened, so the record states a fact rather than an intention.
            if (!string.Equals(session.State, "CLOSED", StringComparison.Ordinal))
            {
                try
                {
                    session.Close(clock.UtcNow);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    error.WriteLine("WJSS Pump command trial: the session close reported " + ex.Message);
                }
            }

            transport.Dispose();
            output.WriteLine("  session_state=" + session.State);
            output.WriteLine("  transport_released=True");
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
        ProcessIoPumpCommandBoundary boundary,
        ProcessIoPhysicalTransport transport,
        ProcessIoSession session,
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        SequencingPressureThresholds thresholds,
        PumpLifecycleConfiguration configuration,
        int pumpSelection,
        TimeProvider time,
        // The concrete clock, not IClock, on purpose: this is the trial's own private helper and it is
        // called exactly once, with the SystemClock created above. Declaring an interface here would only
        // add a virtual dispatch per UtcNow read, and under this repo's warnings-as-errors build CA1859
        // reports it. Everything the ladder actually needs from the clock is UtcNow.
        SystemClock clock,
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

        // A rung is claimed only when the state itself was observed. Ordinals are never compared for this:
        // PumpLifecycleStates.IsFailure is ordinal 20 and up, so the failure values sit ABOVE the progression
        // values, and a ladder that timed out waiting for running feedback would otherwise report
        // RAMPING_OUTPUT as reached. The rungs crossed inside Start() cannot be observed by this loop at all,
        // so every coil claim is taken from the boundary's own dispatch log instead of from a state name.
        var visited = new HashSet<PumpLifecycleState> { lifecycle.State };
        var trace = new List<string> { lifecycle.State.ToString() };
        bool sawFault = false;

        int maximumSteps = (int)(budget.TotalMilliseconds / DriveIntervalMilliseconds) + 1000;
        for (int step = 0; step < maximumSteps; step += 1)
        {
            if (visited.Add(lifecycle.State))
            {
                trace.Add(lifecycle.State.ToString());
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
                trace.Add(lifecycle.State.ToString());
                output.WriteLine("  ladder=" + string.Join(">", trace));
                output.WriteLine("  trial_code=PUMP_COMMAND_TRIAL_TIMEOUT_" + lifecycle.State);
                return 3;
            }

            lifecycle.Step();
            Thread.Sleep(DriveIntervalMilliseconds);
        }

        PumpLifecycleResult? ended = lifecycle.Result;
        if (ended is not null && visited.Add(ended.TerminalState))
        {
            trace.Add(ended.TerminalState.ToString());
        }

        IReadOnlyList<PumpCommandOutcome> log = boundary.CommandLog;

        bool Written(string tag, bool value) => log.Any(o => o.Succeeded
            && string.Equals(o.Tag, tag, StringComparison.Ordinal)
            && o.Value == value);

        string Reached(PumpLifecycleState rung) => visited.Contains(rung) ? "True" : "False";

        output.WriteLine("  ladder=" + string.Join(">", trace));
        output.WriteLine("  selection_pair_written=" + (Written(PumpCommandContract.SelectPumpOneTag, true)
            && Written(PumpCommandContract.SelectPumpTwoTag, false) ? "True" : "False"));
        output.WriteLine("  pump_enable_written=" + (Written(PumpCommandContract.EnableInverterTag, true) ? "True" : "False"));
        output.WriteLine("  pump_run_written=" + (Written(PumpCommandContract.RunPumpTag, true) ? "True" : "False"));
        output.WriteLine("  running_feedback_confirmed=" + Reached(PumpLifecycleState.RAMPING_OUTPUT));
        output.WriteLine("  analog_ramp_executed=" + Reached(PumpLifecycleState.RAMPING_OUTPUT));
        output.WriteLine("  pump_ready_confirmed=" + Reached(PumpLifecycleState.PUMP_READY));
        output.WriteLine("  fault_feedback_never_active=" + (sawFault ? "False" : "True"));
        output.WriteLine("  valve_open_admitted_by_lifecycle=" + (lifecycle.AllowsValveOpen ? "True" : "False"));

        // The dispatch ledger, printed rather than inferred. Admission and dispatch are kept apart so a
        // session refusal never reads as a transport failure and neither reads as a successful write.
        output.WriteLine("  command_dispatches=" + log.Count.ToString(CultureInfo.InvariantCulture));
        output.WriteLine(
            "  command_dispatch_refusals=" + log.Count(o => !o.Succeeded).ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  dispatch_summary=" + (log.Count == 0
            ? "NONE"
            : string.Join("|", log.Select(o => o.Tag + "=" + o.Value + ":"
                + (o.Succeeded ? "OK" : o.Code)
                + (o.Dispatched ? string.Empty : ":NOT_DISPATCHED")))));
        DateTimeOffset firstAt = default;
        DateTimeOffset? previousAt = null;
        foreach (PumpCommandOutcome outcome in log)
        {
            if (previousAt is null)
            {
                firstAt = outcome.At;
            }

            double gapMs = previousAt is null ? 0d : (outcome.At - previousAt.Value).TotalMilliseconds;
            output.WriteLine("  dispatch=" + outcome.Tag
                + " coil=" + outcome.CoilAddress.ToString(CultureInfo.InvariantCulture)
                + " value=" + outcome.Value.ToString()
                + " admission=" + (outcome.Admitted ? "ADMITTED" : "REFUSED")
                + " dispatch=" + (outcome.Dispatched ? "DISPATCHED" : "NOT_DISPATCHED")
                + " code=" + outcome.Code
                + " gap_ms=" + gapMs.ToString("0.#", CultureInfo.InvariantCulture)
                + (outcome.TransportFaulted ? " transport=FAULTED" : string.Empty));
            previousAt = outcome.At;
        }

        // The dwell between the enable and the Run command is a physical question the Owner answers with the
        // inverter manual, not a guess this runner may fill in: Start() dispatches the whole sequence without
        // an artificial delay (packages/application/ProcessIo/ProcessIoPumpLifecycle.cs:288-321), so the only
        // spacing is the Modbus round trip. These two numbers say what that spacing actually was.
        output.WriteLine("  dispatch_window_ms=" + (log.Count == 0
            ? "0"
            : (log[log.Count - 1].At - firstAt).TotalMilliseconds.ToString("0.#", CultureInfo.InvariantCulture)));

        ReportFeedbackDiagnostics(boundary, catalogue, configuration, output);
        ReportTransportDiagnostics(transport, output);

        ReportPressureCrossCheck(session, catalogue, thresholds, configuration, time, output);

        if (ended is null)
        {
            PumpLifecycleStopOutcome stopped = lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
            output.WriteLine("  stop_accepted=" + (stopped.StopConfirmed ? "True" : "False"));
            output.WriteLine("  stop_code=" + stopped.Code);
        }
        else
        {
            // A failure inside the wait already drove the Stop path, so the ladder is terminal before this
            // point. Calling Stop again only earns a refusal code and prints a second, contradicting story
            // about the same teardown, so the record states what happened instead of asking again.
            output.WriteLine("  stop_accepted=False");
            output.WriteLine("  stop_code=NOT_REQUESTED_LADDER_ALREADY_TERMINAL");
        }

        TimeSpan stopBudget = TimeSpan.FromTicks(configuration.Ramp.Interval.Ticks * rampSteps)
            + configuration.StoppedFeedbackTimeout;
        DateTimeOffset stopDeadline = clock.UtcNow + stopBudget;
        int stopMaximumSteps = (int)(stopBudget.TotalMilliseconds / DriveIntervalMilliseconds) + 1000;

        for (int step = 0; step < stopMaximumSteps && lifecycle.Result is null; step += 1)
        {
            if (visited.Add(lifecycle.State))
            {
                trace.Add(lifecycle.State.ToString());
            }

            if (step > 0 && clock.UtcNow >= stopDeadline)
            {
                break;
            }

            lifecycle.Step();
            Thread.Sleep(DriveIntervalMilliseconds);
        }

        PumpLifecycleResult? result = lifecycle.Result;
        if (result is not null && visited.Add(result.TerminalState))
        {
            trace.Add(result.TerminalState.ToString());
        }

        output.WriteLine("  ladder_complete=" + string.Join(">", trace));
        output.WriteLine("  analog_stop_sentinel_written=" + Reached(PumpLifecycleState.RAMPING_DOWN));
        output.WriteLine("  pump_run_cleared=" + Reached(PumpLifecycleState.CLEARING_RUN));
        output.WriteLine("  pump_enable_released=" + Reached(PumpLifecycleState.DISABLING_INVERTER));
        output.WriteLine("  run_cleared_dispatch=" + (Written(PumpCommandContract.RunPumpTag, false) ? "True" : "False"));
        output.WriteLine("  enable_released_dispatch=" + (Written(PumpCommandContract.EnableInverterTag, false) ? "True" : "False"));
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

        // Success is the whole of it, not the absence of a complaint: the ladder must have reached PUMP_READY,
        // stopped in the STOPPED terminal state with a complete cleanup, released the command channel and been
        // reported safe to complete, and no fault input may have been seen active on the way. Each half is a
        // separate claim, so a run that tore down cleanly after a timeout cannot report an accepted trial.
        bool sawReady = visited.Contains(PumpLifecycleState.PUMP_READY);
        bool complete = result is not null
            && result.TerminalState == PumpLifecycleState.STOPPED
            && result.StoppedConfirmed
            && result.CleanupState == PumpCleanupStates.Complete
            && !lifecycle.OwnsPumpCommand
            && lifecycle.SafeToReportComplete
            && sawReady
            && !sawFault;

        output.WriteLine("  run_reached_ready=" + (sawReady ? "True" : "False"));
        output.WriteLine("  trial_code=" + (complete
            ? "PUMP_COMMAND_TRIAL_ACCEPTED"
            : "PUMP_COMMAND_TRIAL_" + (result?.TerminalState.ToString() ?? lifecycle.State.ToString())));
        return complete ? 0 : 2;
    }

    /// <summary>
    /// The two feedback inputs, read read-only after the ladder. Four different failures look identical from
    /// a state name alone - the bit is genuinely low, the polarity is not commissioned, the catalogue row is
    /// not bound, or the transport never answered - and each one needs a different correction. This prints the
    /// evidence that tells them apart and commands nothing.
    /// </summary>
    private static void ReportFeedbackDiagnostics(
        ProcessIoPumpCommandBoundary boundary,
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        PumpLifecycleConfiguration configuration,
        TextWriter output)
    {
        PumpFeedbackConfiguration feedback = configuration.Feedback;
        output.WriteLine("  running_commissioned=" + (feedback.RunningCommissioned ? "True" : "False"));
        output.WriteLine("  fault_commissioned=" + (feedback.FaultCommissioned ? "True" : "False"));
        output.WriteLine("  running_binding=" + DescribeBinding(catalogue, PumpCommandContract.RunningFeedbackTag));
        output.WriteLine("  fault_binding=" + DescribeBinding(catalogue, PumpCommandContract.FaultFeedbackTag));

        PumpFeedbackRead running = boundary.ReadRunning();
        output.WriteLine("  last_running_usable=" + (running.Usable ? "True" : "False"));
        output.WriteLine("  last_running_raw_bit=" + (running.RawBit ? "True" : "False"));
        output.WriteLine("  last_running_active=" + (running.Active ? "True" : "False"));
        output.WriteLine("  last_running_code=" + running.Code);

        PumpFeedbackRead fault = boundary.ReadFault();
        output.WriteLine("  last_fault_usable=" + (fault.Usable ? "True" : "False"));
        output.WriteLine("  last_fault_raw_bit=" + (fault.RawBit ? "True" : "False"));
        output.WriteLine("  last_fault_active=" + (fault.Active ? "True" : "False"));
        output.WriteLine("  last_fault_code=" + fault.Code);
    }

    /// <summary>
    /// What the catalogue binds one tag to, in the transport's own terms. The panel number is the Modbus
    /// address plus one, so this line can be compared with the mapping workbook without opening the CSV.
    /// </summary>
    private static string DescribeBinding(
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        string tag)
    {
        if (!catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row) || row is null)
        {
            return tag + "=ROW_MISSING";
        }

        if (row.BindingState == ProcessIoBindingState.UNBOUND || row.Binding is null)
        {
            return tag + "=UNBOUND " + row.CommissioningState;
        }

        string area = row.Binding.Area == ProcessIoArea.DiscreteInput ? "DI" : "CO";
        return tag + "=" + area + "@" + row.Binding.Address.ToString(CultureInfo.InvariantCulture)
            + " " + row.CommissioningState;
    }

    /// <summary>
    /// The transport's own counters for this run. Attempts and errors are what separate a device that
    /// answered "low" from a device that stopped answering: the observation path never fabricates a bit, so a
    /// missing answer arrives here as a refused observation plus a ledger error, never as a quiet false.
    /// </summary>
    private static void ReportTransportDiagnostics(ProcessIoPhysicalTransport transport, TextWriter output)
    {
        IReadOnlyList<ProcessIoTransportLedgerEntry> ledger = transport.GetLedger();
        List<ProcessIoTransportLedgerEntry> errors =
            ledger.Where(entry => !string.Equals(entry.Status, "SUCCESS", StringComparison.Ordinal)).ToList();
        output.WriteLine("  network_attempts=" + transport.NetworkAttempts.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  transport_errors=" + errors.Count.ToString(CultureInfo.InvariantCulture));
        // the send file carries one line per key, so the whole ledger has to fit on this one as well as in
        // the raw stdout file below it
        output.WriteLine("  transport_error_summary=" + (errors.Count == 0
            ? "NONE"
            : string.Join("|", errors.Select(entry => entry.FunctionCode + "@"
                + entry.Address.ToString(CultureInfo.InvariantCulture) + ":" + entry.Status))));
        foreach (ProcessIoTransportLedgerEntry entry in errors)
        {
            output.WriteLine("  transport_error=" + entry.Operation
                + " " + entry.FunctionCode
                + " address=" + entry.Address.ToString(CultureInfo.InvariantCulture)
                + " status=" + entry.Status
                + (string.IsNullOrEmpty(entry.Diagnostic) ? string.Empty : " detail=" + entry.Diagnostic));
        }
    }

    /// <summary>
    /// One independent read of the Pump Outlet Pressure through the read-only observer, over the same open
    /// session and the same catalogue, taken while the lifecycle reports readiness. It is a cross-check:
    /// the readiness decision was the lifecycle's, and a refused binding here changes nothing about that
    /// decision. It writes nothing.
    /// </summary>
    private static void ReportPressureCrossCheck(
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
}
