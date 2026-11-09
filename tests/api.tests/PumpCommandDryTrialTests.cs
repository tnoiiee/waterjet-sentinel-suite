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

    private static Func<string, string?> Reader(IDictionary<string, string?> values) => key =>
        values.TryGetValue(key, out string? value) ? value : null;

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
    public void The_Trial_Composes_No_Valve_Boundary_No_Motion_And_No_Second_Ladder()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        // No valve surface of any kind: not the boundary, not a request, not a valve tag.
        foreach (string forbidden in new[]
                 {
                     "ProcessIoEquipmentBoundary",
                     "RequestValve",
                     "CleaningPathCompositionCoordinator",
                     "MotionCompositionRoot",
                     "CleaningPathDryComposition.TryCreate",
                     "SafeCleanupCloseAll",
                 })
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

        foreach (string key in new[]
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
                 })
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
    public void The_Ladder_Rungs_Are_Compared_On_The_Existing_State_Order()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "PumpCommandDryTrial.cs"));

        // The rung judgement is an ordinal comparison over the adopted lifecycle states: no rung name is
        // invented here and no rung is inferred from a later one.
        Assert.Contains("(int)highest >= (int)rung", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.PUMP_SELECTED", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.ENABLE_DISPATCHED", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.RUN_DISPATCHED", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.RAMPING_OUTPUT", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.RAMPING_DOWN", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.CLEARING_RUN", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleState.DISABLING_INVERTER", text, StringComparison.Ordinal);
        Assert.Contains("PumpLifecycleStates.IsTerminal(lifecycle.State)", text, StringComparison.Ordinal);
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
