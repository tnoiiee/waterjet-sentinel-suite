namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The canonical, redacted outcome vocabulary of the C1 Stop path. Codes only: no vendor text, no
/// native result name, no endpoint, no serial and no exception message ever leaves this boundary.
///
/// There is deliberately NO confirmed-stop code. A dispatch that completed under the carrier
/// contract is reported as <see cref="Dispatched"/>. Confirmation requires a later observed-state
/// contract that is not part of this software foundation.
/// </summary>
internal static class MotionStopOutcomeCodes
{
    /// <summary>The single native dispatch completed, returned success, and the reply carried no refusal marker and no reply text.</summary>
    internal const string Dispatched = "STOP_DISPATCHED";

    /// <summary>The reply carried the controller refusal marker. The command was not accepted.</summary>
    internal const string RejectedByController = "STOP_REJECTED_BY_CONTROLLER";

    /// <summary>A reply arrived that is not an established acknowledgement. Dispatch occurred, but no success is claimed.</summary>
    internal const string ReplyUnclassified = "STOP_REPLY_UNCLASSIFIED";

    /// <summary>The native dispatch returned a non-success result, or the call did not return cleanly.</summary>
    internal const string DispatchFailed = "STOP_DISPATCH_FAILED";

    /// <summary>The native call returned after the session generation had advanced. Its result is not accepted as current.</summary>
    internal const string ResultStaleRejected = "STOP_RESULT_STALE_REJECTED";

    /// <summary>The requested axis is outside the closed A/B domain. Nothing was dispatched.</summary>
    internal const string AxisOutOfDomain = "STOP_AXIS_OUT_OF_DOMAIN";

    /// <summary>Another Stop operation is pending or in flight in this session. Nothing was dispatched.</summary>
    internal const string OperationBusy = "STOP_OPERATION_BUSY";

    /// <summary>The operation was already dispatched and cannot be dispatched again. Nothing was dispatched.</summary>
    internal const string OperationReplayed = "STOP_OPERATION_REPLAYED";

    /// <summary>The operation was not accepted by Begin. Nothing was dispatched.</summary>
    internal const string OperationNotAccepted = "STOP_OPERATION_NOT_ACCEPTED";

    /// <summary>The operation belongs to another session. Nothing was dispatched.</summary>
    internal const string OperationForeign = "STOP_OPERATION_FOREIGN";

    /// <summary>The operation was issued for an earlier session generation. Nothing was dispatched.</summary>
    internal const string GenerationStale = "STOP_GENERATION_STALE";

    /// <summary>The session is closed or invalidated. Nothing was dispatched.</summary>
    internal const string SessionClosed = "STOP_SESSION_CLOSED";

    /// <summary>The dispatch boundary has no bound handle or export. Nothing was dispatched.</summary>
    internal const string BoundaryUnavailable = "STOP_BOUNDARY_UNAVAILABLE";

    /// <summary>The dispatch boundary was invalidated before the call. Nothing was dispatched.</summary>
    internal const string BoundaryClosed = "STOP_BOUNDARY_CLOSED";

    /// <summary>The fixed redacted detail for one code. Unknown codes map to the generic refusal text.</summary>
    internal static string DetailFor(string code) => code switch
    {
        Dispatched =>
            "One native Stop dispatch completed and returned no refusal. Dispatch is NOT confirmed stop: no observed state was read and no stop is claimed.",
        RejectedByController =>
            "The controller reply carried a refusal marker, so the Stop was not accepted. Nothing is retried.",
        ReplyUnclassified =>
            "A native dispatch occurred but the reply is not an established acknowledgement. No success is claimed and nothing is retried.",
        DispatchFailed =>
            "The native Stop dispatch did not return success. The redacted result is reported; nothing is retried.",
        ResultStaleRejected =>
            "The native call returned after the session generation advanced, so its result is rejected as stale. No success is claimed.",
        AxisOutOfDomain =>
            "The requested axis is outside the closed A/B domain. Nothing was dispatched.",
        OperationBusy =>
            "Another Stop operation is pending or in flight in this session. The request was refused, not queued.",
        OperationReplayed =>
            "The operation was already dispatched and cannot be replayed. Nothing was dispatched.",
        OperationNotAccepted =>
            "The operation was not accepted. Nothing was dispatched.",
        OperationForeign =>
            "The operation does not belong to this session. Nothing was dispatched.",
        GenerationStale =>
            "The operation was issued for an earlier session generation. Nothing was dispatched.",
        SessionClosed =>
            "The Stop session is closed. Nothing was dispatched and the session never reopens.",
        BoundaryUnavailable =>
            "The dispatch boundary is not bound to a controller handle and export. Nothing was dispatched.",
        BoundaryClosed =>
            "The dispatch boundary was invalidated before the call. Nothing was dispatched.",
        _ =>
            "The Stop request was refused. Nothing is retried.",
    };
}

/// <summary>
/// The classification of one reply buffer from a single native Stop dispatch.
/// </summary>
internal enum MotionStopReplyClass
{
    /// <summary>The reply is empty: the first byte is the terminator. This is the absence of a refusal, not a parsed acknowledgement.</summary>
    NoReply = 0,

    /// <summary>The reply carries the controller refusal marker.</summary>
    ControllerRejected = 1,

    /// <summary>The reply is unterminated, non-printable, or non-empty text that is not an established acknowledgement.</summary>
    Unclassified = 2,
}

/// <summary>
/// The separate Stop acknowledgement classifier.
///
/// It is NOT the B2 numeric observation parser. The B2 parser accepts one bounded numeric token and
/// refuses everything else, which is the correct behaviour for a read. A Stop reply has no
/// documented numeric or text form, so this classifier never interprets a reply as a value. It
/// answers only three questions: was the reply terminated, does it carry the refusal marker, and
/// is it empty. Anything else is reported as unclassified, never as success.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal static class MotionStopAcknowledgement
{
    private const byte RefusalMarker = 0x3F;

    /// <summary>Classifies one reply buffer. The buffer is the fixed-size reply buffer of one dispatch.</summary>
    internal static MotionStopReplyClass ClassifyReply(byte[] replyBuffer)
    {
        ArgumentNullException.ThrowIfNull(replyBuffer);

        var terminator = Array.IndexOf(replyBuffer, (byte)0);
        if (terminator < 0)
        {
            return MotionStopReplyClass.Unclassified;
        }

        if (terminator == 0)
        {
            return MotionStopReplyClass.NoReply;
        }

        var hasRefusalMarker = false;
        for (var index = 0; index < terminator; index++)
        {
            var value = replyBuffer[index];
            if (value == RefusalMarker)
            {
                hasRefusalMarker = true;
                continue;
            }

            var printable = (value >= 0x20 && value <= 0x7E) || value == 0x09 || value == 0x0D;
            if (!printable)
            {
                return MotionStopReplyClass.Unclassified;
            }
        }

        return hasRefusalMarker
            ? MotionStopReplyClass.ControllerRejected
            : MotionStopReplyClass.Unclassified;
    }

    /// <summary>
    /// Maps one native dispatch result to one canonical outcome code. A non-success result, a fault
    /// or a result that was not attempted is never mapped to <see cref="MotionStopOutcomeCodes.Dispatched"/>.
    /// </summary>
    internal static string ClassifyOutcome(MotionStopNativeReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);

        if (!reply.Attempted)
        {
            return string.IsNullOrEmpty(reply.RefusalCode)
                ? MotionStopOutcomeCodes.BoundaryUnavailable
                : reply.RefusalCode;
        }

        if (!reply.Returned || reply.NativeResult != MotionControllerApi.ResultSuccess)
        {
            return MotionStopOutcomeCodes.DispatchFailed;
        }

        return ClassifyReply(reply.ReplyBuffer) switch
        {
            MotionStopReplyClass.NoReply => MotionStopOutcomeCodes.Dispatched,
            MotionStopReplyClass.ControllerRejected => MotionStopOutcomeCodes.RejectedByController,
            _ => MotionStopOutcomeCodes.ReplyUnclassified,
        };
    }
}
