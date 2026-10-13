using Wjss.Adapters.TcAcquisition;
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
        Assert.Equal(40, result.RawWords!.Length);
        Assert.Equal((ushort)0x1234, result.RawWords[0]);
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
        var saved = first.RawWords!.ToArray();
        var second = await client.ReadAsync(Request(client, endpoint), CancellationToken.None);
        Assert.Equal(saved, first.RawWords);
        Assert.NotEqual(first.RawWords[0], second.RawWords![0]);
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
    public void Public_Boundary_Has_No_Write_Or_Generic_Execution_Operation()
    {
        var methods = typeof(ITcReadOnlyClient).GetMethods().Select(m => m.Name).ToArray();
        Assert.DoesNotContain(methods, name => name.Contains("Write", StringComparison.OrdinalIgnoreCase) || name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
        Assert.All(typeof(TcReadResult).GetProperties(), p => Assert.True(p.PropertyType.Namespace?.StartsWith("Wjss", StringComparison.Ordinal) == true || p.PropertyType.Namespace?.StartsWith("System", StringComparison.Ordinal) == true));
    }
}
