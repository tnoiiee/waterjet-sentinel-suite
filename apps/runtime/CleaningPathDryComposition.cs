using System.Globalization;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.MotionComposition;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;

namespace Wjss.Runtime;

/// <summary>
/// The truthful provenance of one TEST_HARDWARE_DRY composition. Every field is a statement the
/// composition can actually observe about itself. A simulated value can never appear here as a
/// physical one, and no endpoint address, consent phrase, profile value or file path is included:
/// only the redacted endpoint fingerprint and canonical codes.
/// </summary>
public sealed record CleaningPathDryProvenance
{
    /// <summary>The device profile this composition was built for (always TEST_HARDWARE_DRY).</summary>
    public required string Profile { get; init; }

    /// <summary>The explicit dry command-composition mode selector that was accepted.</summary>
    public required string Mode { get; init; }

    /// <summary>The redacted Process-Io endpoint identity; never the address.</summary>
    public required string EndpointFingerprint { get; init; }

    /// <summary>The number of verified catalogue rows the command path was proven against.</summary>
    public required int CatalogueRowCount { get; init; }

    /// <summary>The adopted Process-Io device identity the bindings were proven on.</summary>
    public required string ProcessIoDeviceKey { get; init; }

    /// <summary>The Pump-ready source identity the readiness gate reads (AI-003).</summary>
    public required string PumpReadySourceTag { get; init; }

    /// <summary>The canonical Sensor identity this composition is resolving paths for.</summary>
    public required string SensorId { get; init; }

    /// <summary>The resolved Effective Water Jet identity.</summary>
    public required string WaterJetId { get; init; }

    /// <summary>The paired Isolation Valve identity, derived from the Water Jet ordinal.</summary>
    public required string IsolationValveId { get; init; }

    /// <summary>The horizontal axis letter resolved by the canonical catalogs.</summary>
    public required string HorizontalAxisId { get; init; }

    /// <summary>The vertical axis letter resolved by the canonical catalogs.</summary>
    public required string VerticalAxisId { get; init; }

    /// <summary>The motion port identity. "HardwareCapable" is the only admissible value here.</summary>
    public required string MotionPortMode { get; init; }

    /// <summary>The accepted motion profile code (a code only, never a value from the file).</summary>
    public required string MotionProfileCode { get; init; }

    /// <summary>True only when the Process-Io session is the sole write authority for this composition.</summary>
    public required bool SessionAuthorizedWrites { get; init; }

    /// <summary>True only when both coordinators are the composed application coordinators.</summary>
    public required bool CoordinatorsComposed { get; init; }

    /// <summary>The Pump ordinal this composition selects. Stated, never inferred at a call site.</summary>
    public required int PumpSelection { get; init; }

    /// <summary>
    /// True only when both commissioned feedback polarities were configured. When false the Pump
    /// lifecycle refuses every physical command, so the Cleaning Path cannot open an Isolation Valve.
    /// </summary>
    public required bool PumpFeedbackCommissioned { get; init; }

    /// <summary>The typed owner kind the Pump command channel is acquired under.</summary>
    public required string PumpCommandOwner { get; init; }

    /// <summary>True only when the active Cleaning Path equipment boundary is the Pump gate.</summary>
    public required bool PumpGateComposed { get; init; }

    /// <summary>
    /// Which evidence source stood behind the Pump precondition of this run: PHYSICAL, or
    /// SOFTWARE_SIMULATED for an Owner-declared Pump Outlet Pressure. The default is NOT_RECORDED, so a
    /// composition that fails to state its source is visible as a defect instead of reading as physical.
    /// </summary>
    public string PumpEvidenceKind { get; init; } = "NOT_RECORDED";

    /// <summary>
    /// True only when a real Pump command and its commissioned feedback were part of this evidence. It
    /// defaults to false: a declared value never verifies a physical Pump.
    /// </summary>
    public bool PhysicalPumpVerified { get; init; }

    /// <summary>
    /// The Pump Outlet Pressure this run declared, in bar. Zero means the run declared nothing, which is
    /// the physical case: a measured Pump never has a declared value to report.
    /// </summary>
    public double DeclaredPumpOutletBar { get; init; }

    /// <summary>
    /// The fixed, non-negotiable statements every dry composition carries. They exist so a reader of
    /// the evidence cannot mistake a dry composition for a verified or authorized one.
    /// </summary>
    public static IReadOnlyList<string> StandingStatements { get; } = new[]
    {
        "PHYSICAL HARDWARE: NOT EXECUTED / NOT VERIFIED",
        "PRODUCTION: NOT AUTHORIZED",
        "SIMULATED_FALLBACK: NOT PERMITTED",
    };
}

/// <summary>
/// The ONE TEST_HARDWARE_DRY composition root.
///
/// It builds, in one place and in one order:
///   canonical topology (Owner-local Cleaning Path store) + Sensor-owned P1 through P6 (Owner-local
///   sensor-parameter CSV) -> the Phase 3A resolver -> the Process-Io transport, session and
///   equipment boundary -> the shared motion composition (existing owner, existing port, existing
///   manual motion session) -> the Phase 3B manual motion boundary -> the ONE
///   CleaningPathCompositionCoordinator, which composes the ONE CleaningPathExecutionCoordinator
///   internally as its point-advance gate.
///
/// It is never routed through SimulatorRuntime, it exposes no write command and it adds no Kiosk
/// control. Construction performs no physical contact: the Process-Io transport is constructed
/// without connecting, the motion owner is constructed without loading a native module and the
/// Process-Io session is not opened. Contact begins only when the trial authority calls
/// <see cref="Prepare"/> and then <see cref="Start"/>.
/// </summary>
public sealed class CleaningPathDryComposition : IDisposable
{
    public const string SensorVariable = "WJSS_CLEANING_SENSOR_ID";
    public const string PressureLowVariable = "WJSS_PRESSURE_LOW_BAR";
    public const string PressureHighVariable = "WJSS_PRESSURE_HIGH_BAR";
    public const string PumpReadySetpointVariable = "WJSS_PUMP_READY_SETPOINT_BAR";
    public const string ValveSupervisionVariable = "WJSS_VALVE_SUPERVISION_MS";
    public const string CatalogueVariable = "WJSS_PROCESS_IO_CATALOGUE";

    /// <summary>The Owner-local Pump ordinal this composition selects. 1 or 2, never defaulted.</summary>
    public const string PumpSelectionVariable = "WJSS_PUMP_SELECTION";

    /// <summary>The Owner-local analog speed reference, in milliamps. 4 to 20 mA inclusive.</summary>
    public const string PumpAnalogTargetVariable = "WJSS_PUMP_ANALOG_TARGET_MA";

    /// <summary>The Owner-local Pump Outlet pressure freshness budget, in milliseconds.</summary>
    public const string PumpFreshnessVariable = "WJSS_PUMP_FRESHNESS_MS";

    /// <summary>DI-004 polarity. Absent means NOT_CONFIGURED, and then the Pump command fails closed.</summary>
    public const string PumpRunningPolarityVariable = "WJSS_PUMP_RUNNING_POLARITY";

    /// <summary>DI-005 polarity. Absent means NOT_CONFIGURED, and then the Pump command fails closed.</summary>
    public const string PumpFaultPolarityVariable = "WJSS_PUMP_FAULT_POLARITY";

    /// <summary>
    /// How long the Main Valve may take to reach its open limit (DI-013). Required in the physical Pump
    /// class and never defaulted: a travel bound the Product invented would be a wait nobody authorized,
    /// and the field driver is the place that states the 30000 ms the Owner set.
    /// </summary>
    public const string MainValveOpenTimeoutVariable = "WJSS_MAIN_VALVE_OPEN_TIMEOUT_MS";

    /// <summary>How long the Main Valve may take to reach its closed limit (DI-014). Required, no default.</summary>
    public const string MainValveCloseTimeoutVariable = "WJSS_MAIN_VALVE_CLOSE_TIMEOUT_MS";

    /// <summary>How long the commissioned running feedback may take. Required, no default.</summary>
    public const string PumpRunningConfirmVariable = "WJSS_PUMP_RUNNING_CONFIRM_MS";

    /// <summary>How long the outlet pressure may take to exceed the setpoint. Required, no default.</summary>
    public const string PumpReadyTimeoutVariable = "WJSS_PUMP_READY_TIMEOUT_MS";

    /// <summary>How long the commissioned not-running feedback may take during Stop. Required, no default.</summary>
    public const string PumpStoppedConfirmVariable = "WJSS_PUMP_STOPPED_CONFIRM_MS";

    public const string RefusedNotDry = "DRY_COMPOSITION_REFUSED_NOT_DRY_MODE";
    public const string RefusedCatalogue = "DRY_COMPOSITION_REFUSED_CATALOGUE";
    public const string RefusedStore = "DRY_COMPOSITION_REFUSED_STORE";
    public const string RefusedSensorParameters = "DRY_COMPOSITION_REFUSED_SENSOR_PARAMETERS";
    public const string RefusedSensorId = "DRY_COMPOSITION_REFUSED_SENSOR_ID";
    public const string RefusedResolution = "DRY_COMPOSITION_REFUSED_RESOLUTION";
    public const string RefusedThresholds = "DRY_COMPOSITION_REFUSED_THRESHOLDS";
    public const string RefusedSupervision = "DRY_COMPOSITION_REFUSED_SUPERVISION_WINDOW";
    public const string RefusedMotion = "DRY_COMPOSITION_REFUSED_MOTION";
    public const string RefusedMotionNotHardwareCapable = "DRY_COMPOSITION_REFUSED_MOTION_NOT_HARDWARE_CAPABLE";
    public const string RefusedPumpConfiguration = "DRY_COMPOSITION_REFUSED_PUMP_CONFIGURATION";

    /// <summary>
    /// The Main Valve travel timeouts were missing, out of range or unusable, so the physical Pump class was
    /// refused before contact. A valve admission with no bound on its own wait is not a safety rule.
    /// </summary>
    public const string RefusedMainValveConfiguration = "DRY_COMPOSITION_REFUSED_MAIN_VALVE_CONFIGURATION";

    /// <summary>
    /// The selector of the Stage D simulated-Pump-ready evidence class. It must be absent (the physical
    /// Pump is the readiness source) or exactly <see cref="SimulatedPumpReadyLiteral"/>. Any other value is
    /// a refusal: a near miss is never treated as an instruction to simulate, and the selector can never
    /// turn a physical Pump start into a declared one by accident.
    /// </summary>
    public const string SimulatedPumpReadyVariable = "WJSS_DRY_SIMULATED_PUMP_READY";
    public const string SimulatedPumpReadyLiteral = "SOFTWARE_SIMULATED_AI003";

    /// <summary>The declared Pump Outlet Pressure, in bar, of the simulated evidence class.</summary>
    public const string SimulatedPumpBarVariable = "WJSS_DRY_SIMULATED_AI003_BAR";

    /// <summary>
    /// The Owner's typed attestation that the Pump is STOPPED at the panel for the simulated evidence
    /// class. It is a required exact literal: the Product cannot observe the panel, so the Owner states it.
    /// </summary>
    public const string SimulatedPumpStoppedVariable = "WJSS_DRY_SIMULATED_PUMP_STOPPED";
    public const string SimulatedPumpStoppedLiteral = "PUMP_STOPPED_OWNER_ATTESTED";

    /// <summary>
    /// The separate acknowledgement required of the PHYSICAL variant, in which the Pump runs and water can
    /// reach the nozzle while the paired Isolation Valve is open. It is deliberately not the same
    /// acknowledgement as the dry consent: the consequence being accepted is a physical one.
    /// </summary>
    public const string WetSequenceAckVariable = "WJSS_DRY_WET_SEQUENCE_ACK";
    public const string WetSequenceAckLiteral = "WATER_AT_NOZZLE_AUTHORIZED";

    public const string RefusedSimulatedPumpSelector = "DRY_COMPOSITION_REFUSED_SIMULATED_PUMP_SELECTOR";
    public const string RefusedSimulatedPumpBar = "DRY_COMPOSITION_REFUSED_SIMULATED_PUMP_BAR";
    public const string RefusedSimulatedPumpAttestation = "DRY_COMPOSITION_REFUSED_SIMULATED_PUMP_ATTESTATION";
    public const string RefusedWetSequenceAcknowledgement = "DRY_COMPOSITION_REFUSED_WET_SEQUENCE_NOT_ACKNOWLEDGED";

    /// <summary>The deterministic pause between two Pump driver steps. Bounded and never zero.</summary>
    private const int PumpDriveIntervalMilliseconds = 5;
    public const string Composed = "DRY_COMPOSITION_COMPOSED";

    private readonly ProcessIoEndpointHandle _endpoint;
    private readonly ProcessIoPhysicalTransport _transport;
    private readonly ProcessIoSession _session;
    private readonly ProcessIoEquipmentBoundary _equipment;
    private readonly ProcessIoPumpLifecycle _pump;

    /// <summary>
    /// The Main Valve gate, composed only for the physical Pump class. Null is a stated fact and not a
    /// fallback: the declared-value class commands no Pump, so it must not move the valve either.
    /// </summary>
    private readonly ProcessIoMainValveGate? _mainValve;
    private readonly CleaningPathPumpLifecycleGate? _pumpGate;
    private readonly CleaningPathSimulatedPumpReadyGate? _simulatedPumpGate;
    private readonly bool _pumpSimulated;
    private readonly PumpLifecycleConfiguration _pumpConfiguration;
    private readonly int _pumpSelection;
    private readonly string _pumpOwnerId;
    private bool _pumpStarted;
    private bool _mainValveOpened;
    private readonly MotionCompositionRoot _motionRoot;
    private readonly ManualMotionCleaningPathBoundary _motion;
    private readonly CleaningPathCompositionCoordinator _composition;
    private readonly IClock _clock;
    private readonly string _horizontalAxisId;
    private readonly string _verticalAxisId;
    private bool _disposed;

    private CleaningPathDryComposition(
        ProcessIoEndpointHandle endpoint,
        ProcessIoPhysicalTransport transport,
        ProcessIoSession session,
        ProcessIoEquipmentBoundary equipment,
        ProcessIoPumpLifecycle pump,
        CleaningPathPumpLifecycleGate? pumpGate,
        CleaningPathSimulatedPumpReadyGate? simulatedPumpGate,
        bool pumpSimulated,
        PumpLifecycleConfiguration pumpConfiguration,
        int pumpSelection,
        string pumpOwnerId,
        ProcessIoMainValveGate? mainValve,
        MotionCompositionRoot motionRoot,
        ManualMotionCleaningPathBoundary motion,
        CleaningPathCompositionCoordinator composition,
        CleaningPathSnapshot storeSnapshot,
        CleaningPathResolutionResult resolution,
        CleaningPathCompositionRequest request,
        IClock clock,
        CleaningPathDryProvenance provenance)
    {
        _endpoint = endpoint;
        _transport = transport;
        _session = session;
        _equipment = equipment;
        _pump = pump;
        _pumpGate = pumpGate;
        _simulatedPumpGate = simulatedPumpGate;
        _pumpSimulated = pumpSimulated;
        _pumpConfiguration = pumpConfiguration;
        _pumpSelection = pumpSelection;
        _pumpOwnerId = pumpOwnerId;
        _mainValve = mainValve;
        _motionRoot = motionRoot;
        _motion = motion;
        _composition = composition;
        StoreSnapshot = storeSnapshot;
        Resolution = resolution;
        Request = request;
        Provenance = provenance;
        _clock = clock;
        _horizontalAxisId = provenance.HorizontalAxisId;
        _verticalAxisId = provenance.VerticalAxisId;
    }

    /// <summary>The canonical Owner-local topology this composition resolved against.</summary>
    public CleaningPathSnapshot StoreSnapshot { get; }

    /// <summary>The accepted Phase 3A resolution (Sensor-owned path, Centre, WJn / IVn, axes).</summary>
    public CleaningPathResolutionResult Resolution { get; }

    /// <summary>The typed composition request (thresholds and supervision window).</summary>
    public CleaningPathCompositionRequest Request { get; }

    /// <summary>The observable provenance of this composition.</summary>
    public CleaningPathDryProvenance Provenance { get; }

    /// <summary>The ONE composition coordinator. The execution coordinator is composed inside it.</summary>
    public CleaningPathCompositionCoordinator Composition => _composition;

    /// <summary>The Phase 3B manual motion boundary over the shared motion session.</summary>
    public ManualMotionCleaningPathBoundary Motion => _motion;

    /// <summary>The Process-Io equipment boundary: the sole write path, through the session.</summary>
    public ProcessIoEquipmentBoundary Equipment => _equipment;

    /// <summary>The Process-Io session: the sole write-lifecycle authority.</summary>
    public ProcessIoSession Session => _session;

    /// <summary>
    /// The ONE Pump lifecycle of this composition. It is constructed here, in the production
    /// composition root, and nowhere else: the Cleaning Path cannot reach a Pump command without it.
    /// </summary>
    public ProcessIoPumpLifecycle Pump => _pump;

    /// <summary>
    /// The Pump precondition decorator the active Cleaning Path equipment boundary was wrapped with.
    /// An IV OPEN is refused by it unless the Pump lifecycle is at confirmed readiness.
    /// </summary>
    public CleaningPathPumpLifecycleGate? PumpGate => _pumpGate;

    /// <summary>
    /// The declared-value precondition decorator, present only in the simulated Pump-ready evidence class.
    /// It is exposed so the evidence line can print the exact declared bar instead of a paraphrase.
    /// </summary>
    public CleaningPathSimulatedPumpReadyGate? SimulatedPumpGate => _simulatedPumpGate;

    /// <summary>True when this run admitted an Owner-declared Pump-ready value instead of a measured one.</summary>
    public bool PumpReadyIsDeclared => _pumpSimulated;

    /// <summary>The typed Pump lifecycle configuration composed from the Owner-local values.</summary>
    public PumpLifecycleConfiguration PumpConfiguration => _pumpConfiguration;

    /// <summary>The shared motion composition (owner, port, session).</summary>
    public MotionCompositionRoot MotionRoot => _motionRoot;

    /// <summary>
    /// Builds the composition from the validated dry options and the Owner-local external source.
    ///
    /// The order is part of the safety posture. Every refusal returns before a later stage runs, so a
    /// refused composition has no partially built authority: the Process-Io session is never opened,
    /// no native module is loaded and no socket is created.
    /// </summary>
    public static bool TryCreate(
        RuntimeHostOptions options,
        Func<string, string?> readEnvironment,
        IClock clock,
        out CleaningPathDryComposition? composition,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(clock);

        composition = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        // 1. Only the explicit dry composition may reach here. The options were already validated,
        //    but this method is public and must fail closed on its own.
        ProcessIoEndpointHandle? resolvedEndpoint = options.ProcessIoEndpoint;
        if (!options.DryCommandMode
            || options.Profile != DeviceProfile.TEST_HARDWARE_DRY
            || resolvedEndpoint is null
            || string.IsNullOrEmpty(options.ProcessIoConsent))
        {
            refusalCode = RefusedNotDry;
            refusalDetail =
                "The dry composition is built only for TEST_HARDWARE_DRY with an explicit mode selector, "
                + "a resolved Owner-local endpoint and the accepted consent.";
            return false;
        }

        ProcessIoEndpointHandle endpoint = resolvedEndpoint;

        // 2. The write authorization still comes from the gate, and the gate still owns the rule. No
        //    second grant type is introduced and no token is minted here.
        if (!ProcessIoDryCommandGate.TryAuthorize(
                options.Profile,
                options.DryCommandMode,
                endpoint.ToString(),
                options.ProcessIoConsent,
                out ProcessIoWriteAuthorization? authorization,
                out string authorizationCode)
            || authorization is null)
        {
            refusalCode = authorizationCode;
            refusalDetail =
                "The dry command gate refused the write authorization. No composition exists after a "
                + "refusal, so no command path can be opened.";
            return false;
        }

        // 3. The committed Process-Io catalogue, fully verified by the loader (row count, column
        //    contract, source order and the per-row provenance hash). A mismatched file is refused.
        Dictionary<string, ProcessIoCatalogueRow> catalogue;
        try
        {
            if (!TryReadOwnerLocalFile(
                    readEnvironment, CatalogueVariable, MaximumCatalogueBytes, RefusedCatalogue,
                    out string? catalogueText, out refusalCode, out refusalDetail))
            {
                return false;
            }

            var rows = ProcessIoCatalogueLoader.Load(catalogueText!);
            var index = new Dictionary<string, ProcessIoCatalogueRow>(StringComparer.Ordinal);
            foreach (ProcessIoCatalogueRow row in rows)
            {
                index[row.Tag] = row;
            }

            catalogue = index;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            refusalCode = RefusedCatalogue;
            refusalDetail =
                "The Process-Io catalogue was refused by the loader: "
                + ex.Message
                + ". No composition exists after a refused catalogue.";
            return false;
        }

        // 4. The adopted bindings must be present BEFORE anything is composed over them.
        if (!ProcessIoEndpointConfiguration.TryValidateAdoptedBindings(catalogue, out refusalCode, out refusalDetail))
        {
            return false;
        }

        // 5. The Owner-local topology and the Sensor-owned parameters. Both are external files; both
        //    are refused rather than defaulted.
        CleaningPathStoreResult store = CleaningPathStoreFile.Load(readEnvironment, clock);
        CleaningPathSnapshot? acceptedSnapshot = store.Snapshot;
        if (!store.Accepted || acceptedSnapshot is null)
        {
            refusalCode = store.Errors.Count > 0 ? store.Errors[0].Code : RefusedStore;
            refusalDetail =
                "The Owner-local Cleaning Path store was refused, so the canonical topology and the "
                + "Sensor-owned P1 through P6 points cannot be established.";
            return false;
        }

        IReadOnlyList<SensorConfigurationRecord> sensors;
        try
        {
            if (!TryReadOwnerLocalFile(
                    readEnvironment, SensorParameterFileVariable, MaximumSensorParameterBytes, RefusedSensorParameters,
                    out string? sensorText, out refusalCode, out refusalDetail))
            {
                return false;
            }

            SensorParameterImportOutcome import = SensorParameterCsvImporter.Import(sensorText!);
            if (!import.Accepted || import.Result is null)
            {
                refusalCode = import.Refusal?.Code ?? RefusedSensorParameters;
                refusalDetail =
                    "The Owner-local sensor-parameter CSV was refused, so no Sensor-owned path can be "
                    + "resolved and no P1 through P6 point can be attributed.";
                return false;
            }

            sensors = import.Result.Sensors;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            refusalCode = RefusedSensorParameters;
            refusalDetail = "The Owner-local sensor-parameter CSV could not be read or parsed.";
            return false;
        }

        string? sensorId = readEnvironment(SensorVariable);
        if (string.IsNullOrWhiteSpace(sensorId))
        {
            refusalCode = RefusedSensorId;
            refusalDetail =
                $"The dry composition requires the canonical Sensor identity in {SensorVariable}. It is "
                + "never inferred from a wall, a region or a position.";
            return false;
        }

        SensorConfigurationRecord? sensorConfiguration =
            sensors.FirstOrDefault(sensor => string.Equals(sensor.SensorId, sensorId.Trim(), StringComparison.Ordinal));
        if (sensorConfiguration is null)
        {
            refusalCode = RefusedSensorId;
            refusalDetail =
                $"The Sensor identity named by {SensorVariable} is not present in the Owner-local "
                + "sensor-parameter CSV.";
            return false;
        }

        CleaningPathSnapshot storeSnapshot = acceptedSnapshot;

        // 6. The Phase 3A resolver owns the topology derivation: the Sensor-owned path, the fixed
        //    Centre, the Effective Water Jet, the paired Isolation Valve and the axis letters. This
        //    composition copies nothing and re-derives nothing.
        CleaningPathResolutionResult resolution = CleaningPathResolver.Resolve(
            storeSnapshot,
            sensorConfiguration,
            new CleaningPathResolutionRequest { SensorId = sensorConfiguration.SensorId });
        ResolvedCleaningPath? resolved = resolution.Resolution;
        if (!resolution.Accepted || resolved is null)
        {
            refusalCode = RefusedResolution;
            refusalDetail =
                "The Phase 3A resolver refused the requested path (code " + resolution.Code + "). No "
                + "topology value is inferred and no axis is guessed.";
            return false;
        }

        // 7. The typed pressure thresholds and supervision window. They are Owner-local values, never
        //    invented defaults, and they are validated by the existing typed record.
        if (!TryReadThresholds(readEnvironment, out SequencingPressureThresholds? thresholds, out refusalCode, out refusalDetail)
            || thresholds is null)
        {
            return false;
        }

        if (!TryReadSupervision(readEnvironment, out TimeSpan supervision, out refusalCode, out refusalDetail))
        {
            return false;
        }

        // 7b. Which source stands behind the Pump precondition of this run. The Owner states it; nothing is
        //     inferred from an absent value.
        //
        //   PHYSICAL (the selector absent): the lifecycle commands the real Pump and an Isolation Valve may
        //     open only while it reports confirmed readiness. Water can then reach the nozzle, so this
        //     variant additionally requires the physical-consequence acknowledgement below. Nothing about
        //     the readiness rule changes: the setpoint comparison stays strict and the commissioned
        //     feedback polarities stay mandatory.
        //
        //   SOFTWARE_SIMULATED_AI003 (the selector present with its exact literal): the Owner attests the
        //     Pump is stopped at the panel and declares the Pump Outlet Pressure the path would have seen.
        //     The declared value must be strictly above the same setpoint, the paired-IV and Cleaning Path
        //     commands stay fully physical and the evidence is labelled as declared, never as measured.
        bool simulatedPumpReady = false;
        double declaredPumpOutletBar = 0.0;
        string? pumpReadySelector = readEnvironment(SimulatedPumpReadyVariable);
        if (!string.IsNullOrWhiteSpace(pumpReadySelector))
        {
            if (!string.Equals(pumpReadySelector, SimulatedPumpReadyLiteral, StringComparison.Ordinal))
            {
                refusalCode = RefusedSimulatedPumpSelector;
                refusalDetail =
                    $"{SimulatedPumpReadyVariable} must be absent, or exactly '{SimulatedPumpReadyLiteral}'. "
                    + "A different value is refused instead of guessing which Pump-ready evidence class was "
                    + "intended.";
                return false;
            }

            if (!string.Equals(readEnvironment(SimulatedPumpStoppedVariable), SimulatedPumpStoppedLiteral, StringComparison.Ordinal))
            {
                refusalCode = RefusedSimulatedPumpAttestation;
                refusalDetail =
                    $"{SimulatedPumpStoppedVariable} must state exactly '{SimulatedPumpStoppedLiteral}'. The "
                    + "Product cannot observe the panel, so the Owner states that the Pump is stopped and no "
                    + "Pump command is issued in this evidence class.";
                return false;
            }

            if (!TryReadDouble(readEnvironment, SimulatedPumpBarVariable, out declaredPumpOutletBar)
                || !double.IsFinite(declaredPumpOutletBar)
                || declaredPumpOutletBar <= thresholds.PumpReadySetpointBar)
            {
                refusalCode = RefusedSimulatedPumpBar;
                refusalDetail =
                    $"{SimulatedPumpBarVariable} must be a finite number of bar strictly above the Pump-ready "
                    + $"setpoint of {thresholds.PumpReadySetpointBar.ToString(CultureInfo.InvariantCulture)} bar. "
                    + "The strict comparison is not loosened for a declared value.";
                return false;
            }

            simulatedPumpReady = true;
        }
        else if (!string.Equals(readEnvironment(WetSequenceAckVariable), WetSequenceAckLiteral, StringComparison.Ordinal))
        {
            refusalCode = RefusedWetSequenceAcknowledgement;
            refusalDetail =
                $"{WetSequenceAckVariable} must state exactly '{WetSequenceAckLiteral}' before a composition "
                + "that runs the Pump with the paired Isolation Valve is built: water can then leave the "
                + "nozzle while the path moves. The dry consent authorizes the write path, not the "
                + "consequence at the nozzle, so the two are asked for separately.";
            return false;
        }

        // 8. The shared motion composition. Built from the Owner-local external source only, with no
        //    native load and no open at construction.
        if (!MotionCompositionRoot.TryCreate(readEnvironment, clock, out MotionCompositionRoot? motionRoot, out refusalCode, out refusalDetail)
            || motionRoot is null)
        {
            refusalCode = refusalCode.Length == 0 ? RefusedMotion : refusalCode;
            return false;
        }

        if (!motionRoot.HardwareCapable)
        {
            motionRoot.Shutdown();
            refusalCode = RefusedMotionNotHardwareCapable;
            refusalDetail =
                "The composed motion port did not report itself hardware-capable. A simulated port is "
                + "never accepted as the motion authority of a dry Hardware composition.";
            return false;
        }

        // 9. The Process-Io transport, session and equipment boundary. Constructing the transport opens
        //    no socket; the session stays IDLE until the trial authority opens it.
        var endpoints = new Dictionary<string, ProcessIoEndpointConfig>(StringComparer.Ordinal)
        {
            [ProcessIoDevices.RackT8] = new ProcessIoEndpointConfig
            {
                TargetAddress = endpoint.Host,
                Port = endpoint.Port,
                UnitId = endpoint.UnitId,
                TimeoutMs = ProcessIoTransportShape.ReadTimeoutMs,
            },
        };

        var transport = new ProcessIoPhysicalTransport(endpoints);
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        var equipment = new ProcessIoEquipmentBoundary(session, catalogue, authorization);

        // 9b. The ONE Pump command channel of this composition: one boundary over the same session,
        //     one lifecycle, one ownership authority and one time source. A composition without a
        //     commissionable Pump configuration is refused here: the alternative would be a Cleaning
        //     Path that can move and open an Isolation Valve with no Pump authority at all.
        if (!TryReadPumpConfiguration(
                readEnvironment,
                thresholds,
                out PumpLifecycleConfiguration pumpConfiguration,
                out int pumpSelection,
                out refusalCode,
                out refusalDetail))
        {
            transport.Dispose();
            return false;
        }

        // 9b. The Main Valve (block 1.27). It is composed for the physical Pump class only, and its travel
        //     bounds are read here, before any contact, because a valve admission without a bound on its own
        //     wait is not a rule anyone could audit. The declared-value class keeps NotEnforced, which is the
        //     configuration the boundary was built with before this scope existed, so that class is unchanged.
        var mainValveConfiguration = ProcessIoMainValveConfiguration.NotEnforced;
        if (!simulatedPumpReady
            && !TryReadMainValveConfiguration(
                readEnvironment, out mainValveConfiguration, out refusalCode, out refusalDetail))
        {
            transport.Dispose();
            return false;
        }

        var pumpTime = new ClockTimeProvider(clock);
        var pumpOwnership = new PumpCommandOwnership();
        var pumpBoundary = new ProcessIoPumpCommandBoundary(
            session,
            catalogue,
            authorization,
            pumpConfiguration.Feedback,
            pumpTime,
            pumpConfiguration.PressureFreshnessBudget,
            mainValveConfiguration);
        var pumpLifecycle = new ProcessIoPumpLifecycle(pumpBoundary, pumpConfiguration, pumpOwnership, pumpTime);
        ProcessIoMainValveGate? mainValveGate = simulatedPumpReady
            ? null
            : new ProcessIoMainValveGate(pumpBoundary, mainValveConfiguration, pumpTime);

        // The precondition is structural, not advisory: the coordinator receives the gated boundary,
        // so it has no reference through which it could request an IV OPEN without the Pump.
        // The variable handed to the coordinator is named for the decision it carries, whatever that
        // decision's source turns out to be: pumpGate is the ONLY equipment authority the path receives.
        CleaningPathPumpLifecycleGate? lifecyclePumpGate = null;
        CleaningPathSimulatedPumpReadyGate? simulatedPumpGate = null;
        ICleaningPathEquipmentBoundary pumpGate;
        if (simulatedPumpReady)
        {
            // The lifecycle is composed and stays IDLE: it is never started, so it writes no selection, no
            // enable and no analog value. It is deliberately NOT the authority behind the path here, because
            // an unstarted lifecycle cannot permit a valve OPEN and this evidence class is built on the
            // Owner's declaration instead. The valve write path itself is untouched: the decorator delegates
            // every command and every observation to the real boundary.
            simulatedPumpGate = new CleaningPathSimulatedPumpReadyGate(
                equipment, clock, declaredPumpOutletBar, thresholds.PumpReadySetpointBar);
            pumpGate = simulatedPumpGate;
        }
        else
        {
            lifecyclePumpGate = new CleaningPathPumpLifecycleGate(equipment, pumpLifecycle);
            pumpGate = lifecyclePumpGate;
        }
        string pumpOwnerId = "CLEANING_PATH_DRY:" + resolved.SensorId + ":" + resolved.DerivedIsolationValveId;

        // 10. The Phase 3B manual motion boundary over the shared motion session, then the ONE
        //     composition coordinator, which composes the execution coordinator inside itself.
        var motion = new ManualMotionCleaningPathBoundary(
            motionRoot.Session,
            resolved.HorizontalAxisId,
            resolved.VerticalAxisId);
        var compositionCoordinator = new CleaningPathCompositionCoordinator(motion, pumpGate, clock);

        var provenance = new CleaningPathDryProvenance
        {
            Profile = nameof(DeviceProfile.TEST_HARDWARE_DRY),
            Mode = ProcessIoEndpointConfiguration.DryModeVariable,
            EndpointFingerprint = endpoint.Fingerprint,
            CatalogueRowCount = catalogue.Count,
            ProcessIoDeviceKey = ProcessIoDevices.RackT8,
            PumpReadySourceTag = ProcessIoIsolationValves.PumpOutletPressureTag,
            SensorId = resolved.SensorId,
            WaterJetId = resolved.EffectiveWaterJetId,
            IsolationValveId = resolved.DerivedIsolationValveId,
            HorizontalAxisId = resolved.HorizontalAxisId,
            VerticalAxisId = resolved.VerticalAxisId,
            MotionPortMode = motionRoot.Mode.ToString(),
            MotionProfileCode = motionRoot.ProfileCode,
            SessionAuthorizedWrites = true,
            CoordinatorsComposed = true,
            PumpSelection = pumpSelection,
            PumpFeedbackCommissioned = pumpConfiguration.Feedback.IsCommissioned,
            PumpCommandOwner = PumpCommandOwnerKind.INTEGRATED_DRY_CYCLE.ToString(),
            PumpGateComposed = !simulatedPumpReady,
            PumpEvidenceKind = simulatedPumpReady
                ? CleaningPathSimulatedPumpReadyGate.EvidenceKind
                : "PHYSICAL",
            PhysicalPumpVerified = !simulatedPumpReady,
            DeclaredPumpOutletBar = simulatedPumpReady ? declaredPumpOutletBar : 0.0,
        };

        composition = new CleaningPathDryComposition(
            endpoint,
            transport,
            session,
            equipment,
            pumpLifecycle,
            lifecyclePumpGate,
            simulatedPumpGate,
            simulatedPumpReady,
            pumpConfiguration,
            pumpSelection,
            pumpOwnerId,
            mainValveGate,
            motionRoot,
            motion,
            compositionCoordinator,
            storeSnapshot,
            resolution,
            new CleaningPathCompositionRequest
            {
                PressureThresholds = thresholds,
                ValveSupervisionTimeout = supervision,
            },
            clock,
            provenance);

        refusalCode = Composed;
        return true;
    }

    /// <summary>
    /// Opens the Process-Io session and brings both resolved axes to move-ready under the existing
    /// motion lifecycle. This is the first physical contact of the composition and it happens only
    /// under the trial authority.
    ///
    /// It is bounded: it gives up with a typed code rather than spinning, and it reports the exact
    /// stage that failed. Nothing is retried, nothing is reconnected and no Stop is skipped.
    /// </summary>
    public bool Prepare(int maximumSteps, out string code, out string detail)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSteps);

        code = string.Empty;
        detail = string.Empty;

        _session.Open(_clock.UtcNow);

        // One motion boundary step drives the existing session towards opening the port. The port
        // open proves Gate A: a refused open is a refusal, never a fallback.
        for (int step = 0; step < maximumSteps; step += 1)
        {
            _motion.Step();
            if (_motion.SessionReady || _motion.SessionFaulted)
            {
                break;
            }
        }

        if (!_motion.SessionReady)
        {
            code = _motion.SessionFaulted
                ? "DRY_PREPARE_REFUSED_MOTION_FAULTED"
                : "DRY_PREPARE_REFUSED_PORT_NOT_READY";
            detail =
                "The motion port did not reach the ready state within the bounded prepare window. No "
                + "path is started and no valve is commanded.";
            return false;
        }

        // Bring both resolved axes to move-ready through the existing typed Home command. The
        // composition's own start gate requires move-ready on both axes; that gate is not bypassed
        // and no axis is marked ready without the session reporting it.
        //
        // An axis letter outside A through D is a refusal, never a defaulted axis: homing an axis
        // the resolver did not name would move hardware the composition was not authorized to move.
        if (!TryAxisFor(_horizontalAxisId, out ManualMotionAxis horizontalAxis)
            || !TryAxisFor(_verticalAxisId, out ManualMotionAxis verticalAxis)
            || horizontalAxis == verticalAxis)
        {
            code = "DRY_PREPARE_REFUSED_AXIS_NOT_RESOLVED";
            detail =
                "The resolved axis assignment is not two distinct axes within the closed A through D "
                + "set. No Home is requested and no axis is moved.";
            return false;
        }

        _motionRoot.Session.RequestHome(horizontalAxis);
        _motionRoot.Session.RequestHome(verticalAxis);

        for (int step = 0; step < maximumSteps; step += 1)
        {
            _motion.Step();
            if (_motion.SessionFaulted)
            {
                code = "DRY_PREPARE_REFUSED_MOTION_FAULTED";
                detail = "The motion session faulted while homing the resolved axes.";
                return false;
            }

            if (_motion.AxisMoveReady(CleaningPathExecutionRole.Horizontal)
                && _motion.AxisMoveReady(CleaningPathExecutionRole.Vertical))
            {
                code = "DRY_PREPARE_READY";
                detail = "The motion session is ready and both resolved axes are move-ready.";
                return true;
            }
        }

        code = "DRY_PREPARE_REFUSED_NOT_MOVE_READY";
        detail =
            "Both resolved axes did not reach move-ready within the bounded prepare window. The path is "
            + "not started, no point is attributed and no valve is commanded.";
        return false;
    }

    /// <summary>
    /// Starts the ONE composition in the order the safety position requires: the Pump is admitted,
    /// selected, enabled, run and ramped to confirmed readiness FIRST, and only then is the path
    /// started. Pump-ready (AI-003, served only through the gate) is evaluated by the coordinator before
    /// P1 is requested, so no Isolation Valve can open on an unproven Pump.
    ///
    /// Every refusal is returned unchanged with its own code, and a refused Pump start means the path
    /// was never started at all.
    /// </summary>
    public CleaningPathCompositionStartOutcome Start()
    {
        // The order of these two statements is a contract, not a style choice: the Pump precondition is
        // admitted before the path is started, in every evidence class.
        //
        // In the declared-value class there is no Pump command to admit, so the same position in the order
        // is taken by beginning the run's readiness window - and it is still BEFORE the path starts.
        // The Main Valve is proven OPEN before the Pump is admitted, selected, enabled or ramped (Owner
        // ruling 6). A refusal here means the Pump ladder was never started and no Pump channel was written.
        if (!_pumpSimulated && !OpenMainValve(out string mainValveCode))
        {
            return new CleaningPathCompositionStartOutcome(false, mainValveCode, null, null);
        }

        if (!_pumpSimulated && !StartPump(out string pumpCode))
        {
            return new CleaningPathCompositionStartOutcome(false, pumpCode, null, null);
        }

        if (_pumpSimulated)
        {
            // No Pump command is issued in this evidence class. The declared readiness becomes admissible
            // only now, so a paired-IV OPEN cannot be requested before the run has begun, which mirrors the
            // ordering the physical precondition provides.
            _simulatedPumpGate?.Begin();
        }

        return _composition.Start(StoreSnapshot, Resolution, Request);
    }

    /// <summary>
    /// One deterministic driver step: the Pump lifecycle first, then the composed path, motion and
    /// supervision.
    ///
    /// The Pump order is not cosmetic. The lifecycle is the only thing that can observe a lost Running
    /// input, an active Fault or a lost AI-003, and it withdraws <see cref="ProcessIoPumpLifecycle.AllowsValveOpen"/>
    /// the moment it does. Stepping it before the path means the gate has already been closed before the
    /// coordinator can request another Isolation Valve OPEN in the same step.
    /// </summary>
    public void Step()
    {
        if (_pumpStarted && !PumpLifecycleStates.IsTerminal(_pump.State))
        {
            _pump.Step();
        }

        // The valve is stepped after the Pump for the same reason the Pump is stepped first: the lifecycle is
        // the authority that can withdraw a valve permission, and it has already done so for this step before
        // anything here asks the Pump to stop.
        StepMainValve();

        _composition.Step();
    }

    /// <summary>The terminal result, or null while the composition is still active.</summary>
    public CleaningPathCompositionResult? Result => _composition.Result;

    /// <summary>True while the composition is running.</summary>
    public bool Active => _composition.Active;

    /// <summary>
    /// The ONE cleanup, in the order the safety position requires: the Pump Stop first (withdraw the
    /// analog reference, then Run Pump FALSE exactly once, then Enable Inverter FALSE exactly once, then
    /// wait for confirmed not-running), then the motion Stop, then the Isolation Valve close-all through
    /// the session, then the session close.
    ///
    /// Every stage is captured separately and nothing is collapsed into a single boolean. This method
    /// reports rather than throws for recoverable faults, and it never claims a clean shutdown it did
    /// not observe.
    /// </summary>
    public CleaningPathDryCleanupReport Shutdown()
    {
        CleaningPathPumpCleanupReport pump = StopPump();

        // The valve is closed only once the Pump stop is confirmed, and only after that confirmation is
        // already recorded. Nothing in this call can delay the motion stop, the valve close-all or the session
        // close below it, whatever the valve does next (Owner rulings 7 and 8).
        MainValveCleanupRecord? mainValve = CloseMainValve(pump.StoppedConfirmed);

        MotionCleanupRecord motion = _motionRoot.Shutdown();
        ProcessIoCleanupResult valves = _equipment.SafeCleanupCloseAll();
        _session.Close(_clock.UtcNow);

        return new CleaningPathDryCleanupReport
        {
            Pump = pump,
            MainValve = mainValve,
            Motion = motion,
            Valves = valves,
            SessionState = _session.State,
            SessionGeneration = _session.Generation,
        };
    }

    /// <summary>
    /// Proves the Main Valve OPEN before the Pump is admitted, exactly as <see cref="StartPump"/> proves the
    /// Pump: the wait is bounded by the configured travel timeout, the drive is this composition's own step
    /// loop rather than a timer, and a refusal means no Pump channel was written.
    /// </summary>
    private bool OpenMainValve(out string code)
    {
        ProcessIoMainValveGate? gate = _mainValve;
        if (gate is null)
        {
            // The declared-value class composes no valve path. Stated here so the absence is a fact a reader
            // can see rather than a silence that has to be interpreted.
            code = "MAIN_VALVE_NOT_COMPOSED";
            return true;
        }

        if (gate.BeginOpen(out code))
        {
            _mainValveOpened = true;
            code = "MAIN_VALVE_OPEN";
            return true;
        }

        if (!gate.Settled)
        {
            int maximumSteps = (int)(gate.Configuration.OpenTimeout.TotalMilliseconds
                / PumpDriveIntervalMilliseconds) + 1000;

            for (int step = 0; step < maximumSteps && !gate.Settled; step += 1)
            {
                // Supervision is inert until OPEN is confirmed - during the travel it is Step() itself that
                // watches DI-012 - so this call costs nothing here and covers the case where the travel
                // confirmed on the previous line and the trip arrived in the same breath.
                if (gate.Supervise())
                {
                    break;
                }

                gate.Step();

                if (!gate.Settled)
                {
                    Thread.Sleep(PumpDriveIntervalMilliseconds);
                }
            }
        }

        if (gate.AdmitsPumpEnergise)
        {
            _mainValveOpened = true;
            code = "MAIN_VALVE_OPEN";
            return true;
        }

        code = gate.Code == PumpCommandRefusals.Granted
            ? "MAIN_VALVE_TRAVEL_BUDGET_EXHAUSTED"
            : gate.Code;
        return false;
    }

    /// <summary>
    /// Drives an unfinished travel and applies the two supervision duties the Owner ruled for the run: the
    /// over-torque trip and the loss of both limit inputs. Both go through the ONE Pump stop path, and the
    /// valve is not touched further until the stop is confirmed.
    /// </summary>
    private void StepMainValve()
    {
        ProcessIoMainValveGate? gate = _mainValve;
        if (gate is null)
        {
            return;
        }

        if (!gate.Settled)
        {
            gate.Step();
        }

        if (gate.Phase != ProcessIoMainValveGatePhase.ABORTED && !gate.Supervise())
        {
            return;
        }

        if (_pumpStarted && _pump.Result is null && !PumpLifecycleStates.IsTerminal(_pump.State))
        {
            _pump.Stop(PumpStopReason.SEQUENCE_FAILURE);
        }
    }

    /// <summary>
    /// Commands the travel to CLOSED once, and only on confirmed evidence that the Pump stopped. The Pump stop
    /// report is already assembled by the time this runs, and nothing here can delay the motion stop, the
    /// Isolation Valve close-all or the session close that follow it: a valve fault is recorded, never allowed
    /// to become the reason a Pump stayed at speed.
    /// </summary>
    private MainValveCleanupRecord CloseMainValve(bool pumpStopConfirmed)
    {
        ProcessIoMainValveGate? gate = _mainValve;
        if (gate is null)
        {
            return new MainValveCleanupRecord
            {
                Attempted = false,
                Position = ProcessIoMainValvePosition.UNKNOWN.ToString(),
                ClosedConfirmed = false,
                CoilsResting = true,
                OverTorqueSeen = false,
                Code = "MAIN_VALVE_NOT_COMPOSED",
                PumpStopConfirmed = pumpStopConfirmed,
                Writes = 0,
                OpenedThisRun = false,
            };
        }

        bool closed = gate.BeginClose(pumpStopConfirmed, out string code);

        if (!closed && !gate.Settled && code != PumpCommandRefusals.RefusedMainValveCloseNotStopped)
        {
            int maximumSteps = (int)(gate.Configuration.CloseTimeout.TotalMilliseconds
                / PumpDriveIntervalMilliseconds) + 1000;

            for (int step = 0; step < maximumSteps && !gate.Settled; step += 1)
            {
                gate.Step();

                if (!gate.Settled)
                {
                    Thread.Sleep(PumpDriveIntervalMilliseconds);
                }
            }
        }

        return new MainValveCleanupRecord
        {
            Attempted = gate.Writes.Count > 0,
            Position = gate.PositionAtEnd.ToString(),
            ClosedConfirmed = gate.Phase == ProcessIoMainValveGatePhase.CLOSED_CONFIRMED,
            CoilsResting = gate.CoilsResting,
            OverTorqueSeen = gate.OverTorqueSeen,
            Code = closed || code == PumpCommandRefusals.Granted ? gate.Code : code,
            PumpStopConfirmed = pumpStopConfirmed,
            Writes = gate.Writes.Count,
            OpenedThisRun = _mainValveOpened,
        };
    }

    /// <summary>
    /// Starts the Pump and drives it to confirmed readiness under the existing bounded supervision. The
    /// budget is the configured ramp duration plus the two configured feedback timeouts, so the wait is
    /// derived from configuration and never from an invented constant.
    /// </summary>
    private bool StartPump(out string code)
    {
        code = string.Empty;

        var request = new PumpLifecycleStartRequest
        {
            Owner = PumpCommandOwnerKind.INTEGRATED_DRY_CYCLE,
            OwnerId = _pumpOwnerId,
            Selection = _pumpSelection,
        };

        PumpLifecycleStartOutcome admitted = _pump.Start(request);
        if (!admitted.Started)
        {
            code = admitted.Code;
            return false;
        }

        _pumpStarted = true;

        // The budget is derived from configuration: the full down-ramp span, plus the two configured
        // supervision timeouts. Nothing here invents a wait.
        int rampSteps = (int)Math.Ceiling(
            PumpAnalogCommandContract.LiveMaximumMilliamps / _pumpConfiguration.Ramp.Step);
        TimeSpan budget = TimeSpan.FromTicks(_pumpConfiguration.Ramp.Interval.Ticks * rampSteps)
            + _pumpConfiguration.RunningFeedbackTimeout
            + _pumpConfiguration.PumpReadyTimeout;
        DateTimeOffset deadline = _clock.UtcNow + budget;

        // A second, independent bound. A stalled transport or a clock that does not advance must never
        // turn a refusal into a hang, so the drive also stops after a derived number of iterations.
        int maximumSteps = (int)(budget.TotalMilliseconds / PumpDriveIntervalMilliseconds) + 1000;

        for (int step = 0; step < maximumSteps; step += 1)
        {
            if (_pump.State == PumpLifecycleState.PUMP_READY && _pump.AllowsValveOpen)
            {
                return true;
            }

            PumpLifecycleResult? terminal = _pump.Result;
            if (terminal is not null)
            {
                code = "PUMP_START_REFUSED_" + terminal.TerminalState;
                return false;
            }

            if (step > 0 && _clock.UtcNow >= deadline)
            {
                code = "PUMP_START_TIMEOUT_" + _pump.State;
                return false;
            }

            _pump.Step();

            // Owner rulings 3 and 8, applied while the Pump is coming up: an over-torque trip or the loss of
            // both limit inputs stops the Pump through the ONE stop path. The valve is never closed here.
            if (_mainValve is not null && _mainValve.Supervise())
            {
                _pump.Stop(PumpStopReason.SEQUENCE_FAILURE);
                code = "PUMP_START_REFUSED_" + _mainValve.Code;
                return false;
            }

            Thread.Sleep(PumpDriveIntervalMilliseconds);
        }

        code = "PUMP_START_TIMEOUT_" + _pump.State;
        return false;
    }

    /// <summary>
    /// Stops the Pump through the ONE Stop path and reports each stage separately. Ownership is released
    /// by the lifecycle only on a confirmed Stop, so an unconfirmed Stop leaves the channel held and the
    /// cleanup incomplete.
    /// </summary>
    private CleaningPathPumpCleanupReport StopPump()
    {
        if (!_pumpStarted || _pump.State == PumpLifecycleState.IDLE)
        {
            return new CleaningPathPumpCleanupReport
            {
                Attempted = false,
                TerminalState = _pump.State.ToString(),
                CleanupState = PumpCleanupStates.Complete,
                StoppedConfirmed = false,
                OwnershipReleased = !_pump.OwnsPumpCommand,
                CleanupFaultCodes = Array.Empty<string>(),
            };
        }

        if (_pump.Result is null)
        {
            _pump.Stop(PumpStopReason.APPLICATION_CLEANUP);

            int rampSteps = (int)Math.Ceiling(
                PumpAnalogCommandContract.LiveMaximumMilliamps / _pumpConfiguration.Ramp.Step);
            TimeSpan budget = TimeSpan.FromTicks(_pumpConfiguration.Ramp.Interval.Ticks * rampSteps)
                + _pumpConfiguration.StoppedFeedbackTimeout;
            DateTimeOffset deadline = _clock.UtcNow + budget;
            int maximumSteps = (int)(budget.TotalMilliseconds / PumpDriveIntervalMilliseconds) + 1000;

            for (int step = 0; step < maximumSteps && _pump.Result is null; step += 1)
            {
                if (step > 0 && _clock.UtcNow >= deadline) break;

                _pump.Step();
                Thread.Sleep(PumpDriveIntervalMilliseconds);
            }
        }

        PumpLifecycleResult? result = _pump.Result;
        if (result is null)
        {
            // The bounded Stop did not converge. Nothing is claimed and the channel stays held.
            return new CleaningPathPumpCleanupReport
            {
                Attempted = true,
                TerminalState = _pump.State.ToString(),
                CleanupState = PumpCleanupStates.Incomplete,
                StoppedConfirmed = false,
                OwnershipReleased = !_pump.OwnsPumpCommand,
                CleanupFaultCodes = _pump.CleanupFaultCodes,
            };
        }

        return new CleaningPathPumpCleanupReport
        {
            Attempted = true,
            TerminalState = result.TerminalState.ToString(),
            CleanupState = result.CleanupState,
            StoppedConfirmed = result.StoppedConfirmed,
            OwnershipReleased = !_pump.OwnsPumpCommand,
            CleanupFaultCodes = result.CleanupFaultCodes,
        };
    }

    /// <summary>Releases the Process-Io transport. The motion release is reported by <see cref="Shutdown"/>.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _transport.Dispose();
    }

    /// <summary>The variable naming the Owner-local sensor-parameter CSV (same one the commissioning app uses).</summary>
    private const string SensorParameterFileVariable = "WJSS_SENSOR_PARAMETER_CSV";

    /// <summary>The largest accepted committed catalogue file. The generated inventory is well under this.</summary>
    private const long MaximumCatalogueBytes = 1048576;

    /// <summary>The largest accepted Owner-local sensor-parameter CSV: the complete 108-row matrix.</summary>
    private const long MaximumSensorParameterBytes = 1048576;

    private static bool TryReadOwnerLocalFile(
        Func<string, string?> readEnvironment,
        string variable,
        long maximumBytes,
        string refusalCodeOnFailure,
        out string? text,
        out string refusalCode,
        out string refusalDetail)
    {
        text = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? location = readEnvironment(variable);
        if (string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location))
        {
            refusalCode = refusalCodeOnFailure;
            refusalDetail = $"{variable} must name one fully qualified Owner-local file.";
            return false;
        }

        try
        {
            var info = new FileInfo(location);
            if (!info.Exists)
            {
                refusalCode = refusalCodeOnFailure;
                refusalDetail = $"The Owner-local file named by {variable} does not exist.";
                return false;
            }

            if (info.Length > maximumBytes)
            {
                refusalCode = refusalCodeOnFailure;
                refusalDetail = $"The Owner-local file named by {variable} exceeds the accepted bound.";
                return false;
            }

            text = File.ReadAllText(location);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            refusalCode = refusalCodeOnFailure;
            refusalDetail = $"The Owner-local file named by {variable} could not be read.";
            return false;
        }
    }

    internal static bool TryReadThresholds(
        Func<string, string?> readEnvironment,
        out SequencingPressureThresholds? thresholds,
        out string refusalCode,
        out string refusalDetail)
    {
        thresholds = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        if (!TryReadDouble(readEnvironment, PressureLowVariable, out double low)
            || !TryReadDouble(readEnvironment, PressureHighVariable, out double high)
            || !TryReadDouble(readEnvironment, PumpReadySetpointVariable, out double setpoint))
        {
            refusalCode = RefusedThresholds;
            refusalDetail =
                $"The dry composition requires finite Owner-local values in {PressureLowVariable}, "
                + $"{PressureHighVariable} and {PumpReadySetpointVariable}. They are never defaulted: an "
                + "invented threshold would fabricate a Pump-ready decision.";
            return false;
        }

        var candidate = new SequencingPressureThresholds(low, high, setpoint);
        if (!candidate.Valid)
        {
            refusalCode = SequencingPressureThresholds.InvalidCode;
            refusalDetail =
                "The Owner-local pressure thresholds are not valid: Low must be at least zero, High must "
                + "exceed Low, and the Pump-ready setpoint must be at least zero.";
            return false;
        }

        thresholds = candidate;
        return true;
    }

    /// <summary>
    /// Reads the typed Pump lifecycle configuration from Owner-local values. The setpoint is the ONE
    /// setpoint the composition already resolved, so a second setpoint can never disagree with it.
    ///
    /// The two feedback polarities are the only OPTIONAL values, and their absence is NOT a default
    /// command: it leaves them NOT_CONFIGURED, which makes the physical Pump command fail closed. A
    /// supplied value that is not a known polarity is refused rather than silently downgraded.
    /// </summary>
    internal static bool TryReadPumpConfiguration(
        Func<string, string?> readEnvironment,
        SequencingPressureThresholds thresholds,
        out PumpLifecycleConfiguration configuration,
        out int selection,
        out string refusalCode,
        out string refusalDetail)
    {
        configuration = null!;
        selection = 0;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        if (!TryReadOptionalInt(readEnvironment, PumpSelectionVariable, null, out int? parsedSelection)
            || parsedSelection is not { } pumpOrdinal
            || !PumpCommandContract.IsValidSelection(pumpOrdinal))
        {
            refusalCode = RefusedPumpConfiguration;
            refusalDetail =
                $"{PumpSelectionVariable} must name exactly Pump 1 or Pump 2. It is never defaulted: an "
                + "assumed Pump ordinal would command a machine the composition was not authorized for.";
            return false;
        }

        if (!TryReadDouble(readEnvironment, PumpAnalogTargetVariable, out double target)
            || !PumpAnalogCommandContract.IsLive(target))
        {
            refusalCode = RefusedPumpConfiguration;
            refusalDetail =
                $"{PumpAnalogTargetVariable} must be a finite running target inside the commissioned 4 to "
                + "20 mA span. A target below 4 mA is an invalid configuration, and no value is clamped.";
            return false;
        }

        if (!TryReadOptionalInt(
                readEnvironment,
                PumpFreshnessVariable,
                PumpLifecycleConfiguration.OwnerInitialFreshnessBudgetMilliseconds,
                out int? parsedFreshness)
            || parsedFreshness is not { } freshness
            || freshness is < 1 or > 600000)
        {
            refusalCode = RefusedPumpConfiguration;
            refusalDetail = $"{PumpFreshnessVariable} must be an integer between 1 and 600000 milliseconds.";
            return false;
        }

        if (!TryReadRequiredTimeout(readEnvironment, PumpRunningConfirmVariable, out TimeSpan runningTimeout)
            || !TryReadRequiredTimeout(readEnvironment, PumpReadyTimeoutVariable, out TimeSpan readyTimeout)
            || !TryReadRequiredTimeout(readEnvironment, PumpStoppedConfirmVariable, out TimeSpan stoppedTimeout))
        {
            refusalCode = RefusedPumpConfiguration;
            refusalDetail =
                $"The Pump supervision timeouts in {PumpRunningConfirmVariable}, {PumpReadyTimeoutVariable} "
                + $"and {PumpStoppedConfirmVariable} are required integers between 1 and 600000 "
                + "milliseconds. They are never defaulted.";
            return false;
        }

        if (!TryReadPolarity(readEnvironment, PumpRunningPolarityVariable, out PumpFeedbackPolarity runningPolarity)
            || !TryReadPolarity(readEnvironment, PumpFaultPolarityVariable, out PumpFeedbackPolarity faultPolarity))
        {
            refusalCode = RefusedPumpConfiguration;
            refusalDetail =
                $"{PumpRunningPolarityVariable} and {PumpFaultPolarityVariable} must each be absent, "
                + "ACTIVE_HIGH or ACTIVE_LOW. A supplied value that is none of these is refused rather "
                + "than treated as uncommissioned.";
            return false;
        }

        var candidate = new PumpLifecycleConfiguration
        {
            PumpReadySetpointBar = thresholds.PumpReadySetpointBar,
            PressureFreshnessBudget = TimeSpan.FromMilliseconds(freshness),
            RunningFeedbackTimeout = runningTimeout,
            PumpReadyTimeout = readyTimeout,
            StoppedFeedbackTimeout = stoppedTimeout,
            Ramp = new PumpSpeedRampConfiguration
            {
                Interval = TimeSpan.FromMilliseconds(PumpSpeedRampConfiguration.OwnerInitialIntervalMilliseconds),
                Step = PumpSpeedRampConfiguration.OwnerInitialStepMilliamps,
                TargetEngineeringMilliamps = target,
            },
            Feedback = new PumpFeedbackConfiguration
            {
                RunningPolarity = runningPolarity,
                FaultPolarity = faultPolarity,
            },
        };

        if (!candidate.Valid)
        {
            refusalCode = RefusedPumpConfiguration;
            refusalDetail = "The composed Pump lifecycle configuration is not valid.";
            return false;
        }

        configuration = candidate;
        selection = pumpOrdinal;
        return true;
    }

    /// <summary>
    /// Reads the Main Valve travel bounds from Owner-local values for the physical Pump class. Both are
    /// required and neither is defaulted: a wait this Product invented for itself is not an authorized bound
    /// on a machine, and a missing one would leave the gate either hanging or never waiting at all. The
    /// ceiling is the contract's own, so a mistyped six-digit value is refused rather than honored.
    /// </summary>
    internal static bool TryReadMainValveConfiguration(
        Func<string, string?> readEnvironment,
        out ProcessIoMainValveConfiguration configuration,
        out string refusalCode,
        out string refusalDetail)
    {
        configuration = null!;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        if (!TryReadRequiredTimeout(readEnvironment, MainValveOpenTimeoutVariable, out TimeSpan openTimeout)
            || !TryReadRequiredTimeout(readEnvironment, MainValveCloseTimeoutVariable, out TimeSpan closeTimeout)
            || openTimeout > TimeSpan.FromMilliseconds(ProcessIoMainValve.MaximumTravelTimeoutMilliseconds)
            || closeTimeout > TimeSpan.FromMilliseconds(ProcessIoMainValve.MaximumTravelTimeoutMilliseconds))
        {
            refusalCode = RefusedMainValveConfiguration;
            refusalDetail =
                $"The Main Valve travel bounds in {MainValveOpenTimeoutVariable} and "
                + $"{MainValveCloseTimeoutVariable} are required integers between 1 and "
                + $"{ProcessIoMainValve.MaximumTravelTimeoutMilliseconds} milliseconds. They are never "
                + "defaulted: an unbounded wait for a limit switch is not a safety rule.";
            return false;
        }

        var candidate = new ProcessIoMainValveConfiguration
        {
            Enforcement = ProcessIoMainValveEnforcement.ENFORCED,
            OpenTimeout = openTimeout,
            CloseTimeout = closeTimeout,
        };

        if (!candidate.Valid)
        {
            refusalCode = RefusedMainValveConfiguration;
            refusalDetail = "The composed Main Valve configuration is not valid.";
            return false;
        }

        configuration = candidate;
        return true;
    }

    /// <summary>
    /// Reads one feedback polarity. Absence means NOT_CONFIGURED, which is not an error: it leaves the
    /// physical Pump command fail-closed. A supplied value that is not a known polarity IS an error.
    /// </summary>
    private static bool TryReadPolarity(
        Func<string, string?> readEnvironment,
        string variable,
        out PumpFeedbackPolarity polarity)
    {
        polarity = PumpFeedbackPolarity.NOT_CONFIGURED;

        string? raw = readEnvironment(variable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        string value = raw.Trim();
        if (string.Equals(value, "ACTIVE_HIGH", StringComparison.OrdinalIgnoreCase))
        {
            polarity = PumpFeedbackPolarity.ACTIVE_HIGH;
            return true;
        }

        if (string.Equals(value, "ACTIVE_LOW", StringComparison.OrdinalIgnoreCase))
        {
            polarity = PumpFeedbackPolarity.ACTIVE_LOW;
            return true;
        }

        return false;
    }

    /// <summary>Reads one required supervision timeout in milliseconds. Absence is a refusal.</summary>
    private static bool TryReadRequiredTimeout(
        Func<string, string?> readEnvironment,
        string variable,
        out TimeSpan timeout)
    {
        timeout = TimeSpan.Zero;
        if (!TryReadOptionalInt(readEnvironment, variable, null, out int? parsed)
            || parsed is not { } milliseconds
            || milliseconds is < 1 or > 600000)
        {
            return false;
        }

        timeout = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    /// <summary>
    /// Reads one optional integer. When the variable is absent the fallback is used, and a null fallback
    /// means the value was required.
    /// </summary>
    private static bool TryReadOptionalInt(
        Func<string, string?> readEnvironment,
        string variable,
        int? fallback,
        out int? value)
    {
        value = null;

        string? raw = readEnvironment(variable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (fallback is null) return false;
            value = fallback;
            return true;
        }

        if (!int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryReadSupervision(
        Func<string, string?> readEnvironment,
        out TimeSpan supervision,
        out string refusalCode,
        out string refusalDetail)
    {
        supervision = TimeSpan.Zero;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(ValveSupervisionVariable);
        if (string.IsNullOrWhiteSpace(raw)
            || !int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int milliseconds)
            || milliseconds is < 1 or > 600000)
        {
            refusalCode = RefusedSupervision;
            refusalDetail =
                $"{ValveSupervisionVariable} must be an integer between 1 and 600000 milliseconds. It is "
                + "never defaulted.";
            return false;
        }

        supervision = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    private static bool TryReadDouble(Func<string, string?> readEnvironment, string variable, out double value)
    {
        value = double.NaN;
        string? raw = readEnvironment(variable);
        return !string.IsNullOrWhiteSpace(raw)
            && double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    private static bool TryAxisFor(string axisId, out ManualMotionAxis axis)
    {
        switch (axisId)
        {
            case "A":
                axis = ManualMotionAxis.A;
                return true;
            case "B":
                axis = ManualMotionAxis.B;
                return true;
            case "C":
                axis = ManualMotionAxis.C;
                return true;
            case "D":
                axis = ManualMotionAxis.D;
                return true;
            default:
                axis = default;
                return false;
        }
    }
}

/// <summary>One combined dry-cleanup report: motion and valve evidence stay separate.</summary>
/// <summary>
/// The Pump cleanup evidence of one composition. It is captured separately from the motion and valve
/// cleanups so a complete valve close can never hide an unconfirmed Pump stop.
/// </summary>
public sealed record CleaningPathPumpCleanupReport
{
    /// <summary>False only when no Pump lifecycle was ever started, so there was nothing to stop.</summary>
    public required bool Attempted { get; init; }

    /// <summary>The terminal lifecycle state. STOPPED only when the stop was confirmed.</summary>
    public required string TerminalState { get; init; }

    /// <summary>The typed cleanup state. Never claimed complete without a confirmed stopped reading.</summary>
    public required string CleanupState { get; init; }

    /// <summary>True only when the commissioned not-running feedback was observed.</summary>
    public required bool StoppedConfirmed { get; init; }

    /// <summary>
    /// True only when the command channel was released. Ownership is released after the Stop is
    /// confirmed, never before, so an unconfirmed stop keeps the channel held.
    /// </summary>
    public required bool OwnershipReleased { get; init; }

    /// <summary>The ordered cleanup fault codes. Never merged into the primary failure.</summary>
    public required IReadOnlyList<string> CleanupFaultCodes { get; init; }

    /// <summary>True only when every attempted stage was independently observed complete.</summary>
    public bool Complete => !Attempted
        || (StoppedConfirmed && OwnershipReleased
            && string.Equals(CleanupState, PumpCleanupStates.Complete, StringComparison.Ordinal));
}

public sealed record CleaningPathDryCleanupReport
{
    /// <summary>The Pump Stop evidence of this composition.</summary>
    public required CleaningPathPumpCleanupReport Pump { get; init; }

    /// <summary>The motion Stop, session close and native release evidence.</summary>
    public required MotionCleanupRecord Motion { get; init; }

    /// <summary>The Isolation Valve close-all evidence, captured through the session.</summary>
    public required ProcessIoCleanupResult Valves { get; init; }

    /// <summary>
    /// The Main Valve travel-and-close evidence of this composition, or null when no valve path was composed
    /// (the declared-value class). It is deliberately NOT required: a composition that never commanded the
    /// valve must keep constructing this report exactly as it did before the Main Valve existed.
    /// </summary>
    public MainValveCleanupRecord? MainValve { get; init; }

    /// <summary>The Process-Io session state after cleanup.</summary>
    public required string SessionState { get; init; }

    /// <summary>The Process-Io session generation after cleanup.</summary>
    public required int SessionGeneration { get; init; }

    /// <summary>
    /// True only when both cleanups were independently observed complete. Neither side can mask the
    /// other: a complete valve close never hides a failed motion Stop.
    /// </summary>
    public bool Complete => Pump.Complete
        && (MainValve is null || MainValve.Complete)
        && Motion.Complete
        && Valves.Complete;
}

/// <summary>
/// What the composition did with the Main Valve on the way out. Reported separately from the Pump cleanup on
/// purpose: a valve that did not arrive at its closed limit is a fault the Owner must see as its own line, and
/// it is never merged into a claim that the Pump was stopped.
/// </summary>
public sealed record MainValveCleanupRecord
{
    /// <summary>True when a close was attempted at all, i.e. when a valve path is composed.</summary>
    public required bool Attempted { get; init; }

    /// <summary>The position the last read saw.</summary>
    public required string Position { get; init; }

    /// <summary>True when DI-014 confirmed the closed limit.</summary>
    public required bool ClosedConfirmed { get; init; }

    /// <summary>True when neither valve coil is believed energised.</summary>
    public required bool CoilsResting { get; init; }

    /// <summary>True when DI-012 was seen at any point in the run.</summary>
    public required bool OverTorqueSeen { get; init; }

    /// <summary>The gate's own refusal, or <c>OK</c>.</summary>
    public required string Code { get; init; }

    /// <summary>The Pump stop confirmation this close was gated on. False means the close was not attempted.</summary>
    public required bool PumpStopConfirmed { get; init; }

    /// <summary>The number of valve coil writes this run issued.</summary>
    public required int Writes { get; init; }

    /// <summary>True when this run confirmed the valve OPEN at its start. Reported, never inferred.</summary>
    public required bool OpenedThisRun { get; init; }

    /// <summary>True when the valve ended at its closed limit with both coils released.</summary>
    public bool Complete => !Attempted || (ClosedConfirmed && CoilsResting && !OverTorqueSeen);
}
