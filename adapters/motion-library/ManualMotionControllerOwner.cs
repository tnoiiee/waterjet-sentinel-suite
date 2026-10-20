namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The single, long-lived controller owner of one Owner-local WSB1 Manual Motion Commissioning
/// session.
///
/// Lifecycle (one owner, one open, never reopened):
///   1. Gate A proves the approved native modules first (identity, architecture, loaded path and
///      version), exactly as the existing probe does;
///   2. the companion and the primary module load through the restricted loader, and each loaded
///      path is proven against the approved path;
///   3. only the approved direct-mode, open and close exports plus the already-approved command
///      export are resolved (the command export through the existing seam);
///   4. direct mode is selected once, before the single open with the validated address only;
///   5. three boundaries are bound to the one handle: the existing typed B2 read kernel, the existing
///      typed A/B Stop session and the typed commissioning command carrier;
///   6. close invalidates every boundary first, then closes the handle, then releases the primary and
///      the companion module. Nothing is closed or unloaded after a failed invalidation.
///
/// Concurrency: one controller-wide lock serializes EVERY native call, so reads, commissioning
/// commands and Stop never overlap on the handle. A result that returns after the owner generation
/// advanced is rejected as stale. There is no retry, no reconnect, no replay, no discovery, no
/// fallback endpoint and no raw command surface: every request is a closed typed value.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal sealed class ManualMotionControllerOwner
{
    private readonly object _controllerGate = new();
    private readonly MotionLibraryProbeConfiguration _nativeConfiguration;
    private readonly MotionControllerEndpointConfiguration _endpointConfiguration;
    private readonly Func<MotionLibraryProbeOutcome> _gateAProbe;
    private readonly Func<IControllerKernel> _controllerKernelFactory;
    private readonly Func<IntPtr, IntPtr, IObservationReadKernel> _readKernelFactory;
    private readonly Func<IntPtr, IntPtr, IStopDispatchKernel> _stopKernelFactory;
    private readonly Func<IntPtr, IntPtr, IManualMotionCommandKernel> _commandKernelFactory;

    private IControllerKernel? _kernel;
    private IntPtr _companionModule;
    private IntPtr _primaryModule;
    private IntPtr _handle;
    private IntPtr _closeExport;
    private IObservationReadKernel? _readKernel;
    private MotionStopSession? _stopSession;
    private IManualMotionCommandKernel? _commandKernel;
    private bool _openAttempted;
    private bool _open;
    private bool _untrackedModuleLoadPossible;
    private long _generation;
    private ManualMotionCloseOutcome? _closeOutcome;

    /// <summary>Fake-kernel seam for focused tests; production composition uses <see cref="CreateProduction"/>.</summary>
    internal ManualMotionControllerOwner(
        MotionLibraryProbeConfiguration nativeConfiguration,
        MotionControllerEndpointConfiguration endpointConfiguration,
        Func<MotionLibraryProbeOutcome> gateAProbe,
        Func<IControllerKernel> controllerKernelFactory,
        Func<IntPtr, IntPtr, IObservationReadKernel> readKernelFactory,
        Func<IntPtr, IntPtr, IStopDispatchKernel> stopKernelFactory,
        Func<IntPtr, IntPtr, IManualMotionCommandKernel> commandKernelFactory)
    {
        _nativeConfiguration = nativeConfiguration ?? throw new ArgumentNullException(nameof(nativeConfiguration));
        _endpointConfiguration = endpointConfiguration ?? throw new ArgumentNullException(nameof(endpointConfiguration));
        _gateAProbe = gateAProbe ?? throw new ArgumentNullException(nameof(gateAProbe));
        _controllerKernelFactory = controllerKernelFactory ?? throw new ArgumentNullException(nameof(controllerKernelFactory));
        _readKernelFactory = readKernelFactory ?? throw new ArgumentNullException(nameof(readKernelFactory));
        _stopKernelFactory = stopKernelFactory ?? throw new ArgumentNullException(nameof(stopKernelFactory));
        _commandKernelFactory = commandKernelFactory ?? throw new ArgumentNullException(nameof(commandKernelFactory));
    }

    /// <summary>The production composition: the restricted loader, the real Gate A probe and the three bound native boundaries.</summary>
    internal static ManualMotionControllerOwner CreateProduction(
        MotionLibraryProbeConfiguration nativeConfiguration,
        MotionControllerEndpointConfiguration endpointConfiguration)
    {
        ArgumentNullException.ThrowIfNull(nativeConfiguration);
        ArgumentNullException.ThrowIfNull(endpointConfiguration);

        return new ManualMotionControllerOwner(
            nativeConfiguration,
            endpointConfiguration,
            () => new MotionLibraryProbeRunner(nativeConfiguration).Probe(),
            () => new WindowsControllerKernel(),
            static (handle, export) => new BoundObservationReadKernel(handle, export),
            static (handle, export) => new BoundStopDispatchKernel(handle, export),
            static (handle, export) => new BoundManualMotionCommandKernel(handle, export));
    }

    /// <summary>The owner generation. It is 0 before the open, 1 while open, and advances on close.</summary>
    internal long Generation
    {
        get
        {
            lock (_controllerGate)
            {
                return _generation;
            }
        }
    }

    /// <summary>Opens the controller once. A second call is refused and nothing reconnects.</summary>
    internal ManualMotionOpenOutcome Open()
    {
        lock (_controllerGate)
        {
            if (_openAttempted)
            {
                return ManualMotionOpenOutcome.Refused(
                    ManualMotionControllerCodes.OpenAlreadyPerformed,
                    string.Empty,
                    _generation);
            }

            _openAttempted = true;

            MotionLibraryProbeOutcome? gateA;
            try
            {
                gateA = _gateAProbe();
            }
            catch (Exception)
            {
                gateA = null;
            }

            if (gateA is null || !gateA.Accepted || !gateA.NativeHandlesReleased)
            {
                return RefuseOpen(ManualMotionControllerCodes.NativeProofRefused, SafeProbeCode(gateA?.Code));
            }

            IControllerKernel kernel;
            try
            {
                kernel = _controllerKernelFactory();
            }
            catch (Exception)
            {
                return RefuseOpen(ManualMotionControllerCodes.NativeProofRefused, MotionLibraryProbeVocabulary.NativeLoadFailed);
            }

            _kernel = kernel;

            // Companion first, then primary: the existing restricted lifecycle order.
            var companionRefusal = LoadApproved(kernel, _nativeConfiguration.CompanionModulePath, ref _companionModule);
            if (companionRefusal.Length > 0)
            {
                return RefuseOpen(ManualMotionControllerCodes.NativeProofRefused, companionRefusal);
            }

            var primaryRefusal = LoadApproved(kernel, _nativeConfiguration.PrimaryModulePath, ref _primaryModule);
            if (primaryRefusal.Length > 0)
            {
                return RefuseOpen(ManualMotionControllerCodes.NativeProofRefused, primaryRefusal);
            }

            IntPtr forceDirectExport;
            IntPtr openExport;
            IntPtr commandExport;
            try
            {
                forceDirectExport = kernel.ResolveExport(_primaryModule, MotionControllerApi.ForceDirectModeExportName);
                openExport = kernel.ResolveExport(_primaryModule, MotionControllerApi.OpenExportName);
                _closeExport = kernel.ResolveExport(_primaryModule, MotionControllerApi.CloseExportName);
                commandExport = MotionControllerCommandApi.ResolveCommandExport(kernel, _primaryModule);
            }
            catch (Exception)
            {
                return RefuseOpen(ManualMotionControllerCodes.NativeProofRefused, MotionLibraryProbeVocabulary.NativeExportNotFound);
            }

            if (forceDirectExport == IntPtr.Zero
                || openExport == IntPtr.Zero
                || _closeExport == IntPtr.Zero
                || commandExport == IntPtr.Zero)
            {
                return RefuseOpen(ManualMotionControllerCodes.NativeProofRefused, MotionLibraryProbeVocabulary.NativeExportNotFound);
            }

            // Direct/no-gcaps mode is process-wide and is selected exactly once, before the one open.
            try
            {
                kernel.InvokeForceDirectMode(forceDirectExport, directOnly: 0);
            }
            catch (Exception)
            {
                return RefuseOpen(
                    MotionControllerIdentityVocabulary.DirectModeNotEnforced,
                    MotionControllerIdentityVocabulary.DirectModeNotEnforced);
            }

            var address = MotionControllerApi.BuildOpenAddress(_endpointConfiguration);
            var addressBuffer = ToAddressBuffer(address);
            int openResult;
            try
            {
                openResult = kernel.InvokeOpen(openExport, addressBuffer, out _handle);
            }
            catch (Exception)
            {
                return RefuseOpen(
                    MotionControllerIdentityVocabulary.ConnectionFailed,
                    MotionControllerIdentityVocabulary.ConnectionFailed);
            }

            if (openResult != MotionControllerApi.ResultSuccess)
            {
                var classification = MotionControllerApi.ClassifyOpenRefusal(openResult);
                return RefuseOpen(classification, classification);
            }

            if (_handle == IntPtr.Zero)
            {
                return RefuseOpen(
                    MotionControllerIdentityVocabulary.ConnectionHandleInvalid,
                    MotionControllerIdentityVocabulary.ConnectionHandleInvalid);
            }

            try
            {
                _readKernel = _readKernelFactory(_handle, commandExport);
                _stopSession = new MotionStopSession(_stopKernelFactory(_handle, commandExport));
                _commandKernel = _commandKernelFactory(_handle, commandExport);
            }
            catch (Exception)
            {
                return RefuseOpen(
                    ManualMotionControllerCodes.BoundaryUnavailable,
                    ManualMotionControllerCodes.BoundaryUnavailable);
            }

            _open = true;
            _generation = 1;
            return ManualMotionOpenOutcome.Accepted(_generation);
        }
    }

    /// <summary>Dispatches one typed plan command with exactly one native call. Never retried.</summary>
    internal ManualMotionDispatchResult Dispatch(ManualMotionPlanCommand command)
    {
        lock (_controllerGate)
        {
            var commandKernel = _commandKernel;
            if (!_open || commandKernel is null)
            {
                return new ManualMotionDispatchResult(false, false, false, ManualMotionControllerCodes.NotOpen, _generation);
            }

            var generation = _generation;
            ManualMotionNativeReply reply;
            try
            {
                reply = commandKernel.Dispatch(command);
            }
            catch (Exception)
            {
                reply = ManualMotionNativeReply.Faulted();
            }

            if (generation != _generation)
            {
                return new ManualMotionDispatchResult(false, false, reply.Attempted, ManualMotionControllerCodes.ResultStaleRejected, generation);
            }

            return Classify(reply, generation);
        }
    }

    /// <summary>
    /// Dispatches the existing typed Stop for one axis through the existing Stop session: one Stop,
    /// at most one native call, no retry. A dispatched Stop is never a confirmed stop.
    /// </summary>
    internal ManualMotionDispatchResult Stop(ManualMotionPlanAxis axis)
    {
        lock (_controllerGate)
        {
            var stopSession = _stopSession;
            if (!_open || stopSession is null)
            {
                return new ManualMotionDispatchResult(false, false, false, ManualMotionControllerCodes.NotOpen, _generation);
            }

            MotionStopAxis stopAxis;
            switch (axis)
            {
                case ManualMotionPlanAxis.A:
                    stopAxis = MotionStopAxis.A;
                    break;
                case ManualMotionPlanAxis.B:
                    stopAxis = MotionStopAxis.B;
                    break;
                default:
                    return new ManualMotionDispatchResult(false, false, false, ManualMotionControllerCodes.AxisOutOfDomain, _generation);
            }

            var generation = _generation;
            MotionStopOutcome outcome;
            try
            {
                outcome = stopSession.Stop(stopAxis);
            }
            catch (Exception)
            {
                return new ManualMotionDispatchResult(false, true, true, ManualMotionControllerCodes.DispatchFailed, generation);
            }

            if (generation != _generation)
            {
                return new ManualMotionDispatchResult(false, false, outcome.NativeDispatchAttempted, ManualMotionControllerCodes.ResultStaleRejected, generation);
            }

            if (outcome.StopDispatched && outcome.NativeDispatchAttempted)
            {
                return new ManualMotionDispatchResult(true, false, true, outcome.Code, generation);
            }

            var fault = string.Equals(outcome.Code, MotionStopOutcomeCodes.DispatchFailed, StringComparison.Ordinal);
            return new ManualMotionDispatchResult(false, fault, outcome.NativeDispatchAttempted, outcome.Code, generation);
        }
    }

    /// <summary>Performs exactly one bounded typed read. A refused read carries no value; nothing is repaired.</summary>
    internal ManualMotionReadResult Read(ManualMotionReadTarget target)
    {
        lock (_controllerGate)
        {
            var readKernel = _readKernel;
            if (!_open || readKernel is null)
            {
                return new ManualMotionReadResult(false, false, 0m, ManualMotionControllerCodes.NotOpen, _generation);
            }

            if (!TryMapRead(target, out var intent))
            {
                return new ManualMotionReadResult(false, false, 0m, ManualMotionControllerCodes.ReadTargetOutOfDomain, _generation);
            }

            var generation = _generation;
            ObservationReadResult read;
            try
            {
                read = readKernel.Issue(intent);
            }
            catch (Exception)
            {
                return new ManualMotionReadResult(false, true, 0m, MotionObservedStateVocabulary.ObservationCommandFailed, generation);
            }

            if (generation != _generation)
            {
                return new ManualMotionReadResult(false, false, 0m, ManualMotionControllerCodes.ResultStaleRejected, generation);
            }

            if (!read.Succeeded)
            {
                var code = read.RefusalCode.Length == 0
                    ? MotionObservedStateVocabulary.ObservationCommandFailed
                    : read.RefusalCode;
                var fault = string.Equals(code, MotionObservedStateVocabulary.ObservationCommandFailed, StringComparison.Ordinal);
                return new ManualMotionReadResult(false, fault, 0m, code, generation);
            }

            if (!MotionObservationCommandPlan.TryParseValue(read.RawText, out var value))
            {
                return new ManualMotionReadResult(false, false, 0m, MotionObservedStateVocabulary.ObservationResponseInvalid, generation);
            }

            if (IsStatusTarget(target) && (value < 0m || value > 255m || value != decimal.Truncate(value)))
            {
                return new ManualMotionReadResult(false, false, 0m, ManualMotionControllerCodes.ReadValueOutOfRange, generation);
            }

            if (IsOutputTarget(target) && !MotionObservationCommandPlan.TryParseOutputState(read.RawText, out _))
            {
                return new ManualMotionReadResult(false, false, 0m, ManualMotionControllerCodes.ReadValueOutOfRange, generation);
            }

            return new ManualMotionReadResult(true, false, value, ManualMotionControllerCodes.ReadAccepted, generation);
        }
    }

    /// <summary>
    /// Invalidates every boundary, then closes the handle, then releases the primary and the companion
    /// module. Idempotent: the first outcome is kept and no second close or release is attempted.
    /// </summary>
    internal ManualMotionCloseOutcome Close()
    {
        lock (_controllerGate)
        {
            if (_closeOutcome is not null)
            {
                return _closeOutcome;
            }

            if (!_openAttempted)
            {
                _openAttempted = true;
                _closeOutcome = new ManualMotionCloseOutcome(true, true, true);
                return _closeOutcome;
            }

            _closeOutcome = CleanupLocked();
            return _closeOutcome;
        }
    }

    private ManualMotionOpenOutcome RefuseOpen(string code, string detailCode)
    {
        _closeOutcome = CleanupLocked();
        return ManualMotionOpenOutcome.Refused(code, detailCode, _generation);
    }

    private ManualMotionCloseOutcome CleanupLocked()
    {
        _open = false;
        _generation++;

        var invalidated = InvalidateBoundaries();
        bool handleClosed;
        bool primaryReleased;
        bool companionReleased;
        if (invalidated)
        {
            handleClosed = TryCloseHandle();
            primaryReleased = TryReleaseModule(ref _primaryModule);
            companionReleased = TryReleaseModule(ref _companionModule);
        }
        else
        {
            // An uninvalidated boundary must not be followed by another native lifetime operation.
            handleClosed = _handle == IntPtr.Zero;
            primaryReleased = _primaryModule == IntPtr.Zero;
            companionReleased = _companionModule == IntPtr.Zero;
        }

        return new ManualMotionCloseOutcome(
            invalidated,
            handleClosed,
            primaryReleased && companionReleased && !_untrackedModuleLoadPossible);
    }

    private bool InvalidateBoundaries()
    {
        var invalidated = true;

        try
        {
            _readKernel?.Dispose();
        }
        catch (Exception)
        {
            invalidated = false;
        }

        try
        {
            _stopSession?.Invalidate();
        }
        catch (Exception)
        {
            invalidated = false;
        }

        try
        {
            _commandKernel?.Dispose();
        }
        catch (Exception)
        {
            invalidated = false;
        }

        _readKernel = null;
        _stopSession = null;
        _commandKernel = null;
        return invalidated;
    }

    private string LoadApproved(IControllerKernel kernel, string approvedPath, ref IntPtr module)
    {
        try
        {
            _untrackedModuleLoadPossible = true;
            module = kernel.LoadApprovedModule(approvedPath);
            _untrackedModuleLoadPossible = false;
        }
        catch (Exception)
        {
            // A throwing load may still have loaded a module: never guess that it did not.
            return MotionLibraryProbeVocabulary.NativeLoadFailed;
        }

        if (module == IntPtr.Zero)
        {
            return MotionLibraryProbeVocabulary.NativeLoadFailed;
        }

        string? loadedPath;
        try
        {
            loadedPath = kernel.GetLoadedModulePath(module);
        }
        catch (Exception)
        {
            return MotionLibraryProbeVocabulary.NativeModulePathMismatch;
        }

        var proven = loadedPath is not null
            && string.Equals(Path.GetFullPath(loadedPath), approvedPath, StringComparison.OrdinalIgnoreCase);
        return proven ? string.Empty : MotionLibraryProbeVocabulary.NativeModulePathMismatch;
    }

    private bool TryCloseHandle()
    {
        if (_handle == IntPtr.Zero)
        {
            return true;
        }

        var kernel = _kernel;
        if (kernel is null || _closeExport == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var result = kernel.InvokeClose(_closeExport, ref _handle);
            return result == MotionControllerApi.ResultSuccess && _handle == IntPtr.Zero;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool TryReleaseModule(ref IntPtr module)
    {
        if (module == IntPtr.Zero)
        {
            return true;
        }

        var kernel = _kernel;
        if (kernel is null)
        {
            return false;
        }

        var handle = module;
        module = IntPtr.Zero; // one attempt only, including when the kernel throws.
        try
        {
            return kernel.ReleaseModule(handle);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ManualMotionDispatchResult Classify(ManualMotionNativeReply reply, long generation)
    {
        if (!reply.Attempted)
        {
            var refusal = reply.RefusalCode.Length == 0 ? ManualMotionControllerCodes.BoundaryUnavailable : reply.RefusalCode;
            return new ManualMotionDispatchResult(false, false, false, refusal, generation);
        }

        if (!reply.Returned || reply.NativeResult != MotionControllerApi.ResultSuccess)
        {
            return new ManualMotionDispatchResult(false, true, true, ManualMotionControllerCodes.DispatchFailed, generation);
        }

        return MotionStopAcknowledgement.ClassifyReply(reply.ReplyBuffer) switch
        {
            MotionStopReplyClass.NoReply => new ManualMotionDispatchResult(true, false, true, ManualMotionControllerCodes.Dispatched, generation),
            MotionStopReplyClass.ControllerRejected => new ManualMotionDispatchResult(false, false, true, ManualMotionControllerCodes.RejectedByController, generation),
            _ => new ManualMotionDispatchResult(false, false, true, ManualMotionControllerCodes.ReplyUnclassified, generation),
        };
    }

    private static bool TryMapRead(ManualMotionReadTarget target, out MotionObservationIntent intent)
    {
        switch (target)
        {
            case ManualMotionReadTarget.StatusA:
                intent = MotionObservationIntent.AxisStatusA;
                return true;
            case ManualMotionReadTarget.StatusB:
                intent = MotionObservationIntent.AxisStatusB;
                return true;
            case ManualMotionReadTarget.TpA:
                intent = MotionObservationIntent.CommandPositionA;
                return true;
            case ManualMotionReadTarget.TpB:
                intent = MotionObservationIntent.CommandPositionB;
                return true;
            case ManualMotionReadTarget.RpA:
                intent = MotionObservationIntent.ActualPositionA;
                return true;
            case ManualMotionReadTarget.RpB:
                intent = MotionObservationIntent.ActualPositionB;
                return true;
            case ManualMotionReadTarget.Output1:
                intent = MotionObservationIntent.DigitalOutput1;
                return true;
            case ManualMotionReadTarget.Output2:
                intent = MotionObservationIntent.DigitalOutput2;
                return true;
            default:
                intent = default;
                return false;
        }
    }

    private static bool IsStatusTarget(ManualMotionReadTarget target) =>
        target is ManualMotionReadTarget.StatusA or ManualMotionReadTarget.StatusB;

    private static bool IsOutputTarget(ManualMotionReadTarget target) =>
        target is ManualMotionReadTarget.Output1 or ManualMotionReadTarget.Output2;

    private static byte[] ToAddressBuffer(string address)
    {
        var buffer = new byte[System.Text.Encoding.ASCII.GetByteCount(address) + 1];
        _ = System.Text.Encoding.ASCII.GetBytes(address, 0, address.Length, buffer, 0);
        return buffer;
    }

    private static string SafeProbeCode(string? code) => code switch
    {
        MotionLibraryProbeVocabulary.NativeLibDirNotConfigured => MotionLibraryProbeVocabulary.NativeLibDirNotConfigured,
        MotionLibraryProbeVocabulary.NativeLibDirRejected => MotionLibraryProbeVocabulary.NativeLibDirRejected,
        MotionLibraryProbeVocabulary.NativeFileNotFound => MotionLibraryProbeVocabulary.NativeFileNotFound,
        MotionLibraryProbeVocabulary.NativeFileIdentityMismatch => MotionLibraryProbeVocabulary.NativeFileIdentityMismatch,
        MotionLibraryProbeVocabulary.NativeFileReadFailed => MotionLibraryProbeVocabulary.NativeFileReadFailed,
        MotionLibraryProbeVocabulary.NativeArchitectureMismatch => MotionLibraryProbeVocabulary.NativeArchitectureMismatch,
        MotionLibraryProbeVocabulary.NativeLoadFailed => MotionLibraryProbeVocabulary.NativeLoadFailed,
        MotionLibraryProbeVocabulary.NativeExportNotFound => MotionLibraryProbeVocabulary.NativeExportNotFound,
        MotionLibraryProbeVocabulary.NativeVersionCallFailed => MotionLibraryProbeVocabulary.NativeVersionCallFailed,
        MotionLibraryProbeVocabulary.NativeVersionMismatch => MotionLibraryProbeVocabulary.NativeVersionMismatch,
        MotionLibraryProbeVocabulary.NativeModulePathMismatch => MotionLibraryProbeVocabulary.NativeModulePathMismatch,
        MotionLibraryProbeVocabulary.NativeUnloadFailed => MotionLibraryProbeVocabulary.NativeUnloadFailed,
        MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform => MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform,
        _ => ManualMotionControllerCodes.NativeProofRefused,
    };
}
