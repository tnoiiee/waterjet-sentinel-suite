using System.Globalization;

namespace Wjss.Kiosk;

/// <summary>
/// Stage 0.4C-K2 shell constants: the single APPROVED LOCAL ORIGIN and the Stage boundary text.
///
/// The origin is assembled from a loopback literal and the local preview port, so the shell source
/// contains no other host. The SCHEME and the HOST are hard constants: no environment variable, no
/// command-line argument and no configuration file can change them, and the approved origin
/// therefore cannot be pointed at another machine, at another scheme or at a local file
/// (repository rules S1 and S6 are the machine check of this rule). The PORT alone may be matched
/// to the local host's port for the Owner-local preview, because a port is not a trust boundary
/// here: whatever answers on it is still only reachable at loopback from this machine.
///
/// The shell requires no credential in this Stage: it holds no secret of any kind, for any account,
/// and it never asks the Operator for one.
/// </summary>
internal static class KioskShellOptions
{
    /// <summary>The scheme of the approved origin. A constant, never configurable.</summary>
    private const string Scheme = "http";

    /// <summary>The only permitted origin host. A constant, never configurable.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>Default port of the local Kiosk Preview host (apps/ops-preview).</summary>
    public const int DefaultUiPort = 5187;

    /// <summary>
    /// Environment variable that may match the PORT only. Its name deliberately contains no HOST
    /// fragment: repository rule S6 forbids an environment-controlled bind host in the Product tree.
    /// </summary>
    public const string PortVariable = "WJSS_KIOSK_UI_PORT";

    /// <summary>The approved origin. The only address this shell will ever display.</summary>
    public static readonly Uri ApprovedOrigin =
        new($"{Scheme}://{LoopbackHost}:{ResolvePort()}/", UriKind.Absolute);

    /// <summary>Health route of the local host, used by the shell's availability probe.</summary>
    public static readonly Uri HealthUri = new(ApprovedOrigin, "api/health");

    /// <summary>Stage identity, shown in the shell chrome.</summary>
    public const string StageId = "0.4C-K2";

    /// <summary>The Stage boundary statement. Displayed on screen at all times.</summary>
    public const string BoundaryText =
        "SIMULATION / PREVIEW  -  NO HARDWARE ACCESS  -  NO DEVICE WRITE  -  FIELD VERIFICATION PENDING";

    /// <summary>Shell window title.</summary>
    public const string WindowTitle = "WaterJet Sentinel Suite - Kiosk Preview (Stage 0.4C-K2)";

    /// <summary>Reconnect cadence of the local host probe, in milliseconds.</summary>
    public const int ProbeIntervalMs = 2000;

    /// <summary>Probe timeout, in milliseconds. Short: a missing local host must never freeze the shell.</summary>
    public const int ProbeTimeoutMs = 1200;

    /// <summary>Resolves the port only: the default, or an Owner-local preview override.</summary>
    private static int ResolvePort()
    {
        var raw = Environment.GetEnvironmentVariable(PortVariable);
        if (!string.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed is > 0 and < 65536)
        {
            return parsed;
        }

        return DefaultUiPort;
    }

    /// <summary>
    /// True only for the approved loopback origin: same scheme, same loopback host, same port.
    /// Anything else - another host, another scheme (including a local file) or another port - is
    /// refused, so the window cannot be pointed away from the approved local route.
    /// </summary>
    public static bool IsApprovedOrigin(Uri? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        return string.Equals(candidate.Scheme, Scheme, StringComparison.Ordinal)
            && string.Equals(candidate.Host, LoopbackHost, StringComparison.Ordinal)
            && candidate.Port == ApprovedOrigin.Port;
    }
}
