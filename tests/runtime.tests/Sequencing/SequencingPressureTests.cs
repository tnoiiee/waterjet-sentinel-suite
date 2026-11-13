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

    // ------------------------------------------------------- Pump-stopped supervision (D-sim, B2)
    //
    // These facts pin the TWO EXTRA classifiers a declared-Pump run is allowed to use. They never
    // replace the physical ones: a fact at the end of this section re-reads the physical route to prove
    // the inference it makes from pressure is still there, unchanged, for every boundary that declares
    // nothing.

    [Theory]
    [InlineData(0.0, true, false, ValveOpenResolution.OPEN_CONFIRMED, ValveDiagnosis.NONE)]
    [InlineData(0.999, true, true, ValveOpenResolution.OPEN_CONFIRMED, ValveDiagnosis.NONE)]
    [InlineData(0.0, false, true, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_FAILED_TO_OPEN)]
    [InlineData(0.5, false, true, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_FAILED_TO_OPEN)]
    [InlineData(1.0, true, false, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION)]
    [InlineData(1.0, false, false, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION)]
    [InlineData(15.0, false, true, ValveOpenResolution.BLOCKED, ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION)]
    public void PumpStoppedOpenMatrix(
        double bar,
        bool upper,
        bool timeout,
        ValveOpenResolution resolution,
        ValveDiagnosis diagnosis)
    {
        // Under an attested Pump stop the Upper limit is the open proof, at any bar below the low band and
        // with or without the window having elapsed; any live pressure at or above the band is a
        // contradiction that fails immediately instead of waiting, and it stays a contradiction even at
        // the high band, where the physical route would have inferred the open from pressure alone.
        var result = SequencingPressure.OpenPumpStopped(Sample(bar), upper, timeout, Thresholds);
        Assert.Equal(resolution, result.Open);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Null(result.Close);
    }

    [Fact]
    public void PumpStoppedOpenWaitsForTheUpperLimitInsideTheWindow()
    {
        // 0 bar with the Pump stopped is the expected state of the line, so it proves nothing yet: the
        // window stays open and no band is inferred, exactly as the physical route holds a pending read.
        var pending = SequencingPressure.OpenPumpStopped(Sample(0.0), false, false, Thresholds);
        Assert.Null(pending.Open);
        Assert.Equal(ValveDiagnosis.NONE, pending.Diagnosis);
    }

    [Fact]
    public void PumpStoppedOpenNeverInfersAnOpenFromPressureAlone()
    {
        // The widening never turns the declared run into a pressure-only open: for every bar at or above
        // the low band and every limit/timeout combination the answer is BLOCKED with the contradiction,
        // and OPEN_BY_PRESSURE never appears at all in this route.
        foreach (var bar in new[] { 1.0, 1.001, 8.0, 14.999, 15.0, 20.0, 1_000.0 })
        {
            foreach (var upper in new[] { true, false })
            {
                foreach (var timeout in new[] { true, false })
                {
                    var result = SequencingPressure.OpenPumpStopped(Sample(bar), upper, timeout, Thresholds);
                    Assert.Equal(ValveOpenResolution.BLOCKED, result.Open);
                    Assert.Equal(ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION, result.Diagnosis);
                }
            }
        }
    }

    [Fact]
    public void PumpStoppedOpenRefusesAnInvalidPressureInput()
    {
        // Quality and source validity are still judged first, in this order and with no exception: an
        // unreadable observation is never read as "0 bar under a stopped Pump" just because that value
        // would now satisfy the open, and the refusal is the same fail-closed class the physical route uses.
        foreach (var sample in new PressureSample?[]
        {
            null, Sample(null), Sample(double.NaN), Sample(double.PositiveInfinity),
            Sample(double.NegativeInfinity), Sample(-0.1), Sample(16, PressureQuality.BAD),
            Sample(16, PressureQuality.STALE), Sample(16, stale: true),
        })
        {
            var result = SequencingPressure.OpenPumpStopped(sample, true, true, Thresholds);
            Assert.Equal(ValveOpenResolution.BLOCKED, result.Open);
            Assert.Equal(ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID, result.Diagnosis);
        }
    }

    [Theory]
    [InlineData(0.5, true, false, ValveCloseResolution.LOWER_LIMIT_CONFIRMED, ValveDiagnosis.NONE)]
    [InlineData(0.0, true, true, ValveCloseResolution.LOWER_LIMIT_CONFIRMED, ValveDiagnosis.NONE)]
    [InlineData(0.0, true, false, ValveCloseResolution.LOWER_LIMIT_CONFIRMED, ValveDiagnosis.NONE)]
    [InlineData(1.0, true, false, ValveCloseResolution.LEAK_SUSPECTED, ValveDiagnosis.VALVE_LEAK_SUSPECTED)]
    [InlineData(8.0, false, true, ValveCloseResolution.LEAK_SUSPECTED, ValveDiagnosis.VALVE_LEAK_SUSPECTED)]
    [InlineData(15.0, true, false, ValveCloseResolution.NOT_FULLY_CLOSED, ValveDiagnosis.VALVE_NOT_FULLY_CLOSED)]
    [InlineData(0.5, false, true, ValveCloseResolution.NOT_FULLY_CLOSED, ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT)]
    [InlineData(0.0, false, true, ValveCloseResolution.NOT_FULLY_CLOSED, ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT)]
    public void PumpStoppedCloseMatrix(
        double bar,
        bool lower,
        bool timeout,
        ValveCloseResolution resolution,
        ValveDiagnosis diagnosis)
    {
        // The bands that prove a valve is NOT closed are kept exactly as the physical classifier reads
        // them, and they are judged before the limit: pressure under a stopped Pump can contradict a
        // closed claim but never support one. Below the low band the Lower limit is the only closure proof,
        // and a window that ends without it stays a failure instead of a pressure inference.
        var result = SequencingPressure.ClosePumpStopped(Sample(bar), lower, timeout, Thresholds);
        Assert.Equal(resolution, result.Close);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Null(result.Open);
    }

    [Fact]
    public void PumpStoppedCloseWaitsForTheLowerLimitInsideTheWindow()
    {
        var pending = SequencingPressure.ClosePumpStopped(Sample(0.0), false, false, Thresholds);
        Assert.Null(pending.Close);
        Assert.Equal(ValveDiagnosis.NONE, pending.Diagnosis);
    }

    [Fact]
    public void PumpStoppedCloseNeverInfersClosureFromAnExpectedZeroBar()
    {
        // This is the clause the widening must NOT relax: CLOSED_BY_PRESSURE is the physical route's way of
        // trusting a low reading as evidence of a closed valve, and under an attested stop a low reading is
        // simply what the line looks like, so it can never close the claim. The resolution stays non-null
        // so the supervision terminates and the cleanup record keeps its failure state.
        foreach (var bar in new[] { 0.0, 0.25, 0.5, 0.999 })
        {
            var result = SequencingPressure.ClosePumpStopped(Sample(bar), false, true, Thresholds);
            Assert.NotEqual(ValveCloseResolution.CLOSED_BY_PRESSURE, result.Close);
            Assert.Equal(ValveCloseResolution.NOT_FULLY_CLOSED, result.Close);
            Assert.Equal(ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT, result.Diagnosis);
        }
    }

    [Fact]
    public void PumpStoppedCloseRefusesAnInvalidPressureInput()
    {
        foreach (var sample in new PressureSample?[]
        {
            null, Sample(null), Sample(double.NaN), Sample(double.PositiveInfinity),
            Sample(double.NegativeInfinity), Sample(-0.1), Sample(0.0, PressureQuality.BAD),
            Sample(0.0, PressureQuality.STALE), Sample(0.0, stale: true),
        })
        {
            var result = SequencingPressure.ClosePumpStopped(sample, true, true, Thresholds);
            Assert.Equal(ValveCloseResolution.PRESSURE_INPUT_INVALID, result.Close);
            Assert.Equal(ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID, result.Diagnosis);
        }
    }

    [Fact]
    public void ThePhysicalClassifiersKeepTheirPressureInferenceUnchanged()
    {
        // The non-interference pin. Both extra classifiers are new members beside Open and Close, so the
        // physical route still infers an open by pressure at the high band with an Upper-limit fault, and
        // still closes a valve by pressure when the Lower limit is missing. A run that did not enter the
        // declared mode cannot reach the two new members from the composition at all.
        var open = SequencingPressure.Open(Sample(20.0), false, true, Thresholds);
        Assert.Equal(ValveOpenResolution.OPEN_BY_PRESSURE, open.Open);
        Assert.Equal(ValveDiagnosis.UPPER_LIMIT_SENSOR_FAULT, open.Diagnosis);

        var close = SequencingPressure.Close(Sample(0.5), false, true, Thresholds);
        Assert.Equal(ValveCloseResolution.CLOSED_BY_PRESSURE, close.Close);
        Assert.Equal(ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT, close.Diagnosis);

        // And the physical open still accepts the high band as a confirmed open with the limit detected,
        // while the Pump-stopped route calls that same reading a contradiction.
        Assert.Equal(
            ValveOpenResolution.OPEN_CONFIRMED,
            SequencingPressure.Open(Sample(20.0), true, false, Thresholds).Open);
        Assert.Equal(
            ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION,
            SequencingPressure.OpenPumpStopped(Sample(20.0), true, false, Thresholds).Diagnosis);
    }

}
