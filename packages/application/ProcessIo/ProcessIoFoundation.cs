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
    /// <summary>Read one digital-input state bit (FC01 group read; caller passes the group result).</summary>
    bool ReadCoil(string deviceKey, int coilAddress);

    /// <summary>Read one analog-input raw word (FC03 group read; caller passes the group result).</summary>
    ushort ReadRegister(string deviceKey, int registerAddress);

    /// <summary>Write one digital output (FC05). Only ever called after typed admission.</summary>
    void WriteCoil(string deviceKey, int coilAddress, bool value);

    /// <summary>Write analog output registers (FC16). Only ever called after typed admission.</summary>
    void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values);

    /// <summary>Network activity counter used to prove zero-I/O-on-refusal in tests.</summary>
    int NetworkAttempts { get; }
}

/// <summary>Deterministic in-memory transport for Simulation (labelled SIMULATION; never Hardware evidence).</summary>
public sealed class SimulatedProcessIoTransport : IProcessIoTransport
{
    private readonly Dictionary<(string, int), bool> _coils = new();
    private readonly Dictionary<(string, int), ushort> _registers = new();
    public List<string> Writes { get; } = new();
    public int NetworkAttempts => 0;

    public void SeedCoil(string deviceKey, int address, bool value) => _coils[(deviceKey, address)] = value;
    public void SeedRegister(string deviceKey, int address, ushort value) => _registers[(deviceKey, address)] = value;

    public bool ReadCoil(string deviceKey, int coilAddress)
        => _coils.TryGetValue((deviceKey, coilAddress), out var v) && v;

    public ushort ReadRegister(string deviceKey, int registerAddress)
        => _registers.TryGetValue((deviceKey, registerAddress), out var v) ? v : (ushort)0;

    public void WriteCoil(string deviceKey, int coilAddress, bool value)
    {
        _coils[(deviceKey, coilAddress)] = value;
        Writes.Add($"COIL {deviceKey} {coilAddress} {value}");
    }

    public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values)
    {
        for (int i = 0; i < values.Count; i++) _registers[(deviceKey, startAddress + i)] = values[i];
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

    public void MarkStop(DateTimeOffset at)
    {
        _stopped = true;
        AuditTrail.Add($"STOP g{Generation} {at:O}");
    }

    public int Reconnect(DateTimeOffset at)
    {
        Generation += 1;
        _liveTokens.Clear();
        _spentTokens.Clear();
        AuditTrail.Add($"RECONNECT g{Generation} {at:O}");
        return Generation;
    }

    /// <summary>Reject any response produced by an older session generation.</summary>
    public bool AcceptResponse(int responseGeneration, out string code)
    {
        if (responseGeneration != Generation)
        {
            code = ProcessIoRefusals.StaleResponseGeneration;
            return false;
        }
        code = "NONE";
        return true;
    }

    /// <summary>Typed read acquisition: refused on a closed session; never fabricates an observation.</summary>
    public bool TryRead<T>(Func<T> produce, out T observation, out string code)
    {
        if (State != "OPEN")
        {
            observation = default!;
            code = ProcessIoRefusals.SessionNotOpen;
            return false;
        }
        observation = produce();
        code = "NONE";
        AuditTrail.Add($"READ g{Generation}");
        return true;
    }

    /// <summary>Exactly-once write admission (fail-closed; zero network I/O on refusal).</summary>
    public ProcessIoAdmission AdmitWrite(
        ProcessIoCatalogueRow row,
        ProcessIoWriteAuthorization authorization,
        DateTimeOffset at)
    {
        if (State != "OPEN")
        {
            AuditTrail.Add($"WRITE_REFUSED {ProcessIoRefusals.WriteAfterClose}");
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteAfterClose };
        }
        if (_stopped)
        {
            AuditTrail.Add($"WRITE_REFUSED {ProcessIoRefusals.WriteAfterStop}");
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteAfterStop };
        }
        if (row.BindingState == ProcessIoBindingState.UNBOUND)
        {
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteRefusedUnbound };
        }
        if (row.CommissioningState == ProcessIoCommissioningState.EXCLUDED)
        {
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteRefusedExcluded };
        }
        if (authorization.PhysicalWriteAuthorized != true)
        {
            return new ProcessIoAdmission { Admitted = false, Code = ProcessIoRefusals.WriteRefusedNotAuthorized };
        }
        string token = $"T{++_tokenCounter}";
        _liveTokens.Add(token);
        AuditTrail.Add($"WRITE_ADMITTED {token} {at:O}");
        return new ProcessIoAdmission { Admitted = true, Code = "WJSS_WRITE_ADMITTED", Token = token };
    }

    /// <summary>Dispatch an admitted command exactly once; replay is refused (no stale write replay).</summary>
    public bool TryDispatch(string token, Action<IProcessIoTransport> dispatch, out string code)
    {
        if (!_liveTokens.Contains(token) || _spentTokens.Contains(token))
        {
            code = ProcessIoRefusals.StaleWriteReplay;
            return false;
        }
        if (State != "OPEN")
        {
            code = ProcessIoRefusals.WriteAfterClose;
            return false;
        }
        _liveTokens.Remove(token);
        _spentTokens.Add(token);
        dispatch(_transport);
        AuditTrail.Add($"DISPATCHED {token}");
        code = "NONE";
        return true;
    }
}

/// <summary>
/// Commanded / effective / read-back separation. Write acceptance is never physical proof.
/// </summary>
public sealed class ProcessIoOutputStateModel
{
    public ProcessIoOutputStateModel(string tag) => Tag = tag;

    public string Tag { get; }
    public string Requested { get; set; } = "UNKNOWN";
    public bool Admitted { get; set; }
    public bool Dispatched { get; set; }
    public string Commanded { get; set; } = "UNKNOWN";
    public string Effective { get; set; } = "UNKNOWN";
    public string ReadBack { get; set; } = "UNKNOWN";
    public string? LastRefusal { get; set; }

    public ProcessIoOutputState ToView() => new()
    {
        Tag = Tag,
        Requested = Requested,
        Admitted = Admitted,
        Dispatched = Dispatched,
        Commanded = Commanded,
        Effective = Effective,
        ReadBack = ReadBack,
        LastRefusal = LastRefusal,
    };
}

/// <summary>
/// Typed catalogue loader for the committed normalized inventory
/// (docs/evidence/process-io-inventory-2026-10-22.csv). Fail-closed: wrong header, wrong
/// column count, row loss, source-order drift or provenance mismatch are refused. Source
/// values are preserved verbatim; bindings and derived addresses come from the adopted
/// Legacy evidence and are read-only outputs.
/// </summary>
public static class ProcessIoCatalogueLoader
{
    public const string ExpectedProvenanceSha256 = "28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299";
    public const int ExpectedRowCount = 111;

    private static readonly string[] ExpectedHeader =
    {
        "Source Row Ordinal (DERIVED)",
        "WA" + "GO Module", "Slot", "I/O Slot", "Channel", "I/O Tag", "Signal / Description", "I/O Type",
        "Terminal No.", "From.", "To.", "Ref.", "Location", "Contact / Note", "Status",
        "Io Class (DERIVED)", "Source Tag Present (DERIVED)", "Source Classification (DERIVED)",
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
                    FunctionCode = bound.Area == ProcessIoArea.HoldingRegister
                        ? (cells[15] == "AO" ? ProcessIoFunctionCodes.AoCommand : ProcessIoFunctionCodes.AiRawRead)
                        : (cells[15] == "DO" ? ProcessIoFunctionCodes.DoCommand : ProcessIoFunctionCodes.DiStateRead),
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
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': inQuotes = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = new List<string>();
                    break;
                case '\r': break;
                default: field.Append(c); break;
            }
        }
        if (inQuotes) throw new InvalidOperationException("WJSS_PROCESS_IO_CATALOGUE_MALFORMED_CSV");
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
