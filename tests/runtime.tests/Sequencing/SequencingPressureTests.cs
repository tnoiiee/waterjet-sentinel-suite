using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

public sealed class SequencingPressureTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
    private static readonly SequencingPressureThresholds Thresholds = new();
    private static PressureSample Sample(double? pressure, PressureQuality quality = PressureQuality.GOOD, bool stale = false) =>
        new(pressure, quality, stale, Now, PressureSample.ValveOutletSource("IV1"));

    private static PressureSample Pump(double? pressure, PressureQuality quality = PressureQuality.GOOD) =>
        new(pressure, quality, false, Now, PressureSample.PumpOutletSource);

    [Fact]
    public void DevelopmentThresholdsAndPumpGate()
    {
        Assert.True(Thresholds.Valid);
        Assert.Equal(1, Thresholds.LowPressureThresholdBar);
        Assert.Equal(15, Thresholds.HighPressureThresholdBar);
        Assert.Equal(15, Thresholds.PumpReadySetpointBar);
        Assert.False(new SequencingPressureThresholds(-1, 15).Valid);
        Assert.False(new SequencingPressureThresholds(1, 1).Valid);
        Assert.False(new SequencingPressureThresholds(1, 0).Valid);
        Assert.False(new SequencingPressureThresholds(double.NaN, 15).Valid);
        Assert.False(new SequencingPressureThresholds(1, double.PositiveInfinity).Valid);
        Assert.False(new SequencingPressureThresholds(1, 15, double.NegativeInfinity).Valid);
        Assert.False(SequencingPressure.PumpReady(Pump(15), Thresholds));
        Assert.True(SequencingPressure.PumpReady(Pump(15.01), Thresholds));
        Assert.False(SequencingPressure.PumpReady(Pump(16, PressureQuality.BAD), Thresholds));
        Assert.False(SequencingPressure.PumpReady(Sample(100), Thresholds)); // IV1 cannot ready the Pump
        Assert.True(SequencingPressure.PumpReady(Pump(16), new(1, 200, 15)));
        Assert.False(SequencingPressure.PumpReady(Pump(16), new(1, 2, 16)));
    }

    [Fact]
    public void InvalidPressureCannotEnterAnyBand()
    {
        foreach (var sample in new PressureSample?[]
        {
            null, Sample(null), Sample(double.NaN), Sample(double.PositiveInfinity),
            Sample(double.NegativeInfinity), Sample(-0.1), Sample(16, PressureQuality.BAD),
            Sample(16, PressureQuality.STALE), Sample(16, stale: true),
        })
        {
            Assert.False(SequencingPressure.Valid(sample));
            Assert.False(SequencingPressure.PumpReady(sample, Thresholds));
            Assert.Equal(ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID, SequencingPressure.Open(sample, true, false, Thresholds).Diagnosis);
            Assert.Equal(ValveCloseResolution.PRESSURE_INPUT_INVALID, SequencingPressure.Close(sample, true, false, Thresholds).Close);
        }
    }

    [Fact]
    public void OpenWaitsForTheIndependentUpperLimitTimeout()
    {
        var pending = SequencingPressure.Open(Sample(20), false, false, Thresholds);
        Assert.Null(pending.Open);
        Assert.Equal(ValveDiagnosis.NONE, pending.Diagnosis);
    }

    [Theory]
    [InlineData(true, false, 0.5, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION)]
    [InlineData(true, false, 1.0, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_LEAK_SUSPECTED)]
    [InlineData(true, false, 15.0, ValveOpenResolution.OPEN_CONFIRMED, ValveDiagnosis.NONE)]
    [InlineData(false, true, 0.5, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_FAILED_TO_OPEN)]
    [InlineData(false, true, 1.0, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_LEAK_SUSPECTED)]
    [InlineData(false, true, 15.0, ValveOpenResolution.OPEN_BY_PRESSURE, ValveDiagnosis.UPPER_LIMIT_SENSOR_FAULT)]
    public void OpenMatrix(bool upper, bool timeout, double bar, ValveOpenResolution resolution, ValveDiagnosis diagnosis)
    {
        var result = SequencingPressure.Open(Sample(bar), upper, timeout, Thresholds);
        Assert.Equal(resolution, result.Open);
        Assert.Equal(diagnosis, result.Diagnosis);
    }

    [Theory]
    [InlineData(true, false, 0.5, ValveCloseResolution.LOWER_LIMIT_CONFIRMED, ValveDiagnosis.NONE)]
    [InlineData(true, false, 1.0, ValveCloseResolution.LEAK_SUSPECTED, ValveDiagnosis.VALVE_LEAK_SUSPECTED)]
    [InlineData(true, false, 15.0, ValveCloseResolution.NOT_FULLY_CLOSED, ValveDiagnosis.VALVE_NOT_FULLY_CLOSED)]
    [InlineData(false, true, 0.5, ValveCloseResolution.CLOSED_BY_PRESSURE, ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT)]
    [InlineData(false, true, 1.0, ValveCloseResolution.LEAK_SUSPECTED, ValveDiagnosis.VALVE_LEAK_SUSPECTED)]
    [InlineData(false, true, 15.0, ValveCloseResolution.NOT_FULLY_CLOSED, ValveDiagnosis.VALVE_NOT_FULLY_CLOSED)]
    public void CloseMatrix(bool lower, bool timeout, double bar, ValveCloseResolution resolution, ValveDiagnosis diagnosis)
    {
        var result = SequencingPressure.Close(Sample(bar), lower, timeout, Thresholds);
        Assert.Equal(resolution, result.Close);
        Assert.Equal(diagnosis, result.Diagnosis);
    }
}
