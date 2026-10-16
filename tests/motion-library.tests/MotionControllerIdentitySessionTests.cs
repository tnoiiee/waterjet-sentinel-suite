using System.Text;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Complete B1 identity-session defect-class coverage WITHOUT any native module and WITHOUT any
/// controller: a recording kernel seam drives every phase (proof refusal, precondition refusal,
/// direct-mode ordering, single open, fixed read order, response-shape validation, model
/// confirmation policy, close-on-every-path, unload truth, budgets, busy and stale-generation
/// fencing). No test here contacts anything, and no committed value is a real endpoint, serial,
/// path or hash.
/// </summary>
public sealed class MotionControllerIdentitySessionTests
{
    // ------------------------------------------------------------------ recording kernel seam

    private sealed class FakeControllerKernel : IControllerKernel
    {
        public readonly List<string> Log = new();
        public readonly Dictionary<IntPtr, string> ModulePaths = new();
        public IntPtr NextModuleHandle = new(101);
        public IntPtr NextExportHandle = new(501);

        public byte LastForceByte = 1;
        public string? LastAddress;
        public int OpenCount;
        public int CloseCount;

        public int OpenResult = 0;
        public IntPtr OpenHandle = new(201);
        public int CloseResult = 0;
        public bool CloseThrows;
        public bool CloseZeroesHandle = true;

        public int ConnectionTypeResult = 0;
        public int ConnectionTypeValue = 1;
        public Action? OnConnectionType;

        public int RevisionResult = 0;
        public byte[]? RevisionPayload;
        public Action? OnRevision;

        public int SerialResult = 0;
        public uint SerialValue = 12345;

        public bool ReleaseResult = true;
        public readonly List<string> ReleasedModuleFileNames = new();
        public Func<string, bool>? LoadFailure;
        public Action? OnOpen;

        public IntPtr LoadApprovedModule(string approvedAbsolutePath)
        {
            Log.Add("load");
            if (LoadFailure is not null && LoadFailure(approvedAbsolutePath))
            {
                return IntPtr.Zero;
            }

            var module = NextModuleHandle;
            NextModuleHandle = IntPtr.Add(NextModuleHandle, 1);
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
            Log.Add("resolve:" + exportName);
            var export = NextExportHandle;
            NextExportHandle = IntPtr.Add(NextExportHandle, 1);
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
            LastAddress = Encoding.ASCII.GetString(addressBuffer, 0, addressBuffer.Length - 1);
            OnOpen?.Invoke();
            handle = OpenResult == 0 ? OpenHandle : IntPtr.Zero;
            return OpenResult;
        }

        public int InvokeClose(IntPtr export, ref IntPtr handle)
        {
            Log.Add("close");
            CloseCount++;
            if (CloseThrows)
            {
                throw new InvalidOperationException("synthetic close failure");
            }

            if (CloseZeroesHandle && CloseResult == 0)
            {
                handle = IntPtr.Zero;
            }

            return CloseResult;
        }

        public int InvokeConnectionType(IntPtr export, IntPtr handle, out int connectionType)
        {
            Log.Add("connection-type");
            connectionType = ConnectionTypeValue;
            OnConnectionType?.Invoke();
            return ConnectionTypeResult;
        }

        public int InvokeRevisionInformation(IntPtr export, IntPtr handle, byte[] buffer)
        {
            Log.Add("revision");
            OnRevision?.Invoke();
            if (RevisionResult == 0)
            {
                var payload = RevisionPayload ?? Encoding.ASCII.GetBytes("DMC-B140-M Rev 1.2.3\0");
                Array.Copy(payload, buffer, Math.Min(payload.Length, buffer.Length));
            }

            return RevisionResult;
        }

        public int InvokeSerialNumber(IntPtr export, IntPtr handle, out uint serialNumber)
        {
            Log.Add("serial");
            serialNumber = SerialValue;
            return SerialResult;
        }
    }

    // ------------------------------------------------------------------------------- fixtures

    private static MotionLibraryProbeConfiguration ProbeConfiguration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wjss-motion-native-shape");
        return new MotionLibraryProbeConfiguration
        {
            NativeDirectory = directory,
            PrimaryModulePath = Path.Combine(directory, "gclib.dll"),
            CompanionModulePath = Path.Combine(directory, "gclibo.dll"),
            ExpectedPrimaryHash = new byte[32],
            ExpectedCompanionHash = new byte[32],
            ExpectedVersion = "test-version-shape",
        };
    }

    private static MotionControllerEndpointConfiguration EndpointConfiguration() =>
        new() { EndpointValue = string.Join(".", 10, 20, 30, 40) };

    private static MotionLibraryProbeOutcome ProbeAccepted() =>
        MotionLibraryProbeOutcome.Complete("test-version-shape", "synthetic proof");

    private static MotionControllerIdentitySession Session(
        FakeControllerKernel kernel,
        MotionLibraryProbeOutcome? probeOutcome = null,
        ControllerAuthorityPrecondition? precondition = null,
        Func<long>? elapsed = null,
        Action<MotionControllerIdentitySession>? wire = null)
    {
        MotionControllerIdentitySession? created = null;
        created = new MotionControllerIdentitySession(
            ProbeConfiguration(),
            EndpointConfiguration(),
            () => probeOutcome ?? ProbeAccepted(),
            () => kernel,
            () => precondition ?? ControllerAuthorityPrecondition.Allow(),
            elapsed ?? (() => 0L));
        wire?.Invoke(created);
        return created;
    }

    // ---------------------------------------------------------------------------- happy path

    [Fact]
    public void Happy_Path_Opens_Once_Reads_In_Order_Redacts_And_Cleans_Up()
    {
        var kernel = new FakeControllerKernel();
        var outcome = Session(kernel).Run();

        Assert.True(outcome.Accepted);
        Assert.Equal("IDENTITY_SESSION_COMPLETE", outcome.Code);
        Assert.Equal("MODEL_MATCHED", outcome.ModelComparison);
        Assert.Equal("GCLIB_ETHERNET", outcome.ConnectionTypeStatus);
        Assert.Equal("SERIAL_OBSERVED_REDACTED", outcome.SerialStatus);
        Assert.Equal("FIRMWARE_OBSERVED", outcome.FirmwareStatus);
        Assert.Equal("MC-WSB1", outcome.ExpectedLogicalIdentity);
        Assert.Equal("OWNER_MANUAL_CONFIRMATION_REQUIRED", outcome.LogicalIdentityStatus);
        Assert.Equal("DMC-B140-M Rev 1.2.3", outcome.RevisionInformation);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(1, kernel.CloseCount);
        Assert.True(kernel.Log.IndexOf("close") < kernel.Log.IndexOf("release"));
        Assert.Equal(
            new[]
            {
                MotionLibraryProbeVocabulary.PrimaryModuleFileName,
                MotionLibraryProbeVocabulary.CompanionModuleFileName,
            },
            kernel.ReleasedModuleFileNames);
        Assert.True(kernel.Log.IndexOf("force") < kernel.Log.IndexOf("open"));
        Assert.True(kernel.Log.IndexOf("open") < kernel.Log.IndexOf("connection-type"));
        Assert.True(kernel.Log.IndexOf("connection-type") < kernel.Log.IndexOf("revision"));
        Assert.True(kernel.Log.IndexOf("revision") < kernel.Log.IndexOf("serial"));
        Assert.True(kernel.Log.IndexOf("serial") < kernel.Log.IndexOf("close"));

        // Direct mode is enforced with the zero byte (no gcaps) and the open-address text is the
        // documented direct form with the approved timeout.
        Assert.Equal(0, kernel.LastForceByte);
        Assert.Equal(
            string.Join(".", 10, 20, 30, 40) + " --direct --timeout 2000",
            kernel.LastAddress);

        // The serial value never appears in any renderable outcome field.
        Assert.DoesNotContain("12345", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("12345", outcome.SerialStatus ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(string.Join(".", 10, 20, 30, 40), outcome.Detail, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------- refusal pre-conditions

    [Fact]
    public void Precondition_Refusal_Stops_Before_Anything_Is_Loaded()
    {
        var kernel = new FakeControllerKernel();
        var outcome = Session(kernel, precondition: new ControllerAuthorityPrecondition
        {
            Allowed = false,
            RefusalCode = "CONCURRENT_CONTROLLER_AUTHORITY_DETECTED",
            RefusalDetail = "synthetic refusal",
        }).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CONCURRENT_CONTROLLER_AUTHORITY_DETECTED", outcome.Code);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Equal(0, kernel.CloseCount);
        Assert.Empty(kernel.ReleasedModuleFileNames);
        Assert.Empty(kernel.Log);
    }

    [Fact]
    public void Native_Proof_Refusal_Prevents_Any_Connection_Attempt()
    {
        var kernel = new FakeControllerKernel();
        var refusedProbe = MotionLibraryProbeOutcome.Refused("NATIVE_LOAD_FAILED", "synthetic", true);
        var outcome = Session(kernel, probeOutcome: refusedProbe).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("NATIVE_PROOF_REFUSED", outcome.Code);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Equal(0, kernel.CloseCount);
        Assert.Empty(kernel.ReleasedModuleFileNames);
        Assert.Empty(kernel.Log);
    }

    // ------------------------------------------------------------------------- open lifecycle

    [Fact]
    public void Open_Timeout_Is_Classified_And_Never_Retried()
    {
        var kernel = new FakeControllerKernel { OpenResult = 3 };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CONNECTION_TIMEOUT", outcome.Code);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(0, kernel.CloseCount);
    }

    [Fact]
    public void Open_Failure_Is_Classified_And_Never_Retried()
    {
        var kernel = new FakeControllerKernel { OpenResult = 6 };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CONNECTION_FAILED", outcome.Code);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Equal(1, kernel.OpenCount);
        Assert.Equal(0, kernel.CloseCount);
    }

    // ------------------------------------------------------------------------------ read rules

    [Fact]
    public void Non_Ethernet_Connection_Type_Refuses_But_Still_Closes()
    {
        var kernel = new FakeControllerKernel { ConnectionTypeValue = 2 };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CONNECTION_TYPE_MISMATCH", outcome.Code);
        Assert.Equal("GCLIB_SERIAL", outcome.ConnectionTypeStatus);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Equal(1, kernel.CloseCount);
    }

    [Fact]
    public void Unterminated_Revision_Buffer_Refuses()
    {
        var kernel = new FakeControllerKernel
        {
            // Fills the whole bounded buffer with printable bytes and never terminates: the
            // response-shape proof must refuse it.
            RevisionPayload = Enumerable.Repeat((byte)'A', MotionControllerApi.RevisionBufferLength).ToArray(),
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("REVISION_RESPONSE_INVALID", outcome.Code);
        Assert.True(outcome.ControllerHandleClosed);
    }

    [Fact]
    public void Non_Printable_Revision_Buffer_Refuses()
    {
        var kernel = new FakeControllerKernel
        {
            RevisionPayload = new byte[] { 0x41, 0x01, 0x42, 0x00 },
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("REVISION_RESPONSE_INVALID", outcome.Code);
    }

    [Fact]
    public void Empty_Revision_Buffer_Refuses()
    {
        var kernel = new FakeControllerKernel
        {
            RevisionPayload = new byte[] { 0x00 },
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("REVISION_RESPONSE_INVALID", outcome.Code);
    }

    [Fact]
    public void Zero_Serial_Refuses_And_The_Value_Is_Never_Rendered()
    {
        var kernel = new FakeControllerKernel { SerialValue = 0 };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("SERIAL_RESPONSE_INVALID", outcome.Code);
        Assert.Equal("DMC-B140-M Rev 1.2.3", outcome.RevisionInformation);
    }

    [Fact]
    public void Missing_Model_Token_Requires_Model_Confirmation_Never_A_False_Rejection()
    {
        var kernel = new FakeControllerKernel
        {
            RevisionPayload = Encoding.ASCII.GetBytes("OTHER Rev 9.9\0"),
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("MODEL_CONFIRMATION_REQUIRED", outcome.Code);
        Assert.Equal("MODEL_CONFIRMATION_REQUIRED", outcome.ModelComparison);
        Assert.Equal("OTHER Rev 9.9", outcome.RevisionInformation);
        Assert.Equal("SERIAL_OBSERVED_REDACTED", outcome.SerialStatus);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
    }

    // --------------------------------------------------------------------- cleanup truth rules

    [Fact]
    public void Close_Failure_Is_Reported_Truthfully_On_An_Otherwise_Successful_Session()
    {
        var kernel = new FakeControllerKernel { CloseResult = 1 };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CLEANUP_UNPROVEN", outcome.Code);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Contains("controller handle close could not be confirmed", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(2, kernel.ReleasedModuleFileNames.Count);
        Assert.Equal("DMC-B140-M Rev 1.2.3", outcome.RevisionInformation);
        Assert.Equal("MODEL_MATCHED", outcome.ModelComparison);
    }

    [Fact]
    public void Close_Exception_Is_Reported_Truthfully()
    {
        var kernel = new FakeControllerKernel { CloseThrows = true };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CLEANUP_UNPROVEN", outcome.Code);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Contains("controller handle close could not be confirmed", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(2, kernel.ReleasedModuleFileNames.Count);
    }

    [Fact]
    public void Accepted_Identity_Is_Refused_And_Both_Cleanup_Failures_Are_Reported()
    {
        var kernel = new FakeControllerKernel
        {
            CloseResult = 1,
            ReleaseResult = false,
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CLEANUP_UNPROVEN", outcome.Code);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.False(outcome.NativeHandlesReleased);
        Assert.Contains("controller handle close could not be confirmed", outcome.Detail, StringComparison.Ordinal);
        Assert.Contains("Deterministic cleanup of the loaded native handles could not be", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(2, kernel.ReleasedModuleFileNames.Count);
    }

    [Fact]
    public void Primary_Read_Refusal_Is_Preserved_When_Close_Fails()
    {
        var kernel = new FakeControllerKernel
        {
            ConnectionTypeValue = 2,
            CloseResult = 1,
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CONNECTION_TYPE_MISMATCH", outcome.Code);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Contains("controller handle close could not be confirmed", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(2, kernel.ReleasedModuleFileNames.Count);
    }

    [Fact]
    public void Primary_Read_Refusal_Is_Preserved_When_Unload_Fails()
    {
        var kernel = new FakeControllerKernel
        {
            ConnectionTypeValue = 2,
            ReleaseResult = false,
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CONNECTION_TYPE_MISMATCH", outcome.Code);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.False(outcome.NativeHandlesReleased);
        Assert.Contains("Deterministic cleanup of the loaded native handles could not be", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(2, kernel.ReleasedModuleFileNames.Count);
    }

    [Fact]
    public void Primary_Read_Refusal_Reports_Both_Close_And_Unload_Failures()
    {
        var kernel = new FakeControllerKernel
        {
            ConnectionTypeValue = 2,
            CloseResult = 1,
            ReleaseResult = false,
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("CONNECTION_TYPE_MISMATCH", outcome.Code);
        Assert.False(outcome.ControllerHandleClosed);
        Assert.False(outcome.NativeHandlesReleased);
        Assert.Contains("controller handle close could not be confirmed", outcome.Detail, StringComparison.Ordinal);
        Assert.Contains("Deterministic cleanup of the loaded native handles could not be", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(2, kernel.ReleasedModuleFileNames.Count);
    }

    [Fact]
    public void Revision_Read_Exception_Still_Closes_And_Unloads()
    {
        var kernel = new FakeControllerKernel
        {
            OnRevision = () => throw new InvalidOperationException("synthetic revision read failure"),
        };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("REVISION_RESPONSE_INVALID", outcome.Code);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(2, kernel.ReleasedModuleFileNames.Count);
        Assert.True(kernel.Log.IndexOf("close") < kernel.Log.IndexOf("release"));
    }

    [Fact]
    public void Native_Unload_Failure_Is_Reported_Truthfully()
    {
        var kernel = new FakeControllerKernel { ReleaseResult = false };
        var outcome = Session(kernel).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("NATIVE_UNLOAD_FAILED", outcome.Code);
        Assert.False(outcome.NativeHandlesReleased);
        Assert.True(outcome.ControllerHandleClosed);
        Assert.Contains("native module handle could not be released", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal(1, kernel.CloseCount);
        Assert.Equal(2, kernel.ReleasedModuleFileNames.Count);
    }

    // ------------------------------------------------------------- budgets, busy and fencing

    [Fact]
    public void Lifecycle_Budget_Exceeded_Is_Refused()
    {
        var kernel = new FakeControllerKernel();
        var outcome = Session(kernel, elapsed: () => 20000L).Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("LIFECYCLE_BUDGET_EXCEEDED", outcome.Code);
        Assert.Empty(kernel.Log);
    }

    [Fact]
    public void The_Native_Slot_Is_Fail_Fast_Busy()
    {
        var kernel = new FakeControllerKernel();
        var session = Session(kernel);
        Assert.True(session.TryBeginNativeOperation());
        Assert.False(session.TryBeginNativeOperation());
        session.EndNativeOperation();
        Assert.True(session.TryBeginNativeOperation());
        session.EndNativeOperation();
    }

    [Fact]
    public void A_Second_Run_Is_Refused_As_One_Shot()
    {
        var kernel = new FakeControllerKernel();
        var session = Session(kernel);
        Assert.True(session.Run().Accepted);

        var second = session.Run();
        Assert.False(second.Accepted);
        Assert.Equal("OPERATION_BUSY", second.Code);
        Assert.Equal(1, kernel.OpenCount);
    }

    [Fact]
    public void A_Late_Result_From_A_Previous_Generation_Is_Rejected()
    {
        var kernel = new FakeControllerKernel();
        MotionControllerIdentitySession? session = null;
        session = Session(kernel, wire: s => session = s);
        kernel.OnConnectionType = () => session!.InvalidateGeneration();
        var outcome = session.Run();

        Assert.False(outcome.Accepted);
        Assert.Equal("STALE_GENERATION_RESULT", outcome.Code);
        Assert.True(outcome.Generation > 1);

        // The stale path still closes the handle and releases the modules.
        Assert.True(outcome.ControllerHandleClosed);
        Assert.Equal(1, kernel.CloseCount);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.True(session.CurrentGeneration > outcome.Generation);
    }

    // ------------------------------------------------------------- source-shape requirements

    [Fact]
    public void No_Raw_Command_Axis_Read_Or_Motion_Can_Be_Requested_Of_The_Kernel()
    {
        // The kernel seam has no command surface at all: the complete call vocabulary a session
        // can ever produce is load / module-path / resolve / release / force / open / close /
        // connection-type / revision / serial. This pins that no raw-command call exists.
        var kernel = new FakeControllerKernel();
        _ = Session(kernel).Run();

        foreach (var entry in kernel.Log)
        {
            Assert.DoesNotContain("command", entry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("axis", entry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("motion", entry, StringComparison.OrdinalIgnoreCase);
        }
    }
}
