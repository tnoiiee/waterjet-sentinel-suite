using Wjss.Contracts;

namespace Wjss.Domain;

/// <summary>
/// Startup gate for device profiles (ADR-0012, Owner Stage 0.3A gate).
///
/// Standing rule: only SIMULATOR may start, and any attempt to start with another profile is
/// refused explicitly — never silently downgraded to SIMULATOR, never partially started.
///
/// One narrow, explicit extension (One-Node Physical TC Read-Only Product Integration, Owner
/// decision R1): <see cref="DeviceProfile.TEST_HARDWARE_READ_ONLY"/> may start ONLY when the caller
/// states that the explicit one-Node physical read-only acquisition mode was requested. That mode is
/// selected by its own startup configuration variable, is refused when the Owner-local endpoint is
/// absent or invalid, and remains read-only, quarantined and non-commissioning. It is not a
/// physical-capable build identity, carries no key, signature, token or authorization artifact, and
/// grants no write, command or control authority. <see cref="DeviceProfile.TEST_HARDWARE"/> and
/// <see cref="DeviceProfile.PRODUCTION"/> are never startable.
/// </summary>
public static class ProfileStartPolicy
{
    /// <summary>Machine reason code returned when startup is refused.</summary>
    public const string RefusalCode = "PROFILE_NOT_AUTHORIZED_FOR_STAGE_03A";

    /// <summary>
    /// The standing default: only SIMULATOR may start. Use this overload when no physical
    /// read-only acquisition mode was requested.
    /// </summary>
    public static bool IsStartable(DeviceProfile profile) => profile == DeviceProfile.SIMULATOR;

    /// <summary>
    /// True when the profile may start for the stated acquisition mode.
    /// <see cref="DeviceProfile.TEST_HARDWARE_READ_ONLY"/> is startable only for the explicit
    /// physical read-only mode; SIMULATOR is startable in every mode, but a SIMULATOR run never
    /// carries a physical record (the Runtime invariants enforce that). TEST_HARDWARE and PRODUCTION
    /// remain unstartable in every mode.
    /// </summary>
    public static bool IsStartable(DeviceProfile profile, bool physicalReadOnlyMode) =>
        profile == DeviceProfile.SIMULATOR
        || (physicalReadOnlyMode && profile == DeviceProfile.TEST_HARDWARE_READ_ONLY);

    /// <summary>
    /// True when startup may proceed. On refusal, <paramref name="refusalReason"/>
    /// carries an explicit, operator-readable reason including the governing rule.
    /// </summary>
    public static bool TryRequireStartable(
        DeviceProfile profile,
        out string refusalCode,
        out string refusalReason) =>
        TryRequireStartable(profile, physicalReadOnlyMode: false, out refusalCode, out refusalReason);

    /// <summary>
    /// True when startup may proceed for the stated acquisition mode. On refusal,
    /// <paramref name="refusalReason"/> carries an explicit, operator-readable reason.
    /// </summary>
    public static bool TryRequireStartable(
        DeviceProfile profile,
        bool physicalReadOnlyMode,
        out string refusalCode,
        out string refusalReason)
    {
        if (IsStartable(profile, physicalReadOnlyMode))
        {
            refusalCode = string.Empty;
            refusalReason = string.Empty;
            return true;
        }

        refusalCode = RefusalCode;
        refusalReason = physicalReadOnlyMode
            ? $"Startup refused: device profile '{profile}' is not authorized for the explicit " +
              "one-Node physical READ-ONLY acquisition mode. Only TEST_HARDWARE_READ_ONLY may start there, " +
              "and only in combination with a successfully resolved Owner-local endpoint. PRODUCTION is " +
              "never startable, no profile is silently substituted, and no write or control authority " +
              "exists (docs/SAFETY_BOUNDARY.md, ADR-0012). The runtime exits without binding any port."
            : $"Startup refused: device profile '{profile}' is not authorized in Stage 0.3A. " +
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
