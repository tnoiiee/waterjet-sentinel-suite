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

    /// <summary>
    /// The number of ticks of one full ramp cycle of the demo RH reading.
    /// </summary>
    public const int DemoRampTicks = 120;

    /// <summary>
    /// The highest accepted sample tick. A reading generation is the one-based ordinal of its sample,
    /// so the successor of this tick is the last representable generation: no reading identity exists
    /// beyond it. The bound is a typing fact, never a process or commissioning value.
    /// </summary>
    public const long MaximumSampleTick = long.MaxValue - 1;

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
    /// The synthetic RH reading at one tick, or a typed refusal. It is the fail-closed form of
    /// <see cref="RhSampleAt"/>: the accepted tick counter is passed through WITHOUT a narrowing
    /// conversion, so no reading identity can wrap, go negative or be fabricated, and a tick outside
    /// the representable range is a typed refusal rather than an overflow exception.
    /// </summary>
    /// <param name="tick">The accepted tick counter, in whole ticks since the session started.</param>
    /// <param name="instantUtc">The observing instant, which becomes the reading timestamp.</param>
    /// <param name="input">The reading, or null when the tick has no representable reading identity.</param>
    /// <param name="refusal">The typed refusal, or <see cref="CountdownRefusalCode.NONE"/>.</param>
    public bool TryRhSampleAt(
        long tick,
        DateTimeOffset instantUtc,
        out RhTimeDelayInput? input,
        out CountdownRefusalCode refusal)
    {
        input = null;
        refusal = CountdownRefusalCode.NONE;

        if (FixedRhInput is not null)
        {
            input = FixedRhInput;
            return true;
        }

        // The reading generation is tick + 1. Both bounds are checked BEFORE the arithmetic, so the
        // generation is always positive and always representable.
        if (tick < 0L || tick > MaximumSampleTick)
        {
            refusal = CountdownRefusalCode.RH_GENERATION_INVALID;
            return false;
        }

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

        input = new RhTimeDelayInput
        {
            RhTemperature = Math.Round(temperature, 3, MidpointRounding.ToEven),
            Quality = Quality.GOOD,
            ObservedAt = UtcTimestamps.Format(instantUtc),
            Generation = tick + 1L,
        };
        return true;
    }

    /// <summary>
    /// The synthetic RH reading at one tick. Deterministic: the value depends only on the tick, and
    /// the timestamp is the observing instant, never wall time.
    ///
    /// <para>
    /// This is the total-or-throw convenience form, kept for previews and tests: an out-of-range tick
    /// is the caller's defect and throws. Every execution path uses <see cref="TryRhSampleAt"/>, so a
    /// refusal is always typed and never an exception.
    /// </para>
    /// </summary>
    public RhTimeDelayInput RhSampleAt(long tick, DateTimeOffset instantUtc)
    {
        if (TryRhSampleAt(tick, instantUtc, out var input, out var refusal)) return input!;

        throw new ArgumentOutOfRangeException(
            nameof(tick),
            tick,
            $"The sample tick must be 0 through {MaximumSampleTick} to carry a reading generation [{refusal}].");
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
