using Wjss.Contracts;
using Wjss.Runtime;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Focused coverage for the isolated IV Hardware trial (Trial B of the two-stage evidence split).
///
/// Every test here is configuration, refusal, admission or evidence-classification proof. The only
/// transport is a deterministic in-memory double, so no test contacts a device, opens a socket, loads
/// a native module or commands a real valve. The physical trial itself is a separate, separately
/// authorized Owner-local activity.
///
/// The values are synthetic: a non-routable placeholder host name, the published consent constant and
/// a canonical valve identity. No Owner-local endpoint, no committed address and no secret appears.
/// </summary>
public sealed class DryIvTrialTests
{
    private const string BenchHost = "bench-host.local";
    private const string Valve = "IV1";

    /// <summary>Command coil identity of IV1 (DO-023 / zero-based coil 22).</summary>
    private const int Iv1CommandCoil = 22;

    /// <summary>Upper/open feedback discrete input of IV1 (DI-021, zero-based address 20).</summary>
    private const int Iv1UpperInput = 20;

    /// <summary>Lower/closed feedback discrete input of IV1 (DI-029, zero-based address 28).</summary>
    private const int Iv1LowerInput = 28;

    private static readonly SequencingPressureThresholds Thresholds = new(1.0, 15.0, 15.0);

    private static readonly TimeSpan Supervision = TimeSpan.FromMilliseconds(20);

    // ------------------------------------------------------------------ selector

    [Fact]
    public void An_Absent_Trial_Selector_Does_Not_Select_The_Trial()
    {
        Assert.True(DryIvTrial.TryReadMode(Read(new Dictionary<string, string?>()), out bool selected, out _, out _));
        Assert.False(selected);
    }

    [Fact]
    public void The_Integrated_Dry_Literal_Does_Not_Select_The_Iv_Trial()
    {
        var environment = Read(new Dictionary<string, string?>
        {
            [DryIvTrial.ModeVariable] = nameof(DeviceProfile.TEST_HARDWARE_DRY),
        });

        Assert.False(DryIvTrial.TryReadMode(environment, out bool selected, out string code, out _));
        Assert.False(selected);
        Assert.Equal(DryIvTrial.RefusedModeInvalid, code);
    }

    [Fact]
    public void Only_The_Exact_Trial_Literal_Selects_The_Trial()
    {
        var environment = Read(new Dictionary<string, string?>
        {
            [DryIvTrial.ModeVariable] = DryIvTrial.ModeSoftwareSimulatedAi003,
        });

        Assert.True(DryIvTrial.TryReadMode(environment, out bool selected, out _, out _));
        Assert.True(selected);
    }

    [Fact]
    public void A_Differently_Cased_Trial_Literal_Is_Refused_Rather_Than_Normalized()
    {
        var environment = Read(new Dictionary<string, string?>
        {
            [DryIvTrial.ModeVariable] = DryIvTrial.ModeSoftwareSimulatedAi003.ToLowerInvariant(),
        });

        Assert.False(DryIvTrial.TryReadMode(environment, out bool selected, out string code, out _));
        Assert.False(selected);
        Assert.Equal(DryIvTrial.RefusedModeInvalid, code);
    }

    [Fact]
    public void The_Integrated_Dry_Selector_Is_Refused_When_Both_Selectors_Are_Set()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[ProcessIoEndpointConfiguration.DryModeVariable] = nameof(DeviceProfile.TEST_HARDWARE_DRY);

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedIntegratedDrySelected, code);
    }

    [Fact]
    public void Production_Can_Never_Select_The_Trial()
    {
        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.PRODUCTION, Read(ValidEnvironment()), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedProduction, code);
    }

    [Fact]
    public void Simulator_Can_Never_Select_The_Trial()
    {
        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.SIMULATOR, Read(ValidEnvironment()), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedProfileNotDry, code);
    }

    [Fact]
    public void A_Missing_Selector_Refuses_A_Direct_Configuration_Read()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(DryIvTrial.ModeVariable);

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedModeNotSelected, code);
    }

    // ------------------------------------------------------------------ consent and attestation

    [Fact]
    public void A_Missing_Consent_Refuses_Before_Anything_Is_Constructed()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(ProcessIoEndpointConfiguration.ConsentVariable);

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoEndpointConfiguration.ConsentMissing, code);
    }

    [Fact]
    public void A_Mismatched_Consent_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[ProcessIoEndpointConfiguration.ConsentVariable] =
            ProcessIoConsentPhrases.PhysicalFullSelectedSequence;

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoEndpointConfiguration.ConsentMismatch, code);
    }

    [Fact]
    public void A_Missing_Pump_Stopped_Attestation_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(DryIvTrial.PumpStoppedVariable);

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedPumpStoppedMissing, code);
    }

    [Fact]
    public void A_Non_Literal_Pump_Stopped_Attestation_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[DryIvTrial.PumpStoppedVariable] = "PUMP_IS_STOPPED";

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedPumpStoppedMissing, code);
    }

    [Fact]
    public void The_Trial_Write_Gate_Requires_The_Attestation_And_The_Simulated_Input()
    {
        Assert.False(ProcessIoDryCommandGate.TryAuthorizeIvTrial(
            DeviceProfile.TEST_HARDWARE_DRY, true, "bench.invalid:502", ProcessIoConsentPhrases.PhysicalDoSingle,
            pumpStoppedAttested: false, out _, out string noAttestation));
        Assert.Equal(ProcessIoDryCommandGate.RefusedPumpStoppedAttestationMissing, noAttestation);

        Assert.False(ProcessIoDryCommandGate.TryAuthorizeIvTrial(
            DeviceProfile.TEST_HARDWARE_DRY, false, "bench.invalid:502", ProcessIoConsentPhrases.PhysicalDoSingle,
            pumpStoppedAttested: true, out _, out string noSimulation));
        Assert.Equal(ProcessIoDryCommandGate.RefusedSimulatedInputNotSelected, noSimulation);

        Assert.False(ProcessIoDryCommandGate.TryAuthorizeIvTrial(
            DeviceProfile.PRODUCTION, true, "bench.invalid:502", ProcessIoConsentPhrases.PhysicalDoSingle,
            pumpStoppedAttested: true, out _, out string production));
        Assert.Equal(ProcessIoDryCommandGate.RefusedProductionRequested, production);
    }

    // ------------------------------------------------------------------ endpoint, valve, catalogue

    [Fact]
    public void A_Missing_Endpoint_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(ProcessIoEndpointConfiguration.HostVariable);

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out _, out _));
    }

    [Fact]
    public void A_Missing_Valve_Identity_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(DryIvTrial.ValveVariable);

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedValveIdentity, code);
    }

    [Theory]
    [InlineData("IV0")]
    [InlineData("IV9")]
    [InlineData("iv1")]
    [InlineData("IV01")]
    [InlineData("WJ1")]
    [InlineData("DO-023")]
    public void A_Non_Canonical_Valve_Identity_Refuses(string valveId)
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[DryIvTrial.ValveVariable] = valveId;

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedValveIdentity, code);
    }

    // ------------------------------------------------------------------ simulated pressure

    [Fact]
    public void A_Missing_Simulated_Pressure_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(DryIvTrial.SimulatedPressureVariable);

        Assert.False(DryIvTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(environment), out _, out string code, out _));
        Assert.Equal(DryIvTrial.RefusedSimulatedPressureMissing, code);
    }

    [Fact]
    public void The_Simulated_Pressure_Must_Exceed_The_Configured_Setpoint()
    {
        Assert.False(DryIvTrial.TryCreateSimulatedPumpReady(
            15.0, DateTimeOffset.UtcNow, Thresholds, 1, out _, out string code));
        Assert.Equal(DryIvTrial.RefusedSimulatedPressureAtOrBelowSetpoint, code);

        Assert.False(DryIvTrial.TryCreateSimulatedPumpReady(
            14.999, DateTimeOffset.UtcNow, Thresholds, 1, out _, out string codeBelow));
        Assert.Equal(DryIvTrial.RefusedSimulatedPressureAtOrBelowSetpoint, codeBelow);
    }

    [Fact]
    public void The_Simulated_Pressure_Must_Be_Finite_And_Inside_The_Engineering_Domain()
    {
        Assert.False(DryIvTrial.TryCreateSimulatedPumpReady(
            double.NaN, DateTimeOffset.UtcNow, Thresholds, 1, out _, out string notFinite));
        Assert.Equal(DryIvTrial.RefusedSimulatedPressureNotFinite, notFinite);

        Assert.False(DryIvTrial.TryCreateSimulatedPumpReady(
            double.PositiveInfinity, DateTimeOffset.UtcNow, Thresholds, 1, out _, out string infinite));
        Assert.Equal(DryIvTrial.RefusedSimulatedPressureNotFinite, infinite);

        Assert.False(DryIvTrial.TryCreateSimulatedPumpReady(
            1_000.0, DateTimeOffset.UtcNow, Thresholds, 1, out _, out string outOfRange));
        Assert.Equal(DryIvTrial.RefusedSimulatedPressureOutOfRange, outOfRange);
    }

    [Fact]
    public void An_Accepted_Simulated_Sample_Carries_The_Ai003_Identity_And_Simulated_Provenance()
    {
        Assert.True(DryIvTrial.TryCreateSimulatedPumpReady(
            20.0, DateTimeOffset.UtcNow, Thresholds, 7, out DryIvTrialSimulatedPumpReady? sample, out string code));

        Assert.Equal(DryIvTrial.PumpReadyAccepted, code);
        Assert.NotNull(sample);
        Assert.Equal("AI-003+", sample!.SourceTag);
        Assert.Equal(DryIvTrial.EvidenceKindSimulated, sample.EvidenceKind);
        Assert.Equal("SOFTWARE_SIMULATED", sample.EvidenceKind);
        Assert.Equal(DryIvTrial.SimulatedProvenance, sample.Provenance);
        Assert.Equal(DryIvTrial.TrialPurpose, sample.TrialPurpose);
        Assert.Equal(7, sample.TrialGeneration);
        Assert.Equal(PressureSample.PumpOutletSource, sample.Sample.SourceId);
        Assert.True(SequencingPressure.PumpReady(sample.Sample, Thresholds));

        // It is never classed as physical evidence.
        Assert.DoesNotContain("PHYSICAL", sample.EvidenceKind, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Simulated_Sample_Is_Never_Mistaken_For_The_Valve_Outlet_Source()
    {
        Assert.True(DryIvTrial.TryCreateSimulatedPumpReady(
            20.0, DateTimeOffset.UtcNow, Thresholds, 1, out DryIvTrialSimulatedPumpReady? sample, out _));

        Assert.NotEqual(PressureSample.ValveOutletSource(Valve), sample!.Sample.SourceId);
        Assert.NotEqual(ProcessIoIsolationValves.PumpInletPressureTag, sample.SourceTag);
    }

    // ------------------------------------------------------------------ sequence refusals

    [Fact]
    public void The_Sequence_Refuses_When_The_Session_Is_Not_Open()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport _) = CreateBoundary();
        session.Close(DateTimeOffset.UtcNow);

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.False(result.Accepted);
        Assert.Equal(DryIvTrial.RefusedSessionNotOpen, result.Code);
    }

    [Fact]
    public void The_Sequence_Refuses_A_Stale_Generation_Sample()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();

        // The sample was minted for generation 1; a reopen moves the session to generation 2.
        DryIvTrialSimulatedPumpReady stale = Sample(20.0, generation: 1);
        session.Close(DateTimeOffset.UtcNow);
        session.Open(DateTimeOffset.UtcNow);

        Assert.NotEqual(stale.TrialGeneration, session.Generation);

        DryIvTrialResult result = DryIvTrial.Execute(Request(stale), session, boundary);

        Assert.False(result.Accepted);
        Assert.Equal(DryIvTrial.RefusedGenerationChanged, result.Code);
        Assert.Empty(transport.CoilWrites);
    }

    [Fact]
    public void A_Contradictory_Initial_Feedback_Refuses_Before_Any_Write()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.DiscreteInputs[Iv1UpperInput] = true;
        transport.DiscreteInputs[Iv1LowerInput] = true;

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.False(result.Accepted);
        Assert.Equal(DryIvTrial.RefusedInitialStateContradictory, result.Code);
        Assert.Empty(transport.CoilWrites);
        Assert.Equal(0, transport.NetworkAttempts);
    }

    [Fact]
    public void An_Already_Open_Initial_State_Refuses_Before_Any_Write()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.DiscreteInputs[Iv1UpperInput] = true;

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.False(result.Accepted);
        Assert.Equal(DryIvTrial.RefusedInitialStateUnsafe, result.Code);
        Assert.Empty(transport.CoilWrites);
    }

    // ------------------------------------------------------------------ the happy path

    [Fact]
    public void The_Open_Command_Writes_Do023_Coil_22_True_And_Is_Field_Confirmed_By_The_Upper_Input()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.DiscreteInputs[Iv1UpperInput] = true;

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.True(result.Accepted);
        Assert.Equal(DryIvTrial.TrialAccepted, result.Code);
        Assert.True(result.OpenCommand!.Dispatched);
        Assert.Equal("DO-023", result.OpenCommand.Tag);
        Assert.Equal(Iv1CommandCoil, result.OpenCommand.CoilAddress);
        Assert.True(result.OpenCommand.CoilValue);
        Assert.True(result.Open!.FieldConfirmed);
        Assert.Equal("IV_OPEN_FIELD_CONFIRMED", result.Open.AssessmentCode);

        Assert.Contains(transport.CoilWrites, w => w.Address == Iv1CommandCoil && w.Value);
    }

    [Fact]
    public void The_Close_Command_Writes_The_Same_Coil_False_And_Is_Confirmed_By_The_Lower_Input()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.DiscreteInputs[Iv1UpperInput] = true;

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.True(result.Accepted);
        Assert.True(result.CloseCommand!.Dispatched);
        Assert.Equal("DO-023", result.CloseCommand.Tag);
        Assert.Equal(Iv1CommandCoil, result.CloseCommand.CoilAddress);
        Assert.False(result.CloseCommand.CoilValue);
        Assert.True(result.Close!.FieldConfirmed);
        Assert.Equal("IV_CLOSED_FIELD_CONFIRMED", result.Close.AssessmentCode);

        Assert.Contains(transport.CoilWrites, w => w.Address == Iv1CommandCoil && !w.Value);
    }

    [Fact]
    public void The_Open_Is_Never_Accepted_From_The_Fc01_Process_Image_Alone()
    {
        // The process image reads back true, but no discrete input reports the valve open.
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.Coils[Iv1CommandCoil] = true;

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.False(result.Accepted);
        Assert.Equal(DryIvTrial.RefusedOpenNotConfirmed, result.Code);
        Assert.True(result.TimedOut);
        Assert.False(result.Open!.FieldConfirmed);
    }

    [Fact]
    public void Both_Limits_After_The_Write_Refuse_The_Trial_And_Still_Clean_Up()
    {
        // The fake latches both feedback inputs as soon as the OPEN coil is written.
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.OnCoilWrite = (_, value) =>
        {
            if (value)
            {
                transport.DiscreteInputs[Iv1UpperInput] = true;
                transport.DiscreteInputs[Iv1LowerInput] = true;
            }
        };

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.False(result.Accepted);
        Assert.Equal(DryIvTrial.RefusedOpenContradictory, result.Code);

        // The valve was left energised by the failure, so cleanup is the only thing that de-energises it.
        Assert.NotNull(result.Cleanup);
        Assert.Contains("IV1", result.Cleanup!.AttemptedValveIds);
        Assert.Contains("IV1", result.Cleanup.ClosedValveIds);
        Assert.Empty(result.Cleanup.RemainingUnknownState);
        Assert.Contains(transport.CoilWrites, write => write.Address == Iv1CommandCoil && !write.Value);
    }

    [Fact]
    public void A_Clean_Run_Leaves_Nothing_Open_For_Cleanup_To_Close()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.DiscreteInputs[Iv1UpperInput] = true;

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.True(result.Accepted);
        Assert.NotNull(result.Cleanup);

        // Cleanup acts only on a valve this boundary left energised. The trial closed its own valve, so
        // the correct report is "attempted nothing, nothing unknown" — never a second CLOSE dispatch.
        Assert.Empty(result.Cleanup!.AttemptedValveIds);
        Assert.Empty(result.Cleanup.ClosedValveIds);
        Assert.Empty(result.Cleanup.RemainingUnknownState);
        Assert.True(result.Cleanup.Complete);

        // Exactly one OPEN and one CLOSE reached the wire, both on the same coil.
        Assert.Equal(2, transport.CoilWrites.Count);
        Assert.All(transport.CoilWrites, write => Assert.Equal(Iv1CommandCoil, write.Address));
    }

    [Fact]
    public void Unrelated_Isolation_Valves_Are_Never_Commanded()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.DiscreteInputs[Iv1UpperInput] = true;

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.True(result.Accepted);

        // IV2..IV8 coils are 23..29; not one of them may appear on the wire.
        int[] otherCoils = [23, 24, 25, 26, 27, 28, 29];
        Assert.DoesNotContain(transport.CoilWrites, w => otherCoils.Contains(w.Address));
        Assert.All(result.Cleanup!.AttemptedValveIds, id => Assert.Equal("IV1", id));
    }

    [Fact]
    public void A_Refused_Admission_Writes_Nothing_And_Reports_A_Typed_Code()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) =
            CreateBoundary(authorization: Unauthorized());

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        Assert.False(result.Accepted);
        Assert.Equal(DryIvTrial.RefusedWriteNotAuthorized, result.Code);
        Assert.Empty(transport.CoilWrites);
        Assert.Equal(0, transport.NetworkAttempts);
    }

    // ------------------------------------------------------------------ evidence classification

    [Fact]
    public void The_Trial_Result_Separates_Physical_Feedback_From_Simulated_Pump_Ready()
    {
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session, FakeTransport transport) = CreateBoundary();
        transport.DiscreteInputs[Iv1UpperInput] = true;

        DryIvTrialResult result = DryIvTrial.Execute(Request(Sample(20.0)), session, boundary);

        // Physical evidence members exist only for the command and the field feedback.
        Assert.True(result.OpenCommand!.Dispatched);
        Assert.True(result.Open!.UpperLimitDetected);
        Assert.True(result.Close!.LowerLimitDetected);

        // Nothing on the result claims a verified Pump or a verified physical AI-003 transmitter.
        Assert.DoesNotContain("PHYSICAL", DryIvTrial.EvidenceKindSimulated, StringComparison.Ordinal);
        Assert.Equal("SOFTWARE_SIMULATED_AI003_FOR_IV_TRIAL", DryIvTrial.SimulatedProvenance);
        Assert.Equal("IV_OPEN_CLOSE_HARDWARE_TRIAL_ONLY", DryIvTrial.TrialPurpose);
    }

    // ------------------------------------------------------------------ source identity

    [Fact]
    public void The_Trial_Is_Isolated_From_The_Integrated_Dry_Path_And_Adds_No_Route()
    {
        string root = RepoRoot();
        string trial = File.ReadAllText(Path.Combine(root, "apps", "runtime", "DryIvTrial.cs"));

        Assert.Contains("--dry-iv-trial", trial, StringComparison.Ordinal);
        Assert.Contains("CONSOLE_ONLY_NO_PORT_NO_ROUTE_NO_KIOSK_CONTROL", trial, StringComparison.Ordinal);

        foreach (string forbidden in new[] { "MapPost", "MapPut", "MapPatch", "MapDelete", "text/event-stream" })
        {
            Assert.DoesNotContain(forbidden, trial, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Trial_Never_Reaches_The_Simulator_Runtime_The_Queue_Or_Motion()
    {
        string trial = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "DryIvTrial.cs"));

        foreach (string forbidden in new[]
                 {
                     "SimulatorRuntime", "Queue", "TimeDelay", "Gc\\.", "Move", "Home",
                     "Application.Run", "MessageBox", "gclib",
                 })
        {
            Assert.DoesNotContain(forbidden, trial, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Trial_Writes_Only_Through_The_Session_And_Never_Touches_The_Transport_Directly()
    {
        string trial = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "DryIvTrial.cs"));

        Assert.Contains("equipment.RequestValve", trial, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteCoil(", trial, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchDigitalOutput(", trial, StringComparison.Ordinal);
        Assert.DoesNotContain("AdmitWrite(", trial, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Host_Routes_The_Trial_Before_The_Simulator_Composition_And_Leaves_The_Dry_Path_Intact()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "Program.cs"));

        int ivTrial = program.IndexOf("return DryIvTrial.Run(profile,", StringComparison.Ordinal);
        int dry = program.IndexOf("return CleaningPathDryTrial.Run(profile, port,", StringComparison.Ordinal);
        int simulator = program.IndexOf("SimulatorRuntime.Create(options, clock", StringComparison.Ordinal);

        Assert.True(ivTrial > 0, "the IV trial must be routed by the host");
        Assert.True(dry > ivTrial, "the IV trial selector must be read before the integrated dry branch");
        Assert.True(simulator > dry, "the dry branch must still return before the simulator composition");
        Assert.Contains("if (ivTrialSelected)", program, StringComparison.Ordinal);
        Assert.Contains("DryIvTrial.TryReadMode", program, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Trial_Declares_Both_Required_Evidence_Statements()
    {
        string trial = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "DryIvTrial.cs"));

        Assert.Contains("PUMP_READY_EVIDENCE = ", trial, StringComparison.Ordinal);
        Assert.Contains("PHYSICAL_PUMP_EVIDENCE = NOT_ESTABLISHED_BY_THIS_TRIAL", trial, StringComparison.Ordinal);
        Assert.Contains("PUMP_STOPPED: ", trial, StringComparison.Ordinal);
        Assert.Contains("NOT SOFTWARE_VERIFIED", trial, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ helpers

    private static Func<string, string?> Read(Dictionary<string, string?> values) => key => values.GetValueOrDefault(key);

    private static Dictionary<string, string?> ValidEnvironment() => new(StringComparer.Ordinal)
    {
        [DryIvTrial.ModeVariable] = DryIvTrial.ModeSoftwareSimulatedAi003,
        [ProcessIoEndpointConfiguration.HostVariable] = BenchHost,
        [ProcessIoEndpointConfiguration.PortVariable] = "502",
        [ProcessIoEndpointConfiguration.UnitIdVariable] = "1",
        [ProcessIoEndpointConfiguration.ConsentVariable] = ProcessIoConsentPhrases.PhysicalDoSingle,
        [DryIvTrial.PumpStoppedVariable] = DryIvTrial.PumpStoppedAttestation,
        [DryIvTrial.ValveVariable] = Valve,
        [DryIvTrial.SimulatedPressureVariable] = "20",
        [CleaningPathDryComposition.PressureLowVariable] = "1",
        [CleaningPathDryComposition.PressureHighVariable] = "15",
        [CleaningPathDryComposition.PumpReadySetpointVariable] = "15",
        [CleaningPathDryComposition.ValveSupervisionVariable] = "20",
    };

    private static DryIvTrialSimulatedPumpReady Sample(double bar, int generation = 1)
    {
        Assert.True(DryIvTrial.TryCreateSimulatedPumpReady(
            bar, DateTimeOffset.UtcNow, Thresholds, generation, out DryIvTrialSimulatedPumpReady? sample, out _));
        return sample!;
    }

    private static DryIvTrialRequest Request(DryIvTrialSimulatedPumpReady sample) => new()
    {
        ValveId = Valve,
        Thresholds = Thresholds,
        Supervision = Supervision,
        PumpReady = sample,
    };

    private static ProcessIoWriteAuthorization Unauthorized() => new()
    {
        PhysicalWriteAuthorized = false,
        Basis = "TEST_ONLY_NOT_AUTHORIZED",
    };

    private static (ProcessIoEquipmentBoundary Boundary, ProcessIoSession Session, FakeTransport Transport)
        CreateBoundary(ProcessIoWriteAuthorization? authorization = null)
    {
        var transport = new FakeTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(DateTimeOffset.UtcNow);

        ProcessIoWriteAuthorization grant = authorization ?? new ProcessIoWriteAuthorization
        {
            PhysicalWriteAuthorized = true,
            Basis = "TEST_ONLY_GRANTED",
        };

        return (new ProcessIoEquipmentBoundary(session, BuildCatalogue(), grant), session, transport);
    }

    /// <summary>
    /// The adopted catalogue built from the real binding table, exactly as the composition root does.
    /// The identities named here are the canonical IV1 route plus the Pump Outlet row this trial
    /// reports.
    /// </summary>
    private static Dictionary<string, ProcessIoCatalogueRow> BuildCatalogue()
    {
        var rows = new Dictionary<string, ProcessIoCatalogueRow>(StringComparer.Ordinal);
        string[] tags =
        [
            "AI-002+", "AI-003+",
            "AI-004+", "DI-021", "DI-029",
            "DO-023",
        ];

        foreach (string tag in tags)
        {
            Assert.True(ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding));
            rows[tag] = new ProcessIoCatalogueRow
            {
                SourceRowOrdinal = 1,
                Tag = tag,
                Description = tag,
                Module = string.Empty,
                Slot = string.Empty,
                IoSlot = string.Empty,
                Channel = string.Empty,
                Terminal = string.Empty,
                From = string.Empty,
                To = string.Empty,
                Reference = string.Empty,
                Location = string.Empty,
                Note = string.Empty,
                SourceStatus = "USED",
                IoClass = binding.Area == ProcessIoArea.Coil
                    ? "DO"
                    : binding.Area == ProcessIoArea.DiscreteInput ? "DI" : "AI",
                SourceClassification = "USED",
                ProvenanceArtifact = "test",
                ProvenanceSha256 = "test",
                Binding = binding,
                BindingState = ProcessIoBindingState.BOUND_LEGACY_EXACT,
                Address = null!,
                CommissioningState = ProcessIoCommissioningState.READ_ONLY_CANDIDATE,
            };
        }

        return rows;
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

    /// <summary>
    /// Deterministic in-memory double. It is not the product Simulation transport: a physical command
    /// path must never be backed by one, so this stands in for the adopted bench transport.
    /// </summary>
    private sealed class FakeTransport : IProcessIoTransport
    {
        public List<(string Device, int Address, bool Value)> CoilWrites { get; } = new();

        public Dictionary<int, bool> DiscreteInputs { get; } = new();

        public Dictionary<int, ushort> InputRegisters { get; } = new();

        public Dictionary<int, bool> Coils { get; } = new();

        public Action<string, bool>? OnCoilWrite { get; set; }

        public int NetworkAttempts => CoilWrites.Count;

        public bool ReadDiscreteInput(string deviceKey, int address)
            => DiscreteInputs.TryGetValue(address, out bool value) && value;

        public bool ReadCoil(string deviceKey, int coilAddress)
            => Coils.TryGetValue(coilAddress, out bool value) && value;

        public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity)
            => Enumerable.Range(startAddress, quantity).Select(a => Coils.TryGetValue(a, out bool v) && v).ToList();

        public ushort ReadInputRegister(string deviceKey, int registerAddress)
            => InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;

        public ushort ReadRegister(string deviceKey, int registerAddress)
            => InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;

        public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity)
            => Enumerable.Range(startAddress, quantity)
                .Select(a => InputRegisters.TryGetValue(a, out ushort v) ? v : (ushort)0).ToList();

        public void WriteCoil(string deviceKey, int coilAddress, bool value)
        {
            CoilWrites.Add((deviceKey, coilAddress, value));
            Coils[coilAddress] = value;
            OnCoilWrite?.Invoke(deviceKey, value);
        }

        public void WriteSingleRegister(string deviceKey, int registerAddress, ushort value) { }

        public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values) { }
    }
}
