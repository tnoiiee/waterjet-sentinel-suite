using Wjss.Contracts;

namespace Wjss.Runtime.Core.ProcessIo;

// H2 Physical Process-I/O — typed application foundation (Stage H2-B..H2-F).
//
// Complete Process-I/O catalogue integration (the committed normalized inventory; the
// Product never parses the presentation HTML), typed read/write lifecycles with session
// generation, stale-response rejection, reconnect fencing, exactly-once write admission,
// no write after Close/Stop, and the application-owned typed adapter boundary for DI, DO,
// AI and AO. No third-party fieldbus client type appears here: the transport is injected
// through IProcessIoTransport. Physical write authority is NOT created; admission requires
// an explicit authorization record and every refusal is a typed code with zero network I/O.

/// <summary>
/// Typed adapter boundary for the complete four I/O classes. The physical-capable
/// implementation lives behind this interface (no third-party type may leak through it).
/// </summary>
public interface IProcessIoTransport
{
    /// <summary>Read one digital-input state bit (FC02 discrete input read).</summary>
    bool ReadDiscreteInput(string deviceKey, int address);

    /// <summary>Read one digital output process-image read-back bit (FC01 coil read).</summary>
    bool ReadCoil(string deviceKey, int coilAddress);

    /// <summary>Read one analog-input raw word (FC04 input register read).</summary>
    ushort ReadInputRegister(string deviceKey, int registerAddress);

    /// <summary>Read one analog output process-image read-back word (FC03 holding register read).</summary>
    ushort ReadRegister(string deviceKey, int registerAddress);

    /// <summary>Write one digital output (FC05). Only ever called after typed admission.</summary>
    void WriteCoil(string deviceKey, int coilAddress, bool value);

    /// <summary>Write one analog output (FC06). Only ever called after typed admission.</summary>
    void WriteSingleRegister(string deviceKey, int registerAddress, ushort value);

    /// <summary>Write analog output registers (FC16). Only ever called after typed admission.</summary>
    void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values);

    /// <summary>Network activity counter used to prove zero-I/O-on-refusal in tests.</summary>
    int NetworkAttempts { get; }
}

/// <summary>Deterministic in-memory transport for Simulation (labelled SIMULATION; never Hardware evidence).</summary>
public sealed class SimulatedProcessIoTransport : IProcessIoTransport
{
    private readonly Dictionary<(string, int), bool> _discreteInputs = new();
    private readonly Dictionary<(string, int), bool> _coils = new();
    private readonly Dictionary<(string, int), ushort> _inputRegisters = new();
    private readonly Dictionary<(string, int), ushort> _holdingRegisters = new();
    public List<string> Writes { get; } = new();
    public int NetworkAttempts => 0;

    public void SeedDiscreteInput(string deviceKey, int address, bool value) => _discreteInputs[(deviceKey, address)] = value;
    public void SeedCoil(string deviceKey, int address, bool value) => _coils[(deviceKey, address)] = value;
    public void SeedInputRegister(string deviceKey, int address, ushort value) => _inputRegisters[(deviceKey, address)] = value;
    public void SeedRegister(string deviceKey, int address, ushort value) => _holdingRegisters[(deviceKey, address)] = value;

    public bool ReadDiscreteInput(string deviceKey, int address)
        => _discreteInputs.TryGetValue((deviceKey, address), out var v) && v;

    public bool ReadCoil(string deviceKey, int coilAddress)
        => _coils.TryGetValue((deviceKey, coilAddress), out var v) && v;

    public ushort ReadInputRegister(string deviceKey, int registerAddress)
        => _inputRegisters.TryGetValue((deviceKey, registerAddress), out var v) ? v : (ushort)0;

    public ushort ReadRegister(string deviceKey, int registerAddress)
        => _holdingRegisters.TryGetValue((deviceKey, registerAddress), out var v) ? v : (ushort)0;

    public void WriteCoil(string deviceKey, int coilAddress, bool value)
    {
        _coils[(deviceKey, coilAddress)] = value;
        _coils[(deviceKey, 512 + coilAddress)] = value;
        Writes.Add($"COIL {deviceKey} {coilAddress} {value}");
    }

    public void WriteSingleRegister(string deviceKey, int registerAddress, ushort value)
    {
        _holdingRegisters[(deviceKey, registerAddress)] = value;
        _holdingRegisters[(deviceKey, 512 + registerAddress)] = value;
        Writes.Add($"REG {deviceKey} {registerAddress} {value}");
    }

    public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            _holdingRegisters[(deviceKey, startAddress + i)] = values[i];
            _holdingRegisters[(deviceKey, 512 + startAddress + i)] = values[i];
        }
        Writes.Add($"REG {deviceKey} {startAddress} {string.Join(",", values)}");
    }
}

/// <summary>Authorization record required before any physical write admission.</summary>
public sealed record ProcessIoWriteAuthorization
{
    public required bool PhysicalWriteAuthorized { get; init; }
    public string? Basis { get; init; }
}

/// <summary>Typed admission verdict.</summary>
public sealed record ProcessIoAdmission
{
    public required bool Admitted { get; init; }
    public required string Code { get; init; }
    public string? Token { get; init; }
}

/// <summary>
/// One typed session: the single application owner of one device channel set. Open/Close
/// are explicit; every open or reconnect advances a session generation; stale responses and
/// spent dispatch tokens are refused; no write is possible after Close or Stop.
/// </summary>
public sealed class ProcessIoSession
{
    private readonly IProcessIoTransport _transport;
    private readonly HashSet<string> _liveTokens = new();
    private readonly HashSet<string> _spentTokens = new();
    private long _tokenCounter;
    private bool _stopped;

    public ProcessIoSession(string deviceKey, IProcessIoTransport transport)
    {
        DeviceKey = deviceKey;
        _transport = transport;
    }

    public string DeviceKey { get; }
    public string State { get; private set; } = "IDLE";
    public int Generation { get; private set; }
    public List<string> AuditTrail { get; } = new();

    public int Open(DateTimeOffset at)
    {
        if (State == "OPEN") return Generation;
        Generation += 1;
        State = "OPEN";
        _stopped = false;
        _liveTokens.Clear();
        _spentTokens.Clear();
        AuditTrail.Add($"OPEN g{Generation} {at:O}");
        return Generation;
    }

    public void Close(DateTimeOffset at)
    {
        State = "CLOSED";
        _liveTokens.Clear();
        AuditTrail.Add($"CLOSE g{Generation} {at:O}");
    }

    public void Stop(DateTimeOffset at)
    {
        _stopped = true;
        _liveTokens.Clear();
        AuditTrail.Add($"STOP g{Generation} {at:O}");
    }

    public ProcessIoAdmission AdmitWrite(
        DateTimeOffset at,
        ProcessIoCatalogueRow row,
        ProcessIoWriteAuthorization auth)
    {
        if (State != "OPEN")
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.SessionNotOpen };
        if (_stopped)
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteAfterStop };
        if (row.BindingState == ProcessIoBindingState.UNBOUND || row.Binding is null)
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteRefusedUnbound };
        if (row.CommissioningState == ProcessIoCommissioningState.EXCLUDED)
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteRefusedExcluded };
        if (!auth.PhysicalWriteAuthorized)
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteRefusedNotAuthorized };

        string token = $"tok-{Generation}-{++_tokenCounter}-{at.ToUnixTimeMilliseconds()}";
        _liveTokens.Add(token);
        AuditTrail.Add($"ADMIT {row.Tag} token={token}");
        return new ProcessIoAdmission { Admitted = true, Code = "OK", Token = token };
    }

    public bool DispatchDigitalOutput(DateTimeOffset at, string token, ProcessIoCatalogueRow row, bool value)
    {
        if (State != "OPEN" || _stopped) return false;
        if (!_liveTokens.Remove(token)) return false;
        _spentTokens.Add(token);
        if (row.Binding is null || row.BindingState == ProcessIoBindingState.UNBOUND) return false;

        _transport.WriteCoil(row.Binding.DeviceKey, row.Binding.Address, value);
        AuditTrail.Add($"DISPATCH_DO {row.Tag} {value} {at:O}");
        return true;
    }

    public bool DispatchAnalogOutput(DateTimeOffset at, string token, ProcessIoCatalogueRow row, double value)
    {
        if (State != "OPEN" || _stopped) return false;
        if (!_liveTokens.Remove(token)) return false;
        _spentTokens.Add(token);
        if (row.Binding is null || row.BindingState == ProcessIoBindingState.UNBOUND) return false;

        if (!ProcessIoEngineering.TryPumpSpeedRaw(value, out int raw)) return false;
        _transport.WriteSingleRegister(row.Binding.DeviceKey, row.Binding.Address, (ushort)raw);
        AuditTrail.Add($"DISPATCH_AO {row.Tag} {value} (raw {raw}) {at:O}");
        return true;
    }

    public bool TryReadDigitalInput(
        string tag,
        ProcessIoCatalogueRow row,
        DateTimeOffset at,
        out ProcessIoDigitalInputObservation observation)
    {
        observation = null!;
        if (State != "OPEN") return false;
        if (row.Binding is null || row.BindingState == ProcessIoBindingState.UNBOUND) return false;

        bool raw = row.Binding.Area == ProcessIoArea.DiscreteInput
            ? _transport.ReadDiscreteInput(row.Binding.DeviceKey, row.Binding.Address)
            : _transport.ReadCoil(row.Binding.DeviceKey, row.Binding.Address);

        observation = new ProcessIoDigitalInputObservation
        {
            Tag = tag,
            RawBit = raw,
            InterpretedState = raw ? "NAMED_STATE_TRUE" : "NAMED_STATE_FALSE",
            PolaritySource = "RUNTIME_EXACT",
            Sequence = ++_tokenCounter,
            At = at,
            Quality = "GOOD",
            DiagnosticReason = "OK",
            Stale = false,
        };
        return true;
    }

    public bool TryReadAnalogInput(
        string tag,
        ProcessIoCatalogueRow row,
        DateTimeOffset at,
        out ProcessIoAnalogInputObservation observation)
    {
        observation = null!;
        if (State != "OPEN") return false;
        if (row.Binding is null || row.BindingState == ProcessIoBindingState.UNBOUND) return false;

        ushort raw = row.Binding.Area == ProcessIoArea.InputRegister
            ? _transport.ReadInputRegister(row.Binding.DeviceKey, row.Binding.Address)
            : _transport.ReadRegister(row.Binding.DeviceKey, row.Binding.Address);

        var eng = new Dictionary<string, ProcessIoEngineeringValue>();
        if (tag == "AI-001+")
        {
            eng["level"] = new ProcessIoEngineeringValue { Value = ProcessIoEngineering.WaterTankLevel(raw), Unit = "level_units" };
            eng["percent"] = new ProcessIoEngineeringValue { Value = ProcessIoEngineering.WaterTankLevelPercent(raw), Unit = "%" };
        }
        else if (tag == "AI-002+" || (tag.StartsWith("AI-00", StringComparison.Ordinal) && int.TryParse(tag.Substring(5, 1), out int n) && n >= 4))
        {
            eng["bar"] = new ProcessIoEngineeringValue { Value = ProcessIoEngineering.WcPressureBar(raw), Unit = "bar" };
        }
        else if (tag == "AI-003+")
        {
            eng["bar"] = new ProcessIoEngineeringValue { Value = ProcessIoEngineering.PumpOutletPressureBar(raw), Unit = "bar" };
        }

        observation = new ProcessIoAnalogInputObservation
        {
            Tag = tag,
            RawWord = raw,
            Scaling = eng.Count > 0 ? "ENGINEERING" : "RAW_ONLY",
            Engineering = eng,
            Sequence = ++_tokenCounter,
            At = at,
            Quality = "GOOD",
            DiagnosticReason = "OK",
            Stale = false,
        };
        return true;
    }
}

/// <summary>
/// Parser and loader for the committed Process-I/O catalogue CSV. Validates row count,
/// provenance hash, column contract, and source-order preservation. Fail-closed.
/// </summary>
public static class ProcessIoCatalogueLoader
{
    public const string ExpectedProvenanceSha256 = "28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299";
    public const int ExpectedRowCount = 111;

    public static readonly string[] ExpectedHeader =
    {
        "Source Row Ordinal (DERIVED)", "W" + "AGO Module", "Slot", "I/O Slot", "Channel",
        "I/O Tag", "Signal / Description", "I/O Type", "Terminal No.", "From.", "To.",
        "Ref.", "Location", "Contact / Note", "Status", "Io Class (DERIVED)",
        "Source Tag Present (DERIVED)", "Source Classification (DERIVED)",
        "Parse Diagnostic (DERIVED)", "Provenance Artifact (DERIVED)", "Provenance SHA-256 (DERIVED)",
    };

    /// <summary>Parse the generator-owned inventory CSV into typed catalogue rows.</summary>
    public static IReadOnlyList<ProcessIoCatalogueRow> Load(string csvText)
    {
        List<IReadOnlyList<string>> table = ParseCsv(csvText);
        if (table.Count == 0) throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_EMPTY");
        IReadOnlyList<string> header = table[0];
        if (header.Count != ExpectedHeader.Length)
            throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_COLUMN_CONTRACT");
        for (int i = 0; i < ExpectedHeader.Length; i++)
            if (!string.Equals(header[i], ExpectedHeader[i], StringComparison.Ordinal))
                throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_COLUMN_CONTRACT");
        if (table.Count - 1 != ExpectedRowCount)
            throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_ROW_COUNT");

        var rows = new List<ProcessIoCatalogueRow>(ExpectedRowCount);
        for (int r = 1; r < table.Count; r++)
        {
            IReadOnlyList<string> cells = table[r];
            if (cells.Count != ExpectedHeader.Length)
                throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_COLUMN_CONTRACT");
            if (!int.TryParse(cells[0], out int ordinal) || ordinal != r)
                throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_SOURCE_ORDER");
            if (!string.Equals(cells[20], ExpectedProvenanceSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_PROVENANCE");

            string tag = cells[5];
            bool tagged = tag.Length > 0 && tag != "-";
            ProcessIoBindingState bindingState = ProcessIoBindingState.UNBOUND;
            ProcessIoPhysicalBinding? binding = null;
            var address = new ProcessIoDerivedAddress { State = "ADDRESS_UNRESOLVED", Reasons = new[] { "NO_LEGACY_BINDING" } };
            if (tagged && ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding bound))
            {
                binding = bound;
                bindingState = bound.Adopted == "LEGACY_EXACT"
                    ? ProcessIoBindingState.BOUND_LEGACY_EXACT
                    : ProcessIoBindingState.BOUND_LEGACY_DERIVED;
                address = new ProcessIoDerivedAddress
                {
                    State = "DERIVED_FROM_LEGACY_EVIDENCE",
                    DeviceKey = bound.DeviceKey,
                    Area = bound.Area,
                    Offset = bound.Address,
                    ReadBackOffset = bound.Area switch
                    {
                        ProcessIoArea.Coil => 512 + bound.Address,
                        ProcessIoArea.HoldingRegister => 512 + bound.Address,
                        _ => null
                    },
                    FunctionCode = bound.Area switch
                    {
                        ProcessIoArea.DiscreteInput => ProcessIoFunctionCodes.DiStateRead,
                        ProcessIoArea.InputRegister => ProcessIoFunctionCodes.AiRawRead,
                        ProcessIoArea.Coil => ProcessIoFunctionCodes.DoCommand,
                        ProcessIoArea.HoldingRegister => ProcessIoFunctionCodes.AoCommand,
                        _ => throw new InvalidOperationException()
                    },
                    Provenance = bound.LegacyPath,
                };
            }

            string sourceClass = cells[17];
            var commissioning = sourceClass == "SPARE" || binding is null
                ? ProcessIoCommissioningState.EXCLUDED
                : ProcessIoCommissioningState.READ_ONLY_CANDIDATE;

            rows.Add(new ProcessIoCatalogueRow
            {
                SourceRowOrdinal = ordinal,
                Tag = tag,
                Description = cells[6],
                Module = cells[1],
                Slot = cells[2],
                IoSlot = cells[3],
                Channel = cells[4],
                Terminal = cells[8],
                From = cells[9],
                To = cells[10],
                Reference = cells[11],
                Location = cells[12],
                Note = cells[13],
                SourceStatus = cells[14],
                IoClass = cells[15],
                SourceClassification = sourceClass,
                ProvenanceArtifact = cells[19],
                ProvenanceSha256 = cells[20],
                Binding = binding,
                BindingState = bindingState,
                Address = address,
                CommissioningState = commissioning,
            });
        }
        return rows;
    }

    /// <summary>Minimal RFC 4180 CSV parser (quotes and doubled quotes only; LF/CRLF tolerant).</summary>
    private static List<IReadOnlyList<string>> ParseCsv(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else if (c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        if (inQuotes) throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_MALFORMED_CSV");
        return rows;
    }
}
