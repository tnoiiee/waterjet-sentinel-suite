using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Deterministic test harness for the Phase 3B Simulation-only execution scenarios: one real
/// Manual Motion session over the deterministic simulated port, the typed Manual Motion boundary,
/// and the execution coordinator, driven by one clock at 25 ms per step. Every value is synthetic
/// test data (formula path points from the Phase 3A synthetic store, synthetic profile speeds and
/// the Owner-approved native bounds 0..2800000); no Owner-local path, no real coordinate and no
/// Hardware value appears here. A simulated result is never hardware evidence.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal sealed class CleaningPathExecutionHarness
{
    internal const string Sensor = "G+201";

    private static readonly DateTimeOffset Instant = new(2026, 10, 23, 6, 0, 0, TimeSpan.Zero);

    internal CleaningPathExecutionHarness(
        ManualMotionSimulationOptions? options = null,
        int horizontalSpeed = 400000,
        int verticalSpeed = 200000,
        int horizontalMoveTimeoutMilliseconds = 20000,
        int verticalMoveTimeoutMilliseconds = 20000,
        string assignedWaterJetId = "WJ4",
        string assignedIsolationValveId = "IV4",
        string? overrideWaterJetId = null)
    {
        Clock = new TestClock(Instant);
        Profile = CreatePathProfile(horizontalSpeed, verticalSpeed, horizontalMoveTimeoutMilliseconds, verticalMoveTimeoutMilliseconds);
        Port = new ManualMotionSimulatedPort(Clock, Profile, options);
        Session = new ManualMotionSession(Port, ManualMotionProfileResult.FromProfile(Profile), Clock);

        StoreSnapshot = SyntheticCleaningPathStores.ValidSnapshot();
        SensorConfiguration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, assignedWaterJetId, assignedIsolationValveId);
        Resolution = CleaningPathResolver.Resolve(
            StoreSnapshot,
            SensorConfiguration,
            new CleaningPathResolutionRequest { SensorId = Sensor, RequestedOverrideWaterJetId = overrideWaterJetId });

        Assert.True(Resolution.Accepted);
        Boundary = new ManualMotionCleaningPathBoundary(
            Session,
            Resolution.Resolution!.HorizontalAxisId,
            Resolution.Resolution!.VerticalAxisId);
        Coordinator = new CleaningPathExecutionCoordinator(Boundary, Clock);
    }

    internal TestClock Clock { get; }
    internal ManualMotionProfile Profile { get; }
    internal ManualMotionSimulatedPort Port { get; }
    internal ManualMotionSession Session { get; }
    internal ICleaningPathMotionBoundary Boundary { get; }
    internal CleaningPathExecutionCoordinator Coordinator { get; }
    internal CleaningPathSnapshot StoreSnapshot { get; }
    internal SensorConfigurationRecord SensorConfiguration { get; }
    internal CleaningPathResolutionResult Resolution { get; }

    internal static CleaningPathExecutionRole HorizontalRole => CleaningPathExecutionRole.Horizontal;
    internal static CleaningPathExecutionRole VerticalRole => CleaningPathExecutionRole.Vertical;

    internal void Drive(int steps, int millisecondsPerStep = 25)
    {
        for (var index = 0; index < steps; index++)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(millisecondsPerStep));
            Boundary.Step();
            Coordinator.Step();
        }
    }

    internal void OpenAndObserve()
    {
        Clock.Advance(TimeSpan.FromMilliseconds(25));
        Boundary.Step();
        Drive(16);
        Assert.True(Boundary.SessionReady);
    }

    internal void HomeRoles()
    {
        var horizontal = AxisFor(Resolution.Resolution!.HorizontalAxisId);
        var vertical = AxisFor(Resolution.Resolution!.VerticalAxisId);
        Session.RequestHome(horizontal);
        Session.RequestHome(vertical);
        DriveUntil(
            () => Boundary.AxisMoveReady(CleaningPathExecutionRole.Horizontal)
                && Boundary.AxisMoveReady(CleaningPathExecutionRole.Vertical),
            4000);
    }

    private static ManualMotionAxis AxisFor(string axisId) => axisId switch
    {
        "A" => ManualMotionAxis.A,
        "B" => ManualMotionAxis.B,
        "C" => ManualMotionAxis.C,
        _ => ManualMotionAxis.D,
    };

    internal CleaningPathExecutionStartOutcome Start() => Coordinator.Start(StoreSnapshot, Resolution);

    internal void DriveUntil(Func<bool> done, int maximumSteps = 6000, int millisecondsPerStep = 25)
    {
        for (var index = 0; index < maximumSteps && !done(); index++)
        {
            Drive(1, millisecondsPerStep);
        }

        Assert.True(done(), "the expected state was not reached within the step budget");
    }

    internal void DriveUntilTerminal(int maximumSteps = 8000)
    {
        DriveUntil(() => Coordinator.Result is not null, maximumSteps);
    }

    /// <summary>Every dispatched command addressed to an axis outside the two resolved roles.</summary>
    internal IReadOnlyList<ManualMotionPortCommand> UnrelatedAxisCommands()
    {
        var horizontalLetter = Resolution.Resolution!.HorizontalAxisId;
        var verticalLetter = Resolution.Resolution!.VerticalAxisId;
        var unrelated = new List<ManualMotionPortCommand>();
        foreach (var command in Port.ExecutedCommands)
        {
            var letter = command.Axis switch
            {
                ManualMotionAxis.A => "A",
                ManualMotionAxis.B => "B",
                ManualMotionAxis.C => "C",
                ManualMotionAxis.D => "D",
                _ => "?",
            };
            if (!string.Equals(letter, horizontalLetter, StringComparison.Ordinal)
                && !string.Equals(letter, verticalLetter, StringComparison.Ordinal))
            {
                unrelated.Add(command);
            }
        }

        return unrelated;
    }

    /// <summary>The dispatched commands of one kind, in dispatch order.</summary>
    internal IReadOnlyList<ManualMotionPortCommand> CommandsOf(ManualMotionPortCommandKind kind)
    {
        var matching = new List<ManualMotionPortCommand>();
        foreach (var command in Port.ExecutedCommands)
        {
            if (command.Kind == kind)
            {
                matching.Add(command);
            }
        }

        return matching;
    }

    /// <summary>The first dispatch index of the Move Begin command of one role, or -1.</summary>
    internal int BeginIndexOf(CleaningPathExecutionRole role)
    {
        var axis = role == CleaningPathExecutionRole.Horizontal
            ? Resolution.Resolution!.HorizontalAxisId
            : Resolution.Resolution!.VerticalAxisId;
        for (var index = 0; index < Port.ExecutedCommands.Count; index++)
        {
            var command = Port.ExecutedCommands[index];
            if (command.Kind == ManualMotionPortCommandKind.Begin && LetterOf(command.Axis) == axis)
            {
                return index;
            }
        }

        return -1;
    }

    internal static string LetterOf(ManualMotionAxis axis) => axis switch
    {
        ManualMotionAxis.A => "A",
        ManualMotionAxis.B => "B",
        ManualMotionAxis.C => "C",
        _ => "D",
    };

    internal static ManualMotionProfile CreatePathProfile(
        int horizontalSpeed,
        int verticalSpeed,
        int horizontalMoveTimeoutMilliseconds = 20000,
        int verticalMoveTimeoutMilliseconds = 20000)
    {
        var simulation = ManualMotionProfile.CreateSimulationProfile();
        return new ManualMotionProfile
        {
            Source = simulation.Source,
            ControllerId = simulation.ControllerId,
            ObservationIntervalMilliseconds = simulation.ObservationIntervalMilliseconds,
            ObservationFreshnessMilliseconds = simulation.ObservationFreshnessMilliseconds,
            JogHeartbeatTimeoutMilliseconds = simulation.JogHeartbeatTimeoutMilliseconds,
            JogMaximumHoldMilliseconds = simulation.JogMaximumHoldMilliseconds,
            AxisA = PathAxis(1, horizontalSpeed, horizontalMoveTimeoutMilliseconds, simulation.AxisA ?? throw new InvalidOperationException("The built-in simulation profile must provide Axis A.")),
            AxisB = PathAxis(2, verticalSpeed, verticalMoveTimeoutMilliseconds, simulation.AxisB ?? throw new InvalidOperationException("The built-in simulation profile must provide Axis B.")),
            AxisC = PathAxis(3, horizontalSpeed, horizontalMoveTimeoutMilliseconds, simulation.AxisC ?? throw new InvalidOperationException("The built-in simulation profile must provide Axis C.")),
            AxisD = PathAxis(4, verticalSpeed, verticalMoveTimeoutMilliseconds, simulation.AxisD ?? throw new InvalidOperationException("The built-in simulation profile must provide Axis D.")),
        };
    }

    /// <summary>
    /// One synthetic axis profile for path execution: the Owner-approved native target bounds
    /// 0..2800000 and a synthetic move speed; every other value mirrors the built-in simulation
    /// profile. Test-local synthetic values only.
    /// </summary>
    private static ManualMotionAxisProfile PathAxis(
        int brakeOutput,
        int moveSpeed,
        int moveTimeoutMilliseconds,
        ManualMotionAxisProfile simulation) => new()
    {
        BrakeOutput = brakeOutput,
        BrakeReleaseAction = simulation.BrakeReleaseAction,
        BrakeSettleMilliseconds = simulation.BrakeSettleMilliseconds,
        JogSpeed = simulation.JogSpeed,
        HomeSpeed = 4000,
        HomeTimeoutMilliseconds = simulation.HomeTimeoutMilliseconds,
        MoveAcceleration = simulation.MoveAcceleration,
        MoveSpeed = moveSpeed,
        MoveDeceleration = simulation.MoveDeceleration,
        MoveMinimumTarget = 0,
        MoveMaximumTarget = CleaningPathUnits.MaximumNative,
        PositionTolerance = simulation.PositionTolerance,
        MotionTimeoutMilliseconds = moveTimeoutMilliseconds,
        StopTimeoutMilliseconds = simulation.StopTimeoutMilliseconds,
        MotionStatusBit = simulation.MotionStatusBit,
        MotionActiveValue = simulation.MotionActiveValue,
        HomeStatusBit = simulation.HomeStatusBit,
        HomeActiveValue = simulation.HomeActiveValue,
        DualZeroPermitted = simulation.DualZeroPermitted,
    };

    /// <summary>Per-axis simulation overrides for one axis letter (A..D), simulation-only.</summary>
    internal static ManualMotionSimulationOptions OptionsWith(string axisLetter, ManualMotionAxisSimulationOptions axisOptions) =>
        axisLetter switch
        {
            "A" => new ManualMotionSimulationOptions { AxisA = axisOptions },
            "B" => new ManualMotionSimulationOptions { AxisB = axisOptions },
            "C" => new ManualMotionSimulationOptions { AxisC = axisOptions },
            _ => new ManualMotionSimulationOptions { AxisD = axisOptions },
        };
}
