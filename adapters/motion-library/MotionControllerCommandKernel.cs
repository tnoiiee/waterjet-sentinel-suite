using System.Runtime.InteropServices;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The shared, restricted carrier for the one approved native command-and-response export.
/// B2 uses it only through its fixed read-only observation boundary. The separate C1 Stop-only
/// composition uses the same approved export through its own closed typed plan and dispatch
/// boundary; it does not enter the B2 read kernel.
///
/// Authority. B1 identity remains six typed identity exports with no raw command. B2 issues only
/// the fixed read-only intents in <see cref="MotionObservationCommandPlan"/>. C1 Stop accepts
/// only its closed A/B axis domain and maps it to the two already-owned Stop templates. Both
/// command-capable paths are typed, internal, fixed-plan boundaries: no public member, route,
/// contract, UI, input string, reflection, dynamic invocation, unmanaged callback or second loader
/// can supply arbitrary command text. The vendor export is general-purpose, so safety comes from
/// the two separate closed plans and owners, not from trusting a caller.
///
/// Lifecycle. The B2 read kernel and C1 Stop carrier own NOTHING they could release: each holds
/// only a controller handle and export pointer already resolved by its respective lifecycle owner.
/// Their invalidation performs no native call, and each owner closes its handle and releases its
/// module only after invalidation. These separate cleanup orders are pinned by focused tests.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal static class MotionControllerCommandApi
{
    /// <summary>
    /// The single approved native command-and-response export name, shared by the separate B2
    /// fixed read boundary and C1 typed Stop boundary. The vendor declaration takes the handle,
    /// request text, response buffer and pointer-sized response length; the vendor documentation
    /// says the call issues the request to the Controller and provides the response.
    ///
    /// This constant is the only source occurrence of the native export name, and source-boundary
    /// tests pin that fact with an exact-path, exact-token exception. Neither boundary accepts
    /// caller-supplied request text.
    /// </summary>
    internal const string CommandExportName = "gclib_command";

    /// <summary>
    /// Resolves the one approved command export in an already loaded module. The kernel seam is
    /// reused; no second loader, no static import and no handle of its own are introduced.
    /// </summary>
    internal static IntPtr ResolveCommandExport(IControllerKernel kernel, IntPtr module)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        return kernel.ResolveExport(module, CommandExportName);
    }
}

/// <summary>
/// The concrete B2 read kernel: a thin, guarded invocation of the one approved export against
/// one already-open controller handle.
///
/// It is bound once and invalidated once. It accepts a typed intent only, it validates the
/// request length before the call, it reads into a fixed-size zeroed buffer, and it refuses
/// everything it cannot prove: an unbound or closed boundary, an intent outside the closed set,
/// a non-success result, an unterminated or non-printable reply, and any reply that is not one
/// bounded numeric token.
///
/// The delegate declares exactly the installed-header signature: the handle, the request text,
/// the response buffer and its length as a pointer-sized unsigned value. It is declared for the
/// 64-bit Windows process the Owner-local run requires; there is no x86 calling-convention
/// ambiguity at this boundary. No vendor error text, result name or exception message is ever
/// surfaced: every failure becomes one canonical refusal word.
/// </summary>
internal sealed class BoundObservationReadKernel : IObservationReadKernel
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CommandExport(
        IntPtr handle,
        byte[] commandBuffer,
        byte[] responseBuffer,
        UIntPtr length);

    private readonly object _gate = new();

    private IntPtr _handle;
    private IntPtr _export;
    private bool _invalidated;

    /// <summary>
    /// Binds the kernel to one open controller handle and one resolved command export. Both are
    /// owned by the caller; this kernel never opens, closes, loads, unloads or resolves anything
    /// by itself, so it cannot extend a native lifetime.
    /// </summary>
    internal BoundObservationReadKernel(IntPtr controllerHandle, IntPtr commandExport)
    {
        _handle = controllerHandle;
        _export = commandExport;
    }

    /// <summary>True while a handle and an export pointer are both present and not invalidated.</summary>
    internal bool IsBound
    {
        get
        {
            lock (_gate)
            {
                return !_invalidated && _handle != IntPtr.Zero && _export != IntPtr.Zero;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsInvalidated
    {
        get
        {
            lock (_gate)
            {
                return _invalidated;
            }
        }
    }

    /// <inheritdoc/>
    public ObservationReadResult Issue(MotionObservationIntent intent)
    {
        lock (_gate)
        {
            if (_invalidated)
            {
                return ObservationReadResult.NotAttempted(
                    MotionObservedStateVocabulary.ObservationKernelClosed);
            }

            if (_handle == IntPtr.Zero || _export == IntPtr.Zero)
            {
                return ObservationReadResult.NotAttempted(
                    MotionObservedStateVocabulary.ObservationKernelUnbound);
            }

            if (!MotionObservationCommandPlan.TryGetTemplate(intent, out var request))
            {
                return ObservationReadResult.NotAttempted(
                    MotionObservedStateVocabulary.ObservationIntentUnmapped);
            }

            if (request.Length == 0
                || request.Length > MotionObservationCommandPlan.MaximumRequestLength)
            {
                return ObservationReadResult.NotAttempted(
                    MotionObservedStateVocabulary.ObservationIntentUnmapped);
            }

            var requestBuffer = CreateAsciiBuffer(request);
            var responseBuffer = new byte[MotionObservationCommandPlan.MaximumResponseBytes];

            int result;
            try
            {
                var export = Marshal.GetDelegateForFunctionPointer<CommandExport>(_export);
                result = export(
                    _handle,
                    requestBuffer,
                    responseBuffer,
                    (UIntPtr)responseBuffer.Length);
            }
            catch (Exception)
            {
                // No exception message, type name or vendor text leaves this boundary.
                return ObservationReadResult.Failed(
                    MotionObservedStateVocabulary.ObservationCommandFailed);
            }

            if (result != MotionControllerApi.ResultSuccess)
            {
                return ObservationReadResult.Failed(
                    MotionObservedStateVocabulary.ObservationCommandFailed);
            }

            if (!MotionObservationCommandPlan.TryExtractResponseText(
                    responseBuffer,
                    responseBuffer.Length,
                    out var rawText,
                    out var refusalCode))
            {
                return ObservationReadResult.Failed(refusalCode);
            }

            if (!MotionObservationCommandPlan.TryNormalizeValueText(
                    rawText,
                    out var valueText,
                    out refusalCode))
            {
                return ObservationReadResult.Failed(refusalCode);
            }

            return ObservationReadResult.FromValue(valueText);
        }
    }

    /// <summary>
    /// Invalidates the boundary permanently. Idempotent, never throws and performs NO native
    /// call: the controller handle and the module belong to the lifecycle owner, which closes
    /// and unloads them in that order after invalidating this kernel first.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _invalidated = true;
            _handle = IntPtr.Zero;
            _export = IntPtr.Zero;
        }
    }

    /// <summary>
    /// One NUL-terminated ASCII request buffer. The request text is a literal from the immutable
    /// plan, already length-checked, so this method never encodes caller text.
    /// </summary>
    private static byte[] CreateAsciiBuffer(string requestText)
    {
        var buffer = new byte[requestText.Length + 1];
        _ = System.Text.Encoding.ASCII.GetBytes(
            requestText,
            0,
            requestText.Length,
            buffer,
            0);
        return buffer;
    }
}
