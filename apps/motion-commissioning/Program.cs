using Wjss.Adapters.MotionLibrary;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;

namespace Wjss.MotionCommissioning;

/// <summary>
/// Entry point of the isolated Owner-local Manual Motion Commissioning application (WSB1 A/B).
///
/// Exactly four invocation shapes are accepted:
///   simulated      - the deterministic in-process simulated port; no native library, no controller,
///                    no network and no output. The built-in synthetic profile is used unless the
///                    Owner-local profile variable is set.
///   wsb1-hardware  - the hardware-capable motion-library path. It requires the exact Owner-local
///                    consent, the existing endpoint and Gate A values, and the Owner-local profile
///                    file. Use it only under the separate hardware commissioning Scope Gate.
///   h1-simulated   - the H1 dry Cleaning Path commissioning surface over the simulated port, with
///                    the Owner-local Cleaning Path store and sensor-parameter CSV.
///   h1-wsb1-hardware - the same H1 surface over the hardware-capable motion-library path. Use it
///                    only under the explicit Owner authorization for the exact reviewed trial.
/// Anything else is refused with exit code 4. A precondition refusal exits with code 2.
///
/// No endpoint, path, hash, consent value or native detail is ever displayed: only canonical codes.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal static class Program
{
    private const string SimulatedModeToken = "simulated";
    private const string HardwareModeToken = "wsb1-hardware";
    private const string H1SimulatedModeToken = "h1-simulated";
    private const string H1HardwareModeToken = "h1-wsb1-hardware";

    /// <summary>
    /// Owner-local variable naming the migrated sensor-parameter CSV. The file lives outside the
    /// repository, is read once and is never written: it is the canonical assignment source the
    /// typed resolver consumes (never inferred from wall or region).
    /// </summary>
    private const string SensorParameterCsvVariable = "WJSS_SENSOR_PARAMETER_CSV";

    private const string Title = "WJSS Manual Motion Commissioning";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args is null || args.Length != 1)
        {
            ShowRefusal("INVALID_INVOCATION", "Use exactly one mode: simulated, wsb1-hardware, h1-simulated or h1-wsb1-hardware.");
            return 4;
        }

        var clock = new SystemClock();
        IManualMotionPort port;
        ManualMotionProfileResult profile;
        var h1Mode = string.Equals(args[0], H1SimulatedModeToken, StringComparison.Ordinal)
            || string.Equals(args[0], H1HardwareModeToken, StringComparison.Ordinal);

        if (string.Equals(args[0], SimulatedModeToken, StringComparison.Ordinal)
            || string.Equals(args[0], H1SimulatedModeToken, StringComparison.Ordinal))
        {
            profile = ManualMotionProfileFile.LoadIfConfigured(Environment.GetEnvironmentVariable)
                ?? ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile());
            port = new ManualMotionSimulatedPort(clock, profile.Profile ?? ManualMotionProfile.CreateSimulationProfile());
        }
        else if (string.Equals(args[0], HardwareModeToken, StringComparison.Ordinal)
            || string.Equals(args[0], H1HardwareModeToken, StringComparison.Ordinal))
        {
            // Consent is consumed first, then the existing native and endpoint configuration is
            // validated; nothing is loaded or opened here. The owner proves Gate A at the open.
            if (!ManualMotionControllerInvocation.TryCreateFromProcessEnvironment(out var owner, out var refusalCode)
                || owner is null)
            {
                ShowRefusal(refusalCode, "The hardware-capable session was refused before any native load.");
                return 2;
            }

            profile = ManualMotionProfileFile.Load(Environment.GetEnvironmentVariable);
            port = new MotionLibraryManualMotionPort(owner);
        }
        else
        {
            ShowRefusal("INVALID_INVOCATION", "Use exactly one mode: simulated, wsb1-hardware, h1-simulated or h1-wsb1-hardware.");
            return 4;
        }

        // A refused profile is shown in the window: the session then never opens the port.
        var session = new ManualMotionSession(port, profile, clock);

        if (h1Mode)
        {
            var store = CleaningPathStoreFile.Load(Environment.GetEnvironmentVariable, clock);
            if (!store.Accepted || store.Snapshot is null)
            {
                ShowRefusal(
                    store.Errors.Count > 0 ? store.Errors[0].Code : "CLEANING_PATH_STORE_REFUSED",
                    "The Owner-local Cleaning Path store was refused.");
                return 2;
            }

            var sensorConfigurations = LoadSensorConfigurations();
            if (sensorConfigurations is null)
            {
                return 2;
            }

            using var h1Form = new H1DryCommissioningForm(session, clock, store.Snapshot, sensorConfigurations);
            Application.Run(h1Form);
            return 0;
        }

        using var runner = new ManualMotionSessionRunner(session);
        using var form = new ManualMotionForm(session, runner);
        runner.Start();
        Application.Run(form);
        return 0;
    }

    /// <summary>
    /// Reads the Owner-local migrated sensor-parameter CSV and returns the 108 canonical Sensor
    /// configuration records, or null after a fail-closed refusal. The file lives outside the
    /// repository and is read once: nothing from it is displayed beyond canonical identities.
    /// </summary>
    private static IReadOnlyList<SensorConfigurationRecord>? LoadSensorConfigurations()
    {
        try
        {
            var location = Environment.GetEnvironmentVariable(SensorParameterCsvVariable);
            if (string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location))
            {
                ShowRefusal("SENSOR_PARAMETERS_NOT_CONFIGURED", "The Owner-local sensor-parameter CSV is not configured.");
                return null;
            }

            var outcome = SensorParameterCsvImporter.Import(File.ReadAllText(location));
            if (!outcome.Accepted || outcome.Result is null)
            {
                ShowRefusal(
                    outcome.Refusal?.Code ?? "SENSOR_PARAMETERS_REFUSED",
                    "The Owner-local sensor-parameter CSV was refused.");
                return null;
            }

            return outcome.Result.Sensors;
        }
        catch (Exception)
        {
            ShowRefusal("SENSOR_PARAMETERS_REFUSED", "The Owner-local sensor-parameter CSV was refused.");
            return null;
        }
    }

    private static void ShowRefusal(string code, string detail)
    {
        _ = MessageBox.Show(
            string.Concat(detail, Environment.NewLine, Environment.NewLine, "Code: ", code),
            Title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }
}
