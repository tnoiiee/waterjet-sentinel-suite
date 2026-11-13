using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// ACCELERATED-CLEANING-INTEGRATION-01 fake integrated dry-cycle matrix.
//
// Every test here is FAKE / DRY: the transport is a deterministic in-memory double, no socket is
// opened and no device is contacted. The matrix proves:
//
//   - Legacy maintained single-coil authority: OPEN = true, CLOSE = false, coil 21+n, FC05
//   - every command passes through the existing ProcessIoSession admission/dispatch lifecycle
//   - corrected feedback: Upper/open DI-020+n, Lower/closed DI-028+n
//   - FC01 read-back is process-image evidence only and never field confirmation
//   - observations carry truthful quality/freshness; nothing is fabricated GOOD
//   - safe cleanup is structured and never hides the primary failure
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

public sealed class ProcessIoDryCycleMatrixTests
{
    private const string Consent = "I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL";

    /// <summary>
    /// Every non-canonical valve spelling the strict parser must refuse. The canonical identities are
    /// exactly "IV1".."IV8": no zero padding, no surrounding whitespace, no sign and no alias.
    /// </summary>
    private static readonly string?[] InvalidValveIds =
    {
        "IV0", "IV9", "IV10", "IV", "iv1", "Iv1", "WJ1", "", "  ",
        "IV01", "IV001", " IV1", "IV1 ", "IV 1", "+IV1", "IV+1", null,
    };

    /// <summary>
    /// Outlet-pressure spellings that name no adopted row and must be refused, including the
    /// four-digit forms that unpadded concatenation would have produced.
    /// </summary>
    private static readonly string[] MalformedOutletPressureTags =
    {
        "AI-003+", "AI-012+", "AI-0010+", "AI-0011+", "AI-010", "AI-011", "AI-0004+",
        "ai-010+", "AI-010 +", "AI-010+ ",
    };

    /// <summary>
    /// Deterministic fake transport. Not the product Simulation transport: a physical command path must
    /// never be backed by it, so this double stands in for the adopted bench transport.
    /// </summary>
    private sealed class FakeTransport : IProcessIoTransport
    {
        public List<(string Device, int Address, bool Value)> CoilWrites { get; } = new();
        public Dictionary<int, bool> DiscreteInputs { get; } = new();
        public Dictionary<int, ushort> InputRegisters { get; } = new();
        public Dictionary<int, bool> Coils { get; } = new();
        public bool ThrowOnWrite { get; set; }

        /// <summary>Faults every read, so a read failure can be proven not to be field evidence.</summary>
        public bool ThrowOnRead { get; set; }

        /// <summary>
        /// Runs inside every read with that address, so a test can consume elapsed clock time exactly
        /// where a real device would — and can make one tag slow while another stays fast.
        /// </summary>
        public Action<int>? DuringRead { get; set; }

        public int ReadAttempts { get; private set; }

        public int NetworkAttempts => CoilWrites.Count;

        public bool ReadDiscreteInput(string deviceKey, int address)
        {
            OnRead(address);
            return DiscreteInputs.TryGetValue(address, out bool value) && value;
        }

        public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity)
        {
            OnRead(startAddress);
            return Enumerable.Range(startAddress, quantity).Select(a => Coils.TryGetValue(a, out bool v) && v).ToList();
        }

        public ushort ReadInputRegister(string deviceKey, int registerAddress)
        {
            OnRead(registerAddress);
            return InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;
        }

        public ushort ReadRegister(string deviceKey, int registerAddress)
            => InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;

        public bool ReadCoil(string deviceKey, int coilAddress)
        {
            OnRead(coilAddress);
            return Coils.TryGetValue(coilAddress, out bool value) && value;
        }

        public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity)
            => Enumerable.Range(startAddress, quantity)
                .Select(a => InputRegisters.TryGetValue(a, out ushort v) ? v : (ushort)0).ToList();

        public void WriteCoil(string deviceKey, int coilAddress, bool value)
        {
            if (ThrowOnWrite) throw new InvalidOperationException("FAKE_TRANSPORT_FAULT");
            CoilWrites.Add((deviceKey, coilAddress, value));
            Coils[coilAddress] = value;
        }

        private void OnRead(int address)
        {
            ReadAttempts++;
            if (ThrowOnRead) throw new IOException("FAKE_READ_TIMEOUT");
            DuringRead?.Invoke(address);
        }

        public void WriteSingleRegister(string deviceKey, int registerAddress, ushort value) { }

        public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values) { }
    }

    /// <summary>Builds the adopted catalogue from the real binding table, as the composition root does.</summary>
    private static Dictionary<string, ProcessIoCatalogueRow> BuildCatalogue()
    {
        var rows = new Dictionary<string, ProcessIoCatalogueRow>(StringComparer.Ordinal);
        string[] tags =
        [
            "AI-002+", "AI-003+",
            "AI-004+", "AI-005+", "AI-006+", "AI-007+", "AI-008+", "AI-009+", "AI-010+", "AI-011+",
            "DI-021", "DI-022", "DI-023", "DI-024", "DI-025", "DI-026", "DI-027", "DI-028",
            "DI-029", "DI-030", "DI-031", "DI-032", "DI-033", "DI-034", "DI-035", "DI-036",
            "DO-023", "DO-024", "DO-025", "DO-026", "DO-027", "DO-028", "DO-029", "DO-030",
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
                IoClass = binding.Area == ProcessIoArea.Coil ? "DO" : binding.Area == ProcessIoArea.DiscreteInput ? "DI" : "AI",
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

    private static ProcessIoWriteAuthorization Authorize()
    {
        Assert.True(ProcessIoDryCommandGate.TryAuthorize(
            DeviceProfile.TEST_HARDWARE_DRY,
            dryCommandMode: true,
            ownerLocalEndpoint: "dry-run.invalid:502",
            suppliedConsent: Consent,
            out ProcessIoWriteAuthorization? authorization,
            out string code));
        Assert.Equal(ProcessIoDryCommandGate.Granted, code);
        return authorization!;
    }

    private static (ProcessIoEquipmentBoundary Boundary, ProcessIoSession Session) CreateGranted(
        FakeTransport transport, TimeSpan? freshness = null, TimeProvider? time = null)
    {
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(DateTimeOffset.UnixEpoch);
        var boundary = new ProcessIoEquipmentBoundary(
            session, BuildCatalogue(), Authorize(), time: time, freshnessBudget: freshness ?? TimeSpan.FromMinutes(5));
        return (boundary, session);
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

        foreach (string? invalid in InvalidValveIds)
        {
            Assert.False(ProcessIoIsolationValves.TryParseValveId(invalid, out _));
        }
    }

    [Fact]
    public void Outlet_Pressure_Membership_Accepts_Exactly_The_Eight_Adopted_Tags()
    {
        string[] adopted =
        {
            "AI-004+", "AI-005+", "AI-006+", "AI-007+", "AI-008+", "AI-009+", "AI-010+", "AI-011+",
        };

        Assert.Equal(ProcessIoIsolationValves.Count, adopted.Length);

        for (int n = 1; n <= ProcessIoIsolationValves.Count; n += 1)
        {
            string tag = adopted[n - 1];

            // The generator is the membership predicate's only source, so both must agree exactly.
            Assert.Equal(tag, ProcessIoIsolationValves.OutletPressureTag(n));
            Assert.True(ProcessIoIsolationValves.IsOutletPressureTag(tag));

            // Every generated tag names an adopted input register, at FC04 input register 2+n.
            Assert.True(ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding));
            Assert.Equal(ProcessIoArea.InputRegister, binding.Area);
            Assert.Equal("READ", binding.Direction);
            Assert.Equal(2 + n, binding.Address);
        }

        // The decimal-width boundary is inside the adopted range, not past it.
        Assert.Equal("AI-009+", ProcessIoIsolationValves.OutletPressureTag(6));
        Assert.Equal("AI-010+", ProcessIoIsolationValves.OutletPressureTag(7));
        Assert.Equal("AI-011+", ProcessIoIsolationValves.OutletPressureTag(8));

        foreach (string malformed in MalformedOutletPressureTags)
        {
            Assert.False(ProcessIoIsolationValves.IsOutletPressureTag(malformed));
        }

        Assert.False(ProcessIoIsolationValves.IsOutletPressureTag(null));
        Assert.False(ProcessIoIsolationValves.IsOutletPressureTag(string.Empty));
        Assert.False(ProcessIoIsolationValves.IsOutletPressureTag("   "));
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
            Assert.Equal(19 + n, open.Address);
            Assert.Equal(27 + n, closed.Address);
            Assert.True(open.Address < closed.Address);
        }
    }

    // ---------------------------------------------------------------- session lifecycle reuse

    [Fact]
    public void Commands_Flow_Through_The_Session_Admission_And_Dispatch_Lifecycle()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        ProcessIoCommandOutcome outcome = Assert.Single(boundary.CommandLog);
        Assert.True(outcome.Dispatched);
        Assert.Equal("OK", outcome.AdmissionCode);
        Assert.Equal("DISPATCHED", outcome.DispatchCode);
        Assert.Equal(session.Generation, outcome.SessionGeneration);
        Assert.Contains(session.AuditTrail, entry => entry.StartsWith("ADMIT DO-023", StringComparison.Ordinal));
        Assert.Contains(session.AuditTrail, entry => entry.StartsWith("DISPATCH_DO DO-023 True", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Closed_Session_Refuses_The_Command_With_Zero_Writes()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session) = CreateGranted(transport);
        session.Close(DateTimeOffset.UnixEpoch);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        Assert.Empty(transport.CoilWrites);
        ProcessIoCommandOutcome outcome = Assert.Single(boundary.CommandLog);
        Assert.False(outcome.Dispatched);
        Assert.Equal(ProcessIoRefusals.SessionNotOpen, outcome.AdmissionCode);
    }

    [Fact]
    public void A_Stopped_Session_Refuses_The_Command_With_Zero_Writes()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session) = CreateGranted(transport);
        session.Stop(DateTimeOffset.UnixEpoch);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV2", CleaningPathValveCommandKind.Open));

        Assert.Empty(transport.CoilWrites);
        Assert.False(Assert.Single(boundary.CommandLog).Dispatched);
    }

    [Fact]
    public void Reopening_Advances_The_Generation_And_The_Boundary_Reports_It()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session) = CreateGranted(transport);
        int first = session.Generation;

        session.Close(DateTimeOffset.UnixEpoch);
        int second = session.Open(DateTimeOffset.UnixEpoch);

        Assert.True(second > first);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV3", CleaningPathValveCommandKind.Open));
        Assert.Equal(second, Assert.Single(boundary.CommandLog).SessionGeneration);
    }

    // ---------------------------------------------------------------- command polarity

    [Fact]
    public void Open_Writes_True_And_Close_Writes_False_On_The_Same_Coil()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

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
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest(
            ProcessIoIsolationValves.ValveId(ordinal), CleaningPathValveCommandKind.Open));

        Assert.Single(transport.CoilWrites);
        Assert.Equal(expectedCoil, transport.CoilWrites[0].Address);
        Assert.True(transport.CoilWrites[0].Value);
    }

    [Fact]
    public void Command_Is_Maintained_Not_A_Pulse()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV3", CleaningPathValveCommandKind.Open));

        Assert.Single(transport.CoilWrites);
        Assert.True(transport.Coils[24]);
    }

    [Fact]
    public void An_Invalid_Valve_Identity_Is_Refused_With_Zero_Writes()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV9", CleaningPathValveCommandKind.Open));

        Assert.Empty(transport.CoilWrites);
        ProcessIoCommandOutcome outcome = Assert.Single(boundary.CommandLog);
        Assert.False(outcome.Dispatched);
        Assert.Equal("IV_COMMAND_REFUSED_VALVE_IDENTITY_INVALID", outcome.AdmissionCode);
    }

    [Fact]
    public void A_Transport_Fault_Is_Captured_Not_Propagated()
    {
        var transport = new FakeTransport { ThrowOnWrite = true };
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        ProcessIoCommandOutcome outcome = Assert.Single(boundary.CommandLog);
        Assert.False(outcome.Dispatched);
        Assert.True(outcome.TransportFaulted);
        Assert.Equal(ProcessIoRefusals.ObservationTransportFault, outcome.DispatchCode);
    }

    // ---------------------------------------------------------------- observation quality

    [Fact]
    public void An_Observation_Carries_Generation_Timestamps_And_Provenance()
    {
        var transport = new FakeTransport();
        transport.InputRegisters[2] = 32767;
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session) = CreateGranted(transport);

        ProcessIoPhysicalObservation observation = boundary.Observe("AI-003+");

        Assert.Equal("AI-003+", observation.Tag);
        Assert.Equal(32767, observation.RawValue);
        Assert.Equal(40.0, observation.EngineeringValue!.Value, 3);
        Assert.Equal("bar", observation.Unit);
        Assert.Equal(ProcessIoObservationQuality.GOOD, observation.Quality);
        Assert.True(observation.Fresh);
        Assert.Equal(session.Generation, observation.SourceGeneration);
        Assert.Equal(ProcessIoObservationProvenance.PHYSICAL_READ, observation.Provenance);
        Assert.True(observation.ReceivedAt >= observation.ObservedAt);
    }

    [Fact]
    public void A_Simulated_Transport_Is_Never_Accepted_As_Process_Evidence()
    {
        var simulated = new SimulatedProcessIoTransport();
        simulated.SeedInputRegister(ProcessIoDevices.RackT8, 2, 32767);
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, simulated);
        session.Open(DateTimeOffset.UnixEpoch);
        var boundary = new ProcessIoEquipmentBoundary(
            session, BuildCatalogue(), Authorize(), null, TimeSpan.FromMinutes(5));

        ProcessIoPhysicalObservation observation = boundary.Observe("AI-003+");
        Assert.Equal(ProcessIoObservationProvenance.SIMULATED_READ, observation.Provenance);

        // The Pump-ready authority refuses it: a simulated value is never physical evidence.
        Assert.Null(boundary.ReadPumpOutletPressure());
    }

    [Fact]
    public void A_Closed_Session_Produces_A_Refused_Observation_Not_A_Good_One()
    {
        var transport = new FakeTransport();
        transport.InputRegisters[2] = 32767;
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session) = CreateGranted(transport);
        session.Close(DateTimeOffset.UnixEpoch);

        ProcessIoPhysicalObservation observation = boundary.Observe("AI-003+");

        Assert.NotEqual(ProcessIoObservationQuality.GOOD, observation.Quality);
        Assert.False(observation.Fresh);
        Assert.Equal(ProcessIoObservationProvenance.NOT_OBSERVED, observation.Provenance);
        Assert.False(observation.IsUsable);
        Assert.Null(boundary.ReadPumpOutletPressure());
    }

    [Fact]
    public void A_Zero_Length_Freshness_Budget_Makes_The_Observation_Stale()
    {
        var transport = new FakeTransport();
        transport.InputRegisters[2] = 32767;
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport, TimeSpan.Zero);

        ProcessIoPhysicalObservation observation = boundary.Observe("AI-003+");

        // The claimed instant and the received instant differ, so nothing is fresh at zero budget.
        Assert.False(observation.Fresh);
        Assert.False(observation.IsUsable);
        Assert.Null(boundary.ReadPumpOutletPressure());
    }

    // ---------------------------------------------------------------- observation timestamp bracketing
    //
    // C# evaluates method arguments before the call, so the boundary must not sample an observation
    // instant and a receive instant and hand both to the session: both would be taken BEFORE the
    // transport read, and a slow read would be accepted as current. The boundary therefore hands its
    // clock to the session and lets the session bracket the read. These tests drive a controllable
    // clock to prove the bracket is real on every path the boundary exposes.

    [Fact]
    public void Analog_Boundary_Fast_Read_Is_Fresh_And_Good()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.InputRegisters[2] = 24000;
        transport.DuringRead = address =>
        {
            if (address == 2) clock.Advance(TimeSpan.FromMilliseconds(5));
        };

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);

        ProcessIoPhysicalObservation observation = boundary.Observe("AI-003+");

        Assert.True(observation.Fresh);
        Assert.Equal(ProcessIoObservationQuality.GOOD, observation.Quality);
        Assert.Equal(TimeSpan.FromMilliseconds(5), observation.ReceivedAt - observation.ObservedAt);
        Assert.NotNull(boundary.ReadPumpOutletPressure());
    }

    [Fact]
    public void Analog_Boundary_Slow_Read_Is_Stale_And_Refused()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.InputRegisters[2] = 24000;
        transport.DuringRead = address =>
        {
            if (address == 2) clock.Advance(TimeSpan.FromMilliseconds(60));
        };

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);

        ProcessIoPhysicalObservation observation = boundary.Observe("AI-003+");

        Assert.False(observation.Fresh);
        Assert.Equal(ProcessIoObservationQuality.STALE, observation.Quality);
        Assert.Equal(TimeSpan.FromMilliseconds(60), observation.ReceivedAt - observation.ObservedAt);
        Assert.False(observation.IsUsable);

        // The Pump-ready authority refuses a stale reading outright: no sample is produced at all.
        Assert.Null(boundary.ReadPumpOutletPressure());
    }

    [Fact]
    public void Upper_Open_Fast_Read_Confirms_Field_Position()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = true;
        transport.DiscreteInputs[28] = false;
        transport.DuringRead = address =>
        {
            if (address is 20 or 28) clock.Advance(TimeSpan.FromMilliseconds(5));
        };

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        ProcessIoValveObservation observation = boundary.ObserveValve("IV1");

        Assert.True(observation.UpperLimitDetected);
        Assert.False(observation.LowerLimitDetected);
        Assert.True(observation.FieldConfirmed);
        Assert.Equal("IV_OPEN_FIELD_CONFIRMED", observation.AssessmentCode);
    }

    [Fact]
    public void Upper_Open_Slow_Read_Refuses_Field_Confirmation()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = true;
        transport.DiscreteInputs[28] = false;
        transport.DuringRead = address =>
        {
            if (address == 20) clock.Advance(TimeSpan.FromMilliseconds(60));
        };

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        ProcessIoValveObservation observation = boundary.ObserveValve("IV1");

        // The bit arrived OPEN, but too late to be current: it is not field evidence of position.
        Assert.False(observation.UpperLimitDetected);
        Assert.False(observation.FieldConfirmed);
        Assert.Equal("IV_FIELD_NOT_CONFIRMED", observation.AssessmentCode);
    }

    [Fact]
    public void Lower_Closed_Fast_Read_Confirms_Field_Position()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = false;
        transport.DiscreteInputs[28] = true;
        transport.DuringRead = address =>
        {
            if (address is 20 or 28) clock.Advance(TimeSpan.FromMilliseconds(5));
        };

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Close));

        ProcessIoValveObservation observation = boundary.ObserveValve("IV1");

        Assert.True(observation.LowerLimitDetected);
        Assert.False(observation.UpperLimitDetected);
        Assert.True(observation.FieldConfirmed);
        Assert.Equal("IV_CLOSED_FIELD_CONFIRMED", observation.AssessmentCode);
    }

    [Fact]
    public void Lower_Closed_Slow_Read_Refuses_Field_Confirmation()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = false;
        transport.DiscreteInputs[28] = true;
        transport.DuringRead = address =>
        {
            if (address == 28) clock.Advance(TimeSpan.FromMilliseconds(60));
        };

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Close));

        ProcessIoValveObservation observation = boundary.ObserveValve("IV1");

        Assert.False(observation.LowerLimitDetected);
        Assert.False(observation.FieldConfirmed);
        Assert.Equal("IV_FIELD_NOT_CONFIRMED", observation.AssessmentCode);
    }

    [Fact]
    public void Boundary_Freshness_Budget_Is_Inclusive_At_Its_Exact_Boundary()
    {
        var atClock = new TestClock();
        var atTransport = new FakeTransport();
        atTransport.InputRegisters[2] = 24000;
        atTransport.DuringRead = address =>
        {
            if (address == 2) atClock.Advance(TimeSpan.FromMilliseconds(20));
        };
        (ProcessIoEquipmentBoundary atBoundary, _) =
            CreateGranted(atTransport, TimeSpan.FromMilliseconds(20), atClock);

        ProcessIoPhysicalObservation atObservation = atBoundary.Observe("AI-003+");
        Assert.True(atObservation.Fresh);
        Assert.NotNull(atBoundary.ReadPumpOutletPressure());

        var pastClock = new TestClock();
        var pastTransport = new FakeTransport();
        pastTransport.InputRegisters[2] = 24000;
        pastTransport.DuringRead = address =>
        {
            if (address == 2) pastClock.Advance(TimeSpan.FromMilliseconds(21));
        };
        (ProcessIoEquipmentBoundary pastBoundary, _) =
            CreateGranted(pastTransport, TimeSpan.FromMilliseconds(20), pastClock);

        ProcessIoPhysicalObservation pastObservation = pastBoundary.Observe("AI-003+");
        Assert.False(pastObservation.Fresh);
        Assert.Null(pastBoundary.ReadPumpOutletPressure());
    }

    [Fact]
    public void Boundary_Transport_Fault_Is_Bad_And_Never_Field_Confirmation()
    {
        var clock = new TestClock();
        var transport = new FakeTransport
        {
            ThrowOnRead = true,
        };

        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport, TimeSpan.FromSeconds(2), clock);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        ProcessIoValveObservation observation = boundary.ObserveValve("IV1");

        Assert.False(observation.UpperLimitDetected);
        Assert.False(observation.LowerLimitDetected);
        Assert.False(observation.FieldConfirmed);

        ProcessIoPhysicalObservation faulted = boundary.Observe("AI-003+");
        Assert.Equal(ProcessIoObservationQuality.BAD, faulted.Quality);
        Assert.Equal(ProcessIoObservationProvenance.NOT_OBSERVED, faulted.Provenance);
        Assert.Null(boundary.ReadPumpOutletPressure());
    }

    [Fact]
    public void Boundary_Generation_Remains_Current_Across_A_Slow_Read()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.InputRegisters[2] = 24000;
        transport.DuringRead = address =>
        {
            if (address == 2) clock.Advance(TimeSpan.FromMilliseconds(60));
        };

        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);

        ProcessIoPhysicalObservation observation = boundary.Observe("AI-003+");

        // Staleness is a freshness verdict, never a generation change.
        Assert.False(observation.Fresh);
        Assert.Equal(session.Generation, observation.SourceGeneration);
    }

    [Fact]
    public void Pump_Ready_Cannot_Be_Satisfied_By_A_Stale_Physical_Ai003()
    {
        var thresholds = new SequencingPressureThresholds(1.0, 15.0, 15.0);

        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.InputRegisters[2] = 24000;
        transport.DuringRead = address =>
        {
            if (address == 2) clock.Advance(TimeSpan.FromMilliseconds(60));
        };

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);

        // A stale reading produces no sample, so the existing Pump-ready rule can never accept it.
        PressureSample? sample = boundary.ReadPumpOutletPressure();
        Assert.Null(sample);
        Assert.False(SequencingPressure.PumpReady(sample, thresholds));

        // Control: the identical reading, delivered in time, does produce a Pump-ready sample.
        var fastClock = new TestClock();
        var fastTransport = new FakeTransport();
        fastTransport.InputRegisters[2] = 24000;
        (ProcessIoEquipmentBoundary fastBoundary, _) =
            CreateGranted(fastTransport, TimeSpan.FromMilliseconds(20), fastClock);

        PressureSample? fastSample = fastBoundary.ReadPumpOutletPressure();
        Assert.NotNull(fastSample);
        Assert.True(SequencingPressure.PumpReady(fastSample, thresholds));
    }

    [Fact]
    public void Valve_Pressure_Supervision_Cannot_Accept_Stale_Pressure()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.InputRegisters[4] = 16384;
        transport.DuringRead = address =>
        {
            if (address == 4) clock.Advance(TimeSpan.FromMilliseconds(60));
        };

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromMilliseconds(20), clock);

        Assert.Null(boundary.ReadValveOutletPressure("IV2"));

        // Control: the same register, delivered in time, is accepted as valve-outlet evidence.
        var fastClock = new TestClock();
        var fastTransport = new FakeTransport();
        fastTransport.InputRegisters[4] = 16384;
        (ProcessIoEquipmentBoundary fastBoundary, _) =
            CreateGranted(fastTransport, TimeSpan.FromMilliseconds(20), fastClock);

        PressureSample? fastSample = fastBoundary.ReadValveOutletPressure("IV2");
        Assert.NotNull(fastSample);
        Assert.Equal(PressureSample.ValveOutletSource("IV2"), fastSample!.SourceId);
    }

    [Fact]
    public void Observation_Paths_Perform_No_Write()
    {
        var clock = new TestClock();
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = true;
        transport.DiscreteInputs[28] = false;
        transport.InputRegisters[2] = 24000;

        (ProcessIoEquipmentBoundary boundary, _) =
            CreateGranted(transport, TimeSpan.FromSeconds(2), clock);

        // No valve was commanded, so these are pure observation paths.
        boundary.Observe("AI-003+");
        boundary.Observe("DI-021");
        boundary.ReadPumpOutletPressure();
        boundary.ReadUpperLimitDetected("IV1");
        boundary.ReadLowerLimitDetected("IV1");
        boundary.ReadValveOutletPressure("IV2");

        Assert.Empty(transport.CoilWrites);
        Assert.True(transport.ReadAttempts > 0);
    }

    /// <summary>A clock the test moves on purpose, so freshness is a fact rather than a race.</summary>
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;

        public void Advance(TimeSpan delta) => UtcNow += delta;
    }

    // ---------------------------------------------------------------- separated evidence

    [Fact]
    public void Upper_Open_Lower_Closed_And_Pressure_Are_Three_Separate_Evidence_Kinds()
    {
        var transport = new FakeTransport();
        transport.DiscreteInputs[21] = true;
        transport.DiscreteInputs[29] = false;
        transport.InputRegisters[4] = 16384;

        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV2", CleaningPathValveCommandKind.Open));

        ProcessIoValveObservation observation = boundary.ObserveValve("IV2");

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
        // The output image says energised, but no field feedback exists.
        transport.Coils[22] = true;
        transport.DiscreteInputs[20] = false;
        transport.DiscreteInputs[28] = false;

        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        ProcessIoValveObservation observation = boundary.ObserveValve("IV1");

        Assert.True(observation.ProcessImageReadBack);
        Assert.False(observation.UpperLimitDetected);
        Assert.False(observation.FieldConfirmed);
        Assert.Equal("IV_FIELD_NOT_CONFIRMED", observation.AssessmentCode);
    }

    [Fact]
    public void Contradictory_Feedback_Is_Never_Resolved_In_Favour_Of_The_Command()
    {
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = true;
        transport.DiscreteInputs[28] = true;

        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Close));

        ProcessIoValveObservation observation = boundary.ObserveValve("IV1");

        Assert.False(observation.FieldConfirmed);
        Assert.Equal("IV_FEEDBACK_CONTRADICTORY_BOTH_LIMITS", observation.AssessmentCode);
    }

    [Fact]
    public void Observation_Without_A_Dispatched_Command_Is_Not_Confirmed()
    {
        var transport = new FakeTransport();
        transport.DiscreteInputs[20] = true;

        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);
        ProcessIoValveObservation observation = boundary.ObserveValve("IV1");

        Assert.False(observation.CommandDispatched);
        Assert.False(observation.FieldConfirmed);
        Assert.Equal("IV_OBSERVATION_NO_DISPATCHED_COMMAND", observation.AssessmentCode);
    }

    // ---------------------------------------------------------------- structured cleanup

    [Fact]
    public void Safe_Cleanup_Writes_False_And_Reports_Complete()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV5", CleaningPathValveCommandKind.Open));

        ProcessIoCleanupResult cleanup = boundary.SafeCleanupCloseAll();

        Assert.Equal(2, cleanup.AttemptedValveIds.Count);
        Assert.Equal(2, cleanup.AdmittedValveIds.Count);
        Assert.Equal(2, cleanup.ClosedValveIds.Count);
        Assert.Empty(cleanup.RefusedValves);
        Assert.Empty(cleanup.TransportFailures);
        Assert.Empty(cleanup.RemainingUnknownState);
        Assert.True(cleanup.Complete);

        Assert.False(transport.Coils[22]);
        Assert.False(transport.Coils[26]);
        Assert.Equal(4, transport.CoilWrites.Count);
    }

    [Fact]
    public void Safe_Cleanup_Never_Touches_A_Valve_It_Did_Not_Open()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV4", CleaningPathValveCommandKind.Open));
        boundary.SafeCleanupCloseAll();

        Assert.All(transport.CoilWrites, write => Assert.Equal(25, write.Address));
    }

    [Fact]
    public void Safe_Cleanup_Reports_Transport_Failures_And_Remaining_Unknown_State()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        // Every subsequent write faults: cleanup must report it, not throw and not claim success.
        transport.ThrowOnWrite = true;
        ProcessIoCleanupResult cleanup = boundary.SafeCleanupCloseAll();

        Assert.Equal("IV1", Assert.Single(cleanup.AttemptedValveIds));
        Assert.Single(cleanup.AdmittedValveIds);
        Assert.Empty(cleanup.ClosedValveIds);
        Assert.Contains("IV1", cleanup.TransportFailures.Keys);
        Assert.Contains("IV1", cleanup.RemainingUnknownState);
        Assert.False(cleanup.Complete);
    }

    [Fact]
    public void Safe_Cleanup_After_Close_Reports_Refusals_Instead_Of_Claiming_Success()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, ProcessIoSession session) = CreateGranted(transport);

        boundary.RequestValve(new CleaningPathValveCommandRequest("IV2", CleaningPathValveCommandKind.Open));
        session.Close(DateTimeOffset.UnixEpoch);

        ProcessIoCleanupResult cleanup = boundary.SafeCleanupCloseAll();

        Assert.Equal("IV2", Assert.Single(cleanup.AttemptedValveIds));
        Assert.Empty(cleanup.ClosedValveIds);
        Assert.Equal(ProcessIoRefusals.SessionNotOpen, cleanup.RefusedValves["IV2"]);
        Assert.Contains("IV2", cleanup.RemainingUnknownState);
        Assert.False(cleanup.Complete);
    }

    // ---------------------------------------------------------------- authority refusals

    [Theory]
    [InlineData(DeviceProfile.SIMULATOR)]
    [InlineData(DeviceProfile.TEST_HARDWARE)]
    [InlineData(DeviceProfile.TEST_HARDWARE_READ_ONLY)]
    public void Only_The_Dry_Profile_Is_Authorized(DeviceProfile profile)
    {
        Assert.False(ProcessIoDryCommandGate.TryAuthorize(
            profile, true, "dry-run.invalid:502", Consent,
            out ProcessIoWriteAuthorization? authorization, out string code));
        Assert.Null(authorization);
        Assert.Equal(ProcessIoDryCommandGate.RefusedProfileNotDry, code);
    }

    [Fact]
    public void Production_Is_Refused_Before_Any_Other_Check()
    {
        Assert.False(ProcessIoDryCommandGate.TryAuthorize(
            DeviceProfile.PRODUCTION, true, null, null,
            out ProcessIoWriteAuthorization? authorization, out string code));
        Assert.Null(authorization);
        Assert.Equal(ProcessIoDryCommandGate.RefusedProductionRequested, code);
    }

    [Fact]
    public void A_Missing_Endpoint_Or_Consent_Is_Refused()
    {
        Assert.False(ProcessIoDryCommandGate.TryAuthorize(
            DeviceProfile.TEST_HARDWARE_DRY, true, "  ", Consent, out _, out string missingEndpoint));
        Assert.Equal(ProcessIoDryCommandGate.RefusedEndpointMissing, missingEndpoint);

        Assert.False(ProcessIoDryCommandGate.TryAuthorize(
            DeviceProfile.TEST_HARDWARE_DRY, true, "dry-run.invalid:502", null, out _, out string missingConsent));
        Assert.Equal(ProcessIoDryCommandGate.RefusedConsentMissing, missingConsent);

        Assert.False(ProcessIoDryCommandGate.TryAuthorize(
            DeviceProfile.TEST_HARDWARE_DRY, true, "dry-run.invalid:502", "wrong", out _, out string mismatch));
        Assert.Equal(ProcessIoDryCommandGate.RefusedConsentMismatch, mismatch);
    }

    [Fact]
    public void The_Endpoint_Is_Never_Exposed_Only_A_Digest_Prefix()
    {
        string fingerprint = ProcessIoDryCommandGate.EndpointFingerprint("dry-run.invalid:502");
        Assert.StartsWith("endpoint-sha256-prefix:", fingerprint);
        Assert.DoesNotContain("dry-run.invalid", fingerprint);
        Assert.Equal(fingerprint, ProcessIoDryCommandGate.EndpointFingerprint("dry-run.invalid:502"));
        Assert.NotEqual(fingerprint, ProcessIoDryCommandGate.EndpointFingerprint("other.invalid:502"));
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
        Assert.False(SequencingPressure.PumpReady(ready with { SourceId = "PUMP_INLET" }, thresholds));
        Assert.False(SequencingPressure.PumpReady(ready with { Bar = 10.0 }, thresholds));
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

        Assert.True(SequencingPressure.PumpReady(future, thresholds));
        Assert.False(SequencingPressure.PumpReady(future, thresholds, now, TimeSpan.Zero));
        Assert.True(SequencingPressure.PumpReady(future with { At = now }, thresholds, now, TimeSpan.Zero));
    }

    // ---------------------------------------------------------------- integrated dry cycle

    [Fact]
    public void Integrated_Dry_Cycle_Commands_Supervises_And_Cleans_Up()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

        // 1. Pump-ready composition reads AI-003 (input register 2) only.
        transport.InputRegisters[2] = 32767;
        PressureSample? pumpOutlet = boundary.ReadPumpOutletPressure();
        Assert.NotNull(pumpOutlet);
        Assert.Equal(PressureSample.PumpOutletSource, pumpOutlet!.SourceId);
        Assert.Equal(40.0, pumpOutlet.Bar!.Value, 3);
        Assert.True(SequencingPressure.PumpReady(
            pumpOutlet, new SequencingPressureThresholds(),
            DateTimeOffset.UtcNow.AddSeconds(1), TimeSpan.FromMinutes(1)));

        // 2. Open IV6 (cannon 6): coil 27 energised.
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV6", CleaningPathValveCommandKind.Open));
        Assert.Equal(27, transport.CoilWrites[^1].Address);
        Assert.True(transport.CoilWrites[^1].Value);

        // 3. Field confirms open through the corrected Upper limit (FC02 25).
        transport.DiscreteInputs[25] = true;
        transport.DiscreteInputs[33] = false;
        ProcessIoValveObservation opened = boundary.ObserveValve("IV6");
        Assert.True(opened.FieldConfirmed);
        Assert.Equal("IV_OPEN_FIELD_CONFIRMED", opened.AssessmentCode);

        // 4. Close IV6: coil 27 de-energised.
        boundary.RequestValve(new CleaningPathValveCommandRequest("IV6", CleaningPathValveCommandKind.Close));
        Assert.False(transport.CoilWrites[^1].Value);

        // 5. Field confirms closed through the corrected Lower limit (FC02 33).
        transport.DiscreteInputs[25] = false;
        transport.DiscreteInputs[33] = true;
        ProcessIoValveObservation closed = boundary.ObserveValve("IV6");
        Assert.True(closed.FieldConfirmed);
        Assert.Equal("IV_CLOSED_FIELD_CONFIRMED", closed.AssessmentCode);

        // 6. Nothing is left open, so cleanup attempts nothing and writes nothing more.
        int writesBeforeCleanup = transport.CoilWrites.Count;
        ProcessIoCleanupResult cleanup = boundary.SafeCleanupCloseAll();
        Assert.Equal(writesBeforeCleanup, transport.CoilWrites.Count);
        Assert.True(cleanup.Complete);
    }

    [Fact]
    public void Integrated_Dry_Cycle_Leaves_Every_Valve_DeEnergised_After_Cleanup()
    {
        var transport = new FakeTransport();
        (ProcessIoEquipmentBoundary boundary, _) = CreateGranted(transport);

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
    }
}
