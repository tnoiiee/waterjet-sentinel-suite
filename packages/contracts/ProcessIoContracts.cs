namespace Wjss.Contracts;

// H2 Physical Process-I/O — typed Product contracts (Stage H2-B..H2-F).
//
// Every Process-I/O observation and command crosses one of these typed boundaries. The
// adopted physical values (device keys, transport shape, function codes, register/coil
// offsets, conversions, lifecycle constants) come from the Authoritative T8 fieldbus
// Mapping Workbook (the commissioned functional reference for the unchanged Hardware) and
// are mirrored 1:1 by packages/process-io/src/legacyEvidence.mjs, whose Node parity test
// pins this file. Endpoint values are Owner-local configuration and are never committed.
// No third-party fieldbus client type appears in this file. Physical write authority is NOT
// created here.

/// <summary>Device keys of the commissioned Process-I/O fleet (hosts are Owner-local).</summary>
public static class ProcessIoDevices
{
    public const string RackT8 = "PROCESS_IO_W" + "AGO_T8";
    public static readonly IReadOnlyList<string> All = new[] { RackT8 };
}

/// <summary>Committed transport shape (exact T8 physical defaults).</summary>
public static class ProcessIoTransportShape
{
    public const int Port = 502;
    public const int DefaultUnitId = 1;
    public const int UnitId = 1;
    public const int MinUnitId = 1;
    public const int MaxUnitId = 247;
    public const int ConnectTimeoutMs = 1000;
    public const int ReadTimeoutMs = 1000;
    public const int WriteTimeoutMs = 1000;
    public const int PollIntervalMs = 200;
    public const int ConnectionCheckIntervalMs = 5000;
    public const int ReadGroupStart = 0;
    public const int ReadGroupCount = 40;
    public const int MinDoHoldMs = 100;
    public const int MaxDoHoldMs = 2000;
}

/// <summary>Explicit read profiles for Process-I/O observation.</summary>
public static class ProcessIoReadProfiles
{
    public const string WorkbookCanonical = "WORKBOOK_CANONICAL";
    public const string LegacyProductionCompat = "LEGACY_PRODUCTION_COMPAT";
}

/// <summary>Canonical consent phrases for physical Process-I/O commissioning modes.</summary>
public static class ProcessIoConsentPhrases
{
    public const string PhysicalDiAiRead = "I AUTHORIZE READ ONLY PROCESS IO BENCH TRIAL";
    public const string PhysicalDoSingle = "I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL";
    public const string PhysicalAoSingle = "I AUTHORIZE SINGLE AO PROCESS IO BENCH TRIAL";
    public const string PhysicalFullSelectedSequence = "I AUTHORIZE FULL SELECTED PROCESS IO BENCH SEQUENCE";
}

/// <summary>Function codes proven on the authoritative T8 memory map.</summary>
public static class ProcessIoFunctionCodes
{
    public const string DiStateRead = "FC02";
    public const string DoReadBack = "FC01";
    public const string DoCommand = "FC05";
    public const string DoMultipleCommand = "FC15";
    public const string AiRawRead = "FC04";
    public const string AoReadBack = "FC03";
    public const string AoCommand = "FC06";
    public const string AoMultipleCommand = "FC16";
}

/// <summary>Raw representation proven by the T8 runtime (ushort words, 0..32767 full scale).</summary>
public static class ProcessIoRepresentation
{
    public const int WordWidthBits = 16;
    public const int RawFullScale = 32767;
}

public enum ProcessIoArea
{
    DiscreteInput,
    Coil,
    InputRegister,
    HoldingRegister,
}

public enum ProcessIoBindingState
{
    UNBOUND,
    BOUND_LEGACY_EXACT,
    BOUND_LEGACY_DERIVED,
}

public enum ProcessIoCommissioningState
{
    EXCLUDED,
    READ_ONLY_CANDIDATE,
}

/// <summary>Derived process-image addressing: read-only output of fixed evidence, never an input.</summary>
public sealed record ProcessIoDerivedAddress
{
    public required string State { get; init; }
    public string? DeviceKey { get; init; }
    public ProcessIoArea? Area { get; init; }
    public int? Offset { get; init; }
    public int? ReadBackOffset { get; init; }
    public string? FunctionCode { get; init; }
    public string? Provenance { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

/// <summary>Physical binding adopted from the Authoritative Workbook for one catalogue row.</summary>
public sealed record ProcessIoPhysicalBinding
{
    public required string DeviceKey { get; init; }
    public required ProcessIoArea Area { get; init; }
    public required int Address { get; init; }
    public required string Direction { get; init; }
    public required string LegacyPath { get; init; }
    public required string Adopted { get; init; }
    public string? Note { get; init; }
}

/// <summary>One complete Process-I/O catalogue row (source evidence preserved verbatim).</summary>
public sealed record ProcessIoCatalogueRow
{
    public required int SourceRowOrdinal { get; init; }
    public required string Tag { get; init; }
    public required string Description { get; init; }
    public required string Module { get; init; }
    public required string Slot { get; init; }
    public required string IoSlot { get; init; }
    public required string Channel { get; init; }
    public required string Terminal { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Reference { get; init; }
    public required string Location { get; init; }
    public required string Note { get; init; }
    public required string SourceStatus { get; init; }
    public required string IoClass { get; init; }
    public required string SourceClassification { get; init; }
    public required string ProvenanceArtifact { get; init; }
    public required string ProvenanceSha256 { get; init; }
    public ProcessIoPhysicalBinding? Binding { get; init; }
    public required ProcessIoBindingState BindingState { get; init; }
    public required ProcessIoDerivedAddress Address { get; init; }
    public required ProcessIoCommissioningState CommissioningState { get; init; }
}

/// <summary>Typed Digital Input observation.</summary>
public sealed record ProcessIoDigitalInputObservation
{
    public required string Tag { get; init; }
    public required bool RawBit { get; init; }
    public required string InterpretedState { get; init; }
    public required string PolaritySource { get; init; }
    public required long Sequence { get; init; }
    public required DateTimeOffset At { get; init; }
    public required string Quality { get; init; }
    public required string DiagnosticReason { get; init; }
    public required bool Stale { get; init; }
}

/// <summary>Typed Analog Input observation (raw word plus engineering conversions).</summary>
public sealed record ProcessIoAnalogInputObservation
{
    public required string Tag { get; init; }
    public required int RawWord { get; init; }
    public required string Scaling { get; init; }
    public IReadOnlyDictionary<string, ProcessIoEngineeringValue> Engineering { get; init; }
        = new Dictionary<string, ProcessIoEngineeringValue>();
    public required long Sequence { get; init; }
    public required DateTimeOffset At { get; init; }
    public required string Quality { get; init; }
    public required string DiagnosticReason { get; init; }
    public required bool Stale { get; init; }
}

public sealed record ProcessIoEngineeringValue
{
    public required double Value { get; init; }
    public required string Unit { get; init; }
}

/// <summary>Typed Digital Output command request (never a raw coil value at a call site).</summary>
public sealed record ProcessIoDigitalOutputCommand
{
    public required string Tag { get; init; }
    public required bool RequestedState { get; init; }
    public required DateTimeOffset At { get; init; }
}

/// <summary>Typed Analog Output command request (engineering value; raw conversion is derived).</summary>
public sealed record ProcessIoAnalogOutputCommand
{
    public required string Tag { get; init; }
    public required double EngineeringValue { get; init; }
    public required string Unit { get; init; }
    public required DateTimeOffset At { get; init; }
}

/// <summary>Commanded / effective / read-back separation for one output row.</summary>
public sealed record ProcessIoOutputState
{
    public required string Tag { get; init; }
    public string Requested { get; init; } = "UNKNOWN";
    public bool Admitted { get; init; }
    public bool Dispatched { get; init; }
    public string Commanded { get; init; } = "UNKNOWN";
    public string Effective { get; init; } = "UNKNOWN";
    public string ReadBack { get; init; } = "UNKNOWN";
    public string? LastRefusal { get; init; }
}

/// <summary>Communication state of one typed session.</summary>
public sealed record ProcessIoCommunicationState
{
    public required string DeviceKey { get; init; }
    public required string State { get; init; }
    public required int Generation { get; init; }
    public required bool Stale { get; init; }
    public required string DiagnosticReason { get; init; }
}

/// <summary>Provenance record carried by every observation and command.</summary>
public sealed record ProcessIoProvenance
{
    public required string Artifact { get; init; }
    public required string ArtifactSha256 { get; init; }
    public string? LegacyPath { get; init; }
}

/// <summary>Typed refusal vocabulary (fail-closed). Never string matching at call sites.</summary>
public static class ProcessIoRefusals
{
    public const string WriteRefusedNotAuthorized = "WJSS_WRITE_REFUSED_NOT_AUTHORIZED";
    public const string WriteRefusedUnbound = "WJSS_WRITE_REFUSED_UNBOUND";
    public const string WriteRefusedExcluded = "WJSS_WRITE_REFUSED_EXCLUDED";
    public const string WriteAfterClose = "WJSS_WRITE_AFTER_CLOSE";
    public const string WriteAfterStop = "WJSS_WRITE_AFTER_STOP";
    public const string StaleWriteReplay = "WJSS_STALE_WRITE_REPLAY";
    public const string StaleResponseGeneration = "WJSS_STALE_RESPONSE_GENERATION";
    public const string StaleResponseFreshness = "WJSS_STALE_RESPONSE_FRESHNESS";
    public const string SessionNotOpen = "WJSS_SESSION_NOT_OPEN";
    public const string ConfigRefused = "WJSS_CONFIG_REFUSED";
    public const string NoNetworkIoOnRefusal = "WJSS_NO_NETWORK_IO_ON_REFUSAL";

    // Observation refusals (typed values, never exceptions, zero network I/O on refusal).
    public const string ObservationRefusedUnbound = "WJSS_OBSERVATION_REFUSED_UNBOUND";
    public const string ObservationRefusedNotCommissioned = "WJSS_OBSERVATION_REFUSED_NOT_COMMISSIONED";
    public const string ObservationTransportFault = "WJSS_OBSERVATION_TRANSPORT_FAULT";
    public const string ObservationStale = "WJSS_OBSERVATION_STALE";
    public const string ObservationOutOfDomain = "WJSS_OBSERVATION_OUT_OF_DOMAIN";
    public const string ObservationRefusedGeneration = "WJSS_OBSERVATION_REFUSED_GENERATION";
}

/// <summary>
/// Exact engineering conversion constants. The formulas are the clamped linear map
/// chains of the runtime call sites (raw 0..32767 full scale), with the two corrections the commissioned
/// hardware forced: the Pump Outlet live zero is applied by the channel and not a second time in software,
/// and the water-level span follows Legacy rev2 (0..240). Both are recorded with their evidence in
/// docs/evidence/LEGACY_REV2_SCALING_CORRECTIONS_2026-11-07.md. Mirrored by legacyEvidence.mjs.
/// </summary>
public static class ProcessIoEngineering
{
    /// <summary>Exact map: clamp to [inMin, inMax], then linear conversion.</summary>
    public static double LegacyMap(double x, double inMin, double inMax, double outMin, double outMax)
    {
        double v = x;
        if (v >= inMax) v = inMax;
        if (v <= inMin) v = inMin;
        return (v - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
    }

    /// <summary>WC/IV pressure: raw to bar 0..40 (single stage).</summary>
    public static double WcPressureBar(int rawWord)
        => LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 40);

    /// <summary>
    /// Pump outlet pressure: raw 0..32767 to bar 0..40, ONE stage. The 4 mA live zero is already applied
    /// by the commissioned channel (750-471 slot 13 ch 3, signal type 4-20 mA, user limits 32767/-32768),
    /// so raw 0 IS 4 mA and raw 32767 IS 20 mA; subtracting the live zero a second time in software was a
    /// defect inherited from the Legacy call site (rev1 and rev2 both chain raw -&gt; 0..20 mA and then
    /// 4..20 mA -&gt; 0..40 bar), which under-reported pressure by a factor of 1.54 and silently raised the
    /// effective Pump-ready threshold from 15 bar to just over 20 bar. Field evidence at 2026-11-07 settled
    /// it: raw 580 at rest (4.28 mA, ~0.7 bar) and raw 13631 with the pump running against the Owner's
    /// independent reading of 16.x bar. This is the Owner ruling "4-20 mA, 0-40 bar" implemented exactly, and
    /// it is now the same conversion as the other pressure transmitters. A below-4 mA loop is reported by
    /// the channel as a negative word and is refused by the raw-domain check (0..32767) as BAD, never
    /// clamped into a value, so the fail-closed behaviour is unchanged.
    /// See docs/evidence/LEGACY_REV2_SCALING_CORRECTIONS_2026-11-07.md.
    /// </summary>
    public static double PumpOutletPressureBar(int rawWord)
        => LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 40);

    /// <summary>
    /// Water level: raw to level 0..240. Legacy rev1 used a 0..270 span; rev2 corrected the same call site to
    /// 0..240, and the commissioned equipment follows rev2. The percentage output below is unaffected,
    /// because rev2 derives it as level over the same 0..240 span, which is algebraically raw 0..32767 to
    /// 0..100. See docs/evidence/LEGACY_REV2_SCALING_CORRECTIONS_2026-11-07.md.
    /// </summary>
    public static double WaterTankLevel(int rawWord)
        => LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 240);

    /// <summary>Water level percent: raw to 0..100.</summary>
    public static double WaterTankLevelPercent(int rawWord)
        => LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 100);

    /// <summary>Pump speed reference: engineering mA 0..20 to raw 0..32767.</summary>
    public static bool TryPumpSpeedRaw(double milliamps, out int raw)
    {
        raw = 0;
        if (!double.IsFinite(milliamps) || milliamps < 0 || milliamps > 20) return false;
        raw = (int)Math.Round(LegacyMap(milliamps, 0, 20, 0, ProcessIoRepresentation.RawFullScale));
        return true;
    }
}

/// <summary>
/// Exact physical bindings adopted per catalogue row (tag to device/area/address).
/// Mirrored 1:1 by packages/process-io/src/legacyEvidence.mjs ROW_BINDINGS (parity-tested).
/// </summary>
public static class ProcessIoBindings
{
    private static readonly Dictionary<string, (string Device, ProcessIoArea Area, int Address, string Direction)> Table =
        new Dictionary<string, (string, ProcessIoArea, int, string)>
        {
            ["DI-001"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 0, "READ"),
            ["DI-002"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 1, "READ"),
            ["DI-003"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 2, "READ"),
            ["DI-004"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 3, "READ"),
            ["DI-005"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 4, "READ"),
            ["DI-006"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 5, "READ"),
            ["DI-007"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 6, "READ"),
            ["DI-008"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 7, "READ"),
            ["DI-009"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 8, "READ"),
            ["DI-010"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 9, "READ"),
            ["DI-011"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 10, "READ"),
            ["DI-012"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 11, "READ"),
            ["DI-013"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 12, "READ"),
            ["DI-014"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 13, "READ"),
            ["DI-015"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 14, "READ"),
            ["DI-016"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 15, "READ"),
            ["DI-017"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 16, "READ"),
            ["DI-018"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 17, "READ"),
            ["DI-019"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 18, "READ"),
            ["DI-020"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 19, "READ"),
            ["DI-021"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 20, "READ"),
            ["DI-022"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 21, "READ"),
            ["DI-023"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 22, "READ"),
            ["DI-024"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 23, "READ"),
            ["DI-025"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 24, "READ"),
            ["DI-026"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 25, "READ"),
            ["DI-027"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 26, "READ"),
            ["DI-028"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 27, "READ"),
            ["DI-029"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 28, "READ"),
            ["DI-030"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 29, "READ"),
            ["DI-031"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 30, "READ"),
            ["DI-032"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 31, "READ"),
            ["DI-033"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 32, "READ"),
            ["DI-034"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 33, "READ"),
            ["DI-035"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 34, "READ"),
            ["DI-036"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 35, "READ"),
            ["DI-037"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 36, "READ"),
            ["DI-038"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 37, "READ"),
            ["DI-039"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 38, "READ"),
            ["DI-040"] = (ProcessIoDevices.RackT8, ProcessIoArea.DiscreteInput, 39, "READ"),
            ["DO-001"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 0, "WRITE"),
            ["DO-002"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 1, "WRITE"),
            ["DO-003"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 2, "WRITE"),
            ["DO-004"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 3, "WRITE"),
            ["DO-005"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 4, "WRITE"),
            ["DO-006"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 5, "WRITE"),
            ["DO-007"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 6, "WRITE"),
            ["DO-008"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 7, "WRITE"),
            ["DO-009"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 8, "WRITE"),
            ["DO-010"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 9, "WRITE"),
            ["DO-011"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 10, "WRITE"),
            ["DO-012"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 11, "WRITE"),
            ["DO-013"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 12, "WRITE"),
            ["DO-014"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 13, "WRITE"),
            ["DO-015"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 14, "WRITE"),
            ["DO-016"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 15, "WRITE"),
            ["DO-017"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 16, "WRITE"),
            ["DO-018"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 17, "WRITE"),
            ["DO-019"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 18, "WRITE"),
            ["DO-020"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 19, "WRITE"),
            ["DO-021"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 20, "WRITE"),
            ["DO-022"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 21, "WRITE"),
            ["DO-023"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 22, "WRITE"),
            ["DO-024"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 23, "WRITE"),
            ["DO-025"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 24, "WRITE"),
            ["DO-026"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 25, "WRITE"),
            ["DO-027"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 26, "WRITE"),
            ["DO-028"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 27, "WRITE"),
            ["DO-029"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 28, "WRITE"),
            ["DO-030"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 29, "WRITE"),
            ["DO-031"] = (ProcessIoDevices.RackT8, ProcessIoArea.Coil, 30, "WRITE"),
            ["AI-001+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 0, "READ"),
            ["AI-002+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 1, "READ"),
            ["AI-003+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 2, "READ"),
            ["AI-004+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 3, "READ"),
            ["AI-005+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 4, "READ"),
            ["AI-006+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 5, "READ"),
            ["AI-007+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 6, "READ"),
            ["AI-008+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 7, "READ"),
            ["AI-009+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 8, "READ"),
            ["AI-010+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 9, "READ"),
            ["AI-011+"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 10, "READ"),
            ["AI-012"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 11, "READ"),
            ["AI-013"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 12, "READ"),
            ["AI-014"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 13, "READ"),
            ["AI-015"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 14, "READ"),
            ["AI-016"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 15, "READ"),
            ["AI-017"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 16, "READ"),
            ["AI-018"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 17, "READ"),
            ["AI-019"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 18, "READ"),
            ["AI-020"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 19, "READ"),
            ["AI-021"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 20, "READ"),
            ["AI-022"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 21, "READ"),
            ["AI-023"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 22, "READ"),
            ["AI-024"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 23, "READ"),
            ["AI-025"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 24, "READ"),
            ["AI-026"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 25, "READ"),
            ["AI-027"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 26, "READ"),
            ["AI-028"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 27, "READ"),
            ["AI-029"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 28, "READ"),
            ["AI-030"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 29, "READ"),
            ["AI-031"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 30, "READ"),
            ["AI-032"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 31, "READ"),
            ["AI-033"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 32, "READ"),
            ["AI-034"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 33, "READ"),
            ["AI-035"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 34, "READ"),
            ["AI-036"] = (ProcessIoDevices.RackT8, ProcessIoArea.InputRegister, 35, "READ"),
            ["AO-001+"] = (ProcessIoDevices.RackT8, ProcessIoArea.HoldingRegister, 0, "WRITE"),
        };

    public static bool TryGet(string tag, out ProcessIoPhysicalBinding binding)
    {
        if (Table.TryGetValue(tag, out var entry))
        {
            binding = new ProcessIoPhysicalBinding
            {
                DeviceKey = entry.Device,
                Area = entry.Area,
                Address = entry.Address,
                Direction = entry.Direction,
                LegacyPath = ProcessIoLegacyProvenance.PathFor(tag),
                Adopted = ProcessIoLegacyProvenance.AdoptionFor(tag),
            };
            return true;
        }
        binding = null!;
        return false;
    }
}

/// <summary>Adoption classification per bound row (LEGACY_EXACT or LEGACY_DERIVED).</summary>
public static class ProcessIoLegacyProvenance
{
    private static readonly HashSet<string> Derived = new();

    public static string AdoptionFor(string tag) => Derived.Contains(tag) ? "LEGACY_DERIVED" : "LEGACY_EXACT";

    public static string PathFor(string tag) =>
        $"Authoritative T8 fieldbus mapping workbook for {tag} (see packages/process-io/src/legacyEvidence.mjs)";
}

/// <summary>
/// Canonical Isolation Valve identity and its exact adopted Process-Io rows
/// (ACCELERATED-CLEANING-INTEGRATION-01, Owner decisions D1 and D3).
///
/// This is NOT a second I/O catalogue: every accessor below resolves through
/// <see cref="ProcessIoBindings"/> for the address and only supplies the documented ordinal
/// derivation. The ordinals are fixed by the corrected authoritative workbook and the exact Legacy
/// command chain:
///
///   Legacy cannon n  -> WJn            (canonical Product derivation, unchanged)
///   WJn              <-> IVn           (one-to-one by ordinal)
///   IVn command      -> DO-0(22+n)     coil 21+n, FC05, MAINTAINED single coil
///   IVn OPEN  feedback -> DI-0(20+n)   FC02 address 19+n  (WSB Upper Limit Switch #n)
///   IVn CLOSED feedback -> DI-0(28+n)  FC02 address 27+n  (WSB Lower Limit Switch #n)
///   IVn outlet pressure -> AI-(3+n, exactly three digits)+ FC04 input register 2+n
///                          so n=1 is AI-004+ and n=8 is AI-011+; the tag is never four digits
///
/// Command semantics are maintained state, never a pulse: true (energised) = OPEN,
/// false (de-energised) = CLOSED. Safe cleanup writes false and retains the de-energised state.
/// FC01 output read-back is process-image evidence only and is never field confirmation.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the gate).
/// </summary>
public static class ProcessIoIsolationValves
{
    /// <summary>Exactly eight Water Jets and eight Isolation Valves (never nine, never inferred).</summary>
    public const int Count = 8;

    /// <summary>Command shapes that carry command authority. Read shapes never write.</summary>
    public const string MaintainedCommandSemantics = "MAINTAINED_SINGLE_COIL";

    /// <summary>Canonical Product identity, e.g. 1 -> "IV1".</summary>
    public static string ValveId(int ordinal)
    {
        RequireOrdinal(ordinal);
        return "IV" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Strict parse of "IV1".."IV8"; anything else is not an Isolation Valve identity. The supplied
    /// spelling must be the canonical identity itself, so a zero-padded ordinal or a whitespace-wrapped
    /// spelling is refused rather than normalized: a physical command identity is never an alias.
    /// </summary>
    public static bool TryParseValveId(string? valveId, out int ordinal)
    {
        ordinal = 0;
        if (string.IsNullOrEmpty(valveId)) return false;
        if (!valveId.StartsWith("IV", StringComparison.Ordinal)) return false;
        if (!int.TryParse(valveId.AsSpan(2), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int parsed))
        {
            return false;
        }
        if (parsed < 1 || parsed > Count) return false;

        // Range validation alone is not strict: "IV01" parses to the in-range ordinal 1. Requiring the
        // supplied value to be the canonical identity rejects every alias without normalizing it.
        if (!string.Equals(valveId, ValveId(parsed), StringComparison.Ordinal)) return false;

        ordinal = parsed;
        return true;
    }

    /// <summary>The maintained FC05 command coil for IVn (DO-0(22+n)).</summary>
    public static string CommandTag(int ordinal)
    {
        RequireOrdinal(ordinal);
        return "DO-0" + (22 + ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The OPEN-position feedback row for IVn (WSB Upper Limit Switch #n).</summary>
    public static string OpenFeedbackTag(int ordinal)
    {
        RequireOrdinal(ordinal);
        return "DI-0" + (20 + ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The CLOSED-position feedback row for IVn (WSB Lower Limit Switch #n).</summary>
    public static string ClosedFeedbackTag(int ordinal)
    {
        RequireOrdinal(ordinal);
        return "DI-0" + (28 + ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The IVn outlet pressure row, AI-004+ .. AI-011+, ordinal by IV identity. The numeric identity is
    /// exactly three decimal digits, so ordinal 7 is AI-010+ and ordinal 8 is AI-011+. Unpadded
    /// concatenation would widen to a fourth digit at ordinal 7 and name no adopted row.
    /// </summary>
    public static string OutletPressureTag(int ordinal)
    {
        RequireOrdinal(ordinal);
        return "AI-"
            + (3 + ordinal).ToString("D3", System.Globalization.CultureInfo.InvariantCulture)
            + "+";
    }

    /// <summary>True for exactly the eight IV outlet pressure tags, AI-004+ .. AI-011+.</summary>
    public static bool IsOutletPressureTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        for (int n = 1; n <= Count; n += 1)
        {
            if (string.Equals(tag, OutletPressureTag(n), StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>The Pump Outlet Pressure row, the sole Pump-ready pressure authority.</summary>
    public const string PumpOutletPressureTag = "AI-003+";

    /// <summary>The Pump Inlet Pressure row: diagnostic and trend only, never a Pump-ready source.</summary>
    public const string PumpInletPressureTag = "AI-002+";

    /// <summary>The adopted command coil address (zero-based coil 21+n).</summary>
    public static int CommandCoilAddress(int ordinal)
    {
        RequireOrdinal(ordinal);
        if (!ProcessIoBindings.TryGet(CommandTag(ordinal), out ProcessIoPhysicalBinding binding))
        {
            throw new InvalidOperationException($"No adopted binding exists for {CommandTag(ordinal)}.");
        }
        if (binding.Area != ProcessIoArea.Coil || binding.Direction != "WRITE")
        {
            throw new InvalidOperationException(
                $"{CommandTag(ordinal)} is not an adopted WRITE coil binding; refusing to command it.");
        }
        return binding.Address;
    }

    /// <summary>
    /// The maintained command state for a typed command kind. OPEN energises (true); CLOSE
    /// de-energises (false). This is the only place the polarity is expressed.
    /// </summary>
    public static bool CoilValueFor(bool open) => open;

    private static void RequireOrdinal(int ordinal)
    {
        if (ordinal < 1 || ordinal > Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordinal), ordinal, $"Isolation Valve ordinal must be 1..{Count}.");
        }
    }
}

/// <summary>
/// Truthful quality of one physical Process-Io observation. A synchronous transport read that
/// returned is evidence that a value was RECEIVED; it is not by itself evidence that the value is
/// GOOD, current or plausible. The session assigns these from what it actually knows.
/// </summary>
public enum ProcessIoObservationQuality
{
    /// <summary>The read returned and the value is inside the adopted engineering domain.</summary>
    GOOD = 0,

    /// <summary>The read returned but the session cannot vouch for the value (unbound, excluded, wrong generation).</summary>
    UNCERTAIN = 1,

    /// <summary>The read did not return a usable value.</summary>
    BAD = 2,

    /// <summary>The value is older than the freshness budget, or belongs to a superseded generation.</summary>
    STALE = 3,

    /// <summary>The channel is not commissioned for observation.</summary>
    DISABLED = 4,
}

/// <summary>Where a physical observation came from, as a value a report can carry verbatim.</summary>
public enum ProcessIoObservationProvenance
{
    /// <summary>Read through the adopted catalogue row on the physical-capable transport.</summary>
    PHYSICAL_READ = 0,

    /// <summary>Read from a deterministic in-memory transport (never physical evidence).</summary>
    SIMULATED_READ = 1,

    /// <summary>No read was attempted because the session or row refused it.</summary>
    NOT_OBSERVED = 2,
}

/// <summary>
/// One typed physical observation. Every field is something the session actually knows: the raw
/// word, the engineering value and its unit, the quality it can justify, both timestamps, the
/// connection generation the value was read under, the freshness verdict and the provenance.
///
/// A caller must never synthesise this record from a bare transport call: that would fabricate a
/// GOOD, current observation. Only <see cref="Wjss.Runtime.Core.ProcessIo.ProcessIoSession"/>
/// produces it, and only after it has consulted its own generation and state.
/// </summary>
public sealed record ProcessIoPhysicalObservation
{
    /// <summary>Product tag of the catalogue row, e.g. "AI-003+" or "DI-021".</summary>
    public required string Tag { get; init; }

    /// <summary>The raw transport value: the word for AI, the bit for DI.</summary>
    public required int RawValue { get; init; }

    /// <summary>Engineering value when a conversion is adopted for the tag; null for raw-only rows.</summary>
    public double? EngineeringValue { get; init; }

    /// <summary>Engineering unit for <see cref="EngineeringValue"/>, e.g. "bar".</summary>
    public string? Unit { get; init; }

    public required ProcessIoObservationQuality Quality { get; init; }

    /// <summary>The instant the value is claimed to describe.</summary>
    public required DateTimeOffset ObservedAt { get; init; }

    /// <summary>The instant the transport actually returned it. Never earlier than ObservedAt.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>The session connection generation this value was read under.</summary>
    public required int SourceGeneration { get; init; }

    /// <summary>True only when the quality is GOOD and the sample is inside its freshness budget.</summary>
    public required bool Fresh { get; init; }

    public required ProcessIoObservationProvenance Provenance { get; init; }

    /// <summary>Typed machine reason, "OK" when nothing was refused.</summary>
    public required string DiagnosticCode { get; init; }

    /// <summary>True when the record may be used as current process evidence.</summary>
    public bool IsUsable => Quality == ProcessIoObservationQuality.GOOD && Fresh;

    /// <summary>
    /// True when the record is current, usable and belongs to the stated live generation. A value
    /// from a superseded generation is refused rather than reinterpreted.
    /// </summary>
    public bool IsUsableAt(DateTimeOffset now, int liveGeneration, TimeSpan freshnessBudget) =>
        IsUsable
        && SourceGeneration == liveGeneration
        && ObservedAt <= now
        && now - ObservedAt <= freshnessBudget;
}

/// <summary>
/// One admitted IV command result. The token belongs to the existing
/// <see cref="Wjss.Runtime.Core.ProcessIo.ProcessIoSession"/> lifecycle; this record only reports
/// what happened to it.
/// </summary>
public sealed record ProcessIoCommandOutcome
{
    public required string ValveId { get; init; }

    public required string Tag { get; init; }

    public required int CoilAddress { get; init; }

    /// <summary>The maintained coil value sent: true = energised = OPEN, false = CLOSED.</summary>
    public required bool CoilValue { get; init; }

    /// <summary>True only when admission AND dispatch both succeeded.</summary>
    public required bool Dispatched { get; init; }

    public required string AdmissionCode { get; init; }

    public required string DispatchCode { get; init; }

    public required int SessionGeneration { get; init; }

    public required DateTimeOffset At { get; init; }

    /// <summary>True when the transport threw during dispatch; the exception is captured, never propagated.</summary>
    public bool TransportFaulted { get; init; }
}
