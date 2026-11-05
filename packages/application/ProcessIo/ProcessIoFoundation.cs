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

    /// <summary>Read coils in bulk (FC01 coil read).</summary>
    IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity);

    /// <summary>Read one analog-input raw word (FC04 input register read).</summary>
    ushort ReadInputRegister(string deviceKey, int registerAddress);

    /// <summary>Read one analog output process-image read-back word (FC03 holding register read).</summary>
    ushort ReadRegister(string deviceKey, int registerAddress);

    /// <summary>Read holding registers in bulk (FC03 holding register read).</summary>
    IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity);

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

    public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity)
    {
        var list = new List<bool>(quantity);
        for (int i = 0; i < quantity; i++)
        {
            list.Add(_coils.TryGetValue((deviceKey, startAddress + i), out var v) && v);
        }
        return list;
    }

    public ushort ReadInputRegister(string deviceKey, int registerAddress)
        => _inputRegisters.TryGetValue((deviceKey, registerAddress), out var v) ? v : (ushort)0;

    public ushort ReadRegister(string deviceKey, int registerAddress)
        => _holdingRegisters.TryGetValue((deviceKey, registerAddress), out var v) ? v : (ushort)0;

    public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity)
    {
        var list = new List<ushort>(quantity);
        for (int i = 0; i < quantity; i++)
        {
            list.Add(_holdingRegisters.TryGetValue((deviceKey, startAddress + i), out var v) ? v : (ushort)0);
        }
        return list;
    }

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
    /// <summary>Authoritative source classification that may produce a Product observation.</summary>
    private const string ObservableSourceClassification = "USED";

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

    /// <summary>
    /// Writes one analog word using the general 0 to 20 mA helper. This is NOT the Product Pump
    /// command path: the owner of the Pump speed reference is
    /// <see cref="DispatchPumpAnalogOutput"/>, whose conversion is the commissioned 4 to 20 mA map.
    /// The general entry is retained unchanged for the existing Hardware bench diagnostic caller.
    /// </summary>
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

    /// <summary>
    /// Writes one typed Pump analog command through the commissioned 4 to 20 mA conversion. The
    /// session remains the only write authority: this method only turns an already-admitted token and
    /// an already-validated command into one register write.
    ///
    /// The engineering identity is written to the audit trail as the command kind, because the raw
    /// word is 0 for both the 4 mA live minimum and the 0 mA safe-stop sentinel.
    /// </summary>
    public bool DispatchPumpAnalogOutput(
        DateTimeOffset at,
        string token,
        ProcessIoCatalogueRow row,
        PumpAnalogCommand command)
    {
        if (State != "OPEN" || _stopped) return false;
        if (!_liveTokens.Remove(token)) return false;
        _spentTokens.Add(token);
        if (row.Binding is null || row.BindingState == ProcessIoBindingState.UNBOUND) return false;
        if (!PumpAnalogCommandContract.TryToRaw(command, out int raw)) return false;

        _transport.WriteSingleRegister(row.Binding.DeviceKey, row.Binding.Address, (ushort)raw);
        AuditTrail.Add(
            $"DISPATCH_AO {row.Tag} {command.KindCode} {command.Milliamps} mA (raw {raw}) {at:O}");
        return true;
    }

    /// <summary>
    /// Fail-closed Product observation gate. Only a row whose authoritative source classification
    /// is USED can produce a Product DI or AI observation. A SPARE row keeps its Workbook-derived
    /// physical binding, so the explicit Hardware bench still reads the canonical physical address
    /// ranges for commissioning, but the Product never observes it: no transport call is made and
    /// no observation is fabricated.
    /// </summary>
    private static bool IsProductObservable(ProcessIoCatalogueRow row)
        => string.Equals(row.SourceClassification, ObservableSourceClassification, StringComparison.Ordinal);

    public bool TryReadDigitalInput(
        string tag,
        ProcessIoCatalogueRow row,
        DateTimeOffset at,
        out ProcessIoDigitalInputObservation observation)
    {
        observation = null!;
        if (State != "OPEN") return false;
        if (!IsProductObservable(row)) return false;
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
        if (!IsProductObservable(row)) return false;
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
        else if (tag == "AI-002+" || ProcessIoIsolationValves.IsOutletPressureTag(tag))
        {
            // WC pressure: the Pump Inlet diagnostic row and all eight IV outlet rows. The IV family is
            // resolved by its canonical membership authority, so AI-010+ and AI-011+ scale exactly like
            // AI-004+ through AI-009+ instead of falling through to RAW_ONLY at the decimal-width boundary.
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

    // ------------------------------------------------------------ physical observation model
    //
    // The two methods below are the ONLY way Product code obtains process evidence. They never
    // assume GOOD: quality is derived from what this session can actually justify (state, row
    // binding, generation, engineering domain, and whether the transport returned at all), and the
    // received timestamp is captured separately from the claimed observation instant.

    /// <summary>Provenance of this session's transport, determined from the injected instance.</summary>
    public ProcessIoObservationProvenance TransportProvenance =>
        _transport is SimulatedProcessIoTransport
            ? ProcessIoObservationProvenance.SIMULATED_READ
            : ProcessIoObservationProvenance.PHYSICAL_READ;

    /// <summary>
    /// Observes one analog input row as a typed physical observation, using caller-supplied timestamps.
    /// Refusals are values with a typed code and no transport call; a transport fault is captured as
    /// BAD, never propagated.
    ///
    /// This overload cannot bracket the read: both timestamps are evaluated by the caller before the
    /// call, so the transport has not run yet when <paramref name="receivedAt"/> is determined. A caller
    /// that wants the elapsed read time reflected in freshness must use the
    /// <see cref="TimeProvider"/> overload instead.
    /// </summary>
    public ProcessIoPhysicalObservation ObserveAnalogInput(
        string tag,
        ProcessIoCatalogueRow? row,
        DateTimeOffset observedAt,
        DateTimeOffset receivedAt,
        TimeSpan freshnessBudget) =>
        ObserveAnalogInputCore(tag, row, observedAt, () => receivedAt, freshnessBudget);

    /// <summary>
    /// Observes one analog input row as a typed physical observation, bracketing the physical transport
    /// read with the supplied clock.
    ///
    /// C# evaluates arguments before the call, so a caller cannot capture a receive instant that lies
    /// after a read it has not issued yet. This overload therefore takes the clock instead of the
    /// timestamps and captures them itself: <paramref name="time"/> is read immediately before the
    /// transport read and again immediately after it. A read that consumes more than
    /// <paramref name="freshnessBudget"/> is consequently reported STALE with Fresh false, rather than
    /// silently accepted with identical timestamps.
    ///
    /// This is the same observation authority as the timestamp overload: one shared implementation, one
    /// read, one engineering conversion, one quality/freshness/generation decision. It never writes,
    /// never retries and never fabricates a reading.
    /// </summary>
    public ProcessIoPhysicalObservation ObserveAnalogInput(
        string tag,
        ProcessIoCatalogueRow? row,
        TimeProvider time,
        TimeSpan freshnessBudget)
    {
        ArgumentNullException.ThrowIfNull(time);
        return ObserveAnalogInputCore(tag, row, time.GetUtcNow(), () => time.GetUtcNow(), freshnessBudget);
    }

    /// <summary>
    /// The single analog observation authority both overloads share. <paramref name="receivedAt"/> is a
    /// factory rather than a value so the receive instant is only determined once the transport read has
    /// actually returned; the timestamp overload simply supplies a constant.
    /// </summary>
    private ProcessIoPhysicalObservation ObserveAnalogInputCore(
        string tag,
        ProcessIoCatalogueRow? row,
        DateTimeOffset observedAt,
        Func<DateTimeOffset> receivedAt,
        TimeSpan freshnessBudget)
    {
        string? refusal = ClassifyObservationRefusal(row);
        if (refusal is not null || row is null || row.Binding is null)
        {
            return Refused(tag, observedAt, receivedAt(), refusal ?? ProcessIoRefusals.ObservationRefusedUnbound);
        }

        int raw;
        try
        {
            raw = row.Binding.Area == ProcessIoArea.InputRegister
                ? _transport.ReadInputRegister(row.Binding.DeviceKey, row.Binding.Address)
                : _transport.ReadRegister(row.Binding.DeviceKey, row.Binding.Address);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return Refused(tag, observedAt, receivedAt(), ProcessIoRefusals.ObservationTransportFault)
                with { RawValue = 0 };
        }

        // The receive instant is taken only now, after the read returned. Everything below judges the
        // span the transport actually consumed.
        DateTimeOffset received = receivedAt();

        bool converted = TryEngineering(tag, (ushort)raw, out double engineering, out string unit);
        bool finite = !converted || double.IsFinite(engineering);
        bool inDomain = raw is >= 0 and <= ProcessIoRepresentation.RawFullScale;

        ProcessIoObservationQuality quality = !finite || !inDomain
            ? ProcessIoObservationQuality.BAD
            : ProcessIoObservationQuality.GOOD;

        bool fresh = quality == ProcessIoObservationQuality.GOOD
            && observedAt <= received
            && received - observedAt <= freshnessBudget;

        if (quality == ProcessIoObservationQuality.GOOD && !fresh)
        {
            quality = ProcessIoObservationQuality.STALE;
        }

        return new ProcessIoPhysicalObservation
        {
            Tag = tag,
            RawValue = raw,
            EngineeringValue = converted ? engineering : null,
            Unit = converted ? unit : null,
            Quality = quality,
            ObservedAt = observedAt,
            ReceivedAt = received,
            SourceGeneration = Generation,
            Fresh = fresh,
            Provenance = TransportProvenance,
            DiagnosticCode = quality switch
            {
                ProcessIoObservationQuality.GOOD => "OK",
                ProcessIoObservationQuality.STALE => ProcessIoRefusals.ObservationStale,
                _ => ProcessIoRefusals.ObservationOutOfDomain,
            },
        };
    }

    /// <summary>
    /// Observes one digital input row, using caller-supplied timestamps. The bit is reported as read; the
    /// caller decides what a named state means. Quality and freshness follow the same rules as the analog
    /// path.
    ///
    /// This overload cannot bracket the read: both timestamps are evaluated by the caller before the
    /// call, so the transport has not run yet when <paramref name="receivedAt"/> is determined. A caller
    /// that wants the elapsed read time reflected in freshness must use the
    /// <see cref="TimeProvider"/> overload instead.
    /// </summary>
    public ProcessIoPhysicalObservation ObserveDigitalInput(
        string tag,
        ProcessIoCatalogueRow? row,
        DateTimeOffset observedAt,
        DateTimeOffset receivedAt,
        TimeSpan freshnessBudget) =>
        ObserveDigitalInputCore(tag, row, observedAt, () => receivedAt, freshnessBudget);

    /// <summary>
    /// Observes one digital input row, bracketing the physical transport read with the supplied clock.
    ///
    /// The same argument-evaluation defect the analog path had applies here: a caller cannot capture a
    /// receive instant that lies after a read it has not issued yet. This overload takes the clock and
    /// captures both instants itself, so a bit that takes longer than
    /// <paramref name="freshnessBudget"/> to arrive is reported STALE with Fresh false instead of being
    /// accepted as current field feedback.
    ///
    /// It shares one implementation with the timestamp overload: one read, one quality/freshness/
    /// generation decision. It never writes, never retries and never fabricates a bit.
    /// </summary>
    public ProcessIoPhysicalObservation ObserveDigitalInput(
        string tag,
        ProcessIoCatalogueRow? row,
        TimeProvider time,
        TimeSpan freshnessBudget)
    {
        ArgumentNullException.ThrowIfNull(time);
        return ObserveDigitalInputCore(tag, row, time.GetUtcNow(), () => time.GetUtcNow(), freshnessBudget);
    }

    /// <summary>
    /// The single digital observation authority both overloads share. <paramref name="receivedAt"/> is a
    /// factory rather than a value so the receive instant is only determined once the transport read has
    /// actually returned; the timestamp overload simply supplies a constant.
    /// </summary>
    private ProcessIoPhysicalObservation ObserveDigitalInputCore(
        string tag,
        ProcessIoCatalogueRow? row,
        DateTimeOffset observedAt,
        Func<DateTimeOffset> receivedAt,
        TimeSpan freshnessBudget)
    {
        string? refusal = ClassifyObservationRefusal(row);
        if (refusal is not null || row is null || row.Binding is null)
        {
            return Refused(tag, observedAt, receivedAt(), refusal ?? ProcessIoRefusals.ObservationRefusedUnbound);
        }

        bool raw;
        try
        {
            raw = row.Binding.Area == ProcessIoArea.DiscreteInput
                ? _transport.ReadDiscreteInput(row.Binding.DeviceKey, row.Binding.Address)
                : _transport.ReadCoil(row.Binding.DeviceKey, row.Binding.Address);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return Refused(tag, observedAt, receivedAt(), ProcessIoRefusals.ObservationTransportFault);
        }

        // The receive instant is taken only now, after the read returned.
        DateTimeOffset received = receivedAt();

        bool fresh = observedAt <= received && received - observedAt <= freshnessBudget;
        return new ProcessIoPhysicalObservation
        {
            Tag = tag,
            RawValue = raw ? 1 : 0,
            EngineeringValue = null,
            Unit = null,
            Quality = fresh ? ProcessIoObservationQuality.GOOD : ProcessIoObservationQuality.STALE,
            ObservedAt = observedAt,
            ReceivedAt = received,
            SourceGeneration = Generation,
            Fresh = fresh,
            Provenance = TransportProvenance,
            DiagnosticCode = fresh ? "OK" : ProcessIoRefusals.ObservationStale,
        };
    }

    /// <summary>Shared refusal classification: state, observability, binding and commissioning.</summary>
    private string? ClassifyObservationRefusal(ProcessIoCatalogueRow? row)
    {
        if (State != "OPEN" || _stopped) return ProcessIoRefusals.SessionNotOpen;
        if (row is null) return ProcessIoRefusals.ObservationRefusedUnbound;
        if (!IsProductObservable(row)) return ProcessIoRefusals.ObservationRefusedNotCommissioned;
        if (row.Binding is null || row.BindingState == ProcessIoBindingState.UNBOUND)
            return ProcessIoRefusals.ObservationRefusedUnbound;
        if (row.CommissioningState == ProcessIoCommissioningState.EXCLUDED)
            return ProcessIoRefusals.ObservationRefusedNotCommissioned;
        return null;
    }

    /// <summary>A refused observation carries no value and can never be mistaken for evidence.</summary>
    private ProcessIoPhysicalObservation Refused(
        string tag,
        DateTimeOffset observedAt,
        DateTimeOffset receivedAt,
        string code) =>
        new()
        {
            Tag = tag,
            RawValue = 0,
            EngineeringValue = null,
            Unit = null,
            Quality = code == ProcessIoRefusals.ObservationRefusedNotCommissioned
                ? ProcessIoObservationQuality.DISABLED
                : ProcessIoObservationQuality.BAD,
            ObservedAt = observedAt,
            ReceivedAt = receivedAt,
            SourceGeneration = Generation,
            Fresh = false,
            Provenance = ProcessIoObservationProvenance.NOT_OBSERVED,
            DiagnosticCode = code,
        };

    /// <summary>
    /// The adopted engineering conversion for one analog tag. Reuses the exact constants; returns
    /// false for a raw-only row so no engineering value is fabricated.
    /// </summary>
    private static bool TryEngineering(string tag, ushort raw, out double value, out string unit)
    {
        value = 0;
        unit = string.Empty;

        if (tag == "AI-001+")
        {
            value = ProcessIoEngineering.WaterTankLevel(raw);
            unit = "level_units";
            return true;
        }

        if (tag == "AI-003+")
        {
            value = ProcessIoEngineering.PumpOutletPressureBar(raw);
            unit = "bar";
            return true;
        }

        if (tag == "AI-002+" || ProcessIoIsolationValves.IsOutletPressureTag(tag))
        {
            value = ProcessIoEngineering.WcPressureBar(raw);
            unit = "bar";
            return true;
        }

        return false;
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
