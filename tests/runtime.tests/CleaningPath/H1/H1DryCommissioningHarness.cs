using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.CleaningPath.H1;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Deterministic test harness for the H1 dry Cleaning Path commissioning scenarios: one real
/// Manual Motion session over the deterministic simulated port, the typed Manual Motion Cleaning
/// Path boundary and the H1 dry commissioning coordinator, driven by one clock at 25 ms per step.
/// The selected Sensor is assigned to WJ2 (WSB1, axes A/B) so the WSB1-only admission is exercised
/// on every run unless a test intentionally selects another Water Jet. Every value is synthetic
/// test data; no Owner-local path, no real coordinate and no Hardware value appears here. A
/// simulated result is never hardware evidence.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal sealed class H1DryCommissioningHarness
{
    internal const string Sensor = "G+201";
    internal const string Wsb1WaterJetId = "WJ2";
    internal const string Wsb1ValveId = "IV2";
    internal const string OtherCabinetWaterJetId = "WJ4";
    internal const string OtherCabinetValveId = "IV4";

    private static readonly DateTimeOffset Instant = new(2026, 10, 25, 6, 0, 0, TimeSpan.Zero);

    internal H1DryCommissioningHarness(
        ManualMotionSimulationOptions? options = null,
        string assignedWaterJetId = Wsb1WaterJetId,
        string assignedIsolationValveId = Wsb1ValveId,
        string? overrideWaterJetId = null,
        int horizontalSpeed = 400000,
        int verticalSpeed = 200000)
    {
        Clock = new TestClock(Instant);
        Profile = CleaningPathExecutionHarness.CreatePathProfile(horizontalSpeed, verticalSpeed);
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
        Coordinator = new H1DryCommissioningCoordinator(Session, Boundary, Clock);
    }

    internal TestClock Clock { get; }
    internal ManualMotionProfile Profile { get; }
    internal ManualMotionSimulatedPort Port { get; }
    internal ManualMotionSession Session { get; }
    internal ManualMotionCleaningPathBoundary Boundary { get; }
    internal H1DryCommissioningCoordinator Coordinator { get; }
    internal CleaningPathSnapshot StoreSnapshot { get; }
    internal SensorConfigurationRecord SensorConfiguration { get; }
    internal CleaningPathResolutionResult Resolution { get; }

    /// <summary>The explicit host-attested guard set with every required fact declared true.</summary>
    internal static H1DryCommissioningGuards AllGuardsTrue => new()
    {
        ControllerIdentityVerified = true,
        ControllerEndpointConfigured = true,
        OwnerLocalProfileConfigured = true,
        DryModeDeclared = true,
        PumpCommandPathUnavailable = true,
        PhysicalValveCommandPathUnavailable = true,
        QueueAutoSequenceUnavailable = true,
        PhysicalAbortAccessible = true,
        OperatorObserverPresent = true,
    };

    internal H1DryCommissioningStartOutcome Start(H1DryCommissioningGuards? guards = null) =>
        Coordinator.Start(new H1DryCommissioningRequest
        {
            PathStore = StoreSnapshot,
            Resolution = Resolution,
            SelectedSensorId = Sensor,
            Guards = guards ?? AllGuardsTrue,
        });

    internal void Drive(int steps, int millisecondsPerStep = 25)
    {
        for (var index = 0; index < steps; index++)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(millisecondsPerStep));
            Boundary.Step();
            Coordinator.Step();
        }
    }

    internal void DriveUntil(Func<bool> done, int maximumSteps = 8000, int millisecondsPerStep = 25)
    {
        for (var index = 0; index < maximumSteps && !done(); index++)
        {
            Drive(1, millisecondsPerStep);
        }

        Assert.True(done(), "the expected state was not reached within the step budget");
    }

    internal void DriveUntilTerminal(int maximumSteps = 12000) =>
        DriveUntil(() => Coordinator.Result is not null, maximumSteps);

    /// <summary>Opens the port and lets the observation cycle publish current readings.</summary>
    internal void OpenAndObserve()
    {
        Clock.Advance(TimeSpan.FromMilliseconds(25));
        Boundary.Step();
        Drive(16);
        Assert.True(Boundary.SessionReady);
    }

    /// <summary>Opens the port with almost no observation cadence: readings are not yet current.</summary>
    internal void OpenWithoutObservationCadence()
    {
        Clock.Advance(TimeSpan.FromMilliseconds(25));
        Boundary.Step();
    }

    /// <summary>Every dispatched command addressed to an axis outside the two resolved roles.</summary>
    internal IReadOnlyList<ManualMotionPortCommand> UnrelatedAxisCommands()
    {
        var horizontalLetter = Resolution.Resolution!.HorizontalAxisId;
        var verticalLetter = Resolution.Resolution!.VerticalAxisId;
        var unrelated = new List<ManualMotionPortCommand>();
        foreach (var command in Port.ExecutedCommands)
        {
            var letter = CleaningPathExecutionHarness.LetterOf(command.Axis);
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

    /// <summary>The dispatch indices of the typed Move Begin commands of one axis letter.</summary>
    internal IReadOnlyList<int> MoveBeginIndicesOf(string axisLetter) =>
        BeginIndicesArmedBy(axisLetter, ManualMotionPortCommandKind.PositionAbsolute);

    /// <summary>The dispatch indices of the typed Move Begin commands of one role.</summary>
    internal IReadOnlyList<int> MoveBeginIndicesOf(CleaningPathExecutionRole role) =>
        MoveBeginIndicesOf(
            role == CleaningPathExecutionRole.Horizontal
                ? Resolution.Resolution!.HorizontalAxisId
                : Resolution.Resolution!.VerticalAxisId);

    private List<int> BeginIndicesArmedBy(string axisLetter, ManualMotionPortCommandKind armKind)
    {
        var indices = new List<int>();
        var armed = false;
        for (var index = 0; index < Port.ExecutedCommands.Count; index++)
        {
            var command = Port.ExecutedCommands[index];
            if (CleaningPathExecutionHarness.LetterOf(command.Axis) != axisLetter)
            {
                continue;
            }

            if (command.Kind == armKind)
            {
                armed = true;
            }
            else if (command.Kind == ManualMotionPortCommandKind.Home
                || command.Kind == ManualMotionPortCommandKind.PositionAbsolute)
            {
                armed = false;
            }
            else if (command.Kind == ManualMotionPortCommandKind.Begin)
            {
                if (armed)
                {
                    indices.Add(index);
                }

                armed = false;
            }
        }

        return indices;
    }
}
