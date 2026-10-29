using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Wjss.Contracts;

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
/// Handles binary framing for FC01, FC02, FC03, FC04, FC05, FC06, and FC16 over standard TCP network streams.
/// Zero third-party dependencies. Fail-closed error handling with raw transaction ledger capture.
/// </summary>
public sealed class ProcessIoPhysicalTransport : IProcessIoTransport, IDisposable
{
    private readonly Dictionary<string, ProcessIoEndpointConfig> _endpoints;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new(StringComparer.Ordinal);
    private readonly List<ProcessIoTransportLedgerEntry> _ledger = new();
    private readonly object _lock = new();
    private int _txCounter;
    private int _networkAttempts;
    private bool _disposed;

    public ProcessIoPhysicalTransport(IReadOnlyDictionary<string, ProcessIoEndpointConfig> endpoints)
    {
        _endpoints = new Dictionary<string, ProcessIoEndpointConfig>(endpoints, StringComparer.Ordinal);
    }

    public int NetworkAttempts => _networkAttempts;

    public IReadOnlyList<ProcessIoTransportLedgerEntry> GetLedger()
    {
        lock (_lock)
        {
            return _ledger.ToArray();
        }
    }

    public bool ReadDiscreteInput(string deviceKey, int address)
    {
        bool result = false;
        Execute(deviceKey, (stream, config, txId) =>
        {
            // FC02: Read Discrete Inputs (quantity 1)
            byte[] request = new byte[12];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0); // Protocol ID
            WriteBe16(request, 4, 6); // Length
            request[6] = config.UnitId;
            request[7] = 0x02; // FC02
            WriteBe16(request, 8, (ushort)address);
            WriteBe16(request, 10, 1); // Qty = 1

            stream.Write(request, 0, request.Length);
            // MBAP(7) + FC(1) + ByteCount(1) + 1 byte data = 10 bytes
            byte[] response = ReadExact(stream, 10, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x02);
            result = (response[9] & 0x01) != 0;
            return (0x02, 1, result ? "TRUE" : "FALSE");
        }, "READ_DISCRETE_INPUT", address);
        return result;
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
            result = (response[9] & 0x01) != 0;
            return (0x01, 1, result ? "TRUE" : "FALSE");
        }, "READ_COIL", address);
        return result;
    }

    public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity)
    {
        var result = new List<bool>(quantity);
        Execute(deviceKey, (stream, config, txId) =>
        {
            byte[] request = new byte[12];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0);
            WriteBe16(request, 4, 6);
            request[6] = config.UnitId;
            request[7] = 0x01; // FC01
            WriteBe16(request, 8, (ushort)startAddress);
            WriteBe16(request, 10, (ushort)quantity);

            stream.Write(request, 0, request.Length);
            int byteCount = (quantity + 7) / 8;
            byte[] response = ReadExact(stream, 9 + byteCount, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x01);
            for (int i = 0; i < quantity; i++)
            {
                int byteIdx = 9 + (i / 8);
                int bitIdx = i % 8;
                result.Add((response[byteIdx] & (1 << bitIdx)) != 0);
            }
            return (0x01, quantity, $"COUNT={quantity}");
        }, "READ_COILS_BULK", startAddress);
        return result;
    }

    public ushort ReadInputRegister(string deviceKey, int address)
    {
        ushort result = 0;
        Execute(deviceKey, (stream, config, txId) =>
        {
            // FC04: Read Input Registers (quantity 1)
            byte[] request = new byte[12];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0); // Protocol ID
            WriteBe16(request, 4, 6); // Length
            request[6] = config.UnitId;
            request[7] = 0x04; // FC04
            WriteBe16(request, 8, (ushort)address);
            WriteBe16(request, 10, 1); // Qty = 1

            stream.Write(request, 0, request.Length);
            // MBAP(7) + FC(1) + ByteCount(1) + 2 bytes data = 11 bytes
            byte[] response = ReadExact(stream, 11, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x04);
            result = ReadBe16(response, 9);
            return (0x04, 1, $"0x{result:X4}");
        }, "READ_INPUT_REGISTER", address);
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

    public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity)
    {
        var result = new List<ushort>(quantity);
        Execute(deviceKey, (stream, config, txId) =>
        {
            byte[] request = new byte[12];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0);
            WriteBe16(request, 4, 6);
            request[6] = config.UnitId;
            request[7] = 0x03; // FC03
            WriteBe16(request, 8, (ushort)startAddress);
            WriteBe16(request, 10, (ushort)quantity);

            stream.Write(request, 0, request.Length);
            int byteCount = quantity * 2;
            byte[] response = ReadExact(stream, 9 + byteCount, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x03);
            for (int i = 0; i < quantity; i++)
            {
                result.Add(ReadBe16(response, 9 + i * 2));
            }
            return (0x03, quantity, $"COUNT={quantity}");
        }, "READ_HOLDING_REGISTERS_BULK", startAddress);
        return result;
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

    public void WriteSingleRegister(string deviceKey, int address, ushort value)
    {
        Execute(deviceKey, (stream, config, txId) =>
        {
            // FC06: Write Single Register
            byte[] request = new byte[12];
            WriteBe16(request, 0, (ushort)txId);
            WriteBe16(request, 2, 0); // Protocol ID
            WriteBe16(request, 4, 6); // Length
            request[6] = config.UnitId;
            request[7] = 0x06; // FC06
            WriteBe16(request, 8, (ushort)address);
            WriteBe16(request, 10, value);

            stream.Write(request, 0, request.Length);
            byte[] response = ReadExact(stream, 12, config.TimeoutMs);
            ValidateHeader(response, txId, config.UnitId, 0x06);
            return (0x06, 1, $"0x{value:X4}");
        }, "WRITE_SINGLE_REGISTER", address);
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
                    WriteSingleRegister(row.Binding.DeviceKey, row.Binding.Address, 0);
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
        if (config.UnitId < ProcessIoTransportShape.MinUnitId || config.UnitId > ProcessIoTransportShape.MaxUnitId)
        {
            throw new ArgumentOutOfRangeException(nameof(config.UnitId), $"Unit ID {config.UnitId} is out of valid physical range (1-247).");
        }

        Interlocked.Increment(ref _networkAttempts);
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
        var connectAsync = client.ConnectAsync(config.TargetAddress, config.Port);
        if (!connectAsync.Wait(config.TimeoutMs))
        {
            client.Dispose();
            throw new TimeoutException($"TCP connect timeout to {deviceKey} ({config.TargetAddress}:{config.Port}) after {config.TimeoutMs}ms");
        }

        _clients[deviceKey] = client;
        return client;
    }

    private static byte[] ReadExact(NetworkStream stream, int expectedBytes, int timeoutMs)
    {
        byte[] buffer = new byte[expectedBytes];
        int totalRead = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (totalRead < expectedBytes)
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException($"ReadExact timed out after {timeoutMs}ms (read {totalRead}/{expectedBytes} bytes).");
            }

            int read = stream.Read(buffer, totalRead, expectedBytes - totalRead);
            if (read == 0)
            {
                throw new IOException($"Socket closed prematurely (read {totalRead}/{expectedBytes} bytes).");
            }
            totalRead += read;
        }

        return buffer;
    }

    private static void ValidateHeader(byte[] header, int expectedTxId, byte expectedUnitId, byte expectedFc)
    {
        if (header.Length < 8) throw new InvalidDataException("Response header shorter than 8 bytes.");

        ushort txId = ReadBe16(header, 0);
        ushort proto = ReadBe16(header, 2);
        byte unit = header[6];
        byte fc = header[7];

        if (txId != (ushort)expectedTxId)
        {
            throw new InvalidDataException($"Transaction ID mismatch: expected {expectedTxId}, got {txId}");
        }
        if (proto != 0)
        {
            throw new InvalidDataException($"Protocol ID mismatch: expected 0 (TCP fieldbus), got {proto}");
        }
        if (unit != expectedUnitId)
        {
            throw new InvalidDataException($"Unit ID mismatch: expected {expectedUnitId}, got {unit}");
        }

        if (fc == (expectedFc | 0x80))
        {
            byte exc = header.Length > 8 ? header[8] : (byte)0;
            throw new InvalidOperationException($"Protocol exception 0x{exc:X2} for FC0x{expectedFc:X2}");
        }

        if (fc != expectedFc)
        {
            throw new InvalidDataException($"Function code mismatch: expected 0x{expectedFc:X2}, got 0x{fc:X2}");
        }
    }

    private static void WriteBe16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }

    private static ushort ReadBe16(byte[] buffer, int offset)
    {
        return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
    }

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
