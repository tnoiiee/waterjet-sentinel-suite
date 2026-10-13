using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Wjss.TcAcquisition.Tests;

public enum ReplyShape { Normal, Short, Excessive, OddData, ProtocolId, Exception, Closed, Delayed }

// Read-response fixture only. Fixed loopback bind, ephemeral port, no outbound connection.
internal sealed class LoopbackResponder : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(10));
    private readonly Task _task;
    private readonly ReplyShape _shape;
    private readonly int _reads;
    public int Port { get; }
    public TaskCompletionSource<byte[]> RequestReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public byte[]? LastRequest { get; private set; }

    public LoopbackResponder(ReplyShape shape = ReplyShape.Normal, int reads = 1)
    {
        _shape = shape;
        _reads = reads;
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        if (Port == 502) throw new InvalidOperationException("EPHEMERAL_PORT_REJECTED");
        _task = ServeAsync();
    }

    private async Task ServeAsync()
    {
        try
        {
            using var socket = await _listener.AcceptTcpClientAsync(_stop.Token);
            using var stream = socket.GetStream();
            for (var n = 0; n < _reads; n++)
            {
                var request = new byte[12];
                await stream.ReadExactlyAsync(request, _stop.Token);
                LastRequest = request;
                RequestReceived.TrySetResult(request);
                if (_shape == ReplyShape.Closed) { socket.Client.LingerState = new LingerOption(true, 0); return; }
                if (_shape == ReplyShape.Delayed) { await Task.Delay(Timeout.InfiniteTimeSpan, _stop.Token); return; }
                var count = _shape == ReplyShape.Short ? 39 : _shape == ReplyShape.Excessive ? 41 : 40;
                var dataBytes = _shape == ReplyShape.OddData ? 81 : count * 2;
                var response = new byte[_shape == ReplyShape.Exception ? 9 : 9 + dataBytes];
                request.AsSpan(0, 2).CopyTo(response);
                BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), (ushort)(_shape == ReplyShape.ProtocolId ? 1 : 0));
                BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), (ushort)(response.Length - 6));
                response[6] = request[6];
                response[7] = _shape == ReplyShape.Exception ? (byte)(request[7] | 0x80) : request[7];
                response[8] = _shape == ReplyShape.Exception ? (byte)2 : (byte)dataBytes;
                if (_shape != ReplyShape.Exception)
                    for (var i = 0; i < count; i++) BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(9 + i * 2, 2), (ushort)(0x1234 + i + n));
                // Deliberate fragmentation proves stream framing rather than packet-size assumptions.
                await stream.WriteAsync(response.AsMemory(0, 7), _stop.Token);
                await stream.WriteAsync(response.AsMemory(7), _stop.Token);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
            // Fixture stop/peer cancellation only; the owned task is observed in DisposeAsync.
            RequestReceived.TrySetCanceled();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try { await _task; }
        finally { _stop.Dispose(); }
    }
}
