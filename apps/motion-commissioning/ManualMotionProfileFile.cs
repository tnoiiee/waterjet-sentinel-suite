using Wjss.Runtime.Core.Motion.Manual;

namespace Wjss.MotionCommissioning;

/// <summary>
/// Reads the Owner-local Manual Motion Commissioning profile file named by one environment variable.
/// The file lives outside the repository, is read once and is never written. The path, the file text
/// and every value stay local: only canonical codes leave this type. Parsing is delegated to the
/// strict, fail-closed Application parser.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal static class ManualMotionProfileFile
{
    /// <summary>The environment variable that names the Owner-local profile file.</summary>
    internal const string ProfileVariable = "WJSS_MANUAL_MOTION_PROFILE";

    /// <summary>The largest accepted profile file, in bytes.</summary>
    internal const long MaximumBytes = 65536;

    /// <summary>Reads the profile, or returns null when the variable is not set at all.</summary>
    internal static ManualMotionProfileResult? LoadIfConfigured(Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);

        string? location;
        try
        {
            location = readEnvironment(ProfileVariable);
        }
        catch (Exception)
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileFileRefused);
        }

        return location is null ? null : LoadFrom(location);
    }

    /// <summary>Reads the profile; an unset variable is a refusal.</summary>
    internal static ManualMotionProfileResult Load(Func<string, string?> readEnvironment) =>
        LoadIfConfigured(readEnvironment) ?? ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileNotProvided);

    private static ManualMotionProfileResult LoadFrom(string location)
    {
        if (string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location))
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileFileRefused);
        }

        try
        {
            var info = new FileInfo(location);
            if (!info.Exists)
            {
                return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileFileRefused);
            }

            if (info.Length > MaximumBytes)
            {
                return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileTooLarge);
            }

            return ManualMotionProfileParser.Parse(File.ReadAllText(location));
        }
        catch (Exception)
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileFileRefused);
        }
    }
}
