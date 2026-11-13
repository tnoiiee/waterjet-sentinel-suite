using Wjss.Contracts;
using Wjss.Runtime;
using Wjss.Runtime.Core;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Coverage for the Pump command ladder trial (runbook Stage D, first variant).
///
/// Every test here is selector, refusal-order or source-identity evidence: nothing contacts a device,
/// opens a socket or commands anything. The decisive claim of this evidence class is a structural one, so
/// it is tested structurally: the runner composes no equipment boundary and no motion composition, which
/// means there is no valve command surface in the run at all.
///
/// The values used are synthetic: a non-routable placeholder host name, the published consent constant and
/// the published selectors. No Owner-local endpoint and no Secret appears in this file.
/// </summary>
public sealed class PumpCommandDryTrialTests
{
    private const string BenchHost = "bench-host.local";

    private static Dictionary<string, string?> Valid() => new(StringComparer.Ordinal)
    {
        [PumpCommandDryTrial.ModeVariable] = PumpCommandDryTrial.ModePumpOnly,
        [ProcessIoEndpointConfiguration.DryModeVariable] = nameof(DeviceProfile.TEST_HARDWARE_DRY),
        [ProcessIoEndpointConfiguration.HostVariable] = BenchHost,
        [ProcessIoEndpointConfiguration.ConsentVariable] = ProcessIoConsentPhrases.PhysicalDoSingle,
    };

    /// <summary>
    /// The environment reader every refusal test hands to the trial. It takes the concrete dictionary type
    /// because that is what all of them build - Valid() and the inline dictionaries - and a private helper
    /// whose parameter is IDictionary while every caller passes Dictionary is exactly what CA1859 reports
    /// under this repo's warnings-as-errors build. (CleaningPathDryCompositionTests.cs keeps IDictionary for
    /// its identical helper because one of ITS callers forwards an IDictionary parameter of its own.)
    /// </summary>
    private static Func<string, string?> Reader(Dictionary<string, string?> values) => key =>
        values.TryGetValue(key, out string? value) ? value : null;

    /// <summary>
    /// The markers that must not appear in the Pump-only trial, hoisted into an immutable field so the
    /// structural scan reuses one array (CA1861) instead of allocating a constant array per call.
    /// </summary>
    private static readonly string[] ForbiddenValveSurfaceMarkers =
    {
        "ProcessIoEquipmentBoundary",
        "RequestValve",
        "CleaningPathCompositionCoordinator",
        "MotionCompositionRoot",
        "CleaningPathDryComposition.TryCreate",
        "SafeCleanupCloseAll",
    };

    /// <summary>
    /// Every Main Valve claim the valve class must print. The pump-only class prints none of them except the
    /// two that state its own policy, and that difference is the point: a stage that commanded no valve must
    /// not leave a reader guessing whether the valve was checked.
    /// </summary>
    private static readonly string[] MainValveReportKeys =
    {
        "main_valve_policy=",
        "main_valve_block=",
        "main_valve_command_executed=",
        "main_valve_position_at_start=",
        "main_valve_open_source=",
        "main_valve_open_confirmed=",
        "main_valve_open_refusal=",
        "main_valve_open_elapsed_ms=",
        "main_valve_phase_after_open=",
        "main_valve_close_requested=",
        "main_valve_pump_stop_confirmed=",
        "main_valve_close_confirmed=",
        "main_valve_close_elapsed_ms=",
        "main_valve_position_at_end=",
        "main_valve_coils_resting=",
        "main_valve_over_torque=",
        "main_valve_position_lost=",
        "main_valve_held_open=",
        "main_valve_supervision_fault=",
        "main_valve_position_reads=",
        "main_valve_refusal=",
        "main_valve_writes=",
        "main_valve_write=",
        "main_valve_last_opened_raw=",
        "main_valve_last_closed_raw=",
        "main_valve_last_over_torque_raw=",
        "main_valve_last_read_code=",
        "pump_stop_reason=",
    };

    /// <summary>
    /// Every rung the Pump-only trial must print, hoisted for the same reason as the marker list above.
    /// </summary>
    private static readonly string[] PrintedRungKeys =
    {
        "start_accepted=",
        "selection_pair_written=",
        "pump_enable_written=",
        "pump_run_written=",
        "running_feedback_confirmed=",
        "analog_ramp_executed=",
        "pump_ready_confirmed=",
        "fault_feedback_never_active=",
        "analog_stop_sentinel_written=",
        "pump_run_cleared=",
        "pump_enable_released=",
        "terminal_state=",
        "stopped_confirmed=",
        "cleanup_state=",
        "ownership_released=",
        "safe_to_report_complete=",
        "session_state=",
        "transport_released=",
    };

    // ------------------------------------------------------------ selector semantics

    [Fact]
    public void An_Absent_Selector_Means_This_Evidence_Class_Was_Not_Chosen()
    {
        Assert.True(PumpCommandDryTrial.TryReadMode(
            Reader(new Dictionary<string, string?>(StringComparer.Ordinal)),
            out bool selected, out string code, out string detail));

        Assert.False(selected);
        Assert.Empty(code);
        Assert.Empty(detail);
    }

    [Theory]
    [InlineData("pump_only_no_valves")]
    [InlineData("PUMP_ONLY")]
    [InlineData("SIMULATED")]
    [InlineData("PUMP_ONLY_NO_VALVES ")]
    public void A_Selector_That_Is_Not_The_Exact_Literal_Is_Refused(string supplied)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PumpCommandDryTrial.ModeVariable] = supplied,
        };

        Assert.False(PumpCommandDryTrial.TryReadMode(Reader(values), out bool selected, out string code, out _));

        Assert.False(selected);
        Assert.Equal(PumpCommandDryTrial.RefusedModeInvalid, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Blank_Selector_Means_Not_Chosen_Rather_Than_Invalid(string supplied)
    {
        // Blank is treated the same way as absent: this evidence class was not asked for. Only a non-blank
        // value that is not the exact literal is a refusal, so a stray quote in a pasted block cannot be
        // mistaken for an instruction to run the Pump ladder.
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PumpCommandDryTrial.ModeVariable] = supplied,
        };

        Assert.True(PumpCommandDryTrial.TryReadMode(Reader(values), out bool selected, out string code, out _));

        Assert.False(selected);
        Assert.Empty(code);
    }

    [Fact]
    public void An_Unselected_Trial_Still_Refuses_When_Invoked_Directly()
    {
        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.TEST_HARDWARE_DRY,
            Reader(new Dictionary<string, string?>(StringComparer.Ordinal)),
            out string code, out _));

        Assert.Equal(PumpCommandDryTrial.RefusedModeNotSelected, code);
    }

    // ------------------------------------------------------------ profile and mode

    [Fact]
    public void The_Trial_Never_Starts_Production()
    {
        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.PRODUCTION, Reader(Valid()), out string code, out string detail));

        Assert.Equal(PumpCommandDryTrial.RefusedProduction, code);
        Assert.Contains("Nothing was read", detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DeviceProfile.SIMULATOR)]
    [InlineData(DeviceProfile.TEST_HARDWARE)]
    [InlineData(DeviceProfile.TEST_HARDWARE_READ_ONLY)]
    public void Only_The_Dry_Profile_Carries_The_Authorization_This_Trial_Consumes(DeviceProfile profile)
    {
        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            profile, Reader(Valid()), out string code, out _));

        Assert.Equal(PumpCommandDryTrial.RefusedProfile, code);
    }

    [Fact]
    public void The_Dry_Command_Mode_Is_Never_Inferred_From_The_Profile()
    {
        Dictionary<string, string?> values = Valid();
        values.Remove(ProcessIoEndpointConfiguration.DryModeVariable);

        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.TEST_HARDWARE_DRY, Reader(values), out string code, out _));

        Assert.Equal(PumpCommandDryTrial.RefusedProfile, code);
    }

    // ------------------------------------------------------------ evidence-class exclusivity

    [Theory]
    [InlineData(DryIvTrial.ModeVariable)]
    [InlineData(ProcessIoPumpReadyReadTrial.ModeVariable)]
    public void Two_Physical_Evidence_Classes_Are_Never_Combined_In_One_Run(string variable)
    {
        Dictionary<string, string?> values = Valid();
        values[variable] = "PRESENT";

        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.TEST_HARDWARE_DRY, Reader(values), out string code, out string detail));

        Assert.Equal(PumpCommandDryTrial.RefusedIvTrialSelected, code);
        Assert.Contains("which one produced what", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Declared_Pump_Pressure_Is_Never_Mixed_Into_A_Physical_Pump_Run()
    {
        Dictionary<string, string?> values = Valid();
        values[CleaningPathDryComposition.SimulatedPumpReadyVariable] =
            CleaningPathDryComposition.SimulatedPumpReadyLiteral;

        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.TEST_HARDWARE_DRY, Reader(values), out string code, out string detail));

        Assert.Equal(PumpCommandDryTrial.RefusedSimulatedSelector, code);
        Assert.Contains("physical Pump ladder", detail, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ Owner-local configuration

    [Fact]
    public void A_Missing_Endpoint_Is_Refused_Before_Anything_Is_Contacted()
    {
        Dictionary<string, string?> values = Valid();
        values.Remove(ProcessIoEndpointConfiguration.HostVariable);

        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.TEST_HARDWARE_DRY, Reader(values), out string code, out _));

        Assert.NotEmpty(code);
        Assert.NotEqual(PumpCommandDryTrial.RefusedModeInvalid, code);
    }

    [Fact]
    public void A_Missing_Consent_Is_Refused_And_Never_Substituted()
    {
        Dictionary<string, string?> values = Valid();
        values.Remove(ProcessIoEndpointConfiguration.ConsentVariable);

        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.TEST_HARDWARE_DRY, Reader(values), out string code, out _));

        Assert.Equal(ProcessIoEndpointConfiguration.ConsentMissing, code);
    }

    [Fact]
    public void A_Wrong_Consent_Is_Refused_By_The_Same_Gate_The_Composition_Uses()
    {
        Dictionary<string, string?> values = Valid();
        values[ProcessIoEndpointConfiguration.ConsentVariable] = ProcessIoConsentPhrases.PhysicalDiAiRead;

        Assert.False(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.TEST_HARDWARE_DRY, Reader(values), out string code, out _));

        Assert.Equal(ProcessIoEndpointConfiguration.ConsentMismatch, code);
    }

    [Fact]
    public void The_Published_Bench_Configuration_Is_Admitted()
    {
        Assert.True(PumpCommandDryTrial.TryValidateInvocation(
            DeviceProfile.TEST_HARDWARE_DRY, Reader(Valid()), out string code, out string detail));

        Assert.Empty(code);
        Assert.Empty(detail);
    }

    // ------------------------------------------------------------ the structural claim

    [Fact]
    public void The_Trial_Composes_No_Isolation_Valve_Boundary_No_Motion_And_No_Second_Ladder()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        // No Isolation Valve surface of any kind: not the equipment boundary, not a request, not a valve tag.
        // The Main Valve of block 1.27 is the one valve this file may command, and only in its own evidence
        // class; it reaches the rack through the same Pump boundary, never through a second write path.
        Assert.Contains("new ProcessIoMainValveGate(boundary, mainValveConfiguration, time)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("_transport.WriteCoil", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteSingleRegister", text, StringComparison.Ordinal);
        foreach (string forbidden in ForbiddenValveSurfaceMarkers)
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
        }

        // The ladder is the existing lifecycle's, so this file must not implement a second one.
        Assert.Contains("new ProcessIoPumpLifecycle(", text, StringComparison.Ordinal);
        Assert.Contains("lifecycle.Start(new PumpLifecycleStartRequest", text, StringComparison.Ordinal);
        Assert.Contains("lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION)", text, StringComparison.Ordinal);
        Assert.Contains("iv_command_executed=", text, StringComparison.Ordinal);
        Assert.Contains("iv_command_surface=", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Trial_Prints_Every_Rung_It_Is_Asked_To_Prove()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        foreach (string key in PrintedRungKeys)
        {
            Assert.Contains(key, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Host_Builds_This_Trial_Before_The_Profile_Gate_And_The_Simulator()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "Program.cs"));

        int pumpCommand = program.IndexOf("PumpCommandDryTrial.Run(profile", StringComparison.Ordinal);
        int simulator = program.IndexOf("SimulatorRuntime.Create(options, clock", StringComparison.Ordinal);

        Assert.True(pumpCommand > 0, "the host must run the Pump command ladder trial");
        Assert.True(simulator > pumpCommand, "the trial must return before any simulator composition");
        Assert.Contains("PumpCommandDryTrial.TryReadMode(", program, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Ladder_Rungs_Are_Proven_By_Observed_States_And_By_The_Dispatch_Log()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        // A rung is claimed only when the ladder was actually shown that state. Ordinals are never compared:
        // PumpLifecycleStates.IsFailure is ordinal 20 and up, so the failure values sit above the progression
        // values and RUNNING_FEEDBACK_TIMEOUT would have reported RAMPING_OUTPUT as reached. That is the
        // difference between a record and a story, and it is the judgement the field evidence turns on.
        Assert.Contains("var visited = new HashSet<PumpLifecycleState> { lifecycle.State };", text, StringComparison.Ordinal);
        Assert.Contains("visited.Contains(rung)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(int)highest >= (int)rung", text, StringComparison.Ordinal);

        // The rungs before WAITING_FOR_RUNNING are crossed inside Start(), where no loop can observe them, so
        // those claims come from the boundary's own dispatch log: the tag, the value and a successful dispatch.
        Assert.Contains("log.Any(o => o.Succeeded", text, StringComparison.Ordinal);
        Assert.Contains("PumpCommandContract.SelectPumpOneTag, true", text, StringComparison.Ordinal);
        Assert.Contains("PumpCommandContract.SelectPumpTwoTag, false", text, StringComparison.Ordinal);
        Assert.Contains("PumpCommandContract.EnableInverterTag, true", text, StringComparison.Ordinal);
        Assert.Contains("PumpCommandContract.RunPumpTag, true", text, StringComparison.Ordinal);

        // Only states the existing lifecycle enum names, never an invented rung.
        Assert.Contains("PumpLifecycleState.RAMPING_OUTPUT", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.RAMPING_DOWN", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.CLEARING_RUN", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.DISABLING_INVERTER", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.PUMP_READY", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleStates.IsTerminal(lifecycle.State)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Stop_Order_Is_Not_Asked_Twice_And_Complete_Is_The_Whole_Record()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        // A failure inside the wait already drove the Stop path, so the ladder is terminal before this point.
        // Asking again used to earn WJSS_PUMP_COMMAND_REFUSED_ALREADY_TERMINAL and print a second,
        // contradicting story about the same teardown; now the record states that no second order was sent.
        Assert.Contains("if (ended is null)", text, StringComparison.Ordinal);
        Assert.Contains("stop_code=NOT_REQUESTED_LADDER_ALREADY_TERMINAL", text, StringComparison.Ordinal);

        // An accepted trial is the whole of it: the ladder reached PUMP_READY, ended in the STOPPED terminal
        // state with a complete cleanup, released the command channel, was reported safe to complete, and never
        // saw a Fault input active. A run that tore down cleanly after a timeout cannot report an accepted trial.
        Assert.Contains("result.TerminalState == PumpLifecycleState.STOPPED", text, StringComparison.Ordinal);
        Assert.Contains("&& result.StoppedConfirmed", text, StringComparison.Ordinal);
        Assert.Contains("&& !lifecycle.OwnsPumpCommand", text, StringComparison.Ordinal);
        Assert.Contains("&& lifecycle.SafeToReportComplete", text, StringComparison.Ordinal);
        Assert.Contains("&& sawReady", text, StringComparison.Ordinal);
        Assert.Contains("&& !sawFault", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Timeout_Names_The_Feedback_It_Waited_For_And_The_Transport_It_Used()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        // Nothing about a refusal may stay silent: the bit, its usability, the refusal code, the catalogue
        // binding, the commissioned polarity and the transport counters are all printed, so a reader can
        // tell a low input from an unbound row and from a device that never answered.
        foreach (string key in new[]
        {
            "last_running_usable=", "last_running_raw_bit=", "last_running_active=", "last_running_code=",
            "last_fault_usable=", "last_fault_raw_bit=", "last_fault_active=", "last_fault_code=",
            "running_binding=", "fault_binding=", "running_commissioned=", "fault_commissioned=",
            "network_attempts=", "transport_errors=", "transport_error_summary=", "dispatch_summary=",
            "last_running_observation_raw=", "last_fault_observation_raw=", "dispatch_address_agreement=",
        })
        {
            Assert.Contains("  " + key, text, StringComparison.Ordinal);
        }

        // The pace is a bounded constant, ten times finer than the wall-clock gate the ladder waits on.
        Assert.Contains("private const int DriveIntervalMilliseconds = 100;", text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ the Main Valve evidence class
    //
    // These four tests hold the second selector to the same standard as the first one: it is chosen by an
    // exact literal, it is refused before any contact when its own configuration is unusable, it prints every
    // claim the field driver asserts, and a clean Pump teardown cannot certify it.

    [Theory]
    [InlineData(PumpCommandDryTrial.ModePumpOnly)]
    [InlineData(PumpCommandDryTrial.ModeMainValveAndPump)]
    public void Either_Exact_Selector_Chooses_This_Evidence_Class(string supplied)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PumpCommandDryTrial.ModeVariable] = supplied,
        };

        Assert.True(PumpCommandDryTrial.TryReadMode(
            Reader(values), out bool selected, out string code, out string detail));

        Assert.True(selected);
        Assert.Empty(code);
        Assert.Empty(detail);
    }

    [Fact]
    public void The_Main_Valve_Class_Prints_Its_Own_Identity_And_Every_Valve_Claim()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        // A different evidence class needs a different identity line, so a transcript cannot be filed under
        // the pump-only run that never touched a valve.
        Assert.Contains(
            "  trial_identity=\" + (mainValveMode ? MainValveTrialIdentity : TrialIdentity)", text,
            StringComparison.Ordinal);

        foreach (string key in MainValveReportKeys)
        {
            Assert.Contains("  " + key, text, StringComparison.Ordinal);
        }

        // And the pump-only class keeps saying what it always said.
        Assert.Contains("iv_command_executed=\" + IvCommandExecutedNever", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Valve_Configuration_Is_Refused_Before_The_Trial_Contacts_Anything()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));
        string composition = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));

        int valveConfiguration = text.IndexOf(
            "CleaningPathDryComposition.TryReadMainValveConfiguration", StringComparison.Ordinal);
        int transport = text.IndexOf("new ProcessIoPhysicalTransport(endpoints)", StringComparison.Ordinal);

        Assert.True(valveConfiguration > 0, "the trial must read the Main Valve bounds");
        Assert.True(transport > 0, "the trial must build its transport");
        Assert.True(valveConfiguration < transport, "an unusable valve configuration must be refused before a transport exists");

        // The bound is required, so it is never defaulted and the trial never waits on an invented number.
        // The trial does not name the variables itself: it hands the read to the composition, and the
        // composition is what refuses. So the required bound is pinned where it is enforced.
        Assert.Contains("CleaningPathDryComposition.TryReadMainValveConfiguration(", text, StringComparison.Ordinal);
        Assert.Contains("WJSS_MAIN_VALVE_OPEN_TIMEOUT_MS", composition, StringComparison.Ordinal);
        Assert.Contains("WJSS_MAIN_VALVE_CLOSE_TIMEOUT_MS", composition, StringComparison.Ordinal);
        Assert.Contains(
            "TryReadRequiredTimeout(readEnvironment, MainValveOpenTimeoutVariable, out TimeSpan openTimeout)",
            composition, StringComparison.Ordinal);
        Assert.Contains(
            "TryReadRequiredTimeout(readEnvironment, MainValveCloseTimeoutVariable, out TimeSpan closeTimeout)",
            composition, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Valve_Failure_Cannot_Be_Passed_Over_By_A_Clean_Pump_Teardown()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        // The valve half is a conjunct of the verdict, not a note beside it.
        Assert.Contains("&& mainValveComplete;", text, StringComparison.Ordinal);
        Assert.Contains("\"PUMP_COMMAND_TRIAL_MAIN_VALVE_REFUSED\"", text, StringComparison.Ordinal);

        // A trip or a lost position during the ramp goes through the ONE stop path with the lifecycle's own
        // failure reason, and the valve is not touched again until the stop is confirmed.
        Assert.Contains("mainValve.Supervise()", text, StringComparison.Ordinal);
        Assert.Contains("lifecycle.Stop(PumpStopReason.SEQUENCE_FAILURE)", text, StringComparison.Ordinal);
        Assert.Contains("gate.BeginClose(pumpStopConfirmed, out string closeCode)", text, StringComparison.Ordinal);
        Assert.Contains("PumpCommandRefusals.RefusedMainValveCloseNotStopped", text, StringComparison.Ordinal);

        // And the close itself is a bounded wait, derived from the configured bound like every other wait here.
        Assert.Contains("gate.Configuration.CloseTimeout.TotalMilliseconds", text, StringComparison.Ordinal);
        Assert.Contains("gate.Configuration.OpenTimeout.TotalMilliseconds", text, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
