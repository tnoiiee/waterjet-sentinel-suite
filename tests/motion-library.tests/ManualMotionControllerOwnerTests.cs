using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Fake-kernel coverage of the Manual Motion Commissioning controller owner and its invocation
/// gate: one Gate-A-proven open, controller-wide native-call serialization, typed dispatch/read/Stop
/// classification, generation fencing and the deterministic invalidate → close → unload cleanup.
/// These tests never load a vendor DLL, open a real endpoint or contact a Controller; every native
/// boundary terminates in a recording fake and every value is synthetic.
/// </summary>
public sealed class ManualMotionControllerOwnerTests
{
    private sealed class FakeControllerKernel : IControllerKernel
    {
        private IntPtr _nextModule = new(101);
        private IntPtr _nextExport = new(501);

        internal List<string> Log { get; } = new();

        internal Dictionary<IntPtr, string> ModulePaths { get; } = new();

        internal int OpenResult { get; set; } = MotionControllerApi.ResultSuccess;

        internal int OpenCount { get; private set; }

        internal int CloseCount { get; private set; }

        internal int DirectModeCount { get; private set; }

        public IntPtr LoadApprovedModule(string approvedAbsolutePath)
        {
            Log.Add("load-" + Path.GetFileName(approvedAbsolutePath));
            var module = _nextModule;
            _nextModule = IntPtr.Add(_nextModule, 1);
            ModulePaths[module] = approvedAbsolutePath;
            return module;
        }

        public string? GetLoadedModulePath(IntPtr module)
        {
            Log.Add("path-" + Path.GetFileName(ModulePaths[module]));
            return ModulePaths[module];
        }

        public IntPtr ResolveExport(IntPtr module, string exportName)
        {
            Log.Add("resolve");
            var export = _nextExport;
            _nextExport = IntPtr.Add(_nextExport, 1);
            return export;
        }

        public bool ReleaseModule(IntPtr module)
        {
            Log.Add("release-" + Path.GetFileName(ModulePaths[module]));
            return true;
        }

        public void InvokeForceDirectMode(IntPtr export, byte directOnly)
        {
            DirectModeCount++;
            Log.Add("force-direct");
        }

        public int InvokeOpen(IntPtr export, byte[] addressBuffer, out IntPtr handle)
        {
            OpenCount++;
            Log.Add("open");
            handle = OpenResult == MotionControllerApi.ResultSuccess ? new IntPtr(201) : IntPtr.Zero;
            return OpenResult;
        }

        public int InvokeClose(IntPtr export, ref IntPtr handle)
        {
            CloseCount++;
            Log.Add("close");
            handle = IntPtr.Zero;
            return MotionControllerApi.ResultSuccess;
        }

        public int InvokeConnectionType(IntPtr export, IntPtr handle, out int connectionType)
        {
            connectionType = MotionControllerApi.ConnectionTypeEthernet;
            return MotionControllerApi.ResultSuccess;
        }

        public int InvokeRevisionInformation(IntPtr export, IntPtr handle, byte[] buffer) =>
            MotionControllerApi.ResultSuccess;

        public int InvokeSerialNumber(IntPtr export, IntPtr handle, out uint serialNumber)
        {
            serialNumber = 1;
            return MotionControllerApi.ResultSuccess;
        }
    }

    private sealed class FakeReadKernel : IObservationReadKernel
    {
        private readonly List<string> _log;

        internal FakeReadKernel(List<string> log) => _log = log;

        internal Func<MotionObservationIntent, ObservationReadResult> Handler { get; set; } =
            _ => ObservationReadResult.FromValue("0");

        internal int IssueCount { get; private set; }

        public bool IsInvalidated { get; private set; }

        public ObservationReadResult Issue(MotionObservationIntent intent)
        {
            IssueCount++;
            return Handler(intent);
        }

        public void Dispose()
        {
            IsInvalidated = true;
            _log.Add("invalidate-read");
        }
    }

    private sealed class FakeStopKernel : IStopDispatchKernel
    {
        private readonly List<string> _log;

        internal FakeStopKernel(List<string> log) => _log = log;

        internal List<MotionStopAxis> DispatchedAxes { get; } = new();

        public bool IsInvalidated { get; private set; }

        public MotionStopNativeReply Dispatch(MotionStopAxis axis)
        {
            DispatchedAxes.Add(axis);
            return MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, new byte[8]);
        }

        public void Dispose()
        {
            IsInvalidated = true;
            _log.Add("invalidate-stop");
        }
    }

    private sealed class FakeCommandKernel : IManualMotionCommandKernel
    {
        private readonly List<string> _log;
        private int _inside;
        private int _entered;

        internal FakeCommandKernel(List<string> log) => _log = log;

        internal Func<ManualMotionPlanCommand, ManualMotionNativeReply> Handler { get; set; } =
            _ => ManualMotionNativeReply.Completed(MotionControllerApi.ResultSuccess, new byte[8]);

        internal List<ManualMotionPlanCommand> Commands { get; } = new();

        internal int MaxConcurrent { get; private set; }

        internal int EnteredCount => Volatile.Read(ref _entered);

        internal ManualResetEventSlim? FirstEntrySignal { get; set; }

        internal ManualResetEventSlim? ReleaseSignal { get; set; }

        public bool IsInvalidated { get; private set; }

        public ManualMotionNativeReply Dispatch(ManualMotionPlanCommand command)
        {
            var inside = Interlocked.Increment(ref _inside);
            Interlocked.Increment(ref _entered);
            try
            {
                // Single-threaded tests never contend, so a plain max update is exact enough to
                // detect any second concurrent entry in the serialization test.
                if (inside > MaxConcurrent)
                {
                    MaxConcurrent = inside;
                }

                Commands.Add(command);
                FirstEntrySignal?.Set();
                ReleaseSignal?.Wait(TimeSpan.FromSeconds(10));
                return Handler(command);
            }
            finally
            {
                Interlocked.Decrement(ref _inside);
            }
        }

        public void Dispose()
        {
            IsInvalidated = true;
            _log.Add("invalidate-command");
        }
    }

    private sealed class FakeEnvironment
    {
        internal Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);

        internal List<string> Removals { get; } = new();

        internal string? Read(string name) =>
            Values.TryGetValue(name, out var value) ? value : null;

        internal void Remove(string name)
        {
            Removals.Add(name);
            Values.Remove(name);
        }
    }

    private static readonly string SyntheticAddress = string.Join(".", 10, 20, 30, 40);

    private static readonly string[] ExpectedCleanupOrder =
    {
        "invalidate-read",
        "invalidate-stop",
        "invalidate-command",
        "close",
        "release-module-primary-shape",
        "release-module-companion-shape",
    };

    private sealed class OwnerRig
    {
        internal OwnerRig(Func<MotionLibraryProbeOutcome>? gateAProbe = null)
        {
            ReadKernel = new FakeReadKernel(Kernel.Log);
            StopKernel = new FakeStopKernel(Kernel.Log);
            CommandKernel = new FakeCommandKernel(Kernel.Log);
            Owner = new ManualMotionControllerOwner(
                ProbeConfiguration(),
                EndpointConfiguration(),
                () =>
                {
                    Kernel.Log.Add("gate-a");
                    GateAProbeCount++;
                    return gateAProbe?.Invoke()
                        ?? MotionLibraryProbeOutcome.Complete("synthetic-version", "synthetic proof");
                },
                () =>
                {
                    KernelFactoryCount++;
                    return Kernel;
                },
                (_, _) => ReadKernel,
                (_, _) => StopKernel,
                (_, _) => CommandKernel);
        }

        internal FakeControllerKernel Kernel { get; } = new();

        internal FakeReadKernel ReadKernel { get; }

        internal FakeStopKernel StopKernel { get; }

        internal FakeCommandKernel CommandKernel { get; }

        internal ManualMotionControllerOwner Owner { get; }

        internal int GateAProbeCount { get; private set; }

        internal int KernelFactoryCount { get; private set; }
    }

    private static MotionLibraryProbeConfiguration ProbeConfiguration()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wjss-manual-motion-shape"));
        return new MotionLibraryProbeConfiguration
        {
            NativeDirectory = directory,
            PrimaryModulePath = Path.Combine(directory, "module-primary-shape"),
            CompanionModulePath = Path.Combine(directory, "module-companion-shape"),
            ExpectedPrimaryHash = new byte[32],
            ExpectedCompanionHash = new byte[32],
            ExpectedVersion = "synthetic-version",
        };
    }

    private static MotionControllerEndpointConfiguration EndpointConfiguration()
    {
        var resolved = MotionControllerEndpointResolver.TryResolve(
            name => string.Equals(name, MotionControllerEndpointResolver.EndpointVariable, StringComparison.Ordinal)
                ? SyntheticAddress
                : null,
            out var configuration,
            out var refusalCode,
            out _);
        Assert.True(resolved, $"synthetic endpoint shape refused with {refusalCode}");
        return configuration;
    }

    private static FakeEnvironment ValidEnvironment(bool includeConsent = true, string? consentValue = null)
    {
        var environment = new FakeEnvironment();
        environment.Values[MotionLibraryProbeVocabulary.NativeDirectoryVariable] = @"C:\Synthetic\Approved\Library";
        environment.Values[MotionLibraryProbeVocabulary.ExpectedPrimaryHashVariable] =
            "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        environment.Values[MotionLibraryProbeVocabulary.ExpectedCompanionHashVariable] =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        environment.Values[MotionLibraryProbeVocabulary.ExpectedVersionVariable] = "9.9.9-test";
        environment.Values[MotionControllerEndpointResolver.EndpointVariable] = SyntheticAddress;
        if (includeConsent)
        {
            environment.Values[ManualMotionControllerCodes.ConsentVariable] =
                consentValue ?? ManualMotionControllerCodes.ExactConsentValue;
        }

        return environment;
    }

    [Fact]
    public void Happy_Path_Opens_Once_Dispatches_Reads_Stops_And_Cleans_Up_In_Order()
    {
        var rig = new OwnerRig();
        rig.ReadKernel.Handler = intent => ObservationReadResult.FromValue(
            intent == MotionObservationIntent.AxisStatusA ? "64" : "1");

        var opened = rig.Owner.Open();
        Assert.True(opened.Opened);
        Assert.Equal(1, opened.Generation);
        Assert.Equal(1, rig.Kernel.OpenCount);
        Assert.Equal(1, rig.Kernel.DirectModeCount);
        Assert.Equal("gate-a", rig.Kernel.Log[0]);

        var dispatch = rig.Owner.Dispatch(new ManualMotionPlanCommand(
            ManualMotionPlanCommandKind.BrakeOutputSet,
            ManualMotionPlanAxis.A,
            ManualMotionBrakeOutput.Output1,
            0));
        Assert.True(dispatch.Dispatched);
        Assert.False(dispatch.Fault);
        Assert.Equal(ManualMotionControllerCodes.Dispatched, dispatch.Code);
        Assert.Single(rig.CommandKernel.Commands);

        var status = rig.Owner.Read(ManualMotionReadTarget.StatusA);
        Assert.True(status.Succeeded);
        Assert.Equal(64m, status.Value);

        var output = rig.Owner.Read(ManualMotionReadTarget.Output1);
        Assert.True(output.Succeeded);
        Assert.Equal(1m, output.Value);

        var stop = rig.Owner.Stop(ManualMotionPlanAxis.B);
        Assert.True(stop.Dispatched);
        Assert.Equal(new[] { MotionStopAxis.B }, rig.StopKernel.DispatchedAxes.ToArray());

        var closed = rig.Owner.Close();
        Assert.Equal(ManualMotionControllerCodes.CleanupComplete, closed.Code);
        Assert.True(closed.BoundariesInvalidated);
        Assert.True(closed.HandleClosed);
        Assert.True(closed.ModulesReleased);

        var tail = rig.Kernel.Log.Skip(rig.Kernel.Log.IndexOf("invalidate-read")).ToArray();
        Assert.Equal(ExpectedCleanupOrder, tail);

        var closedAgain = rig.Owner.Close();
        Assert.Same(closed, closedAgain);
        Assert.Equal(1, rig.Kernel.CloseCount);

        var afterClose = rig.Owner.Dispatch(new ManualMotionPlanCommand(
            ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0));
        Assert.False(afterClose.Dispatched);
        Assert.False(afterClose.NativeAttempted);
        Assert.Equal(ManualMotionControllerCodes.NotOpen, afterClose.Code);
        Assert.Single(rig.CommandKernel.Commands);
    }

    [Fact]
    public void A_Second_Open_Is_Refused_And_Nothing_Reconnects()
    {
        var rig = new OwnerRig();
        Assert.True(rig.Owner.Open().Opened);

        var second = rig.Owner.Open();

        Assert.False(second.Opened);
        Assert.Equal(ManualMotionControllerCodes.OpenAlreadyPerformed, second.Code);
        Assert.Equal(1, rig.Kernel.OpenCount);
        Assert.Equal(1, rig.GateAProbeCount);
    }

    [Fact]
    public void A_Gate_A_Refusal_Loads_Nothing_And_The_Open_Is_Not_Retried()
    {
        var rig = new OwnerRig(() => MotionLibraryProbeOutcome.Refused(
            MotionLibraryProbeVocabulary.NativeLoadFailed,
            "synthetic refusal",
            nativeHandlesReleased: true));

        var opened = rig.Owner.Open();

        Assert.False(opened.Opened);
        Assert.Equal(ManualMotionControllerCodes.NativeProofRefused, opened.Code);
        Assert.Equal(0, rig.KernelFactoryCount);
        Assert.Equal(0, rig.Kernel.OpenCount);

        var second = rig.Owner.Open();
        Assert.Equal(ManualMotionControllerCodes.OpenAlreadyPerformed, second.Code);
        Assert.Equal(1, rig.GateAProbeCount);
    }

    [Fact]
    public void An_Open_Native_Refusal_Releases_The_Loaded_Modules()
    {
        var rig = new OwnerRig();
        rig.Kernel.OpenResult = MotionControllerApi.ResultNotConnected;

        var opened = rig.Owner.Open();

        Assert.False(opened.Opened);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenNotConnected, opened.Code);
        Assert.Equal(1, rig.Kernel.OpenCount);
        Assert.Contains("release-module-primary-shape", rig.Kernel.Log);
        Assert.Contains("release-module-companion-shape", rig.Kernel.Log);
        Assert.Equal(0, rig.Kernel.CloseCount);
    }

    [Fact]
    public void Dispatch_Before_Open_Makes_No_Native_Call()
    {
        var rig = new OwnerRig();

        var dispatch = rig.Owner.Dispatch(new ManualMotionPlanCommand(
            ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0));
        var read = rig.Owner.Read(ManualMotionReadTarget.TpA);
        var stop = rig.Owner.Stop(ManualMotionPlanAxis.A);

        Assert.Equal(ManualMotionControllerCodes.NotOpen, dispatch.Code);
        Assert.Equal(ManualMotionControllerCodes.NotOpen, read.Code);
        Assert.Equal(ManualMotionControllerCodes.NotOpen, stop.Code);
        Assert.Empty(rig.CommandKernel.Commands);
        Assert.Equal(0, rig.ReadKernel.IssueCount);
        Assert.Empty(rig.StopKernel.DispatchedAxes);
        Assert.Equal(0, rig.GateAProbeCount);
    }

    [Fact]
    public void Dispatch_Classification_Separates_Rejection_From_Fault_From_Success()
    {
        var rig = new OwnerRig();
        Assert.True(rig.Owner.Open().Opened);
        var command = new ManualMotionPlanCommand(
            ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0);

        rig.CommandKernel.Handler = _ => ManualMotionNativeReply.Completed(
            MotionControllerApi.ResultSuccess,
            new byte[] { (byte)'?', 0, 0, 0 });
        var rejected = rig.Owner.Dispatch(command);
        Assert.False(rejected.Dispatched);
        Assert.False(rejected.Fault);
        Assert.Equal(ManualMotionControllerCodes.RejectedByController, rejected.Code);

        rig.CommandKernel.Handler = _ => ManualMotionNativeReply.Completed(
            MotionControllerApi.ResultTimeout,
            new byte[8]);
        var faulted = rig.Owner.Dispatch(command);
        Assert.False(faulted.Dispatched);
        Assert.True(faulted.Fault);
        Assert.Equal(ManualMotionControllerCodes.DispatchFailed, faulted.Code);

        rig.CommandKernel.Handler = _ => ManualMotionNativeReply.NotAttempted(
            ManualMotionControllerCodes.BoundaryClosed);
        var notAttempted = rig.Owner.Dispatch(command);
        Assert.False(notAttempted.Dispatched);
        Assert.False(notAttempted.NativeAttempted);
        Assert.Equal(ManualMotionControllerCodes.BoundaryClosed, notAttempted.Code);

        // Exactly one kernel dispatch per owner dispatch: no retry on any outcome.
        Assert.Equal(3, rig.CommandKernel.Commands.Count);
    }

    [Fact]
    public void Reads_Are_Bounded_And_A_Refused_Read_Carries_No_Value()
    {
        var rig = new OwnerRig();
        Assert.True(rig.Owner.Open().Opened);

        rig.ReadKernel.Handler = _ => ObservationReadResult.FromValue("256");
        var statusOutOfRange = rig.Owner.Read(ManualMotionReadTarget.StatusA);
        Assert.False(statusOutOfRange.Succeeded);
        Assert.Equal(0m, statusOutOfRange.Value);
        Assert.Equal(ManualMotionControllerCodes.ReadValueOutOfRange, statusOutOfRange.Code);

        rig.ReadKernel.Handler = _ => ObservationReadResult.FromValue("2");
        var outputOutOfRange = rig.Owner.Read(ManualMotionReadTarget.Output2);
        Assert.False(outputOutOfRange.Succeeded);
        Assert.Equal(ManualMotionControllerCodes.ReadValueOutOfRange, outputOutOfRange.Code);

        rig.ReadKernel.Handler = _ => ObservationReadResult.FromValue("not-a-number");
        var invalid = rig.Owner.Read(ManualMotionReadTarget.TpB);
        Assert.False(invalid.Succeeded);
        Assert.Equal(MotionObservedStateVocabulary.ObservationResponseInvalid, invalid.Code);

        var issuesBefore = rig.ReadKernel.IssueCount;
        var outOfDomain = rig.Owner.Read((ManualMotionReadTarget)99);
        Assert.False(outOfDomain.Succeeded);
        Assert.Equal(ManualMotionControllerCodes.ReadTargetOutOfDomain, outOfDomain.Code);
        Assert.Equal(issuesBefore, rig.ReadKernel.IssueCount);
    }

    [Fact]
    public async Task Native_Calls_Are_Serialized_Controller_Wide()
    {
        var rig = new OwnerRig();
        Assert.True(rig.Owner.Open().Opened);

        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        rig.CommandKernel.FirstEntrySignal = entered;
        rig.CommandKernel.ReleaseSignal = release;

        var command = new ManualMotionPlanCommand(
            ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0);
        var first = Task.Run(() => rig.Owner.Dispatch(command));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "the first dispatch did not reach the kernel");

        var second = Task.Run(() => rig.Owner.Dispatch(command));
        Thread.Sleep(200);

        // The owner's single controller gate holds the second dispatch outside the kernel while the
        // first native call is still in flight.
        Assert.Equal(1, rig.CommandKernel.EnteredCount);
        Assert.Equal(1, rig.CommandKernel.MaxConcurrent);

        release.Set();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, rig.CommandKernel.EnteredCount);
        Assert.Equal(1, rig.CommandKernel.MaxConcurrent);
        Assert.All(results, result => Assert.True(result.Dispatched));
    }

    [Fact]
    public void Invocation_Refuses_Missing_Or_Wrong_Consent_Without_Creating_An_Owner()
    {
        var missing = ValidEnvironment(includeConsent: false);
        var factoryCalls = 0;

        var created = ManualMotionControllerInvocation.TryCreate(
            missing.Read,
            missing.Remove,
            (native, endpoint) =>
            {
                factoryCalls++;
                return ManualMotionControllerOwner.CreateProduction(native, endpoint);
            },
            out var owner,
            out var refusalCode);

        Assert.False(created);
        Assert.Null(owner);
        Assert.Equal(ManualMotionControllerCodes.ConsentNotConfigured, refusalCode);
        Assert.Equal(0, factoryCalls);
        Assert.Contains(ManualMotionControllerCodes.ConsentVariable, missing.Removals);

        var wrong = ValidEnvironment(consentValue: "authorize please");
        created = ManualMotionControllerInvocation.TryCreate(
            wrong.Read,
            wrong.Remove,
            (native, endpoint) =>
            {
                factoryCalls++;
                return ManualMotionControllerOwner.CreateProduction(native, endpoint);
            },
            out owner,
            out refusalCode);

        Assert.False(created);
        Assert.Null(owner);
        Assert.Equal(ManualMotionControllerCodes.ConsentRejected, refusalCode);
        Assert.Equal(0, factoryCalls);
        Assert.DoesNotContain(ManualMotionControllerCodes.ConsentVariable, wrong.Values.Keys);
    }

    [Fact]
    public void Invocation_Consumes_Consent_Before_Later_Preconditions_And_Creates_One_Owner()
    {
        var withoutNative = ValidEnvironment();
        withoutNative.Values.Remove(MotionLibraryProbeVocabulary.NativeDirectoryVariable);
        var factoryCalls = 0;

        var created = ManualMotionControllerInvocation.TryCreate(
            withoutNative.Read,
            withoutNative.Remove,
            (native, endpoint) =>
            {
                factoryCalls++;
                return ManualMotionControllerOwner.CreateProduction(native, endpoint);
            },
            out var owner,
            out var refusalCode);

        Assert.False(created);
        Assert.Null(owner);
        Assert.False(string.IsNullOrEmpty(refusalCode));
        Assert.Equal(0, factoryCalls);

        // The one-use consent is consumed even though a later precondition refused the invocation.
        Assert.DoesNotContain(ManualMotionControllerCodes.ConsentVariable, withoutNative.Values.Keys);

        var valid = ValidEnvironment();
        created = ManualMotionControllerInvocation.TryCreate(
            valid.Read,
            valid.Remove,
            (native, endpoint) =>
            {
                factoryCalls++;
                return ManualMotionControllerOwner.CreateProduction(native, endpoint);
            },
            out owner,
            out refusalCode);

        Assert.True(created);
        Assert.NotNull(owner);
        Assert.Equal(string.Empty, refusalCode);
        Assert.Equal(1, factoryCalls);
        Assert.DoesNotContain(ManualMotionControllerCodes.ConsentVariable, valid.Values.Keys);

        // Construction performed no native work: the owner is unopened and generation zero. It is
        // never opened by this test.
        Assert.Equal(0, owner!.Generation);
    }
}
