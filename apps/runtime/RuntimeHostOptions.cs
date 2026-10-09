using System.Globalization;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core;
using Wjss.Runtime.Core.Simulator;
using SimulatorSeed = Wjss.Adapters.Simulator.SyntheticSeed;

namespace Wjss.Runtime;

/// <summary>
/// Explicit configuration of the SIMULATOR runtime host. Every value is a
/// DEVELOPMENT presentation value: a synthetic seed, a review-friendly tick
/// interval and bounded history capacities. Nothing here is a Production limit,
/// a device address or a calibration constant.
///
/// Ordering is part of the safety posture: configuration and device-profile
/// validation complete BEFORE any host object exists, so a refused profile or a
/// malformed value can never bind a port.
/// </summary>
public sealed record RuntimeHostOptions
{
    /// <summary>Device-profile variable (SIMULATOR default; TEST_HARDWARE and PRODUCTION are refused).</summary>
    public const string ProfileVariable = "WJSS_DEVICE_PROFILE";

    /// <summary>Startup-only exact scenario identity.</summary>
    public const string ScenarioVariable = "WJSS_SIMULATOR_SCENARIO";
    public const string InvalidSimulatorScenario = "INVALID_SIMULATOR_SCENARIO";

    /// <summary>Loopback API port variable.</summary>
    public const string PortVariable = "WJSS_API_PORT";

    /// <summary>Synthetic determinism seed variable (decimal, unsigned 64-bit).</summary>
    public const string SeedVariable = "WJSS_SYNTHETIC_SEED";

    /// <summary>Synthetic tick interval variable in milliseconds.</summary>
    public const string TickIntervalVariable = "WJSS_TICK_INTERVAL_MS";

    /// <summary>Runtime state-history capacity variable.</summary>
    public const string StateHistoryVariable = "WJSS_STATE_HISTORY_CAPACITY";

    /// <summary>Delta-history capacity variable.</summary>
    public const string DeltaHistoryVariable = "WJSS_DELTA_HISTORY_CAPACITY";

    /// <summary>Tick interval used when the environment does not set one (visible review pace).</summary>
    public const int DefaultTickIntervalMilliseconds = 1000;

    /// <summary>Slowest accepted tick interval: below this the review pace is noise, above it the UI looks frozen.</summary>
    public const int MaximumTickIntervalMilliseconds = 60000;

    /// <summary>Fastest accepted tick interval: bounded so a development run cannot become a high-frequency churn loop.</summary>
    public const int MinimumTickIntervalMilliseconds = 100;

    /// <summary>Port outside 1-65535, or not an integer.</summary>
    public const string InvalidPort = "INVALID_API_PORT";

    /// <summary>Seed label that is not a decimal unsigned 64-bit value.</summary>
    public const string InvalidSyntheticSeed = "INVALID_SYNTHETIC_SEED";

    /// <summary>Tick interval outside the accepted review bounds.</summary>
    public const string InvalidTickInterval = "INVALID_TICK_INTERVAL";

    /// <summary>State-history capacity outside the accepted bounds.</summary>
    public const string InvalidStateHistoryCapacity = "INVALID_STATE_HISTORY_CAPACITY";

    /// <summary>Delta-history capacity outside the accepted bounds.</summary>
    public const string InvalidDeltaHistoryCapacity = "INVALID_DELTA_HISTORY_CAPACITY";

    public SimulatorScenarioId Scenario { get; init; } = SimulatorScenarioId.IDLE;

    public required DeviceProfile Profile { get; init; }

    public required int Port { get; init; }

    /// <summary>Synthetic value-stream identity; two runs with the same seed produce the same values.</summary>
    public required ulong SyntheticSeed { get; init; }

    public required int TickIntervalMilliseconds { get; init; }

    public required int StateHistoryCapacity { get; init; }

    public required int DeltaHistoryCapacity { get; init; }

    /// <summary>
    /// Reads the synthetic configuration values (seed, tick interval, history
    /// capacities) from a variable reader, for an already-gated profile and port.
    /// The device profile and the loopback port are validated by the host before
    /// this call, so profile refusals always happen before any host object exists.
    /// Refusals are values, never exceptions: the caller prints the code and exits
    /// before a listener is created.
    /// </summary>
    public static bool TryLoadSynthetic(
        DeviceProfile profile,
        int port,
        Func<string, string?> readEnvironment,
        out RuntimeHostOptions options,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);

        options = null!;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        // Profile gate first, even for callers bypassing Program. Read the scenario exactly once.
        if (!ProfileStartPolicy.TryRequireStartable(profile, out refusalCode, out refusalDetail))
            return false;
        var scenarioLabel = readEnvironment(ScenarioVariable);
        var scenario = SimulatorScenarioId.IDLE;
        if (scenarioLabel is not null && !SimulatorScenarioCatalogue.TryParse(scenarioLabel, out scenario))
        {
            refusalCode = InvalidSimulatorScenario;
            refusalDetail = $"{ScenarioVariable} must be an exact scenario name; got '{scenarioLabel}'.";
            return false;
        }

        var syntheticSeed = SimulatorSeed.DefaultSeed.Value;
        var seedLabel = readEnvironment(SeedVariable);
        if (!string.IsNullOrWhiteSpace(seedLabel)
            && !ulong.TryParse(seedLabel.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out syntheticSeed))
        {
            refusalCode = InvalidSyntheticSeed;
            refusalDetail = $"{SeedVariable} must be a decimal unsigned 64-bit value; got '{seedLabel}'.";
            return false;
        }

        var tickInterval = DefaultTickIntervalMilliseconds;
        var intervalLabel = readEnvironment(TickIntervalVariable);
        if (!string.IsNullOrWhiteSpace(intervalLabel)
            && !TryReadBounded(
                intervalLabel,
                TickIntervalVariable,
                MinimumTickIntervalMilliseconds,
                MaximumTickIntervalMilliseconds,
                out tickInterval,
                out var intervalDetail))
        {
            refusalCode = InvalidTickInterval;
            refusalDetail = intervalDetail;
            return false;
        }

        var stateHistoryCapacity = RuntimeLimits.DefaultRevisionHistoryCapacity;
        var stateLabel = readEnvironment(StateHistoryVariable);
        if (!string.IsNullOrWhiteSpace(stateLabel)
            && !TryReadBounded(
                stateLabel,
                StateHistoryVariable,
                RuntimeLimits.MinimumRevisionHistoryCapacity,
                RuntimeLimits.MaximumRevisionHistoryCapacity,
                out stateHistoryCapacity,
                out var stateDetail))
        {
            refusalCode = InvalidStateHistoryCapacity;
            refusalDetail = stateDetail;
            return false;
        }

        var deltaHistoryCapacity = RuntimeLimits.DefaultDeltaHistoryCapacity;
        var deltaLabel = readEnvironment(DeltaHistoryVariable);
        if (!string.IsNullOrWhiteSpace(deltaLabel)
            && !TryReadBounded(
                deltaLabel,
                DeltaHistoryVariable,
                RuntimeLimits.MinimumDeltaHistoryCapacity,
                RuntimeLimits.MaximumDeltaHistoryCapacity,
                out deltaHistoryCapacity,
                out var deltaDetail))
        {
            refusalCode = InvalidDeltaHistoryCapacity;
            refusalDetail = deltaDetail;
            return false;
        }

        options = new RuntimeHostOptions
        {
            Profile = profile,
            Scenario = scenario,
            Port = port,
            SyntheticSeed = syntheticSeed,
            TickIntervalMilliseconds = tickInterval,
            StateHistoryCapacity = stateHistoryCapacity,
            DeltaHistoryCapacity = deltaHistoryCapacity,
        };

        return options.TryValidate(out refusalCode, out refusalDetail);
    }

    /// <summary>
    /// Validates an assembled configuration (including the profile policy), so a
    /// hand-built options object cannot compose a runtime that the policy refuses.
    /// </summary>
    public bool TryValidate(out string refusalCode, out string refusalDetail)
    {
        if (!ProfileStartPolicy.TryRequireStartable(Profile, out refusalCode, out refusalDetail))
        {
            return false;
        }

        if (!SimulatorScenarioCatalogue.TryParse(Scenario.ToString(), out var parsedScenario) || parsedScenario != Scenario)
        {
            refusalCode = InvalidSimulatorScenario;
            refusalDetail = $"Unknown scenario {Scenario}.";
            return false;
        }

        if (Port is < 1 or > 65535)
        {
            refusalCode = InvalidPort;
            refusalDetail = $"{PortVariable} must be an integer between 1 and 65535; got {Port}.";
            return false;
        }

        if (TickIntervalMilliseconds < MinimumTickIntervalMilliseconds
            || TickIntervalMilliseconds > MaximumTickIntervalMilliseconds)
        {
            refusalCode = InvalidTickInterval;
            refusalDetail =
                $"{TickIntervalVariable} must be between {MinimumTickIntervalMilliseconds} and "
                + $"{MaximumTickIntervalMilliseconds}; got {TickIntervalMilliseconds}.";
            return false;
        }

        if (StateHistoryCapacity < RuntimeLimits.MinimumRevisionHistoryCapacity
            || StateHistoryCapacity > RuntimeLimits.MaximumRevisionHistoryCapacity)
        {
            refusalCode = InvalidStateHistoryCapacity;
            refusalDetail =
                $"{StateHistoryVariable} must be between {RuntimeLimits.MinimumRevisionHistoryCapacity} and "
                + $"{RuntimeLimits.MaximumRevisionHistoryCapacity}; got {StateHistoryCapacity}.";
            return false;
        }

        if (DeltaHistoryCapacity < RuntimeLimits.MinimumDeltaHistoryCapacity
            || DeltaHistoryCapacity > RuntimeLimits.MaximumDeltaHistoryCapacity)
        {
            refusalCode = InvalidDeltaHistoryCapacity;
            refusalDetail =
                $"{DeltaHistoryVariable} must be between {RuntimeLimits.MinimumDeltaHistoryCapacity} and "
                + $"{RuntimeLimits.MaximumDeltaHistoryCapacity}; got {DeltaHistoryCapacity}.";
            return false;
        }

        return true;
    }

    /// <summary>Period of one synthetic tick.</summary>
    public TimeSpan TickInterval => TimeSpan.FromMilliseconds(TickIntervalMilliseconds);

    private static bool TryReadBounded(
        string label,
        string variable,
        int minimum,
        int maximum,
        out int value,
        out string detail)
    {
        value = 0;
        detail = string.Empty;

        if (!int.TryParse(label.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            detail = $"{variable} must be an integer; got '{label}'.";
            return false;
        }

        if (value < minimum || value > maximum)
        {
            detail = $"{variable} must be between {minimum} and {maximum}; got {value}.";
            return false;
        }

        return true;
    }
}
