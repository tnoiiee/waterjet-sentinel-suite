// WJSS Stage 0.2.1A — render counters for render-isolation evidence (spike diagnostics).

export const renderCounter = {
  sensorCellRenders: 0,
  perSensor: new Map<string, number>(),
  record(sensorId: string): void {
    this.sensorCellRenders += 1;
    this.perSensor.set(sensorId, (this.perSensor.get(sensorId) ?? 0) + 1);
  },
  reset(): void {
    this.sensorCellRenders = 0;
    this.perSensor.clear();
  },
};
