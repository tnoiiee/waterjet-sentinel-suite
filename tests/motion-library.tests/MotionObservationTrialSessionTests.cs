using System.Globalization;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Focused coverage of the B2-H one-shot observation trial composition WITHOUT any native module
/// and WITHOUT any controller: a recording kernel seam and a recording read kernel drive the whole
/// lifecycle (proof refusal, module load, export resolution, direct-mode ordering, single open,
/// the fixed sixteen-intent pass, boundary invalidation, close-before-unload, and cleanup truth).
///
/// Every fixture value is synthetic: no endpoint, serial, module path or hash in this file is a
/// real Owner-local value.
/// </summary>
public sealed class MotionObservationTrialSessionTests
{
    // ------------------------------------------------------------------ recording kernel seam

    private sealed class FakeControllerKernel : IControllerKernel
    {
        internal List<string> Log { get; } = new();

        internal Dictionary<IntPtr, string> ModulePaths { get; } = new();

        private IntPtr _nextModuleHandle = new(101);
        private IntPtr _nextExportHandle = new(501);

        internal byte LastForceByte = 1;
        internal string? LastAddress;
        internal int OpenCount;
        internal int CloseCount;

        internal int OpenResult;
        internal IntPtr OpenHandle = new(201);
        internal bool ReturnHandleOnRefusedOpen;
        internal int CloseResult;
        internal bool WithholdCommandExport;
        internal bool ThrowOnPrimaryLoad;
        internal bool ReleaseResult = true;
        internal List<string> ReleasedModuleFileNames { get; } = new();

        public IntPtr LoadApprovedModule(string approvedAbsolutePath)
        {
            Log.Add("load");

            if (ThrowOnPrimaryLoad && Log.FindAll(entry => entry == "load").Count > 1)
            {
                throw new InvalidOperationException("synthetic kernel failure");
            }

            var module = _nextModuleHandle;
            _nextModuleHandle = IntPtr.Add(_nextModuleHandle, 1);
            ModulePaths[module] = approvedAbsolutePath;
            return module;
        }

        public string? GetLoadedModulePath(IntPtr module)
        {
            Log.Add("module-path");
            return ModulePaths.TryGetValue(module, out var path) ? path : null;
        }

        public IntPtr ResolveExport(IntPtr module, string exportName)
        {
            Log.Add("resolve");

            if (WithholdCommandExport
                && string.Equals(exportName, MotionControllerCommandApi.CommandExportName, StringComparison.Ordinal))
            {
                return IntPtr.Zero;
            }

            var export = _nextExportHandle;
            _nextExportHandle = IntPtr.Add(_nextExportHandle, 1);
            return export;
        }

        public bool ReleaseModule(IntPtr module)
        {
            Log.Add("release");
            ReleasedModuleFileNames.Add(Path.GetFileName(ModulePaths[module]));
            return ReleaseResult;
        }

        public void InvokeForceDirectMode(IntPtr export, byte directOnly)
        {
            Log.Add("force");
            LastForceByte = directOnly;
        }

        public int InvokeOpen(IntPtr export, byte[] addressBuffer, out IntPtr handle)
        {
            Log.Add("open");
            OpenCount++;
            LastAddress = System.Text.Encoding.ASCII.GetString(addressBuffer, 0, addressBuffer.Length - 1);
            handle = OpenResult == 0 || ReturnHandleOnRefusedOpen ? OpenHandle : IntPtr.Zero;
            return OpenResult;
        }

        public int InvokeClose(IntPtr export, ref IntPtr handle)
        {
            Log.Add("close");
            CloseCount++;
            if (CloseResult == 0)
            {
                handle = IntPtr.Zero;
            }

            return CloseResult;
        }

        public int InvokeConnectionType(IntPtr export, IntPtr handle, out int connectionType)
        {
            Log.Add("connection-type");
            connectionType = 1;
            return 0;
        }

        public int InvokeRevisionInformation(IntPtr export, IntPtr handle, byte[] buffer)
        {
            Log.Add("revision");
            return 0;
        }

        public int InvokeSerialNumber(IntPtr export, IntPtr handle, out uint serialNumber)
        {
            Log.Add("serial");
            serialNumber = 0;
            return 0;
        }
    }

    // --------------------------------------------------------------- recording read boundary

    private sealed class FakeReadKernel : IObservationReadKernel
    {
        private readonly Queue<ObservationReadResult> _results = new();

        internal List<MotionObservationIntent> Issued { get; } = new();

        internal Action? OnInvalidated { get; set; }

        public bool IsInvalidated { get; private set; }

        internal FakeReadKernel Returns(ObservationReadResult result)
        {
            _results.Enqueue(result);
            return this;
        }

        public ObservationReadResult Issue(MotionObservationIntent intent)
        {
            Issued.Add(intent);
            return _results.Count > 0
                ? _results.Dequeue()
                : ObservationReadResult.FromValue("1.0000");
        }

        public void Dispose()
        {
            IsInvalidated = true;
            OnInvalidated?.Invoke();
        }
    }

    // ------------------------------------------------------------------------------- fixtures

    /// <summary>
    /// Synthetic address shape used by the composition tests, assembled from octets so this file
    /// carries no dotted literal. Not an Owner-local value.
    /// </summary>
    private static readonly string SyntheticAddress = string.Join(".", 10, 20, 30, 40);

    /// <summary>The deterministic release order: the primary module first, the companion second.</summary>
    private static readonly string[] ExpectedReleaseOrder =
    {
        "module-primary-shape",
        "module-companion-shape",
    };

    /// <summary>The exact load/proof sequence before the export resolution begins.</summary>
    private static readonly string[] ExpectedLoadSequence =
    {
        "load",
        "module-path",
        "load",
        "module-path",
    };

    /// <summary>The closed output namespace, one to four, in plan order.</summary>
    private static readonly int?[] ExpectedOutputNumbers = { 1, 2, 3, 4 };

    /// <summary>The first two intents the plan issues when the second read is refused.</summary>
    private static readonly MotionObservationIntent[] ExpectedFirstTwoIntents =
    {
        MotionObservationIntent.AxisStatusA,
        MotionObservationIntent.AxisStatusB,
    };

    private static MotionLibraryProbeConfiguration ProbeConfiguration()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wjss-motion-observation-shape"));
        return new MotionLibraryProbeConfiguration
        {
            NativeDirectory = directory,
            PrimaryModulePath = Path.Combine(directory, "module-primary-shape"),
            CompanionModulePath = Path.Combine(directory, "module-companion-shape"),
            ExpectedPrimaryHash = new byte[32],
            ExpectedCompanionHash = new byte[32],
            ExpectedVersion = "version-shape",
        };
    }

    private static MotionControllerEndpointConfiguration EndpointConfiguration()
    {
        var resolved = MotionControllerEndpointResolver.TryResolve(
            name => name == MotionControllerEndpointResolver.EndpointVariable
                ? SyntheticAddress
                : null,
            out var configuration,
            out var refusalCode,
            out _);
        if (!resolved)
        {
            throw new InvalidOperationException($"Synthetic endpoint shape refused: {refusalCode}");
        }

        return configuration;
    }

    private static MotionLibraryProbeOutcome ProbeAccepted() =>
        MotionLibraryProbeOutcome.Complete("version-shape", "synthetic proof");

    private static MotionObservationTrialSession Session(
        FakeControllerKernel kernel,
        FakeReadKernel readKernel,
        MotionLibraryProbeOutcome? probeOutcome = null) =>
        new(
            ProbeConfiguration(),
            EndpointConfiguration(),
            () => probeOutcome ?? ProbeAccepted(),
            () => kernel,
            (_, _) => readKernel);

    // --------------------------------------------------------------------------- happy path

    [Fact]
    public void The_Trial_Opens_Once_Reads_The_Sixteen_Intents_In_Order_And_Cleans_Up()
    {
        var kernel = new FakeControllerKernel();
        var readKernel = new FakeReadKernel();
        var outcome = Session(kernel, readKernel).Run();

        Assert.True(outcome.Accepted);
        Assert.Equal(MotionObservationTrialVocabulary.TrialComplete, outcome.Code);
        Assert.Equal(MotionObservationTrialVocabulary.OpenSucceeded, outcome.OpenClassification);
        Assert.Equal("MC-WSB1", outcome.ExpectedLogicalIdentity);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(ExpectedReleaseOrder, kernel.ReleasedModuleFileNames.ToArray());
        Assert.Equal(16, outcome.Readings.Count);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.True(outcome.ObservationBoundaryInvalidated);
    }

    [Fact]
    public void The_Trial_Executes_Exactly_The_Closed_Sixteen_Intent_Plan_In_Order()
    {
        var kernel = new FakeControllerKernel();
        var readKernel = new FakeReadKernel();
        _ = Session(kernel, readKernel).Run();

        Assert.Equal(16, MotionObservationCommandPlan.Intents.Count);
        Assert.Equal(MotionObservationCommandPlan.Intents.ToArray(), readKernel.Issued.ToArray());
    }

    [Fact]
    public void Every_Reading_Carries_Only_The_Approved_Model_Fields()
    {
        var kernel = new FakeControllerKernel();
        var outcome = Session(kernel, new FakeReadKernel()).Run();

        Assert.All(outcome.Readings, reading =>
        {
            var state = reading.State;
            Assert.NotNull(state);
            Assert.Equal(MotionObservedStateVocabulary.ObservationAccepted, reading.Code);
            Assert.Equal("1.0000", state!.NativeValueText);
            Assert.Contains(state.InterpretationState, MotionObservedStateVocabulary.ReachableInterpretationStates);
            Assert.NotEqual(MotionObservedStateVocabulary.InterpretationVerified, state.InterpretationState);
            Assert.NotEqual(MotionObservedStateVocabulary.InterpretationStale, state.InterpretationState);
        });

        var statuses = outcome.Readings.Take(4).Select(reading => reading.State!.InterpretationState).ToArray();
        var positions = outcome.Readings.Skip(4).Take(8).Select(reading => reading.State!.InterpretationState).ToArray();
        var outputs = outcome.Readings.Skip(12).Select(reading => reading.State!.InterpretationState).ToArray();

        Assert.All(statuses, value => Assert.Equal(MotionObservedStateVocabulary.InterpretationUnconfirmed, value));
        Assert.All(positions, value => Assert.Equal(MotionObservedStateVocabulary.InterpretationRawObserved, value));
        Assert.All(outputs, value => Assert.Equal(MotionObservedStateVocabulary.InterpretationUnconfirmed, value));
    }

    [Fact]
    public void The_Fixed_Plan_Covers_Only_Axes_A_To_D_And_Outputs_One_To_Four()
    {
        var kernel = new FakeControllerKernel();
        var outcome = Session(kernel, new FakeReadKernel()).Run();

        // The boundary between the axis group and the output group is pinned by position, not by
        // a collection-size equality: reading twelve is the last axis reading and reading thirteen
        // is the first output reading.
        Assert.Equal(12, outcome.Readings[11].Ordinal);
        Assert.Equal(MotionObservedStateVocabulary.KindActualPosition, outcome.Readings[11].State!.Kind);
        Assert.Equal(13, outcome.Readings[12].Ordinal);
        Assert.Equal(MotionObservedStateVocabulary.KindDigitalOutput, outcome.Readings[12].State!.Kind);

        Assert.All(outcome.Readings.Take(12), reading =>
        {
            Assert.Contains(reading.State!.AxisId, MotionObservedStateVocabulary.AxisIds);
            Assert.Null(reading.State.OutputNumber);
            Assert.NotEqual(MotionObservedStateVocabulary.KindDigitalOutput, reading.State.Kind);
        });

        Assert.Equal(
            ExpectedOutputNumbers,
            outcome.Readings.Skip(12).Select(reading => reading.State!.OutputNumber).ToArray());
        Assert.All(
            outcome.Readings.Skip(12),
            reading =>
            {
                Assert.Equal(MotionObservedStateVocabulary.NoAxis, reading.State!.AxisId);
                Assert.Equal(MotionObservedStateVocabulary.KindDigitalOutput, reading.State.Kind);
            });
    }

    // ------------------------------------------------------------------------- native order

    [Fact]
    public void The_Direct_Mode_Call_Precedes_The_Single_Open()
    {
        var kernel = new FakeControllerKernel();
        _ = Session(kernel, new FakeReadKernel()).Run();

        Assert.Equal(0, kernel.LastForceByte);
        Assert.True(kernel.Log.IndexOf("force") < kernel.Log.IndexOf("open"));
        Assert.Equal(1, kernel.OpenCount);
        var forceCalls = kernel.Log.FindAll(entry => entry == "force");
        Assert.Equal("force", Assert.Single(forceCalls));
    }

    [Fact]
    public void The_Open_Receives_The_Validated_Address_Only()
    {
        var kernel = new FakeControllerKernel();
        _ = Session(kernel, new FakeReadKernel()).Run();

        Assert.Equal(SyntheticAddress, kernel.LastAddress);
        Assert.DoesNotContain("--", kernel.LastAddress!, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", kernel.LastAddress!, StringComparison.Ordinal);
        Assert.DoesNotContain(":", kernel.LastAddress!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Observation_Boundary_Is_Invalidated_Before_The_Close_And_Release()
    {
        var kernel = new FakeControllerKernel();
        var readKernel = new FakeReadKernel
        {
            OnInvalidated = () => kernel.Log.Add("invalidate"),
        };

        var outcome = Session(kernel, readKernel).Run();

        Assert.True(outcome.Accepted);
        var invalidatedAt = kernel.Log.IndexOf("invalidate");
        var closedAt = kernel.Log.LastIndexOf("close");
        var releasedAt = kernel.Log.LastIndexOf("release");

        Assert.True(invalidatedAt >= 0);
        Assert.True(invalidatedAt < closedAt);
        Assert.True(closedAt < releasedAt);
        Assert.True(readKernel.IsInvalidated);
    }

    [Fact]
    public void A_Second_Run_Is_Refused_As_One_Shot()
    {
        var kernel = new FakeControllerKernel();
        var session = Session(kernel, new FakeReadKernel());

        var first = session.Run();
        var second = session.Run();

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Equal(MotionObservationTrialVocabulary.TrialAlreadyRun, second.Code);
        Assert.Equal(string.Empty, second.OpenClassification);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(1, kernel.CloseCount);
    }

    // ------------------------------------------------------------------------ refusal paths

    [Fact]
    public void Gate_A_Refusal_Prevents_Load_Open_And_Read()
    {
        var kernel = new FakeControllerKernel();
        var readKernel = new FakeReadKernel();
        var refusedProbe = MotionLibraryProbeOutcome.Refused("NATIVE_LOAD_FAILED", "synthetic", true);

        var outcome = Session(kernel, readKernel, refusedProbe).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionControllerIdentityVocabulary.NativeProofRefused, outcome.Code);
        Assert.Empty(kernel.Log);
        Assert.Empty(readKernel.Issued);
        Assert.Equal(string.Empty, outcome.OpenClassification);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
    }

    [Fact]
    public void A_Missing_Read_Export_Refuses_Before_The_Open()
    {
        var kernel = new FakeControllerKernel { WithholdCommandExport = true };
        var outcome = Session(kernel, new FakeReadKernel()).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionObservationTrialVocabulary.ObservationBoundaryUnavailable, outcome.Code);
        Assert.Equal(0, kernel.OpenCount);
        Assert.DoesNotContain("force", kernel.Log);
        Assert.DoesNotContain("close", kernel.Log);
        Assert.Equal(ExpectedLoadSequence, kernel.Log.Take(4).ToArray());
        Assert.Equal(ExpectedReleaseOrder, kernel.ReleasedModuleFileNames.ToArray());
        Assert.True(outcome.NativeHandlesReleased);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.Equal(string.Empty, outcome.OpenClassification);
    }

    [Fact]
    public void An_Open_Refusal_Uses_The_Canonical_Classification()
    {
        var kernel = new FakeControllerKernel { OpenResult = 6 };
        var readKernel = new FakeReadKernel();
        var outcome = Session(kernel, readKernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenNotConnected, outcome.Code);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenNotConnected, outcome.OpenClassification);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(0, kernel.CloseCount);
        Assert.Empty(readKernel.Issued);
        Assert.True(outcome.ObservationBoundaryInvalidated);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Equal(ExpectedReleaseOrder, kernel.ReleasedModuleFileNames.ToArray());
    }

    [Fact]
    public void An_Unrecognised_Open_Result_Is_Never_Emitted_Numerically()
    {
        var kernel = new FakeControllerKernel { OpenResult = 42 };
        var outcome = Session(kernel, new FakeReadKernel()).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenResultUnrecognised, outcome.Code);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenResultUnrecognised, outcome.OpenClassification);
        Assert.DoesNotContain("42", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("42", outcome.OpenClassification, StringComparison.Ordinal);
        Assert.Equal(1, kernel.OpenCount);
    }

    [Fact]
    public void An_Open_Without_A_Successful_Handle_Is_Refused_Without_A_Read()
    {
        var kernel = new FakeControllerKernel { OpenResult = 0, OpenHandle = IntPtr.Zero };
        var readKernel = new FakeReadKernel();
        var outcome = Session(kernel, readKernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionControllerIdentityVocabulary.ConnectionHandleInvalid, outcome.Code);
        Assert.Equal(MotionObservationTrialVocabulary.OpenSucceeded, outcome.OpenClassification);
        Assert.Empty(readKernel.Issued);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(0, kernel.CloseCount);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.Equal(ExpectedReleaseOrder, kernel.ReleasedModuleFileNames.ToArray());
    }

    [Fact]
    public void A_Handle_Returned_With_A_Refused_Open_Is_Closed_And_Reported_Truthfully()
    {
        var kernel = new FakeControllerKernel
        {
            OpenResult = 6,
            ReturnHandleOnRefusedOpen = true,
        };

        var outcome = Session(kernel, new FakeReadKernel()).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenNotConnected, outcome.Code);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenNotConnected, outcome.OpenClassification);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(ExpectedReleaseOrder, kernel.ReleasedModuleFileNames.ToArray());
    }

    [Fact]
    public void A_Refused_Read_Stops_The_Remaining_Sequence_Without_Retry()
    {
        var kernel = new FakeControllerKernel();
        var readKernel = new FakeReadKernel()
            .Returns(ObservationReadResult.FromValue("1.0000"))
            .Returns(ObservationReadResult.Failed(
                MotionObservedStateVocabulary.ObservationResponseUnterminated));

        var outcome = Session(kernel, readKernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionObservedStateVocabulary.ObservationResponseUnterminated, outcome.Code);
        Assert.Equal(ExpectedFirstTwoIntents, readKernel.Issued.ToArray());
        Assert.Equal(2, outcome.Readings.Count);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(1, kernel.CloseCount);
        Assert.True(outcome.ObservationBoundaryInvalidated);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.Equal(MotionObservationTrialVocabulary.OpenSucceeded, outcome.OpenClassification);
    }

    [Fact]
    public void An_Out_Of_Range_Output_State_Is_Reported_Unavailable_Not_Repaired()
    {
        var kernel = new FakeControllerKernel();
        var readKernel = new FakeReadKernel();
        for (var index = 0; index < 12; index++)
        {
            _ = readKernel.Returns(ObservationReadResult.FromValue("1.0000"));
        }

        _ = readKernel.Returns(ObservationReadResult.FromValue("2.0000"));

        var outcome = Session(kernel, readKernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionObservedStateVocabulary.ObservationOutputStateOutOfRange, outcome.Code);
        Assert.Equal(MotionObservationIntent.DigitalOutput1, readKernel.Issued[^1]);
        Assert.Equal(13, outcome.Readings.Count);
        var last = outcome.Readings[12].State;
        Assert.NotNull(last);
        Assert.Equal(
            MotionObservedStateVocabulary.InterpretationUnavailable,
            last!.InterpretationState);
        Assert.Equal(
            MotionObservedStateVocabulary.ObservationOutputStateOutOfRange,
            last.DetailCode);
        Assert.Null(last.NativeValueText);
    }

    [Fact]
    public void A_Close_Failure_Prevents_Acceptance()
    {
        var kernel = new FakeControllerKernel { CloseResult = 1 };
        var outcome = Session(kernel, new FakeReadKernel()).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionControllerIdentityVocabulary.CleanupUnproven, outcome.Code);
        Assert.Equal(MotionObservationTrialVocabulary.OpenSucceeded, outcome.OpenClassification);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Contains(
            "controller handle close could not be confirmed",
            outcome.Detail,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_Refused_Open_With_A_Failed_Close_Keeps_Its_Classification()
    {
        // The open is refused and yet returns a handle that cannot be closed: the overall code and
        // the classification stay the canonical refusal word, and only the cleanup truth changes.
        var kernel = new FakeControllerKernel
        {
            OpenResult = 6,
            ReturnHandleOnRefusedOpen = true,
            CloseResult = 1,
        };

        var outcome = Session(kernel, new FakeReadKernel()).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenNotConnected, outcome.Code);
        Assert.Equal(MotionControllerIdentityVocabulary.OpenNotConnected, outcome.OpenClassification);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.Equal(1, kernel.CloseCount);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Contains(
            "controller handle close could not be confirmed",
            outcome.Detail,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_Unload_Failure_Prevents_Acceptance()
    {
        var kernel = new FakeControllerKernel { ReleaseResult = false };
        var outcome = Session(kernel, new FakeReadKernel()).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionControllerIdentityVocabulary.NativeUnloadFailed, outcome.Code);
        Assert.Equal(MotionObservationTrialVocabulary.OpenSucceeded, outcome.OpenClassification);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.False(outcome.NativeHandlesReleased);
    }

    [Fact]
    public void An_Unexpected_Managed_Failure_Is_Refused_After_Cleanup()
    {
        var kernel = new FakeControllerKernel { ThrowOnPrimaryLoad = true };
        var readKernel = new FakeReadKernel();

        var outcome = Session(kernel, readKernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionObservationTrialVocabulary.TrialAborted, outcome.Code);
        Assert.Empty(readKernel.Issued);
        Assert.True(outcome.ObservationBoundaryInvalidated);
        Assert.True(outcome.NativeHandlesReleased);
        var releasedModule = Assert.Single(kernel.ReleasedModuleFileNames);
        Assert.Equal("module-companion-shape", releasedModule);
        Assert.DoesNotContain("synthetic kernel failure", outcome.Detail, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------- redaction

    [Fact]
    public void The_Outcome_Model_Carries_No_Endpoint_Path_Hash_Or_Request_Text()
    {
        var kernel = new FakeControllerKernel();
        var outcome = Session(kernel, new FakeReadKernel()).Run();

        var readings = string.Join(
            "|",
            outcome.Readings.Select(reading =>
                reading.Ordinal.ToString(CultureInfo.InvariantCulture)
                + ":" + reading.Code
                + ":" + reading.Accepted
                + ":" + reading.Generation.ToString(CultureInfo.InvariantCulture)
                + ":" + reading.State?.Kind
                + ":" + reading.State?.AxisId
                + ":" + reading.State?.OutputNumber?.ToString(CultureInfo.InvariantCulture)
                + ":" + reading.State?.NativeValueText
                + ":" + reading.State?.NativeUnit
                + ":" + reading.State?.InterpretationState
                + ":" + reading.State?.DetailCode));

        var text = outcome.Code
            + "|" + outcome.Detail
            + "|" + outcome.ExpectedLogicalIdentity
            + "|" + outcome.OpenClassification
            + "|" + outcome.Generation.ToString(CultureInfo.InvariantCulture)
            + "|" + outcome.ObservationBoundaryInvalidated
            + "|" + outcome.ControllerHandleClosed
            + "|" + outcome.NativeHandlesReleased
            + "|" + readings;

        Assert.DoesNotContain(SyntheticAddress, text, StringComparison.Ordinal);
        Assert.DoesNotContain(string.Join(".", 10, 20, 30), text, StringComparison.Ordinal);
        Assert.DoesNotContain(MotionControllerEndpointResolver.EndpointVariable, text, StringComparison.Ordinal);
        Assert.DoesNotContain("module-primary", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("module-companion", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", text);
        Assert.DoesNotMatch(@"\b[0-9A-Fa-f]{32,}\b", text);
        Assert.DoesNotContain("@OUT[", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WJSS_", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Trial_Surface_Accepts_No_String_And_No_Raw_Command()
    {
        var publicSurface = typeof(MotionObservationTrialSession)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(MotionObservationTrialSession))
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.NotEmpty(publicSurface);

        foreach (var method in publicSurface)
        {
            Assert.DoesNotContain(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(string));
        }

        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "adapters",
            "motion-library",
            "MotionObservationTrialSession.cs"));

        Assert.DoesNotContain("string command", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StringBuilder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.", source, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
