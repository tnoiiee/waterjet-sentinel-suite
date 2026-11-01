using System.Globalization;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.MotionComposition;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.CleaningPath.Execution;
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
    public const string Composed = "DRY_COMPOSITION_COMPOSED";

    private readonly ProcessIoEndpointHandle _endpoint;
    private readonly ProcessIoPhysicalTransport _transport;
    private readonly ProcessIoSession _session;
    private readonly ProcessIoEquipmentBoundary _equipment;
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
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue;
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

        // 10. The Phase 3B manual motion boundary over the shared motion session, then the ONE
        //     composition coordinator, which composes the execution coordinator inside itself.
        var motion = new ManualMotionCleaningPathBoundary(
            motionRoot.Session,
            resolved.HorizontalAxisId,
            resolved.VerticalAxisId);
        var compositionCoordinator = new CleaningPathCompositionCoordinator(motion, equipment, clock);

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
        };

        composition = new CleaningPathDryComposition(
            endpoint,
            transport,
            session,
            equipment,
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
    /// Starts the ONE composition: Pump-ready (AI-003) is evaluated first and P1 is requested only
    /// when it is accepted. Every refusal is returned unchanged with its own code.
    /// </summary>
    public CleaningPathCompositionStartOutcome Start() => _composition.Start(StoreSnapshot, Resolution, Request);

    /// <summary>One deterministic driver step of the composed path, motion and supervision.</summary>
    public void Step() => _composition.Step();

    /// <summary>The terminal result, or null while the composition is still active.</summary>
    public CleaningPathCompositionResult? Result => _composition.Result;

    /// <summary>True while the composition is running.</summary>
    public bool Active => _composition.Active;

    /// <summary>
    /// Cleanup in the order the safety position requires: motion Stop and session close first, then
    /// the Isolation Valve close-all through the session.
    ///
    /// Every stage is captured separately and nothing is collapsed into a single boolean. This method
    /// reports rather than throws for recoverable faults, and it never claims a clean shutdown it did
    /// not observe.
    /// </summary>
    public CleaningPathDryCleanupReport Shutdown()
    {
        MotionCleanupRecord motion = _motionRoot.Shutdown();
        ProcessIoCleanupResult valves = _equipment.SafeCleanupCloseAll();
        _session.Close(_clock.UtcNow);

        return new CleaningPathDryCleanupReport
        {
            Motion = motion,
            Valves = valves,
            SessionState = _session.State,
            SessionGeneration = _session.Generation,
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

    private static bool TryReadThresholds(
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
public sealed record CleaningPathDryCleanupReport
{
    /// <summary>The motion Stop, session close and native release evidence.</summary>
    public required MotionCleanupRecord Motion { get; init; }

    /// <summary>The Isolation Valve close-all evidence, captured through the session.</summary>
    public required ProcessIoCleanupResult Valves { get; init; }

    /// <summary>The Process-Io session state after cleanup.</summary>
    public required string SessionState { get; init; }

    /// <summary>The Process-Io session generation after cleanup.</summary>
    public required int SessionGeneration { get; init; }

    /// <summary>
    /// True only when both cleanups were independently observed complete. Neither side can mask the
    /// other: a complete valve close never hides a failed motion Stop.
    /// </summary>
    public bool Complete => Motion.Complete && Valves.Complete;
}
