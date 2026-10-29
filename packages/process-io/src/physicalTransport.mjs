// H2 Physical Process-I/O — standard TCP socket transport.
// Handles binary framing for FC01, FC02, FC03, FC04, FC05, FC06, and FC16 over standard TCP streams (node:net).
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

  getRedactedLedger() {
    return this.ledger.map((entry) => ({
      ...entry,
      deviceKey: entry.deviceKey,
      endpoint: entry.endpoint ? entry.endpoint.replace(/\d+$/, 'xxx') : undefined,
    }));
  }

  async readDiscreteInput(deviceKey, address) {
    return this._execute(deviceKey, address, 'READ_DISCRETE_INPUT', (txId, unitId) => {
      // FC02: Read Discrete Inputs (qty 1)
      const buf = Buffer.alloc(12);
      buf.writeUInt16BE(txId, 0);
      buf.writeUInt16BE(0, 2); // Protocol ID
      buf.writeUInt16BE(6, 4); // Length
      buf.writeUInt8(unitId, 6);
      buf.writeUInt8(0x02, 7); // FC02
      buf.writeUInt16BE(address, 8);
      buf.writeUInt16BE(1, 10);
      return {
        request: buf,
        expectedResponseBytes: 10,
        expectedFc: 0x02,
        parse: (res) => (res[9] & 0x01) !== 0,
      };
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

  async readInputRegister(deviceKey, address) {
    return this._execute(deviceKey, address, 'READ_INPUT_REGISTER', (txId, unitId) => {
      // FC04: Read Input Registers (qty 1)
      const buf = Buffer.alloc(12);
      buf.writeUInt16BE(txId, 0);
      buf.writeUInt16BE(0, 2);
      buf.writeUInt16BE(6, 4);
      buf.writeUInt8(unitId, 6);
      buf.writeUInt8(0x04, 7); // FC04
      buf.writeUInt16BE(address, 8);
      buf.writeUInt16BE(1, 10);
      return {
        request: buf,
        expectedResponseBytes: 11,
        expectedFc: 0x04,
        parse: (res) => res.readUInt16BE(9),
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
      buf.writeUInt8(0x03, 7); // FC03
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

  async writeCoil(deviceKey, address, value) {
    return this._execute(deviceKey, address, 'WRITE_COIL', (txId, unitId) => {
      // FC05: Write Single Coil
      const buf = Buffer.alloc(12);
      buf.writeUInt16BE(txId, 0);
      buf.writeUInt16BE(0, 2); // Protocol ID
      buf.writeUInt16BE(6, 4); // Length
      buf.writeUInt8(unitId, 6);
      buf.writeUInt8(0x05, 7); // FC05
      buf.writeUInt16BE(address, 8);
      buf.writeUInt16BE(value ? 0xFF00 : 0x0000, 10);
      return { request: buf, expectedResponseBytes: 12, expectedFc: 0x05, parse: () => value };
    });
  }

  async writeSingleRegister(deviceKey, address, value) {
    return this._execute(deviceKey, address, 'WRITE_SINGLE_REGISTER', (txId, unitId) => {
      // FC06: Write Single Register
      const buf = Buffer.alloc(12);
      buf.writeUInt16BE(txId, 0);
      buf.writeUInt16BE(0, 2);
      buf.writeUInt16BE(6, 4);
      buf.writeUInt8(unitId, 6);
      buf.writeUInt8(0x06, 7); // FC06
      buf.writeUInt16BE(address, 8);
      buf.writeUInt16BE(value, 10);
      return { request: buf, expectedResponseBytes: 12, expectedFc: 0x06, parse: () => value };
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

  async executeOutputCleanup(touchedOutputs = []) {
    const errors = [];
    for (const item of touchedOutputs) {
      try {
        if (item.area === 'COIL' || item.ioClass === 'DO') {
          await this.writeCoil(item.deviceKey, item.address, false);
        } else if (item.area === 'HOLDING_REGISTER' || item.ioClass === 'AO') {
          await this.writeSingleRegister(item.deviceKey, item.address, 0);
        }
      } catch (err) {
        errors.push(`CLEANUP_FAILED for ${item.tag ?? item.address}: ${err.message}`);
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
    const { request, expectedResponseBytes, expectedFc, parse } = builder(txId, unitId);
    const start = Date.now();

    const ledgerEntry = {
      transactionId: txId,
      deviceKey,
      operation,
      functionCode: `FC${expectedFc.toString(16).padStart(2, '0').toUpperCase()}`,
      address,
      quantity: 1,
      payloadSummary: '',
      status: 'PENDING',
      elapsedMs: 0,
      timestamp: new Date(start).toISOString(),
      diagnostic: null,
    };

    try {
      const socket = await this._getSocket(deviceKey, endpoint);
      const resBuf = await this._sendAndReceive(socket, request, expectedResponseBytes, endpoint.timeoutMs ?? 1000);

      // Validate header
      const resTxId = resBuf.readUInt16BE(0);
      const resProto = resBuf.readUInt16BE(2);
      const resUnitId = resBuf.readUInt8(6);
      const resFc = resBuf.readUInt8(7);

      if (resTxId !== txId || resProto !== 0 || resUnitId !== unitId) {
        throw new Error(`Protocol header mismatch: tx=${resTxId}/${txId}, proto=${resProto}, unit=${resUnitId}/${unitId}`);
      }

      if (resFc === (expectedFc | 0x80)) {
        const excCode = resBuf.readUInt8(8);
        throw new Error(`Protocol exception 0x${excCode.toString(16).padStart(2, '0')} for ${operation}`);
      }

      if (resFc !== expectedFc) {
        throw new Error(`Unexpected function code in response: 0x${resFc.toString(16)} expected 0x${expectedFc.toString(16)}`);
      }

      const result = parse(resBuf);
      ledgerEntry.status = 'SUCCESS';
      ledgerEntry.payloadSummary = typeof result === 'boolean' ? (result ? 'TRUE' : 'FALSE') : String(result);
      ledgerEntry.elapsedMs = Date.now() - start;
      this.ledger.push(ledgerEntry);
      return result;
    } catch (err) {
      ledgerEntry.status = 'FAILED';
      ledgerEntry.diagnostic = err.message;
      ledgerEntry.elapsedMs = Date.now() - start;
      this.ledger.push(ledgerEntry);
      throw err;
    }
  }

  async _getSocket(deviceKey, endpoint) {
    let socket = this.connections.get(deviceKey);
    if (socket && !socket.destroyed && socket.writable) return socket;

    socket = await new Promise((resolve, reject) => {
      const s = new net.Socket();
      const timeout = endpoint.timeoutMs ?? 1000;
      s.setTimeout(timeout);

      s.once('connect', () => {
        s.removeAllListeners('error');
        s.removeAllListeners('timeout');
        resolve(s);
      });
      s.once('error', (err) => {
        s.destroy();
        reject(err);
      });
      s.once('timeout', () => {
        s.destroy();
        reject(new Error(`Connection timeout to ${deviceKey}`));
      });

      s.connect(endpoint.port ?? 502, endpoint.targetAddress);
    });

    this.connections.set(deviceKey, socket);
    return socket;
  }

  async _sendAndReceive(socket, request, expectedBytes, timeoutMs) {
    return new Promise((resolve, reject) => {
      let chunks = [];
      let totalBytes = 0;

      const onData = (data) => {
        chunks.push(data);
        totalBytes += data.length;
        if (totalBytes >= expectedBytes) {
          cleanup();
          resolve(Buffer.concat(chunks, totalBytes));
        }
      };

      const onError = (err) => {
        cleanup();
        reject(err);
      };

      const onTimeout = () => {
        cleanup();
        reject(new Error(`Socket read timeout (${timeoutMs}ms)`));
      };

      const cleanup = () => {
        socket.removeListener('data', onData);
        socket.removeListener('error', onError);
        socket.removeListener('timeout', onTimeout);
      };

      socket.setTimeout(timeoutMs);
      socket.on('data', onData);
      socket.once('error', onError);
      socket.once('timeout', onTimeout);

      socket.write(request, (err) => {
        if (err) {
          cleanup();
          reject(err);
        }
      });
    });
  }

  close() {
    this.closed = true;
    for (const socket of this.connections.values()) {
      if (!socket.destroyed) socket.destroy();
    }
    this.connections.clear();
  }
}
