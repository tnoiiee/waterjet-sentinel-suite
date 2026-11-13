namespace Wjss.Runtime.Core.Sequencing;

/// <summary>SIMULATOR-only pressure input. Staleness is explicit; no production timing policy is inferred.</summary>
public sealed record PressureSample(double? Bar, PressureQuality Quality, bool Stale, DateTimeOffset At, string SourceId)
{
    public const string PumpOutletSource = "PUMP_OUTLET";
    public static string ValveOutletSource(string valveId) => valveId + "_OUTLET";
}
public enum PressureQuality { GOOD, BAD, STALE }
public enum ValveCloseResolution { LOWER_LIMIT_CONFIRMED, CLOSED_BY_PRESSURE, LEAK_SUSPECTED, NOT_FULLY_CLOSED, PRESSURE_INPUT_INVALID }
public enum ValveDiagnosis { NONE, UPPER_LIMIT_SENSOR_FAULT, LOWER_LIMIT_SENSOR_FAULT, VALVE_LEAK_SUSPECTED, VALVE_NOT_FULLY_CLOSED, VALVE_PRESSURE_INPUT_INVALID, VALVE_OPEN_FEEDBACK_CONTRADICTION, VALVE_FAILED_TO_OPEN }
public enum ValveOpenResolution { OPEN_CONFIRMED, OPEN_BY_PRESSURE, BLOCKED }

/// <summary>Explicit development-only thresholds, never a hardware-certified configuration.</summary>
public sealed record SequencingPressureThresholds(double LowPressureThresholdBar = 1.0, double HighPressureThresholdBar = 15.0, double PumpReadySetpointBar = 15.0)
{
    public const string InvalidCode = "INVALID_PRESSURE_THRESHOLDS";
    public bool Valid => double.IsFinite(LowPressureThresholdBar) && double.IsFinite(HighPressureThresholdBar)
        && double.IsFinite(PumpReadySetpointBar) && LowPressureThresholdBar >= 0
        && HighPressureThresholdBar > LowPressureThresholdBar && PumpReadySetpointBar >= 0;
}

public sealed record ValveAssessment(ValveOpenResolution? Open, ValveCloseResolution? Close, ValveDiagnosis Diagnosis);

/// <summary>Pure quality-first classification. Never assign an invalid reading to a pressure band.</summary>
public static class SequencingPressure
{
    public static bool Valid(PressureSample? sample) => sample is { Quality: PressureQuality.GOOD, Stale: false, Bar: { } bar }
        && double.IsFinite(bar) && bar >= 0 && sample.At.Offset == TimeSpan.Zero;

    public static bool PumpReady(PressureSample? sample, SequencingPressureThresholds thresholds) =>
        Valid(sample) && sample!.SourceId == PressureSample.PumpOutletSource
        && sample.Bar!.Value > thresholds.PumpReadySetpointBar;

    /// <summary>
    /// Time-aware validity: the base quality rules plus a future-dated refusal. A sample stamped after
    /// the caller's clock (beyond <paramref name="tolerance"/>) is not evidence of the present and is
    /// refused instead of being clamped to now. Default tolerance is zero: any future stamp is refused.
    /// </summary>
    public static bool Valid(PressureSample? sample, DateTimeOffset now, TimeSpan tolerance) =>
        Valid(sample) && sample!.At <= now + tolerance;

    /// <summary>Time-aware Pump-ready gate: the base gate plus the future-dated refusal.</summary>
    public static bool PumpReady(
        PressureSample? sample,
        SequencingPressureThresholds thresholds,
        DateTimeOffset now,
        TimeSpan tolerance) =>
        PumpReady(sample, thresholds) && sample!.At <= now + tolerance;

    public static ValveAssessment Open(PressureSample? sample, bool upperDetected, bool timedOut, SequencingPressureThresholds thresholds)
    {
        if (!Valid(sample)) return new(ValveOpenResolution.BLOCKED, null, ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID);
        var pressure = sample!.Bar!.Value;
        if (upperDetected)
            return pressure < thresholds.LowPressureThresholdBar
                ? new(ValveOpenResolution.BLOCKED, null, ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION)
                : pressure < thresholds.HighPressureThresholdBar
                    ? new(ValveOpenResolution.BLOCKED, null, ValveDiagnosis.VALVE_LEAK_SUSPECTED)
                    : new(ValveOpenResolution.OPEN_CONFIRMED, null, ValveDiagnosis.NONE);
        if (!timedOut) return new(null, null, ValveDiagnosis.NONE);
        return pressure < thresholds.LowPressureThresholdBar
            ? new(ValveOpenResolution.BLOCKED, null, ValveDiagnosis.VALVE_FAILED_TO_OPEN)
            : pressure < thresholds.HighPressureThresholdBar
                ? new(ValveOpenResolution.BLOCKED, null, ValveDiagnosis.VALVE_LEAK_SUSPECTED)
                : new(ValveOpenResolution.OPEN_BY_PRESSURE, null, ValveDiagnosis.UPPER_LIMIT_SENSOR_FAULT);
    }

    public static ValveAssessment Close(PressureSample? sample, bool lowerDetected, bool timedOut, SequencingPressureThresholds thresholds)
    {
        if (!Valid(sample)) return new(null, ValveCloseResolution.PRESSURE_INPUT_INVALID, ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID);
        if (!lowerDetected && !timedOut) return new(null, null, ValveDiagnosis.NONE);
        var pressure = sample!.Bar!.Value;
        if (pressure >= thresholds.HighPressureThresholdBar)
            return new(null, ValveCloseResolution.NOT_FULLY_CLOSED, ValveDiagnosis.VALVE_NOT_FULLY_CLOSED);
        if (pressure >= thresholds.LowPressureThresholdBar)
            return new(null, ValveCloseResolution.LEAK_SUSPECTED, ValveDiagnosis.VALVE_LEAK_SUSPECTED);
        if (lowerDetected) return new(null, ValveCloseResolution.LOWER_LIMIT_CONFIRMED, ValveDiagnosis.NONE);
        return timedOut
            ? new(null, ValveCloseResolution.CLOSED_BY_PRESSURE, ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT)
            : new(null, null, ValveDiagnosis.NONE);
    }

    /// <summary>
    /// The paired-IV open classifier for the ONE evidence class in which the Pump is Owner-attested
    /// STOPPED and Pump-ready was admitted from a declared value: no measured line pressure can exist
    /// while that valve travels, so the limit switch is the open proof and pressure is only ever a
    /// contradiction. Quality and source validity still come first, an invalid reading is never assigned
    /// to a pressure band, and a live reading at or above the low band fails closed IMMEDIATELY because it
    /// contradicts the attested stop. 0 bar never confirms an open by pressure here: without the Upper
    /// limit the supervision window simply ends as a failed open. This is the only difference from
    /// <see cref="Open"/>; the physical route above is untouched.
    /// </summary>
    public static ValveAssessment OpenPumpStopped(
        PressureSample? sample,
        bool upperDetected,
        bool timedOut,
        SequencingPressureThresholds thresholds)
    {
        if (!Valid(sample)) return new(ValveOpenResolution.BLOCKED, null, ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID);
        if (sample!.Bar!.Value >= thresholds.LowPressureThresholdBar)
            return new(ValveOpenResolution.BLOCKED, null, ValveDiagnosis.VALVE_OPEN_FEEDBACK_CONTRADICTION);
        if (upperDetected) return new(ValveOpenResolution.OPEN_CONFIRMED, null, ValveDiagnosis.NONE);
        if (!timedOut) return new(null, null, ValveDiagnosis.NONE);
        return new(ValveOpenResolution.BLOCKED, null, ValveDiagnosis.VALVE_FAILED_TO_OPEN);
    }

    /// <summary>
    /// The paired-IV close classifier for the same Pump-stopped evidence class. The bands that prove a
    /// valve is NOT closed are kept unchanged, because pressure under a stopped Pump can only ever
    /// contradict a closed claim, never support one; and a timeout without the Lower limit stays
    /// NOT_FULLY_CLOSED instead of <see cref="ValveCloseResolution.CLOSED_BY_PRESSURE"/>, because 0 bar
    /// with the Pump stopped is the expected state of the whole line and proves nothing about this valve.
    /// The classification is always non-null once the window ends, so the supervision terminates and the
    /// cleanup record keeps its failure state.
    /// </summary>
    public static ValveAssessment ClosePumpStopped(
        PressureSample? sample,
        bool lowerDetected,
        bool timedOut,
        SequencingPressureThresholds thresholds)
    {
        if (!Valid(sample))
            return new(null, ValveCloseResolution.PRESSURE_INPUT_INVALID, ValveDiagnosis.VALVE_PRESSURE_INPUT_INVALID);
        if (!lowerDetected && !timedOut) return new(null, null, ValveDiagnosis.NONE);
        var pressure = sample!.Bar!.Value;
        if (pressure >= thresholds.HighPressureThresholdBar)
            return new(null, ValveCloseResolution.NOT_FULLY_CLOSED, ValveDiagnosis.VALVE_NOT_FULLY_CLOSED);
        if (pressure >= thresholds.LowPressureThresholdBar)
            return new(null, ValveCloseResolution.LEAK_SUSPECTED, ValveDiagnosis.VALVE_LEAK_SUSPECTED);
        if (lowerDetected) return new(null, ValveCloseResolution.LOWER_LIMIT_CONFIRMED, ValveDiagnosis.NONE);
        return new(null, ValveCloseResolution.NOT_FULLY_CLOSED, ValveDiagnosis.LOWER_LIMIT_SENSOR_FAULT);
    }
}
