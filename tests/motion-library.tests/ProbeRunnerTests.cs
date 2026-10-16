using System.Security.Cryptography;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// The full probe orchestration defect class, exercised end to end WITHOUT any real native
/// module: synthetic x64 PE fixture files in a per-test temporary directory (real SHA-256 over
/// synthetic content) and a recording kernel seam. Every refusal path is also asserted to keep
/// detail free of directories, paths, hashes, expected values and exception internals.
///
/// These tests run Owner-local on Windows x64: the runner's platform gate admits nothing else.
/// </summary>
public sealed class ProbeRunnerTests : IDisposable
{
    private const string ExpectedVersion = "9.9.9-test";

    /// <summary>The expected kernel-event sequence of one accepted probe, allocated once.</summary>
    private static readonly string[] ExpectedHappyPathEvents =
    {
        $"load:{MotionLibraryProbeVocabulary.CompanionModuleFileName}",
        $"getpath:{MotionLibraryProbeVocabulary.CompanionModuleFileName}",
        $"load:{MotionLibraryProbeVocabulary.PrimaryModuleFileName}",
        $"getpath:{MotionLibraryProbeVocabulary.PrimaryModuleFileName}",
        $"resolve:{MotionLibraryProbeVocabulary.AuthorizedExportName}",
        "invoke:export",
        $"release:{MotionLibraryProbeVocabulary.PrimaryModuleFileName}",
        $"release:{MotionLibraryProbeVocabulary.CompanionModuleFileName}",
    };

    /// <summary>The complete resolve-event set of any probe run, allocated once.</summary>
    private static readonly string[] ExpectedResolveOnlyEvents =
    {
        $"resolve:{MotionLibraryProbeVocabulary.AuthorizedExportName}",
    };

    private readonly string _directory;

    public ProbeRunnerTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "wjss-probe-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Test-fixture cleanup must never mask the test outcome.
        }
    }

    private string PrimaryPath => Path.Combine(_directory, MotionLibraryProbeVocabulary.PrimaryModuleFileName);

    private string CompanionPath => Path.Combine(_directory, MotionLibraryProbeVocabulary.CompanionModuleFileName);

    /// <summary>Recording kernel: deterministic control over every primitive, full event log.</summary>
    private sealed class RecordingKernel : INativeKernel
    {
        private readonly Dictionary<IntPtr, string> _loadedBy = new();
        private readonly Dictionary<IntPtr, string> _reportedPaths = new();
        private readonly List<string> _events = new();
        private int _nextHandle = 1000;

        public string? FailLoadForFile { get; set; }

        public string? ModulePathSubstitutionForFile { get; set; }

        public bool ExportMissing { get; set; }

        /// <summary>Bytes the fake export writes (no terminator needed; the buffer rest stays zero).</summary>
        public byte[]? VersionBytes { get; set; } = System.Text.Encoding.ASCII.GetBytes(ExpectedVersion);

        /// <summary>True: fill the whole buffer, leaving no terminator anywhere in the bound.</summary>
        public bool FillWithoutTerminator { get; set; }

        public bool FailRelease { get; set; }

        public List<string> Events => _events;

        public IntPtr LoadApprovedModule(string approvedAbsolutePath)
        {
            var file = Path.GetFileName(approvedAbsolutePath);
            _events.Add($"load:{file}");
            if (string.Equals(file, FailLoadForFile, StringComparison.Ordinal))
            {
                return IntPtr.Zero;
            }

            var handle = new IntPtr(_nextHandle += 7);
            _loadedBy[handle] = approvedAbsolutePath;
            _reportedPaths[handle] = string.Equals(file, ModulePathSubstitutionForFile, StringComparison.Ordinal)
                ? approvedAbsolutePath + ".substituted"
                : approvedAbsolutePath;
            return handle;
        }

        public IntPtr ResolveExport(IntPtr module, string exportName)
        {
            _events.Add($"resolve:{exportName}");
            return ExportMissing ? IntPtr.Zero : new IntPtr(4242);
        }

        public string? GetLoadedModulePath(IntPtr module)
        {
            _events.Add("getpath:" + Path.GetFileName(_loadedBy[module]));
            return _reportedPaths[module];
        }

        public bool ReleaseModule(IntPtr module)
        {
            _events.Add("release:" + Path.GetFileName(_loadedBy.Remove(module, out var path) ? path : "unknown"));
            return !FailRelease;
        }

        public int InvokeAuthorizedVersionExport(IntPtr exportAddress, byte[] versionBuffer)
        {
            _events.Add("invoke:export");
            if (FillWithoutTerminator)
            {
                for (var index = 0; index < versionBuffer.Length; index++)
                {
                    versionBuffer[index] = (byte)'X';
                }

                return 0;
            }

            if (VersionBytes is not null)
            {
                Array.Copy(VersionBytes, versionBuffer, Math.Min(VersionBytes.Length, versionBuffer.Length - 1));
            }

            return 0;
        }
    }

    private void WriteModules(ushort machine = 0x8664, ushort? secondaryMachine = null)
    {
        File.WriteAllBytes(CompanionPath, NativeFileInspectionTests.MakePeImage(secondaryMachine ?? machine, 4096));
        File.WriteAllBytes(PrimaryPath, NativeFileInspectionTests.MakePeImage(machine, 8192));
    }

    private MotionLibraryProbeConfiguration CreateConfiguration()
    {
        return new MotionLibraryProbeConfiguration
        {
            NativeDirectory = _directory,
            PrimaryModulePath = PrimaryPath,
            CompanionModulePath = CompanionPath,
            ExpectedPrimaryHash = File.Exists(PrimaryPath) ? SHA256.HashData(File.ReadAllBytes(PrimaryPath)) : new byte[32],
            ExpectedCompanionHash = File.Exists(CompanionPath) ? SHA256.HashData(File.ReadAllBytes(CompanionPath)) : new byte[32],
            ExpectedVersion = ExpectedVersion,
        };
    }

    private MotionLibraryProbeOutcome Run(RecordingKernel kernel)
    {
        var runner = new MotionLibraryProbeRunner(CreateConfiguration(), kernel);
        return runner.Probe();
    }

    private void AssertRedacted(MotionLibraryProbeOutcome outcome)
    {
        Assert.DoesNotContain(_directory, outcome.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SHA256", outcome.Detail, StringComparison.Ordinal);
        if (!outcome.Accepted)
        {
            Assert.Null(outcome.LibraryVersion);
        }
    }

    [Fact]
    public void The_Happy_Path_Loads_Verifies_Proves_And_Unloads_In_The_Approved_Order()
    {
        WriteModules();
        var kernel = new RecordingKernel();

        var outcome = Run(kernel);

        Assert.True(outcome.Accepted, $"expected acceptance, refused with {outcome.Code}: {outcome.Detail}");
        Assert.Equal(MotionLibraryProbeVocabulary.NativeLibraryProbeComplete, outcome.Code);
        Assert.Equal(ExpectedVersion, outcome.LibraryVersion);
        Assert.True(outcome.NativeHandlesReleased);
        AssertRedacted(outcome);

        Assert.Equal(ExpectedHappyPathEvents, kernel.Events);
    }

    [Fact]
    public void Only_The_Authorized_Export_Is_Ever_Resolved()
    {
        WriteModules();
        var kernel = new RecordingKernel();
        _ = Run(kernel);

        var resolved = kernel.Events.Where(e => e.StartsWith("resolve:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(ExpectedResolveOnlyEvents, resolved);
        Assert.DoesNotContain(kernel.Events, e => e.Contains("open", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(kernel.Events, e => e.Contains("close", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(kernel.Events, e => e.Contains("command", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_Missing_Module_File_Is_Not_Found_And_Nothing_Is_Loaded()
    {
        WriteModules();
        File.Delete(CompanionPath);
        var kernel = new RecordingKernel();

        var outcome = Run(kernel);

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeFileNotFound, outcome.Code);
        Assert.Contains(MotionLibraryProbeVocabulary.CompanionModuleFileName, outcome.Detail, StringComparison.Ordinal);
        Assert.Empty(kernel.Events);
        AssertRedacted(outcome);
    }

    [Fact]
    public void A_Identity_Mismatch_Is_A_Refusal_Before_Any_Load()
    {
        WriteModules();
        // Break the companion identity after configuration capture.
        var configuration = CreateConfiguration();
        File.AppendAllText(CompanionPath, "tampered");

        var kernel = new RecordingKernel();
        var runner = new MotionLibraryProbeRunner(configuration, kernel);
        var outcome = runner.Probe();

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeFileIdentityMismatch, outcome.Code);
        Assert.DoesNotContain(kernel.Events, e => e.StartsWith("load:", StringComparison.Ordinal));
        AssertRedacted(outcome);
    }

    [Fact]
    public void A_Non_x64_Module_Is_An_Architecture_Refusal_Before_Any_Load()
    {
        WriteModules(machine: 0x8664, secondaryMachine: 0x014C);
        var kernel = new RecordingKernel();

        var outcome = Run(kernel);

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeArchitectureMismatch, outcome.Code);
        Assert.Contains("0x014C", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(kernel.Events, e => e.StartsWith("load:", StringComparison.Ordinal));
        AssertRedacted(outcome);
    }

    [Fact]
    public void A_Load_Failure_Is_A_Refusal_And_Nothing_Further_Is_Resolved()
    {
        WriteModules();
        var kernel = new RecordingKernel { FailLoadForFile = MotionLibraryProbeVocabulary.PrimaryModuleFileName };

        var outcome = Run(kernel);

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeLoadFailed, outcome.Code);
        Assert.DoesNotContain(kernel.Events, e => e.StartsWith("resolve:", StringComparison.Ordinal));
        // The companion that did load was deterministically released by cleanup.
        Assert.Contains($"release:{MotionLibraryProbeVocabulary.CompanionModuleFileName}", kernel.Events);
        AssertRedacted(outcome);
    }

    [Fact]
    public void A_Substituted_Loaded_Module_Path_Is_A_Refusal()
    {
        WriteModules();
        var kernel = new RecordingKernel { ModulePathSubstitutionForFile = MotionLibraryProbeVocabulary.PrimaryModuleFileName };

        var outcome = Run(kernel);

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeModulePathMismatch, outcome.Code);
        Assert.DoesNotContain(kernel.Events, e => e.StartsWith("resolve:", StringComparison.Ordinal));
        AssertRedacted(outcome);
    }

    [Fact]
    public void A_Missing_Authorized_Export_Is_A_Refusal_And_Handles_Are_Released()
    {
        WriteModules();
        var kernel = new RecordingKernel { ExportMissing = true };

        var outcome = Run(kernel);

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeExportNotFound, outcome.Code);
        Assert.True(outcome.NativeHandlesReleased);
        Assert.Contains($"release:{MotionLibraryProbeVocabulary.PrimaryModuleFileName}", kernel.Events);
        Assert.Contains($"release:{MotionLibraryProbeVocabulary.CompanionModuleFileName}", kernel.Events);
        AssertRedacted(outcome);
    }

    [Fact]
    public void An_Empty_Or_Unterminated_Version_Buffer_Is_A_Call_Failure()
    {
        WriteModules();
        var silent = new RecordingKernel { VersionBytes = null };
        var outcome = Run(silent);
        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeVersionCallFailed, outcome.Code);
        AssertRedacted(outcome);

        var unterminated = new RecordingKernel { FillWithoutTerminator = true };
        outcome = Run(unterminated);
        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeVersionCallFailed, outcome.Code);
        AssertRedacted(outcome);
    }

    [Fact]
    public void A_Different_Version_Is_A_Mismatch_And_Never_An_Acceptance()
    {
        WriteModules();
        var kernel = new RecordingKernel { VersionBytes = System.Text.Encoding.ASCII.GetBytes("1.2.3-other") };

        var outcome = Run(kernel);

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeVersionMismatch, outcome.Code);
        Assert.DoesNotContain("1.2.3-other", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(ExpectedVersion, outcome.Detail, StringComparison.Ordinal);
        AssertRedacted(outcome);
    }

    [Fact]
    public void An_Unload_Failure_After_Passing_Proofs_Is_An_Unload_Refusal_Never_A_Success()
    {
        WriteModules();
        var kernel = new RecordingKernel { FailRelease = true };

        var outcome = Run(kernel);

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionLibraryProbeVocabulary.NativeUnloadFailed, outcome.Code);
        Assert.False(outcome.NativeHandlesReleased);
        AssertRedacted(outcome);
    }
}
