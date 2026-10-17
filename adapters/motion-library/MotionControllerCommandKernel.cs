using System.Runtime.InteropServices;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The B2 private read boundary: the ONE approved native command-and-response export, resolved
/// through the already-authorized kernel seam, and the ONE bound kernel that can invoke it.
///
/// Authority. The B1 identity surface is unchanged and still resolves exactly six typed identity
/// exports with no raw command of any kind. The separately authorized B2 read boundary adds
/// exactly one further export for exactly one purpose: issuing one of the sixteen fixed
/// read-only request literals of <see cref="MotionObservationCommandPlan"/> and returning its
/// bounded response. The export is a general command-and-response entry point in the vendor
/// library, so the boundary is made safe by construction rather than by trusting the caller:
///
///   - the only public entry is a typed intent, and the request text is looked up from an
///     immutable literal table inside this assembly;
///   - this class is internal, has an internal constructor and is never exposed by a public
///     member, a route, a contract, a UI or an outcome;
///   - the request is length-bounded before the call, the response buffer is a fixed size, and
///     the reply must be one terminated printable numeric token;
///   - no write, stop, jog, brake, home, define, move, auto, program-array or output-mutation
///     mnemonic exists in the literal table, so no such request can be formed at all;
///   - no reflection, no dynamic invocation, no unmanaged callback and no second loader exist on
///     this path.
///
/// Lifecycle. This kernel owns NOTHING it could release: it holds the controller handle the
/// owner already opened and the export pointer the owner already resolved, and its
/// <see cref="Dispose"/> performs no native call. The owner closes the controller handle and
/// releases the module afterwards; because this kernel refuses every later call once
/// invalidated, an ordering mistake cannot turn into a post-close native call. That is the whole
/// cleanup truth of the B2 layer, and it is stated as such in the evidence baseline.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal static class MotionControllerCommandApi
{
    /// <summary>
    /// The single approved B2 native export name, taken verbatim from the installed-header
    /// evidence. The vendor declaration takes the handle, the request text, the response buffer
    /// and the response length as a pointer-sized unsigned value, and the vendor documentation
    /// states that the call issues the request to the controller and provides the response.
    ///
    /// This constant is the only occurrence of the name in the repository, and the
    /// source-boundary tests pin that fact with an exact-path, exact-token exception.
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
