using System.Globalization;
using Wjss.Contracts;
using Wjss.Time;

namespace Wjss.Runtime.Core.Simulator;

/// <summary>
/// The explicit SIMULATION configuration of the H3-A TimeDelay surface. Nothing here is a
/// commissioning value, a DCS reading or a Production setting: it exists so the deterministic
/// simulator and the Owner-local manual review can exercise both modes without a plant.
///
/// <para>
/// The RH input is a synthetic demo reading. It is stamped at the observing tick instant, so it is
/// neither stale nor future dated, and it is labelled as simulated in the workspace document. A
/// test may instead supply <see cref="FixedRhInput"/> to prove every refusal path (missing, stale,
/// future dated, bad quality, invalid generation) without inventing a second input channel.
/// </para>
/// </summary>
public sealed record SimulationTimeDelaySettings
{
    /// <summary>The label written into the workspace document for the demo surface.</summary>
    public const string DemoSourceLabel = "SIMULATION_TIME_DELAY_DEMO";

    /// <summary>The label used by tests and by any caller that supplies its own readings.</summary>
    public const string TestSourceLabel = "SIMULATION_TEST_INPUT";

    /// <summary>The number of ticks of one full ramp cycle of the demo RH reading.</summary>
    public const int DemoRampTicks = 120;

    /// <summary>The configured TimeDelay policy this simulation applies.</summary>
    public required TimeDelayConfiguration Configuration { get; init; }

    /// <summary>Where the RH reading comes from; a label, never a value.</summary>
    public required string SourceLabel { get; init; }

    /// <summary>
    /// When set, this reading is used verbatim at every tick instead of the ramp. It is how a test
    /// supplies a stale, future dated, bad-quality or invalid-generation reading.
    /// </summary>
    public RhTimeDelayInput? FixedRhInput { get; init; }

    /// <summary>
    /// True for the demo surface: the synthetic RH reading ramps across the configured range so the
    /// live preview visibly changes while the frozen decision does not.
    /// </summary>
    public bool RampRhTemperature { get; init; }

    /// <summary>
    /// The synthetic RH reading at one tick. Deterministic: the value depends only on the tick, and
    /// the timestamp is the observing instant, never wall time.
    /// </summary>
    public RhTimeDelayInput RhSampleAt(int tick, DateTimeOffset instantUtc)
    {
        if (FixedRhInput is not null) return FixedRhInput;
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        var min = Configuration.RhTempMin;
        var max = Configuration.RhTempMax;
        double temperature;
        if (RampRhTemperature)
        {
            var position = tick % DemoRampTicks;
            var ratio = position <= DemoRampTicks / 2
                ? position / (double)(DemoRampTicks / 2)
                : (DemoRampTicks - position) / (double)(DemoRampTicks / 2);
            temperature = min + ((max - min) * ratio);
        }
        else
        {
            temperature = min + ((max - min) * 0.5);
        }

        return new RhTimeDelayInput
        {
            RhTemperature = Math.Round(temperature, 3, MidpointRounding.ToEven),
            Quality = Quality.GOOD,
            ObservedAt = UtcTimestamps.Format(instantUtc),
            Generation = tick + 1L,
        };
    }

    /// <summary>
    /// Builds the demo settings from one host token. The token is explicit and total: an
    /// unrecognised token is a refusal, never a silent default and never a zero delay.
    /// </summary>
    public static bool TryParse(
        string? token,
        out SimulationTimeDelaySettings? settings,
        out string refusalDetail)
    {
        settings = null;
        refusalDetail = string.Empty;

        if (token is null)
        {
            return true;
        }

        var trimmed = token.Trim();
        if (trimmed.Length == 0 || string.Equals(trimmed, "NONE", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.StartsWith("MANUAL_", StringComparison.Ordinal))
        {
            var raw = trimmed["MANUAL_".Length..];
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var manualDelay)
                || manualDelay < 0
                || manualDelay > 3600)
            {
                refusalDetail = "MANUAL_<seconds> must be a whole number of seconds from 0 through 3600.";
                return false;
            }

            settings = new SimulationTimeDelaySettings
            {
                Configuration = TimeDelayConfiguration.LegacySeededSimulationDefaults(
                    TimeDelayMode.MANUAL_FIXED,
                    manualDelay),
                SourceLabel = DemoSourceLabel,
                RampRhTemperature = false,
            };
            return true;
        }

        if (trimmed.StartsWith("AUTO_", StringComparison.Ordinal))
        {
            var raw = trimmed["AUTO_".Length..];
            if (!Enum.TryParse<BoilerCondition>(raw, ignoreCase: false, out var condition)
                || !Enum.IsDefined(condition))
            {
                refusalDetail = "AUTO_<condition> must name one Boiler Condition: VERY_CLEAN, NORMAL_CLEAN, MEDIUM_DIRTY or VERY_DIRTY.";
                return false;
            }

            settings = new SimulationTimeDelaySettings
            {
                Configuration = TimeDelayConfiguration.LegacySeededSimulationDefaults(
                    TimeDelayMode.AUTO_RH_LINEAR_FROZEN,
                    activeBoilerCondition: condition),
                SourceLabel = DemoSourceLabel,
                RampRhTemperature = true,
            };
            return true;
        }

        refusalDetail = "The value must be NONE, MANUAL_<seconds> or AUTO_<condition>.";
        return false;
    }
}
