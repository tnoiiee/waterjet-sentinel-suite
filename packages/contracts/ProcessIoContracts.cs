namespace Wjss.Contracts;

// H2 Physical Process-I/O — typed Product contracts (Stage H2-B..H2-F).
//
// Every Process-I/O observation and command crosses one of these typed boundaries. The
// adopted physical values (device keys, transport shape, function codes, register/coil
// offsets, conversions, lifecycle constants) come from the Exact Legacy Source pack (the
// commissioned functional reference for the unchanged Hardware) and are mirrored 1:1 by
// packages/process-io/src/legacyEvidence.mjs, whose Node parity test pins this file.
// Endpoint values are Owner-local configuration and are never committed. No third-party
// fieldbus client type appears in this file. Physical write authority is NOT created here.

/// <summary>Device keys of the commissioned Process-I/O fleet (hosts are Owner-local).</summary>
public static class ProcessIoDevices
{
    public const string PumpRack = "PROCESS_IO_PUMP_RACK";
    public const string ZoneA = "PROCESS_IO_ZONE_A";
    public const string ZoneB = "PROCESS_IO_ZONE_B";
    public static readonly IReadOnlyList<string> All = new[] { PumpRack, ZoneA, ZoneB };
}

/// <summary>Committed transport shape (exact Legacy defaults).</summary>
public static class ProcessIoTransportShape
{
    public const int Port = 502;
    public const int UnitId = 1;
    public const int ConnectTimeoutMs = 1000;
    public const int ReadTimeoutMs = 1000;
    public const int WriteTimeoutMs = 1000;
    public const int PollIntervalMs = 200;
    public const int ConnectionCheckIntervalMs = 5000;
    public const int ReadGroupStart = 0;
    public const int ReadGroupCount = 100;
}

/// <summary>Function codes proven on the exact Legacy runtime call sites.</summary>
public static class ProcessIoFunctionCodes
{
    public const string AiRawRead = "FC03";
    public const string DiStateRead = "FC01";
    public const string DoCommand = "FC05";
    public const string AoCommand = "FC16";
}

/// <summary>Raw representation proven by the Legacy runtime (ushort words, 0..32767 full scale).</summary>
public static class ProcessIoRepresentation
{
    public const int WordWidthBits = 16;
    public const int RawFullScale = 32767;
}

public enum ProcessIoArea { HoldingRegister, Coil }

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
    public string? FunctionCode { get; init; }
    public string? Provenance { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

/// <summary>Physical binding adopted from the Exact Legacy Source for one catalogue row.</summary>
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

/// <summary>Typed Analog Input observation (raw word + engineering conversions).</summary>
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
}

/// <summary>
/// Exact Legacy engineering conversion constants. The formulas are the clamped linear map
/// chains of the Legacy call sites (raw 0..32767 full scale). Mirrored by legacyEvidence.mjs.
/// </summary>
public static class ProcessIoEngineering
{
    /// <summary>Exact Legacy map: clamp to [inMin, inMax], then linear conversion.</summary>
    public static double LegacyMap(double x, double inMin, double inMax, double outMin, double outMax)
    {
        double v = x;
        if (v >= inMax) v = inMax;
        if (v <= inMin) v = inMin;
        return (v - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
    }

    /// <summary>WC/IV pressure: raw -> bar 0..40 (single stage, Legacy exact).</summary>
    public static double WcPressureBar(int rawWord)
        => LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 40);

    /// <summary>Pump outlet pressure: raw -> signal mA 0..20 -> bar 0..40 (two stage, Legacy exact).</summary>
    public static double PumpOutletPressureBar(int rawWord)
    {
        double milliamps = LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 20);
        return LegacyMap(milliamps, 4, 20, 0, 40);
    }

    /// <summary>Water level: raw -> level 0..270 (Legacy exact).</summary>
    public static double WaterTankLevel(int rawWord)
        => LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 270);

    /// <summary>Water level percent: raw -> 0..100 (Legacy exact).</summary>
    public static double WaterTankLevelPercent(int rawWord)
        => LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 100);

    /// <summary>Pump speed reference: engineering mA 0..20 -> raw 0..32767 (Legacy exact).</summary>
    public static bool TryPumpSpeedRaw(double milliamps, out int raw)
    {
        raw = 0;
        if (!double.IsFinite(milliamps) || milliamps < 0 || milliamps > 20) return false;
        raw = (int)Math.Round(LegacyMap(milliamps, 0, 20, 0, ProcessIoRepresentation.RawFullScale));
        return true;
    }
}

/// <summary>
/// Exact Legacy physical bindings adopted per catalogue row (tag -> device/area/address).
/// Mirrored 1:1 by packages/process-io/src/legacyEvidence.mjs ROW_BINDINGS (parity-tested).
/// </summary>
public static class ProcessIoBindings
{
    private static readonly Dictionary<string, (string Device, ProcessIoArea Area, int Address, string Direction)> Table =
        new Dictionary<string, (string, ProcessIoArea, int, string)>
        {
            ["AI-001+"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 8, "READ"),
            ["AI-003+"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 9, "READ"),
            ["AI-004+"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 0, "READ"),
            ["AI-005+"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 1, "READ"),
            ["AI-006+"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 2, "READ"),
            ["AI-007+"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 3, "READ"),
            ["AI-008+"] = (ProcessIoDevices.ZoneB, ProcessIoArea.HoldingRegister, 0, "READ"),
            ["AI-009+"] = (ProcessIoDevices.ZoneA, ProcessIoArea.HoldingRegister, 1, "READ"),
            ["AI-010+"] = (ProcessIoDevices.ZoneA, ProcessIoArea.HoldingRegister, 0, "READ"),
            ["AI-011+"] = (ProcessIoDevices.ZoneB, ProcessIoArea.HoldingRegister, 1, "READ"),
            ["AI-012"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 4, "READ"),
            ["AI-013"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 5, "READ"),
            ["AI-014"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 6, "READ"),
            ["AI-015"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 7, "READ"),
            ["AO-001+"] = (ProcessIoDevices.PumpRack, ProcessIoArea.HoldingRegister, 0, "WRITE"),
            ["DI-001"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 11, "READ"),
            ["DI-002"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 12, "READ"),
            ["DI-008"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 18, "READ"),
            ["DI-009"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 19, "READ"),
            ["DI-010"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 20, "READ"),
            ["DI-011"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 21, "READ"),
            ["DI-021"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 3, "READ"),
            ["DI-022"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 5, "READ"),
            ["DI-023"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 7, "READ"),
            ["DI-024"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 9, "READ"),
            ["DI-025"] = (ProcessIoDevices.ZoneB, ProcessIoArea.Coil, 1, "READ"),
            ["DI-026"] = (ProcessIoDevices.ZoneA, ProcessIoArea.Coil, 3, "READ"),
            ["DI-027"] = (ProcessIoDevices.ZoneA, ProcessIoArea.Coil, 1, "READ"),
            ["DI-028"] = (ProcessIoDevices.ZoneB, ProcessIoArea.Coil, 3, "READ"),
            ["DI-029"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 4, "READ"),
            ["DI-030"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 6, "READ"),
            ["DI-031"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 8, "READ"),
            ["DI-032"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 10, "READ"),
            ["DI-033"] = (ProcessIoDevices.ZoneB, ProcessIoArea.Coil, 0, "READ"),
            ["DI-034"] = (ProcessIoDevices.ZoneA, ProcessIoArea.Coil, 2, "READ"),
            ["DI-035"] = (ProcessIoDevices.ZoneA, ProcessIoArea.Coil, 0, "READ"),
            ["DI-036"] = (ProcessIoDevices.ZoneB, ProcessIoArea.Coil, 2, "READ"),
            ["DO-001"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 6, "WRITE"),
            ["DO-002"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 7, "WRITE"),
            ["DO-007"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 8, "WRITE"),
            ["DO-008"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 10, "WRITE"),
            ["DO-009"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 11, "WRITE"),
            ["DO-011"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 12, "WRITE"),
            ["DO-012"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 13, "WRITE"),
            ["DO-023"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 514, "WRITE"),
            ["DO-024"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 515, "WRITE"),
            ["DO-025"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 516, "WRITE"),
            ["DO-026"] = (ProcessIoDevices.PumpRack, ProcessIoArea.Coil, 517, "WRITE"),
            ["DO-027"] = (ProcessIoDevices.ZoneB, ProcessIoArea.Coil, 6, "WRITE"),
            ["DO-028"] = (ProcessIoDevices.ZoneA, ProcessIoArea.Coil, 5, "WRITE"),
            ["DO-029"] = (ProcessIoDevices.ZoneA, ProcessIoArea.Coil, 6, "WRITE"),
            ["DO-030"] = (ProcessIoDevices.ZoneB, ProcessIoArea.Coil, 5, "WRITE"),
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
    private static readonly HashSet<string> Derived =
        new()
        {
            "DI-001", "DI-002", "DI-008", "DI-009", "DI-010", "DI-011",
            "DI-021", "DI-022", "DI-023", "DI-024", "DI-025", "DI-026", "DI-027", "DI-028",
            "DI-029", "DI-030", "DI-031", "DI-032", "DI-033", "DI-034", "DI-035", "DI-036",
            "DO-023", "DO-024", "DO-025", "DO-026", "DO-027", "DO-028", "DO-029", "DO-030",
        };

    public static string AdoptionFor(string tag) => Derived.Contains(tag) ? "LEGACY_DERIVED" : "LEGACY_EXACT";

    public static string PathFor(string tag) =>
        $"Exact Legacy Source pack runtime call site for {tag} (see packages/process-io/src/legacyEvidence.mjs for the exact member and comment)";
}
