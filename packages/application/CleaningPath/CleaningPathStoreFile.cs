using Wjss.Domain;
using Wjss.Time;

namespace Wjss.Runtime.Core.CleaningPath;

/// <summary>
/// Reads the Owner-local Cleaning Path Store file named by one environment variable. The file
/// lives outside the repository, is read once and is never written by the application. The path,
/// the file text and every value stay local: only canonical codes and the validated snapshot leave
/// this type, and no projection or log includes the file location. Parsing and complete-file
/// validation are delegated to the strict, fail-closed domain parser.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public static class CleaningPathStoreFile
{
    /// <summary>The environment variable that names the Owner-local Cleaning Path Store file.</summary>
    public const string StoreVariable = "WJSS_CLEANING_PATH_STORE";

    /// <summary>
    /// The largest accepted store file, in bytes. Derived like the parser text bound: the complete
    /// 108-path store is ≈ 55 KiB compact and ≈ 120 KiB indented, so 256 KiB admits indented files
    /// while refusing pathological input. Refusal never truncates.
    /// </summary>
    public const long MaximumBytes = 262144;

    /// <summary>Reads the store, or returns null when the variable is not set at all.</summary>
    public static CleaningPathStoreResult? LoadIfConfigured(Func<string, string?> readEnvironment, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(clock);

        string? location;
        try
        {
            location = readEnvironment(StoreVariable);
        }
        catch (Exception)
        {
            return CleaningPathStoreResult.Refused(new CleaningPathValidationError
            {
                Code = CleaningPathCodes.StoreFileRefused,
                Reason = "the store location could not be read from the environment",
                AllowedContract = "one fully qualified Owner-local file path named by " + StoreVariable,
            });
        }

        return location is null ? null : LoadFrom(location, clock);
    }

    /// <summary>Reads the store; an unset variable is a refusal.</summary>
    public static CleaningPathStoreResult Load(Func<string, string?> readEnvironment, IClock clock) =>
        LoadIfConfigured(readEnvironment, clock) ?? CleaningPathStoreResult.Refused(new CleaningPathValidationError
        {
            Code = CleaningPathCodes.StoreNotProvided,
            Reason = "the store location variable is not set",
            AllowedContract = "one fully qualified Owner-local file path named by " + StoreVariable,
        });

    private static CleaningPathStoreResult LoadFrom(string location, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location))
        {
            return CleaningPathStoreResult.Refused(new CleaningPathValidationError
            {
                Code = CleaningPathCodes.StoreFileRefused,
                Reason = "the store location is not a fully qualified path",
                AllowedContract = "one fully qualified Owner-local file path named by " + StoreVariable,
            });
        }

        try
        {
            var info = new FileInfo(location);
            if (!info.Exists)
            {
                return CleaningPathStoreResult.Refused(new CleaningPathValidationError
                {
                    Code = CleaningPathCodes.StoreFileRefused,
                    Reason = "the store file does not exist",
                    AllowedContract = "one existing Owner-local Cleaning Path store file",
                });
            }

            if (info.Length > MaximumBytes)
            {
                return CleaningPathStoreResult.Refused(new CleaningPathValidationError
                {
                    Code = CleaningPathCodes.StoreTooLarge,
                    Reason = "the store file exceeds the accepted size bound",
                    AllowedContract = $"store file at most {MaximumBytes} bytes",
                });
            }

            return CleaningPathStoreParser.Parse(File.ReadAllText(location), clock.UtcNow);
        }
        catch (Exception)
        {
            return CleaningPathStoreResult.Refused(new CleaningPathValidationError
            {
                Code = CleaningPathCodes.StoreFileRefused,
                Reason = "the store file could not be read",
                AllowedContract = "one readable Owner-local Cleaning Path store file",
            });
        }
    }
}
