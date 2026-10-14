using System.Text.Json;
using Wjss.Adapters.TcAcquisition;
using Wjss.TcBench;
using Xunit;

namespace Wjss.TcAcquisition.Tests;

public sealed class TrialPolicyTests
{
    private static TcNodeEndpoint SoftwareEndpoint => new() { NodeId = "SOFTWARE-01", IpAddress = "127.0.0.1", Port = 12345, UnitIdentifier = 0 };

    private static PhysicalTrialBudgets NarrowBudgets(TimeSpan lifecycle, TimeSpan cleanup, TimeSpan worker) =>
        new() { LifecycleBudget = lifecycle, CleanupObservationBudget = cleanup, WorkerObservationBudget = worker };

    [Fact]
    public void Default_Is_DryRun_And_Constructs_No_Client()
    {
        var options = BenchArguments.Parse([]);
        Assert.Equal(BenchMode.SIMULATION, options.Mode);
        Assert.Null(options.AuthorizationPath);
        Assert.Null(options.SignaturePath);
        var runner = new TrialRunner();
        var evidence = runner.DryRun(TrialCatalog.LoadTrusted(), options.TrialId);
        Assert.Equal("NOT_ISSUED", evidence.Connect.State);
        Assert.Equal("NOT_ISSUED", evidence.Read.State);
        Assert.Empty(evidence.Words);
        Assert.Equal(TrialAcceptance.QUARANTINED, evidence.TrialAcceptance);
        Assert.Equal(PhysicalTrialStates.NotAttempted, evidence.AuthorizationVerification);
        Assert.Equal(PhysicalTrialStates.NotAttempted, evidence.AuthorizationConsumption);
        Assert.Equal(PhysicalTrialStates.NotProvided, evidence.AuthorizationId);
        Assert.Equal("NOT_OBSERVED", evidence.CleanupState);
        Assert.Equal("NOT_OBSERVED", evidence.WorkerObservationState);
        Assert.Throws<InvalidOperationException>(() => runner.DryRun(TrialCatalog.LoadTrusted(), "T01"));
    }

    [Fact]
    public void Catalog_Carries_No_Execution_Authority_And_The_Physical_Gate_Stays_Disabled()
    {
        var catalog = TrialCatalog.LoadTrusted();
        // The catalog artifact can never activate a trial: no execution flag, no authorization digest,
        // no endpoint switch exists anywhere in the policy surface.
        Assert.DoesNotContain(typeof(TrialConfiguration).GetProperties(),
            p => p.Name.Contains("ExecutionAuthorized", StringComparison.Ordinal) || p.Name.Contains("AuthorizationSha256", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(BenchArguments).GetProperties(),
            p => p.Name.Contains("Address", StringComparison.Ordinal) || p.Name.Contains("Port", StringComparison.Ordinal)
                || p.Name.Contains("Unit", StringComparison.Ordinal) || p.Name.Contains("Quantity", StringComparison.Ordinal));
        // The repository build is physically disabled, so a missing or self-declared authorization can
        // never activate T01: the gate refuses before any artifact, path or endpoint is touched.
        var decision = PhysicalTrialAuthorization.Authorize(catalog, BuildIdentity.LoadTrusted(), null, null);
        Assert.False(decision.Authorized);
        Assert.Null(decision.Plan);
        Assert.Equal(PhysicalTrialRefusals.PhysicalCapabilityDisabled, decision.RefusalCode);
    }

    [Theory]
    [InlineData("--ip")]
    [InlineData("--port")]
    [InlineData("--unit")]
    [InlineData("--function")]
    [InlineData("--start")]
    [InlineData("--quantity")]
    [InlineData("--nodes")]
    public void No_FreeForm_Endpoint_Arguments(string option) => Assert.Throws<ArgumentException>(() => BenchArguments.Parse([option, "1"]));

    [Fact]
    public void Authorization_And_Signature_Are_Only_Valid_Together_And_Only_For_Physical_Test()
    {
        Assert.Throws<ArgumentException>(() => BenchArguments.Parse(["--authorization", "a.json"]));
        Assert.Throws<ArgumentException>(() => BenchArguments.Parse(["--signature", "a.sig"]));
        Assert.Throws<ArgumentException>(() => BenchArguments.Parse(["--dry-run", "--authorization", "a.json", "--signature", "a.sig"]));
        Assert.Throws<ArgumentException>(() => BenchArguments.Parse(["--physical-test", "--dry-run"]));
        Assert.Throws<ArgumentException>(() => BenchArguments.Parse(["--physical-test", "--physical-test"]));
        Assert.Throws<ArgumentException>(() => BenchArguments.Parse(["--trial"]));
        Assert.Throws<ArgumentException>(() => BenchArguments.Parse(["--trial", "T02", "--physical-test"]));
        Assert.Throws<ArgumentException>(() => BenchArguments.Parse(["--unapproved"]));
        var parsed = BenchArguments.Parse(["--physical-test", "--trial", "T01", "--authorization", "a.json", "--signature", "a.sig"]);
        Assert.Equal(BenchMode.PHYSICAL_TEST, parsed.Mode);
        Assert.Equal("T01", parsed.TrialId);
        Assert.Equal("a.json", parsed.AuthorizationPath);
        Assert.Equal("a.sig", parsed.SignaturePath);
    }

    [Theory]
    [InlineData("NodeId")]
    [InlineData("CabinetId")]
    [InlineData("Address")]
    [InlineData("Port")]
    [InlineData("Quantity")]
    [InlineData("FunctionCode")]
    [InlineData("UnitId")]
    [InlineData("Start")]
    [InlineData("Endianness")]
    public void Manifest_Cannot_Redefine_Trusted_Fields(string field)
    {
        var catalog = TrialCatalog.LoadTrusted();
        var t = catalog.Select("T01").Trial;
        var changed = field switch
        {
            "NodeId" => t with { NodeId = "TC-NODE-02" },
            "CabinetId" => t with { CabinetId = "CAB2" },
            "Address" => t with { Address = "127.0.0.1" },
            "Port" => t with { Port = 1 },
            "Quantity" => t with { Quantity = 39 },
            "FunctionCode" => t with { FunctionCode = 4 },
            "UnitId" => t with { UnitId = 255 },
            "Start" => t with { Start = 1 },
            _ => t with { Endianness = "LittleEndian" },
        };
        Assert.Throws<InvalidDataException>(() => catalog.ValidateManifest("T01", JsonSerializer.Serialize(changed, StrictJson.Options)));
    }

    [Fact]
    public void Exact_Manifest_Digest_Is_Independent_Of_Supplied_Input()
    {
        var c = TrialCatalog.LoadTrusted();
        var bytes = JsonSerializer.Serialize(c.Select("T01").Trial, StrictJson.Options) + "\n";
        Assert.Equal(c.Select("T01").Trial, c.ValidateManifest("T01", bytes));
        Assert.Throws<InvalidDataException>(() => c.ValidateManifest("T01", bytes + " "));
        Assert.Throws<InvalidDataException>(() => c.Select("T02"));
        Assert.Throws<InvalidDataException>(() => c.ValidateManifest("T01", "{\"nodes\":[]}"));
        Assert.Throws<System.Text.Json.JsonException>(() => StrictJson.Read<TrialConfiguration>("{\"extra\":1}"));
        Assert.Throws<InvalidDataException>(() => StrictJson.Read<TrialConfiguration>("{\"trialId\":\"T01\",\"trialId\":\"T01\"}"));
    }

    [Fact]
    public async Task Software_Seam_Rejects_Configured_Endpoint_And_502_Before_Factory()
    {
        var c = TrialCatalog.LoadTrusted();
        var calls = 0;
        ITcReadOnlyClient Factory() { calls++; return new TestClient(); }
        await Assert.ThrowsAsync<ArgumentException>(() => new TrialRunner().RunSoftwareTrialAsync(c, c.Select("T01").Trial.Endpoint, Factory, TimeSpan.FromSeconds(2), null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => new TrialRunner().RunSoftwareTrialAsync(c, SoftwareEndpoint with { Port = 502 }, Factory, TimeSpan.FromSeconds(2), null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => new TrialRunner().RunSoftwareTrialAsync(c, SoftwareEndpoint with { IpAddress = "localhost" }, Factory, TimeSpan.FromSeconds(2), null, CancellationToken.None));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task One_Connect_One_Read_No_Retry_And_Observed_Cleanup()
    {
        var c = TrialCatalog.LoadTrusted();
        var fake = new TestClient();
        var runner = new TrialRunner();
        var evidence = await runner.RunSoftwareTrialAsync(c, SoftwareEndpoint, () => fake, TimeSpan.FromSeconds(2), null, CancellationToken.None);
        Assert.Equal(1, fake.ConnectCalls);
        Assert.Equal(1, fake.ReadCalls);
        Assert.Equal(1, fake.DisconnectCalls);
        Assert.Equal(1, fake.DisposeCalls);
        Assert.True(fake.Disposed);
        Assert.Equal(40, evidence.Words.Length);
        Assert.Equal("COMPLETE", evidence.TaskCompletion);
        Assert.Equal("COMPLETE", evidence.DisconnectResult);
        Assert.Equal("COMPLETE", evidence.DisposalResult);
        Assert.Equal("DISCONNECTED", evidence.TransportAfter);
        Assert.Equal("NOT_REQUESTED", evidence.StopState);
        Assert.Equal("NOT_ELAPSED", evidence.TimeoutState);
        Assert.Equal(PhysicalTrialStates.NotElapsed, evidence.LifecycleBudgetState);
        Assert.Equal(PhysicalTrialStates.Observed, evidence.CleanupState);
        Assert.Equal(PhysicalTrialStates.Observed, evidence.WorkerObservationState);
        Assert.Equal("CURRENT_AT_DIAGNOSTIC_HANDOFF", evidence.StaleDecision);
        Assert.Equal("RETAINED_QUARANTINED", evidence.FinalHandoffDecision);
        Assert.Equal(WireEvidenceOutcome.NOT_PROVIDED, evidence.WireEvidence.Outcome);
        Assert.Equal(TrialAcceptance.QUARANTINED, evidence.TrialAcceptance);
        TrialEvidence.Validate(evidence, c);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunSoftwareTrialAsync(c, SoftwareEndpoint, () => fake, TimeSpan.FromSeconds(2), null, CancellationToken.None));
    }

    [Theory]
    [InlineData(TcReadKind.ConnectionFailure)]
    [InlineData(TcReadKind.Timeout)]
    [InlineData(TcReadKind.ProtocolException)]
    public async Task Failed_Connect_Never_Reads_Or_Reconnects(TcReadKind kind)
    {
        var fake = new TestClient { ConnectKind = kind };
        var e = await new TrialRunner().RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => fake, TimeSpan.FromSeconds(2), null, CancellationToken.None);
        Assert.Equal(1, fake.ConnectCalls);
        Assert.Equal(0, fake.ReadCalls);
        Assert.Equal("NOT_ISSUED", e.Read.State);
        Assert.Empty(e.Words);
        Assert.True(fake.Disposed);
        Assert.Equal(kind == TcReadKind.Timeout ? "ELAPSED" : "NOT_OBSERVED", e.TimeoutState);
    }

    [Fact]
    public async Task Cancel_During_NonCooperative_Connect_Observes_Late_Completion_And_No_Read()
    {
        using var cts = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new TestClient { ConnectWait = _ => { entered.SetResult(); return release.Task; } };
        var task = new TrialRunner().RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => fake, TimeSpan.FromSeconds(2), null, cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // The synchronous handshake is not cooperatively cancellable: it is observed to completion and
        // the late result is fenced, never detached.
        cts.Cancel();
        Assert.False(task.IsCompleted);
        release.SetResult();
        var e = await task;
        Assert.Equal(0, fake.ReadCalls);
        Assert.True(fake.Disposed);
        Assert.Empty(e.Words);
        Assert.True(e.AcquisitionEpochAfter > e.AcquisitionEpoch);
        Assert.Equal("REQUESTED", e.CancellationState);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Stop_At_Final_Handoff_Prevents_Late_Retention(bool callerCancellation)
    {
        using var cts = new CancellationTokenSource();
        var runner = new TrialRunner();
        var e = await runner.RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => new TestClient(), TimeSpan.FromSeconds(2),
            () => { if (callerCancellation) cts.Cancel(); else runner.Stop(); return Task.CompletedTask; }, cts.Token);
        Assert.Empty(e.Words);
        Assert.Equal("REJECTED_OR_NOT_ISSUED", e.FinalHandoffDecision);
        Assert.Equal("REJECTED_OR_NOT_ISSUED", e.StaleDecision);
        Assert.Equal("REQUESTED", e.StopState);
        Assert.True(e.AcquisitionEpochAfter > e.AcquisitionEpoch);
    }

    [Fact]
    public async Task Stop_During_Read_Cancels_Operation_And_Observes_Cleanup()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new TestClient { ReadWait = async ct => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); } };
        var runner = new TrialRunner();
        var task = runner.RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => fake, TimeSpan.FromSeconds(2), null, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runner.Stop();
        var e = await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(e.Words);
        Assert.True(fake.Disposed);
        Assert.Equal("REQUESTED", e.StopState);
        Assert.Equal("STOP_REQUESTED", e.CancellationState);
        // A requested stop is not reported as an elapsed timeout or as a budget overrun.
        Assert.NotEqual("ELAPSED", e.TimeoutState);
        Assert.Equal(PhysicalTrialStates.Observed, e.CleanupState);
    }

    [Fact]
    public async Task Cleanup_Overdue_Is_Recorded_Separately_And_Cleanup_Is_Still_Observed()
    {
        var fake = new TestClient { DisconnectWait = () => Task.Delay(300) };
        var runner = new TrialRunner { Budgets = NarrowBudgets(TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(5)) };
        var e = await runner.RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => fake, TimeSpan.FromSeconds(2), null, CancellationToken.None);
        Assert.Equal(1, fake.DisconnectCalls);
        Assert.Equal("COMPLETE", e.DisconnectResult);
        Assert.Equal("COMPLETE", e.DisposalResult);
        Assert.Equal(PhysicalTrialStates.Overdue, e.CleanupState);
        Assert.Equal(PhysicalTrialStates.NotElapsed, e.LifecycleBudgetState);
        Assert.Equal(PhysicalTrialStates.Observed, e.WorkerObservationState);
        Assert.Empty(e.Words);
        TrialEvidence.Validate(e, TrialCatalog.LoadTrusted());
    }

    [Fact]
    public async Task Overdue_Worker_Observation_And_Elapsed_Budget_Are_Distinct_From_Cleanup_State()
    {
        var slowWorker = new TestClient { ConnectWait = _ => Task.Delay(250, CancellationToken.None) };
        var workerRunner = new TrialRunner { Budgets = NarrowBudgets(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)) };
        var e = await workerRunner.RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => slowWorker, TimeSpan.FromSeconds(2), null, CancellationToken.None);
        Assert.Equal(PhysicalTrialStates.Overdue, e.WorkerObservationState);
        Assert.Equal(PhysicalTrialStates.Observed, e.CleanupState);
        Assert.Equal(PhysicalTrialStates.NotElapsed, e.LifecycleBudgetState);
        Assert.Empty(e.Words);
        TrialEvidence.Validate(e, TrialCatalog.LoadTrusted());

        var elapsed = new TestClient { ConnectWait = _ => Task.Delay(300, CancellationToken.None) };
        var elapsedRunner = new TrialRunner { Budgets = NarrowBudgets(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)) };
        var expired = await elapsedRunner.RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => elapsed, TimeSpan.FromSeconds(2), null, CancellationToken.None);
        Assert.Equal(PhysicalTrialStates.Elapsed, expired.LifecycleBudgetState);
        Assert.Equal(PhysicalTrialStates.Overdue, expired.CleanupState);
        Assert.Empty(expired.Words);
        TrialEvidence.Validate(expired, TrialCatalog.LoadTrusted());
    }

    [Fact]
    public async Task Invalid_Lifecycle_Budgets_Are_Refused_Before_Any_Client_Is_Constructed()
    {
        var calls = 0;
        ITcReadOnlyClient Factory() { calls++; return new TestClient(); }
        var runner = new TrialRunner { Budgets = NarrowBudgets(TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)) };
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, Factory, TimeSpan.FromSeconds(2), null, CancellationToken.None));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(39)]
    [InlineData(41)]
    public async Task Harness_Does_Not_Pad_Or_Truncate(int count)
    {
        var fake = new TestClient { Data = new ushort[count] };
        var e = await new TrialRunner().RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => fake, TimeSpan.FromSeconds(2), null, CancellationToken.None);
        Assert.Equal(count, e.CopiedWordCount);
        Assert.Empty(e.Words);
    }

    [Fact]
    public async Task Stale_And_Cleanup_Failure_Block_Retention()
    {
        foreach (var fake in new[] { new TestClient { ChangeGenerationDuringRead = true }, new TestClient { CleanupFails = true } })
        {
            var e = await new TrialRunner().RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint, () => fake, TimeSpan.FromSeconds(2), null, CancellationToken.None);
            Assert.Empty(e.Words);
            Assert.True(fake.Disposed);
        }
    }

    [Fact]
    public async Task Throwing_Read_Is_Observed_As_Threw_Not_NotIssued_And_Cleanup_Completes()
    {
        var fake = new TestClient { ReadWait = _ => Task.FromException(new IOException("Synthetic read failure")) };
        var result = await new TrialRunner().RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint,
            () => fake, TimeSpan.FromSeconds(1), null, CancellationToken.None);
        Assert.Equal("THREW", result.Read.State);
        Assert.Equal(nameof(IOException), result.Read.ExceptionType);
        Assert.Null(result.Read.Kind);
        Assert.Equal("COMPLETE", result.TaskCompletion);
        Assert.True(fake.Disposed);
        Assert.Empty(result.Words);
        TrialEvidence.Validate(result, TrialCatalog.LoadTrusted());
    }

    [Fact]
    public async Task Failed_PostCleanup_Observation_Cannot_Produce_Retained_Words()
    {
        var fake = new TestClient { ThrowObservationAfterDisposal = true };
        var result = await new TrialRunner().RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), SoftwareEndpoint,
            () => fake, TimeSpan.FromSeconds(1), null, CancellationToken.None);
        Assert.Equal(1, fake.DisconnectCalls);
        Assert.Equal(1, fake.DisposeCalls);
        Assert.Equal("INCOMPLETE", result.DisposalResult);
        Assert.Equal(nameof(InvalidOperationException), result.LifecycleFailureType);
        Assert.Empty(result.Words);
    }
}
