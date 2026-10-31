using Wjss.Contracts;
using Wjss.Runtime.Core.ProcessIo;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// H2 Physical Process-I/O — focused tests: complete catalogue parity, exact T8
/// adoption (bindings, conversions, function codes), typed read/write lifecycles, admission
/// refusals with zero network I/O, stale/reconnect/close fencing, and the
/// commanded/effective/read-back separation. Deterministic; no device access.
/// </summary>
public sealed class ProcessIoFoundationTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<ProcessIoCatalogueRow> LoadCommittedCatalogue()
        => ProcessIoCatalogueLoader.Load(File.ReadAllText(ProcessIoTestPaths.CommittedCataloguePath));

    [Fact]
    public void Catalogue_Loads_All_111_Rows_With_Class_Parity()
    {
        var rows = LoadCommittedCatalogue();
        Assert.Equal(111, rows.Count);
        Assert.Equal(40, rows.Count(r => r.IoClass == "DI"));
        Assert.Equal(32, rows.Count(r => r.IoClass == "DO"));
        Assert.Equal(36, rows.Count(r => r.IoClass == "AI"));
        Assert.Equal(2, rows.Count(r => r.IoClass == "AO"));
        Assert.Equal(1, rows.Count(r => r.IoClass == "END_MODULE"));
    }

    [Fact]
    public void Catalogue_Preserves_Source_Order_And_Verbatim_Values()
    {
        var rows = LoadCommittedCatalogue();
        for (int i = 0; i < rows.Count; i++) Assert.Equal(i + 1, rows[i].SourceRowOrdinal);
        Assert.Equal("DI-001", rows[0].Tag);
        Assert.Equal("AI-003+", rows[74].Tag);
        Assert.Equal("750-600", rows[110].Module);
        Assert.Equal("XXX รอพี่ยิมตอบ", rows.First(r => r.Tag == "DO-031").Description);
    }

    [Fact]
    public void Catalogue_Refuses_Row_Loss_And_Provenance_Tampering()
    {
        string csv = File.ReadAllText(ProcessIoTestPaths.CommittedCataloguePath);
        string lost = string.Join('\n', csv.Split('\n').Where((_, i) => i != 50));
        Assert.Throws<InvalidOperationException>(() => ProcessIoCatalogueLoader.Load(lost));
        string tampered = csv.Replace(ProcessIoCatalogueLoader.ExpectedProvenanceSha256, new string('0', 64));
        Assert.Throws<InvalidOperationException>(() => ProcessIoCatalogueLoader.Load(tampered));
    }

    [Fact]
    public void Bindings_Adopt_Exact_Offsets_And_Function_Codes()
    {
        var rows = LoadCommittedCatalogue();
        var ai003 = rows.First(r => r.Tag == "AI-003+");
        Assert.Equal(ProcessIoBindingState.BOUND_LEGACY_EXACT, ai003.BindingState);
        Assert.Equal(ProcessIoDevices.RackT8, ai003.Address.DeviceKey);
        Assert.Equal(2, ai003.Address.Offset);
        Assert.Equal(ProcessIoFunctionCodes.AiRawRead, ai003.Address.FunctionCode);
        var do023 = rows.First(r => r.Tag == "DO-023");
        Assert.Equal(22, do023.Address.Offset);
        Assert.Equal(ProcessIoFunctionCodes.DoCommand, do023.Address.FunctionCode);
        var ao001 = rows.First(r => r.Tag == "AO-001+");
        Assert.Equal(0, ao001.Address.Offset);
        Assert.Equal(ProcessIoFunctionCodes.AoCommand, ao001.Address.FunctionCode);
        var di001 = rows.First(r => r.Tag == "DI-001");
        Assert.Equal(0, di001.Address.Offset);
        Assert.Equal(ProcessIoFunctionCodes.DiStateRead, di001.Address.FunctionCode);
    }

    [Fact]
    public void Spare_And_Unresolved_Rows_Are_Excluded_And_Address_Unresolved()
    {
        var rows = LoadCommittedCatalogue();
        foreach (var row in rows.Where(r => r.BindingState == ProcessIoBindingState.UNBOUND))
        {
            Assert.Equal(ProcessIoCommissioningState.EXCLUDED, row.CommissioningState);
            Assert.Equal("ADDRESS_UNRESOLVED", row.Address.State);
        }
        Assert.Equal(6, rows.Count(r => r.SourceClassification == "SPARE"));
    }

    [Fact]
    public void Engineering_Follows_Exact_Legacy_Conversion_Chains()
    {
        Assert.Equal(40.0, ProcessIoEngineering.WcPressureBar(32767), 6);
        Assert.Equal(0.0, ProcessIoEngineering.WcPressureBar(0), 6);
        Assert.Equal(40.0, ProcessIoEngineering.PumpOutletPressureBar(32767), 6);
        Assert.Equal(0.0, ProcessIoEngineering.PumpOutletPressureBar(0), 6);
        Assert.Equal(270.0, ProcessIoEngineering.WaterTankLevel(32767), 6);
        Assert.Equal(100.0, ProcessIoEngineering.WaterTankLevelPercent(32767), 6);
        Assert.True(ProcessIoEngineering.TryPumpSpeedRaw(12, out int raw));
        Assert.Equal((int)Math.Round(12.0 * 32767 / 20), raw);
        Assert.False(ProcessIoEngineering.TryPumpSpeedRaw(25, out _));
    }

    [Fact]
    public void Typed_Read_Acquisition_And_Closed_Session_Refusal()
    {
        var transport = new SimulatedProcessIoTransport();
        transport.SeedDiscreteInput(ProcessIoDevices.RackT8, 0, true);
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var row = LoadCommittedCatalogue().First(r => r.Tag == "DI-001");
        Assert.True(session.TryReadDigitalInput("DI-001", row, At, out var obs));
        Assert.True(obs.RawBit);
        Assert.Equal("GOOD", obs.Quality);
        session.Close(At);
        Assert.False(session.TryReadDigitalInput("DI-001", row, At, out _));
    }

    [Fact]
    public void Write_Admission_Refuses_Without_Authorization_With_Zero_Network_IO()
    {
        var transport = new SimulatedProcessIoTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var row = LoadCommittedCatalogue().First(r => r.Tag == "DO-023");
        var refusal = session.AdmitWrite(At, row, new ProcessIoWriteAuthorization { PhysicalWriteAuthorized = false });
        Assert.False(refusal.Admitted);
        Assert.Equal(ProcessIoRefusals.WriteRefusedNotAuthorized, refusal.Code);
        Assert.Equal(0, transport.NetworkAttempts);
        Assert.Empty(transport.Writes);
    }

    [Fact]
    public void Spare_And_Unbound_Rows_Cannot_Receive_Write_Authority()
    {
        var transport = new SimulatedProcessIoTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var rows = LoadCommittedCatalogue();
        var unbound = rows.First(r => r.SourceRowOrdinal == 72); // DO spare with tag '-'
        var bound = rows.First(r => r.Tag == "DO-023");
        var boundExcluded = bound with { CommissioningState = ProcessIoCommissioningState.EXCLUDED };
        var auth = new ProcessIoWriteAuthorization { PhysicalWriteAuthorized = true };

        var unboundAdmission = session.AdmitWrite(At, unbound, auth);
        Assert.False(unboundAdmission.Admitted);
        Assert.Equal(ProcessIoRefusals.WriteRefusedUnbound, unboundAdmission.Code);

        var excludedAdmission = session.AdmitWrite(At, boundExcluded, auth);
        Assert.False(excludedAdmission.Admitted);
        Assert.Equal(ProcessIoRefusals.WriteRefusedExcluded, excludedAdmission.Code);

        Assert.Empty(transport.Writes);
    }

    [Fact]
    public void Exactly_Once_Dispatch_No_Stale_Write_Replay()
    {
        var transport = new SimulatedProcessIoTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var row = LoadCommittedCatalogue().First(r => r.Tag == "DO-023");
        var admission = session.AdmitWrite(At, row, new ProcessIoWriteAuthorization { PhysicalWriteAuthorized = true });
        Assert.True(admission.Admitted);
        Assert.True(session.DispatchDigitalOutput(At, admission.Token!, row, true));
        Assert.False(session.DispatchDigitalOutput(At, admission.Token!, row, true));
        Assert.Single(transport.Writes);
    }

    [Fact]
    public void No_Write_After_Close_Or_Stop()
    {
        var transport = new SimulatedProcessIoTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var row = LoadCommittedCatalogue().First(r => r.Tag == "DO-023");
        var auth = new ProcessIoWriteAuthorization { PhysicalWriteAuthorized = true };
        session.Stop(At);
        Assert.Equal(ProcessIoRefusals.WriteAfterStop, session.AdmitWrite(At, row, auth).Code);
        session.Close(At);
        Assert.Equal(ProcessIoRefusals.SessionNotOpen, session.AdmitWrite(At, row, auth).Code);
        Assert.Empty(transport.Writes);
    }

    [Fact]
    public void Commanded_Effective_ReadBack_Separation()
    {
        var state = new ProcessIoOutputState
        {
            Tag = "DO-023",
            Requested = "ON",
            Admitted = true,
            Dispatched = true,
            Commanded = "ON",
        };
        Assert.Equal("UNKNOWN", state.Effective);
        Assert.Equal("UNKNOWN", state.ReadBack);
        Assert.Equal("ON", state.Commanded);
    }

    [Fact]
    public void Transport_Boundary_Covers_All_Four_Io_Classes()
    {
        var transport = new SimulatedProcessIoTransport();
        transport.WriteCoil(ProcessIoDevices.RackT8, 0, true);
        transport.WriteSingleRegister(ProcessIoDevices.RackT8, 0, 100);
        Assert.True(transport.ReadCoil(ProcessIoDevices.RackT8, 0));
        Assert.Equal((ushort)100, transport.ReadRegister(ProcessIoDevices.RackT8, 0));
        transport.SeedDiscreteInput(ProcessIoDevices.RackT8, 0, true);
        transport.SeedInputRegister(ProcessIoDevices.RackT8, 0, 200);
        Assert.True(transport.ReadDiscreteInput(ProcessIoDevices.RackT8, 0));
        Assert.Equal((ushort)200, transport.ReadInputRegister(ProcessIoDevices.RackT8, 0));
    }

    [Fact]
    public void Analog_Input_Observations_Preserve_Engineering_Classification_Across_Ai_Tags()
    {
        var transport = new SimulatedProcessIoTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var rows = LoadCommittedCatalogue();

        for (int i = 0; i < 36; i++)
        {
            transport.SeedInputRegister(ProcessIoDevices.RackT8, i, 16384);
        }

        var ai001 = rows.First(r => r.Tag == "AI-001+");
        Assert.True(session.TryReadAnalogInput("AI-001+", ai001, At, out var obs001));
        Assert.Equal("ENGINEERING", obs001.Scaling);
        Assert.True(obs001.Engineering.ContainsKey("level"));
        Assert.True(obs001.Engineering.ContainsKey("percent"));

        var ai002 = rows.First(r => r.Tag == "AI-002+");
        Assert.True(session.TryReadAnalogInput("AI-002+", ai002, At, out var obs002));
        Assert.Equal("ENGINEERING", obs002.Scaling);
        Assert.True(obs002.Engineering.ContainsKey("bar"));

        var ai003 = rows.First(r => r.Tag == "AI-003+");
        Assert.True(session.TryReadAnalogInput("AI-003+", ai003, At, out var obs003));
        Assert.Equal("ENGINEERING", obs003.Scaling);
        Assert.True(obs003.Engineering.ContainsKey("bar"));

        for (int i = 4; i <= 9; i++)
        {
            string tag = $"AI-{i:D3}+";
            var row = rows.First(r => r.Tag == tag);
            Assert.True(session.TryReadAnalogInput(tag, row, At, out var obs));
            Assert.Equal("ENGINEERING", obs.Scaling);
            Assert.True(obs.Engineering.ContainsKey("bar"));
        }

        var ai012 = rows.First(r => r.Tag == "AI-012");
        Assert.True(session.TryReadAnalogInput("AI-012", ai012, At, out var obs012));
        Assert.Equal("RAW_ONLY", obs012.Scaling);
        Assert.Empty(obs012.Engineering);

        var ai036 = rows.First(r => r.Tag == "AI-036");
        Assert.Equal("SPARE", ai036.SourceClassification);
        Assert.False(session.TryReadAnalogInput("AI-036", ai036, At, out _));
    }

    [Fact]
    public void Spare_Analog_Row_Cannot_Produce_Product_Observation_And_Performs_Zero_Reads()
    {
        var transport = new ReadCountingProcessIoTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var ai036 = LoadCommittedCatalogue().First(r => r.Tag == "AI-036");

        // The Workbook-derived physical binding is preserved: only the Product observation is refused.
        Assert.Equal("SPARE", ai036.SourceClassification);
        Assert.Equal(ProcessIoCommissioningState.EXCLUDED, ai036.CommissioningState);
        Assert.NotNull(ai036.Binding);
        Assert.Equal(35, ai036.Binding!.Address);
        Assert.Equal(ProcessIoBindingState.BOUND_LEGACY_EXACT, ai036.BindingState);
        Assert.Equal("DERIVED_FROM_LEGACY_EVIDENCE", ai036.Address.State);

        Assert.False(session.TryReadAnalogInput("AI-036", ai036, At, out var observation));
        Assert.Null(observation);
        Assert.Equal(0, transport.TotalReads);
    }

    [Fact]
    public void Spare_Digital_Rows_Cannot_Produce_Product_Observation_And_Perform_Zero_Reads()
    {
        var transport = new ReadCountingProcessIoTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var rows = LoadCommittedCatalogue();

        foreach (string tag in new[] { "DI-038", "DI-039", "DI-040" })
        {
            var row = rows.First(r => r.Tag == tag);
            Assert.Equal("SPARE", row.SourceClassification);
            Assert.Equal(ProcessIoCommissioningState.EXCLUDED, row.CommissioningState);
            Assert.NotNull(row.Binding);
            Assert.Equal(ProcessIoArea.DiscreteInput, row.Binding!.Area);

            Assert.False(session.TryReadDigitalInput(tag, row, At, out var digital));
            Assert.Null(digital);
        }

        Assert.Equal(0, transport.TotalReads);
    }

    [Fact]
    public void Any_Non_Used_Classification_Cannot_Produce_Product_Observation()
    {
        var transport = new ReadCountingProcessIoTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var used = LoadCommittedCatalogue().First(r => r.Tag == "DI-001");
        Assert.Equal("USED", used.SourceClassification);

        var notUsed = used with { SourceClassification = "UNRESOLVED" };
        Assert.False(session.TryReadDigitalInput("DI-001", notUsed, At, out var observation));
        Assert.Null(observation);
        Assert.Equal(0, transport.TotalReads);
    }

    [Fact]
    public void Used_Digital_And_Analog_Rows_Still_Read_Normally()
    {
        var transport = new ReadCountingProcessIoTransport();
        transport.SeedDiscreteInput(ProcessIoDevices.RackT8, 0, true);
        transport.SeedInputRegister(ProcessIoDevices.RackT8, 0, 16384);
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(At);
        var rows = LoadCommittedCatalogue();

        var di001 = rows.First(r => r.Tag == "DI-001");
        Assert.Equal("USED", di001.SourceClassification);
        Assert.True(session.TryReadDigitalInput("DI-001", di001, At, out var digital));
        Assert.NotNull(digital);
        Assert.Equal("GOOD", digital.Quality);

        var ai001 = rows.First(r => r.Tag == "AI-001+");
        Assert.Equal("USED", ai001.SourceClassification);
        Assert.True(session.TryReadAnalogInput("AI-001+", ai001, At, out var analog));
        Assert.NotNull(analog);
        Assert.Equal("ENGINEERING", analog.Scaling);

        Assert.Equal(1, transport.DiscreteInputReads);
        Assert.Equal(1, transport.InputRegisterReads);
        Assert.Equal(2, transport.TotalReads);
    }

    /// <summary>
    /// Deterministic transport spy: counts every read so a refused Product observation can be
    /// proven to perform zero transport I/O, and refuses every write (a read path must never
    /// write). No device access.
    /// </summary>
    private sealed class ReadCountingProcessIoTransport : IProcessIoTransport
    {
        private readonly SimulatedProcessIoTransport _inner = new();

        public int DiscreteInputReads { get; private set; }
        public int CoilReads { get; private set; }
        public int InputRegisterReads { get; private set; }
        public int RegisterReads { get; private set; }
        public int BulkReads { get; private set; }
        public int TotalReads => DiscreteInputReads + CoilReads + InputRegisterReads + RegisterReads + BulkReads;
        public int NetworkAttempts => TotalReads;

        public void SeedDiscreteInput(string deviceKey, int address, bool value)
            => _inner.SeedDiscreteInput(deviceKey, address, value);

        public void SeedInputRegister(string deviceKey, int address, ushort value)
            => _inner.SeedInputRegister(deviceKey, address, value);

        public bool ReadDiscreteInput(string deviceKey, int address)
        {
            DiscreteInputReads++;
            return _inner.ReadDiscreteInput(deviceKey, address);
        }

        public bool ReadCoil(string deviceKey, int coilAddress)
        {
            CoilReads++;
            return _inner.ReadCoil(deviceKey, coilAddress);
        }

        public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity)
        {
            BulkReads++;
            return _inner.ReadCoilsBulk(deviceKey, startAddress, quantity);
        }

        public ushort ReadInputRegister(string deviceKey, int registerAddress)
        {
            InputRegisterReads++;
            return _inner.ReadInputRegister(deviceKey, registerAddress);
        }

        public ushort ReadRegister(string deviceKey, int registerAddress)
        {
            RegisterReads++;
            return _inner.ReadRegister(deviceKey, registerAddress);
        }

        public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity)
        {
            BulkReads++;
            return _inner.ReadHoldingRegistersBulk(deviceKey, startAddress, quantity);
        }

        public void WriteCoil(string deviceKey, int coilAddress, bool value)
            => throw new InvalidOperationException("a Product observation path must never write");

        public void WriteSingleRegister(string deviceKey, int registerAddress, ushort value)
            => throw new InvalidOperationException("a Product observation path must never write");

        public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values)
            => throw new InvalidOperationException("a Product observation path must never write");
    }
}
