using System.Globalization;
using Wjss.Contracts;

namespace Wjss.Runtime;

/// <summary>
/// A resolved Owner-local Process-Io endpoint.
///
/// The address is confidential local deployment information: it is read from the process environment
/// at startup, held only in memory, and never committed, serialized, returned by an API route, written
/// to a log, rendered by the UI or recorded in evidence. <see cref="ToString"/> returns only the
/// redacted logical identity, so interpolation and diagnostic output can never reveal it.
/// </summary>
public sealed class ProcessIoEndpointHandle
{
    internal ProcessIoEndpointHandle(string host, int port, byte unitId)
    {
        Host = host;
        Port = port;
        UnitId = unitId;
    }

    /// <summary>The bench host. Reachable only to construct the transport; never serialized or displayed.</summary>
    internal string Host { get; }

    internal int Port { get; }

    internal byte UnitId { get; }

    /// <summary>A non-reversible fingerprint, the only endpoint statement any evidence may carry.</summary>
    public string Fingerprint
    {
        get
        {
            byte[] digest = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{Host}:{Port}:{UnitId}"));
            return "endpoint-sha256-prefix:" + Convert.ToHexString(digest, 0, 6).ToLowerInvariant();
        }
    }

    /// <summary>Redacted identity only; the address is never included.</summary>
    public override string ToString() => "PROCESS_IO_ENDPOINT_CONFIGURED/" + Fingerprint;
}

/// <summary>
/// Resolves the TEST_HARDWARE_DRY command-composition configuration from an Owner-local external
/// source (the process environment only).
///
/// Nothing here probes, pings, resolves, scans or connects: resolution is pure string validation and
/// performs no I/O at all. A missing or invalid value is a typed refusal, never a defaulted value and
/// never a fallback to the deterministic SIMULATOR path.
/// </summary>
public static class ProcessIoEndpointConfiguration
{
    /// <summary>Explicit dry command-composition mode selector. Absent means the mode is not selected.</summary>
    public const string DryModeVariable = "WJSS_DRY_COMMAND_MODE";

    /// <summary>Owner-local bench host. Deliberately has no committed configuration path.</summary>
    public const string HostVariable = "WJSS_PROCESS_IO_HOST";

    /// <summary>Owner-local transport port. Defaults to the adopted transport shape, which is not secret.</summary>
    public const string PortVariable = "WJSS_PROCESS_IO_PORT";

    /// <summary>Owner-local unit identity. Defaults to the adopted transport shape, which is not secret.</summary>
    public const string UnitIdVariable = "WJSS_PROCESS_IO_UNIT_ID";

    /// <summary>One-use Owner consent for a single digital-output command path.</summary>
    public const string ConsentVariable = "WJSS_PROCESS_IO_CONSENT";

    public const string NotConfigured = "PROCESS_IO_ENDPOINT_NOT_CONFIGURED";
    public const string Rejected = "PROCESS_IO_ENDPOINT_REJECTED";
    public const string ConsentMissing = "PROCESS_IO_CONSENT_MISSING";
    public const string ConsentMismatch = "PROCESS_IO_CONSENT_MISMATCH";
    public const string NativeIdentityMissing = "PROCESS_IO_NATIVE_IDENTITY_MISSING";
    public const string BindingMissing = "PROCESS_IO_BINDING_MISSING";
    public const string DryModeNotSelected = "DRY_COMMAND_MODE_NOT_SELECTED";
    public const string ProductionRefused = "PRODUCTION_NOT_AUTHORIZED";

    /// <summary>
    /// Reads the explicit dry-mode selector. An absent value means the mode was NOT selected; only the
    /// literal "TEST_HARDWARE_DRY" (case-insensitive) selects it. Nothing is inferred.
    /// </summary>
    public static bool TryReadDryMode(
        Func<string, string?> readEnvironment,
        out bool dryCommandMode,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(DryModeVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            dryCommandMode = false;
            return true;
        }

        if (!string.Equals(raw.Trim(), nameof(DeviceProfile.TEST_HARDWARE_DRY), StringComparison.OrdinalIgnoreCase))
        {
            dryCommandMode = false;
            refusalCode = DryModeNotSelected;
            refusalDetail =
                $"{DryModeVariable} must be exactly 'TEST_HARDWARE_DRY' when present; got a different value. "
                + "No other value selects the dry command mode and nothing is substituted.";
            return false;
        }

        dryCommandMode = true;
        return true;
    }

    /// <summary>
    /// Resolves the Owner-local endpoint. Missing host, unparsable port, or an out-of-range unit id are
    /// typed refusals. No value is defaulted except the two non-secret transport-shape defaults.
    /// </summary>
    public static bool TryResolveEndpoint(
        Func<string, string?> readEnvironment,
        out ProcessIoEndpointHandle? endpoint,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        endpoint = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? host = readEnvironment(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
        {
            refusalCode = NotConfigured;
            refusalDetail =
                $"The dry command mode requires an Owner-local {HostVariable}. It is deliberately not "
                + "committed, never defaulted and never substituted; the runtime refuses before any "
                + "listener is bound.";
            return false;
        }

        host = host.Trim();
        if (host.Length is < 1 or > 253 || host.Contains('/') || host.Contains(' '))
        {
            refusalCode = Rejected;
            refusalDetail = $"{HostVariable} is not a usable host value.";
            return false;
        }

        int port = ProcessIoTransportShape.Port;
        string? rawPort = readEnvironment(PortVariable);
        if (!string.IsNullOrWhiteSpace(rawPort))
        {
            if (!int.TryParse(rawPort.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port)
                || port is < 1 or > 65535)
            {
                refusalCode = Rejected;
                refusalDetail = $"{PortVariable} must be an integer between 1 and 65535.";
                return false;
            }
        }

        byte unitId = (byte)ProcessIoTransportShape.UnitId;
        string? rawUnit = readEnvironment(UnitIdVariable);
        if (!string.IsNullOrWhiteSpace(rawUnit))
        {
            if (!byte.TryParse(rawUnit.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out unitId)
                || unitId is < 1 or > 247)
            {
                refusalCode = Rejected;
                refusalDetail = $"{UnitIdVariable} must be an integer between 1 and 247.";
                return false;
            }
        }

        endpoint = new ProcessIoEndpointHandle(host, port, unitId);
        return true;
    }

    /// <summary>
    /// Reads the Owner-local consent phrase. The phrase is accepted only from the environment, never
    /// from a command-line argument, and must match the adopted consent constant exactly.
    /// </summary>
    public static bool TryReadConsent(
        Func<string, string?> readEnvironment,
        out string? consent,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        consent = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(ConsentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            refusalCode = ConsentMissing;
            refusalDetail =
                $"The dry command mode requires the exact Owner consent in {ConsentVariable}. It is read "
                + "from the environment only, so it never appears in a process table or shell history.";
            return false;
        }

        if (!string.Equals(raw, ProcessIoConsentPhrases.PhysicalDoSingle, StringComparison.Ordinal))
        {
            refusalCode = ConsentMismatch;
            refusalDetail = $"{ConsentVariable} does not match the adopted single-output consent phrase.";
            return false;
        }

        consent = raw;
        return true;
    }

    /// <summary>
    /// Proves the adopted native device identity and every adopted IV binding are present in the
    /// loaded catalogue. A missing identity or binding refuses startup rather than degrading at
    /// command time.
    /// </summary>
    public static bool TryValidateAdoptedBindings(
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        for (int ordinal = 1; ordinal <= ProcessIoIsolationValves.Count; ordinal += 1)
        {
            string commandTag = ProcessIoIsolationValves.CommandTag(ordinal);
            if (!catalogue.TryGetValue(commandTag, out ProcessIoCatalogueRow? row))
            {
                refusalCode = BindingMissing;
                refusalDetail = $"The adopted catalogue has no row for {commandTag}; refusing to compose a command path.";
                return false;
            }

            if (row.Binding is null
                || row.BindingState == ProcessIoBindingState.UNBOUND
                || !string.Equals(row.Binding.DeviceKey, ProcessIoDevices.RackT8, StringComparison.Ordinal)
                || row.Binding.Area != ProcessIoArea.Coil
                || !string.Equals(row.Binding.Direction, "WRITE", StringComparison.Ordinal))
            {
                refusalCode = BindingMissing;
                refusalDetail = $"{commandTag} is not an adopted WRITE coil on the native device identity.";
                return false;
            }

            foreach (string feedbackTag in new[]
                     {
                         ProcessIoIsolationValves.OpenFeedbackTag(ordinal),
                         ProcessIoIsolationValves.ClosedFeedbackTag(ordinal),
                     })
            {
                if (!catalogue.TryGetValue(feedbackTag, out ProcessIoCatalogueRow? feedback)
                    || feedback.Binding is null
                    || feedback.BindingState == ProcessIoBindingState.UNBOUND
                    || feedback.Binding.Area != ProcessIoArea.DiscreteInput)
                {
                    refusalCode = BindingMissing;
                    refusalDetail = $"{feedbackTag} is not an adopted discrete-input binding.";
                    return false;
                }
            }
        }

        if (!catalogue.TryGetValue(ProcessIoIsolationValves.PumpOutletPressureTag, out ProcessIoCatalogueRow? outlet)
            || outlet.Binding is null
            || outlet.Binding.Area != ProcessIoArea.InputRegister)
        {
            refusalCode = NativeIdentityMissing;
            refusalDetail =
                $"{ProcessIoIsolationValves.PumpOutletPressureTag} is not an adopted input register, so no "
                + "Pump-ready authority exists.";
            return false;
        }

        return true;
    }
}
