// H2 Physical Process-I/O — standard TCP socket transport.
// Handles binary framing for FC01, FC03, FC05, and FC16 over standard TCP streams (node:net).
// Zero third-party dependencies. Fail-closed error handling with transaction ledger capture.

import net from 'node:net';

export class PhysicalProcessIoTransport {
  constructor(endpoints = {}) {
    this.endpoints = { ...endpoints };
    this.connections = new Map();
    this.ledger = [];
    this.txCounter = 0;
    this.closed = false;
  }

  getLedger() {
    return [...this.ledger];
  }

  async writeCoil(deviceKey, address, value) {
    return this._execute(deviceKey, address, 'WRITE_COIL', (txId, unitId) => {
      // FC05: Write Single Coil
      const buf = Buffer.alloc(12);
      buf.writeUInt16BE(txId, 0);
      buf.writeUInt16BE(0, 2); // Protocol ID
      buf.writeUInt16BE(6, 4); // Length
      buf.writeUInt8(unitId, 6);
      buf.writeUInt8(0x05, 7);
      buf.writeUInt16BE(address, 8);
      buf.writeUInt16BE(value ? 0xFF00 : 0x0000, 10);
      return { request: buf, expectedResponseBytes: 12, expectedFc: 0x05, parse: () => value };
    });
  }

  async writeRegisters(deviceKey, startAddress, values) {
    if (!values || values.length === 0) return;
    return this._execute(deviceKey, startAddress, 'WRITE_REGISTERS', (txId, unitId) => {
      // FC16 (0x10): Write Multiple Registers
      const byteCount = values.length * 2;
      const buf = Buffer.alloc(13 + byteCount);
      buf.writeUInt16BE(txId, 0);
      buf.writeUInt16BE(0, 2);
      buf.writeUInt16BE(7 + byteCount, 4);
      buf.writeUInt8(unitId, 6);
      buf.writeUInt8(0x10, 7);
      buf.writeUInt16BE(startAddress, 8);
      buf.writeUInt16BE(values.length, 10);
      buf.writeUInt8(byteCount, 12);
      for (let i = 0; i < values.length; i++) {
        buf.writeUInt16BE(values[i], 13 + i * 2);
      }
      return { request: buf, expectedResponseBytes: 12, expectedFc: 0x10, parse: () => values.length };
    });
  }

  async readCoil(deviceKey, address) {
    return this._execute(deviceKey, address, 'READ_COIL', (txId, unitId) => {
      // FC01: Read Coils (qty 1)
      const buf = Buffer.alloc(12);
      buf.writeUInt16BE(txId, 0);
      buf.writeUInt16BE(0, 2);
      buf.writeUInt16BE(6, 4);
      buf.writeUInt8(unitId, 6);
      buf.writeUInt8(0x01, 7);
      buf.writeUInt16BE(address, 8);
      buf.writeUInt16BE(1, 10);
      return {
        request: buf,
        expectedResponseBytes: 10,
        expectedFc: 0x01,
        parse: (res) => (res[9] & 0x01) !== 0,
      };
    });
  }

  async readRegister(deviceKey, address) {
    return this._execute(deviceKey, address, 'READ_REGISTER', (txId, unitId) => {
      // FC03: Read Holding Registers (qty 1)
      const buf = Buffer.alloc(12);
      buf.writeUInt16BE(txId, 0);
      buf.writeUInt16BE(0, 2);
      buf.writeUInt16BE(6, 4);
      buf.writeUInt8(unitId, 6);
      buf.writeUInt8(0x03, 7);
      buf.writeUInt16BE(address, 8);
      buf.writeUInt16BE(1, 10);
      return {
        request: buf,
        expectedResponseBytes: 11,
        expectedFc: 0x03,
        parse: (res) => res.readUInt16BE(9),
      };
    });
  }

  async executeOutputCleanup(catalogue) {
    const errors = [];
    for (const row of catalogue) {
      if (row.bindingState === 'UNBOUND' || !row.binding) continue;
      if (row.binding.direction !== 'WRITE') continue;

      try {
        if (row.binding.area === 'COIL') {
          await this.writeCoil(row.binding.deviceKey, row.binding.address, false);
        } else if (row.binding.area === 'HOLDING_REGISTER') {
          await this.writeRegisters(row.binding.deviceKey, row.binding.address, [0]);
        }
      } catch (err) {
        errors.push(`CLEANUP_FAILED for ${row.tag}: ${err.message}`);
      }
    }
    return errors;
  }

  async _execute(deviceKey, address, operation, builder) {
    if (this.closed) throw new Error('Transport is disposed');
    const endpoint = this.endpoints[deviceKey];
    if (!endpoint) throw new Error(`No endpoint configured for device key ${deviceKey}`);

    const txId = ++this.txCounter;
    const unitId = endpoint.unitId ?? 1;
    const timeoutMs = endpoint.timeoutMs ?? 1000;
    const { request, expectedResponseBytes, expectedFc, parse } = builder(txId, unitId);

    const startedAt = Date.now();
    try {
      const socket = await this._getSocket(deviceKey, endpoint);
      const response = await this._sendAndReceive(socket, request, expectedResponseBytes, timeoutMs);
      this._validateHeader(response, txId, unitId, expectedFc);
      const result = parse(response);

      this.ledger.push({
        transactionId: txId,
        deviceKey,
        operation,
        functionCode: `FC${expectedFc.toString(10).padStart(2, '0')}`,
        address,
        status: 'SUCCESS',
        elapsedMs: Date.now() - startedAt,
        timestamp: new Date(startedAt).toISOString(),
      });
      return result;
    } catch (err) {
      this.ledger.push({
        transactionId: txId,
        deviceKey,
        operation,
        functionCode: `FC${expectedFc.toString(10).padStart(2, '0')}`,
        address,
        status: 'ERROR',
        elapsedMs: Date.now() - startedAt,
        timestamp: new Date(startedAt).toISOString(),
        diagnostic: err.message,
      });
      // Invalidate faulty connection
      const existing = this.connections.get(deviceKey);
      if (existing) {
        existing.destroy();
        this.connections.delete(deviceKey);
      }
      throw err;
    }
  }

  async _getSocket(deviceKey, endpoint) {
    let socket = this.connections.get(deviceKey);
    if (socket && !socket.destroyed) return socket;

    return new Promise((resolve, reject) => {
      const targetAddress = endpoint.targetAddress ?? endpoint.address;
      const port = endpoint.port ?? 502;
      const s = net.createConnection({ host: targetAddress, port, timeout: endpoint.timeoutMs ?? 1000 }, () => {
        s.removeAllListeners('error');
        this.connections.set(deviceKey, s);
        resolve(s);
      });
      s.once('error', (err) => {
        s.destroy();
        reject(err);
      });
      s.once('timeout', () => {
        s.destroy();
        reject(new Error(`TCP connect timeout (${endpoint.timeoutMs ?? 1000} ms)`));
      });
    });
  }

  _sendAndReceive(socket, request, expectedBytes, timeoutMs) {
    return new Promise((resolve, reject) => {
      let chunks = [];
      let total = 0;
      let timer;

      const cleanup = () => {
        if (timer) clearTimeout(timer);
        socket.removeListener('data', onData);
        socket.removeListener('error', onError);
      };

      const onData = (data) => {
        chunks.push(data);
        total += data.length;
        if (total >= expectedBytes) {
          cleanup();
          resolve(Buffer.concat(chunks, total));
        }
      };

      const onError = (err) => {
        cleanup();
        reject(err);
      };

      timer = setTimeout(() => {
        cleanup();
        reject(new Error(`TCP read timeout (${timeoutMs} ms)`));
      }, timeoutMs);

      socket.on('data', onData);
      socket.once('error', onError);
      socket.write(request);
    });
  }

  _validateHeader(frame, expectedTxId, expectedUnitId, expectedFc) {
    if (frame.length < 8) throw new Error('Frame too short');
    const txId = frame.readUInt16BE(0);
    const protoId = frame.readUInt16BE(2);
    if (protoId !== 0) throw new Error(`Invalid protocol id: ${protoId}`);
    const fc = frame[7];
    if ((fc & 0x80) !== 0) {
      const exc = frame[8] ?? 0;
      throw new Error(`Protocol exception response: FC0x${fc.toString(16)} Code=0x${exc.toString(16)}`);
    }
    if (fc !== expectedFc) throw new Error(`Unexpected FC: 0x${fc.toString(16)} (expected 0x${expectedFc.toString(16)})`);
  }

  close() {
    this.closed = true;
    for (const [, socket] of this.connections) {
      try { socket.destroy(); } catch {}
    }
    this.connections.clear();
  }
}
