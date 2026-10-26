using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Wjss.Contracts.ProcessIo;

namespace Wjss.Runtime.Core.ProcessIo;

/// <summary>Endpoint definition for physical Process-I/O communication.</summary>
public sealed record ProcessIoEndpointConfig
{
    public required string TargetAddress { get; init; }
    public int Port { get; init; } = ProcessIoTransportShape.Port;
    public byte UnitId { get; init; } = (byte)ProcessIoTransportShape.UnitId;
    public int TimeoutMs { get; init; } = ProcessIoTransportShape.ReadTimeoutMs;
}

/// <summary>Redacted record of a physical network transaction across the transport boundary.</summary>
public sealed record ProcessIoTransportLedgerEntry
{
    public required int TransactionId { get; init; }
    public required string DeviceKey { get; init; }
    public required string Operation { get; init; }
    public required string FunctionCode { get; init; }
    public required int Address { get; init; }
    public required int Quantity { get; init; }
    public required string PayloadSummary { get; init; }
    public required string Status { get; init; }
    public required double ElapsedMs { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? Diagnostic { get; init; }
}

/// <summary>
/// Physical Process-I/O TCP transport implementing the standard application boundary IProcessIoTransport.
/// Handles binary framing for FC01, FC03, FC05, and FC16 over standard TCP network streams.
/// Zero third-party dependencies. Fail-closed error handling with raw transaction ledger capture.
/// </summary>
public sealed class ProcessIoPhysicalTransport : IProcessIoTransport, IDisposable
{
    private readonly Dictionary<string, ProcessIoEndpointConfig> _endpoints;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new(StringComparer.Ordinal);
    private readonly List<ProcessIoTransportLedgerEntry> _ledger = new();
    private readonly object _lock = new();
    private int _txCounter;
    private bool _disposed;

    public ProcessIoPhysicalTransport(IReadOnlyDictionary<string, ProcessIoEndpointConfig> endpoints)
    {
        _endpoints = new Dictionary<string, ProcessIoEndpointConfig>(endpoints, StringComparer.Ordinal);
    }

    public IReadOnlyList<ProcessIoTransportLedgerEntry> GetLedger()
    {
        lock (_lock)
        {
            return _ledger.ToArray();
        }
    }

    public void WriteCoil(string deviceKey, int address, bool value)
    {
        Execute(deviceKey, (stream, config, txId) =>
        {
            // FC05: Write Single Coil (0xFF00 = ON, 0x0000 = OFF)
            byte[] request = new byte[12];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0); // Protocol ID
            WriteBe16(request, 4, 6); // Length
            request[6] = config.UnitId;
            request[7] = 0x05; // FC05
            WriteBe16(request, 8, (ushort)address);
            WriteBe16(request, 10, (ushort)(value ? 0xFF00 : 0x0000));

            stream.Write(request, 0, request.Length);
            byte[] response = ReadExact(stream, 12, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x05);
            return (0x05, 1, value ? "ON" : "OFF");
        }, "WRITE_COIL", address);
    }

    public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values)
    {
        if (values == null || values.Count == 0) return;

        Execute(deviceKey, (stream, config, txId) =>
        {
            // FC16 (0x10): Write Multiple Registers
            int byteCount = values.Count * 2;
            byte[] request = new byte[13 + byteCount];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0); // Protocol ID
            WriteBe16(request, 4, (ushort)(7 + byteCount)); // Length
            request[6] = config.UnitId;
            request[7] = 0x10; // FC16 (0x10)
            WriteBe16(request, 8, (ushort)startAddress);
            WriteBe16(request, 10, (ushort)values.Count);
            request[12] = (byte)byteCount;
            for (int i = 0; i < values.Count; i++)
            {
                WriteBe16(request, 13 + i * 2, values[i]);
            }

            stream.Write(request, 0, request.Length);
            byte[] response = ReadExact(stream, 12, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x10);
            return (0x10, values.Count, $"COUNT={values.Count}");
        }, "WRITE_REGISTERS", startAddress);
    }

    public bool ReadCoil(string deviceKey, int address)
    {
        bool result = false;
        Execute(deviceKey, (stream, config, txId) =>
        {
            // FC01: Read Coils (quantity 1)
            byte[] request = new byte[12];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0); // Protocol ID
            WriteBe16(request, 4, 6); // Length
            request[6] = config.UnitId;
            request[7] = 0x01; // FC01
            WriteBe16(request, 8, (ushort)address);
            WriteBe16(request, 10, 1); // Qty = 1

            stream.Write(request, 0, request.Length);
            // MBAP(7) + FC(1) + ByteCount(1) + 1 byte data = 10 bytes
            byte[] response = ReadExact(stream, 10, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x01);
            byte dataByte = response[9];
            result = (dataByte & 0x01) != 0;
            return (0x01, 1, result ? "1" : "0");
        }, "READ_COIL", address);
        return result;
    }

    public ushort ReadRegister(string deviceKey, int address)
    {
        ushort result = 0;
        Execute(deviceKey, (stream, config, txId) =>
        {
            // FC03: Read Holding Registers (quantity 1)
            byte[] request = new byte[12];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0); // Protocol ID
            WriteBe16(request, 4, 6); // Length
            request[6] = config.UnitId;
            request[7] = 0x03; // FC03
            WriteBe16(request, 8, (ushort)address);
            WriteBe16(request, 10, 1); // Qty = 1

            stream.Write(request, 0, request.Length);
            // MBAP(7) + FC(1) + ByteCount(1) + 2 bytes data = 11 bytes
            byte[] response = ReadExact(stream, 11, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x03);
            result = ReadBe16(response, 9);
            return (0x03, 1, $"0x{result:X4}");
        }, "READ_REGISTER", address);
        return result;
    }

    /// <summary>
    /// Deterministic cleanup attempt: writes all known bound outputs to safe state (OFF / 0).
    /// Ensures every output is attempted even if earlier operations encounter errors.
    /// </summary>
    public void ExecuteOutputCleanup(IReadOnlyList<ProcessIoCatalogueRow> catalogue, out List<string> cleanupErrors)
    {
        cleanupErrors = new List<string>();
        foreach (var row in catalogue)
        {
            if (row.BindingState == ProcessIoBindingState.UNBOUND || row.Binding == null) continue;
            if (row.Binding.Direction != "WRITE") continue;

            try
            {
                if (row.Binding.Area == ProcessIoArea.Coil)
                {
                    WriteCoil(row.Binding.DeviceKey, row.Binding.Address, false);
                }
                else if (row.Binding.Area == ProcessIoArea.HoldingRegister)
                {
                    WriteRegisters(row.Binding.DeviceKey, row.Binding.Address, new ushort[] { 0 });
                }
            }
            catch (Exception ex)
            {
                cleanupErrors.Add($"CLEANUP_FAILED for {row.Tag} at {row.Binding.DeviceKey}:{row.Binding.Address}: {ex.Message}");
            }
        }
    }

    private void Execute(
        string deviceKey,
        Func<NetworkStream, ProcessIoEndpointConfig, int, (byte fc, int qty, string summary)> action,
        string operation,
        int address)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ProcessIoPhysicalTransport));
        if (!_endpoints.TryGetValue(deviceKey, out var config))
        {
            throw new InvalidOperationException($"No endpoint configuration configured for device key '{deviceKey}'.");
        }

        int txId = Interlocked.Increment(ref _txCounter);
        var startedAt = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var client = GetOrCreateClient(deviceKey, config);
            var stream = client.GetStream();
            stream.ReadTimeout = config.TimeoutMs;
            stream.WriteTimeout = config.TimeoutMs;

            var (fc, qty, summary) = action(stream, config, txId);
            sw.Stop();

            lock (_lock)
            {
                _ledger.Add(new ProcessIoTransportLedgerEntry
                {
                    TransactionId = txId,
                    DeviceKey = deviceKey,
                    Operation = operation,
                    FunctionCode = $"FC{fc:D2}",
                    Address = address,
                    Quantity = qty,
                    PayloadSummary = summary,
                    Status = "SUCCESS",
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    Timestamp = startedAt,
                });
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            // Invalidate cached client on connection/protocol error
            if (_clients.TryRemove(deviceKey, out var faultyClient))
            {
                try { faultyClient.Dispose(); } catch { }
            }

            lock (_lock)
            {
                _ledger.Add(new ProcessIoTransportLedgerEntry
                {
                    TransactionId = txId,
                    DeviceKey = deviceKey,
                    Operation = operation,
                    FunctionCode = "FC??",
                    Address = address,
                    Quantity = 0,
                    PayloadSummary = "FAILED",
                    Status = "ERROR",
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    Timestamp = startedAt,
                    Diagnostic = ex.Message,
                });
            }
            throw;
        }
    }

    private TcpClient GetOrCreateClient(string deviceKey, ProcessIoEndpointConfig config)
    {
        if (_clients.TryGetValue(deviceKey, out var existing) && existing.Connected)
        {
            return existing;
        }

        var client = new TcpClient();
        var connectTask = client.ConnectAsync(config.TargetAddress, config.Port);
        if (!connectTask.Wait(config.TimeoutMs))
        {
            client.Dispose();
            throw new TimeoutException($"TCP connect to {config.TargetAddress}:{config.Port} timed out ({config.TimeoutMs} ms).");
        }

        _clients[deviceKey] = client;
        return client;
    }

    private static byte[] ReadExact(NetworkStream stream, int count, int timeoutMs)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        int remaining = count;
        while (remaining > 0)
        {
            int read = stream.Read(buffer, offset, remaining);
            if (read == 0) throw new EndOfStreamException($"Premature end of stream while reading {count} bytes.");
            offset += read;
            remaining -= read;
        }
        return buffer;
    }

    private static void ValidateHeader(byte[] frame, int expectedTxId, byte expectedUnitId, byte expectedFc)
    {
        if (frame.Length < 8) throw new InvalidDataException("Frame too short.");
        ushort txId = ReadBe16(frame, 0);
        ushort protoId = ReadBe16(frame, 2);
        if (protoId != 0) throw new InvalidDataException($"Invalid protocol identifier: {protoId}");
        byte unitId = frame[6];
        byte fc = frame[7];

        if ((fc & 0x80) != 0)
        {
            byte exc = frame.Length > 8 ? frame[8] : (byte)0;
            throw new InvalidOperationException($"Protocol exception response: FC0x{fc:X2} Code=0x{exc:X2}");
        }
        if (fc != expectedFc) throw new InvalidDataException($"Unexpected function code: 0x{fc:X2} (expected 0x{expectedFc:X2})");
    }

    private static void WriteBe16(byte[] buf, int offset, ushort val)
    {
        buf[offset] = (byte)(val >> 8);
        buf[offset + 1] = (byte)(val & 0xFF);
    }

    private static ushort ReadBe16(byte[] buf, int offset)
        => (ushort)((buf[offset] << 8) | buf[offset + 1]);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var client in _clients.Values)
        {
            try { client.Dispose(); } catch { }
        }
        _clients.Clear();
    }
}
