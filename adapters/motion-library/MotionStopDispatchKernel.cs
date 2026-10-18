using System.Runtime.InteropServices;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The raw result of one native Stop dispatch attempt, before classification.
///
/// It carries the native result number, the fixed reply buffer and whether the call was attempted
/// at all. The native result number is an internal input to the classifier and is never copied
/// into an outcome. No exception object is carried.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopNativeReply
{
    /// <summary>True when a native call was made.</summary>
    internal bool Attempted { get; init; }

    /// <summary>True when the native call returned without throwing.</summary>
    internal bool Returned { get; init; }

    /// <summary>The native result number, meaningful only when <see cref="Returned"/> is true.</summary>
    internal int NativeResult { get; init; }

    /// <summary>The fixed-size reply buffer of the dispatch. Empty when no call was made.</summary>
    internal byte[] ReplyBuffer { get; init; } = Array.Empty<byte>();

    /// <summary>The canonical refusal code when no call was made. Empty otherwise.</summary>
    internal string RefusalCode { get; init; } = string.Empty;

    /// <summary>A dispatch that returned, carrying its native result and reply buffer.</summary>
    internal static MotionStopNativeReply Completed(int nativeResult, byte[] replyBuffer) => new()
    {
        Attempted = true,
        Returned = true,
        NativeResult = nativeResult,
        ReplyBuffer = replyBuffer,
        RefusalCode = string.Empty,
    };

    /// <summary>A dispatch that was attempted but did not return cleanly.</summary>
    internal static MotionStopNativeReply Faulted() => new()
    {
        Attempted = true,
        Returned = false,
        NativeResult = MotionControllerApi.ResultInternalError,
        ReplyBuffer = Array.Empty<byte>(),
        RefusalCode = string.Empty,
    };

    /// <summary>A dispatch that was not attempted, with its canonical refusal code.</summary>
    internal static MotionStopNativeReply NotAttempted(string refusalCode) => new()
    {
        Attempted = false,
        Returned = false,
        NativeResult = MotionControllerApi.ResultInternalError,
        ReplyBuffer = Array.Empty<byte>(),
        RefusalCode = refusalCode,
    };
}

/// <summary>
/// The only seam through which the Stop session reaches the native layer. It takes the closed axis
/// domain and nothing else: no string, no buffer, no handle and no template. It is internal, so no
/// application, route, contract, UI or test fixture outside this assembly can supply an
/// implementation to production code.
/// </summary>
internal interface IStopDispatchKernel : IDisposable
{
    /// <summary>True once the boundary has been invalidated. An invalidated boundary never calls native code.</summary>
    bool IsInvalidated { get; }

    /// <summary>Makes at most one native dispatch for one axis. Never retries.</summary>
    MotionStopNativeReply Dispatch(MotionStopAxis axis);
}

/// <summary>
/// The native Stop carrier: one guarded invocation of the already approved command-and-response
/// export, against one already-open controller handle.
///
/// Export resolution is NOT performed here. The lifecycle owner resolves the export through the
/// existing approved seam (<see cref="MotionControllerCommandApi.ResolveCommandExport"/>) and passes
/// the pointer in. This file therefore never names the native export token; its sole source
/// occurrence remains in <c>MotionControllerCommandKernel.cs</c> for both separate typed boundaries.
///
/// Invalidation takes the same lock as a dispatch. A call that is already in flight therefore
/// completes before invalidation returns, and no call can begin after invalidation. Invalidation
/// itself never makes a native call. This carrier holds no module and unloads nothing.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class BoundStopDispatchKernel : IStopDispatchKernel
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int StopCommandExport(
        IntPtr handle,
        byte[] commandBuffer,
        byte[] replyBuffer,
        UIntPtr length);

    private readonly object _gate = new();

    private IntPtr _handle;
    private IntPtr _export;
    private bool _invalidated;

    /// <summary>
    /// Binds the carrier to one open controller handle and one resolved command export. Both are
    /// owned by the lifecycle owner. A zero value yields a carrier that refuses every dispatch.
    /// </summary>
    internal BoundStopDispatchKernel(IntPtr controllerHandle, IntPtr commandExport)
    {
        _handle = controllerHandle;
        _export = commandExport;
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
    public MotionStopNativeReply Dispatch(MotionStopAxis axis)
    {
        lock (_gate)
        {
            if (_invalidated)
            {
                return MotionStopNativeReply.NotAttempted(MotionStopOutcomeCodes.BoundaryClosed);
            }

            if (_handle == IntPtr.Zero || _export == IntPtr.Zero)
            {
                return MotionStopNativeReply.NotAttempted(MotionStopOutcomeCodes.BoundaryUnavailable);
            }

            if (!MotionStopCommandPlan.TryGetTemplate(axis, out var template))
            {
                return MotionStopNativeReply.NotAttempted(MotionStopOutcomeCodes.AxisOutOfDomain);
            }

            var commandBuffer = CreateAsciiBuffer(template);
            var replyBuffer = new byte[MotionStopCommandPlan.MaximumReplyBytes];

            try
            {
                var export = Marshal.GetDelegateForFunctionPointer<StopCommandExport>(_export);
                var result = export(_handle, commandBuffer, replyBuffer, (UIntPtr)replyBuffer.Length);
                return MotionStopNativeReply.Completed(result, replyBuffer);
            }
            catch (Exception)
            {
                // No exception type, message or vendor text leaves this boundary.
                return MotionStopNativeReply.Faulted();
            }
        }
    }

    /// <summary>
    /// Invalidates the carrier permanently. Idempotent, never throws and performs NO native call.
    /// Taking the dispatch lock means an in-flight call completes before this returns.
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
    /// One NUL-terminated ASCII command buffer. The argument is only ever a template returned by the
    /// closed plan (see <see cref="MotionStopCommandPlan.TryGetTemplate"/>), so this method never
    /// encodes caller text.
    /// </summary>
    private static byte[] CreateAsciiBuffer(string planTemplate)
    {
        var buffer = new byte[planTemplate.Length + 1];
        _ = System.Text.Encoding.ASCII.GetBytes(planTemplate, 0, planTemplate.Length, buffer, 0);
        return buffer;
    }
}
