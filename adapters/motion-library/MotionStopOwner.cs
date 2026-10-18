namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The truthful cleanup state of one Stop owner after close.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopCloseOutcome
{
    /// <summary>True when the Stop session and its dispatch boundary were invalidated.</summary>
    internal bool SessionInvalidated { get; init; }

    /// <summary>True when no controller handle remains open for this owner.</summary>
    internal bool HandleClosed { get; init; }

    /// <summary>True when the native module this owner held was released, or none was held.</summary>
    internal bool ModuleReleased { get; init; }
}

/// <summary>
/// The single controller owner of a C1 Stop operation. It holds one Stop session, one dispatch
/// boundary, one controller handle and the primary module; it alone closes that handle and
/// releases that primary module. The isolated trial releases the approved companion module only
/// after this owner has completed its close sequence.
///
/// Close order is the safety property:
///   1. invalidate the Stop session and its dispatch boundary (no native call);
///   2. close the controller handle (only after invalidation);
///   3. release the native module (only after the handle close attempt).
/// Close is idempotent, and the outcome is recorded once and returned unchanged on later calls.
/// Module release is attempted even when the handle close fails, so a failed close never keeps a
/// module loaded.
///
/// This type performs no open, connection or discovery. It is handed an already-open handle by
/// the isolated Owner-invoked trial composition; focused tests use fake kernels. The B2 observation
/// path does not construct or invoke this C1 Stop owner.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopOwner
{
    private readonly object _gate = new();
    private readonly IControllerKernel _controllerKernel;
    private readonly MotionStopSession _session;
    private readonly IntPtr _module;
    private readonly IntPtr _closeExport;

    private IntPtr _handle;
    private MotionStopCloseOutcome? _closeOutcome;

    internal MotionStopOwner(
        IControllerKernel controllerKernel,
        IStopDispatchKernel dispatchKernel,
        IntPtr module,
        IntPtr controllerHandle,
        IntPtr closeExport)
    {
        ArgumentNullException.ThrowIfNull(controllerKernel);
        ArgumentNullException.ThrowIfNull(dispatchKernel);

        _controllerKernel = controllerKernel;
        _session = new MotionStopSession(dispatchKernel);
        _module = module;
        _handle = controllerHandle;
        _closeExport = closeExport;
    }

    /// <summary>The Stop session owned by this owner.</summary>
    internal MotionStopSession Session => _session;

    /// <summary>
    /// Builds an owner over one already-open handle, resolving the approved command export through
    /// the existing kernel seam. Returns null, with nothing resolved for dispatch, when the command
    /// or close export is missing. The caller must then release the module itself.
    /// </summary>
    internal static MotionStopOwner? Attach(IControllerKernel controllerKernel, IntPtr module, IntPtr controllerHandle)
    {
        ArgumentNullException.ThrowIfNull(controllerKernel);

        var commandExport = MotionControllerCommandApi.ResolveCommandExport(controllerKernel, module);
        var closeExport = controllerKernel.ResolveExport(module, MotionControllerApi.CloseExportName);
        if (commandExport == IntPtr.Zero || closeExport == IntPtr.Zero)
        {
            return null;
        }

        return new MotionStopOwner(
            controllerKernel,
            new BoundStopDispatchKernel(controllerHandle, commandExport),
            module,
            controllerHandle,
            closeExport);
    }

    /// <summary>
    /// Closes the owner in the required order. Idempotent: the first outcome is kept and returned on
    /// every later call, and no second close or release is attempted.
    /// </summary>
    internal MotionStopCloseOutcome Close()
    {
        lock (_gate)
        {
            if (_closeOutcome is not null)
            {
                return _closeOutcome;
            }

            // 1. Invalidate first. This makes no native call and waits for any in-flight dispatch.
            _session.Invalidate();

            // 2. Close the handle only after invalidation.
            var handleClosed = _handle == IntPtr.Zero;
            if (!handleClosed && _closeExport != IntPtr.Zero)
            {
                handleClosed = TryCloseHandle();
            }

            // 3. Release the module last, even when the handle close failed.
            var moduleReleased = _module == IntPtr.Zero || TryReleaseModule();

            var outcome = new MotionStopCloseOutcome
            {
                SessionInvalidated = _session.IsClosed,
                HandleClosed = handleClosed,
                ModuleReleased = moduleReleased,
            };

            _closeOutcome = outcome;
            return outcome;
        }
    }

    private bool TryCloseHandle()
    {
        try
        {
            var result = _controllerKernel.InvokeClose(_closeExport, ref _handle);
            return result == MotionControllerApi.ResultSuccess && _handle == IntPtr.Zero;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool TryReleaseModule()
    {
        try
        {
            return _controllerKernel.ReleaseModule(_module);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
