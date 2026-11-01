using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// ACCELERATED-CLEANING-INTEGRATION-01 fake integrated dry-cycle matrix.
//
// Every test here is FAKE / DRY: the transport is a deterministic in-memory double or the product's
// own SimulatedProcessIoTransport, no socket is opened, and no device is contacted. The matrix proves
// the corrected IV command and feedback composition:
//
//   - Legacy maintained single-coil authority: OPEN = true, CLOSE = false, coil 21+n, FC05
//   - corrected feedback: Upper/open DI-020+n, Lower/closed DI-028+n
//   - FC01 read-back is process-image evidence only and never field confirmation
//   - Upper/open, Lower/closed and paired pressure are three separate evidence kinds
//   - safe cleanup writes false and retains the de-energised CLOSED state
//   - a PRODUCTION or SIMULATOR-backed command path is refused
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

public sealed class ProcessIoDryCycleMatrixTests
{
    private const string Consent = "I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL";

    /// <summary>
    /// Deterministic fake transport. Records every call so a test can prove exactly what was written.
    /// Not the product Simulation transport: this one is only reachable through a valid grant.
    /// </summary>
    private sealed class FakeTransport : IProcessIoTransport
    {
        public List<(string Device, int Address, bool Value)> CoilWrites { get; } = new();
        public Dictionary<int, bool> DiscreteInputs { get; } = new();
        public Dictionary<int, ushort> InputRegisters { get; } = new();
        public Dictionary<int, bool> Coils { get; } = new();
        public int NetworkAttempts => CoilWrites.Count;

        public bool ReadDiscreteInput(string deviceKey, int address)
            => DiscreteInputs.TryGetValue(address, out bool value) && value;

        public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity)
            => Enumerable.Range(startAddress, quantity).Select(a => Coils.TryGetValue(a, out bool v) && v).ToList();

        public ushort ReadInputRegister(string deviceKey, int registerAddress)
            => InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;

        public ushort ReadRegister(string deviceKey, int registerAddress)
            => InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;

        public bool ReadCoil(string deviceKey, int coilAddress)
            => Coils.TryGetValue(coilAddress, out bool value) && value;

        public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity)
            => Enumerable.Range(startAddress, quantity)
                .Select(a => InputRegisters.TryGetValue(a, out ushort v) ? v : (ushort)0).ToList();

        public void WriteCoil(string deviceKey, int coilAddress, bool value)
        {
            CoilWrites.Add((deviceKey, coilAddress, value));
            Coils[coilAddress] = value;
        }

        public void WriteSingleRegister(string deviceKey, int registerAddress, ushort value) { }

        public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values) { }
    }

    private static ProcessIoEquipmentBoundary CreateGranted(FakeTransport transport, DateTimeOffset? at = null)
    {
        Assert.True(ProcessIoDryCommandGate.TryIssue(
            DeviceProfile.TEST_HARDWARE_DRY,
            dryCommandMode: true,
            ownerLocalEndpoint: "dry-run.invalid:502",
            suppliedConsent: Consent,
            at: at ?? new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            out ProcessIoDryCommandGrant? grant,
            out string code));
        Assert.Equal(ProcessIoDryCommandGate.Granted, code);

        Assert.True(ProcessIoEquipmentBoundary.TryCreate(
            transport, grant, TimeProvider.System, out ProcessIoEquipmentBoundary? boundary, out string createCode));
        Assert.Equal(ProcessIoDryCommandGate.Granted, createCode);
        return boundary!;
    }

    // ---------------------------------------------------------------- identity and ordinals

    [Fact]
    public void Identity_Covers_Exactly_Eight_Valves_And_Refuses_Everything_Else()
    {
        Assert.Equal(8, ProcessIoIsolationValves.Count);
        for (int n = 1; n <= 8; n += 1)
        {
            Assert.Equal("IV" + n, ProcessIoIsolationValves.ValveId(n));
            Assert.True(ProcessIoIsolationValves.TryParseValveId("IV" + n, out int parsed));
            Assert.Equal(n, parsed);
        }

        foreach (string invalid in new[] { "IV0", "IV9", "IV", "iv1", "WJ1", "", "  ", "IV01", null! })
        {
            Assert.False(ProcessIoIsolationValves.TryParseValveId(invalid, out _));
        }
    }

    [Theory]
    [InlineData(1, "DO-023", 22, "DI-021", "DI-029", "AI-004+")]
    [InlineData(2, "DO-024", 23, "DI-022", "DI-030", "AI-005+")]
    [InlineData(3, "DO-025", 24, "DI-023", "DI-031", "AI-006+")]
    [InlineData(4, "DO-026", 25, "DI-024", "DI-032", "AI-007+")]
    [InlineData(5, "DO-027", 26, "DI-025", "DI-033", "AI-008+")]
    [InlineData(6, "DO-028", 27, "DI-026", "DI-034", "AI-009+")]
    [InlineData(7, "DO-029", 28, "DI-027", "DI-035", "AI-010+")]
    [InlineData(8, "DO-030", 29, "DI-028", "DI-036", "AI-011+")]
    public void Ordinals_Resolve_To_The_Adopted_Rows_And_Addresses(
        int n, string commandTag, int coil, string openTag, string closedTag, string pressureTag)
    {
        Assert.Equal(commandTag, ProcessIoIsolationValves.CommandTag(n));
        Assert.Equal(coil, ProcessIoIsolationValves.CommandCoilAddress(n));
        Assert.Equal(openTag, ProcessIoIsolationValves.OpenFeedbackTag(n));
        Assert.Equal(closedTag, ProcessIoIsolationValves.ClosedFeedbackTag(n));
        Assert.Equal(pressureTag, ProcessIoIsolationValves.OutletPressureTag(n));
    }

    [Fact]
    public void Open_Feedback_Is_Upper_And_Closed_Feedback_Is_Lower()
    {
        for (int n = 1; n <= 8; n += 1)
        {
            Assert.True(ProcessIoBindings.TryGet(ProcessIoIsolationValves.OpenFeedbackTag(n), out ProcessIoPhysicalBinding open));
            Assert.True(ProcessIoBindings.TryGet(ProcessIoIsolationValves.ClosedFeedbackTag(n), out ProcessIoPhysicalBinding closed));
            Assert.Equal(ProcessIoArea.DiscreteInput, open.Area);
            Assert.Equal(ProcessIoArea.DiscreteInput, closed.Area);
            Assert.Equal(19 + n, open.Address);
            Assert.Equal(27 + n, closed.Address);
            Assert.Equal("READ", open.Direction);
            Assert.Equal("READ", closed.Direction);
            Assert.True(open.Address < closed.Address);
        }
    }

    // ---------------------------------------------------------------- command polarity

    [Fact]
    public void Open_Writes_True_And_Close_Writes_False_On_The_Same_Coil()
    {
        var transport = new FakeTransport();
        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));
        Assert.Single(transport.CoilWrites);
        Assert.Equal((ProcessIoDevices.RackT8, 22, true), transport.CoilWrites[0]);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Close));
        Assert.Equal(2, transport.CoilWrites.Count);
        Assert.Equal((ProcessIoDevices.RackT8, 22, false), transport.CoilWrites[1]);
    }

    [Theory]
    [InlineData(1, 22)]
    [InlineData(2, 23)]
    [InlineData(3, 24)]
    [InlineData(4, 25)]
    [InlineData(5, 26)]
    [InlineData(6, 27)]
    [InlineData(7, 28)]
    [InlineData(8, 29)]
    public void Every_Valve_Commands_Its_Own_Coil_Only(int ordinal, int expectedCoil)
    {
        var transport = new FakeTransport();
        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest(
            ProcessIoIsolationValves.ValveId(ordinal), CleaningPathValveCommandKind.Open));

        Assert.Single(transport.CoilWrites);
        Assert.Equal(expectedCoil, transport.CoilWrites[0].Address);
        Assert.True(transport.CoilWrites[0].Value);
    }

    [Fact]
    public void Command_Is_Maintained_Not_A_Pulse_And_Retains_Its_State()
    {
        var transport = new FakeTransport();
        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV3", CleaningPathValveCommandKind.Open));

        // Exactly one write: no automatic revert write, no trailing pulse-low.
        Assert.Single(transport.CoilWrites);
        Assert.True(boundary.CommandedState[3]);
        Assert.True(transport.Coils[24]);
    }

    [Fact]
    public void An_Invalid_Valve_Identity_Is_Refused_With_Zero_Writes()
    {
        var transport = new FakeTransport();
        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV9", CleaningPathValveCommandKind.Open));

        Assert.Empty(transport.CoilWrites);
        Assert.Equal(0, boundary.WriteAttempts);
        Assert.Equal("IV_COMMAND_REFUSED_VALVE_IDENTITY_INVALID", boundary.LastRefusal);
    }

    // ---------------------------------------------------------------- separated evidence

    [Fact]
    public void Upper_Open_Lower_Closed_And_Pressure_Are_Three_Separate_Evidence_Kinds()
    {
        var transport = new FakeTransport();
        // IV2: Upper/open asserted (FC02 21), Lower/closed clear (FC02 29), pressure present.
        transport.DiscreteInputs[21] = true;
        transport.DiscreteInputs[29] = false;
        transport.InputRegisters[4] = 16384;

        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV2", CleaningPathValveCommandKind.Open));

        ProcessIoValveObservation observation = boundary.Observe("IV2");

        Assert.True(observation.UpperLimitDetected);
        Assert.False(observation.LowerLimitDetected);
        Assert.NotNull(observation.OutletPressure);
        Assert.True(observation.FieldConfirmed);
        Assert.Equal("IV_OPEN_FIELD_CONFIRMED", observation.AssessmentCode);
    }

    [Fact]
    public void Process_Image_Read_Back_Is_Never_Used_As_Field_Confirmation()
    {
        var transport = new FakeTransport();
        // The output image says "energised" but NO field feedback exists.
        transport.Coils[22] = true;
        transport.DiscreteInputs[20] = false;
        transport.DiscreteInputs[28] = false;

        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        ProcessIoValveObservation observation = boundary.Observe("IV1");

        Assert.True(observation.ProcessImageReadBack);      // process image only
        Assert.False(observation.UpperLimitDetected);       // no field evidence
        Assert.False(observation.FieldConfirmed);           // so NOT confirmed
        Assert.Equal("IV_FIELD_NOT_CONFIRMED", observation.AssessmentCode);
    }

    [Fact]
    public void Contradictory_Feedback_Is_Never_Resolved_In_Favour_Of_The_Command()
    {
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = true;   // Upper/open
        transport.DiscreteInputs[28] = true;   // Lower/closed

        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Close));

        ProcessIoValveObservation observation = boundary.Observe("IV1");

        Assert.False(observation.FieldConfirmed);
        Assert.Equal("IV_FEEDBACK_CONTRADICTORY_BOTH_LIMITS", observation.AssessmentCode);
    }

    [Fact]
    public void Observation_Without_A_Commanded_State_Is_Not_Confirmed()
    {
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = true;

        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);
        ProcessIoValveObservation observation = boundary.Observe("IV1");

        Assert.False(observation.CommandAccepted);
        Assert.False(observation.FieldConfirmed);
        Assert.Equal("IV_OBSERVATION_NO_COMMANDED_STATE", observation.AssessmentCode);
    }

    // ---------------------------------------------------------------- safe cleanup

    [Fact]
    public void Safe_Cleanup_Writes_False_And_Retains_The_DeEnergised_State()
    {
        var transport = new FakeTransport();
        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV5", CleaningPathValveCommandKind.Open));

        IReadOnlyList<string> closed = boundary.SafeCleanupCloseAll();

        Assert.Equal(2, closed.Count);
        Assert.Contains("IV1", closed);
        Assert.Contains("IV5", closed);
        Assert.False(boundary.CommandedState[1]);
        Assert.False(boundary.CommandedState[5]);

        // The retained command state is CLOSED and no second revert write followed.
        Assert.Equal(4, transport.CoilWrites.Count);
        Assert.False(transport.CoilWrites[2].Value);
        Assert.False(transport.CoilWrites[3].Value);
        Assert.False(transport.Coils[22]);
        Assert.False(transport.Coils[26]);
    }

    [Fact]
    public void Safe_Cleanup_Never_Touches_A_Valve_It_Did_Not_Command()
    {
        var transport = new FakeTransport();
        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV4", CleaningPathValveCommandKind.Open));
        boundary.SafeCleanupCloseAll();

        // Only coil 25 was ever written; coils 22..24 and 26..29 are untouched.
        Assert.All(transport.CoilWrites, w => Assert.Equal(25, w.Address));
    }

    // ---------------------------------------------------------------- authority refusals

    [Theory]
    [InlineData(DeviceProfile.SIMULATOR)]
    [InlineData(DeviceProfile.TEST_HARDWARE)]
    [InlineData(DeviceProfile.TEST_HARDWARE_READ_ONLY)]
    public void Only_The_Dry_Profile_Is_Granted(DeviceProfile profile)
    {
        Assert.False(ProcessIoDryCommandGate.TryIssue(
            profile, dryCommandMode: true, ownerLocalEndpoint: "dry-run.invalid:502",
            suppliedConsent: Consent, at: DateTimeOffset.UnixEpoch,
            out ProcessIoDryCommandGrant? grant, out string code));
        Assert.Null(grant);
        Assert.Equal(ProcessIoDryCommandGate.RefusedProfileNotDry, code);
    }

    [Fact]
    public void Production_Is_Refused_Before_Any_Other_Check()
    {
        Assert.False(ProcessIoDryCommandGate.TryIssue(
            DeviceProfile.PRODUCTION, dryCommandMode: true, ownerLocalEndpoint: null,
            suppliedConsent: null, at: DateTimeOffset.UnixEpoch,
            out ProcessIoDryCommandGrant? grant, out string code));
        Assert.Null(grant);
        Assert.Equal(ProcessIoDryCommandGate.RefusedProductionRequested, code);
    }

    [Fact]
    public void A_Missing_Endpoint_Or_Consent_Is_Refused()
    {
        Assert.False(ProcessIoDryCommandGate.TryIssue(
            DeviceProfile.TEST_HARDWARE_DRY, true, "  ", Consent, DateTimeOffset.UnixEpoch,
            out _, out string codeMissingEndpoint));
        Assert.Equal(ProcessIoDryCommandGate.RefusedEndpointMissing, codeMissingEndpoint);

        Assert.False(ProcessIoDryCommandGate.TryIssue(
            DeviceProfile.TEST_HARDWARE_DRY, true, "dry-run.invalid:502", null, DateTimeOffset.UnixEpoch,
            out _, out string codeMissingConsent));
        Assert.Equal(ProcessIoDryCommandGate.RefusedConsentMissing, codeMissingConsent);

        Assert.False(ProcessIoDryCommandGate.TryIssue(
            DeviceProfile.TEST_HARDWARE_DRY, true, "dry-run.invalid:502", "wrong phrase", DateTimeOffset.UnixEpoch,
            out _, out string codeMismatch));
        Assert.Equal(ProcessIoDryCommandGate.RefusedConsentMismatch, codeMismatch);
    }

    [Fact]
    public void A_Grant_Is_Single_Use()
    {
        Assert.True(ProcessIoDryCommandGate.TryIssue(
            DeviceProfile.TEST_HARDWARE_DRY, true, "dry-run.invalid:502", Consent, DateTimeOffset.UnixEpoch,
            out ProcessIoDryCommandGrant? grant, out _));

        var first = new FakeTransport();
        Assert.True(ProcessIoEquipmentBoundary.TryCreate(first, grant, TimeProvider.System, out _, out _));

        var second = new FakeTransport();
        Assert.False(ProcessIoEquipmentBoundary.TryCreate(second, grant, TimeProvider.System, out _, out string code));
        Assert.Equal(ProcessIoDryCommandGate.RefusedConsentMissing, code);
    }

    [Fact]
    public void A_Simulation_Transport_Is_Refused_As_A_Command_Backing()
    {
        Assert.True(ProcessIoDryCommandGate.TryIssue(
            DeviceProfile.TEST_HARDWARE_DRY, true, "dry-run.invalid:502", Consent, DateTimeOffset.UnixEpoch,
            out ProcessIoDryCommandGrant? grant, out _));

        Assert.False(ProcessIoEquipmentBoundary.TryCreate(
            new SimulatedProcessIoTransport(), grant, TimeProvider.System, out _, out string code));
        Assert.Equal(ProcessIoDryCommandGate.RefusedProfileNotDry, code);
    }

    [Fact]
    public void The_Endpoint_Is_Never_Exposed_Only_A_Digest_Prefix()
    {
        string fingerprint = ProcessIoDryCommandGate.Fingerprint("dry-run.invalid:502");
        Assert.StartsWith("endpoint-sha256-prefix:", fingerprint);
        Assert.DoesNotContain("dry-run.invalid", fingerprint);
        Assert.DoesNotContain("502", fingerprint);
        Assert.Equal(fingerprint, ProcessIoDryCommandGate.Fingerprint("dry-run.invalid:502"));
        Assert.NotEqual(fingerprint, ProcessIoDryCommandGate.Fingerprint("other.invalid:502"));
    }

    // ---------------------------------------------------------------- profile start policy

    [Fact]
    public void Dry_Profile_Starts_Only_For_The_Dry_Mode()
    {
        Assert.True(Wjss.Domain.ProfileStartPolicy.IsStartable(DeviceProfile.TEST_HARDWARE_DRY, false, true));
        Assert.False(Wjss.Domain.ProfileStartPolicy.IsStartable(DeviceProfile.TEST_HARDWARE_DRY, false, false));
        Assert.False(Wjss.Domain.ProfileStartPolicy.IsStartable(DeviceProfile.TEST_HARDWARE_DRY, true, false));
        Assert.False(Wjss.Domain.ProfileStartPolicy.IsStartable(DeviceProfile.TEST_HARDWARE, false, true));
        Assert.False(Wjss.Domain.ProfileStartPolicy.IsStartable(DeviceProfile.PRODUCTION, false, true));
        Assert.True(Wjss.Domain.ProfileStartPolicy.IsStartable(DeviceProfile.SIMULATOR, false, true));
    }

    // ---------------------------------------------------------------- pressure semantics

    [Fact]
    public void Pump_Ready_Requires_The_Outlet_Source_And_The_Setpoint()
    {
        var thresholds = new SequencingPressureThresholds();
        var ready = new PressureSample(20.0, PressureQuality.GOOD, false,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), PressureSample.PumpOutletSource);

        Assert.True(SequencingPressure.PumpReady(ready, thresholds));

        var inletSource = ready with { SourceId = "PUMP_INLET" };
        Assert.False(SequencingPressure.PumpReady(inletSource, thresholds));

        var belowSetpoint = ready with { Bar = 10.0 };
        Assert.False(SequencingPressure.PumpReady(belowSetpoint, thresholds));
    }

    [Theory]
    [InlineData(PressureQuality.BAD)]
    [InlineData(PressureQuality.STALE)]
    public void Pump_Ready_Refuses_Non_Good_Quality(PressureQuality quality)
    {
        var sample = new PressureSample(20.0, quality, false,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), PressureSample.PumpOutletSource);
        Assert.False(SequencingPressure.PumpReady(sample, new SequencingPressureThresholds()));
    }

    [Fact]
    public void Pump_Ready_Refuses_A_Future_Dated_Sample()
    {
        var now = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var thresholds = new SequencingPressureThresholds();
        var future = new PressureSample(20.0, PressureQuality.GOOD, false,
            now.AddMinutes(5), PressureSample.PumpOutletSource);

        // The base gate has no clock and accepts it; the time-aware gate refuses it.
        Assert.True(SequencingPressure.PumpReady(future, thresholds));
        Assert.False(SequencingPressure.PumpReady(future, thresholds, now, TimeSpan.Zero));

        var present = future with { At = now };
        Assert.True(SequencingPressure.PumpReady(present, thresholds, now, TimeSpan.Zero));
    }

    // ---------------------------------------------------------------- integrated dry cycle

    [Fact]
    public void Integrated_Dry_Cycle_Commands_Supervises_And_Cleans_Up()
    {
        var transport = new FakeTransport();
        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);

        // 1. Pump-ready composition reads AI-003 (input register 2) only.
        transport.InputRegisters[2] = 32767; // full scale -> 40 bar
        PressureSample? pumpOutlet = boundary.ReadPumpOutletPressure();
        Assert.NotNull(pumpOutlet);
        Assert.Equal(PressureSample.PumpOutletSource, pumpOutlet!.SourceId);
        Assert.Equal(40.0, pumpOutlet.Bar!.Value, 3);
        Assert.True(SequencingPressure.PumpReady(
            pumpOutlet, new SequencingPressureThresholds(),
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromMinutes(1)));

        // 2. Open IV6 (cannon 6): coil 27 energised.
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV6", CleaningPathValveCommandKind.Open));
        Assert.Equal(27, transport.CoilWrites[^1].Address);
        Assert.True(transport.CoilWrites[^1].Value);

        // 3. Field confirms open through the corrected Upper limit (FC02 25).
        transport.DiscreteInputs[25] = true;
        transport.DiscreteInputs[33] = false;
        ProcessIoValveObservation opened = boundary.Observe("IV6");
        Assert.True(opened.FieldConfirmed);
        Assert.Equal("IV_OPEN_FIELD_CONFIRMED", opened.AssessmentCode);

        // 4. Close IV6: coil 27 de-energised.
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV6", CleaningPathValveCommandKind.Close));
        Assert.False(transport.CoilWrites[^1].Value);

        // 5. Field confirms closed through the corrected Lower limit (FC02 33).
        transport.DiscreteInputs[25] = false;
        transport.DiscreteInputs[33] = true;
        ProcessIoValveObservation closed = boundary.Observe("IV6");
        Assert.True(closed.FieldConfirmed);
        Assert.Equal("IV_CLOSED_FIELD_CONFIRMED", closed.AssessmentCode);

        // 6. Safe Return cleanup is a no-op here (nothing left open) and writes nothing more.
        int writesBeforeCleanup = transport.CoilWrites.Count;
        boundary.SafeCleanupCloseAll();
        Assert.Equal(writesBeforeCleanup, transport.CoilWrites.Count);
    }

    [Fact]
    public void Integrated_Dry_Cycle_Leaves_Every_Valve_DeEnergised_After_Cleanup()
    {
        var transport = new FakeTransport();
        ProcessIoEquipmentBoundary boundary = CreateGranted(transport);

        for (int n = 1; n <= 8; n += 1)
        {
            boundary.RequestValve(new CleaningPathValveCommandRequest(
                ProcessIoIsolationValves.ValveId(n), CleaningPathValveCommandKind.Open));
        }

        Assert.Equal(8, transport.CoilWrites.Count);
        boundary.SafeCleanupCloseAll();

        Assert.Equal(16, transport.CoilWrites.Count);
        for (int coil = 22; coil <= 29; coil += 1)
        {
            Assert.False(transport.Coils[coil]);
        }

        Assert.All(boundary.CommandedState.Values, open => Assert.False(open));
    }
}
