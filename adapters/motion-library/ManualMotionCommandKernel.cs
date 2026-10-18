using System.Runtime.InteropServices;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The raw result of one native commissioning dispatch, before classification. The native result
/// number and the reply buffer are internal inputs to the owner's classifier and are never copied
/// into an outcome. No exception object is carried.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal sealed class ManualMotionNativeReply
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
    internal static ManualMotionNativeReply Completed(int nativeResult, byte[] replyBuffer) => new()
    {
        Attempted = true,
        Returned = true,
        NativeResult = nativeResult,
        ReplyBuffer = replyBuffer,
    };

    /// <summary>A dispatch that was attempted but did not return cleanly.</summary>
    internal static ManualMotionNativeReply Faulted() => new()
    {
        Attempted = true,
        Returned = false,
        NativeResult = MotionControllerApi.ResultInternalError,
    };

    /// <summary>A dispatch that was not attempted, with its canonical refusal code.</summary>
    internal static ManualMotionNativeReply NotAttempted(string refusalCode) => new()
    {
        Attempted = false,
        Returned = false,
        NativeResult = MotionControllerApi.ResultInternalError,
        RefusalCode = refusalCode,
    };
}

/// <summary>
/// The only seam through which the commissioning owner reaches the native command path. It takes one
/// typed plan command and nothing else: no string, no buffer, no handle and no template. It is
/// internal, so no Runtime, API, Kiosk, Preview or UI code can supply an implementation.
/// </summary>
internal interface IManualMotionCommandKernel : IDisposable
{
    /// <summary>True once the boundary has been invalidated. An invalidated boundary never calls native code.</summary>
    bool IsInvalidated { get; }

    /// <summary>Renders the typed command through the closed plan and makes at most one native dispatch. Never retries.</summary>
    ManualMotionNativeReply Dispatch(ManualMotionPlanCommand command);
}

/// <summary>
/// The native commissioning carrier: one guarded invocation of the already-approved command export
/// against one already-open controller handle.
///
/// Export resolution is NOT performed here: the lifecycle owner resolves the export through the
/// existing approved seam and passes the pointer in, so this file never names the native export
/// token. The carrier holds no module and unloads nothing; invalidation makes no native call and,
/// because it takes the dispatch lock, waits for an in-flight call before returning.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal sealed class BoundManualMotionCommandKernel : IManualMotionCommandKernel
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CommissioningCommandExport(
        IntPtr handle,
        byte[] commandBuffer,
        byte[] replyBuffer,
        UIntPtr length);

    private readonly object _gate = new();

    private IntPtr _handle;
    private IntPtr _export;
    private bool _invalidated;

    /// <summary>
    /// Binds the carrier to one open controller handle and one resolved command export, both owned
    /// by the lifecycle owner. A zero value yields a carrier that refuses every dispatch.
    /// </summary>
    internal BoundManualMotionCommandKernel(IntPtr controllerHandle, IntPtr commandExport)
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
    public ManualMotionNativeReply Dispatch(ManualMotionPlanCommand command)
    {
        lock (_gate)
        {
            if (_invalidated)
            {
                return ManualMotionNativeReply.NotAttempted(ManualMotionControllerCodes.BoundaryClosed);
            }

            if (_handle == IntPtr.Zero || _export == IntPtr.Zero)
            {
                return ManualMotionNativeReply.NotAttempted(ManualMotionControllerCodes.BoundaryUnavailable);
            }

            if (!ManualMotionCommandPlan.TryRender(command, out var planText, out var refusalCode))
            {
                return ManualMotionNativeReply.NotAttempted(refusalCode);
            }

            var commandBuffer = CreateAsciiBuffer(planText);
            var replyBuffer = new byte[ManualMotionCommandPlan.MaximumReplyBytes];

            try
            {
                var export = Marshal.GetDelegateForFunctionPointer<CommissioningCommandExport>(_export);
                var result = export(_handle, commandBuffer, replyBuffer, (UIntPtr)replyBuffer.Length);
                return ManualMotionNativeReply.Completed(result, replyBuffer);
            }
            catch (Exception)
            {
                // No exception type, message or vendor text leaves this boundary.
                return ManualMotionNativeReply.Faulted();
            }
        }
    }

    /// <summary>Invalidates the carrier permanently. Idempotent, never throws and performs NO native call.</summary>
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
    /// One NUL-terminated ASCII command buffer. The argument is only ever a text returned by the closed
    /// plan renderer, so this method never encodes caller text.
    /// </summary>
    private static byte[] CreateAsciiBuffer(string planText)
    {
        var buffer = new byte[planText.Length + 1];
        _ = System.Text.Encoding.ASCII.GetBytes(planText, 0, planText.Length, buffer, 0);
        return buffer;
    }
}
