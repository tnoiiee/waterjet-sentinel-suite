using System.Globalization;
using Wjss.Adapters.TcAcquisition;
using Wjss.Contracts;

namespace Wjss.Runtime;

/// <summary>
/// The explicit TC acquisition mode of this Runtime host.
///
/// There is no inferred mode: it is selected by its own startup configuration variable, and the
/// absence of a value means <see cref="DETERMINISTIC"/> — the existing, non-Hardware Product
/// behaviour. Physical mode is never selected by the mere presence of an endpoint, and deterministic
/// mode is never selected as a fallback after a physical-mode refusal.
/// </summary>
public enum TcAcquisitionMode
{
    /// <summary>Non-Hardware deterministic Product slice. Opens no socket.</summary>
    DETERMINISTIC,

    /// <summary>Explicit one-Node physical READ-ONLY acquisition. Requires an Owner-local endpoint.</summary>
    PHYSICAL_READ_ONLY,
}

/// <summary>
/// A resolved Owner-local endpoint handle.
///
/// It exists so the Runtime can hold the endpoint without ever rendering it: <see cref="ToString"/>
/// returns only the redacted logical identity, so startup diagnostics, structured log templates,
/// exception text, debugger output and any accidental interpolation can never reveal the confidential
/// address. The address itself is reachable only through <see cref="Endpoint"/>, which the Runtime
/// reads solely to construct the acquisition session; it is never part of a contract, an API payload,
/// a fixture, evidence or UI state.
/// </summary>
public sealed class TcEndpointHandle
{
    public TcEndpointHandle(TcNodeEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        endpoint.Validate();
        Endpoint = endpoint;
    }

    /// <summary>The endpoint the acquisition session is constructed for. Never serialized or displayed.</summary>
    public TcNodeEndpoint Endpoint { get; }

    /// <summary>The redacted logical identity only; the address is never included.</summary>
    public override string ToString() => TcEndpointConfiguration.DescribeEndpoint(Endpoint);
}

/// <summary>
/// Resolves the ONE-NODE physical read-only endpoint from an Owner-local external source.
///
/// Public-repository boundary: the endpoint is confidential local deployment information. It is read
/// from the process environment at startup, held only in memory, and never committed, serialized,
/// returned by an API route, written to a log, rendered by the UI, or recorded in evidence. The only
/// endpoint statement any contract may carry is the redacted logical identity
/// <see cref="PhysicalTcVocabulary.EndpointConfigured"/>.
///
/// There is deliberately NO committed configuration path for the endpoint: the resolver accepts only
/// an environment reader, so no repository file can supply, override or default the address. Nothing
/// here probes, pings, resolves, scans, or enumerates anything — resolution is pure string validation
/// and performs no I/O.
/// </summary>
public static class TcEndpointConfiguration
{
    /// <summary>
    /// Owner-local endpoint variable for the one-Node physical read-only mode. The value is supplied
    /// outside the repository and is never committed.
    /// </summary>
    public const string EndpointVariable = "WJSS_TC_NODE_01_ENDPOINT";

    /// <summary>Explicit TC acquisition mode variable (DETERMINISTIC default; no inferred mode).</summary>
    public const string ModeVariable = "WJSS_TC_ACQUISITION_MODE";

    /// <summary>The only accepted non-default mode label.</summary>
    public const string PhysicalReadOnlyLabel = "PHYSICAL_READ_ONLY";

    /// <summary>The explicit default mode label.</summary>
    public const string DeterministicLabel = "DETERMINISTIC";

    /// <summary>The endpoint variable is absent or blank while physical mode was selected.</summary>
    public const string NotConfigured = "TC_ENDPOINT_NOT_CONFIGURED";

    /// <summary>The endpoint value is not a single well-formed non-loopback IPv4 host address.</summary>
    public const string Rejected = "TC_ENDPOINT_REJECTED";

    /// <summary>The mode label is not a recognised mode.</summary>
    public const string InvalidMode = "INVALID_TC_ACQUISITION_MODE";

    /// <summary>The resolved endpoint does not belong to the authorized one-Node logical identity.</summary>
    public const string IdentityMismatch = "TC_ENDPOINT_IDENTITY_MISMATCH";

    /// <summary>
    /// Reads the explicit acquisition mode. An absent or blank value means DETERMINISTIC; any other
    /// unrecognised value is a configuration error and refuses startup. Nothing is inferred from the
    /// endpoint variable.
    /// </summary>
    public static bool TryReadAcquisitionMode(
        Func<string, string?> readEnvironment,
        out TcAcquisitionMode mode,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        mode = TcAcquisitionMode.DETERMINISTIC;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        var label = readEnvironment(ModeVariable);
        if (string.IsNullOrWhiteSpace(label))
        {
            // No value is the existing deterministic Product behaviour; it is never a physical mode.
            return true;
        }

        var trimmed = label.Trim();
        if (string.Equals(trimmed, DeterministicLabel, StringComparison.OrdinalIgnoreCase))
        {
            mode = TcAcquisitionMode.DETERMINISTIC;
            return true;
        }

        if (string.Equals(trimmed, PhysicalReadOnlyLabel, StringComparison.OrdinalIgnoreCase))
        {
            mode = TcAcquisitionMode.PHYSICAL_READ_ONLY;
            return true;
        }

        refusalCode = InvalidMode;
        refusalDetail =
            $"{ModeVariable} must be exactly '{DeterministicLabel}' or '{PhysicalReadOnlyLabel}'. "
            + "The mode is never inferred from another variable and is never silently substituted.";
        return false;
    }

    /// <summary>
    /// Resolves the Owner-local endpoint for the one-Node physical read-only mode. Fails closed on
    /// absence, blankness, malformed input, a host with a port or scheme, more than one value, a
    /// range or CIDR, a loopback or otherwise non-plant address, and a value whose logical identity
    /// is not the authorized one-Node identity.
    /// </summary>
    public static bool TryResolveEndpoint(
        Func<string, string?> readEnvironment,
        out TcNodeEndpoint endpoint,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        endpoint = null!;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        var raw = readEnvironment(EndpointVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            refusalCode = NotConfigured;
            refusalDetail =
                $"Physical read-only mode requires an Owner-local endpoint in {EndpointVariable}. "
                + "It is deliberately not committed: the endpoint is confidential local deployment "
                + "information and no repository value is read, defaulted or substituted.";
            return false;
        }

        var value = raw.Trim();

        // A single host only: no separator can introduce a second value, a port, a path, a scheme,
        // credentials, a range or a CIDR block.
        if (!IsSingleHostToken(value))
        {
            refusalCode = Rejected;
            refusalDetail =
                "The endpoint must be a single bare IPv4 host address: no scheme, no port, no path, no "
                + "query, no credentials, no comma or whitespace list, and no range or CIDR notation.";
            return false;
        }

        if (!TryParseStrictIPv4(value, out var octets))
        {
            refusalCode = Rejected;
            refusalDetail = "The endpoint must be four decimal octets, each from 0 through 255.";
            return false;
        }

        if (!IsPlantAddress(octets))
        {
            refusalCode = Rejected;
            refusalDetail =
                "The endpoint must address a plant device. Loopback, unspecified, broadcast, multicast "
                + "and reserved addresses are refused: a physical read-only record may never be produced "
                + "against a local process, and no address is silently substituted.";
            return false;
        }

        endpoint = new TcNodeEndpoint
        {
            NodeId = PhysicalTcVocabulary.NodeId,
            IpAddress = value,
            Port = PhysicalTcVocabulary.RegisterPort,
            UnitIdentifier = (byte)PhysicalTcVocabulary.UnitIdentifier,
        };

        // The logical identity is fixed and cannot be supplied by the endpoint value.
        if (endpoint.NodeId != PhysicalTcVocabulary.NodeId)
        {
            refusalCode = IdentityMismatch;
            refusalDetail = "The physical read-only endpoint must belong to the authorized one-Node identity.";
            return false;
        }

        endpoint.Validate();
        return true;
    }

    /// <summary>
    /// The ONLY endpoint statement a caller may publish, log or display. It never contains the
    /// address, a form of it, or any part of it.
    /// </summary>
    public static string DescribeEndpoint(TcNodeEndpoint? endpoint) =>
        endpoint is null ? PhysicalTcVocabulary.EndpointNotConfigured : PhysicalTcVocabulary.EndpointConfigured;

    /// <summary>The redacted mode label used by startup diagnostics.</summary>
    public static string DescribeMode(TcAcquisitionMode mode) => mode switch
    {
        TcAcquisitionMode.PHYSICAL_READ_ONLY => PhysicalReadOnlyLabel,
        _ => DeterministicLabel,
    };

    /// <summary>
    /// The authorized logical read-profile identity. It carries no address and is the value the
    /// Runtime requires a physical record to match exactly.
    /// </summary>
    public static TcReadProfileIdentity AuthorizedReadProfile => new()
    {
        FunctionCode = PhysicalTcVocabulary.FunctionCodeHoldingRegisters,
        RegisterArea = PhysicalTcVocabulary.RegisterAreaHoldingRegisters,
        Port = PhysicalTcVocabulary.RegisterPort,
        UnitIdentifier = PhysicalTcVocabulary.UnitIdentifier,
        StartAddress = PhysicalTcVocabulary.StartAddress,
        Quantity = PhysicalTcVocabulary.WordCount,
    };

    // A single bare host token: digits and dots only. Any scheme prefix, port suffix, path, query,
    // user-info, CIDR suffix, separator, trailing character, whitespace, host name or second address
    // is rejected — the value must be one host and nothing else.
    private static bool IsSingleHostToken(string value)
    {
        if (value.Length == 0 || value.Length > 15) return false;
        foreach (var c in value)
        {
            if (c != '.' && (c < '0' || c > '9')) return false;
        }
        return true;
    }

    // Strict dotted-quad parse. No shorthand form, no integer form, no leading-zero ambiguity:
    // every octet is 1-3 decimal digits and within range.
    private static bool TryParseStrictIPv4(string value, out int[] octets)
    {
        octets = new int[4];
        var part = 0;
        var digits = 0;
        var current = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            if (c == '.')
            {
                if (digits == 0 || part >= 3) return false;
                octets[part++] = current;
                digits = 0;
                current = 0;
                continue;
            }

            if (digits == 0 && c == '0' && index + 1 < value.Length && value[index + 1] != '.')
            {
                // A leading zero makes the octet ambiguous (it is read as octal by some resolvers), so
                // the value is refused instead of being silently interpreted as one particular number.
                return false;
            }

            digits++;
            if (digits > 3) return false;
            current = checked((current * 10) + (c - '0'));
            if (current > 255) return false;
        }

        if (digits == 0 || part != 3) return false;
        octets[part] = current;
        return true;
    }

    // Plant addressing only. Refusals are deliberate and are never "corrected" into a nearby value.
    private static bool IsPlantAddress(int[] octets)
    {
        var first = octets[0];
        var second = octets[1];
        var third = octets[2];

        if (first == 0) return false;                                  // "this network"
        if (first == 127) return false;                                // loopback: never a device
        if (first == 255 && second == 255) return false;               // broadcast / limited broadcast
        if (first >= 224) return false;                                // multicast (224-239) and reserved (240-255)
        if (first == 169 && second == 254) return false;               // link-local autoconfiguration
        // Documentation-only ranges (RFC 5737) and the benchmarking range (RFC 2544) are refused: no
        // device can answer there, so accepting one would let a mistyped value masquerade as a
        // configured physical endpoint and produce a "physical" record against nothing. The committed
        // test addresses live in the first of these ranges, so they can never be used as an endpoint.
        if (first == 192 && second == 0 && third == 2) return false;   // TEST-NET-1 (documentation)
        if (first == 198 && second == 51 && third == 100) return false; // TEST-NET-2 (documentation)
        if (first == 203 && second == 0 && third == 113) return false;  // TEST-NET-3 (documentation)
        if (first == 198 && (second == 18 || second == 19)) return false; // benchmarking
        return true;
    }
}
