using Wjss.Contracts;
using Wjss.MotionComposition;
using Wjss.Runtime;
using Wjss.Runtime.Core;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Focused coverage for the ONE TEST_HARDWARE_DRY composition root
/// (ACCELERATED-CLEANING-INTEGRATION-01 repairs 1 through 5).
///
/// Every test here is configuration, refusal or source-identity evidence. Nothing contacts a device,
/// opens a socket, loads a native module, commands a valve or moves an axis: the composition is
/// proven to be fail-closed long before any of that could happen.
///
/// The values used are synthetic: a non-routable placeholder host name, the published consent
/// constant and the published dry-mode selector. No Owner-local endpoint, no committed address and no
/// Secret appears in this file.
/// </summary>
public sealed class CleaningPathDryCompositionTests
{
    private const string BenchHost = "bench-host.local";

    /// <summary>
    /// The two shared motion sources, in ordinal order. Hoisted out of the assertion argument so the
    /// expected set is a single immutable field rather than a constant array allocated per call.
    /// </summary>
    private static readonly string[] ExpectedSharedMotionOwners =
    {
        "apps/motion-composition/ManualMotionProfileFile.cs",
        "apps/motion-composition/MotionLibraryManualMotionPort.cs",
    };

    private static Dictionary<string, string?> ValidDryEnvironment() => new(StringComparer.Ordinal)
    {
        [ProcessIoEndpointConfiguration.DryModeVariable] = nameof(DeviceProfile.TEST_HARDWARE_DRY),
        [ProcessIoEndpointConfiguration.HostVariable] = BenchHost,
        [ProcessIoEndpointConfiguration.ConsentVariable] = ProcessIoConsentPhrases.PhysicalDoSingle,
    };

    private static Func<string, string?> Reader(IDictionary<string, string?> values) => key =>
        values.TryGetValue(key, out string? value) ? value : null;

    private static bool TryLoadDry(
        DeviceProfile profile,
        IDictionary<string, string?> values,
        out RuntimeHostOptions options,
        out string code,
        out string detail) =>
        RuntimeHostOptions.TryLoadDryCommand(profile, 5181, Reader(values), out options, out code, out detail);

    // ------------------------------------------------------------ selector semantics

    [Fact]
    public void An_Absent_Dry_Selector_Means_The_Mode_Was_Not_Selected()
    {
        Assert.True(ProcessIoEndpointConfiguration.TryReadDryMode(
            Reader(new Dictionary<string, string?>(StringComparer.Ordinal)),
            out bool dryCommandMode, out string code, out string detail));

        Assert.False(dryCommandMode);
        Assert.Empty(code);
        Assert.Empty(detail);
    }

    [Fact]
    public void Any_Other_Dry_Selector_Value_Is_Refused()
    {
        foreach (string value in new[] { "true", "1", "DRY", "TEST_HARDWARE", "TEST_HARDWARE_READ_ONLY" })
        {
            var values = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [ProcessIoEndpointConfiguration.DryModeVariable] = value,
            };

            Assert.False(ProcessIoEndpointConfiguration.TryReadDryMode(
                Reader(values), out bool dryCommandMode, out string code, out _));
            Assert.False(dryCommandMode);
            Assert.Equal(ProcessIoEndpointConfiguration.DryModeNotSelected, code);
        }
    }

    // ------------------------------------------------------------ refusal ordering

    [Fact]
    public void Production_Is_Refused_Before_Any_Variable_Is_Read()
    {
        int reads = 0;
        bool created = RuntimeHostOptions.TryLoadDryCommand(
            DeviceProfile.PRODUCTION,
            5181,
            _ =>
            {
                reads += 1;
                return null;
            },
            out _,
            out string code,
            out _);

        Assert.False(created);
        Assert.Equal(0, reads);
        Assert.Equal(ProcessIoEndpointConfiguration.ProductionRefused, code);
    }

    [Fact]
    public void The_Dry_Mode_Rejects_A_Simulator_Profile()
    {
        Assert.False(TryLoadDry(
            DeviceProfile.SIMULATOR, ValidDryEnvironment(), out _, out string code, out string detail));

        Assert.Equal(ProcessIoDryCommandGate.RefusedProfileNotDry, code);
        Assert.Contains("never used as a stand-in", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Dry_Mode_Rejects_The_Physical_Read_Only_Profile()
    {
        Assert.False(TryLoadDry(
            DeviceProfile.TEST_HARDWARE_READ_ONLY, ValidDryEnvironment(), out _, out string code, out _));

        Assert.Equal(ProcessIoDryCommandGate.RefusedProfileNotDry, code);
    }

    [Fact]
    public void The_Dry_Mode_Requires_An_Owner_Local_Endpoint()
    {
        var values = ValidDryEnvironment();
        values.Remove(ProcessIoEndpointConfiguration.HostVariable);

        Assert.False(TryLoadDry(
            DeviceProfile.TEST_HARDWARE_DRY, values, out _, out string code, out string detail));

        Assert.Equal(ProcessIoEndpointConfiguration.NotConfigured, code);
        Assert.Contains("never substituted", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Dry_Mode_Requires_The_Exact_Consent()
    {
        var missing = ValidDryEnvironment();
        missing.Remove(ProcessIoEndpointConfiguration.ConsentVariable);
        Assert.False(TryLoadDry(
            DeviceProfile.TEST_HARDWARE_DRY, missing, out _, out string code, out _));
        Assert.Equal(ProcessIoEndpointConfiguration.ConsentMissing, code);

        var wrong = ValidDryEnvironment();
        wrong[ProcessIoEndpointConfiguration.ConsentVariable] = "I AUTHORIZE SOMETHING ELSE";
        Assert.False(TryLoadDry(
            DeviceProfile.TEST_HARDWARE_DRY, wrong, out _, out string code2, out _));
        Assert.Equal(ProcessIoEndpointConfiguration.ConsentMismatch, code2);
    }

    [Fact]
    public void An_Out_Of_Range_Port_Or_Unit_Id_Is_Refused()
    {
        var badPort = ValidDryEnvironment();
        badPort[ProcessIoEndpointConfiguration.PortVariable] = "70000";
        Assert.False(TryLoadDry(DeviceProfile.TEST_HARDWARE_DRY, badPort, out _, out string portCode, out _));
        Assert.Equal(ProcessIoEndpointConfiguration.Rejected, portCode);

        var badUnit = ValidDryEnvironment();
        badUnit[ProcessIoEndpointConfiguration.UnitIdVariable] = "0";
        Assert.False(TryLoadDry(DeviceProfile.TEST_HARDWARE_DRY, badUnit, out _, out string unitCode, out _));
        Assert.Equal(ProcessIoEndpointConfiguration.Rejected, unitCode);
    }

    // ------------------------------------------------------------ accepted options

    [Fact]
    public void A_Fully_Configured_Dry_Environment_Produces_Validated_Options()
    {
        Assert.True(TryLoadDry(
            DeviceProfile.TEST_HARDWARE_DRY, ValidDryEnvironment(), out RuntimeHostOptions options, out _, out _));

        Assert.True(options.DryCommandMode);
        Assert.Equal(DeviceProfile.TEST_HARDWARE_DRY, options.Profile);
        Assert.NotNull(options.ProcessIoEndpoint);
        Assert.Equal(ProcessIoConsentPhrases.PhysicalDoSingle, options.ProcessIoConsent);
        Assert.Equal(TcAcquisitionMode.DETERMINISTIC, options.AcquisitionMode);
        Assert.Null(options.Endpoint);
    }

    [Fact]
    public void The_Endpoint_Handle_Never_Reveals_Its_Address()
    {
        Assert.True(TryLoadDry(
            DeviceProfile.TEST_HARDWARE_DRY, ValidDryEnvironment(), out RuntimeHostOptions options, out _, out _));

        string rendered = options.ProcessIoEndpoint!.ToString();
        Assert.DoesNotContain(BenchHost, rendered, StringComparison.Ordinal);
        Assert.Contains("PROCESS_IO_ENDPOINT_CONFIGURED", rendered, StringComparison.Ordinal);
        Assert.StartsWith("endpoint-sha256-prefix:", options.ProcessIoEndpoint.Fingerprint, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ composition is fail-closed

    [Fact]
    public void The_Composition_Refuses_When_The_Catalogue_Is_Not_Configured()
    {
        Assert.True(TryLoadDry(
            DeviceProfile.TEST_HARDWARE_DRY, ValidDryEnvironment(), out RuntimeHostOptions options, out _, out _));

        var clock = new SystemClock();
        var values = ValidDryEnvironment();

        Assert.False(CleaningPathDryComposition.TryCreate(
            options, Reader(values), clock, out CleaningPathDryComposition? composition, out string code, out _));

        Assert.Null(composition);
        Assert.Equal(CleaningPathDryComposition.RefusedCatalogue, code);
    }

    [Fact]
    public void The_Composition_Refuses_A_Non_Dry_Option_Set()
    {
        var notDry = new RuntimeHostOptions
        {
            Profile = DeviceProfile.SIMULATOR,
            Port = 5181,
            SyntheticSeed = 0UL,
            TickIntervalMilliseconds = 150,
            StateHistoryCapacity = RuntimeLimits.DefaultRevisionHistoryCapacity,
            DeltaHistoryCapacity = RuntimeLimits.DefaultDeltaHistoryCapacity,
        };

        Assert.False(CleaningPathDryComposition.TryCreate(
            notDry,
            Reader(new Dictionary<string, string?>(StringComparer.Ordinal)),
            new SystemClock(),
            out CleaningPathDryComposition? composition,
            out string code,
            out _));

        Assert.Null(composition);
        Assert.Equal(CleaningPathDryComposition.RefusedNotDry, code);
    }

    [Fact]
    public void The_Motion_Composition_Refuses_Without_Owner_Consent()
    {
        Assert.False(MotionCompositionRoot.TryCreate(
            Reader(new Dictionary<string, string?>(StringComparer.Ordinal)),
            new SystemClock(),
            out MotionCompositionRoot? root,
            out string code,
            out string detail));

        Assert.Null(root);
        Assert.NotEmpty(code);
        Assert.Contains("no simulated port is substituted", detail, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ source and project identity

    [Fact]
    public void The_Host_Builds_The_Dry_Composition_Root_Before_The_Simulator_Runtime()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "Program.cs"));

        int dry = program.IndexOf("return CleaningPathDryTrial.Run(profile, port,", StringComparison.Ordinal);
        int simulator = program.IndexOf("SimulatorRuntime.Create(options, clock", StringComparison.Ordinal);

        Assert.True(dry > 0, "the dry composition root must be built by the host");
        Assert.True(simulator > dry, "the dry branch must return before the simulator composition");
        Assert.Contains("if (dryCommandMode)", program, StringComparison.Ordinal);
        Assert.Contains("ProcessIoEndpointConfiguration.TryReadDryMode", program, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Dry_Surface_Adds_No_Write_Route_And_No_Stream()
    {
        string root = RepoRoot();
        foreach (string file in new[] { "Program.cs", "CleaningPathDryTrial.cs", "CleaningPathDryComposition.cs" })
        {
            string text = File.ReadAllText(Path.Combine(root, "apps", "runtime", file));
            foreach (string forbidden in new[] { "MapPost", "MapPut", "MapPatch", "MapDelete", "text/event-stream" })
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void The_Trial_Is_A_Bounded_Console_Surface_With_No_Kiosk_Control()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryTrial.cs"));

        Assert.Contains("--dry-cleaning-trial", text, StringComparison.Ordinal);
        Assert.Contains("CONSOLE_ONLY_NO_PORT_NO_ROUTE_NO_KIOSK_CONTROL", text, StringComparison.Ordinal);
        Assert.Contains("MaximumSteps", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Application.Run", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Runtime_References_The_Shared_Motion_Composition_Library()
    {
        string root = RepoRoot();
        string runtime = File.ReadAllText(Path.Combine(root, "apps", "runtime", "Wjss.Runtime.csproj"));

        Assert.Contains("motion-composition/Wjss.MotionComposition.csproj", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("<Compile Include", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Adapter_Exposes_Its_Owner_Only_To_The_Shared_Composition_Library()
    {
        string adapter = File.ReadAllText(
            Path.Combine(RepoRoot(), "adapters", "motion-library", "Wjss.Adapters.MotionLibrary.csproj"));

        Assert.Contains("InternalsVisibleTo Include=\"Wjss.MotionComposition\"", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalsVisibleTo Include=\"Wjss.Runtime\"", adapter, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Shared_Composition_Library_Owns_The_Only_Port_And_Profile_Reader()
    {
        string root = RepoRoot();
        string project = File.ReadAllText(
            Path.Combine(root, "apps", "motion-composition", "Wjss.MotionComposition.csproj"));

        // The library references the adapter and the Application; it links nothing from a host.
        Assert.Contains("adapters/motion-library/Wjss.Adapters.MotionLibrary.csproj", project, StringComparison.Ordinal);
        Assert.Contains("packages/application/Wjss.Runtime.Core.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<Compile Include", project, StringComparison.Ordinal);

        // Exactly one port implementation and one profile reader exist in the tree, and they live here.
        string[] owners = Directory
            .EnumerateFiles(Path.Combine(root, "apps"), "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("sealed class MotionLibraryManualMotionPort", StringComparison.Ordinal)
                || File.ReadAllText(file).Contains("static class ManualMotionProfileFile", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedSharedMotionOwners, owners);
    }

    [Fact]
    public void The_Isolated_Application_Links_The_Shared_Files_And_Stays_Unreferenced()
    {
        string root = RepoRoot();
        string app = File.ReadAllText(
            Path.Combine(root, "apps", "motion-commissioning", "Wjss.MotionCommissioning.csproj"));

        // Still exactly two assembly references, and no package reference.
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(app, "<ProjectReference "));
        Assert.DoesNotContain("<PackageReference", app, StringComparison.Ordinal);

        // The shared sources are linked from their single home, never copied. The exact Include
        // attribute is asserted rather than a bare path: "../../motion-composition/..." still
        // contains "../motion-composition/..." as a substring, so a bare path assertion would stay
        // green with the sibling-relative form broken.
        Assert.Contains(
            "Compile Include=\"../motion-composition/MotionLibraryManualMotionPort.cs\"", app, StringComparison.Ordinal);
        Assert.Contains(
            "Compile Include=\"../motion-composition/ManualMotionProfileFile.cs\"", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Include=\"../../motion-composition", app, StringComparison.Ordinal);

        // Nothing references the isolated application.
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "apps"), "*.csproj", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "Wjss.MotionCommissioning.csproj")
            {
                continue;
            }

            Assert.DoesNotContain(
                "motion-commissioning",
                File.ReadAllText(file),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Shared_Composition_Adds_No_Native_Or_Text_Command_Entry_Point()
    {
        string directory = Path.Combine(RepoRoot(), "apps", "motion-composition");
        foreach (string file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            string text = File.ReadAllText(file);
            foreach (string forbidden in new[]
                     {
                         "DllImport", "LibraryImport", "NativeLibrary", "gclib_command",
                         "Process.Start", "new TcpClient", "Socket",
                     })
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void The_Solution_Tracks_The_Shared_Composition_Project()
    {
        string solution = File.ReadAllText(Path.Combine(RepoRoot(), "WaterJetSentinelSuite.sln"));
        Assert.Contains("apps\\motion-composition\\Wjss.MotionComposition.csproj", solution, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ the ONE Pump composition

    [Fact]
    public void The_Production_Composition_Constructs_Exactly_One_Pump_Lifecycle()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));

        // Exactly one of each authority: one boundary, one lifecycle, one ownership channel, one gate.
        Assert.Equal(1, Count(text, "new ProcessIoPumpCommandBoundary("));
        Assert.Equal(1, Count(text, "new ProcessIoPumpLifecycle("));
        Assert.Equal(1, Count(text, "new PumpCommandOwnership("));
        Assert.Equal(1, Count(text, "new CleaningPathPumpLifecycleGate("));

        // The lifecycle is built in the production composition root and exposed only through it.
        Assert.Contains("ProcessIoPumpLifecycle Pump => _pump;", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Active_Cleaning_Path_Uses_The_Pump_Gate_And_Not_The_Raw_Equipment_Boundary()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));

        // The coordinator is handed the gated boundary, so it has no reference through which it could
        // request an Isolation Valve OPEN without consulting the Pump lifecycle.
        Assert.Contains(
            "new CleaningPathCompositionCoordinator(motion, pumpGate, clock)", text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new CleaningPathCompositionCoordinator(motion, equipment, clock)", text, StringComparison.Ordinal);

        // The raw boundary remains the cleanup authority, so a CLOSE is never blocked by a Pump state.
        Assert.Contains("_equipment.SafeCleanupCloseAll()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Composition_Starts_The_Pump_Before_The_Motion_Composition()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));

        int pumpStart = text.IndexOf("StartPump(", StringComparison.Ordinal);
        int compositionStart = text.IndexOf("_composition.Start(StoreSnapshot, Resolution, Request)", StringComparison.Ordinal);

        Assert.True(pumpStart >= 0, "The composition must start the Pump.");
        Assert.True(compositionStart >= 0, "The composition must start the motion composition.");
        Assert.True(pumpStart < compositionStart, "The Pump must be started before the path is started.");

        // And the Pump is stopped before the motion cleanup, so the jet is withdrawn first.
        int pumpStop = text.IndexOf("StopPump()", StringComparison.Ordinal);
        int motionCleanup = text.IndexOf("_motionRoot.Shutdown()", StringComparison.Ordinal);
        Assert.True(pumpStop >= 0 && motionCleanup >= 0);
        Assert.True(pumpStop < motionCleanup, "The Pump Stop must precede the motion cleanup.");
    }

    [Fact]
    public void The_Pump_Configuration_Is_Typed_Owner_Local_And_Never_Defaulted_To_A_Command()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));

        foreach (string variable in new[]
                 {
                     "WJSS_PUMP_SELECTION",
                     "WJSS_PUMP_ANALOG_TARGET_MA",
                     "WJSS_PUMP_FRESHNESS_MS",
                     "WJSS_PUMP_RUNNING_POLARITY",
                     "WJSS_PUMP_FAULT_POLARITY",
                     "WJSS_PUMP_RUNNING_CONFIRM_MS",
                     "WJSS_PUMP_READY_TIMEOUT_MS",
                     "WJSS_PUMP_STOPPED_CONFIRM_MS",
                     "WJSS_MAIN_VALVE_OPEN_TIMEOUT_MS",
                     "WJSS_MAIN_VALVE_CLOSE_TIMEOUT_MS",
                 })
        {
            Assert.Contains(variable, text, StringComparison.Ordinal);
        }

        // A refused Pump configuration is a typed composition refusal, never a silent default command.
        Assert.Contains("DRY_COMPOSITION_REFUSED_PUMP_CONFIGURATION", text, StringComparison.Ordinal);

        // The two Main Valve bounds are refused the same way, so a missing travel bound cannot be honored by
        // inventing one. They are named here rather than in their own test because the rule is identical.
        Assert.Contains("DRY_COMPOSITION_REFUSED_MAIN_VALVE_CONFIGURATION", text, StringComparison.Ordinal);
        Assert.Contains("TryReadMainValveConfiguration", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Main Valve order, asserted as text because it is a safety order and not an implementation detail:
    /// the valve must be proven OPEN before the Pump is admitted, and it may be commanded closed only once the
    /// Pump stop is both recorded and confirmed - which also means the close can never stand between the Pump
    /// and its motion cleanup.
    /// </summary>
    [Fact]
    public void The_Main_Valve_Is_Proven_Open_Before_The_Pump_And_Closed_After_The_Confirmed_Stop()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));

        int valveOpen = text.IndexOf("OpenMainValve(out string mainValveCode)", StringComparison.Ordinal);
        int pumpStart = text.IndexOf("StartPump(out string pumpCode)", StringComparison.Ordinal);
        Assert.True(valveOpen >= 0, "The composition must prove the Main Valve OPEN.");
        Assert.True(pumpStart >= 0, "The composition must start the Pump.");
        Assert.True(valveOpen < pumpStart, "The Main Valve must be proven OPEN before the Pump is started.");

        int pumpStop = text.IndexOf("StopPump();", StringComparison.Ordinal);
        int valveClose = text.IndexOf("CloseMainValve(pump.StoppedConfirmed)", StringComparison.Ordinal);
        int motionCleanup = text.IndexOf("_motionRoot.Shutdown()", StringComparison.Ordinal);
        Assert.True(pumpStop >= 0 && valveClose >= 0 && motionCleanup >= 0);
        Assert.True(pumpStop < valveClose, "The valve may only be closed after the Pump stop is recorded.");
        Assert.True(valveClose < motionCleanup, "A valve close must never delay the motion cleanup.");

        // The valve path is composed for the physical class only, and the declared-value class keeps the
        // configuration that existed before this scope, so a simulated Pump cannot move a real valve.
        Assert.Contains("ProcessIoMainValveConfiguration.NotEnforced", text, StringComparison.Ordinal);
        Assert.Contains("new ProcessIoMainValveGate(pumpBoundary, mainValveConfiguration, pumpTime)", text, StringComparison.Ordinal);

        // The close is gated on the confirmation itself, not on an intention, and the refusal it returns when
        // the Pump stop is unconfirmed is its own code rather than a silent skip.
        string gate = File.ReadAllText(
            Path.Combine(RepoRoot(), "packages", "application", "ProcessIo", "ProcessIoMainValveGate.cs"));
        Assert.Contains("PumpCommandRefusals.RefusedMainValveCloseNotStopped", gate, StringComparison.Ordinal);
        Assert.Contains("public bool BeginClose(bool pumpStoppedConfirmed, out string code)", gate, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessIoPumpLifecycle", gate, StringComparison.Ordinal);
        Assert.DoesNotContain("_boundary.SetEnableInverter(", gate, StringComparison.Ordinal);
        Assert.DoesNotContain("_boundary.SetRunPump(", gate, StringComparison.Ordinal);
        Assert.DoesNotContain("_boundary.SetSpeedCommand(", gate, StringComparison.Ordinal);
        Assert.Contains("_boundary.SetMainValveOpenCoil(", gate, StringComparison.Ordinal);
        Assert.Contains("_boundary.SetMainValveCloseCoil(", gate, StringComparison.Ordinal);
    }

    /// <summary>
    /// The importer refusal is the operator's only view of why a CSV was refused, so the importer's own
    /// detail must be carried verbatim instead of being replaced by a generic sentence. The code stays the
    /// importer's code with the same fallback, and an absent detail never becomes a fabricated reason.
    /// </summary>
    [Fact]
    public void The_Import_Refusal_Carries_The_Importers_Own_Detail()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));

        Assert.Contains("string importDetail = import.Refusal?.Detail ?? string.Empty;", text, StringComparison.Ordinal);
        // The composition source spells the appended clause with a quote on each side of the label, so the
        // pattern is written here as a verbatim literal: doubled quotes, and no escape character to lose.
        Assert.Contains(@""" Importer detail: "" + importDetail.Trim()", text, StringComparison.Ordinal);
        Assert.Contains("importDetail.Trim().Length == 0", text, StringComparison.Ordinal);
        Assert.Contains("refusalCode = import.Refusal?.Code ?? RefusedSensorParameters;", text, StringComparison.Ordinal);

        // Exactly one place quotes an importer detail: the wrapper is not copied into another refusal path.
        Assert.Equal(1, Count(text, "Importer detail:"));

        // The refusal stays fail-closed: the detail is appended to the same sentence and the composition
        // still returns false without any fallback import, any partial Sensor set and any warning promotion.
        int refusal = text.IndexOf("refusalCode = import.Refusal?.Code ?? RefusedSensorParameters;", StringComparison.Ordinal);
        string block = text.Substring(refusal, text.IndexOf("sensors = import.Result.Sensors;", StringComparison.Ordinal) - refusal);
        Assert.Contains("return false;", block, StringComparison.Ordinal);
        Assert.DoesNotContain("import.Result!", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// The accepted Owner-local profile and the resolved path must name the same WSB cabinet, and the
    /// comparison must happen while nothing is open: the four axis letters A/B/C/D are shared by every
    /// cabinet, so they can never detect a profile that belongs to another controller.
    /// </summary>
    [Fact]
    public void The_Profile_Cabinet_Is_Matched_To_The_Resolved_Cabinet_Before_Any_Session_Exists()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));

        int check = text.IndexOf(
            "if (!string.Equals(motionRoot.CabinetId, resolved.CabinetId, StringComparison.Ordinal))",
            StringComparison.Ordinal);
        Assert.True(check >= 0, "the composition must compare the profile cabinet with the resolved cabinet");
        Assert.Equal(1, Count(text, "motionRoot.CabinetId, resolved.CabinetId"));

        int transport = text.IndexOf("new ProcessIoPhysicalTransport(endpoints)", StringComparison.Ordinal);
        int session = text.IndexOf("new ProcessIoSession(ProcessIoDevices.RackT8, transport)", StringComparison.Ordinal);
        int lifecycle = text.IndexOf("new ProcessIoPumpLifecycle(pumpBoundary", StringComparison.Ordinal);
        Assert.True(transport > check, "no transport may exist before the cabinet check");
        Assert.True(session > check, "no session may exist before the cabinet check");
        Assert.True(lifecycle > check, "no Pump lifecycle may exist before the cabinet check");

        int refusal = text.IndexOf("refusalCode = RefusedMotionCabinetMismatch;", StringComparison.Ordinal);
        Assert.True(refusal > check, "the refusal must be typed by the check itself");
        string block = text.Substring(check, refusal - check);
        Assert.Contains("motionRoot.Shutdown();", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispose()", block, StringComparison.Ordinal);

        Assert.Contains(
            @"public const string RefusedMotionCabinetMismatch = ""DRY_COMPOSITION_REFUSED_MOTION_CABINET_MISMATCH"";",
            text,
            StringComparison.Ordinal);

        // The identity comes from the accepted profile itself: never a restated literal, never the file
        // name, and never the built-in simulation profile (which the root refuses for Hardware).
        string root = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "motion-composition", "MotionCompositionRoot.cs"));
        Assert.Contains("profile.Profile!.ControllerId", root, StringComparison.Ordinal);
        Assert.Contains("public string CabinetId => _cabinetId;", root, StringComparison.Ordinal);
        Assert.Equal(1, Count(root, "root = new MotionCompositionRoot("));
        Assert.DoesNotContain("CabinetId = Path", root, StringComparison.Ordinal);
    }

    /// <summary>
    /// The bounded prepare window is a poll budget, not a duration: one step drives at most one port
    /// call, so an un-slept window spends its whole budget in microseconds of wall time while every
    /// timeout the session enforces (the profile Home bound, the brake settle, the observation cadence)
    /// is measured against the injected clock. All three prepare windows are therefore paced, and the
    /// successful path returns without waiting so that the start gate reads the freshness just observed.
    /// </summary>
    [Fact]
    public void The_Prepare_Windows_Are_Paced_And_A_Ready_Window_Waits_For_Nothing()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));
        string prepare = PrepareWindow(text);

        // One pacing sleep for each bounded window: the port open, the observation pre-roll, the homing.
        Assert.Equal(3, Count(prepare, "Thread.Sleep(PumpDriveIntervalMilliseconds);"));
        Assert.Contains("if (_motion.SessionReady || _motion.SessionFaulted)", prepare, StringComparison.Ordinal);

        // The step budget the trial passes is only a window because it is paced: 24000 steps at the
        // 5 millisecond drive interval is 120 seconds, which bounds one Home of each axis plus the
        // stop and settle that follow it. A budget change without a pacing change is a lie.
        string trial = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryTrial.cs"));
        Assert.Contains("private const int MaximumPrepareSteps = 24000;", trial, StringComparison.Ordinal);
        Assert.Contains("dry.Prepare(MaximumPrepareSteps, out code, out detail)", trial, StringComparison.Ordinal);

        int readyCheck = prepare.IndexOf(
            "if (_motion.AxisMoveReady(CleaningPathExecutionRole.Horizontal)", StringComparison.Ordinal);
        int readyReturn = prepare.IndexOf("code = \"DRY_PREPARE_READY\";", StringComparison.Ordinal);
        Assert.True(readyCheck > 0, "move-ready must be read from the session, never assumed");
        Assert.True(readyReturn > readyCheck, "the session must be asked before the window decides");
        Assert.Equal(0, Count(prepare.Substring(readyCheck, readyReturn - readyCheck), "Thread.Sleep"));
    }

    /// <summary>
    /// The session accepts a Home only once it can prove the axis is standing still from a fresh status
    /// observation, and the step that opened the port has published no observation at all: a Home asked
    /// for before that first read is refused OBSERVATION_NOT_FRESH. So the prepare window reads both
    /// resolved axes first, refuses with its own typed code when the read never arrives, and never
    /// substitutes a read of its own.
    /// </summary>
    [Fact]
    public void The_Home_Is_Requested_Only_After_The_Session_Can_Observe_Both_Axes()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));
        string prepare = PrepareWindow(text);

        int preRoll = prepare.IndexOf(
            "BothResolvedAxesObserved(_motionRoot.Session.Snapshot, horizontalAxis, verticalAxis)",
            StringComparison.Ordinal);
        int refusal = prepare.IndexOf("\"DRY_PREPARE_REFUSED_NO_OBSERVATION\";", StringComparison.Ordinal);
        int home = prepare.IndexOf("_motionRoot.Session.RequestHome(horizontalAxis)", StringComparison.Ordinal);
        Assert.True(preRoll > 0, "the prepare window must read the observation state the session published");
        Assert.True(home > 0, "both resolved axes must be homed before the path may start");
        Assert.True(preRoll < home, "an axis the session cannot observe must never be homed");
        Assert.True(refusal < home, "the missing-observation refusal must be typed by the pre-roll itself");
        Assert.Equal(1, Count(text, "\"DRY_PREPARE_REFUSED_NO_OBSERVATION\";"));
        Assert.Contains("no Home is requested", prepare, StringComparison.Ordinal);

        // Both axes are asked in one window because the session excludes per axis, not per session.
        Assert.Equal(1, Count(prepare, "_motionRoot.Session.RequestHome(verticalAxis)"));

        // Nothing about a refusal stays silent: every prepare refusal carries the session's own facts,
        // and those facts are only ever read from the published snapshot, never from the environment.
        Assert.Equal(5, Count(prepare, "PrepareRefusalDiagnostic();"));
        Assert.Contains("ManualMotionSnapshot snapshot = _motionRoot.Session.Snapshot;", prepare, StringComparison.Ordinal);
        Assert.Contains("LastOutcomeCode", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("readEnvironment(", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("Fingerprint", prepare, StringComparison.Ordinal);
    }

    /// <summary>
    /// The text of the prepare window itself, up to the member that starts the composition. Each prepare
    /// contract is asserted inside this slice so a sleep or a diagnostic added to an unrelated Pump or
    /// valve window can never satisfy it by accident.
    /// </summary>
    private static string PrepareWindow(string text)
    {
        int start = text.IndexOf(
            "public bool Prepare(int maximumSteps, out string code, out string detail)", StringComparison.Ordinal);
        int end = text.IndexOf(
            "public CleaningPathCompositionStartOutcome Start()", StringComparison.Ordinal);
        Assert.True(start > 0, "the composition must own exactly one bounded prepare window");
        Assert.True(end > start, "the prepare window must end where the composition start begins");
        return text.Substring(start, end - start);
    }

    /// <summary>
    /// A Home that ends without move-ready ends the window instead of being re-observed for the rest of
    /// the budget: Home is the only route to move-ready here and it is requested exactly once, so an
    /// applied-and-finished attempt with nothing running on either resolved axis has no future. The
    /// evidence is the session's own log past a watermark captured before the requests, exactly as the
    /// H1 commissioning coordinator correlates a per-request outcome, and the refusal code is the one
    /// the prepare window already reported. An axis still homing, a request not yet applied, and an axis
    /// Homed with stale position reads must all keep the window open, and the early end may not
    /// dispatch, retry or abort anything.
    /// </summary>
    [Fact]
    public void The_Prepare_Window_Ends_When_The_Home_Attempt_Cannot_Succeed_Any_More()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));
        string prepare = PrepareWindow(text);

        // The watermark is captured from the published log before either request, so an outcome is
        // attributed to this window rather than to earlier history of the same session.
        int watermark = prepare.IndexOf(
            "long homeLogWatermark = NewestLogSequence(_motionRoot.Session.Snapshot);", StringComparison.Ordinal);
        int home = prepare.IndexOf("_motionRoot.Session.RequestHome(horizontalAxis)", StringComparison.Ordinal);
        Assert.True(watermark > 0, "the window must mark where its own Home outcomes begin");
        Assert.True(watermark < home, "the watermark must precede the requests it measures");

        // Both resolved axes are judged, and only after the window has already failed to see readiness.
        int readyReturn = prepare.IndexOf("code = \"DRY_PREPARE_READY\";", StringComparison.Ordinal);
        int earlyEnd = prepare.IndexOf(
            "if (HomeAttemptFinished(homeSnapshot, horizontalAxis, homeLogWatermark)", StringComparison.Ordinal);
        Assert.True(earlyEnd > readyReturn, "move-ready is decided first, never by the early end");
        Assert.Contains("&& HomeAttemptFinished(homeSnapshot, verticalAxis, homeLogWatermark))", prepare,
            StringComparison.Ordinal);

        // The early end costs no pacing interval: it breaks out of the window, it does not sleep first.
        int sleepAfter = prepare.IndexOf("Thread.Sleep(PumpDriveIntervalMilliseconds);", earlyEnd);
        Assert.True(sleepAfter > earlyEnd, "the paced window must still be paced");
        Assert.Equal(0, Count(prepare.Substring(earlyEnd, sleepAfter - earlyEnd), "Thread.Sleep"));
        Assert.Equal(3, Count(prepare, "Thread.Sleep(PumpDriveIntervalMilliseconds);"));

        // Nothing is concluded while the session still has work on either axis, and a Homed axis whose
        // reads went stale is never treated as a failure: only the session's own state closes the door.
        Assert.Contains("if (entry is null || entry.HomeState == ManualMotionHomeState.Homing)", prepare,
            StringComparison.Ordinal);
        Assert.Contains("homeAttemptsFinished = !BothResolvedAxesHomed(", prepare, StringComparison.Ordinal);
        Assert.Contains("PublishedAxis(snapshot, axis)?.HomeState == ManualMotionHomeState.Homed;", prepare,
            StringComparison.Ordinal);
        Assert.Contains("logEntry.Sequence > watermark", prepare, StringComparison.Ordinal);
        Assert.Contains("logEntry.Operation == ManualMotionOperationKind.Home", prepare,
            StringComparison.Ordinal);

        // One shared refusal for both endings: the early end reuses the typed code and the session's own
        // diagnostic, and each of the two sentences exists exactly once.
        Assert.Equal(1, Count(prepare, "code = \"DRY_PREPARE_REFUSED_NOT_MOVE_READY\";"));
        Assert.Equal(1, Count(prepare, "Both resolved axes finished their Home attempt"));
        Assert.Equal(1, Count(prepare, "Both resolved axes did not reach move-ready within the bounded prepare window"));
        Assert.Equal(1, Count(prepare, "DRY_PREPARE_REFUSED_NOT_MOVE_READY"));

        // Two requests, ever: the early end invents no third command path and no retry of Home.
        Assert.Equal(2, Count(prepare, "_motionRoot.Session.Request"));
        Assert.DoesNotContain("_motionRoot.Session.RequestStop", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestDefine", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestMove", prepare, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a Home zero was reached is decided by the two native position registers and nothing else,
    /// so a prepare refusal that prints only their freshness cannot be adjudicated from the transcript.
    /// The Owner ruling for this scope authorized printing the raw readings, the coordinate state and the
    /// Home-input interpretation of each resolved axis: hardware state from the published snapshot only,
    /// with no scaling, no unit claim and no invented register label.
    /// </summary>
    [Fact]
    public void The_Prepare_Diagnostic_Prints_The_Readings_The_Zero_Decision_Depends_On()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryComposition.cs"));
        string diagnostic = DiagnosticWindow(text);

        Assert.Contains("+ \" tp=\" + NativeReading(entry.PositionTp)", diagnostic, StringComparison.Ordinal);
        Assert.Contains("+ \" rp=\" + NativeReading(entry.PositionRp)", diagnostic, StringComparison.Ordinal);
        Assert.Contains("+ \" coord=\" + entry.CoordinateState", diagnostic, StringComparison.Ordinal);
        Assert.Contains("+ \" homeInput=\" + entry.HomeInput", diagnostic, StringComparison.Ordinal);

        // Nothing that was already printed is dropped: the freshness keys the runbook documents are
        // still there, beside the values they describe.
        foreach (string key in new[]
                 {
                     "\" home=\" + entry.HomeState",
                     "\" motion=\" + entry.Motion",
                     "\" statusFresh=\" + (entry.StatusWord.Fresh",
                     "\" tpFresh=\" + (entry.PositionTp.Fresh",
                     "\" rpFresh=\" + (entry.PositionRp.Fresh",
                 })
        {
            Assert.Contains(key, diagnostic, StringComparison.Ordinal);
        }

        // A reading is published as the value and its freshness, or null. No conversion is applied to it,
        // because the label of these registers is not vendor-confirmed.
        Assert.Contains(
            "(reading.Value?.ToString(\"0.####\", CultureInfo.InvariantCulture) ?? \"null\")", diagnostic,
            StringComparison.Ordinal);
        Assert.Contains("\"(fresh=\" + (reading.Fresh ? \"True\" : \"False\") + \")\"", diagnostic,
            StringComparison.Ordinal);
        Assert.DoesNotContain("10000", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("NativePerMillimetre", diagnostic, StringComparison.Ordinal);

        // Hardware state read from the snapshot: never the environment, never a file, never an endpoint.
        Assert.DoesNotContain("Environment.", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("http", diagnostic, StringComparison.Ordinal);
    }

    /// <summary>
    /// The diagnostic members of the composition, from the axis description to the end of the prepare
    /// window's helpers, so a value printed by an unrelated member cannot satisfy the contract above.
    /// </summary>
    private static string DiagnosticWindow(string text)
    {
        int start = text.IndexOf(
            "private static string DescribeAxis(ManualMotionSnapshot snapshot, ManualMotionAxis axis)",
            StringComparison.Ordinal);
        int end = text.IndexOf(
            "public CleaningPathCompositionStartOutcome Start()", StringComparison.Ordinal);
        Assert.True(start > 0, "the composition must own one prepare-refusal axis description");
        Assert.True(end > start, "the axis description must end where the composition start begins");
        return text.Substring(start, end - start);
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count += 1;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
