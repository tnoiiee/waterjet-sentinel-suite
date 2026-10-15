using Wjss.Adapters.TcAcquisition;
using Wjss.TcBench;
using Xunit;

namespace Wjss.TcAcquisition.Tests;

public sealed class AdapterLifecycleTests
{
    private static TcNodeEndpoint Endpoint(int port, byte unit = 0) => new() { NodeId = "LOOPBACK-01", IpAddress = "127.0.0.1", Port = port, UnitIdentifier = unit };
    private static TcReadRequest Request(ITcReadOnlyClient client, TcNodeEndpoint endpoint, TcRegisterArea area = TcRegisterArea.HoldingRegisters, TimeSpan? timeout = null) => new()
    {
        Endpoint = endpoint, Generation = client.Generation, StartAddress = 0, Quantity = 40,
        RegisterArea = area, Endianness = TcEndiannessCandidate.BigEndian, Timeout = timeout ?? TimeSpan.FromSeconds(2),
    };

    [Fact]
    public async Task Socket_Free_Deterministic_Client_Rejects_Addresses_And_Never_Repeats_A_Read()
    {
        await using var client = TcReadOnlyClientFactory.CreateDeterministic();
        var marker = new TcNodeEndpoint
        {
            NodeId = "TC-NODE-01", IpAddress = DeterministicTcReadOnlyClient.EndpointMarker,
        };
        var wrongEndpoint = marker with { IpAddress = "NOT_A_DETERMINISTIC_MARKER" };
        Assert.Equal(TcReadKind.ConfigurationRejected,
            (await client.ConnectAsync(wrongEndpoint, CancellationToken.None)).Kind);
        Assert.Equal(0, client.Generation);

        var connected = await client.ConnectAsync(marker, CancellationToken.None);
        Assert.Equal(TcReadKind.Success, connected.Kind);
        Assert.True(client.IsConnected);
        var request = Request(client, marker);
        var read = await client.ReadAsync(request, CancellationToken.None);
        Assert.Equal(TcReadKind.Success, read.Kind);
        var words = Assert.IsType<ushort[]>(read.RawWords);
        Assert.Equal(40, words.Length);
        Assert.Equal(Enumerable.Range(100, 40).Select(n => (ushort)n), words);
        var repeated = await client.ReadAsync(request, CancellationToken.None);
        Assert.Equal(TcReadKind.ConfigurationRejected, repeated.Kind);
        Assert.Equal("DETERMINISTIC_ALREADY_READ", repeated.DetailCode);

        await client.DisconnectAsync();
        Assert.False(client.IsConnected);
        Assert.True(client.Generation > connected.Generation);
        Assert.Equal(TcReadKind.StaleGenerationRejected,
            (await client.ReadAsync(request, CancellationToken.None)).Kind);
        Assert.Equal(TcReadKind.ConfigurationRejected,
            (await client.ConnectAsync(marker, CancellationToken.None)).Kind);
    }

    [Fact]
    public async Task Deterministic_Client_Consumes_Invalid_First_Request_Without_A_Corrective_Retry()
    {
        await using var client = TcReadOnlyClientFactory.CreateDeterministic();
        var marker = new TcNodeEndpoint
        {
            NodeId = "TC-NODE-01", IpAddress = DeterministicTcReadOnlyClient.EndpointMarker,
        };
        Assert.Equal(TcReadKind.Success,
            (await client.ConnectAsync(marker, CancellationToken.None)).Kind);
        var request = Request(client, marker);
        var invalid = await client.ReadAsync(request with { Quantity = 39 }, CancellationToken.None);
        Assert.Equal(TcReadKind.ConfigurationRejected, invalid.Kind);
        Assert.Null(invalid.RawWords);
        var retry = await client.ReadAsync(request, CancellationToken.None);
        Assert.Equal(TcReadKind.ConfigurationRejected, retry.Kind);
        Assert.Null(retry.RawWords);
    }

    [Theory]
    [InlineData(TcRegisterArea.HoldingRegisters, 3, 0)]
    [InlineData(TcRegisterArea.InputRegisters, 4, 255)]
    public async Task Explicit_Encoding_And_Copied_Words(TcRegisterArea area, int function, int unit)
    {
        await using var server = new LoopbackResponder();
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port, (byte)unit);
        Assert.Equal(TcReadKind.Success, (await client.ConnectAsync(endpoint, CancellationToken.None)).Kind);
        var result = await client.ReadAsync(Request(client, endpoint, area), CancellationToken.None);
        var request = await server.RequestReceived.Task;
        Assert.Equal(function, request[7]);
        Assert.Equal(unit, request[6]);
        Assert.Equal(new byte[] { 0, 0, 0, 40 }, request[8..12]);
        Assert.Equal(TcReadKind.Success, result.Kind);
        var words = Assert.IsType<ushort[]>(result.RawWords);
        Assert.Equal(40, words.Length);
        Assert.Equal((ushort)0x1234, words[0]);
        Assert.Equal(area, result.RegisterArea);
        await client.DisconnectAsync();
        Assert.False(client.IsConnected);
    }

    [Theory]
    [InlineData("Node")]
    [InlineData("Address")]
    [InlineData("Port")]
    [InlineData("Unit")]
    public async Task Endpoint_Mismatch_Rejected_Before_Read(string field)
    {
        await using var server = new LoopbackResponder();
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        // Address variant remains loopback even though it is rejected without transmission.
        var changed = field switch
        {
            "Node" => endpoint with { NodeId = "OTHER" },
            "Address" => endpoint with { IpAddress = "localhost" },
            "Port" => endpoint with { Port = server.Port == 12345 ? 12346 : 12345 },
            _ => endpoint with { UnitIdentifier = 255 },
        };
        var result = await client.ReadAsync(Request(client, changed), CancellationToken.None);
        Assert.Equal(TcReadKind.ConfigurationRejected, result.Kind);
        Assert.Equal("ENDPOINT_MISMATCH", result.DetailCode);
        Assert.False(server.RequestReceived.Task.IsCompleted);
    }

    [Theory]
    [InlineData(ReplyShape.Short)]
    [InlineData(ReplyShape.Excessive)]
    [InlineData(ReplyShape.OddData)]
    [InlineData(ReplyShape.ProtocolId)]
    [InlineData(ReplyShape.Exception)]
    public async Task Partial_Excessive_Malformed_And_Exception_Fail_Closed(ReplyShape shape)
    {
        await using var server = new LoopbackResponder(shape);
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        var result = await client.ReadAsync(Request(client, endpoint), CancellationToken.None);
        Assert.NotEqual(TcReadKind.Success, result.Kind);
        Assert.Null(result.RawWords);
        if (shape == ReplyShape.Excessive) Assert.Equal("WORD_COUNT_MISMATCH", result.DetailCode);
        if (shape is ReplyShape.Exception or ReplyShape.ProtocolId) Assert.Equal(TcReadKind.ProtocolException, result.Kind);
        if (shape is ReplyShape.Exception or ReplyShape.Short or ReplyShape.ProtocolId) Assert.NotNull(result.ExceptionType);
    }

    [Fact]
    public async Task READ_BUSY_Is_FailFast_Without_Another_Request()
    {
        await using var server = new LoopbackResponder(ReplyShape.Delayed);
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        var first = client.ReadAsync(Request(client, endpoint), cts.Token);
        await server.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await client.ReadAsync(Request(client, endpoint), CancellationToken.None);
        Assert.Equal("READ_BUSY", second.DetailCode);
        cts.Cancel();
        Assert.NotEqual(TcReadKind.Success, (await first).Kind);
    }

    [Fact]
    public async Task PerCall_Timeout_Is_Classified()
    {
        await using var server = new LoopbackResponder(ReplyShape.Delayed);
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        var result = await client.ReadAsync(Request(client, endpoint, timeout: TimeSpan.FromMilliseconds(100)), CancellationToken.None);
        Assert.Equal(TcReadKind.Timeout, result.Kind);
    }

    [Fact]
    public async Task Caller_Cancellation_Is_Distinct()
    {
        await using var server = new LoopbackResponder(ReplyShape.Delayed);
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        var task = client.ReadAsync(Request(client, endpoint), cts.Token);
        await server.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        Assert.Equal(TcReadKind.Cancelled, (await task).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disconnect_Or_Dispose_During_Read_Never_Returns_Late_Success(bool dispose)
    {
        await using var server = new LoopbackResponder(ReplyShape.Delayed);
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        var generation = client.Generation;
        var task = client.ReadAsync(Request(client, endpoint), CancellationToken.None);
        await server.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (dispose) await client.DisposeAsync(); else await client.DisconnectAsync();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(TcReadKind.Success, result.Kind);
        Assert.True(client.Generation > generation);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task Stale_Generation_And_Invalid_Candidates_Send_No_Read()
    {
        await using var server = new LoopbackResponder();
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        var request = Request(client, endpoint);
        Assert.Equal(TcReadKind.StaleGenerationRejected, (await client.ReadAsync(request with { Generation = client.Generation - 1 }, CancellationToken.None)).Kind);
        Assert.Equal(TcReadKind.ConfigurationRejected, (await client.ReadAsync(request with { RegisterArea = (TcRegisterArea)23 }, CancellationToken.None)).Kind);
        Assert.Equal(TcReadKind.ConfigurationRejected, (await client.ReadAsync(request with { Endianness = TcEndiannessCandidate.LittleEndian }, CancellationToken.None)).Kind);
        Assert.False(server.RequestReceived.Task.IsCompleted);
    }

    [Fact]
    public async Task Later_Read_Cannot_Mutate_First_Owned_Buffer()
    {
        await using var server = new LoopbackResponder(reads: 2);
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        var first = await client.ReadAsync(Request(client, endpoint), CancellationToken.None);
        var firstWords = Assert.IsType<ushort[]>(first.RawWords);
        var saved = firstWords.ToArray();
        var second = await client.ReadAsync(Request(client, endpoint), CancellationToken.None);
        var secondWords = Assert.IsType<ushort[]>(second.RawWords);
        Assert.Equal(saved, firstWords);
        Assert.NotEqual(firstWords[0], secondWords[0]);
    }

    [Fact]
    public async Task Transport_Reset_Is_Not_Success()
    {
        await using var server = new LoopbackResponder(ReplyShape.Closed);
        await using var client = TcReadOnlyClientFactory.Create();
        var endpoint = Endpoint(server.Port);
        await client.ConnectAsync(endpoint, CancellationToken.None);
        var result = await client.ReadAsync(Request(client, endpoint), CancellationToken.None);
        Assert.NotEqual(TcReadKind.Success, result.Kind);
        Assert.Null(result.RawWords);
    }

    [Fact]
    public async Task Software_Seam_Over_Real_Loopback_Performs_One_Connect_One_Read_And_Observed_Cleanup()
    {
        // The real adapter behind the isolated software seam: one connect, at most one read, always
        // observed cleanup, quarantined acquisition-only evidence. Never a physical endpoint.
        await using var server = new LoopbackResponder();
        var endpoint = Endpoint(server.Port);
        var runner = new TrialRunner();
        var evidence = await runner.RunSoftwareTrialAsync(TrialCatalog.LoadTrusted(), endpoint,
            () => TcReadOnlyClientFactory.Create(), TimeSpan.FromSeconds(2), null, CancellationToken.None);
        Assert.Equal(TcReadKind.Success, evidence.Connect.Kind);
        Assert.Equal(TcReadKind.Success, evidence.Read.Kind);
        Assert.Equal("COMPLETE", evidence.TaskCompletion);
        Assert.Equal("COMPLETE", evidence.DisconnectResult);
        Assert.Equal("COMPLETE", evidence.DisposalResult);
        Assert.Equal("DISCONNECTED", evidence.TransportAfter);
        Assert.Equal(PhysicalTrialStates.Observed, evidence.CleanupState);
        Assert.Equal(40, evidence.Words.Length);
        Assert.Equal("SYNTHETIC_TEST_ONLY", evidence.Provenance);
        Assert.Equal("RETAINED_QUARANTINED", evidence.FinalHandoffDecision);
        Assert.Equal(TrialAcceptance.QUARANTINED, evidence.TrialAcceptance);
        TrialEvidence.Validate(evidence, TrialCatalog.LoadTrusted());
        var request = await server.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(12, request.Length);
        Assert.Equal(3, request[7]);
        Assert.Equal(0, request[6]);
        Assert.Equal(new byte[] { 0, 0, 0, 40 }, request[8..12]);
    }

    [Fact]
    public async Task Local_Simulation_Path_Over_Real_Loopback_Issues_One_Exact_FC03_Request_And_Observes_Cleanup()
    {
        // The public Local TCP Simulation path with the real adapter. The responder binds an ephemeral
        // loopback port (never 502), so only the invocation port differs from the Owner-local command
        // line; address, request shape, lifecycle and evidence class are exactly the production path.
        await using var server = new LoopbackResponder();
        var catalog = TrialCatalog.LoadTrusted();
        var invocation = LocalSimulation.InvocationConfiguration(catalog) with { Port = server.Port };
        var runner = new TrialRunner();
        var evidence = await runner.RunLocalSimulationAsync(catalog, invocation, () => TcReadOnlyClientFactory.Create(), CancellationToken.None);
        var request = await server.RequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(12, request.Length);
        Assert.Equal(0, request[6]);
        Assert.Equal(3, request[7]);
        Assert.Equal(new byte[] { 0, 0, 0, 40 }, request[8..12]);
        Assert.Equal(TcReadKind.Success, evidence.Connect.Kind);
        Assert.Equal(TcReadKind.Success, evidence.Read.Kind);
        Assert.Equal(LocalSimulation.Provenance, evidence.Provenance);
        Assert.Equal("127.0.0.1", evidence.Endpoint);
        Assert.Equal(server.Port, evidence.Port);
        Assert.Equal(40, evidence.CopiedWordCount);
        Assert.Equal(40, evidence.Words.Length);
        Assert.Equal(0, evidence.Words[0].Index);
        Assert.Equal((ushort)0x1234, evidence.Words[0].Value);
        Assert.Equal(39, evidence.Words[39].Index);
        Assert.Equal((ushort)(0x1234 + 39), evidence.Words[39].Value);
        Assert.Equal("COMPLETE", evidence.TaskCompletion);
        Assert.Equal("COMPLETE", evidence.DisconnectResult);
        Assert.Equal("COMPLETE", evidence.DisposalResult);
        Assert.Equal("DISCONNECTED", evidence.TransportAfter);
        Assert.Equal(PhysicalTrialStates.Observed, evidence.CleanupState);
        Assert.Equal("RETAINED_QUARANTINED", evidence.FinalHandoffDecision);
        Assert.Equal(TrialAcceptance.QUARANTINED, evidence.TrialAcceptance);
        Assert.Equal(PhysicalTrialStates.NotAttempted, evidence.AuthorizationVerification);
        Assert.Equal(PhysicalTrialStates.NotProvided, evidence.AuthorizationId);
        Assert.False(evidence.PhysicalCapabilityEnabled);
        TrialEvidence.Validate(evidence, catalog);
    }

    [Fact]
    public void Public_Boundary_Has_No_Write_Or_Generic_Execution_Operation()
    {
        var methods = typeof(ITcReadOnlyClient).GetMethods().Select(m => m.Name).ToArray();
        Assert.DoesNotContain(methods, name => name.Contains("Write", StringComparison.OrdinalIgnoreCase) || name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
        Assert.All(typeof(TcReadResult).GetProperties(), p => Assert.True(p.PropertyType.Namespace?.StartsWith("Wjss", StringComparison.Ordinal) == true || p.PropertyType.Namespace?.StartsWith("System", StringComparison.Ordinal) == true));
    }
}
