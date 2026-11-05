using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

// PUMP-COMMAND-PRODUCT-INTEGRATION: the focused Pump lifecycle matrix.
//
// Every test here is FAKE. The transport is a deterministic in-memory double: no socket is opened, no
// device is contacted, and no value is a hardware observation. The matrix proves the commissioned
// command contract, the fail-closed admission, the AI-003-only readiness rule, the CORRECTED Stop
// order, and the cleanup obligations.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

public sealed class ProcessIoPumpLifecycleTests
{
    private const string Owner = "owner-test";
    private const int CoilSelect1 = 0;
    private const int CoilSelect2 = 1;
    private const int CoilEnable = 2;
    private const int CoilRun = 6;
    private const int InputRunning = 3;
    private const int InputFault = 4;
    private const int RegisterPumpOutlet = 2;
    private const int RegisterPumpInlet = 1;

    /// <summary>Raw word whose converted outlet pressure is just BELOW the 15 bar setpoint.</summary>
    private const ushort RawJustBelow15Bar = 16383;

    /// <summary>Raw word whose converted outlet pressure is just ABOVE the 15 bar setpoint.</summary>
    private const ushort RawJustAbove15Bar = 16384;

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Supervision = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Freshness = TimeSpan.FromMinutes(5);

    // ---------------------------------------------------------------- scaffolding

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 5, 6, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    /// <summary>Deterministic transport double. Never the Product Simulation transport.</summary>
    private sealed class FakeTransport : IProcessIoTransport
    {
        public List<(string Device, int Address, bool Value)> CoilWrites { get; } = new();
        public List<(int Address, ushort Value)> RegisterWrites { get; } = new();
        public List<int> RegisterReads { get; } = new();
        public Dictionary<int, bool> DiscreteInputs { get; } = new();
        public Dictionary<int, ushort> InputRegisters { get; } = new();
        public bool ThrowOnWrite { get; set; }
        public bool ThrowOnAnalogWrite { get; set; }

        public int NetworkAttempts => CoilWrites.Count + RegisterWrites.Count;

        public bool ReadDiscreteInput(string deviceKey, int address) =>
            DiscreteInputs.TryGetValue(address, out bool value) && value;

        public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity) =>
            Enumerable.Range(startAddress, quantity)
                .Select(address => DiscreteInputs.TryGetValue(address, out bool value) && value)
                .ToList();

        public ushort ReadInputRegister(string deviceKey, int registerAddress)
        {
            RegisterReads.Add(registerAddress);
            return InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;
        }

        public ushort ReadRegister(string deviceKey, int registerAddress)
        {
            RegisterReads.Add(registerAddress);
            return InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;
        }

        public bool ReadCoil(string deviceKey, int coilAddress) =>
            CoilWrites.Any(write => write.Address == coilAddress && write.Value);

        public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity) =>
            Enumerable.Range(startAddress, quantity)
                .Select(address => InputRegisters.TryGetValue(address, out ushort value) ? value : (ushort)0)
                .ToList();

        public void WriteCoil(string deviceKey, int coilAddress, bool value)
        {
            if (ThrowOnWrite) throw new InvalidOperationException("FAKE_TRANSPORT_FAULT");
            CoilWrites.Add((deviceKey, coilAddress, value));
        }

        public void WriteSingleRegister(string deviceKey, int registerAddress, ushort value)
        {
            if (ThrowOnWrite || ThrowOnAnalogWrite) throw new InvalidOperationException("FAKE_TRANSPORT_FAULT");
            RegisterWrites.Add((registerAddress, value));
        }

        public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values)
        {
            if (ThrowOnWrite || ThrowOnAnalogWrite) throw new InvalidOperationException("FAKE_TRANSPORT_FAULT");
            for (int index = 0; index < values.Count; index += 1)
            {
                RegisterWrites.Add((startAddress + index, values[index]));
            }
        }
    }

    /// <summary>
    /// A pressure-only boundary double. It lets the lifecycle rule be exercised at an exact pressure
    /// value that the raw conversion cannot land on, without a second Production implementation.
    /// </summary>
    private sealed class PressureOnlyBoundary : IPumpCommandBoundary
    {
        private readonly FakeTime _time;

        public PressureOnlyBoundary(FakeTime time, PumpFeedbackConfiguration feedback)
        {
            _time = time;
            Feedback = feedback;
            Running = true;
        }

        public PumpFeedbackConfiguration Feedback { get; }

        public int SessionGeneration => 1;

        public bool Running { get; set; }

        public bool FaultActive { get; set; }

        public double? OutletBar { get; set; }

        public PressureQuality OutletQuality { get; set; } = PressureQuality.GOOD;

        public IReadOnlyList<PumpCommandOutcome> SelectPump(int selection) => new[]
        {
            Record(PumpCommandContract.SelectPumpOneTag, true),
            Record(PumpCommandContract.SelectPumpTwoTag, !selection.Equals(1)),
        };

        public PumpCommandOutcome SetEnableInverter(bool value) => Record(PumpCommandContract.EnableInverterTag, value);

        public PumpCommandOutcome SetRunPump(bool value) => Record(PumpCommandContract.RunPumpTag, value);

        public PumpAnalogCommandOutcome SetSpeedCommand(double engineeringMilliamps) => new()
        {
            Tag = PumpCommandContract.SpeedReferenceTag,
            Address = 0,
            RequestedEngineering = engineeringMilliamps,
            Admitted = true,
            Dispatched = true,
            AdmissionCode = PumpCommandRefusals.Granted,
            DispatchCode = PumpCommandRefusals.Granted,
            SessionGeneration = 1,
            At = _time.GetUtcNow(),
        };

        public PumpFeedbackRead ReadRunning() => new()
        {
            Tag = PumpCommandContract.RunningFeedbackTag,
            Commissioned = Feedback.RunningCommissioned,
            Code = PumpCommandRefusals.Granted,
            RawBit = Running,
            Active = Running,
            Current = true,
            SessionGeneration = 1,
            At = _time.GetUtcNow(),
        };

        public PumpFeedbackRead ReadFault() => new()
        {
            Tag = PumpCommandContract.FaultFeedbackTag,
            Commissioned = Feedback.FaultCommissioned,
            Code = PumpCommandRefusals.Granted,
            RawBit = FaultActive,
            Active = FaultActive,
            Current = true,
            SessionGeneration = 1,
            At = _time.GetUtcNow(),
        };

        public PumpPressureRead ReadPumpOutletPressure()
        {
            ProcessIoObservationQuality quality = OutletQuality switch
            {
                PressureQuality.BAD => ProcessIoObservationQuality.BAD,
                PressureQuality.STALE => ProcessIoObservationQuality.STALE,
                _ => ProcessIoObservationQuality.GOOD,
            };

            return new PumpPressureRead
            {
                Tag = PumpCommandContract.PumpOutletPressureTag,
                Accepted = OutletBar is not null && OutletQuality == PressureQuality.GOOD,
                Code = PumpCommandRefusals.Granted,
                Quality = quality,
                Sample = OutletBar is { } bar
                    ? new PressureSample(
                        bar,
                        OutletQuality,
                        OutletQuality == PressureQuality.STALE,
                        _time.GetUtcNow(),
                        PressureSample.PumpOutletSource)
                    : null,
                SessionGeneration = 1,
                At = _time.GetUtcNow(),
            };
        }

        private PumpCommandOutcome Record(string tag, bool value) => new()
        {
            Tag = tag,
            Address = 0,
            RequestedValue = value,
            Admitted = true,
            Dispatched = true,
            AdmissionCode = PumpCommandRefusals.Granted,
            DispatchCode = PumpCommandRefusals.Granted,
            SessionGeneration = 1,
            At = _time.GetUtcNow(),
        };
    }

    private sealed class Rig
    {
        public required FakeTransport Transport { get; init; }
        public required FakeTime Time { get; init; }
        public required ProcessIoSession Session { get; init; }
        public required PumpCommandOwnership Ownership { get; init; }
        public required ProcessIoPumpLifecycle Lifecycle { get; init; }
        public required PumpLifecycleConfiguration Configuration { get; init; }
    }

    private static PumpFeedbackConfiguration Commissioned() => new()
    {
        RunningPolarity = PumpFeedbackPolarity.ACTIVE_HIGH,
        FaultPolarity = PumpFeedbackPolarity.ACTIVE_HIGH,
    };

    private static PumpLifecycleConfiguration ConfigurationFor(PumpFeedbackConfiguration feedback, double target = 4.0) => new()
    {
        PumpReadySetpointBar = PumpLifecycleConfiguration.OwnerInitialPumpReadySetpointBar,
        PressureFreshnessBudget = Freshness,
        RunningFeedbackTimeout = Supervision,
        PumpReadyTimeout = Supervision,
        StoppedFeedbackTimeout = Supervision,
        Ramp = PumpSpeedRampConfiguration.OwnerInitial(target) with { Interval = Interval },
        Feedback = feedback,
    };

    private static Dictionary<string, ProcessIoCatalogueRow> Catalogue()
    {
        string[] tags =
        [
            PumpCommandContract.SelectPumpOneTag, PumpCommandContract.SelectPumpTwoTag,
            PumpCommandContract.EnableInverterTag, PumpCommandContract.RunPumpTag,
            PumpCommandContract.SpeedReferenceTag,
            PumpCommandContract.RunningFeedbackTag, PumpCommandContract.FaultFeedbackTag,
            PumpCommandContract.PumpOutletPressureTag, PumpCommandContract.PumpInletPressureTag,
        ];

        var rows = new Dictionary<string, ProcessIoCatalogueRow>(StringComparer.Ordinal);
        foreach (string tag in tags)
        {
            Assert.True(ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding));
            string ioClass = binding.Area switch
            {
                ProcessIoArea.Coil => "DO",
                ProcessIoArea.DiscreteInput => "DI",
                ProcessIoArea.InputRegister => "AI",
                _ => "AO",
            };
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
                IoClass = ioClass,
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
            ownerLocalEndpoint: "pump-only.invalid:502",
            suppliedConsent: ProcessIoConsentPhrases.PhysicalDoSingle,
            out ProcessIoWriteAuthorization? authorization,
            out string code));
        Assert.Equal(ProcessIoDryCommandGate.Granted, code);
        return authorization!;
    }

    /// <summary>
    /// Builds a granted lifecycle. The ownership instance the lifecycle uses is the one exposed, so a
    /// test can assert on the real channel rather than on a copy.
    /// </summary>
    private static Rig CreateWired(PumpFeedbackConfiguration configuration, double target = 4.0)
    {
        PumpLifecycleConfiguration lifecycleConfiguration = ConfigurationFor(configuration, target);

        var transport = new FakeTransport();
        var time = new FakeTime();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(time.GetUtcNow());

        var boundary = new ProcessIoPumpCommandBoundary(
            session,
            Catalogue(),
            Authorize(),
            configuration,
            time,
            lifecycleConfiguration.PressureFreshnessBudget);

        var ownership = new PumpCommandOwnership();
        return new Rig
        {
            Transport = transport,
            Time = time,
            Session = session,
            Ownership = ownership,
            Lifecycle = new ProcessIoPumpLifecycle(boundary, lifecycleConfiguration, ownership, time),
            Configuration = lifecycleConfiguration,
        };
    }

    private static void Drive(Rig rig, int maxSteps = 400, PumpLifecycleState? stopAt = null)
    {
        for (int step = 0; step < maxSteps; step += 1)
        {
            if (rig.Lifecycle.Result is not null) return;
            if (stopAt is { } target && rig.Lifecycle.State == target) return;
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }
    }

    /// <summary>Starts, seeds a running Pump and drives to confirmed readiness.</summary>
    private static Rig CreateReady()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;

        Assert.True(Start(rig).Started);
        Drive(rig, stopAt: PumpLifecycleState.PUMP_READY);
        Assert.Equal(PumpLifecycleState.PUMP_READY, rig.Lifecycle.State);
        return rig;
    }

    private static PumpLifecycleStartRequest Request(
        PumpCommandOwnerKind owner = PumpCommandOwnerKind.CLEANING_JOB,
        int selection = 1) => new()
        {
            Owner = owner,
            OwnerId = Owner,
            Selection = selection,
        };

    private static PumpLifecycleStartOutcome Start(Rig rig, int selection = 1) =>
        rig.Lifecycle.Start(Request(selection: selection));

    private static List<int> ClearedCoils(Rig rig) =>
        rig.Transport.CoilWrites.Where(write => !write.Value).Select(write => write.Address).ToList();

    // ---------------------------------------------------------------- 1. feedback configuration

    [Fact]
    public void Feedback_Polarity_Defaults_To_Not_Configured()
    {
        PumpFeedbackConfiguration configuration = PumpFeedbackConfiguration.Uncommissioned;

        Assert.Equal(PumpFeedbackPolarity.NOT_CONFIGURED, configuration.RunningPolarity);
        Assert.Equal(PumpFeedbackPolarity.NOT_CONFIGURED, configuration.FaultPolarity);
        Assert.False(configuration.RunningCommissioned);
        Assert.False(configuration.FaultCommissioned);
        Assert.False(configuration.IsCommissioned);
        Assert.Equal(PumpFeedbackConfiguration.NotCommissionedCode, configuration.CommissioningCode);
    }

    [Fact]
    public void A_Half_Commissioned_Pair_Is_Still_Not_Commissioned()
    {
        var half = new PumpFeedbackConfiguration { RunningPolarity = PumpFeedbackPolarity.ACTIVE_HIGH };

        Assert.True(half.RunningCommissioned);
        Assert.False(half.FaultCommissioned);
        Assert.False(half.IsCommissioned);
        Assert.Equal(PumpFeedbackConfiguration.NotCommissionedCode, half.CommissioningCode);
    }

    // ---------------------------------------------------------------- 2. commissioning gate

    [Fact]
    public void A_Physical_Pump_Command_Refuses_Before_Polarity_Commissioning_And_Writes_Nothing()
    {
        Rig rig = CreateWired(PumpFeedbackConfiguration.Uncommissioned);
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;

        PumpLifecycleStartOutcome start = Start(rig);

        Assert.False(start.Started);
        Assert.Equal(PumpCommandRefusals.RefusedPolarityNotCommissioned, start.Code);
        Assert.Equal(PumpLifecycleState.IDLE, rig.Lifecycle.State);
        Assert.Empty(rig.Transport.CoilWrites);
        Assert.Empty(rig.Transport.RegisterWrites);
        Assert.False(rig.Ownership.IsHeld);
    }

    // ---------------------------------------------------------------- 3 to 5. selection

    [Fact]
    public void Pump_One_Selection_Writes_Do001_True_Then_Do002_False()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;

        Start(rig, selection: 1);

        Assert.Equal(2, rig.Transport.CoilWrites.Count);
        Assert.Equal((CoilSelect1, true), (rig.Transport.CoilWrites[0].Address, rig.Transport.CoilWrites[0].Value));
        Assert.Equal((CoilSelect2, false), (rig.Transport.CoilWrites[1].Address, rig.Transport.CoilWrites[1].Value));
        Assert.Equal(PumpLifecycleState.WAITING_FOR_RUNNING, rig.Lifecycle.State);
    }

    [Fact]
    public void Pump_Two_Selection_Writes_Do002_True_Then_Do001_False()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;

        Start(rig, selection: 2);

        Assert.Equal(2, rig.Transport.CoilWrites.Count);
        Assert.Equal((CoilSelect2, true), (rig.Transport.CoilWrites[0].Address, rig.Transport.CoilWrites[0].Value));
        Assert.Equal((CoilSelect1, false), (rig.Transport.CoilWrites[1].Address, rig.Transport.CoilWrites[1].Value));
    }

    [Fact]
    public void Selection_Is_Mutually_Exclusive_And_Refuses_Every_Other_Ordinal()
    {
        IReadOnlyList<PumpCommandStep> one = PumpCommandContract.SelectionSequence(1);
        IReadOnlyList<PumpCommandStep> two = PumpCommandContract.SelectionSequence(2);

        Assert.NotEqual(one[0].Tag, two[0].Tag);
        Assert.Contains(one, step => step.Tag == PumpCommandContract.SelectPumpOneTag && step.Value);
        Assert.Contains(one, step => step.Tag == PumpCommandContract.SelectPumpTwoTag && !step.Value);
        Assert.Contains(two, step => step.Tag == PumpCommandContract.SelectPumpTwoTag && step.Value);
        Assert.Contains(two, step => step.Tag == PumpCommandContract.SelectPumpOneTag && !step.Value);

        foreach (int invalid in new[] { 0, 3, 8, -1 })
        {
            Assert.False(PumpCommandContract.IsValidSelection(invalid));
            Assert.Empty(PumpCommandContract.SelectionSequence(invalid));

            Rig rig = CreateWired(Commissioned());
            PumpLifecycleStartOutcome outcome = Start(rig, selection: invalid);

            Assert.False(outcome.Started);
            Assert.Equal(PumpCommandRefusals.RefusedInvalidSelection, outcome.Code);
            Assert.Empty(rig.Transport.CoilWrites);
        }
    }

    // ---------------------------------------------------------------- 6 to 8. start order and semantics

    [Fact]
    public void Start_Writes_Do003_True_Then_Do007_True()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;

        Start(rig);

        Assert.Equal(4, rig.Transport.CoilWrites.Count);
        Assert.Equal((CoilEnable, true), (rig.Transport.CoilWrites[2].Address, rig.Transport.CoilWrites[2].Value));
        Assert.Equal((CoilRun, true), (rig.Transport.CoilWrites[3].Address, rig.Transport.CoilWrites[3].Value));
    }

    [Fact]
    public void Start_Commands_Are_Maintained_And_No_Timer_Clears_Them()
    {
        // Neither output is a pulse: there is no duration, no release write and no scheduled clear.
        Assert.Equal("MAINTAINED_ACTIVE_HIGH", PumpCommandContract.CommandSemantics);
        Assert.Equal("NO_PULSE_DURATION_APPLIES", PumpCommandContract.PulseSemantics);
        Assert.Equal("FC05", ProcessIoFunctionCodes.DoCommand);

        Rig rig = CreateReady();

        // A long, healthy run must not de-energise either output on a timer.
        for (int step = 0; step < 30; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }

        Assert.Equal(PumpLifecycleState.PUMP_READY, rig.Lifecycle.State);
        Assert.DoesNotContain(rig.Transport.CoilWrites, write => write.Address == CoilRun && !write.Value);
        Assert.DoesNotContain(rig.Transport.CoilWrites, write => write.Address == CoilEnable && !write.Value);
        Assert.Single(rig.Transport.CoilWrites, write => write.Address == CoilRun && write.Value);
        Assert.Single(rig.Transport.CoilWrites, write => write.Address == CoilEnable && write.Value);
    }

    // ---------------------------------------------------------------- 9. fault admission

    [Fact]
    public void An_Active_Configured_Fault_Refuses_Start()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputFault] = true;
        rig.Transport.DiscreteInputs[InputRunning] = false;

        PumpLifecycleStartOutcome start = Start(rig);

        Assert.False(start.Started);
        Assert.Equal(PumpCommandRefusals.RefusedFaultActive, start.Code);
        Assert.Empty(rig.Transport.CoilWrites);
        Assert.Equal(PumpLifecycleState.FAULT_ACTIVE, rig.Lifecycle.State);
        Assert.False(rig.Ownership.IsHeld);
        Assert.False(rig.Lifecycle.SafeToReportComplete);
    }

    // ---------------------------------------------------------------- 10 and 11. running feedback

    [Fact]
    public void Running_Feedback_Uses_Di004_Only()
    {
        Assert.Equal("DI-004", PumpCommandContract.RunningFeedbackTag);
        Assert.True(PumpCommandContract.TryDiscreteInputAddress(PumpCommandContract.RunningFeedbackTag, out int address));
        Assert.Equal(InputRunning, address);

        // Only DI-004 is seeded false, so the Pump never reports running whatever else is high.
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;

        Start(rig);
        for (int step = 0; step < 5; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }

        Assert.Equal(PumpLifecycleState.WAITING_FOR_RUNNING, rig.Lifecycle.State);
    }

    [Fact]
    public void A_Stale_Or_Wrong_Generation_Running_Feedback_Is_Never_Accepted_As_Running()
    {
        var observation = new ProcessIoPhysicalObservation
        {
            Tag = PumpCommandContract.RunningFeedbackTag,
            RawValue = 1,
            EngineeringValue = null,
            Unit = null,
            Quality = ProcessIoObservationQuality.STALE,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ReceivedAt = DateTimeOffset.UnixEpoch,
            SourceGeneration = 1,
            Fresh = false,
            Provenance = ProcessIoObservationProvenance.PHYSICAL_READ,
            DiagnosticCode = ProcessIoRefusals.ObservationStale,
        };

        DateTimeOffset now = DateTimeOffset.UnixEpoch.AddMinutes(10);
        Assert.False(ProcessIoEvidenceAcceptance.Accepts(observation, 1, now, Freshness));

        // A current value from a superseded generation is refused too.
        var superseded = observation with
        {
            Quality = ProcessIoObservationQuality.GOOD,
            Fresh = true,
            ObservedAt = now,
            ReceivedAt = now,
            SourceGeneration = 2,
        };
        Assert.False(ProcessIoEvidenceAcceptance.Accepts(superseded, 1, now, Freshness));

        // A simulated read is never physical evidence, however current it looks.
        Assert.False(ProcessIoEvidenceAcceptance.Accepts(
            superseded with { Provenance = ProcessIoObservationProvenance.SIMULATED_READ }, 2, now, Freshness));

        // The same record from the live generation, inside the budget, is accepted.
        Assert.True(ProcessIoEvidenceAcceptance.Accepts(superseded, 2, now, Freshness));
    }

    // ---------------------------------------------------------------- 12 and 13. source identity

    [Fact]
    public void The_Pump_Outlet_Source_Is_Ai003_And_The_Inlet_Is_Diagnostic_Only()
    {
        Assert.Equal("AI-003+", PumpCommandContract.PumpOutletPressureTag);
        Assert.Equal("AI-002+", PumpCommandContract.PumpInletPressureTag);

        Assert.True(ProcessIoBindings.TryGet(PumpCommandContract.PumpOutletPressureTag, out ProcessIoPhysicalBinding outlet));
        Assert.Equal(ProcessIoArea.InputRegister, outlet.Area);
        Assert.Equal(RegisterPumpOutlet, outlet.Address);

        Assert.True(ProcessIoBindings.TryGet(PumpCommandContract.PumpInletPressureTag, out ProcessIoPhysicalBinding inlet));
        Assert.Equal(ProcessIoArea.InputRegister, inlet.Area);
        Assert.Equal(RegisterPumpInlet, inlet.Address);
    }

    [Fact]
    public void Ai002_Cannot_Satisfy_Pump_Ready_And_Is_Never_Read()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpInlet] = ushort.MaxValue;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustBelow15Bar;

        Start(rig);
        Drive(rig);

        // A saturated inlet value changes nothing: the row is never read for readiness.
        Assert.DoesNotContain(RegisterPumpInlet, rig.Transport.RegisterReads);
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, rig.Lifecycle.State);
        Assert.Equal(PumpLifecycleState.AI003_TIMEOUT, rig.Lifecycle.PrimaryFailure);
        Assert.Equal(PumpLifecycleState.AI003_TIMEOUT, rig.Lifecycle.Result!.PrimaryFailure);
    }

    // ---------------------------------------------------------------- 14 to 17. the strict rule

    [Fact]
    public void A_Bad_Or_Stale_Ai003_Refuses_Ready()
    {
        var time = new FakeTime();
        var boundary = new PressureOnlyBoundary(time, Commissioned());
        var lifecycle = new ProcessIoPumpLifecycle(
            boundary, ConfigurationFor(Commissioned()), new PumpCommandOwnership(), time);

        Assert.True(lifecycle.Start(Request()).Started);
        boundary.OutletBar = 18.0;

        for (int step = 0; step < 20 && lifecycle.State != PumpLifecycleState.WAITING_FOR_AI003_READY; step += 1)
        {
            time.Advance(Interval);
            lifecycle.Step();
        }

        Assert.Equal(PumpLifecycleState.WAITING_FOR_AI003_READY, lifecycle.State);

        boundary.OutletQuality = PressureQuality.STALE;
        time.Advance(Interval);
        lifecycle.Step();

        Assert.Equal(PumpLifecycleState.AI003_STALE, lifecycle.PrimaryFailure);
        Assert.Equal(PumpLifecycleState.RAMPING_DOWN, lifecycle.State);
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, lifecycle.State);
    }

    [Fact]
    public void A_Bad_Ai003_Refuses_Ready()
    {
        var time = new FakeTime();
        var boundary = new PressureOnlyBoundary(time, Commissioned());
        var lifecycle = new ProcessIoPumpLifecycle(
            boundary, ConfigurationFor(Commissioned()), new PumpCommandOwnership(), time);

        Assert.True(lifecycle.Start(Request()).Started);
        boundary.OutletBar = 18.0;

        for (int step = 0; step < 20 && lifecycle.State != PumpLifecycleState.WAITING_FOR_AI003_READY; step += 1)
        {
            time.Advance(Interval);
            lifecycle.Step();
        }

        boundary.OutletQuality = PressureQuality.BAD;
        time.Advance(Interval);
        lifecycle.Step();

        Assert.Equal(PumpLifecycleState.AI003_INVALID, lifecycle.PrimaryFailure);
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, lifecycle.State);
    }

    [Fact]
    public void Ai003_Equal_To_The_Setpoint_Is_Not_Ready()
    {
        // Proves the comparison is STRICT: exactly at the setpoint is refused, and so is anything below.
        var thresholds = new SequencingPressureThresholds(PumpReadySetpointBar: 15.0);
        DateTimeOffset at = DateTimeOffset.UnixEpoch;

        Assert.False(SequencingPressure.PumpReady(
            new PressureSample(15.0, PressureQuality.GOOD, false, at, PressureSample.PumpOutletSource), thresholds));
        Assert.False(SequencingPressure.PumpReady(
            new PressureSample(14.999, PressureQuality.GOOD, false, at, PressureSample.PumpOutletSource), thresholds));
        Assert.True(SequencingPressure.PumpReady(
            new PressureSample(15.001, PressureQuality.GOOD, false, at, PressureSample.PumpOutletSource), thresholds));

        // And through the lifecycle, at exactly the setpoint.
        var time = new FakeTime();
        var boundary = new PressureOnlyBoundary(time, Commissioned());
        var lifecycle = new ProcessIoPumpLifecycle(
            boundary, ConfigurationFor(Commissioned()), new PumpCommandOwnership(), time);

        Assert.True(lifecycle.Start(Request()).Started);
        boundary.OutletBar = 15.0;

        for (int step = 0; step < 20 && lifecycle.State != PumpLifecycleState.WAITING_FOR_AI003_READY; step += 1)
        {
            time.Advance(Interval);
            lifecycle.Step();
        }

        Assert.Equal(PumpLifecycleState.WAITING_FOR_AI003_READY, lifecycle.State);
        time.Advance(Interval);
        lifecycle.Step();
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, lifecycle.State);
    }

    [Fact]
    public void Ai003_Below_The_Setpoint_Is_Not_Ready_And_Above_It_Is()
    {
        Rig below = CreateWired(Commissioned());
        below.Transport.DiscreteInputs[InputRunning] = true;
        below.Transport.InputRegisters[RegisterPumpOutlet] = RawJustBelow15Bar;
        Start(below);

        for (int step = 0; step < 80 && below.Lifecycle.State != PumpLifecycleState.WAITING_FOR_AI003_READY; step += 1)
        {
            below.Time.Advance(Interval);
            below.Lifecycle.Step();
        }

        Assert.Equal(PumpLifecycleState.WAITING_FOR_AI003_READY, below.Lifecycle.State);
        below.Time.Advance(Interval);
        below.Lifecycle.Step();
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, below.Lifecycle.State);

        Rig above = CreateWired(Commissioned());
        above.Transport.DiscreteInputs[InputRunning] = true;
        above.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;
        Start(above);
        Drive(above, stopAt: PumpLifecycleState.PUMP_READY);

        Assert.Equal(PumpLifecycleState.PUMP_READY, above.Lifecycle.State);
    }

    // ---------------------------------------------------------------- 18 to 22. the analog command

    [Fact]
    public void The_Committed_Analog_Conversion_Is_Preserved()
    {
        // The existing boundary is authoritative: engineering milliamps 0..20 map linearly to raw
        // 0..32767. This pins the anchors through that existing conversion rather than adding a second
        // map, and records the 4 mA live-signal anchor explicitly.
        Assert.True(ProcessIoEngineering.TryPumpSpeedRaw(0.0, out int zero));
        Assert.True(ProcessIoEngineering.TryPumpSpeedRaw(20.0, out int full));
        Assert.True(ProcessIoEngineering.TryPumpSpeedRaw(4.0, out int live));

        Assert.Equal(0, zero);
        Assert.Equal(ProcessIoRepresentation.RawFullScale, full);
        Assert.Equal(6553, live);

        Assert.False(ProcessIoEngineering.TryPumpSpeedRaw(-0.001, out _));
        Assert.False(ProcessIoEngineering.TryPumpSpeedRaw(20.001, out _));
        Assert.False(ProcessIoEngineering.TryPumpSpeedRaw(double.NaN, out _));
        Assert.False(ProcessIoEngineering.TryPumpSpeedRaw(double.PositiveInfinity, out _));

        Assert.Equal("mA", PumpAnalogCommandContract.EngineeringUnit);
        Assert.Equal(0.0, PumpAnalogCommandContract.EngineeringMinimum);
        Assert.Equal(20.0, PumpAnalogCommandContract.EngineeringMaximum);
    }

    [Fact]
    public void The_Commissioned_Ramp_Is_A_Thousand_Millisecond_Interval_And_A_Step_Of_Two()
    {
        Assert.Equal(1000, PumpAnalogCommandContract.RampIntervalMilliseconds);
        Assert.Equal(2.0, PumpAnalogCommandContract.RampStepEngineering);

        PumpSpeedRampConfiguration ownerInitial = PumpSpeedRampConfiguration.OwnerInitial(10.0);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), ownerInitial.Interval);
        Assert.Equal(2.0, ownerInitial.Step);
        Assert.Equal(0.0, ownerInitial.SafeZeroEngineeringCommand);
        Assert.True(ownerInitial.Valid);
    }

    [Fact]
    public void The_Ramp_Never_Exceeds_Its_Target()
    {
        Assert.Equal(2.0, PumpSpeedRamp.Next(0.0, 10.0, 2.0));
        Assert.Equal(10.0, PumpSpeedRamp.Next(9.0, 10.0, 2.0));
        Assert.Equal(10.0, PumpSpeedRamp.Next(8.0, 10.0, 5.0));

        double current = 0.0;
        for (int step = 0; step < 20; step += 1)
        {
            current = PumpSpeedRamp.Next(current, 10.0, 2.0);
            Assert.True(current <= 10.0);
        }

        Assert.Equal(10.0, current);
        Assert.True(PumpSpeedRamp.AtTarget(current, 10.0));
    }

    [Fact]
    public void The_Stop_Ramp_Reaches_The_Configured_Safe_Zero()
    {
        double current = 10.0;
        for (int step = 0; step < 20 && !PumpSpeedRamp.AtTarget(current, 0.0); step += 1)
        {
            current = PumpSpeedRamp.Next(current, 0.0, 2.0);
            Assert.True(current >= 0.0);
        }

        Assert.Equal(0.0, current);
        Assert.True(PumpSpeedRamp.AtTarget(current, PumpAnalogCommandContract.SafeZeroEngineeringCommand));

        // The live I/O path reaches the same safe zero.
        Rig rig = CreateReady();
        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        Drive(rig);

        Assert.Equal(PumpLifecycleState.STOPPED, rig.Lifecycle.State);
        Assert.Equal(0.0, rig.Lifecycle.SpeedCommand);
        Assert.Equal((ushort)0, rig.Transport.RegisterWrites[^1].Value);
    }

    // ---------------------------------------------------------------- 23 to 26. corrected stop order

    [Fact]
    public void The_Stop_Writes_Do007_False_Before_Do003_False()
    {
        Rig rig = CreateReady();
        rig.Transport.DiscreteInputs[InputRunning] = false;

        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        Drive(rig);

        Assert.Equal(PumpLifecycleState.STOPPED, rig.Lifecycle.State);

        int runIndex = rig.Transport.CoilWrites.FindIndex(write => write.Address == CoilRun && !write.Value);
        int enableIndex = rig.Transport.CoilWrites.FindIndex(write => write.Address == CoilEnable && !write.Value);

        Assert.True(runIndex >= 0);
        Assert.True(enableIndex >= 0);
        Assert.True(runIndex < enableIndex, "Run Pump must be de-energised BEFORE Enable Inverter.");
    }

    [Fact]
    public void Each_De_Energisation_Happens_Exactly_Once()
    {
        Rig rig = CreateReady();
        rig.Transport.DiscreteInputs[InputRunning] = false;

        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        Drive(rig);

        // Stepping well past the terminal must not re-issue anything.
        for (int step = 0; step < 10; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }

        Assert.Single(rig.Transport.CoilWrites, write => write.Address == CoilRun && !write.Value);
        Assert.Single(rig.Transport.CoilWrites, write => write.Address == CoilEnable && !write.Value);
    }

    [Fact]
    public void Enable_Is_Never_Cleared_Before_Run_During_Stop()
    {
        // The superseded Legacy order cleared Enable first. It must not be reachable at all.
        Assert.True(PumpCommandContract.IsCorrectedStopOrder(PumpCommandContract.StopSequence));
        Assert.Equal(PumpCommandContract.RunPumpTag, PumpCommandContract.StopSequence[0].Tag);
        Assert.Equal(PumpCommandContract.EnableInverterTag, PumpCommandContract.StopSequence[1].Tag);
        Assert.False(PumpCommandContract.StopSequence[0].Value);
        Assert.False(PumpCommandContract.StopSequence[1].Value);

        var superseded = new[]
        {
            new PumpCommandStep(1, PumpCommandContract.EnableInverterTag, false),
            new PumpCommandStep(2, PumpCommandContract.RunPumpTag, false),
        };
        Assert.False(PumpCommandContract.IsCorrectedStopOrder(superseded));
        Assert.False(PumpCommandContract.IsCorrectedStopOrder(null));
        Assert.False(PumpCommandContract.IsCorrectedStopOrder(Array.Empty<PumpCommandStep>()));

        // And the live path agrees with the declared order.
        Rig rig = CreateReady();
        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        Drive(rig);

        Assert.Equal(new[] { CoilRun, CoilEnable }, ClearedCoils(rig));
    }

    // ---------------------------------------------------------------- 27 to 30. every path shares the order

    [Fact]
    public void Cancellation_Uses_The_Same_Stop_Order() => AssertSameStopOrder(PumpStopReason.CANCELLATION);

    [Fact]
    public void Fault_Cleanup_Uses_The_Same_Stop_Order() => AssertSameStopOrder(PumpStopReason.FAULT);

    [Fact]
    public void Downstream_Iv_Failure_Uses_The_Same_Stop_Order() =>
        AssertSameStopOrder(PumpStopReason.DOWNSTREAM_IV_FAILURE);

    [Fact]
    public void Cleaning_Job_Sequence_Application_And_Timeout_Failures_Use_The_Same_Stop_Order()
    {
        AssertSameStopOrder(PumpStopReason.CLEANING_JOB_FAILURE);
        AssertSameStopOrder(PumpStopReason.SEQUENCE_FAILURE);
        AssertSameStopOrder(PumpStopReason.APPLICATION_CLEANUP);
        AssertSameStopOrder(PumpStopReason.AI003_TIMEOUT);
        AssertSameStopOrder(PumpStopReason.RUNNING_FEEDBACK_TIMEOUT);
        AssertSameStopOrder(PumpStopReason.AI003_INVALID);
        AssertSameStopOrder(PumpStopReason.AI003_STALE);
        AssertSameStopOrder(PumpStopReason.START_FAILURE);
    }

    private static void AssertSameStopOrder(PumpStopReason reason)
    {
        Rig rig = CreateReady();
        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Lifecycle.Stop(reason);
        Drive(rig);

        Assert.Equal(new[] { CoilRun, CoilEnable }, ClearedCoils(rig));
        Assert.Equal(reason, rig.Lifecycle.Result?.StopReason);
    }

    [Fact]
    public void A_Ramp_Failure_Still_Attempts_Both_De_Energisations_In_Order()
    {
        Rig rig = CreateReady();

        // The analog reference refuses from now on. The down-ramp cannot be dispatched, but the two
        // digital de-energisations must still be issued, in the corrected order.
        rig.Transport.ThrowOnAnalogWrite = true;
        rig.Transport.DiscreteInputs[InputRunning] = false;

        rig.Lifecycle.Stop(PumpStopReason.CLEANING_JOB_FAILURE);
        Drive(rig);

        PumpLifecycleResult? result = rig.Lifecycle.Result;
        Assert.NotNull(result);
        Assert.Contains(PumpLifecycleState.RAMP_DOWN_FAILED.ToString(), result!.CleanupFaultCodes);
        Assert.Equal(new[] { CoilRun, CoilEnable }, ClearedCoils(rig));
        Assert.Equal(PumpCleanupStates.Incomplete, result.CleanupState);
        Assert.Equal(PumpLifecycleState.CLEANUP_INCOMPLETE, result.TerminalState);
        Assert.False(rig.Lifecycle.SafeToReportComplete);
    }

    // ---------------------------------------------------------------- 31. no IV OPEN before Ready

    [Fact]
    public void An_Iv_Open_Cannot_Occur_Before_Pump_Ready()
    {
        // 1. The lifecycle itself never permits a valve open before confirmed readiness.
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustBelow15Bar;
        Assert.False(rig.Lifecycle.AllowsValveOpen);

        Start(rig);
        Assert.False(rig.Lifecycle.AllowsValveOpen);

        for (int step = 0; step < 80 && rig.Lifecycle.State != PumpLifecycleState.WAITING_FOR_AI003_READY; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }

        Assert.False(rig.Lifecycle.AllowsValveOpen);

        // 2. The Cleaning Path precondition gate refuses the OPEN and the readiness evidence.
        var inner = new RecordingBoundary();
        var gate = new CleaningPathPumpLifecycleGate(inner, rig.Lifecycle);

        gate.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));
        Assert.Empty(inner.Requests);
        Assert.Single(gate.RefusedRequests);
        Assert.Null(gate.ReadPumpOutletPressure());

        // A CLOSE is always delegated: a cleanup close must never be blocked by a Pump state.
        gate.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Close));
        Assert.Single(inner.Requests);

        // 3. Once ready, the OPEN is permitted and the evidence is served.
        Rig ready = CreateReady();
        var readyInner = new RecordingBoundary();
        var readyGate = new CleaningPathPumpLifecycleGate(readyInner, ready.Lifecycle);

        Assert.True(ready.Lifecycle.AllowsValveOpen);
        readyGate.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));
        Assert.Single(readyInner.Requests);
        Assert.NotNull(readyGate.ReadPumpOutletPressure());

        // 4. A Stop request closes the gate immediately, before the terminal is reached.
        ready.Lifecycle.Stop(PumpStopReason.DOWNSTREAM_IV_FAILURE);
        Assert.False(ready.Lifecycle.AllowsValveOpen);
        readyGate.RequestValve(new CleaningPathValveCommandRequest("IV2", CleaningPathValveCommandKind.Open));
        Assert.Single(readyInner.Requests);
        Assert.Null(readyGate.ReadPumpOutletPressure());
    }

    private sealed class RecordingBoundary : ICleaningPathEquipmentBoundary
    {
        public List<CleaningPathValveCommandRequest> Requests { get; } = new();

        public void RequestValve(CleaningPathValveCommandRequest request) => Requests.Add(request);

        public PressureSample? ReadPumpOutletPressure() => new(
            20.0, PressureQuality.GOOD, false, DateTimeOffset.UnixEpoch, PressureSample.PumpOutletSource);

        public PressureSample? ReadValveOutletPressure(string valveId) => null;

        public bool ReadUpperLimitDetected(string valveId) => false;

        public bool ReadLowerLimitDetected(string valveId) => false;
    }

    // ---------------------------------------------------------------- 32 and 33. ownership and unknown state

    [Fact]
    public void Ownership_Is_Not_Released_Before_A_Confirmed_Stop()
    {
        Rig rig = CreateReady();
        Assert.True(rig.Ownership.IsHeld);
        Assert.True(rig.Lifecycle.OwnsPumpCommand);

        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);

        // The Stop has been requested but not confirmed, so the channel is still held.
        Assert.True(rig.Ownership.IsHeld);

        rig.Transport.DiscreteInputs[InputRunning] = false;
        Drive(rig);

        Assert.Equal(PumpLifecycleState.STOPPED, rig.Lifecycle.State);
        Assert.False(rig.Ownership.IsHeld);
        Assert.True(rig.Lifecycle.Result!.StoppedConfirmed);
        Assert.True(rig.Lifecycle.SafeToReportComplete);
    }

    [Fact]
    public void An_Unknown_Stopped_State_Requires_A_Physical_Abort()
    {
        Rig rig = CreateReady();

        // The running feedback never goes false, so the stopped state stays unknown.
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        Drive(rig);

        Assert.Equal(PumpLifecycleState.PHYSICAL_ABORT_REQUIRED, rig.Lifecycle.State);
        Assert.Equal(PumpCleanupStates.PhysicalAbortRequired, rig.Lifecycle.Result!.CleanupState);
        Assert.False(rig.Lifecycle.Result.StoppedConfirmed);
        Assert.False(rig.Lifecycle.SafeToReportComplete);
        Assert.Contains(PumpLifecycleState.STOPPED_FEEDBACK_TIMEOUT.ToString(), rig.Lifecycle.Result.CleanupFaultCodes);

        // Ownership is released, but the unknown state is reported rather than hidden.
        Assert.False(rig.Ownership.IsHeld);
    }

    // ---------------------------------------------------------------- 34. primary fault separation

    [Fact]
    public void The_Primary_Fault_Remains_Separate_From_Cleanup_Failures()
    {
        Rig rig = CreateReady();

        // Trip while the stop cannot be confirmed: the trip and the cleanup failure must both survive,
        // in separate fields.
        rig.Transport.DiscreteInputs[InputFault] = true;
        rig.Time.Advance(Interval);
        rig.Lifecycle.Step();

        Assert.Equal(PumpLifecycleState.FAULT_ACTIVE, rig.Lifecycle.PrimaryFailure);

        Drive(rig);

        PumpLifecycleResult? result = rig.Lifecycle.Result;
        Assert.NotNull(result);
        Assert.Equal(PumpLifecycleState.FAULT_ACTIVE, result!.PrimaryFailure);
        Assert.NotEmpty(result.CleanupFaultCodes);
        Assert.Equal(PumpCleanupStates.PhysicalAbortRequired, result.CleanupState);
        Assert.Equal(CriticalPumpKind.MAIN_PUMP_TRIP, result.CriticalEvent);
        Assert.False(rig.Lifecycle.SafeToReportComplete);
    }

    [Fact]
    public void An_Unexpected_Stop_While_Ready_Raises_The_Critical_Event()
    {
        Rig rig = CreateReady();
        rig.Transport.DiscreteInputs[InputRunning] = false;

        rig.Time.Advance(Interval);
        rig.Lifecycle.Step();

        Assert.Equal(PumpLifecycleState.FAULT_ACTIVE, rig.Lifecycle.PrimaryFailure);
        Assert.Equal(PumpLifecycleState.RAMPING_DOWN, rig.Lifecycle.State);

        Drive(rig);
        Assert.Equal(CriticalPumpKind.MAIN_PUMP_UNEXPECTED_STOP, rig.Lifecycle.Result!.CriticalEvent);
        Assert.Equal(PumpLifecycleState.FAULT_ACTIVE, rig.Lifecycle.Result.PrimaryFailure);
    }

    // ---------------------------------------------------------------- admission boundary

    [Fact]
    public void Production_Is_Refused()
    {
        Rig rig = CreateWired(Commissioned());
        PumpLifecycleStartOutcome start = rig.Lifecycle.Start(Request(PumpCommandOwnerKind.PRODUCTION));

        Assert.False(start.Started);
        Assert.Equal(PumpCommandRefusals.RefusedProduction, start.Code);
        Assert.Empty(rig.Transport.CoilWrites);
        Assert.False(rig.Ownership.IsHeld);
    }

    [Fact]
    public void A_Conflicting_Owner_And_A_Concurrent_Lifecycle_Are_Refused()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;
        Assert.True(Start(rig).Started);

        int writesAfterStart = rig.Transport.CoilWrites.Count;

        Assert.False(rig.Ownership.TryAcquire(
            PumpCommandOwnerKind.MANUAL_COMMISSIONING, "other-owner", out string conflicting));
        Assert.Equal(PumpCommandRefusals.RefusedConflictingOwner, conflicting);

        // The same owner asking again is a concurrent lifecycle, not a re-entrant grant.
        Assert.False(rig.Ownership.TryAcquire(
            PumpCommandOwnerKind.CLEANING_JOB, Owner, out string concurrent));
        Assert.Equal(PumpCommandRefusals.RefusedConcurrentLifecycle, concurrent);

        PumpLifecycleStartOutcome again = Start(rig);
        Assert.False(again.Started);
        Assert.Equal(PumpCommandRefusals.RefusedConcurrentLifecycle, again.Code);
        Assert.Equal(writesAfterStart, rig.Transport.CoilWrites.Count);
    }

    [Fact]
    public void An_Invalid_Configuration_Is_Refused()
    {
        PumpLifecycleConfiguration invalid =
            ConfigurationFor(Commissioned()) with { PressureFreshnessBudget = TimeSpan.Zero };
        Assert.False(invalid.Valid);

        var transport = new FakeTransport();
        var time = new FakeTime();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(time.GetUtcNow());
        var boundary = new ProcessIoPumpCommandBoundary(
            session, Catalogue(), Authorize(), Commissioned(), time, Freshness);
        var lifecycle = new ProcessIoPumpLifecycle(boundary, invalid, new PumpCommandOwnership(), time);

        PumpLifecycleStartOutcome start = lifecycle.Start(Request());
        Assert.False(start.Started);
        Assert.Equal(PumpCommandRefusals.RefusedInvalidConfiguration, start.Code);
        Assert.Empty(transport.CoilWrites);
    }

    [Fact]
    public void A_Wrong_Tag_Or_Address_Is_Refused_By_The_Command_Contract()
    {
        Assert.True(PumpCommandContract.IsPumpCommandTag("DO-001"));
        Assert.True(PumpCommandContract.IsPumpCommandTag("DO-002"));
        Assert.True(PumpCommandContract.IsPumpCommandTag("DO-003"));
        Assert.True(PumpCommandContract.IsPumpCommandTag("DO-007"));

        Assert.False(PumpCommandContract.IsPumpCommandTag("DO-004"));
        Assert.False(PumpCommandContract.IsPumpCommandTag("DO-023"));
        Assert.False(PumpCommandContract.IsPumpCommandTag("DI-004"));
        Assert.False(PumpCommandContract.IsPumpCommandTag("AI-003+"));
        Assert.False(PumpCommandContract.IsPumpCommandTag(null));

        // The addresses are DERIVED from the one binding table, never restated.
        Assert.True(PumpCommandContract.TryCoilAddress(PumpCommandContract.SelectPumpOneTag, out int one));
        Assert.True(PumpCommandContract.TryCoilAddress(PumpCommandContract.SelectPumpTwoTag, out int two));
        Assert.True(PumpCommandContract.TryCoilAddress(PumpCommandContract.EnableInverterTag, out int enable));
        Assert.True(PumpCommandContract.TryCoilAddress(PumpCommandContract.RunPumpTag, out int run));
        Assert.Equal(CoilSelect1, one);
        Assert.Equal(CoilSelect2, two);
        Assert.Equal(CoilEnable, enable);
        Assert.Equal(CoilRun, run);

        // A discrete input is not a writable coil, so it cannot be given a coil address.
        Assert.False(PumpCommandContract.TryCoilAddress(PumpCommandContract.RunningFeedbackTag, out _));
        Assert.False(PumpCommandContract.TryRegisterAddress(PumpCommandContract.RunPumpTag, out _));
    }

    // ---------------------------------------------------------------- 40. no automatic retry

    [Fact]
    public void Nothing_Retries_After_A_Terminal()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = false;
        Start(rig);

        Drive(rig);
        Assert.Equal(PumpLifecycleState.RUNNING_FEEDBACK_TIMEOUT, rig.Lifecycle.PrimaryFailure);

        int writes = rig.Transport.CoilWrites.Count;

        for (int step = 0; step < 50; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }

        Assert.Equal(writes, rig.Transport.CoilWrites.Count);
        Assert.False(Start(rig).Started);
        Assert.Equal(PumpCommandRefusals.RefusedAlreadyTerminal, Start(rig).Code);

        // The lifecycle never re-energises after termination.
        Assert.DoesNotContain(rig.Transport.CoilWrites.Skip(writes), write => write.Value);
    }

    // ---------------------------------------------------------------- ordering invariant

    [Fact]
    public void The_Lifecycle_Reaches_Every_Progression_State_In_Order()
    {
        Rig rig = CreateWired(Commissioned());
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAbove15Bar;

        var seen = new List<PumpLifecycleState>();

        void Record()
        {
            if (seen.Count == 0 || seen[^1] != rig.Lifecycle.State) seen.Add(rig.Lifecycle.State);
        }

        Record();
        Start(rig);
        Record();

        for (int step = 0; step < 400 && rig.Lifecycle.Result is null; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
            Record();

            if (rig.Lifecycle.State == PumpLifecycleState.PUMP_READY)
            {
                rig.Transport.DiscreteInputs[InputRunning] = false;
                rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
                Record();
            }
        }

        Assert.Equal(
            new[]
            {
                PumpLifecycleState.IDLE,
                PumpLifecycleState.WAITING_FOR_RUNNING,
                PumpLifecycleState.RAMPING_OUTPUT,
                PumpLifecycleState.WAITING_FOR_AI003_READY,
                PumpLifecycleState.PUMP_READY,
                PumpLifecycleState.RAMPING_DOWN,
                PumpLifecycleState.CLEARING_RUN,
                PumpLifecycleState.DISABLING_INVERTER,
                PumpLifecycleState.WAITING_FOR_STOPPED,
                PumpLifecycleState.STOPPED,
            },
            seen);
    }
}
