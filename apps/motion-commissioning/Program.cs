using Wjss.Adapters.MotionLibrary;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;

namespace Wjss.MotionCommissioning;

/// <summary>
/// Entry point of the isolated Owner-local Manual Motion Commissioning application (WSB1 A/B).
///
/// Exactly two invocation shapes are accepted:
///   simulated      - the deterministic in-process simulated port; no native library, no controller,
///                    no network and no output. The built-in synthetic profile is used unless the
///                    Owner-local profile variable is set.
///   wsb1-hardware  - the hardware-capable motion-library path. It requires the exact Owner-local
///                    consent, the existing endpoint and Gate A values, and the Owner-local profile
///                    file. Use it only under the separate hardware commissioning Scope Gate.
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
    private const string Title = "WJSS Manual Motion Commissioning";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args is null || args.Length != 1)
        {
            ShowRefusal("INVALID_INVOCATION", "Use exactly one mode: simulated or wsb1-hardware.");
            return 4;
        }

        var clock = new SystemClock();
        IManualMotionPort port;
        ManualMotionProfileResult profile;

        if (string.Equals(args[0], SimulatedModeToken, StringComparison.Ordinal))
        {
            profile = ManualMotionProfileFile.LoadIfConfigured(Environment.GetEnvironmentVariable)
                ?? ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile());
            port = new ManualMotionSimulatedPort(clock, profile.Profile ?? ManualMotionProfile.CreateSimulationProfile());
        }
        else if (string.Equals(args[0], HardwareModeToken, StringComparison.Ordinal))
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
            ShowRefusal("INVALID_INVOCATION", "Use exactly one mode: simulated or wsb1-hardware.");
            return 4;
        }

        // A refused profile is shown in the window: the session then never opens the port.
        var session = new ManualMotionSession(port, profile, clock);
        using var runner = new ManualMotionSessionRunner(session);
        using var form = new ManualMotionForm(session, runner);
        runner.Start();
        Application.Run(form);
        return 0;
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
