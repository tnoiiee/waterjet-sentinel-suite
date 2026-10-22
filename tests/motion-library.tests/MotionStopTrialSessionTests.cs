using System.Text;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Fake-kernel coverage of the isolated Owner-invoked C1 Stop lifecycle. These tests never load a
/// vendor DLL, open a real endpoint, or contact a Controller. All local values and paths are
/// synthetic, and all dispatches terminate in a recording fake.
/// </summary>
public sealed class MotionStopTrialSessionTests
{
    private sealed class FakeControllerKernel : IControllerKernel
    {
        private IntPtr _nextModule = new(101);
        private IntPtr _nextExport = new(501);
        private int _loadCalls;

        internal List<string> Log { get; } = new();
        internal List<string> ResolvedExports { get; } = new();
        internal List<string> ReleasedModules { get; } = new();
        internal Dictionary<IntPtr, string> ModulePaths { get; } = new();
        internal Dictionary<IntPtr, string> LoadedPathOverrides { get; } = new();
        internal HashSet<string> MissingExports { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> FailedReleaseNames { get; } = new(StringComparer.Ordinal);

        internal int OpenResult { get; set; } = MotionControllerApi.ResultSuccess;
        internal IntPtr OpenHandle { get; set; } = new(201);
        internal bool ReturnHandleOnRefusedOpen { get; set; }
        internal bool ReturnZeroHandleOnSuccess { get; set; }
        internal bool ThrowOnOpenAfterSettingHandle { get; set; }
        internal bool ThrowOnDirectMode { get; set; }
        internal int CloseResult { get; set; } = MotionControllerApi.ResultSuccess;
        internal bool ThrowOnClose { get; set; }
        internal int ThrowOnLoadCall { get; set; }
        internal bool ReleaseResult { get; set; } = true;
        internal bool ThrowOnPathProof { get; set; }
        internal int OpenCount { get; private set; }
        internal int CloseCount { get; private set; }
        internal int DirectModeCount { get; private set; }
        internal byte LastDirectModeByte { get; private set; } = 1;
        internal string? LastAddress { get; private set; }
        internal int IdentityReadCount { get; private set; }

        public IntPtr LoadApprovedModule(string approvedAbsolutePath)
        {
            _loadCalls++;
            var fileName = Path.GetFileName(approvedAbsolutePath);
            Log.Add("load-" + fileName);
            if (_loadCalls == ThrowOnLoadCall)
            {
                throw new InvalidOperationException("synthetic load failure");
            }

            var module = _nextModule;
            _nextModule = IntPtr.Add(_nextModule, 1);
            ModulePaths[module] = approvedAbsolutePath;
            return module;
        }

        public string? GetLoadedModulePath(IntPtr module)
        {
            Log.Add("path-" + Path.GetFileName(ModulePaths[module]));
            if (ThrowOnPathProof)
            {
                throw new InvalidOperationException("synthetic path failure");
            }

            return LoadedPathOverrides.TryGetValue(module, out var replacement)
                ? replacement
                : ModulePaths[module];
        }

        public IntPtr ResolveExport(IntPtr module, string exportName)
        {
            ResolvedExports.Add(exportName);
            Log.Add("resolve-" + ExportLabel(exportName));

            var isAllowed = string.Equals(exportName, MotionControllerApi.ForceDirectModeExportName, StringComparison.Ordinal)
                || string.Equals(exportName, MotionControllerApi.OpenExportName, StringComparison.Ordinal)
                || string.Equals(exportName, MotionControllerApi.CloseExportName, StringComparison.Ordinal)
                || string.Equals(exportName, MotionControllerCommandApi.CommandExportName, StringComparison.Ordinal);
            if (!isAllowed)
            {
                throw new InvalidOperationException("unapproved export request");
            }

            if (MissingExports.Contains(exportName))
            {
                return IntPtr.Zero;
            }

            var export = _nextExport;
            _nextExport = IntPtr.Add(_nextExport, 1);
            return export;
        }

        public bool ReleaseModule(IntPtr module)
        {
            var fileName = Path.GetFileName(ModulePaths[module]);
            ReleasedModules.Add(fileName);
            Log.Add("release-" + fileName);
            return ReleaseResult && !FailedReleaseNames.Contains(fileName);
        }

        public void InvokeForceDirectMode(IntPtr export, byte directOnly)
        {
            DirectModeCount++;
            LastDirectModeByte = directOnly;
            Log.Add("force-direct");
            if (ThrowOnDirectMode)
            {
                throw new InvalidOperationException("synthetic direct-mode failure");
            }
        }

        public int InvokeOpen(IntPtr export, byte[] addressBuffer, out IntPtr handle)
        {
            OpenCount++;
            Log.Add("open");
            LastAddress = Encoding.ASCII.GetString(addressBuffer, 0, addressBuffer.Length - 1);
            handle = OpenResult == MotionControllerApi.ResultSuccess
                ? ReturnZeroHandleOnSuccess ? IntPtr.Zero : OpenHandle
                : ReturnHandleOnRefusedOpen ? OpenHandle : IntPtr.Zero;

            if (ThrowOnOpenAfterSettingHandle)
            {
                throw new InvalidOperationException("synthetic open failure");
            }

            return OpenResult;
        }

        public int InvokeClose(IntPtr export, ref IntPtr handle)
        {
            CloseCount++;
            Log.Add("close");
            if (ThrowOnClose)
            {
                throw new InvalidOperationException("synthetic close failure");
            }

            if (CloseResult == MotionControllerApi.ResultSuccess)
            {
                handle = IntPtr.Zero;
            }

            return CloseResult;
        }

        public int InvokeConnectionType(IntPtr export, IntPtr handle, out int connectionType)
        {
            IdentityReadCount++;
            Log.Add("identity-connection-type");
            connectionType = MotionControllerApi.ConnectionTypeEthernet;
            return MotionControllerApi.ResultSuccess;
        }

        public int InvokeRevisionInformation(IntPtr export, IntPtr handle, byte[] buffer)
        {
            IdentityReadCount++;
            Log.Add("identity-revision");
            return MotionControllerApi.ResultSuccess;
        }

        public int InvokeSerialNumber(IntPtr export, IntPtr handle, out uint serialNumber)
        {
            IdentityReadCount++;
            Log.Add("identity-serial");
            serialNumber = 1;
            return MotionControllerApi.ResultSuccess;
        }

        private static string ExportLabel(string exportName)
        {
            if (string.Equals(exportName, MotionControllerApi.ForceDirectModeExportName, StringComparison.Ordinal))
            {
                return "force-direct";
            }

            if (string.Equals(exportName, MotionControllerApi.OpenExportName, StringComparison.Ordinal))
            {
                return "open";
            }

            if (string.Equals(exportName, MotionControllerApi.CloseExportName, StringComparison.Ordinal))
            {
                return "close";
            }

            return "command";
        }
    }

    private sealed class FakeStopDispatchKernel : IStopDispatchKernel
    {
        private readonly FakeControllerKernel _controllerKernel;

        internal FakeStopDispatchKernel(FakeControllerKernel controllerKernel)
        {
            _controllerKernel = controllerKernel;
        }

        internal MotionStopNativeReply Reply { get; set; } =
            MotionStopNativeReply.Completed(MotionControllerApi.ResultSuccess, new byte[8]);
        internal List<MotionStopAxis> DispatchedAxes { get; } = new();
        internal bool ThrowOnDispose { get; set; }
        internal int InvalidationCount { get; private set; }
        public bool IsInvalidated { get; private set; }

        public MotionStopNativeReply Dispatch(MotionStopAxis axis)
        {
            _controllerKernel.Log.Add("dispatch");
            DispatchedAxes.Add(axis);
            return Reply;
        }

        public void Dispose()
        {
            InvalidationCount++;
            IsInvalidated = true;
            _controllerKernel.Log.Add(ThrowOnDispose ? "invalidate-failed" : "invalidate");
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException("synthetic invalidation failure");
            }
        }
    }

    private static readonly string SyntheticAddress = string.Join(".", 10, 20, 30, 40);

    private static readonly string[] ExpectedReleasedModuleOrder =
    {
        "module-primary-shape",
        "module-companion-shape",
    };

    private static readonly string[] ExpectedGateAOnlyLog =
    {
        "gate-a",
    };

    private static readonly string[] ExpectedCompanionOnlyReleasedModules =
    {
        "module-companion-shape",
    };

    private static readonly string[] ExpectedOpenRefusalCleanupOrder =
    {
        "invalidate",
        "close",
        "release-module-primary-shape",
        "release-module-companion-shape",
    };

    private static readonly string[] ExpectedInvalidationFailureTail =
    {
        "invalidate-failed",
    };

    private static readonly string[] ExpectedExportNames =
    {
        MotionControllerApi.ForceDirectModeExportName,
        MotionControllerApi.OpenExportName,
        MotionControllerApi.CloseExportName,
        MotionControllerCommandApi.CommandExportName,
    };

    private static readonly string[] ExpectedHappyPath =
    {
        "gate-a",
        "load-module-companion-shape",
        "path-module-companion-shape",
        "load-module-primary-shape",
        "path-module-primary-shape",
        "resolve-force-direct",
        "resolve-open",
        "resolve-close",
        "resolve-command",
        "force-direct",
        "open",
        "dispatch",
        "invalidate",
        "close",
        "release-module-primary-shape",
        "release-module-companion-shape",
    };

    private static MotionLibraryProbeConfiguration ProbeConfiguration()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wjss-stop-trial-shape"));
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

    private static MotionStopTrialSession CreateSession(
        MotionStopAxis axis,
        FakeControllerKernel kernel,
        FakeStopDispatchKernel dispatch,
        Func<MotionLibraryProbeOutcome>? gateAProbe = null)
    {
        return new MotionStopTrialSession(
            ProbeConfiguration(),
            EndpointConfiguration(),
            axis,
            () =>
            {
                kernel.Log.Add("gate-a");
                return gateAProbe?.Invoke() ?? MotionLibraryProbeOutcome.Complete("synthetic-version", "synthetic proof");
            },
            () => kernel,
            (_, _) => dispatch);
    }

    [Fact]
    public void Axis_A_Performs_One_Typed_Stop_And_Cleans_Up_In_Order()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var session = CreateSession(MotionStopAxis.A, kernel, dispatch);

        var outcome = session.Run();

        Assert.Equal(MotionStopTrialVocabulary.PreconditionAccepted, outcome.PreconditionCode);
        Assert.Equal(MotionStopTrialVocabulary.OwnerConsentAccepted, outcome.ConsentCode);
        Assert.Equal(MotionControllerIdentityVocabulary.EndpointConfigured, outcome.EndpointCode);
        Assert.Equal(MotionLibraryProbeVocabulary.ConfigurationRedacted, outcome.NativeConfigurationCode);
        Assert.Equal(MotionStopTrialVocabulary.NativeProofAccepted, outcome.NativeProofCode);
        Assert.Equal("A", outcome.Axis);
        Assert.True(outcome.OpenAttempted);
        Assert.Equal(MotionStopTrialVocabulary.OpenSucceeded, outcome.OpenClassification);
        Assert.Equal(MotionStopOutcomeCodes.Dispatched, outcome.StopCode);
        Assert.True(outcome.NativeDispatchAttempted);
        Assert.True(outcome.StopDispatched);
        Assert.False(outcome.StopConfirmed);
        Assert.Equal(MotionStopTrialVocabulary.CleanupComplete, outcome.CleanupCode);
        Assert.True(outcome.StopSessionInvalidated);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeModulesReleased);
        Assert.Equal(0, outcome.ExitCode);

        Assert.Equal(ExpectedHappyPath, kernel.Log);
        Assert.Equal(ExpectedExportNames, kernel.ResolvedExports);
        Assert.Equal(new[] { MotionStopAxis.A }, dispatch.DispatchedAxes);
        Assert.Equal(1, dispatch.InvalidationCount);
        Assert.Equal(1, kernel.DirectModeCount);
        Assert.Equal((byte)0, kernel.LastDirectModeByte);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(SyntheticAddress, kernel.LastAddress);
        Assert.Equal(0, kernel.IdentityReadCount);
        Assert.Equal(ExpectedReleasedModuleOrder, kernel.ReleasedModules);
    }

    [Fact]
    public void Axis_B_Maps_Only_To_The_Typed_Vertical_Stop()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.B, kernel, dispatch).Run();

        Assert.Equal("B", outcome.Axis);
        Assert.Equal(new[] { MotionStopAxis.B }, dispatch.DispatchedAxes);
        Assert.Equal(MotionStopOutcomeCodes.Dispatched, outcome.StopCode);
        Assert.True(outcome.StopDispatched);
        Assert.False(outcome.StopConfirmed);
    }

    [Fact]
    public void A_Second_Run_On_The_Same_Session_Is_Refused_Without_A_Second_Dispatch()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var session = CreateSession(MotionStopAxis.A, kernel, dispatch);

        var first = session.Run();
        var second = session.Run();

        Assert.True(first.StopDispatched);
        Assert.Equal(MotionStopTrialVocabulary.TrialAlreadyRun, second.RefusalCode);
        Assert.False(second.StopDispatched);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Single(dispatch.DispatchedAxes);
        Assert.Equal(1, dispatch.InvalidationCount);
    }

    [Fact]
    public void An_Out_Of_Domain_Axis_Is_Refused_Before_Gate_A_Or_Kernel_Creation()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var session = CreateSession((MotionStopAxis)5, kernel, dispatch);

        var outcome = session.Run();

        Assert.Equal(MotionStopTrialVocabulary.PreconditionRefused, outcome.PreconditionCode);
        Assert.Equal(MotionStopOutcomeCodes.AxisOutOfDomain, outcome.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.NativeProofNotAttempted, outcome.NativeProofCode);
        Assert.Null(outcome.Axis);
        Assert.Empty(kernel.Log);
        Assert.Empty(dispatch.DispatchedAxes);
    }

    [Fact]
    public void Axis_C_Is_A_Shared_Stop_Member_But_Is_Refused_By_The_Trial_Before_Gate_A()
    {
        // The shared Stop domain covers Axis C for Manual Motion; the Trial allowlist does not.
        // A direct C request must refuse before Gate A and must never be recorded as Axis B.
        Assert.True(MotionStopCommandPlan.IsDefined(MotionStopAxis.C));

        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var session = CreateSession(MotionStopAxis.C, kernel, dispatch);

        var outcome = session.Run();

        Assert.Equal(MotionStopTrialVocabulary.PreconditionRefused, outcome.PreconditionCode);
        Assert.Equal(MotionStopOutcomeCodes.AxisOutOfDomain, outcome.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.NativeProofNotAttempted, outcome.NativeProofCode);
        Assert.Null(outcome.Axis);
        Assert.NotEqual("B", outcome.Axis);
        Assert.Empty(kernel.Log);
        Assert.Equal(0, kernel.OpenCount);
        Assert.Empty(dispatch.DispatchedAxes);
    }

    [Fact]
    public void Axis_D_Is_A_Shared_Stop_Member_But_Is_Refused_By_The_Trial_Before_Gate_A()
    {
        Assert.True(MotionStopCommandPlan.IsDefined(MotionStopAxis.D));

        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var session = CreateSession(MotionStopAxis.D, kernel, dispatch);

        var outcome = session.Run();

        Assert.Equal(MotionStopTrialVocabulary.PreconditionRefused, outcome.PreconditionCode);
        Assert.Equal(MotionStopOutcomeCodes.AxisOutOfDomain, outcome.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.NativeProofNotAttempted, outcome.NativeProofCode);
        Assert.Null(outcome.Axis);
        Assert.NotEqual("B", outcome.Axis);
        Assert.Empty(kernel.Log);
        Assert.Equal(0, kernel.OpenCount);
        Assert.Empty(dispatch.DispatchedAxes);
    }

    [Fact]
    public void Gate_A_Refusal_Prevents_Controller_Module_Load_And_Reports_Its_Canonical_Code()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var refused = MotionLibraryProbeOutcome.Refused(
            MotionLibraryProbeVocabulary.NativeFileIdentityMismatch,
            "synthetic refusal",
            nativeHandlesReleased: true);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch, () => refused).Run();

        Assert.Equal(MotionLibraryProbeVocabulary.NativeFileIdentityMismatch, outcome.NativeProofCode);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeFileIdentityMismatch, outcome.RefusalCode);
        Assert.True(outcome.NativeModulesReleased);
        Assert.Equal(MotionStopTrialVocabulary.CleanupComplete, outcome.CleanupCode);
        Assert.Equal(ExpectedGateAOnlyLog, kernel.Log);
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.False(outcome.StopConfirmed);
    }

    [Fact]
    public void Gate_A_Unload_Failure_Is_Not_Masked_And_No_Second_Load_Is_Attempted()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var refused = MotionLibraryProbeOutcome.Refused(
            MotionLibraryProbeVocabulary.NativeUnloadFailed,
            "synthetic refusal",
            nativeHandlesReleased: false);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch, () => refused).Run();

        Assert.Equal(MotionLibraryProbeVocabulary.NativeUnloadFailed, outcome.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.CleanupIncomplete, outcome.CleanupCode);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeUnloadFailed, outcome.CleanupFailureCode);
        Assert.False(outcome.NativeModulesReleased);
        Assert.Equal(ExpectedGateAOnlyLog, kernel.Log);
        Assert.Empty(dispatch.DispatchedAxes);
    }

    [Fact]
    public void A_Thrown_Gate_A_Result_Fails_Closed_With_Unproven_Native_Cleanup()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var session = CreateSession(
            MotionStopAxis.A,
            kernel,
            dispatch,
            () => throw new InvalidOperationException("synthetic probe failure"));

        var outcome = session.Run();

        Assert.Equal(MotionStopTrialVocabulary.TrialAborted, outcome.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.NativeProofRefused, outcome.NativeProofCode);
        Assert.False(outcome.NativeModulesReleased);
        Assert.Equal(MotionStopTrialVocabulary.CleanupIncomplete, outcome.CleanupCode);
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.Equal(0, kernel.OpenCount);
    }

    [Fact]
    public void A_Loaded_Module_Path_Mismatch_Closes_No_Handle_And_Releases_What_Was_Loaded()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel);
        var session = CreateSession(MotionStopAxis.A, kernel, dispatch);
        kernel.LoadedPathOverrides[new IntPtr(101)] = Path.Combine(Path.GetTempPath(), "not-approved.dll");

        var outcome = session.Run();

        Assert.Equal(MotionLibraryProbeVocabulary.NativeModulePathMismatch, outcome.NativeProofCode);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeModulePathMismatch, outcome.RefusalCode);
        Assert.False(outcome.OpenAttempted);
        Assert.Equal(0, kernel.OpenCount);
        Assert.True(outcome.NativeModulesReleased);
        Assert.Equal(ExpectedCompanionOnlyReleasedModules, kernel.ReleasedModules);
        Assert.Empty(dispatch.DispatchedAxes);
    }

    [Fact]
    public void A_Missing_Command_Export_Prevents_Open_And_Stop_Dispatch()
    {
        var kernel = new FakeControllerKernel();
        kernel.MissingExports.Add(MotionControllerCommandApi.CommandExportName);
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.Equal(MotionStopOutcomeCodes.BoundaryUnavailable, outcome.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.NativeProofAccepted, outcome.NativeProofCode);
        Assert.False(outcome.OpenAttempted);
        Assert.Equal(0, kernel.DirectModeCount);
        Assert.Equal(0, kernel.OpenCount);
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.True(outcome.NativeModulesReleased);
        Assert.Equal(new[]
        {
            MotionControllerApi.ForceDirectModeExportName,
            MotionControllerApi.OpenExportName,
            MotionControllerApi.CloseExportName,
            MotionControllerCommandApi.CommandExportName,
        }, kernel.ResolvedExports);
    }

    [Fact]
    public void A_Missing_Lifecycle_Export_Stops_Before_Command_Resolution_And_Open()
    {
        var kernel = new FakeControllerKernel();
        kernel.MissingExports.Add(MotionControllerApi.OpenExportName);
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.B, kernel, dispatch).Run();

        Assert.Equal(MotionLibraryProbeVocabulary.NativeExportNotFound, outcome.RefusalCode);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeExportNotFound, outcome.NativeProofCode);
        Assert.DoesNotContain(MotionControllerCommandApi.CommandExportName, kernel.ResolvedExports);
        Assert.Equal(0, kernel.DirectModeCount);
        Assert.Equal(0, kernel.OpenCount);
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.True(outcome.NativeModulesReleased);
    }

    [Fact]
    public void Direct_Mode_Failure_Prevents_The_Only_Open()
    {
        var kernel = new FakeControllerKernel { ThrowOnDirectMode = true };
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.Equal(MotionControllerIdentityVocabulary.DirectModeNotEnforced, outcome.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.NativeProofAccepted, outcome.NativeProofCode);
        Assert.Equal(1, kernel.DirectModeCount);
        Assert.Equal(0, kernel.OpenCount);
        Assert.False(outcome.OpenAttempted);
        Assert.True(outcome.NativeModulesReleased);
        Assert.Empty(dispatch.DispatchedAxes);
    }

    [Fact]
    public void Open_Refusal_Is_Classified_Once_And_Never_Dispatched()
    {
        var kernel = new FakeControllerKernel { OpenResult = MotionControllerApi.ResultTimeout };
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.Equal(MotionControllerIdentityVocabulary.ConnectionTimeout, outcome.OpenClassification);
        Assert.Equal(MotionControllerIdentityVocabulary.ConnectionTimeout, outcome.RefusalCode);
        Assert.True(outcome.OpenAttempted);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(0, kernel.CloseCount);
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeModulesReleased);
    }

    [Fact]
    public void A_Handle_Returned_With_Open_Refusal_Is_Invalidated_Closed_Then_Unloaded()
    {
        var kernel = new FakeControllerKernel
        {
            OpenResult = MotionControllerApi.ResultTimeout,
            ReturnHandleOnRefusedOpen = true,
        };
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.Equal(MotionControllerIdentityVocabulary.ConnectionTimeout, outcome.RefusalCode);
        Assert.Equal(ExpectedOpenRefusalCleanupOrder, kernel.Log.TakeLast(4));
        Assert.Equal(1, dispatch.InvalidationCount);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.True(outcome.StopSessionInvalidated);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeModulesReleased);
    }

    [Fact]
    public void A_Thrown_Open_With_A_Returned_Handle_Still_Cleans_Up_Without_Dispatch()
    {
        var kernel = new FakeControllerKernel { ThrowOnOpenAfterSettingHandle = true };
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.B, kernel, dispatch).Run();

        Assert.Equal(MotionControllerIdentityVocabulary.ConnectionFailed, outcome.OpenClassification);
        Assert.Equal(MotionControllerIdentityVocabulary.ConnectionFailed, outcome.RefusalCode);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(1, dispatch.InvalidationCount);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeModulesReleased);
    }

    [Fact]
    public void A_Successful_Open_Without_A_Handle_Is_Refused_Without_Stop()
    {
        var kernel = new FakeControllerKernel { ReturnZeroHandleOnSuccess = true };
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.Equal(MotionControllerIdentityVocabulary.ConnectionHandleInvalid, outcome.RefusalCode);
        Assert.Equal(MotionStopTrialVocabulary.OpenSucceeded, outcome.OpenClassification);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(0, kernel.CloseCount);
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.True(outcome.NativeModulesReleased);
    }

    [Fact]
    public void A_Dispatch_Failure_Is_Not_Retried_And_Is_Not_Reported_As_Dispatched()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel) { Reply = MotionStopNativeReply.Faulted() };

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.Equal(MotionStopOutcomeCodes.DispatchFailed, outcome.StopCode);
        Assert.Equal(MotionStopOutcomeCodes.DispatchFailed, outcome.RefusalCode);
        Assert.True(outcome.NativeDispatchAttempted);
        Assert.False(outcome.StopDispatched);
        Assert.False(outcome.StopConfirmed);
        Assert.Single(dispatch.DispatchedAxes);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(1, kernel.CloseCount);
        Assert.True(outcome.NativeModulesReleased);
    }

    [Fact]
    public void A_Refusal_Marker_Is_Not_Retried_Or_Reclassified_As_Success()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel)
        {
            Reply = MotionStopNativeReply.Completed(
                MotionControllerApi.ResultSuccess,
                new byte[] { 0x3F, 0 }),
        };

        var outcome = CreateSession(MotionStopAxis.B, kernel, dispatch).Run();

        Assert.Equal(MotionStopOutcomeCodes.RejectedByController, outcome.StopCode);
        Assert.True(outcome.NativeDispatchAttempted);
        Assert.False(outcome.StopDispatched);
        Assert.Single(dispatch.DispatchedAxes);
        Assert.True(outcome.NativeModulesReleased);
    }

    [Fact]
    public void Close_And_Unload_Failures_Are_Reported_Without_Hiding_That_Dispatch_Occurred()
    {
        var kernel = new FakeControllerKernel
        {
            CloseResult = MotionControllerApi.ResultInternalError,
            ReleaseResult = false,
        };
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.True(outcome.StopDispatched);
        Assert.False(outcome.StopConfirmed);
        Assert.Equal(MotionStopTrialVocabulary.CleanupIncomplete, outcome.CleanupCode);
        Assert.Equal(MotionControllerIdentityVocabulary.CleanupUnproven, outcome.CleanupFailureCode);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.False(outcome.NativeModulesReleased);
        Assert.Equal(MotionStopOutcomeCodes.Dispatched, outcome.StopCode);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(ExpectedReleasedModuleOrder, kernel.ReleasedModules);
        Assert.Equal(2, kernel.ReleasedModules.Count);
    }

    [Fact]
    public void Failed_Boundary_Invalidation_Prevents_Close_And_Unload()
    {
        var kernel = new FakeControllerKernel();
        var dispatch = new FakeStopDispatchKernel(kernel) { ThrowOnDispose = true };

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.True(outcome.StopDispatched);
        Assert.Equal(MotionStopTrialVocabulary.CleanupIncomplete, outcome.CleanupCode);
        Assert.Equal(MotionControllerIdentityVocabulary.CleanupUnproven, outcome.CleanupFailureCode);
        Assert.False(outcome.StopSessionInvalidated);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.False(outcome.NativeModulesReleased);
        Assert.Equal(ExpectedInvalidationFailureTail, kernel.Log.TakeLast(1));
        Assert.Equal(0, kernel.CloseCount);
        Assert.Empty(kernel.ReleasedModules);
    }

    [Fact]
    public void A_Failed_Module_Release_Is_Attempted_Once_And_Reported()
    {
        var kernel = new FakeControllerKernel();
        kernel.FailedReleaseNames.Add("module-primary-shape");
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.Equal(MotionStopTrialVocabulary.CleanupIncomplete, outcome.CleanupCode);
        Assert.Equal(MotionControllerIdentityVocabulary.NativeUnloadFailed, outcome.CleanupFailureCode);
        Assert.False(outcome.NativeModulesReleased);
        Assert.Equal(2, kernel.ReleasedModules.Count);
        Assert.Equal(ExpectedReleasedModuleOrder, kernel.ReleasedModules);
    }

    [Fact]
    public void A_Throwing_Module_Load_Leaves_Cleanup_Unproven_Without_Retry()
    {
        var kernel = new FakeControllerKernel { ThrowOnLoadCall = 1 };
        var dispatch = new FakeStopDispatchKernel(kernel);

        var outcome = CreateSession(MotionStopAxis.A, kernel, dispatch).Run();

        Assert.Equal(MotionStopTrialVocabulary.NativeProofRefused, outcome.NativeProofCode);
        Assert.False(outcome.NativeModulesReleased);
        Assert.Equal(MotionStopTrialVocabulary.CleanupIncomplete, outcome.CleanupCode);
        Assert.Equal(1, kernel.Log.Count(item => item.StartsWith("load-", StringComparison.Ordinal)));
        Assert.Empty(dispatch.DispatchedAxes);
        Assert.Equal(0, kernel.OpenCount);
    }
}
