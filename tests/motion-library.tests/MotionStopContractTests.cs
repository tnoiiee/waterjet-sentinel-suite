using System.Reflection;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Focused coverage of the C1 Stop-only software foundation, WITHOUT any native module and WITHOUT
/// any controller. A recording dispatch kernel and a recording controller kernel drive every
/// property: the closed A/B domain, the exact templates, one dispatch per accepted operation,
/// deterministic duplicate and replay rejection, generation fencing, no retry, redacted outcomes,
/// the invalidate-then-close-then-unload order, and the separation of dispatch from confirmation.
///
/// Every reply in this file is synthetic. No endpoint, serial, module path or hash is real.
/// </summary>
public sealed class MotionStopContractTests
{
    // ------------------------------------------------------------------ expected lifecycle event sequences
    // Expected values only. Each field is private, exact in order, and is read only by Assert.Equal.
    // None is passed to code under test, mutated, or exposed outside this test type.

    private static readonly string[] ExpectedSingleDispatchEvents = ["dispatch"];

    private static readonly string[] ExpectedDispatchThenInvalidationEvents = ["dispatch", "dispose"];

    private static readonly string[] ExpectedDoubleInvalidationEvents = ["dispose", "dispose"];

    private static readonly string[] ExpectedInvalidateCloseReleaseEvents = ["dispose", "close", "release"];

    private static readonly string[] ExpectedInvalidateReleaseEvents = ["dispose", "release"];

    private static readonly string[] ExpectedAttachedCloseReleaseEvents = ["close", "release"];

    // ------------------------------------------------------------------ recording seams

    private sealed class FakeDispatchKernel : IStopDispatchKernel
    {
        private readonly List<string> _events;
        private readonly Queue<MotionStopNativeReply> _replies = new();

        internal FakeDispatchKernel(List<string> events)
        {
            _events = events;
        }

        internal List<MotionStopAxis> Dispatched { get; } = new();

        internal Action? OnDispatch { get; set; }

        public bool IsInvalidated { get; private set; }

        internal FakeDispatchKernel Returns(MotionStopNativeReply reply)
        {
            _replies.Enqueue(reply);
            return this;
        }

        public MotionStopNativeReply Dispatch(MotionStopAxis axis)
        {
            _events.Add("dispatch");
            Dispatched.Add(axis);
            OnDispatch?.Invoke();

            return _replies.Count > 0
                ? _replies.Dequeue()
                : MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, Reply(string.Empty));
        }

        public void Dispose()
        {
            _events.Add("dispose");
            IsInvalidated = true;
        }
    }

    private sealed class FakeControllerKernel : IControllerKernel
    {
        private readonly List<string> _events;

        internal FakeControllerKernel(List<string> events)
        {
            _events = events;
        }

        internal bool WithholdCommandExport { get; set; }

        internal int CloseResult { get; set; }

        internal int CloseCount { get; private set; }

        internal int ReleaseCount { get; private set; }

        public IntPtr LoadApprovedModule(string approvedAbsolutePath) => new(101);

        public string? GetLoadedModulePath(IntPtr module) => null;

        public IntPtr ResolveExport(IntPtr module, string exportName)
        {
            if (WithholdCommandExport
                && string.Equals(exportName, MotionControllerCommandApi.CommandExportName, StringComparison.Ordinal))
            {
                return IntPtr.Zero;
            }

            return new IntPtr(501);
        }

        public bool ReleaseModule(IntPtr module)
        {
            _events.Add("release");
            ReleaseCount++;
            return true;
        }

        public void InvokeForceDirectMode(IntPtr export, byte directOnly) => throw new NotSupportedException();

        public int InvokeOpen(IntPtr export, byte[] addressBuffer, out IntPtr handle) => throw new NotSupportedException();

        public int InvokeClose(IntPtr export, ref IntPtr handle)
        {
            _events.Add("close");
            CloseCount++;
            if (CloseResult == 0)
            {
                handle = IntPtr.Zero;
            }

            return CloseResult;
        }

        public int InvokeConnectionType(IntPtr export, IntPtr handle, out int connectionType) => throw new NotSupportedException();

        public int InvokeRevisionInformation(IntPtr export, IntPtr handle, byte[] buffer) => throw new NotSupportedException();

        public int InvokeSerialNumber(IntPtr export, IntPtr handle, out uint serialNumber) => throw new NotSupportedException();
    }

    private static byte[] Reply(string text)
    {
        var buffer = new byte[MotionStopCommandPlan.MaximumReplyBytes];
        _ = System.Text.Encoding.ASCII.GetBytes(text, 0, text.Length, buffer, 0);
        return buffer;
    }

    private static byte[] Unterminated() =>
        Enumerable.Repeat((byte)'A', MotionStopCommandPlan.MaximumReplyBytes).ToArray();

    // ------------------------------------------------------------------ closed domain and templates

    [Fact]
    public void Axis_A_And_Axis_B_Map_Only_To_Their_Exact_Templates()
    {
        Assert.True(MotionStopCommandPlan.TryGetTemplate(MotionStopAxis.A, out var a));
        Assert.Equal("ST A", a);

        Assert.True(MotionStopCommandPlan.TryGetTemplate(MotionStopAxis.B, out var b));
        Assert.Equal("ST B", b);

        Assert.Equal(2, Enum.GetValues<MotionStopAxis>().Length);
    }

    [Fact]
    public void The_Empty_Default_Axis_Is_Not_Axis_A()
    {
        var empty = default(MotionStopAxis);

        Assert.False(MotionStopCommandPlan.IsDefined(empty));
        Assert.False(MotionStopCommandPlan.TryGetTemplate(empty, out _));
        Assert.NotEqual(MotionStopAxis.A, empty);
    }

    [Fact]
    public void Axis_C_D_Empty_Combined_And_Other_Values_Are_Refused_And_Never_Dispatched()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>());
        var session = new MotionStopSession(dispatch);

        var refused = new[]
        {
            default(MotionStopAxis),
            (MotionStopAxis)3,
            (MotionStopAxis)4,
            MotionStopAxis.A | MotionStopAxis.B,
            (MotionStopAxis)99,
            (MotionStopAxis)(-1),
        };

        foreach (var axis in refused)
        {
            Assert.False(MotionStopCommandPlan.IsDefined(axis));
            Assert.False(MotionStopCommandPlan.TryGetTemplate(axis, out _));

            var outcome = session.Stop(axis);

            Assert.Equal(MotionStopOutcomeCodes.AxisOutOfDomain, outcome.Code);
            Assert.False(outcome.NativeDispatchAttempted);
            Assert.False(outcome.StopDispatched);
        }

        Assert.Empty(dispatch.Dispatched);
        Assert.Equal(0, session.KernelDispatchCount);
    }

    // ------------------------------------------------------------------ dispatch count and confirmation

    [Fact]
    public void An_Accepted_Axis_A_Stop_Dispatches_Exactly_Once_And_Is_Dispatched_Not_Confirmed()
    {
        var events = new List<string>();
        using var dispatch = new FakeDispatchKernel(events);
        var session = new MotionStopSession(dispatch);

        var outcome = session.Stop(MotionStopAxis.A);

        Assert.Equal(MotionStopOutcomeCodes.Dispatched, outcome.Code);
        Assert.True(outcome.StopDispatched);
        Assert.True(outcome.NativeDispatchAttempted);
        Assert.False(outcome.StopConfirmed);
        Assert.False(outcome.RetryPermitted);
        Assert.Equal(new[] { MotionStopAxis.A }, dispatch.Dispatched);
        Assert.Equal(1, session.KernelDispatchCount);

        // No readback, polling or confirmation read follows the single dispatch.
        Assert.Equal(ExpectedSingleDispatchEvents, events);
    }

    [Fact]
    public void An_Accepted_Axis_B_Stop_Dispatches_Only_Axis_B()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>());
        var session = new MotionStopSession(dispatch);

        var outcome = session.Stop(MotionStopAxis.B);

        Assert.Equal(MotionStopOutcomeCodes.Dispatched, outcome.Code);
        Assert.Equal(new[] { MotionStopAxis.B }, dispatch.Dispatched);
    }

    [Fact]
    public void Each_Reply_Shape_Maps_To_One_Deterministic_Outcome_Code()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>())
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, Reply("OK")))
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, Unterminated()))
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, Reply(string.Empty)))
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, Reply("?")))
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultTimeout, Reply(string.Empty)));
        var session = new MotionStopSession(dispatch);

        var codes = new[]
        {
            session.Stop(MotionStopAxis.A).Code,
            session.Stop(MotionStopAxis.A).Code,
            session.Stop(MotionStopAxis.A).Code,
            session.Stop(MotionStopAxis.B).Code,
            session.Stop(MotionStopAxis.B).Code,
        };

        Assert.Equal(
            new[]
            {
                MotionStopOutcomeCodes.ReplyUnclassified,
                MotionStopOutcomeCodes.ReplyUnclassified,
                MotionStopOutcomeCodes.Dispatched,
                MotionStopOutcomeCodes.RejectedByController,
                MotionStopOutcomeCodes.DispatchFailed,
            },
            codes);
    }

    [Fact]
    public void An_Empty_Reply_Is_The_Absence_Of_A_Refusal_Not_A_Parsed_Acknowledgement()
    {
        Assert.Equal(MotionStopReplyClass.NoReply, MotionStopAcknowledgement.ClassifyReply(Reply(string.Empty)));
        Assert.Equal(MotionStopReplyClass.ControllerRejected, MotionStopAcknowledgement.ClassifyReply(Reply("?")));
        Assert.Equal(MotionStopReplyClass.ControllerRejected, MotionStopAcknowledgement.ClassifyReply(Reply("1?")));
        Assert.Equal(MotionStopReplyClass.Unclassified, MotionStopAcknowledgement.ClassifyReply(Reply("OK")));
        Assert.Equal(MotionStopReplyClass.Unclassified, MotionStopAcknowledgement.ClassifyReply(Reply("0")));
        Assert.Equal(MotionStopReplyClass.Unclassified, MotionStopAcknowledgement.ClassifyReply(Reply(":")));
        Assert.Equal(MotionStopReplyClass.Unclassified, MotionStopAcknowledgement.ClassifyReply(Reply("\u0001")));
        Assert.Equal(MotionStopReplyClass.Unclassified, MotionStopAcknowledgement.ClassifyReply(Unterminated()));
    }

    // ------------------------------------------------------------------ duplicate, replay, busy

    [Fact]
    public void A_Duplicate_Issued_During_The_Native_Call_Is_Refused_Without_A_Second_Dispatch()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>());
        var session = new MotionStopSession(dispatch);
        var nested = new List<MotionStopOutcome>();
        dispatch.OnDispatch = () => nested.Add(session.Stop(MotionStopAxis.B));

        var outer = session.Stop(MotionStopAxis.A);

        var inner = Assert.Single(nested);
        Assert.Equal(MotionStopOutcomeCodes.OperationBusy, inner.Code);
        Assert.False(inner.NativeDispatchAttempted);
        Assert.Equal(new[] { MotionStopAxis.A }, dispatch.Dispatched);
        Assert.Equal(MotionStopOutcomeCodes.Dispatched, outer.Code);
    }

    [Fact]
    public void A_Pending_Operation_Blocks_A_Second_Begin_Until_It_Is_Dispatched()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>());
        var session = new MotionStopSession(dispatch);

        var first = session.Begin(MotionStopAxis.A);
        var second = session.Begin(MotionStopAxis.B);

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Equal(MotionStopOutcomeCodes.OperationBusy, second.RefusalCode);

        var outcome = session.Dispatch(first);

        Assert.Equal(MotionStopOutcomeCodes.Dispatched, outcome.Code);
        Assert.Equal(new[] { MotionStopAxis.A }, dispatch.Dispatched);
    }

    [Fact]
    public void A_Consumed_Operation_Is_Refused_As_Replayed_And_Never_Dispatched_Twice()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>());
        var session = new MotionStopSession(dispatch);
        var operation = session.Begin(MotionStopAxis.A);

        Assert.Equal(MotionStopOutcomeCodes.Dispatched, session.Dispatch(operation).Code);

        var replay = session.Dispatch(operation);

        Assert.Equal(MotionStopOutcomeCodes.OperationReplayed, replay.Code);
        Assert.False(replay.NativeDispatchAttempted);
        Assert.Equal(new[] { MotionStopAxis.A }, dispatch.Dispatched);
    }

    [Fact]
    public void An_Operation_Issued_By_Another_Session_Is_Refused_As_Foreign()
    {
        using var ownDispatch = new FakeDispatchKernel(new List<string>());
        using var foreignDispatch = new FakeDispatchKernel(new List<string>());
        var owner = new MotionStopSession(ownDispatch);
        var other = new MotionStopSession(foreignDispatch);
        var operation = owner.Begin(MotionStopAxis.A);

        var outcome = other.Dispatch(operation);

        Assert.Equal(MotionStopOutcomeCodes.OperationForeign, outcome.Code);
        Assert.Empty(foreignDispatch.Dispatched);
        Assert.Empty(ownDispatch.Dispatched);
    }

    // ------------------------------------------------------------------ generation fence and staleness

    [Fact]
    public void An_Operation_Issued_For_An_Earlier_Generation_Is_Refused_Before_Any_Native_Call()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>());
        var session = new MotionStopSession(dispatch);
        var operation = session.Begin(MotionStopAxis.A);

        session.Invalidate();
        var outcome = session.Dispatch(operation);

        Assert.Equal(MotionStopOutcomeCodes.GenerationStale, outcome.Code);
        Assert.False(outcome.NativeDispatchAttempted);
        Assert.Empty(dispatch.Dispatched);
    }

    [Fact]
    public void A_Native_Result_Returned_After_Invalidation_Is_Rejected_As_Stale_Not_Dispatched()
    {
        var events = new List<string>();
        using var dispatch = new FakeDispatchKernel(events);
        var session = new MotionStopSession(dispatch);
        dispatch.OnDispatch = () => session.Invalidate();

        var outcome = session.Stop(MotionStopAxis.A);

        Assert.Equal(MotionStopOutcomeCodes.ResultStaleRejected, outcome.Code);
        Assert.True(outcome.NativeDispatchAttempted);
        Assert.False(outcome.StopDispatched);
        Assert.False(outcome.StopConfirmed);
        Assert.Equal(ExpectedDispatchThenInvalidationEvents, events);
    }

    [Fact]
    public void A_Closed_Session_Refuses_Every_Stop_Without_A_Native_Call()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>());
        var session = new MotionStopSession(dispatch);

        session.Invalidate();
        var outcome = session.Stop(MotionStopAxis.A);

        Assert.Equal(MotionStopOutcomeCodes.SessionClosed, outcome.Code);
        Assert.False(outcome.NativeDispatchAttempted);
        Assert.True(session.IsClosed);
        Assert.Empty(dispatch.Dispatched);
    }

    [Fact]
    public void Invalidation_Makes_No_Native_Call_And_Only_Invalidates_The_Boundary()
    {
        var events = new List<string>();
        using var dispatch = new FakeDispatchKernel(events);
        var session = new MotionStopSession(dispatch);

        session.Invalidate();
        session.Invalidate();

        Assert.Equal(ExpectedDoubleInvalidationEvents, events);
        Assert.True(dispatch.IsInvalidated);
        Assert.True(session.IsClosed);
    }

    // ------------------------------------------------------------------ no retry, failures, leakage

    [Fact]
    public void A_Controller_Refusal_Is_Reported_As_Rejected_And_Is_Never_Retried()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>())
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, Reply("?")));
        var session = new MotionStopSession(dispatch);

        var outcome = session.Stop(MotionStopAxis.B);

        Assert.Equal(MotionStopOutcomeCodes.RejectedByController, outcome.Code);
        Assert.False(outcome.StopDispatched);
        Assert.Equal(new[] { MotionStopAxis.B }, dispatch.Dispatched);
    }

    [Fact]
    public void A_Non_Success_Result_And_A_Fault_Are_Reported_As_Failed_And_Never_Retried()
    {
        using var failing = new FakeDispatchKernel(new List<string>())
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultCommandError, Reply(string.Empty)));
        var failedOutcome = new MotionStopSession(failing).Stop(MotionStopAxis.A);

        Assert.Equal(MotionStopOutcomeCodes.DispatchFailed, failedOutcome.Code);
        Assert.Single(failing.Dispatched);

        using var faulting = new FakeDispatchKernel(new List<string>())
            .Returns(MotionStopNativeReply.Faulted());
        var faultedOutcome = new MotionStopSession(faulting).Stop(MotionStopAxis.A);

        Assert.Equal(MotionStopOutcomeCodes.DispatchFailed, faultedOutcome.Code);
        Assert.True(faultedOutcome.NativeDispatchAttempted);
        Assert.Single(faulting.Dispatched);
    }

    [Fact]
    public void Raw_Reply_Text_And_Native_Result_Never_Reach_The_Outcome()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>())
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultInvalidArgument, Reply("SECRET-VENDOR-TEXT")))
            .Returns(MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, Reply("SECRET-VENDOR-TEXT")));
        var session = new MotionStopSession(dispatch);

        var failed = session.Stop(MotionStopAxis.A);
        var unclassified = session.Stop(MotionStopAxis.B);

        foreach (var outcome in new[] { failed, unclassified })
        {
            foreach (var surface in new[] { outcome.Code, outcome.Detail })
            {
                Assert.DoesNotContain("SECRET", surface, StringComparison.Ordinal);
                Assert.DoesNotContain("VENDOR", surface, StringComparison.Ordinal);
                Assert.DoesNotContain("GCLIB", surface, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Every_Outcome_Code_Is_Distinct_Has_A_Detail_And_Never_Claims_Confirmation()
    {
        var codes = typeof(MotionStopOutcomeCodes)
            .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(codes);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());

        var unknownDetail = MotionStopOutcomeCodes.DetailFor("UNKNOWN_CODE");
        foreach (var code in codes)
        {
            Assert.DoesNotContain("CONFIRM", code, StringComparison.Ordinal);
            Assert.NotEqual(unknownDetail, MotionStopOutcomeCodes.DetailFor(code));
        }
    }

    // ------------------------------------------------------------------ boundary and lifecycle order

    [Fact]
    public void An_Unbound_Or_Invalidated_Native_Carrier_Makes_No_Native_Call()
    {
        using var unbound = new BoundStopDispatchKernel(IntPtr.Zero, IntPtr.Zero);
        var unboundOutcome = new MotionStopSession(unbound).Stop(MotionStopAxis.A);

        Assert.Equal(MotionStopOutcomeCodes.BoundaryUnavailable, unboundOutcome.Code);
        Assert.False(unboundOutcome.NativeDispatchAttempted);

        using var invalidated = new BoundStopDispatchKernel(new IntPtr(1), new IntPtr(2));
        invalidated.Dispose();
        var invalidatedOutcome = new MotionStopSession(invalidated).Stop(MotionStopAxis.B);

        Assert.Equal(MotionStopOutcomeCodes.BoundaryClosed, invalidatedOutcome.Code);
        Assert.False(invalidatedOutcome.NativeDispatchAttempted);
        Assert.True(invalidated.IsInvalidated);
    }

    [Fact]
    public void Invalidation_Precedes_Close_And_Close_Precedes_Unload()
    {
        var events = new List<string>();
        using var dispatch = new FakeDispatchKernel(events);
        var controller = new FakeControllerKernel(events);
        var owner = new MotionStopOwner(controller, dispatch, new IntPtr(101), new IntPtr(201), new IntPtr(501));

        var outcome = owner.Close();

        Assert.Equal(ExpectedInvalidateCloseReleaseEvents, events);
        Assert.True(outcome.SessionInvalidated);
        Assert.True(outcome.HandleClosed);
        Assert.True(outcome.ModuleReleased);
    }

    [Fact]
    public void Close_Is_Idempotent_And_A_Closed_Owner_Refuses_Every_Stop()
    {
        using var dispatch = new FakeDispatchKernel(new List<string>());
        var controller = new FakeControllerKernel(new List<string>());
        var owner = new MotionStopOwner(controller, dispatch, new IntPtr(101), new IntPtr(201), new IntPtr(501));

        var first = owner.Close();
        var second = owner.Close();

        Assert.Same(first, second);
        Assert.Equal(1, controller.CloseCount);
        Assert.Equal(1, controller.ReleaseCount);

        var outcome = owner.Session.Stop(MotionStopAxis.A);

        Assert.Equal(MotionStopOutcomeCodes.SessionClosed, outcome.Code);
        Assert.Empty(dispatch.Dispatched);
    }

    [Fact]
    public void An_Owner_Without_An_Open_Handle_Skips_The_Controller_Close_And_Still_Releases_The_Module()
    {
        var events = new List<string>();
        using var dispatch = new FakeDispatchKernel(events);
        var controller = new FakeControllerKernel(events);
        var owner = new MotionStopOwner(controller, dispatch, new IntPtr(101), IntPtr.Zero, new IntPtr(501));

        var outcome = owner.Close();

        Assert.Equal(ExpectedInvalidateReleaseEvents, events);
        Assert.Equal(0, controller.CloseCount);
        Assert.True(outcome.HandleClosed);
        Assert.True(outcome.ModuleReleased);
    }

    [Fact]
    public void A_Failed_Handle_Close_Still_Releases_The_Module_And_Reports_The_Truth()
    {
        var events = new List<string>();
        using var dispatch = new FakeDispatchKernel(events);
        var controller = new FakeControllerKernel(events) { CloseResult = MotionControllerApi.ResultCommandError };
        var owner = new MotionStopOwner(controller, dispatch, new IntPtr(101), new IntPtr(201), new IntPtr(501));

        var outcome = owner.Close();

        Assert.Equal(ExpectedInvalidateCloseReleaseEvents, events);
        Assert.True(outcome.SessionInvalidated);
        Assert.False(outcome.HandleClosed);
        Assert.True(outcome.ModuleReleased);
    }

    [Fact]
    public void Attach_Resolves_The_Approved_Command_Export_Or_Returns_Nothing()
    {
        var events = new List<string>();
        var controller = new FakeControllerKernel(events) { WithholdCommandExport = true };

        Assert.Null(MotionStopOwner.Attach(controller, new IntPtr(101), new IntPtr(201)));

        controller.WithholdCommandExport = false;
        var owner = MotionStopOwner.Attach(controller, new IntPtr(101), new IntPtr(201));

        Assert.NotNull(owner);

        var outcome = owner!.Close();

        // The bound carrier invalidates without a native call, then the handle and module follow.
        Assert.Equal(ExpectedAttachedCloseReleaseEvents, events);
        Assert.True(outcome.HandleClosed);
        Assert.True(outcome.ModuleReleased);
    }
}
