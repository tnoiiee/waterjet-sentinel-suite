using Wjss.Contracts;

namespace Wjss.Domain;

/// <summary>
/// Startup gate for device profiles (ADR-0012, Owner Stage 0.3A gate):
/// only SIMULATOR may start. TEST_HARDWARE and PRODUCTION are contract
/// identities only in this stage, and any attempt to start with them is
/// refused explicitly - never silently downgraded to simulator, never
/// partially started.
/// </summary>
public static class ProfileStartPolicy
{
    /// <summary>Machine reason code returned when startup is refused.</summary>
    public const string RefusalCode = "PROFILE_NOT_AUTHORIZED_FOR_STAGE_03A";

    public static bool IsStartable(DeviceProfile profile) => profile == DeviceProfile.SIMULATOR;

    /// <summary>
    /// True when startup may proceed. On refusal, <paramref name="refusalReason"/>
    /// carries an explicit, operator-readable reason including the governing rule.
    /// </summary>
    public static bool TryRequireStartable(
        DeviceProfile profile,
        out string refusalCode,
        out string refusalReason)
    {
        if (IsStartable(profile))
        {
            refusalCode = string.Empty;
            refusalReason = string.Empty;
            return true;
        }

        refusalCode = RefusalCode;
        refusalReason =
            $"Startup refused: device profile '{profile}' is not authorized in Stage 0.3A. " +
            "Only SIMULATOR may start; Production device access requires a separate Owner Scope Gate " +
            "(docs/SAFETY_BOUNDARY.md, ADR-0012). The runtime exits without binding any port.";
        return false;
    }

    /// <summary>Parses a profile label strictly; unknown labels are a startup configuration error.</summary>
    public static bool TryParseProfile(string? label, out DeviceProfile profile)
    {
        profile = DeviceProfile.SIMULATOR;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        return Enum.TryParse(label.Trim(), ignoreCase: true, out profile);
    }
}
