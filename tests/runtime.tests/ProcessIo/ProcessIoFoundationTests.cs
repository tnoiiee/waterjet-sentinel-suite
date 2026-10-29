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
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "evidence", "process-io-inventory-2026-10-22.csv");
        return ProcessIoCatalogueLoader.Load(File.ReadAllText(path));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WaterJetSentinelSuite.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

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
        string csv = File.ReadAllText(Path.Combine(FindRepoRoot(), "docs", "evidence", "process-io-inventory-2026-10-22.csv"));
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
}
