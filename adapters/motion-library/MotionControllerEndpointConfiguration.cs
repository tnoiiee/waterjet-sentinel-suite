namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// A resolved Owner-local controller endpoint handle.
///
/// It exists so the identity session can hold the endpoint without ever rendering it:
/// <see cref="ToString"/> returns only the redacted status, so console diagnostics, structured
/// log templates, exception text, debugger output and any accidental interpolation can never
/// reveal the confidential value. The value itself is reachable only through the typed member,
/// which the session reads solely to build the in-memory open-address text; it is never part of
/// an outcome, a fixture, evidence, a document or UI state.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionControllerEndpointConfiguration
{
    /// <summary>The Owner-local endpoint value. Never serialized, logged or displayed.</summary>
    public required string EndpointValue { get; init; }

    /// <summary>The redacted status only; the endpoint value is never included.</summary>
    public override string ToString() => MotionControllerIdentityVocabulary.EndpointConfigured;
}

/// <summary>
/// Resolves the Owner-local controller endpoint from an external source.
///
/// Public-repository boundary: the endpoint is confidential local deployment configuration. It is
/// read from the process environment at session time, held only in memory, and never committed,
/// serialized, returned by an API route, written to a log, rendered by a UI, or recorded in
/// evidence. The only endpoint statement any output may carry is the redacted status
/// <see cref="MotionControllerIdentityVocabulary.EndpointConfigured"/>.
///
/// There is deliberately NO committed configuration path for the endpoint: the resolver accepts
/// only an environment reader, so no repository file can supply, override or default the value.
/// The resolver is fail-closed and performs NO I/O: it validates shape only. The accepted shape
/// is exactly one canonical dotted-quad IPv4 host address — no port, no scheme, no whitespace, no
/// option text, no leading zeros — because the value is concatenated into a open-address text
/// whose documented option switches must never be reachable from configuration. Loopback,
/// unspecified and broadcast/multicast addresses are rejected. Nothing here probes, pings,
/// resolves, scans or enumerates anything.
/// </summary>
public static class MotionControllerEndpointResolver
{
    /// <summary>
    /// Owner-local endpoint variable for the one-controller identity session. The value is
    /// supplied outside the repository and is never committed.
    /// </summary>
    public const string EndpointVariable = "WJSS_MOTION_CONTROLLER_01_ENDPOINT";

    /// <summary>Longest accepted dotted-quad text (four three-digit octets).</summary>
    private const int MaximumEndpointLength = 15;

    /// <summary>
    /// Reads and validates the endpoint. Fails closed on absence, blankness, any character
    /// outside digits and dots, any whitespace (so option text can never be smuggled into the
    /// open-address text), any non-canonical octet, any port or scheme, and the loopback,
    /// unspecified and broadcast/multicast address forms.
    /// </summary>
    public static bool TryResolve(
        Func<string, string?> readEnvironment,
        out MotionControllerEndpointConfiguration configuration,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        configuration = null!;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        var raw = readEnvironment(EndpointVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            refusalCode = MotionControllerIdentityVocabulary.EndpointNotConfigured;
            refusalDetail =
                $"The one-controller identity session requires an Owner-local endpoint in " +
                $"{EndpointVariable}. The value is confidential local configuration and is never " +
                "committed; its absence refuses the session before anything is loaded or contacted.";
            return false;
        }

        if (!IsCanonicalDottedQuad(raw, out var firstOctet))
        {
            return Reject(
                "The endpoint value is not exactly one canonical dotted-quad IPv4 host address. " +
                "Accepted shape: four decimal octets 0-255 separated by single dots, no leading " +
                "zeros, no port, no scheme, no whitespace and no option text. A present value is " +
                "not a valid value.",
                out configuration, out refusalCode, out refusalDetail);
        }

        if (firstOctet == 0 || firstOctet == 127 || firstOctet >= 224)
        {
            return Reject(
                "The endpoint value is a loopback, unspecified or broadcast/multicast address form. " +
                "The one-controller identity session accepts only a unicast host address. A present " +
                "value is not a valid value.",
                out configuration, out refusalCode, out refusalDetail);
        }

        configuration = new MotionControllerEndpointConfiguration { EndpointValue = raw };
        return true;
    }

    /// <summary>
    /// The ONLY endpoint statement a caller may publish, log or display. It never contains the
    /// endpoint value.
    /// </summary>
    public static string DescribeConfiguration(MotionControllerEndpointConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return MotionControllerIdentityVocabulary.EndpointConfigured;
    }

    private static bool IsCanonicalDottedQuad(string value, out int firstOctet)
    {
        firstOctet = 0;
        if (value.Length < 7 || value.Length > MaximumEndpointLength)
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        var octets = new int[4];
        for (var index = 0; index < 4; index++)
        {
            var part = parts[index];
            if (part.Length == 0 || part.Length > 3)
            {
                return false;
            }

            foreach (var character in part)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            if (part.Length > 1 && part[0] == '0')
            {
                // No leading zeros: "010" must never be read as octal or as a second spelling.
                return false;
            }

            if (!int.TryParse(part, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var octet) || octet > 255)
            {
                return false;
            }

            octets[index] = octet;
        }

        if (octets[0] == 255 && octets[1] == 255 && octets[2] == 255 && octets[3] == 255)
        {
            return false;
        }

        firstOctet = octets[0];
        return true;
    }

    private static bool Reject(
        string reason,
        out MotionControllerEndpointConfiguration configuration,
        out string refusalCode,
        out string refusalDetail)
    {
        configuration = null!;
        refusalCode = MotionControllerIdentityVocabulary.EndpointRejected;
        refusalDetail = reason;
        return false;
    }
}
