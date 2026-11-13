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

    /// <summary>Machine reason code returned when two mutually exclusive acquisition modes are both requested.</summary>
    public const string ModeConflictCode = "PROFILE_MODE_CONFLICT";

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
        IsStartable(profile, physicalReadOnlyMode, dryCommandMode: false);

    /// <summary>
    /// True when the profile may start for the stated acquisition modes.
    ///
    /// <see cref="DeviceProfile.TEST_HARDWARE_DRY"/> is the software-only dry command-composition
    /// profile (ACCELERATED-CLEANING-INTEGRATION-01, Owner decision D2). It is startable ONLY for the
    /// explicit dry command mode, and it is never combined with the physical read-only mode: a caller
    /// that asks for both is refused rather than resolved, because the two modes carry different
    /// authorities and the runtime must never guess which one was meant.
    /// </summary>
    public static bool IsStartable(DeviceProfile profile, bool physicalReadOnlyMode, bool dryCommandMode)
    {
        if (physicalReadOnlyMode && dryCommandMode) return false;
        if (profile == DeviceProfile.SIMULATOR) return true;
        if (dryCommandMode) return profile == DeviceProfile.TEST_HARDWARE_DRY;
        return physicalReadOnlyMode && profile == DeviceProfile.TEST_HARDWARE_READ_ONLY;
    }

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
        out string refusalReason) =>
        TryRequireStartable(profile, physicalReadOnlyMode, dryCommandMode: false, out refusalCode, out refusalReason);

    /// <summary>
    /// True when startup may proceed for the stated acquisition modes. On refusal,
    /// <paramref name="refusalReason"/> carries an explicit, operator-readable reason.
    /// </summary>
    public static bool TryRequireStartable(
        DeviceProfile profile,
        bool physicalReadOnlyMode,
        bool dryCommandMode,
        out string refusalCode,
        out string refusalReason)
    {
        if (physicalReadOnlyMode && dryCommandMode && profile != DeviceProfile.SIMULATOR)
        {
            refusalCode = ModeConflictCode;
            refusalReason =
                "Startup refused: the physical read-only mode and the dry command-composition mode are "
                + "mutually exclusive and were both requested. No mode is selected on the caller's behalf "
                + "(docs/SAFETY_BOUNDARY.md, ADR-0012). The runtime exits without binding any port.";
            return false;
        }

        if (IsStartable(profile, physicalReadOnlyMode, dryCommandMode))
        {
            refusalCode = string.Empty;
            refusalReason = string.Empty;
            return true;
        }

        refusalCode = RefusalCode;
        refusalReason = dryCommandMode
            ? $"Startup refused: device profile '{profile}' is not authorized for the explicit dry " +
              "command-composition mode. Only TEST_HARDWARE_DRY may start there, and it is software-only: " +
              "TEST_HARDWARE and PRODUCTION remain unstartable, no profile is silently substituted, and no " +
              "SIMULATOR transport is used for a physical command path " +
              "(docs/SAFETY_BOUNDARY.md, ADR-0012). The runtime exits without binding any port."
            : physicalReadOnlyMode
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
