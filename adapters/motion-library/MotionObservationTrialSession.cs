namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The B2-H one-shot, read-only observation trial: the smallest executable composition that binds
/// the already authorized B2 read boundary to one already-open controller handle, executes the
/// fixed sixteen typed intents exactly once in deterministic order, captures the bounded native
/// values, invalidates the boundary, and then closes the handle and releases the native modules.
///
/// Authority. This type adds NO controller capability. It resolves only the four exports the
/// lifecycle needs (force direct-mode, open, close, and the one scoped read export), it passes
/// only the vendor-documented controller address, and it can form a request only through
/// <see cref="MotionObservationSession"/>, whose requests are typed subjects resolved against the
/// closed literal plan. There is no string parameter in this type, so no caller text, CLI argument,
/// environment value, file, JSON, reflection path or log line can reach a request.
///
/// Discipline. One open, one observation session, one outstanding observation, one deterministic
/// pass, one generation fence, no retry, no reconnect, no replay, no polling and no second owner.
/// The observation boundary is invalidated BEFORE the controller handle is closed, and the close
/// happens BEFORE the native modules are released. A cleanup that cannot be confirmed prevents
/// acceptance and is reported truthfully.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed class MotionObservationTrialSession
{
    /// <summary>
    /// The fixed read plan: four axis status words, four first position values, four second
    /// position values and four digital-output states, in that deterministic order. Each entry is
    /// resolved to its typed intent by the closed plan inside the observation session, so this
    /// table names subjects only and contains no request text.
    /// </summary>
    private static readonly (string Kind, string AxisId, int OutputNumber)[] ReadPlan =
    {
        (MotionObservedStateVocabulary.KindAxisStatus, MotionObservedStateVocabulary.AxisA, 0),
        (MotionObservedStateVocabulary.KindAxisStatus, MotionObservedStateVocabulary.AxisB, 0),
        (MotionObservedStateVocabulary.KindAxisStatus, MotionObservedStateVocabulary.AxisC, 0),
        (MotionObservedStateVocabulary.KindAxisStatus, MotionObservedStateVocabulary.AxisD, 0),
        (MotionObservedStateVocabulary.KindCommandPosition, MotionObservedStateVocabulary.AxisA, 0),
        (MotionObservedStateVocabulary.KindCommandPosition, MotionObservedStateVocabulary.AxisB, 0),
        (MotionObservedStateVocabulary.KindCommandPosition, MotionObservedStateVocabulary.AxisC, 0),
        (MotionObservedStateVocabulary.KindCommandPosition, MotionObservedStateVocabulary.AxisD, 0),
        (MotionObservedStateVocabulary.KindActualPosition, MotionObservedStateVocabulary.AxisA, 0),
        (MotionObservedStateVocabulary.KindActualPosition, MotionObservedStateVocabulary.AxisB, 0),
        (MotionObservedStateVocabulary.KindActualPosition, MotionObservedStateVocabulary.AxisC, 0),
        (MotionObservedStateVocabulary.KindActualPosition, MotionObservedStateVocabulary.AxisD, 0),
        (MotionObservedStateVocabulary.KindDigitalOutput, MotionObservedStateVocabulary.NoAxis, 1),
        (MotionObservedStateVocabulary.KindDigitalOutput, MotionObservedStateVocabulary.NoAxis, 2),
        (MotionObservedStateVocabulary.KindDigitalOutput, MotionObservedStateVocabulary.NoAxis, 3),
        (MotionObservedStateVocabulary.KindDigitalOutput, MotionObservedStateVocabulary.NoAxis, 4),
    };

    private readonly MotionLibraryProbeConfiguration _probeConfiguration;
    private readonly MotionControllerEndpointConfiguration _endpointConfiguration;
    private readonly Func<MotionLibraryProbeOutcome> _gateAProbe;
    private readonly Func<IControllerKernel> _kernelFactory;
    private readonly Func<IntPtr, IntPtr, IObservationReadKernel> _readKernelFactory;

    private int _started;

    /// <summary>
    /// The production boundary: the real probe runner over the real kernel, the real controller
    /// kernel, and the one bound read kernel for this assembly.
    /// </summary>
    public MotionObservationTrialSession(
        MotionLibraryProbeConfiguration probeConfiguration,
        MotionControllerEndpointConfiguration endpointConfiguration)
        : this(
            probeConfiguration,
            endpointConfiguration,
            () => new MotionLibraryProbeRunner(probeConfiguration).Probe(),
            () => new WindowsControllerKernel(),
            static (handle, export) => new BoundObservationReadKernel(handle, export))
    {
    }

    /// <summary>Test seam (InternalsVisibleTo): exercises the complete trial defect class without any native module.</summary>
    internal MotionObservationTrialSession(
        MotionLibraryProbeConfiguration probeConfiguration,
        MotionControllerEndpointConfiguration endpointConfiguration,
        Func<MotionLibraryProbeOutcome> gateAProbe,
        Func<IControllerKernel> kernelFactory,
        Func<IntPtr, IntPtr, IObservationReadKernel> readKernelFactory)
    {
        _probeConfiguration = probeConfiguration ?? throw new ArgumentNullException(nameof(probeConfiguration));
        _endpointConfiguration = endpointConfiguration ?? throw new ArgumentNullException(nameof(endpointConfiguration));
        _gateAProbe = gateAProbe ?? throw new ArgumentNullException(nameof(gateAProbe));
        _kernelFactory = kernelFactory ?? throw new ArgumentNullException(nameof(kernelFactory));
        _readKernelFactory = readKernelFactory ?? throw new ArgumentNullException(nameof(readKernelFactory));
    }

    /// <summary>The number of typed reads in the fixed plan. Exactly sixteen.</summary>
    public static int PlannedReadCount => ReadPlan.Length;

    /// <summary>
    /// Executes the one-shot trial exactly once. A second call is refused, never retried.
    /// </summary>
    public MotionObservationTrialOutcome Run()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return MotionObservationTrialOutcome.Refused(
                MotionObservationTrialVocabulary.TrialAlreadyRun,
                "This observation trial has already run. A trial is one-shot: a new read pass is a " +
                "new explicit Owner decision, not a retry of this one.",
                openClassification: string.Empty,
                generation: 0,
                observationBoundaryInvalidated: true,
                controllerHandleClosed: true,
                nativeHandlesReleased: true);
        }

        return RunCore();
    }

    private MotionObservationTrialOutcome RunCore()
    {
        IControllerKernel? kernel = null;
        IntPtr companionModule = IntPtr.Zero;
        IntPtr primaryModule = IntPtr.Zero;
        IntPtr closeExport = IntPtr.Zero;
        IntPtr handle = IntPtr.Zero;
        var opened = false;
        var openClassification = string.Empty;
        MotionObservationSession? observationSession = null;
        MotionObservationTrialOutcome? finalOutcome = null;

        MotionObservationTrialOutcome ExecuteTrialFlow()
        {
            MotionObservationTrialOutcome Refuse(
                string code,
                string detail,
                string classification = "",
                IReadOnlyList<MotionObservationTrialReading>? readings = null) =>
                MotionObservationTrialOutcome.Refused(
                    code,
                    detail,
                    classification,
                    observationSession?.CurrentGeneration ?? 0,
                    observationBoundaryInvalidated: observationSession is null
                        || observationSession.ReadBoundaryInvalidated,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false,
                    readings);

            // Phase 1: the Gate A native identity/version proof, unchanged. No controller may be
            // opened before it passes.
            var probe = _gateAProbe();
            if (probe is null || !probe.Accepted)
            {
                return Refuse(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "The native identity/version proof refused, so no controller was loaded, opened " +
                    "or read. Nothing was contacted.");
            }

            // Phase 2: load the approved modules (companion first) through the same restricted
            // loader and prove the same loaded-module paths as the identity surface.
            kernel = _kernelFactory();
            companionModule = kernel.LoadApprovedModule(_probeConfiguration.CompanionModulePath);
            if (companionModule == IntPtr.Zero
                || !ProveLoadedModulePath(kernel, companionModule, _probeConfiguration.CompanionModulePath))
            {
                return Refuse(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "The approved companion module could not be loaded and proven from its approved " +
                    "location with the restricted search set. Nothing was contacted.");
            }

            primaryModule = kernel.LoadApprovedModule(_probeConfiguration.PrimaryModulePath);
            if (primaryModule == IntPtr.Zero
                || !ProveLoadedModulePath(kernel, primaryModule, _probeConfiguration.PrimaryModulePath))
            {
                return Refuse(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "The approved primary module could not be loaded and proven from its approved " +
                    "location with the restricted search set. Nothing was contacted.");
            }

            // Phase 3: resolve ONLY the exports this lifecycle needs. The one scoped read export is
            // resolved here, before the open, so a missing read boundary refuses before any read.
            var forceExport = kernel.ResolveExport(primaryModule, MotionControllerApi.ForceDirectModeExportName);
            var openExport = kernel.ResolveExport(primaryModule, MotionControllerApi.OpenExportName);
            closeExport = kernel.ResolveExport(primaryModule, MotionControllerApi.CloseExportName);
            var commandExport = MotionControllerCommandApi.ResolveCommandExport(kernel, primaryModule);

            if (forceExport == IntPtr.Zero || openExport == IntPtr.Zero || closeExport == IntPtr.Zero)
            {
                return Refuse(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "An authorized lifecycle export is absent from the loaded approved module, so no " +
                    "controller was opened and no read was attempted.");
            }

            if (commandExport == IntPtr.Zero)
            {
                return Refuse(
                    MotionObservationTrialVocabulary.ObservationBoundaryUnavailable,
                    "The one approved read export is absent from the loaded approved module, so the " +
                    "observation boundary cannot be bound. No controller was opened and no read was " +
                    "attempted.");
            }

            // Phase 4: the direct/no-gcaps posture is the process-wide selection, made BEFORE the
            // single open. It is the sole direct-mode authority of this process.
            try
            {
                kernel.InvokeForceDirectMode(forceExport, directOnly: 0);
            }
            catch (Exception)
            {
                return Refuse(
                    MotionControllerIdentityVocabulary.DirectModeNotEnforced,
                    "The direct/no-gcaps posture call did not complete. No open was attempted after " +
                    "the failure and nothing was contacted.");
            }

            // Phase 5: exactly one open with the validated controller address and nothing else.
            var addressBuffer = ToAsciiBuffer(
                MotionControllerApi.BuildOpenAddress(_endpointConfiguration));
            int openResult;
            try
            {
                openResult = kernel.InvokeOpen(openExport, addressBuffer, out handle);
            }
            catch (Exception)
            {
                return Refuse(
                    MotionControllerIdentityVocabulary.ConnectionFailed,
                    "The single open attempt failed before a result could be classified. There is no " +
                    "retry and no reconnect; the trial is over.");
            }

            // A handle returned alongside a refusal is still owned by this lifecycle and is closed
            // on the cleanup path.
            opened = handle != IntPtr.Zero;

            if (openResult != MotionControllerApi.ResultSuccess)
            {
                return Refuse(
                    MotionControllerApi.ClassifyOpenRefusal(openResult),
                    "The single open attempt was refused and classified with the canonical " +
                    "open-result word carried by this code. There is no retry, no reconnect and no " +
                    "vendor error text; the trial is over.");
            }

            // The vendor call itself reported success, so the classification is truthful even if
            // the returned handle is unusable.
            openClassification = MotionObservationTrialVocabulary.OpenSucceeded;

            if (handle == IntPtr.Zero)
            {
                return Refuse(
                    MotionControllerIdentityVocabulary.ConnectionHandleInvalid,
                    "The open reported success but returned no controller handle. No identity or " +
                    "observation read was attempted; there is no retry and no reconnect.");
            }

            // Phase 6: one observation session bound to this handle and this export. The read
            // kernel owns neither: it is invalidated first on the cleanup path and never outlives
            // the controller handle.
            observationSession = new MotionObservationSession(
                _readKernelFactory(handle, commandExport));
            var readings = new List<MotionObservationTrialReading>(ReadPlan.Length);
            for (var index = 0; index < ReadPlan.Length; index++)
            {
                var (kind, axisId, outputNumber) = ReadPlan[index];
                var outcome = observationSession.Observe(new MotionObservationRequest
                {
                    Kind = kind,
                    AxisId = axisId,
                    OutputNumber = outputNumber,
                });

                readings.Add(new MotionObservationTrialReading
                {
                    Ordinal = index + 1,
                    Code = outcome.Code,
                    Accepted = outcome.Accepted,
                    Generation = outcome.Generation,
                    State = outcome.States.Count == 1 ? outcome.States[0] : null,
                });

                if (!outcome.Accepted)
                {
                    // Stop the remaining read sequence. The failing typed intent keeps its own
                    // canonical code; nothing is repaired, rounded, retried or replayed.
                    return MotionObservationTrialOutcome.Refused(
                        outcome.Code,
                        "A typed read was refused, so the remaining read sequence was not attempted. " +
                        "No value was repaired, rounded or retried, and the observation is reported " +
                        "as refused instead of a fabricated state.",
                        MotionObservationTrialVocabulary.OpenSucceeded,
                        outcome.Generation,
                        observationBoundaryInvalidated: false,
                        controllerHandleClosed: false,
                        nativeHandlesReleased: false,
                        readings);
                }
            }

            return MotionObservationTrialOutcome.Complete(
                observationSession.CurrentGeneration,
                readings,
                "One open, sixteen bounded reads accepted in plan order, the observation boundary " +
                "invalidated before the close, and every native handle released. The values are " +
                "native observations only: no position, limit, home, brake, fault or motion meaning " +
                "is inferred and no engineering conversion is applied.");
        }

        var finalClosed = false;
        var finalReleased = false;
        var boundaryInvalidated = false;

        try
        {
            finalOutcome = ExecuteTrialFlow();
        }
        catch (Exception)
        {
            // An unexpected managed failure aborts the trial. It is never retried, never re-entered
            // and never surfaced with its stack trace: the cleanup below still runs and the caller
            // receives a redacted refusal that carries the measured cleanup state.
            finalOutcome = null;
        }
        finally
        {
            // Cleanup order is the whole safety property: invalidate the B2 boundary first (no
            // native call), close the controller handle second, release the modules last. The
            // outcome tells the truth about the final state.
            observationSession?.Dispose();
            boundaryInvalidated = observationSession is null || observationSession.ReadBoundaryInvalidated;

            var closeConfirmed = false;
            if (opened && kernel is not null && closeExport != IntPtr.Zero)
            {
                closeConfirmed = TryCloseHandle(kernel, closeExport, ref handle);
            }

            var modulesWereHeld = primaryModule != IntPtr.Zero || companionModule != IntPtr.Zero;
            var released = true;
            if (kernel is not null && modulesWereHeld)
            {
                released = ReleaseModules(kernel, ref primaryModule, ref companionModule);
            }

            finalClosed = !opened || closeConfirmed;
            // Vacuously true when nothing was loaded: the flag asserts that no native module handle
            // this trial loaded is still held, and a trial that never loaded one holds nothing.
            finalReleased = !modulesWereHeld || released;

            if (finalOutcome is not null)
            {
                if (finalOutcome.Accepted)
                {
                    if (!finalClosed)
                    {
                        finalOutcome = MotionObservationTrialOutcome.Refused(
                            MotionControllerIdentityVocabulary.CleanupUnproven,
                            "Every read passed, but the controller handle close could not be " +
                            "confirmed. The trial does not report success with an unproven cleanup; " +
                            "the isolated process containment applies.",
                            finalOutcome.OpenClassification,
                            finalOutcome.Generation,
                            boundaryInvalidated,
                            controllerHandleClosed: false,
                            nativeHandlesReleased: finalReleased,
                            finalOutcome.Readings);
                    }
                    else if (!finalReleased)
                    {
                        finalOutcome = MotionObservationTrialOutcome.Refused(
                            MotionControllerIdentityVocabulary.NativeUnloadFailed,
                            "Every read passed, but a native module handle could not be released " +
                            "deterministically. The trial does not report success with an unreleased " +
                            "handle.",
                            finalOutcome.OpenClassification,
                            finalOutcome.Generation,
                            boundaryInvalidated,
                            controllerHandleClosed: true,
                            nativeHandlesReleased: false,
                            finalOutcome.Readings);
                    }
                    else
                    {
                        finalOutcome = finalOutcome with
                        {
                            ObservationBoundaryInvalidated = boundaryInvalidated,
                            ControllerHandleClosed = true,
                            NativeHandlesReleased = finalReleased,
                        };
                    }
                }
                else
                {
                    var detail = finalOutcome.Detail;
                    if (!finalClosed)
                    {
                        detail += " The controller handle close could not be confirmed; cleanup is " +
                                  "unproven and the isolated process containment applies.";
                    }

                    if (modulesWereHeld && !released)
                    {
                        detail += " Deterministic cleanup of the loaded native handles could not be " +
                                  "fully confirmed.";
                    }

                    finalOutcome = finalOutcome with
                    {
                        Detail = detail,
                        ObservationBoundaryInvalidated = boundaryInvalidated,
                        ControllerHandleClosed = finalClosed,
                        NativeHandlesReleased = finalReleased,
                    };
                }
            }
        }

        if (finalOutcome is not null)
        {
            return finalOutcome;
        }

        return MotionObservationTrialOutcome.Refused(
            MotionObservationTrialVocabulary.TrialAborted,
            "The trial did not produce a result, so no observation is reported. The observation " +
            "boundary was invalidated, and the controller handle close and the native module release " +
            "were attempted; the cleanup flags carry the measured state. Nothing was retried and no " +
            "value was fabricated.",
            openClassification,
            observationSession?.CurrentGeneration ?? 0,
            boundaryInvalidated,
            finalClosed,
            finalReleased);
    }

    /// <summary>Closes the controller handle through the resolved close export; true only when closed and confirmed.</summary>
    private static bool TryCloseHandle(IControllerKernel kernel, IntPtr closeExport, ref IntPtr handle)
    {
        try
        {
            var result = kernel.InvokeClose(closeExport, ref handle);
            return result == MotionControllerApi.ResultSuccess && handle == IntPtr.Zero;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool ReleaseModules(IControllerKernel kernel, ref IntPtr primaryModule, ref IntPtr companionModule)
    {
        var ok = true;
        if (primaryModule != IntPtr.Zero)
        {
            ok = TryReleaseModule(kernel, primaryModule) && ok;
            primaryModule = IntPtr.Zero;
        }

        if (companionModule != IntPtr.Zero)
        {
            ok = TryReleaseModule(kernel, companionModule) && ok;
            companionModule = IntPtr.Zero;
        }

        return ok;
    }

    /// <summary>Releases one module handle; a throwing kernel is reported as an unproven release.</summary>
    private static bool TryReleaseModule(IControllerKernel kernel, IntPtr module)
    {
        try
        {
            return kernel.ReleaseModule(module);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool ProveLoadedModulePath(IControllerKernel kernel, IntPtr module, string approvedPath)
    {
        var loadedPath = kernel.GetLoadedModulePath(module);
        if (loadedPath is null)
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(loadedPath),
            approvedPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] ToAsciiBuffer(string value)
    {
        var bytes = new byte[System.Text.Encoding.ASCII.GetByteCount(value) + 1];
        _ = System.Text.Encoding.ASCII.GetBytes(value, 0, value.Length, bytes, 0);
        return bytes;
    }
}
