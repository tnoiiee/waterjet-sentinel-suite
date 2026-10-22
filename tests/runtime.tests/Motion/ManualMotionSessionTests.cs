using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Behavioural coverage of the Application-owned Manual Motion Commissioning session (WSB1 Axis A
/// Horizontal / Axis B Vertical), driven only through its public request/snapshot surface with the
/// deterministic simulated port or a recording fake port. No native library, controller, network or
/// output is touched, and no simulated result is hardware evidence. Every numeric value is a
/// synthetic test value from the built-in simulation profile or a test-local profile object.
/// </summary>
public sealed class ManualMotionSessionTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    private sealed class FakePort : IManualMotionPort
    {
        internal long Generation { get; init; } = 1;

        internal bool OpenSucceeds { get; init; } = true;

        internal Func<ManualMotionPortCommand, ManualMotionPortCommandResult>? ExecuteHandler { get; init; }

        internal Func<ManualMotionPortRead, ManualMotionPortReadResult>? ReadHandler { get; init; }

        internal int OpenCount { get; private set; }

        internal int CloseCount { get; private set; }

        internal List<ManualMotionPortCommand> Executed { get; } = new();

        public ManualMotionPortMode Mode { get; init; } = ManualMotionPortMode.Simulated;

        public ManualMotionPortOpenResult Open()
        {
            OpenCount++;
            return new ManualMotionPortOpenResult(OpenSucceeds, OpenSucceeds ? "FAKE_OPENED" : "FAKE_OPEN_REFUSED", Generation);
        }

        public ManualMotionPortCommandResult Execute(ManualMotionPortCommand command)
        {
            Executed.Add(command);
            return ExecuteHandler?.Invoke(command)
                ?? new ManualMotionPortCommandResult(true, false, true, "FAKE_DISPATCHED", Generation);
        }

        public ManualMotionPortReadResult Read(ManualMotionPortRead read) =>
            ReadHandler?.Invoke(read)
            ?? new ManualMotionPortReadResult(true, false, 0m, "FAKE_READ", Generation);

        public ManualMotionPortCloseResult Close()
        {
            CloseCount++;
            return new ManualMotionPortCloseResult(true, true, true, "FAKE_CLOSED");
        }
    }

    private sealed class Harness
    {
        internal Harness(ManualMotionSimulationOptions? options = null, ManualMotionProfile? profile = null)
        {
            Profile = profile ?? ManualMotionProfile.CreateSimulationProfile();
            Port = new ManualMotionSimulatedPort(Clock, Profile, options);
            Session = new ManualMotionSession(Port, ManualMotionProfileResult.FromProfile(Profile), Clock);
        }

        internal TestClock Clock { get; } = new(Instant);

        internal ManualMotionProfile Profile { get; }

        internal ManualMotionSimulatedPort Port { get; }

        internal ManualMotionSession Session { get; }

        internal ManualMotionSnapshot Snapshot => Session.Snapshot;

        internal void Drive(int steps, int millisecondsPerStep = 25)
        {
            for (var index = 0; index < steps; index++)
            {
                Clock.Advance(TimeSpan.FromMilliseconds(millisecondsPerStep));
                Session.Step();
            }
        }

        internal void DriveUntil(Func<ManualMotionSnapshot, bool> done, int maximumSteps = 1200, int millisecondsPerStep = 25)
        {
            for (var index = 0; index < maximumSteps && !done(Snapshot); index++)
            {
                Drive(1, millisecondsPerStep);
            }

            Assert.True(done(Snapshot), "the session did not reach the expected state within the step budget");
        }

        internal void OpenAndObserve()
        {
            Session.Step();
            Drive(16);
            Assert.Equal(ManualMotionSessionState.Ready, Snapshot.State);
        }

        internal ManualMotionAxisSnapshot Axis(ManualMotionAxis axis) =>
            Snapshot.Axes[(int)axis - 1];

        internal void HomeAxis(ManualMotionAxis axis)
        {
            Session.RequestHome(axis);
            DriveUntil(snapshot => ActiveOf(snapshot, axis) is null && snapshot.Axes[(int)axis - 1].HomeState == ManualMotionHomeState.Homed);
        }
    }

    /// <summary>The active operation of one axis from the multi-operation snapshot, or null.</summary>
    private static ManualMotionOperationSnapshot? ActiveOf(ManualMotionSnapshot snapshot, ManualMotionAxis axis)
    {
        foreach (var operation in snapshot.ActiveOperations)
        {
            if (operation.Axis == axis)
            {
                return operation;
            }
        }

        return null;
    }

    private static ManualMotionAxisProfile CloneAxis(
        ManualMotionAxisProfile source,
        int? jogSpeed = -1,
        int? motionTimeoutMilliseconds = null)
    {
        // A sentinel keeps the common case terse: jogSpeed -1 copies the source value, an explicit
        // null would be indistinguishable, so the NOT_CONFIGURED case passes jogSpeed: null through
        // the dedicated overload below.
        return new ManualMotionAxisProfile
        {
            BrakeOutput = source.BrakeOutput,
            BrakeReleaseAction = source.BrakeReleaseAction,
            BrakeSettleMilliseconds = source.BrakeSettleMilliseconds,
            JogSpeed = jogSpeed == -1 ? source.JogSpeed : jogSpeed,
            HomeSpeed = source.HomeSpeed,
            HomeTimeoutMilliseconds = source.HomeTimeoutMilliseconds,
            MoveAcceleration = source.MoveAcceleration,
            MoveSpeed = source.MoveSpeed,
            MoveDeceleration = source.MoveDeceleration,
            MoveMinimumTarget = source.MoveMinimumTarget,
            MoveMaximumTarget = source.MoveMaximumTarget,
            PositionTolerance = source.PositionTolerance,
            MotionTimeoutMilliseconds = motionTimeoutMilliseconds ?? source.MotionTimeoutMilliseconds,
            StopTimeoutMilliseconds = source.StopTimeoutMilliseconds,
            MotionStatusBit = source.MotionStatusBit,
            MotionActiveValue = source.MotionActiveValue,
            HomeStatusBit = source.HomeStatusBit,
            HomeActiveValue = source.HomeActiveValue,
            DualZeroPermitted = source.DualZeroPermitted,
        };
    }

    private static ManualMotionAxisProfile AxisWithoutJogSpeed(ManualMotionAxisProfile source) => new()
    {
        BrakeOutput = source.BrakeOutput,
        BrakeReleaseAction = source.BrakeReleaseAction,
        BrakeSettleMilliseconds = source.BrakeSettleMilliseconds,
        JogSpeed = null,
        HomeSpeed = source.HomeSpeed,
        HomeTimeoutMilliseconds = source.HomeTimeoutMilliseconds,
        MoveAcceleration = source.MoveAcceleration,
        MoveSpeed = source.MoveSpeed,
        MoveDeceleration = source.MoveDeceleration,
        MoveMinimumTarget = source.MoveMinimumTarget,
        MoveMaximumTarget = source.MoveMaximumTarget,
        PositionTolerance = source.PositionTolerance,
        MotionTimeoutMilliseconds = source.MotionTimeoutMilliseconds,
        StopTimeoutMilliseconds = source.StopTimeoutMilliseconds,
        MotionStatusBit = source.MotionStatusBit,
        MotionActiveValue = source.MotionActiveValue,
        HomeStatusBit = source.HomeStatusBit,
        HomeActiveValue = source.HomeActiveValue,
        DualZeroPermitted = source.DualZeroPermitted,
    };

    private static ManualMotionProfile ProfileWith(
        ManualMotionProfile source,
        ManualMotionAxisProfile? axisA = null,
        ManualMotionAxisProfile? axisB = null,
        bool omitAxisB = false,
        int? jogMaximumHoldMilliseconds = null,
        int? jogHeartbeatTimeoutMilliseconds = null) => new()
    {
        Source = source.Source,
        ControllerId = source.ControllerId,
        ObservationIntervalMilliseconds = source.ObservationIntervalMilliseconds,
        ObservationFreshnessMilliseconds = source.ObservationFreshnessMilliseconds,
        JogHeartbeatTimeoutMilliseconds = jogHeartbeatTimeoutMilliseconds ?? source.JogHeartbeatTimeoutMilliseconds,
        JogMaximumHoldMilliseconds = jogMaximumHoldMilliseconds ?? source.JogMaximumHoldMilliseconds,
        AxisA = axisA ?? source.AxisA,
        AxisB = omitAxisB ? null : axisB ?? source.AxisB,
        AxisC = source.AxisC,
        AxisD = source.AxisD,
    };

    private static List<ManualMotionPortCommandKind> KindsAfter(Harness harness, int skip) =>
        harness.Port.ExecutedCommands.Skip(skip).Select(command => command.Kind).ToList();

    /// <summary>
    /// The approved-trial timing shape as a synthetic test profile: Axis A only, 100 ms observation
    /// interval against a 300 ms freshness window, motion bit 7 active-high, and every Home, Move
    /// and dual-zero value deliberately NOT_CONFIGURED. Every number is a test value; nothing here
    /// is a hardware commissioning value.
    /// </summary>
    private static ManualMotionProfile TrialLikeProfile(bool includeBrakePolarity = true) => new()
    {
        Source = ManualMotionProfileSource.OwnerLocalFile,
        ControllerId = "WSB1",
        ObservationIntervalMilliseconds = 100,
        ObservationFreshnessMilliseconds = 300,
        JogHeartbeatTimeoutMilliseconds = 5000,
        JogMaximumHoldMilliseconds = 60000,
        AxisA = new ManualMotionAxisProfile
        {
            BrakeOutput = 1,
            BrakeReleaseAction = includeBrakePolarity ? ManualMotionBrakeReleaseAction.SetOutput : null,
            BrakeSettleMilliseconds = 100,
            JogSpeed = 2000,
            StopTimeoutMilliseconds = 1000,
            MotionStatusBit = 7,
            MotionActiveValue = 1,
        },
        AxisB = null,
    };

    /// <summary>
    /// The approved-trial timing shape on Axis A and Axis B together: 100 ms observation interval
    /// against a 300 ms freshness window, motion bit 7 active-high on both axes. Every number is a
    /// synthetic test value; nothing here is a hardware commissioning value.
    /// </summary>
    private static ManualMotionProfile DualTrialLikeProfile() => new()
    {
        Source = ManualMotionProfileSource.OwnerLocalFile,
        ControllerId = "WSB1",
        ObservationIntervalMilliseconds = 100,
        ObservationFreshnessMilliseconds = 300,
        JogHeartbeatTimeoutMilliseconds = 5000,
        JogMaximumHoldMilliseconds = 60000,
        AxisA = new ManualMotionAxisProfile
        {
            BrakeOutput = 1,
            BrakeReleaseAction = ManualMotionBrakeReleaseAction.SetOutput,
            BrakeSettleMilliseconds = 100,
            JogSpeed = 2000,
            StopTimeoutMilliseconds = 1000,
            MotionStatusBit = 7,
            MotionActiveValue = 1,
        },
        AxisB = new ManualMotionAxisProfile
        {
            BrakeOutput = 2,
            BrakeReleaseAction = ManualMotionBrakeReleaseAction.SetOutput,
            BrakeSettleMilliseconds = 100,
            JogSpeed = 2000,
            StopTimeoutMilliseconds = 1000,
            MotionStatusBit = 7,
            MotionActiveValue = 1,
        },
    };

    /// <summary>
    /// The approved-trial timing shape on all four axes at once: 100 ms observation interval against
    /// a 300 ms freshness window, motion bit 7 active-high everywhere. Synthetic test values only.
    /// </summary>
    private static ManualMotionProfile QuadTrialLikeProfile()
    {
        var axis = new ManualMotionAxisProfile
        {
            BrakeOutput = 1,
            BrakeReleaseAction = ManualMotionBrakeReleaseAction.SetOutput,
            BrakeSettleMilliseconds = 100,
            JogSpeed = 2000,
            StopTimeoutMilliseconds = 1000,
            MotionStatusBit = 7,
            MotionActiveValue = 1,
        };
        return new ManualMotionProfile
        {
            Source = ManualMotionProfileSource.OwnerLocalFile,
            ControllerId = "WSB1",
            ObservationIntervalMilliseconds = 100,
            ObservationFreshnessMilliseconds = 300,
            JogHeartbeatTimeoutMilliseconds = 5000,
            JogMaximumHoldMilliseconds = 60000,
            AxisA = axis,
            AxisB = CloneAxisWithOutput(axis, 2),
            AxisC = CloneAxisWithOutput(axis, 3),
            AxisD = CloneAxisWithOutput(axis, 4),
        };
    }

    private static ManualMotionAxisProfile CloneAxisWithOutput(ManualMotionAxisProfile source, int brakeOutput) => new()
    {
        BrakeOutput = brakeOutput,
        BrakeReleaseAction = source.BrakeReleaseAction,
        BrakeSettleMilliseconds = source.BrakeSettleMilliseconds,
        JogSpeed = source.JogSpeed,
        HomeSpeed = source.HomeSpeed,
        HomeTimeoutMilliseconds = source.HomeTimeoutMilliseconds,
        MoveAcceleration = source.MoveAcceleration,
        MoveSpeed = source.MoveSpeed,
        MoveDeceleration = source.MoveDeceleration,
        MoveMinimumTarget = source.MoveMinimumTarget,
        MoveMaximumTarget = source.MoveMaximumTarget,
        PositionTolerance = source.PositionTolerance,
        MotionTimeoutMilliseconds = source.MotionTimeoutMilliseconds,
        StopTimeoutMilliseconds = source.StopTimeoutMilliseconds,
        MotionStatusBit = source.MotionStatusBit,
        MotionActiveValue = source.MotionActiveValue,
        HomeStatusBit = source.HomeStatusBit,
        HomeActiveValue = source.HomeActiveValue,
        DualZeroPermitted = source.DualZeroPermitted,
    };

    /// <summary>
    /// A FakePort session that records every read (with its fake-clock time) and models just enough
    /// controller behaviour for the observation-cycle tests: Begin makes the status motion bit of
    /// ITS axis active, Stop makes ITS axis inactive, and each brake-output read follows the brake
    /// commands on that output. The model is per axis, so overlapping independent operations keep
    /// their own motion and output state without cross-axis mutation.
    /// </summary>
    private sealed class RecordingHarness
    {
        private readonly bool[] _moving = new bool[4];
        private readonly bool[] _brakeReleased = new bool[5];
        private readonly decimal[] _positions = new decimal[4];

        internal RecordingHarness(ManualMotionProfile profile)
        {
            Port = new FakePort
            {
                ReadHandler = read =>
                {
                    Reads.Add(read);
                    ReadTimes.Add(Clock.UtcNow);
                    if (FailReadKinds.Contains(read.Kind))
                    {
                        return new ManualMotionPortReadResult(false, false, 0m, "FAKE_REFUSED", 1);
                    }

                    var value = read.Kind switch
                    {
                        ManualMotionReadKind.StatusWord => _moving[IndexFor(read.Axis)] ? 128m : 0m,
                        ManualMotionReadKind.BrakeOutputState => _brakeReleased[read.BrakeOutput] ? 1m : 0m,
                        _ => _positions[IndexFor(read.Axis)],
                    };
                    return new ManualMotionPortReadResult(true, false, value, "FAKE_READ", 1);
                },
                ExecuteHandler = command =>
                {
                    if (command.Kind == ManualMotionPortCommandKind.Begin)
                    {
                        _moving[IndexFor(command.Axis)] = true;
                    }
                    else if (command.Kind == ManualMotionPortCommandKind.Stop)
                    {
                        if (!IgnoreStopFor.Contains(command.Axis))
                        {
                            _moving[IndexFor(command.Axis)] = false;
                        }

                        ReadsAtLastStop = Reads.Count;
                    }
                    else if (command.Kind == ManualMotionPortCommandKind.BrakeOutputSet)
                    {
                        _brakeReleased[command.BrakeOutput] = true;
                    }
                    else if (command.Kind == ManualMotionPortCommandKind.BrakeOutputClear)
                    {
                        _brakeReleased[command.BrakeOutput] = false;
                    }

                    return new ManualMotionPortCommandResult(true, false, true, "FAKE_DISPATCHED", 1);
                },
            };
            Session = new ManualMotionSession(Port, ManualMotionProfileResult.FromProfile(profile), Clock);
        }

        internal TestClock Clock { get; } = new(Instant);

        internal FakePort Port { get; }

        internal ManualMotionSession Session { get; }

        internal ManualMotionSnapshot Snapshot => Session.Snapshot;

        internal List<ManualMotionPortRead> Reads { get; } = new();

        internal List<DateTimeOffset> ReadTimes { get; } = new();

        internal HashSet<ManualMotionReadKind> FailReadKinds { get; } = new();

        /// <summary>Axes whose simulated Stop does not stop motion: a Stop-not-observed scenario.</summary>
        internal HashSet<ManualMotionAxis> IgnoreStopFor { get; } = new();

        internal int ReadsAtLastStop { get; private set; } = -1;

        internal decimal PositionValue
        {
            get => _positions[IndexFor(ManualMotionAxis.A)];
            set => _positions[IndexFor(ManualMotionAxis.A)] = value;
        }

        private static int IndexFor(ManualMotionAxis axis) => (int)axis - 1;

        internal void OpenSession()
        {
            Session.Step();
            Assert.Equal(ManualMotionSessionState.Ready, Snapshot.State);
        }

        internal void StepOnce(int milliseconds = 10)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
            Session.Step();
        }

        internal void Drive(int steps, int millisecondsPerStep = 10)
        {
            for (var index = 0; index < steps; index++)
            {
                StepOnce(millisecondsPerStep);
            }
        }

        internal void DriveUntil(Func<ManualMotionSnapshot, bool> done, int maximumSteps = 500, int millisecondsPerStep = 10)
        {
            for (var index = 0; index < maximumSteps && !done(Snapshot); index++)
            {
                StepOnce(millisecondsPerStep);
            }

            Assert.True(done(Snapshot), "the session did not reach the expected state within the step budget");
        }

        internal ManualMotionAxisSnapshot Axis(ManualMotionAxis axis) => Snapshot.Axes[(int)axis - 1];
    }

    [Fact]
    public void Session_Opens_Once_And_Publishes_Simulated_Identity_And_Raw_Observations()
    {
        var harness = new Harness();

        harness.OpenAndObserve();

        Assert.Equal(ManualMotionPortMode.Simulated, harness.Snapshot.Mode);
        Assert.Equal(1, harness.Port.OpenCount);
        Assert.Equal(ManualMotionProfileSource.SimulationBuiltIn, harness.Snapshot.ProfileSource);
        Assert.Equal("Horizontal", harness.Axis(ManualMotionAxis.A).RoleLabel);
        Assert.Equal("Vertical", harness.Axis(ManualMotionAxis.B).RoleLabel);
        Assert.Equal("Horizontal", harness.Axis(ManualMotionAxis.C).RoleLabel);
        Assert.Equal("Vertical", harness.Axis(ManualMotionAxis.D).RoleLabel);
        Assert.True(harness.Axis(ManualMotionAxis.A).AllowedByProfile);
        Assert.True(harness.Axis(ManualMotionAxis.A).PositionTp.Fresh);
        Assert.Equal(12000m, harness.Axis(ManualMotionAxis.A).PositionTp.Value);
        Assert.Equal(-8000m, harness.Axis(ManualMotionAxis.B).PositionTp.Value);
        Assert.Equal(5000m, harness.Axis(ManualMotionAxis.C).PositionTp.Value);
        Assert.Equal(-3000m, harness.Axis(ManualMotionAxis.D).PositionTp.Value);

        // The snapshot carries the four axes in the stable A, B, C, D order with the canonical
        // catalogue identity of each axis (A/B serve WJ2, C/D serve WJ3, all on WSB1).
        Assert.Equal(
            new[] { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D },
            harness.Snapshot.Axes.Select(axis => axis.Axis).ToArray());
        Assert.Equal("WSB1", harness.Axis(ManualMotionAxis.C).CabinetId);
        Assert.Equal("WJ2", harness.Axis(ManualMotionAxis.A).WaterJetId);
        Assert.Equal("WJ2", harness.Axis(ManualMotionAxis.B).WaterJetId);
        Assert.Equal("WJ3", harness.Axis(ManualMotionAxis.C).WaterJetId);
        Assert.Equal("WJ3", harness.Axis(ManualMotionAxis.D).WaterJetId);
        Assert.Equal("REAR / UPPER", harness.Axis(ManualMotionAxis.C).InstalledPosition);
        Assert.Equal("FRONT / UPPER", harness.Axis(ManualMotionAxis.D).TargetCoverage);
        Assert.Equal(3, harness.Axis(ManualMotionAxis.C).BrakeOutputNumber);
        Assert.Equal(4, harness.Axis(ManualMotionAxis.D).BrakeOutputNumber);
        Assert.Equal(ManualMotionInterpretation.Inactive, harness.Axis(ManualMotionAxis.A).Motion);
        Assert.True(harness.Axis(ManualMotionAxis.A).MotionInterpretationConfigured);
        Assert.True(harness.Axis(ManualMotionAxis.A).HomeInterpretationConfigured);
        Assert.False(harness.Axis(ManualMotionAxis.A).StopConfirmed);
        Assert.False(harness.Axis(ManualMotionAxis.B).StopConfirmed);
        Assert.Empty(harness.Port.ExecutedCommands);
    }

    [Fact]
    public void A_Refused_Profile_Never_Opens_The_Port()
    {
        var clock = new TestClock(Instant);
        var profile = ManualMotionProfile.CreateSimulationProfile();
        var port = new ManualMotionSimulatedPort(clock, profile);
        var session = new ManualMotionSession(
            port,
            ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileJsonInvalid),
            clock);

        for (var index = 0; index < 4; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }

        Assert.Equal(ManualMotionSessionState.ProfileRefused, session.Snapshot.State);
        Assert.True(session.Snapshot.IsTerminal);
        Assert.Equal(0, port.OpenCount);
    }

    [Fact]
    public void The_Simulation_Profile_Is_Refused_For_A_Hardware_Capable_Port()
    {
        var clock = new TestClock(Instant);
        var port = new FakePort { Mode = ManualMotionPortMode.HardwareCapable };
        var session = new ManualMotionSession(
            port,
            ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile()),
            clock);

        session.Step();

        Assert.Equal(ManualMotionSessionState.ProfileRefused, session.Snapshot.State);
        Assert.Equal(ManualMotionCodes.ProfileSimulationSourceRefusedForHardware, session.Snapshot.ProfileCode);
        Assert.Equal(0, port.OpenCount);
    }

    [Fact]
    public void An_Open_Refusal_Closes_The_Port_And_Never_Reopens()
    {
        var clock = new TestClock(Instant);
        var port = new FakePort { OpenSucceeds = false };
        var session = new ManualMotionSession(
            port,
            ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile()),
            clock);

        for (var index = 0; index < 4; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }

        Assert.Equal(ManualMotionSessionState.OpenRefused, session.Snapshot.State);
        Assert.True(session.Snapshot.IsTerminal);
        Assert.Equal(1, port.OpenCount);
        Assert.Equal(1, port.CloseCount);
        Assert.Empty(port.Executed);
    }

    [Fact]
    public void Brake_Release_And_Apply_Are_Command_State_Only()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestBrakeRelease(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[0].BrakeCommandState == ManualMotionBrakeCommandState.ReleaseCommanded);

        Assert.Equal(ManualMotionCodes.BrakeReleaseCommanded, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        var release = Assert.Single(harness.Port.ExecutedCommands);
        Assert.Equal(ManualMotionPortCommandKind.BrakeOutputSet, release.Kind);
        Assert.Equal(1, release.BrakeOutput);

        harness.Session.RequestBrakeApply(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[0].BrakeCommandState == ManualMotionBrakeCommandState.ApplyCommanded);

        Assert.Equal(ManualMotionCodes.BrakeApplyCommanded, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(
            new[] { ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionPortCommandKind.BrakeOutputClear },
            harness.Port.ExecutedCommands.Select(command => command.Kind).ToArray());
    }

    [Fact]
    public void Same_Axis_Exclusion_Refuses_Every_Second_Operation_On_The_Busy_Axis_And_Stop_Remains_Accepted()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.A);

        harness.Session.RequestMove(ManualMotionAxis.A, -5000);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        // Same-axis exclusion is mandatory: while Move A runs, every further operation family on
        // Axis A is refused with OPERATION_IN_PROGRESS and dispatches nothing.
        harness.Session.RequestMove(ManualMotionAxis.A, -6000);
        harness.Session.RequestHome(ManualMotionAxis.A);
        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 41);
        harness.Session.RequestDefinePositionZero(ManualMotionAxis.A);
        harness.Session.RequestDefineDualZero(ManualMotionAxis.A);
        harness.Session.RequestBrakeRelease(ManualMotionAxis.A);
        harness.Session.RequestBrakeApply(ManualMotionAxis.A);
        harness.Drive(4);

        Assert.Equal(ManualMotionCodes.OperationInProgress, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(
            7,
            harness.Snapshot.Log.Count(entry => entry.Code == ManualMotionCodes.OperationInProgress
                && entry.Axis == ManualMotionAxis.A));
        var active = ActiveOf(harness.Snapshot, ManualMotionAxis.A);
        Assert.NotNull(active);
        Assert.Equal(ManualMotionOperationKind.Move, active?.Kind);

        // Stop A stays accepted while the axis runs, and it stops only this axis operation.
        harness.Session.RequestStop(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null);

        Assert.Equal(ManualMotionCodes.MoveAbortedByOperator, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
    }

    [Fact]
    public void Same_Axis_Exclusion_Is_Generic_And_Applies_On_The_C_D_Pair_Too()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.C);

        harness.Session.RequestMove(ManualMotionAxis.C, 3000);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.C)?.Phase == ManualMotionOperationPhase.Running);

        harness.Session.RequestHome(ManualMotionAxis.C);
        harness.Session.RequestJogStart(ManualMotionAxis.C, ManualMotionJogDirection.Negative, 43);
        harness.Session.RequestBrakeRelease(ManualMotionAxis.C);
        harness.Drive(3);

        Assert.Equal(ManualMotionCodes.OperationInProgress, harness.Axis(ManualMotionAxis.C).LastOutcomeCode);
        Assert.Equal(
            3,
            harness.Snapshot.Log.Count(entry => entry.Code == ManualMotionCodes.OperationInProgress
                && entry.Axis == ManualMotionAxis.C));

        harness.Session.RequestStop(ManualMotionAxis.C);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.C) is null);
        Assert.Equal(ManualMotionCodes.MoveAbortedByOperator, harness.Axis(ManualMotionAxis.C).LastOutcomeCode);
    }

    [Fact]
    public void Jog_Positive_Then_Release_Stops_And_Applies_The_Brake_In_Order()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 7);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        // Hold the press until the session publishes the post-Begin positive position observation:
        // the release follows a fresh observed displacement, never an assumed number of Steps.
        for (var index = 0; index < 40 && !(harness.Axis(ManualMotionAxis.A).PositionTp.Value > 12000m); index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.A, 7);
            harness.Drive(1, 50);
        }

        harness.Session.RequestJogRelease(ManualMotionAxis.A, 7, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0);

        Assert.Equal(ManualMotionCodes.JogStoppedOnRelease, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(
            new[]
            {
                ManualMotionPortCommandKind.BrakeOutputSet,
                ManualMotionPortCommandKind.JogSpeed,
                ManualMotionPortCommandKind.Begin,
                ManualMotionPortCommandKind.Stop,
                ManualMotionPortCommandKind.BrakeOutputClear,
            },
            harness.Port.ExecutedCommands.Select(command => command.Kind).ToArray());
        Assert.Equal(2000, harness.Port.ExecutedCommands[1].Operand);
        Assert.True(harness.Axis(ManualMotionAxis.A).PositionTp.Value > 12000m);
        Assert.Equal(ManualAxisStopState.Issued, harness.Axis(ManualMotionAxis.A).StopState);
        Assert.False(harness.Axis(ManualMotionAxis.A).StopConfirmed);
        Assert.Equal(ManualMotionBrakeCommandState.ApplyCommanded, harness.Axis(ManualMotionAxis.A).BrakeCommandState);
    }

    [Fact]
    public void Jog_Negative_Carries_A_Negative_Speed_And_Capture_Loss_Stops_It()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Negative, 9);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);

        // Hold the press until the post-Begin negative position observation is published.
        for (var index = 0; index < 40 && !(harness.Axis(ManualMotionAxis.B).PositionTp.Value < -8000m); index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 9);
            harness.Drive(1, 50);
        }

        harness.Session.RequestJogRelease(ManualMotionAxis.B, 9, ManualMotionJogEndReason.CaptureLost);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0);

        Assert.Equal(ManualMotionCodes.JogStoppedCaptureLost, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        var jogSpeed = harness.Port.ExecutedCommands.Single(command => command.Kind == ManualMotionPortCommandKind.JogSpeed);
        Assert.Equal(-2000, jogSpeed.Operand);
        Assert.True(harness.Axis(ManualMotionAxis.B).PositionTp.Value < -8000m);
    }

    [Fact]
    public void Jog_On_Axis_C_Runs_The_Same_Lifecycle_As_Axis_A()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.C, ManualMotionJogDirection.Positive, 21);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.C)?.Phase == ManualMotionOperationPhase.Running);

        // Hold the press until the post-Begin positive position observation is published.
        for (var index = 0; index < 40 && !(harness.Axis(ManualMotionAxis.C).PositionTp.Value > 5000m); index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.C, 21);
            harness.Drive(1, 50);
        }

        harness.Session.RequestJogRelease(ManualMotionAxis.C, 21, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0);

        Assert.Equal(ManualMotionCodes.JogStoppedOnRelease, harness.Axis(ManualMotionAxis.C).LastOutcomeCode);
        Assert.Equal(
            new[]
            {
                ManualMotionPortCommandKind.BrakeOutputSet,
                ManualMotionPortCommandKind.JogSpeed,
                ManualMotionPortCommandKind.Begin,
                ManualMotionPortCommandKind.Stop,
                ManualMotionPortCommandKind.BrakeOutputClear,
            },
            harness.Port.ExecutedCommands.Select(command => command.Kind).ToArray());
        Assert.Equal(2000, harness.Port.ExecutedCommands[1].Operand);
        Assert.All(harness.Port.ExecutedCommands, command => Assert.Equal(ManualMotionAxis.C, command.Axis));
        Assert.Equal(3, harness.Port.ExecutedCommands[0].BrakeOutput);
        Assert.True(harness.Axis(ManualMotionAxis.C).PositionTp.Value > 5000m);
        Assert.Equal(ManualAxisStopState.Issued, harness.Axis(ManualMotionAxis.C).StopState);
        Assert.False(harness.Axis(ManualMotionAxis.C).StopConfirmed);
        Assert.Equal(ManualMotionBrakeCommandState.ApplyCommanded, harness.Axis(ManualMotionAxis.C).BrakeCommandState);
    }

    [Fact]
    public void Jog_Negative_On_Axis_D_Carries_A_Negative_Speed_And_Capture_Loss_Stops_It()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.D, ManualMotionJogDirection.Negative, 23);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.D)?.Phase == ManualMotionOperationPhase.Running);

        // Hold the press until the post-Begin negative position observation is published.
        for (var index = 0; index < 40 && !(harness.Axis(ManualMotionAxis.D).PositionTp.Value < -3000m); index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.D, 23);
            harness.Drive(1, 50);
        }

        harness.Session.RequestJogRelease(ManualMotionAxis.D, 23, ManualMotionJogEndReason.CaptureLost);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0);

        Assert.Equal(ManualMotionCodes.JogStoppedCaptureLost, harness.Axis(ManualMotionAxis.D).LastOutcomeCode);
        var jogSpeed = harness.Port.ExecutedCommands.Single(command => command.Kind == ManualMotionPortCommandKind.JogSpeed);
        Assert.Equal(-2000, jogSpeed.Operand);
        Assert.Equal(ManualMotionAxis.D, jogSpeed.Axis);
        Assert.True(harness.Axis(ManualMotionAxis.D).PositionTp.Value < -3000m);
    }

    [Fact]
    public void An_Active_Operation_On_Axis_C_Does_Not_Block_Axis_D()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.C);
        harness.HomeAxis(ManualMotionAxis.D);

        harness.Session.RequestMove(ManualMotionAxis.C, 2500);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.C)?.Phase == ManualMotionOperationPhase.Running);

        // Cross-axis independence: Move D is accepted while C runs. This used to be refused with
        // the global OPERATION_IN_PROGRESS slot; each axis now owns its own lifecycle.
        harness.Session.RequestMove(ManualMotionAxis.D, -1500);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.D)?.Phase == ManualMotionOperationPhase.Running);

        var opC = ActiveOf(harness.Snapshot, ManualMotionAxis.C);
        var opD = ActiveOf(harness.Snapshot, ManualMotionAxis.D);
        Assert.Equal(ManualMotionOperationKind.Move, opC?.Kind);
        Assert.Equal(ManualMotionOperationKind.Move, opD?.Kind);
        Assert.NotEqual(opC?.Id, opD?.Id);

        // Both operations progress and complete through their own lifecycle independently.
        harness.DriveUntil(
            snapshot => ActiveOf(snapshot, ManualMotionAxis.C) is null && ActiveOf(snapshot, ManualMotionAxis.D) is null,
            maximumSteps: 1200);

        Assert.Equal(ManualMotionCodes.MoveCompletedPositionVerified, harness.Axis(ManualMotionAxis.C).LastOutcomeCode);
        Assert.Equal(ManualMotionCodes.MoveCompletedPositionVerified, harness.Axis(ManualMotionAxis.D).LastOutcomeCode);
    }

    [Fact]
    public void Jog_On_Axis_C_With_An_Ignored_Stop_Times_Out_Without_Auto_Brake_Apply()
    {
        var harness = new Harness(new ManualMotionSimulationOptions { StopIgnored = true });
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.C, ManualMotionJogDirection.Positive, 29);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.C)?.Phase == ManualMotionOperationPhase.Running);
        harness.Session.RequestJogRelease(ManualMotionAxis.C, 29, ManualMotionJogEndReason.Released);

        // The Stop is dispatched but the simulated axis keeps moving: the operation can only end
        // by the configured stop timeout, and the brake is never applied automatically.
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0, maximumSteps: 400);

        Assert.Equal(ManualMotionCodes.StopNotObservedWithinTimeout, harness.Axis(ManualMotionAxis.C).LastOutcomeCode);
        Assert.Equal(
            new[]
            {
                ManualMotionPortCommandKind.BrakeOutputSet,
                ManualMotionPortCommandKind.JogSpeed,
                ManualMotionPortCommandKind.Begin,
                ManualMotionPortCommandKind.Stop,
            },
            harness.Port.ExecutedCommands.Select(command => command.Kind).ToArray());
        Assert.DoesNotContain(harness.Port.ExecutedCommands, command => command.Kind == ManualMotionPortCommandKind.BrakeOutputClear);
        Assert.Equal(ManualMotionBrakeCommandState.ReleaseCommanded, harness.Axis(ManualMotionAxis.C).BrakeCommandState);
        Assert.False(harness.Axis(ManualMotionAxis.C).StopConfirmed);
    }

    [Fact]
    public void Axes_C_And_D_Are_Visible_But_Not_Allowed_When_The_Profile_Lists_Only_Axis_A()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(4);

        Assert.True(harness.Axis(ManualMotionAxis.A).AllowedByProfile);
        Assert.False(harness.Axis(ManualMotionAxis.C).AllowedByProfile);
        Assert.False(harness.Axis(ManualMotionAxis.D).AllowedByProfile);
        Assert.Equal("WJ3", harness.Axis(ManualMotionAxis.C).WaterJetId);
        Assert.Equal(4, harness.Snapshot.Axes.Count);

        harness.Session.RequestBrakeRelease(ManualMotionAxis.C);
        harness.Drive(3);

        Assert.Equal(ManualMotionCodes.AxisNotAllowedByProfile, harness.Axis(ManualMotionAxis.C).LastOutcomeCode);
        Assert.Empty(harness.Port.Executed);
    }

    [Fact]
    public void Jog_Heartbeat_Expiry_Stops_Without_A_Release()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 11);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        // No further heartbeat: the configured heartbeat timeout ends the Jog through the Stop path.
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0, maximumSteps: 200, millisecondsPerStep: 50);

        Assert.Equal(ManualMotionCodes.JogStoppedHeartbeatExpired, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Contains(harness.Port.ExecutedCommands, command => command.Kind == ManualMotionPortCommandKind.Stop);
    }

    [Fact]
    public void Jog_Maximum_Hold_Stops_Even_With_Heartbeats()
    {
        var source = ManualMotionProfile.CreateSimulationProfile();
        var profile = ProfileWith(source, jogMaximumHoldMilliseconds: 700, jogHeartbeatTimeoutMilliseconds: 5000);
        var harness = new Harness(profile: profile);
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 13);

        // A queued request is not active until a Step applies it; wait for Running so the loop
        // below cannot exit vacuously on the pre-request idle snapshot.
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);
        for (var index = 0; index < 300 && harness.Snapshot.ActiveOperations.Count > 0; index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.A, 13);
            harness.Drive(1, 25);
        }

        // Reaching idle while heartbeats continue can only be the maximum-hold stop (or a later
        // settling step); drive out any brake-settling remainder before asserting the outcome.
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0);
        Assert.Equal(ManualMotionCodes.JogStoppedMaximumHold, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
    }

    [Fact]
    public void A_Standalone_Stop_Is_Dispatch_Only_And_Never_Confirmation()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestStop(ManualMotionAxis.B);
        harness.DriveUntil(_ => harness.Axis(ManualMotionAxis.B).StopState == ManualAxisStopState.Issued);

        Assert.Equal(ManualMotionCodes.StopIssued, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.False(harness.Axis(ManualMotionAxis.B).StopConfirmed);
        var stop = Assert.Single(harness.Port.ExecutedCommands);
        Assert.Equal(ManualMotionPortCommandKind.Stop, stop.Kind);
        Assert.Equal(ManualMotionAxis.B, stop.Axis);
    }

    [Fact]
    public void Home_Completes_With_Stop_Then_Dp_Zero_Verified_Then_Brake_Apply()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.HomeAxis(ManualMotionAxis.B);

        var axis = harness.Axis(ManualMotionAxis.B);
        Assert.Equal(ManualMotionHomeState.Homed, axis.HomeState);
        Assert.Equal(ManualMotionCoordinateState.ZeroVerified, axis.CoordinateState);
        Assert.Equal(ManualMotionCodes.HomeCompletedZeroVerified, axis.LastOutcomeCode);
        Assert.Equal(ManualMotionBrakeCommandState.ApplyCommanded, axis.BrakeCommandState);
        Assert.Equal(
            new[]
            {
                ManualMotionPortCommandKind.BrakeOutputSet,
                ManualMotionPortCommandKind.HomeSpeed,
                ManualMotionPortCommandKind.Home,
                ManualMotionPortCommandKind.Begin,
                ManualMotionPortCommandKind.Stop,
                ManualMotionPortCommandKind.DefinePositionZero,
                ManualMotionPortCommandKind.BrakeOutputClear,
            },
            harness.Port.ExecutedCommands.Select(command => command.Kind).ToArray());
        Assert.True(Math.Abs(axis.PositionTp.Value ?? 999m) <= 2m);
        Assert.True(Math.Abs(axis.PositionRp.Value ?? 999m) <= 2m);
    }

    [Fact]
    public void Stop_During_Home_Aborts_Through_The_Stop_And_Brake_Path()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestHome(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);
        harness.Session.RequestStop(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0);

        var axis = harness.Axis(ManualMotionAxis.A);
        Assert.Equal(ManualMotionCodes.HomeAbortedByOperator, axis.LastOutcomeCode);
        Assert.Equal(ManualMotionHomeState.HomeFailed, axis.HomeState);
        var kinds = harness.Port.ExecutedCommands.Select(command => command.Kind).ToList();
        Assert.Contains(ManualMotionPortCommandKind.Stop, kinds);
        Assert.Equal(ManualMotionPortCommandKind.BrakeOutputClear, kinds[^1]);
        Assert.DoesNotContain(ManualMotionPortCommandKind.DefinePositionZero, kinds);
    }

    [Fact]
    public void Home_Times_Out_When_The_Switch_Is_Unreachable()
    {
        var harness = new Harness(new ManualMotionSimulationOptions { HomeReachable = false });
        harness.OpenAndObserve();

        harness.Session.RequestHome(ManualMotionAxis.B);

        // Wait until the queued Home has actually begun; no operation is published before it.
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0, maximumSteps: 900, millisecondsPerStep: 100);

        var axis = harness.Axis(ManualMotionAxis.B);
        Assert.Equal(ManualMotionCodes.HomeTimeout, axis.LastOutcomeCode);
        Assert.Equal(ManualMotionHomeState.HomeFailed, axis.HomeState);
        Assert.Contains(harness.Port.ExecutedCommands, command => command.Kind == ManualMotionPortCommandKind.Stop);
    }

    [Fact]
    public void Define_Position_Zero_Verifies_From_Reads_After_The_Define()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestDefinePositionZero(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[0].CoordinateState == ManualMotionCoordinateState.ZeroVerified);

        var axis = harness.Axis(ManualMotionAxis.A);
        Assert.Equal(ManualMotionCodes.PositionZeroVerified, axis.LastOutcomeCode);
        Assert.Equal(0m, axis.PositionTp.Value);
        var define = Assert.Single(harness.Port.ExecutedCommands);
        Assert.Equal(ManualMotionPortCommandKind.DefinePositionZero, define.Kind);
        Assert.Equal(0, define.Operand);
    }

    [Fact]
    public void Define_Position_Zero_Reports_Verification_Failure_When_Nothing_Changes()
    {
        var harness = new Harness(new ManualMotionSimulationOptions { ZeroDefinitionIgnored = true });
        harness.OpenAndObserve();

        harness.Session.RequestDefinePositionZero(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[0].CoordinateState == ManualMotionCoordinateState.ZeroVerificationFailed);

        Assert.Equal(
            ManualMotionCodes.PositionZeroVerificationFailed,
            harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
    }

    [Fact]
    public void Define_Dual_Zero_Stays_Unverified_And_Clears_The_Session_Home_State()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.B);
        var beforeDualZero = harness.Port.ExecutedCommands.Count;

        harness.Session.RequestDefineDualZero(ManualMotionAxis.B);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[1].DualZeroState == ManualMotionDualZeroState.DispatchedUnverified);

        var axis = harness.Axis(ManualMotionAxis.B);
        Assert.Equal(ManualMotionCodes.DualZeroDispatchedEffectUnverified, axis.LastOutcomeCode);
        Assert.Equal(ManualMotionHomeState.NotHomed, axis.HomeState);
        Assert.Equal(
            new[] { ManualMotionPortCommandKind.DefineDualZero },
            KindsAfter(harness, beforeDualZero).ToArray());
    }

    [Fact]
    public void Move_Requires_A_Verified_Home_In_This_Session()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestMove(ManualMotionAxis.A, 100);
        harness.Drive(3);

        Assert.Equal(ManualMotionCodes.MoveRequiresHomed, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Empty(harness.Port.ExecutedCommands);
    }

    [Fact]
    public void Move_Target_Outside_The_Commissioned_Bounds_Is_Refused()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.B);
        var beforeMove = harness.Port.ExecutedCommands.Count;

        harness.Session.RequestMove(ManualMotionAxis.B, 999999);
        harness.Drive(3);

        Assert.Equal(ManualMotionCodes.MoveTargetOutOfBounds, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.Equal(beforeMove, harness.Port.ExecutedCommands.Count);
    }

    [Fact]
    public void Bounded_Absolute_Move_Completes_Within_The_Configured_Tolerance()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.B);
        var beforeMove = harness.Port.ExecutedCommands.Count;

        harness.Session.RequestMove(ManualMotionAxis.B, -3000);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[1].LastOutcomeCode == ManualMotionCodes.MoveCompletedPositionVerified);

        var axis = harness.Axis(ManualMotionAxis.B);
        Assert.True(Math.Abs((axis.PositionTp.Value ?? 99999m) - -3000m) <= 2m);
        Assert.True(Math.Abs((axis.PositionRp.Value ?? 99999m) - -3000m) <= 2m);
        Assert.Equal(
            new[]
            {
                ManualMotionPortCommandKind.BrakeOutputSet,
                ManualMotionPortCommandKind.Acceleration,
                ManualMotionPortCommandKind.Speed,
                ManualMotionPortCommandKind.Deceleration,
                ManualMotionPortCommandKind.PositionAbsolute,
                ManualMotionPortCommandKind.Begin,
                ManualMotionPortCommandKind.BrakeOutputClear,
            },
            KindsAfter(harness, beforeMove).ToArray());
        var absolute = harness.Port.ExecutedCommands
            .Single(command => command.Kind == ManualMotionPortCommandKind.PositionAbsolute);
        Assert.Equal(-3000, absolute.Operand);
    }

    [Fact]
    public void Move_Timeout_Stops_Through_The_Supervised_Stop_Path()
    {
        var source = ManualMotionProfile.CreateSimulationProfile();
        var axisB = CloneAxis(source.AxisB!, motionTimeoutMilliseconds: 300);
        var harness = new Harness(profile: ProfileWith(source, axisB: axisB));
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.B);
        var beforeMove = harness.Port.ExecutedCommands.Count;

        harness.Session.RequestMove(ManualMotionAxis.B, 15000);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[1].LastOutcomeCode == ManualMotionCodes.MoveTimeout);

        var segment = KindsAfter(harness, beforeMove);
        Assert.Contains(ManualMotionPortCommandKind.Stop, segment);
        Assert.Equal(ManualMotionPortCommandKind.BrakeOutputClear, segment[^1]);
    }

    [Fact]
    public void An_Axis_Not_Listed_In_The_Profile_Is_Refused()
    {
        var source = ManualMotionProfile.CreateSimulationProfile();
        var harness = new Harness(profile: ProfileWith(source, omitAxisB: true));
        harness.OpenAndObserve();

        Assert.False(harness.Axis(ManualMotionAxis.B).AllowedByProfile);
        harness.Session.RequestBrakeRelease(ManualMotionAxis.B);
        harness.Drive(3);

        Assert.Equal(ManualMotionCodes.AxisNotAllowedByProfile, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.Empty(harness.Port.ExecutedCommands);
    }

    [Fact]
    public void A_Not_Configured_Value_Refuses_Only_The_Operation_That_Needs_It()
    {
        var source = ManualMotionProfile.CreateSimulationProfile();
        var harness = new Harness(profile: ProfileWith(source, axisA: AxisWithoutJogSpeed(source.AxisA!)));
        harness.OpenAndObserve();

        Assert.Contains("jogSpeed", harness.Axis(ManualMotionAxis.A).NotConfigured);

        harness.Session.RequestBrakeRelease(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[0].BrakeCommandState == ManualMotionBrakeCommandState.ReleaseCommanded);

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 21);
        harness.Drive(3);

        Assert.Equal(ManualMotionCodes.JogSpeedNotConfigured, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.DoesNotContain(
            harness.Port.ExecutedCommands,
            command => command.Kind == ManualMotionPortCommandKind.JogSpeed);
    }

    [Fact]
    public void A_Communication_Fault_Faults_The_Session_And_Nothing_Reconnects()
    {
        var harness = new Harness(new ManualMotionSimulationOptions
        {
            FaultOnCommand = ManualMotionPortCommandKind.Begin,
        });
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 31);
        harness.DriveUntil(snapshot => snapshot.State == ManualMotionSessionState.Faulted, maximumSteps: 100);

        Assert.Contains(harness.Snapshot.Log, entry => entry.Code == ManualMotionCodes.SessionFaulted);
        Assert.Equal(1, harness.Port.OpenCount);

        harness.Session.RequestBrakeRelease(ManualMotionAxis.A);
        harness.Drive(3);
        Assert.Equal(ManualMotionCodes.SessionNotReady, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(1, harness.Port.OpenCount);

        harness.Session.RequestClose();
        harness.DriveUntil(snapshot => snapshot.State == ManualMotionSessionState.Closed, maximumSteps: 100);
        Assert.Equal(1, harness.Port.CloseCount);
        Assert.Equal(1, harness.Port.OpenCount);
    }

    [Fact]
    public void A_Stale_Command_Result_Is_Rejected_And_Not_Applied()
    {
        var clock = new TestClock(Instant);
        var port = new FakePort
        {
            ExecuteHandler = _ => new ManualMotionPortCommandResult(true, false, true, "FAKE_DISPATCHED", 99),
        };
        var session = new ManualMotionSession(
            port,
            ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile()),
            clock);

        session.Step();
        Assert.Equal(ManualMotionSessionState.Ready, session.Snapshot.State);

        session.RequestBrakeRelease(ManualMotionAxis.A);
        clock.Advance(TimeSpan.FromMilliseconds(25));
        session.Step();

        Assert.Equal(ManualMotionSessionState.Faulted, session.Snapshot.State);
        Assert.Equal(
            ManualMotionBrakeCommandState.NotCommanded,
            session.Snapshot.Axes[0].BrakeCommandState);
        Assert.Contains(session.Snapshot.Log, entry => entry.Code == ManualMotionCodes.StaleResultRejected);
    }

    [Fact]
    public void A_Stale_Read_Result_Leaves_No_Value_Behind()
    {
        var clock = new TestClock(Instant);
        var port = new FakePort
        {
            ReadHandler = _ => new ManualMotionPortReadResult(true, false, 123m, "FAKE_READ", 99),
        };
        var session = new ManualMotionSession(
            port,
            ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile()),
            clock);

        session.Step();
        for (var index = 0; index < 4; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }

        var status = session.Snapshot.Axes[0].StatusWord;
        Assert.Null(status.Value);
        Assert.False(status.Fresh);
        Assert.Equal(ManualMotionCodes.StaleResultRejected, status.LastRefusalCode);
        Assert.Equal(ManualMotionSessionState.Ready, session.Snapshot.State);
    }

    [Fact]
    public void A_Refused_Read_Fabricates_Nothing()
    {
        var harness = new Harness(new ManualMotionSimulationOptions
        {
            RefuseReadKind = ManualMotionReadKind.PositionTp,
        });
        harness.OpenAndObserve();

        var position = harness.Axis(ManualMotionAxis.A).PositionTp;
        Assert.Null(position.Value);
        Assert.False(position.Fresh);
        Assert.Equal(ManualMotionSimulatedPort.CodeReadRefused, position.LastRefusalCode);
        Assert.NotNull(harness.Axis(ManualMotionAxis.A).StatusWord.Value);
    }

    [Fact]
    public void Closing_During_Jog_Stops_First_Then_Closes_Exactly_Once()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 51);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        harness.Session.RequestClose();
        harness.DriveUntil(snapshot => snapshot.State == ManualMotionSessionState.Closed, maximumSteps: 200);

        Assert.Equal(ManualMotionCodes.JogStoppedSessionClosing, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Contains(harness.Port.ExecutedCommands, command => command.Kind == ManualMotionPortCommandKind.Stop);
        Assert.Equal(1, harness.Port.OpenCount);
        Assert.Equal(1, harness.Port.CloseCount);
        Assert.True(harness.Snapshot.IsTerminal);

        harness.Drive(3);
        Assert.Equal(1, harness.Port.CloseCount);
    }

    [Fact]
    public void An_Out_Of_Domain_Axis_Is_Refused_Without_Any_Dispatch()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestBrakeRelease((ManualMotionAxis)99);
        harness.Drive(3);

        Assert.Contains(harness.Snapshot.Log, entry => entry.Code == ManualMotionCodes.AxisOutOfDomain);
        Assert.Empty(harness.Port.ExecutedCommands);
    }

    [Fact]
    public void A_Fresh_Other_Axis_Moving_Observation_Never_Refuses_An_Independent_Axis()
    {
        var clock = new TestClock(Instant);
        var port = new FakePort
        {
            ReadHandler = read => new ManualMotionPortReadResult(
                true,
                false,
                read.Kind == ManualMotionReadKind.StatusWord && read.Axis == ManualMotionAxis.B ? 64m : 0m,
                "FAKE_READ",
                1),
        };
        var session = new ManualMotionSession(
            port,
            ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile()),
            clock);

        session.Step();
        for (var index = 0; index < 16; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }

        Assert.Equal(ManualMotionInterpretation.Active, session.Snapshot.Axes[1].Motion);

        session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 61);
        clock.Advance(TimeSpan.FromMilliseconds(25));
        session.Step();

        // Axis B being observed moving never refuses an operation on Axis A: only Axis A's own
        // preconditions are evaluated, and the axes are independent.
        Assert.NotEqual(ManualMotionCodes.OtherAxisMovingObserved, session.Snapshot.Axes[0].LastOutcomeCode);
        Assert.NotEqual(ManualMotionCodes.OperationInProgress, session.Snapshot.Axes[0].LastOutcomeCode);
        Assert.DoesNotContain(session.Snapshot.Log, entry => entry.Code == ManualMotionCodes.OtherAxisMovingObserved);
        var active = Assert.Single(session.Snapshot.ActiveOperations);
        Assert.Equal(ManualMotionAxis.A, active.Axis);
        Assert.Equal(ManualMotionOperationKind.Jog, active.Kind);

        session.RequestJogRelease(ManualMotionAxis.A, 61, ManualMotionJogEndReason.Released);
        for (var index = 0; index < 40 && session.Snapshot.ActiveOperations.Count > 0; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }
    }

    [Fact]
    public void Home_Is_Refused_When_The_Home_Input_Is_Already_Active()
    {
        var clock = new TestClock(Instant);
        var port = new FakePort
        {
            ReadHandler = read => new ManualMotionPortReadResult(
                true,
                false,
                read.Kind == ManualMotionReadKind.StatusWord && read.Axis == ManualMotionAxis.A ? 8m : 0m,
                "FAKE_READ",
                1),
        };
        var session = new ManualMotionSession(
            port,
            ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile()),
            clock);

        session.Step();
        for (var index = 0; index < 16; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }

        Assert.Equal(ManualMotionInterpretation.Active, session.Snapshot.Axes[0].HomeInput);

        session.RequestHome(ManualMotionAxis.A);
        clock.Advance(TimeSpan.FromMilliseconds(25));
        session.Step();

        Assert.Equal(ManualMotionCodes.HomeInputAlreadyActive, session.Snapshot.Axes[0].LastOutcomeCode);
        Assert.Empty(port.Executed);
    }

    [Fact]
    public void Idle_Cycle_Reads_The_Complete_Set_Status_First_At_Cycle_Cadence()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();

        for (var step = 0; step < 40; step++)
        {
            var readsBefore = harness.Reads.Count;
            harness.StepOnce();
            Assert.True(harness.Reads.Count - readsBefore <= 1, "more than one native read in a single Step");
            if (harness.Reads.Count > 0)
            {
                Assert.True(
                    harness.Axis(ManualMotionAxis.A).StatusWord.Fresh,
                    "status went stale inside the 100/300 complete-cycle cadence");
            }
        }

        Assert.Equal(
            new[]
            {
                new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionTp, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionRp, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.A, 1),
            },
            harness.Reads.Take(4).ToArray());
        // The interval is the cadence between cycle STARTS: the second cycle begins one interval
        // after the first, and its remaining reads follow on consecutive steps.
        Assert.Equal(ManualMotionReadKind.StatusWord, harness.Reads[4].Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(100), harness.ReadTimes[4] - harness.ReadTimes[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(10), harness.ReadTimes[5] - harness.ReadTimes[4]);
        // Exactly four complete cycles start within 400 ms of driving; no catch-up burst adds more.
        Assert.Equal(16, harness.Reads.Count);
        Assert.Empty(harness.Port.Executed);
    }

    [Fact]
    public void Four_Allowed_Axes_Cycle_In_Deterministic_Order_With_Status_First_Per_Axis()
    {
        var harness = new RecordingHarness(ManualMotionProfile.CreateSimulationProfile());
        harness.OpenSession();
        harness.Drive(16, 25);

        // The least-recently-served arbitration interleaves complete cycles across axes while each
        // axis keeps its own Status-first order: the four Status reads come first, then TP, RP and
        // the brake outputs, in stable A/B/C/D tie-breaking order.
        Assert.Equal(
            new[]
            {
                new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.B, 0),
                new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.C, 0),
                new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.D, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionTp, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionTp, ManualMotionAxis.B, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionTp, ManualMotionAxis.C, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionTp, ManualMotionAxis.D, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionRp, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionRp, ManualMotionAxis.B, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionRp, ManualMotionAxis.C, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionRp, ManualMotionAxis.D, 0),
                new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.A, 1),
                new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.B, 2),
                new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.C, 3),
                new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.D, 4),
            },
            harness.Reads.Take(16).ToArray());
    }

    [Fact]
    public void Jog_At_The_Approved_Timing_Never_Hits_A_Stale_Status_Window()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(50);

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 71);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        // While the Jog runs, the complete active cycle keeps Status and the brake output current.
        var readsAtRunning = harness.Reads.Count;
        harness.DriveUntil(
            _ => harness.Reads.Skip(readsAtRunning).Any(read => read.Kind == ManualMotionReadKind.StatusWord)
                && harness.Reads.Skip(readsAtRunning).Any(read => read.Kind == ManualMotionReadKind.BrakeOutputState),
            maximumSteps: 50);
        Assert.Equal(ManualMotionInterpretation.Active, harness.Axis(ManualMotionAxis.A).Motion);
        Assert.True(harness.Axis(ManualMotionAxis.A).BrakeOutput.Fresh);
        Assert.Equal(1m, harness.Axis(ManualMotionAxis.A).BrakeOutput.Value);

        harness.Session.RequestJogRelease(ManualMotionAxis.A, 71, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0);

        Assert.Equal(ManualMotionCodes.JogStoppedOnRelease, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(
            new[]
            {
                ManualMotionPortCommandKind.BrakeOutputSet,
                ManualMotionPortCommandKind.JogSpeed,
                ManualMotionPortCommandKind.Begin,
                ManualMotionPortCommandKind.Stop,
                ManualMotionPortCommandKind.BrakeOutputClear,
            },
            harness.Port.Executed.Select(command => command.Kind).ToArray());
        Assert.DoesNotContain(harness.Snapshot.Log, entry => entry.Code == ManualMotionCodes.ObservationNotFresh);
        // After the Stop dispatch, Status evidence had sole priority until the operation completed:
        // no TP, RP or brake-output read was placed ahead of it.
        Assert.True(harness.ReadsAtLastStop >= 0);
        Assert.NotEmpty(harness.Reads.Skip(harness.ReadsAtLastStop));
        Assert.All(
            harness.Reads.Skip(harness.ReadsAtLastStop),
            read => Assert.Equal(ManualMotionReadKind.StatusWord, read.Kind));
    }

    [Fact]
    public void An_Accepted_Begin_Rebases_The_Observation_Cycle_Status_First()
    {
        var harness = new RecordingHarness(TrialLikeProfile()) { PositionValue = 111m };
        harness.OpenSession();
        harness.Drive(4);

        // One complete idle cycle has published the pre-motion position.
        Assert.Equal(4, harness.Reads.Count);
        Assert.Equal(111m, harness.Axis(ManualMotionAxis.A).PositionTp.Value);

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 7);

        // The brake release consumes the step's one port call; no read shares it.
        harness.StepOnce();
        Assert.Equal(4, harness.Reads.Count);
        Assert.Equal(
            new[] { ManualMotionPortCommandKind.BrakeOutputSet },
            harness.Port.Executed.Select(command => command.Kind).ToArray());

        // One long step lands inside the brake settle: a pre-Begin cycle starts with Status and
        // is deliberately left incomplete (TP, RP and the brake output remain unread).
        harness.StepOnce(90);
        Assert.Equal(ManualMotionOperationPhase.BrakeSettling, ActiveOf(harness.Snapshot, ManualMotionAxis.A)?.Phase);
        Assert.Equal(5, harness.Reads.Count);
        Assert.Equal(ManualMotionReadKind.StatusWord, harness.Reads[4].Kind);
        Assert.Single(harness.Port.Executed);
        Assert.Equal(111m, harness.Axis(ManualMotionAxis.A).PositionTp.Value);

        // Configuring and Beginning each consume their step's one port call.
        harness.StepOnce();
        Assert.Equal(5, harness.Reads.Count);
        Assert.Equal(2, harness.Port.Executed.Count);
        harness.StepOnce();
        Assert.Equal(5, harness.Reads.Count);
        Assert.Equal(ManualMotionPortCommandKind.Begin, harness.Port.Executed[2].Kind);
        Assert.Equal(ManualMotionOperationPhase.Running, ActiveOf(harness.Snapshot, ManualMotionAxis.A)?.Phase);

        // Motion has begun: the synthetic position now differs from the pre-Begin value. The
        // pre-Begin cycle remainder is discarded, so the first observation after Begin is a
        // fresh Status read — never the leftover TP — and only then does TP publish the
        // post-Begin position.
        harness.PositionValue = 222m;
        harness.StepOnce();
        Assert.Equal(6, harness.Reads.Count);
        Assert.Equal(ManualMotionReadKind.StatusWord, harness.Reads[5].Kind);
        Assert.Equal(ManualMotionInterpretation.Active, harness.Axis(ManualMotionAxis.A).Motion);
        Assert.Equal(111m, harness.Axis(ManualMotionAxis.A).PositionTp.Value);

        harness.StepOnce();
        Assert.Equal(7, harness.Reads.Count);
        Assert.Equal(ManualMotionReadKind.PositionTp, harness.Reads[6].Kind);
        Assert.Equal(3, harness.Port.Executed.Count);
        Assert.Equal(222m, harness.Axis(ManualMotionAxis.A).PositionTp.Value);

        // Capture loss pre-empts the rest of the cycle: the Stop is the next port call, ahead of
        // the unread RP and brake-output remainder.
        harness.Session.RequestJogRelease(ManualMotionAxis.A, 7, ManualMotionJogEndReason.CaptureLost);
        harness.StepOnce();
        Assert.Equal(7, harness.Reads.Count);
        Assert.Equal(ManualMotionPortCommandKind.Stop, harness.Port.Executed[3].Kind);

        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0);

        Assert.Equal(ManualMotionCodes.JogStoppedCaptureLost, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(
            new[]
            {
                ManualMotionPortCommandKind.BrakeOutputSet,
                ManualMotionPortCommandKind.JogSpeed,
                ManualMotionPortCommandKind.Begin,
                ManualMotionPortCommandKind.Stop,
                ManualMotionPortCommandKind.BrakeOutputClear,
            },
            harness.Port.Executed.Select(command => command.Kind).ToArray());
        // While the stopped state was awaited, Status evidence kept sole priority.
        Assert.Equal(7, harness.ReadsAtLastStop);
        var afterStop = Assert.Single(harness.Reads.Skip(harness.ReadsAtLastStop));
        Assert.Equal(ManualMotionReadKind.StatusWord, afterStop.Kind);
        // The post-Begin position stays published; nothing reverts to the pre-Begin value.
        Assert.Equal(222m, harness.Axis(ManualMotionAxis.A).PositionTp.Value);
    }

    [Fact]
    public void Jog_Still_Refuses_When_Status_Is_Genuinely_Stale()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(20);
        Assert.True(harness.Axis(ManualMotionAxis.A).StatusWord.Fresh);

        harness.FailReadKinds.Add(ManualMotionReadKind.StatusWord);
        harness.Drive(40);
        Assert.False(harness.Axis(ManualMotionAxis.A).StatusWord.Fresh);
        Assert.True(harness.Axis(ManualMotionAxis.A).PositionTp.Fresh);

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 73);
        harness.StepOnce();

        Assert.Equal(ManualMotionCodes.ObservationNotFresh, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Empty(harness.Port.Executed);
    }

    [Fact]
    public void A_Standalone_Stop_Preempts_An_Incomplete_Observation_Cycle()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(2);

        // Status and TP of the first cycle are read; RP and the brake output remain.
        Assert.Equal(2, harness.Reads.Count);
        harness.Session.RequestStop(ManualMotionAxis.A);
        harness.StepOnce();

        Assert.Equal(2, harness.Reads.Count);
        var stop = Assert.Single(harness.Port.Executed);
        Assert.Equal(ManualMotionPortCommandKind.Stop, stop.Kind);

        harness.StepOnce();
        Assert.Equal(3, harness.Reads.Count);
        Assert.Equal(ManualMotionReadKind.PositionRp, harness.Reads[2].Kind);
    }

    [Fact]
    public void Close_Preempts_An_Incomplete_Observation_Cycle()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(2);

        harness.Session.RequestClose();
        harness.DriveUntil(snapshot => snapshot.State == ManualMotionSessionState.Closed, maximumSteps: 20);

        Assert.Equal(2, harness.Reads.Count);
        Assert.Equal(1, harness.Port.CloseCount);
        Assert.True(harness.Snapshot.IsTerminal);

        harness.Drive(3);
        Assert.Equal(2, harness.Reads.Count);
        Assert.Equal(1, harness.Port.CloseCount);
    }

    [Fact]
    public void A_Failed_Read_Keeps_Its_Prior_Value_And_Timestamp_Until_A_Later_Read_Recovers()
    {
        var harness = new RecordingHarness(TrialLikeProfile()) { PositionValue = 111m };
        harness.OpenSession();
        harness.Drive(4);

        var before = harness.Axis(ManualMotionAxis.A).PositionTp;
        Assert.Equal(111m, before.Value);
        Assert.True(before.Fresh);

        harness.FailReadKinds.Add(ManualMotionReadKind.PositionTp);
        harness.Drive(40);

        var stale = harness.Axis(ManualMotionAxis.A).PositionTp;
        Assert.Equal(111m, stale.Value);
        Assert.Equal(before.ObservedAt, stale.ObservedAt);
        Assert.False(stale.Fresh);
        Assert.Equal("FAKE_REFUSED", stale.LastRefusalCode);
        Assert.True(harness.Axis(ManualMotionAxis.A).StatusWord.Fresh);

        harness.FailReadKinds.Remove(ManualMotionReadKind.PositionTp);
        harness.PositionValue = 222m;
        harness.DriveUntil(_ => harness.Axis(ManualMotionAxis.A).PositionTp.Fresh);

        Assert.Equal(222m, harness.Axis(ManualMotionAxis.A).PositionTp.Value);
    }

    [Fact]
    public void A_Long_Pause_Produces_No_Catch_Up_Burst()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();

        harness.StepOnce(1000);
        Assert.Single(harness.Reads);

        harness.Drive(3);
        Assert.Equal(4, harness.Reads.Count);
        Assert.Equal(
            new[]
            {
                ManualMotionReadKind.StatusWord,
                ManualMotionReadKind.PositionTp,
                ManualMotionReadKind.PositionRp,
                ManualMotionReadKind.BrakeOutputState,
            },
            harness.Reads.Select(read => read.Kind).ToArray());
    }

    [Fact]
    public void Motion_And_Home_Interpretation_Configuration_Are_Reported_Independently()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(10);

        var axisA = harness.Axis(ManualMotionAxis.A);
        Assert.True(axisA.MotionInterpretationConfigured);
        Assert.False(axisA.HomeInterpretationConfigured);
        Assert.Equal(ManualMotionInterpretation.Inactive, axisA.Motion);
        Assert.Equal(ManualMotionInterpretation.Unknown, axisA.HomeInput);

        var axisB = harness.Axis(ManualMotionAxis.B);
        Assert.False(axisB.AllowedByProfile);
        Assert.False(axisB.MotionInterpretationConfigured);
        Assert.False(axisB.HomeInterpretationConfigured);
    }

    [Fact]
    public void Brake_Release_Refuses_When_The_Release_Polarity_Is_Not_Configured()
    {
        var harness = new RecordingHarness(TrialLikeProfile(includeBrakePolarity: false));
        harness.OpenSession();
        harness.Drive(4);

        harness.Session.RequestBrakeRelease(ManualMotionAxis.A);
        harness.StepOnce();

        Assert.Equal(ManualMotionCodes.BrakePolarityNotConfigured, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Empty(harness.Port.Executed);
    }

    [Fact]
    public void Home_Refuses_Before_Any_Command_When_Home_Parameters_Are_Not_Configured()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(4);

        harness.Session.RequestHome(ManualMotionAxis.A);
        harness.StepOnce();

        Assert.Equal(ManualMotionCodes.HomeParametersNotConfigured, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Empty(harness.Port.Executed);
    }

    [Fact]
    public void Define_Dual_Zero_Refuses_Without_Explicit_Profile_Permission()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(4);

        harness.Session.RequestDefineDualZero(ManualMotionAxis.A);
        harness.StepOnce();

        Assert.Equal(ManualMotionCodes.DualZeroNotPermittedByProfile, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Empty(harness.Port.Executed);
    }

    [Fact]
    public void Define_Position_Zero_Without_Verification_Configuration_Reports_Not_Verified()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(4);

        harness.Session.RequestDefinePositionZero(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[0].LastOutcomeCode == ManualMotionCodes.PositionZeroNotVerified);

        Assert.Equal(ManualMotionCoordinateState.ZeroDispatched, harness.Axis(ManualMotionAxis.A).CoordinateState);
        var define = Assert.Single(harness.Port.Executed);
        Assert.Equal(ManualMotionPortCommandKind.DefinePositionZero, define.Kind);
        Assert.Equal(0, define.Operand);
    }

    [Fact]
    public void Session_Log_And_Snapshot_Carry_Codes_Only()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.Session.RequestBrakeRelease(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperations.Count == 0
            && snapshot.Axes[0].LastOutcomeCode.Length > 0);

        Assert.All(harness.Snapshot.Log, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Code));
            Assert.Equal(ManualMotionCodes.DetailFor(entry.Code), entry.Detail);
        });
        Assert.All(
            harness.Snapshot.Axes,
            axis => Assert.False(axis.StopConfirmed));
    }

    // ------------------------------------------------------------------
    // Independent per-axis operation lifecycle (same-axis exclusion stays mandatory).
    // ------------------------------------------------------------------

    [Fact]
    public void Move_A_Active_Does_Not_Block_Home_B_And_Both_Progress()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.A);

        harness.Session.RequestMove(ManualMotionAxis.A, -5000);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        // Home B is accepted while Move A runs; both hold their own active operation at once.
        harness.Session.RequestHome(ManualMotionAxis.B);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is not null);
        Assert.NotNull(ActiveOf(harness.Snapshot, ManualMotionAxis.A));
        Assert.NotNull(ActiveOf(harness.Snapshot, ManualMotionAxis.B));

        // Move A completes through its own lifecycle while B keeps homing.
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null);
        Assert.Equal(ManualMotionCodes.MoveCompletedPositionVerified, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.NotNull(ActiveOf(harness.Snapshot, ManualMotionAxis.B));

        // Home B then completes independently.
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is null, maximumSteps: 1200);
        Assert.Equal(ManualMotionHomeState.Homed, harness.Axis(ManualMotionAxis.B).HomeState);
        Assert.Equal(ManualMotionCodes.HomeCompletedZeroVerified, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
    }

    [Fact]
    public void Move_A_Active_Does_Not_Block_Move_B_And_Both_Complete_Their_Own_Target()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.A);
        harness.HomeAxis(ManualMotionAxis.B);

        harness.Session.RequestMove(ManualMotionAxis.A, -5000);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        // Move B is accepted while Move A is already running: both hold active operations at once.
        harness.Session.RequestMove(ManualMotionAxis.B, 5000);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);
        Assert.NotEqual(ActiveOf(harness.Snapshot, ManualMotionAxis.A)?.Id, ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Id);

        harness.DriveUntil(
            snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null && ActiveOf(snapshot, ManualMotionAxis.B) is null,
            maximumSteps: 1200);

        Assert.Equal(ManualMotionCodes.MoveCompletedPositionVerified, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(ManualMotionCodes.MoveCompletedPositionVerified, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.True(Math.Abs((harness.Axis(ManualMotionAxis.A).PositionTp.Value ?? 99999m) - -5000m) <= 2m);
        Assert.True(Math.Abs((harness.Axis(ManualMotionAxis.B).PositionTp.Value ?? 99999m) - 5000m) <= 2m);
    }

    [Fact]
    public void Home_A_Active_Does_Not_Block_Jog_C()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestHome(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        harness.Session.RequestJogStart(ManualMotionAxis.C, ManualMotionJogDirection.Positive, 61);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.C)?.Phase == ManualMotionOperationPhase.Running);
        Assert.NotNull(ActiveOf(harness.Snapshot, ManualMotionAxis.A));

        // Releasing Jog C ends only C; Home A keeps running until its own completion.
        harness.Session.RequestJogRelease(ManualMotionAxis.C, 61, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.C) is null);
        Assert.Equal(ManualMotionCodes.JogStoppedOnRelease, harness.Axis(ManualMotionAxis.C).LastOutcomeCode);
        Assert.NotNull(ActiveOf(harness.Snapshot, ManualMotionAxis.A));

        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null, maximumSteps: 1200);
        Assert.Equal(ManualMotionHomeState.Homed, harness.Axis(ManualMotionAxis.A).HomeState);
    }

    [Fact]
    public void Jog_B_Active_Does_Not_Block_Move_D()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.D);

        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Negative, 63);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);

        harness.Session.RequestMove(ManualMotionAxis.D, -1500);
        harness.Drive(1);
        Assert.True(
            ActiveOf(harness.Snapshot, ManualMotionAxis.D) is not null,
            string.Concat("Move D must be accepted while Jog B runs; refusal code: ", harness.Axis(ManualMotionAxis.D).LastOutcomeCode));

        // Hold-to-run contract: Jog B needs its own heartbeats while Move D progresses, and both
        // operations advance independently until each completes on its own lifecycle.
        for (var index = 0;
             index < 80 && ActiveOf(harness.Snapshot, ManualMotionAxis.D)?.Phase != ManualMotionOperationPhase.Running;
             index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 63);
            harness.Drive(1, 25);
        }
        Assert.Equal(ManualMotionOperationPhase.Running, ActiveOf(harness.Snapshot, ManualMotionAxis.D)?.Phase);
        Assert.NotNull(ActiveOf(harness.Snapshot, ManualMotionAxis.B));

        for (var index = 0; index < 120 && ActiveOf(harness.Snapshot, ManualMotionAxis.D) is not null; index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 63);
            harness.Drive(1, 25);
        }
        Assert.Equal(ManualMotionCodes.MoveCompletedPositionVerified, harness.Axis(ManualMotionAxis.D).LastOutcomeCode);
        Assert.NotNull(ActiveOf(harness.Snapshot, ManualMotionAxis.B));

        harness.Session.RequestJogRelease(ManualMotionAxis.B, 63, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is null);
        Assert.Equal(ManualMotionCodes.JogStoppedOnRelease, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
    }

    [Fact]
    public void The_Active_Operations_Snapshot_Is_Per_Axis_Ordered_Read_Only_And_Removes_Only_The_Completed_Axis()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        Assert.Empty(harness.Snapshot.ActiveOperations);
        Assert.True(((IList<ManualMotionOperationSnapshot>)harness.Snapshot.ActiveOperations).IsReadOnly);

        harness.HomeAxis(ManualMotionAxis.A);
        harness.Session.RequestMove(ManualMotionAxis.A, -5000);
        harness.Session.RequestHome(ManualMotionAxis.B);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A) is not null && ActiveOf(snapshot, ManualMotionAxis.B) is not null);

        // Two active snapshots in stable A then B order, no duplicate axis.
        var two = harness.Snapshot.ActiveOperations;
        Assert.Equal(2, two.Count);
        Assert.Equal(ManualMotionAxis.A, two[0].Axis);
        Assert.Equal(ManualMotionAxis.B, two[1].Axis);
        Assert.Equal(new[] { ManualMotionAxis.A, ManualMotionAxis.B }, two.Select(operation => operation.Axis).ToArray());
        var idB = two[1].Id;

        // Completing A removes only A; B keeps its own ID and lifecycle.
        harness.Session.RequestStop(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null && ActiveOf(snapshot, ManualMotionAxis.B) is not null);
        var remaining = Assert.Single(harness.Snapshot.ActiveOperations);
        Assert.Equal(ManualMotionAxis.B, remaining.Axis);
        Assert.Equal(idB, remaining.Id);
        Assert.Equal(ManualMotionOperationKind.Home, remaining.Kind);

        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is null, maximumSteps: 1200);

        // The C/D pair publishes C then D with no fabricated placeholders.
        harness.HomeAxis(ManualMotionAxis.C);
        harness.HomeAxis(ManualMotionAxis.D);
        harness.Session.RequestMove(ManualMotionAxis.C, 1500);
        harness.Session.RequestMove(ManualMotionAxis.D, -1500);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.C) is not null && ActiveOf(snapshot, ManualMotionAxis.D) is not null);
        var cd = harness.Snapshot.ActiveOperations;
        Assert.Equal(new[] { ManualMotionAxis.C, ManualMotionAxis.D }, cd.Select(operation => operation.Axis).ToArray());
    }

    [Fact]
    public void Stop_Is_Per_Axis_And_Never_Stops_Another_Axis()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.A);
        harness.HomeAxis(ManualMotionAxis.B);
        var beforeStops = harness.Port.ExecutedCommands.Count;

        harness.Session.RequestMove(ManualMotionAxis.A, -5000);
        harness.Session.RequestMove(ManualMotionAxis.B, 5000);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);
        var idB = ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Id;

        // Stop A stops only Axis A; Axis B continues with the same operation.
        harness.Session.RequestStop(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null);
        Assert.Equal(ManualMotionCodes.MoveAbortedByOperator, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(idB, ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Id);
        Assert.Equal(ManualMotionOperationPhase.Running, ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Phase);

        // Stop B later affects only B.
        harness.Session.RequestStop(ManualMotionAxis.B);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is null);
        Assert.Equal(ManualMotionCodes.MoveAbortedByOperator, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);

        // A standalone Stop on the idle Axis C stays typed and serialized, and never touches D.
        var stopStateD = harness.Axis(ManualMotionAxis.D).StopState;
        harness.Session.RequestStop(ManualMotionAxis.C);
        harness.DriveUntil(_ => harness.Axis(ManualMotionAxis.C).StopState == ManualAxisStopState.Issued);

        var stops = harness.Port.ExecutedCommands.Skip(beforeStops).Where(command => command.Kind == ManualMotionPortCommandKind.Stop).ToList();
        Assert.Equal(
            new[] { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C },
            stops.Select(command => command.Axis).ToArray());
        Assert.Equal(stopStateD, harness.Axis(ManualMotionAxis.D).StopState);
    }

    [Fact]
    public void Two_Axes_Awaiting_Stopped_Alternate_Status_Reads_Fairly_And_Time_Out_Without_Fabricated_State()
    {
        var harness = new RecordingHarness(DualTrialLikeProfile());
        harness.OpenSession();
        harness.Drive(8);
        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 401);
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 402);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);

        harness.IgnoreStopFor.Add(ManualMotionAxis.A);
        harness.IgnoreStopFor.Add(ManualMotionAxis.B);
        harness.Session.RequestStop(ManualMotionAxis.A);
        harness.Session.RequestStop(ManualMotionAxis.B);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.AwaitingStopped
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.AwaitingStopped);

        var readsAtBoth = harness.Reads.Count;
        harness.Drive(40);
        var after = harness.Reads.Skip(readsAtBoth).ToList();

        // Status evidence has sole priority for both axes: no TP, RP or output read displaces it,
        // and the two axes alternate fairly so neither starves.
        Assert.NotEmpty(after);
        Assert.All(after, read => Assert.Equal(ManualMotionReadKind.StatusWord, read.Kind));
        var statusA = after.Count(read => read.Axis == ManualMotionAxis.A);
        var statusB = after.Count(read => read.Axis == ManualMotionAxis.B);
        Assert.True(statusA >= 2 && statusB >= 2, "both axes must receive Stop-confirmation Status reads");
        Assert.True(Math.Abs(statusA - statusB) <= 1, "the Status reads must alternate fairly");

        // Neither axis is ever confirmed stopped without an observation: the timeout ends each axis
        // truthfully, without an automatic brake apply and without fabricating the other axis state.
        harness.DriveUntil(
            snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null && ActiveOf(snapshot, ManualMotionAxis.B) is null,
            maximumSteps: 1200);
        Assert.Equal(ManualMotionCodes.StopNotObservedWithinTimeout, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(ManualMotionCodes.StopNotObservedWithinTimeout, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.DoesNotContain(harness.Port.Executed, command => command.Kind == ManualMotionPortCommandKind.BrakeOutputClear);
        Assert.False(harness.Axis(ManualMotionAxis.A).StopConfirmed);
        Assert.False(harness.Axis(ManualMotionAxis.B).StopConfirmed);
    }

    [Fact]
    public void Brake_Apply_Happens_Only_On_The_Axis_Observed_Inactive_And_Timeout_Leaves_Other_Axes_Alone()
    {
        var harness = new RecordingHarness(DualTrialLikeProfile());
        harness.OpenSession();
        harness.Drive(8);
        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 411);
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 412);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);

        // Axis B keeps moving after its Stop; Axis A stops normally.
        harness.IgnoreStopFor.Add(ManualMotionAxis.B);
        harness.Session.RequestStop(ManualMotionAxis.A);
        harness.Session.RequestStop(ManualMotionAxis.B);

        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null);
        Assert.Equal(ManualMotionCodes.JogStoppedByOperator, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Contains(
            harness.Port.Executed,
            command => command.Kind == ManualMotionPortCommandKind.BrakeOutputClear && command.BrakeOutput == 1);
        Assert.DoesNotContain(
            harness.Port.Executed,
            command => command.Kind == ManualMotionPortCommandKind.BrakeOutputClear && command.BrakeOutput == 2);
        Assert.Equal(ManualMotionOperationPhase.AwaitingStopped, ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Phase);

        // The Stop timeout on B is B's own truthful outcome: no brake apply and no fabricated stop.
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is null, maximumSteps: 400);
        Assert.Equal(ManualMotionCodes.StopNotObservedWithinTimeout, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.DoesNotContain(
            harness.Port.Executed,
            command => command.Kind == ManualMotionPortCommandKind.BrakeOutputClear && command.BrakeOutput == 2);
        Assert.False(harness.Axis(ManualMotionAxis.B).StopConfirmed);
    }

    [Fact]
    public void Separate_Jog_Heartbeats_Keep_Only_Their_Own_Axis_Alive()
    {
        var source = ManualMotionProfile.CreateSimulationProfile();
        var harness = new Harness(profile: ProfileWith(source, jogHeartbeatTimeoutMilliseconds: 800));
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 501);
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 502);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);
        var idB = ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Id;

        // Heartbeats for B only: A's heartbeat expires on its own axis while B stays alive.
        for (var index = 0; index < 60 && ActiveOf(harness.Snapshot, ManualMotionAxis.A) is not null; index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 502);
            harness.Drive(1, 50);
        }

        Assert.Equal(ManualMotionCodes.JogStoppedHeartbeatExpired, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(idB, ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Id);
        Assert.Equal(ManualMotionOperationPhase.Running, ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Phase);

        harness.Session.RequestJogRelease(ManualMotionAxis.B, 502, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is null);
        Assert.Equal(ManualMotionCodes.JogStoppedOnRelease, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
    }

    [Fact]
    public void A_Stale_Or_Wrong_Press_Id_Is_Refused_Per_Axis_And_Keeps_Nothing_Alive()
    {
        var source = ManualMotionProfile.CreateSimulationProfile();
        var harness = new Harness(profile: ProfileWith(source, jogHeartbeatTimeoutMilliseconds: 800));
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 511);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running);

        // Heartbeats with a wrong press ID never refresh the axis.
        for (var index = 0; index < 40 && ActiveOf(harness.Snapshot, ManualMotionAxis.A) is not null; index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.A, 999);
            harness.Drive(1, 50);
        }

        Assert.Equal(ManualMotionCodes.JogStoppedHeartbeatExpired, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
    }

    [Fact]
    public void Release_Ends_Only_Its_Own_Axis_And_Capture_Loss_Ends_That_Axis_Safely()
    {
        var harness = new Harness(profile: ProfileWith(ManualMotionProfile.CreateSimulationProfile(), jogHeartbeatTimeoutMilliseconds: 5000));
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 521);
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 522);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);
        var idB = ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Id;

        // Releasing A stops only A; B keeps its own operation untouched.
        harness.Session.RequestJogRelease(ManualMotionAxis.A, 521, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.A) is null);
        Assert.Equal(ManualMotionCodes.JogStoppedOnRelease, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(idB, ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Id);
        Assert.Equal(ManualMotionOperationPhase.Running, ActiveOf(harness.Snapshot, ManualMotionAxis.B)?.Phase);

        // Capture loss on B ends B safely through the supervised Stop path.
        harness.Session.RequestJogRelease(ManualMotionAxis.B, 522, ManualMotionJogEndReason.CaptureLost);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is null);
        Assert.Equal(ManualMotionCodes.JogStoppedCaptureLost, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
    }

    [Fact]
    public void Two_Active_Axes_Keep_Fresh_Complete_Cycle_Status_First_Observations()
    {
        var harness = new RecordingHarness(DualTrialLikeProfile());
        harness.OpenSession();
        harness.Drive(8);
        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 601);
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 602);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);
        var readsAtRunning = harness.Reads.Count;

        for (var step = 0; step < 60; step++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.A, 601);
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 602);
            var callsBefore = harness.Reads.Count + harness.Port.Executed.Count;
            harness.StepOnce();

            // One Step makes at most one port call, and both axes keep every reading fresh.
            Assert.True(
                harness.Reads.Count + harness.Port.Executed.Count - callsBefore <= 1,
                "more than one port call in a single Step");
            Assert.True(
                harness.Axis(ManualMotionAxis.A).StatusWord.Fresh
                && harness.Axis(ManualMotionAxis.A).PositionTp.Fresh
                && harness.Axis(ManualMotionAxis.A).PositionRp.Fresh
                && harness.Axis(ManualMotionAxis.A).BrakeOutput.Fresh,
                "Axis A observations went stale while two axes were active");
            Assert.True(
                harness.Axis(ManualMotionAxis.B).StatusWord.Fresh
                && harness.Axis(ManualMotionAxis.B).PositionTp.Fresh
                && harness.Axis(ManualMotionAxis.B).PositionRp.Fresh
                && harness.Axis(ManualMotionAxis.B).BrakeOutput.Fresh,
                "Axis B observations went stale while two axes were active");
        }

        // Per-axis complete cycles stay Status-first: each axis reads Status, TP, RP, output in order.
        AssertCycleKinds(harness.Reads.Skip(readsAtRunning).Where(read => read.Axis == ManualMotionAxis.A).Select(read => read.Kind));
        AssertCycleKinds(harness.Reads.Skip(readsAtRunning).Where(read => read.Axis == ManualMotionAxis.B).Select(read => read.Kind));

        // A refused read stays per-value and truthful: nothing is fabricated on either axis, and
        // the old value ages out of the freshness window honestly while Status stays current.
        harness.FailReadKinds.Add(ManualMotionReadKind.PositionTp);
        harness.Drive(40);
        Assert.False(harness.Axis(ManualMotionAxis.A).PositionTp.Fresh);
        Assert.Equal("FAKE_REFUSED", harness.Axis(ManualMotionAxis.A).PositionTp.LastRefusalCode);
        Assert.True(harness.Axis(ManualMotionAxis.A).StatusWord.Fresh);
        Assert.True(harness.Axis(ManualMotionAxis.B).StatusWord.Fresh);
    }

    [Fact]
    public void Four_Active_Axes_Receive_Fair_Fresh_Observations_Within_The_Freshness_Window()
    {
        // Worst supported combination: four active operations with the approved 100 ms cycle and
        // 300 ms freshness values. The deterministic steady-state schedule serves one read per Step
        // in complete per-axis Status-first cycles, so a full set of sixteen reads rotates every
        // 160 ms — inside the freshness window; only urgent command work may briefly preempt reads.
        var harness = new RecordingHarness(QuadTrialLikeProfile());
        harness.OpenSession();
        harness.Drive(20);
        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 701);
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 702);
        harness.Session.RequestJogStart(ManualMotionAxis.C, ManualMotionJogDirection.Positive, 703);
        harness.Session.RequestJogStart(ManualMotionAxis.D, ManualMotionJogDirection.Positive, 704);
        harness.DriveUntil(
            snapshot => snapshot.ActiveOperations.Count == 4
                && snapshot.ActiveOperations.All(operation => operation.Phase == ManualMotionOperationPhase.Running),
            maximumSteps: 400);

        // The measurement window opens only after the first COMPLETE fresh observation set of the
        // four active axes: the post-Begin rebases and the startup command calls may legitimately
        // delay reads, so the window never counts never-read or pre-rebase values as stale.
        for (var step = 0; step < 40 && !QuadObservationsFresh(harness); step++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.A, 701);
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 702);
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.C, 703);
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.D, 704);
            harness.StepOnce();
        }
        Assert.True(QuadObservationsFresh(harness), "the first complete fresh four-axis observation set never arrived");
        var readsAtRunning = harness.Reads.Count;

        for (var step = 0; step < 60; step++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.A, 701);
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 702);
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.C, 703);
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.D, 704);
            var callsBefore = harness.Reads.Count + harness.Port.Executed.Count;
            harness.StepOnce();
            Assert.True(harness.Reads.Count + harness.Port.Executed.Count - callsBefore <= 1);

            foreach (var axis in new[] { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D })
            {
                Assert.True(
                    harness.Axis(axis).StatusWord.Fresh
                    && harness.Axis(axis).PositionTp.Fresh
                    && harness.Axis(axis).PositionRp.Fresh
                    && harness.Axis(axis).BrakeOutput.Fresh,
                    string.Concat("stale observations on Axis ", axis.ToString(), " with four active axes"));
            }
        }

        // Fair rotation: every axis receives complete cycles, and no axis starves another.
        var readsPerAxis = new[] { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D }
            .Select(axis => harness.Reads.Skip(readsAtRunning).Count(read => read.Axis == axis))
            .ToArray();
        Assert.All(readsPerAxis, count => Assert.True(count >= 8, "an axis starved during four active operations"));
        Assert.True(readsPerAxis.Max() - readsPerAxis.Min() <= 2, "observation work is not fairly shared");
    }

    [Fact]
    public void Post_Begin_Rebase_Applies_Only_To_The_Axis_That_Began()
    {
        var harness = new RecordingHarness(DualTrialLikeProfile());
        harness.OpenSession();
        harness.Drive(8);

        // Axis B runs first and settles into steady complete cycles.
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 801);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);
        harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 801);
        harness.Drive(20);
        var readsBeforeA = harness.Reads.Count;

        // Axis A starts and begins motion; its own cycle rebases Status-first after its Begin.
        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 802);
        for (var step = 0; step < 80 && ActiveOf(harness.Snapshot, ManualMotionAxis.A)?.Phase != ManualMotionOperationPhase.Running; step++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 801);
            harness.StepOnce();
        }
        Assert.Equal(ManualMotionOperationPhase.Running, ActiveOf(harness.Snapshot, ManualMotionAxis.A)?.Phase);
        harness.Drive(40);

        // Axis B kept its observation progress: its stream never restarts a cycle early (a rebase
        // would insert an out-of-order Status read), so A's Begin discarded only A's own remainder.
        var bKinds = harness.Reads.Skip(readsBeforeA).Where(read => read.Axis == ManualMotionAxis.B).Select(read => read.Kind).ToList();
        AssertCycleKinds(bKinds);

        // Axis A's stream is Status-first after its own rebase.
        var aKinds = harness.Reads.Skip(readsBeforeA).Where(read => read.Axis == ManualMotionAxis.A).Select(read => read.Kind).ToList();
        Assert.NotEmpty(aKinds);
        Assert.Equal(ManualMotionReadKind.StatusWord, aKinds[0]);
    }

    [Fact]
    public void Close_Stops_Every_Active_Motion_Axis_Then_Closes_The_Port_Exactly_Once()
    {
        var harness = new Harness();
        harness.OpenAndObserve();
        harness.HomeAxis(ManualMotionAxis.A);
        harness.HomeAxis(ManualMotionAxis.B);

        harness.Session.RequestMove(ManualMotionAxis.A, -5000);
        harness.Session.RequestMove(ManualMotionAxis.B, 5000);
        harness.DriveUntil(snapshot =>
            ActiveOf(snapshot, ManualMotionAxis.A)?.Phase == ManualMotionOperationPhase.Running
            && ActiveOf(snapshot, ManualMotionAxis.B)?.Phase == ManualMotionOperationPhase.Running);

        // The setup Homes dispatched their own supervised Stops; the close phase is measured on its
        // own so exactly one close Stop per active axis is pinned without erasing the Home lifecycle.
        var stopBaseline = harness.Port.ExecutedCommands.Count;
        harness.Session.RequestClose();
        harness.DriveUntil(snapshot => snapshot.State == ManualMotionSessionState.Closed, maximumSteps: 400);

        Assert.Equal(ManualMotionCodes.MoveAbortedSessionClosing, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(ManualMotionCodes.MoveAbortedSessionClosing, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        var stops = harness.Port.ExecutedCommands.Skip(stopBaseline).Where(command => command.Kind == ManualMotionPortCommandKind.Stop).ToList();
        Assert.Single(stops, command => command.Axis == ManualMotionAxis.A);
        Assert.Single(stops, command => command.Axis == ManualMotionAxis.B);

        // Each axis brake is applied only after that same axis was observed inactive.
        Assert.Contains(
            harness.Port.ExecutedCommands,
            command => command.Kind == ManualMotionPortCommandKind.BrakeOutputClear && command.BrakeOutput == 1);
        Assert.Contains(
            harness.Port.ExecutedCommands,
            command => command.Kind == ManualMotionPortCommandKind.BrakeOutputClear && command.BrakeOutput == 2);

        Assert.Equal(1, harness.Port.OpenCount);
        Assert.Equal(1, harness.Port.CloseCount);
        Assert.True(harness.Snapshot.IsTerminal);
        Assert.Empty(harness.Snapshot.ActiveOperations);

        harness.Drive(3);
        Assert.Equal(1, harness.Port.CloseCount);
    }

    [Fact]
    public void Close_Handles_All_Four_Active_Axes_In_Deterministic_Order_Without_Duplicate_Stops()
    {
        var source = ManualMotionProfile.CreateSimulationProfile();
        var harness = new Harness(profile: ProfileWith(source, jogHeartbeatTimeoutMilliseconds: 5000));
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 901);
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 902);
        harness.Session.RequestJogStart(ManualMotionAxis.C, ManualMotionJogDirection.Positive, 903);
        harness.Session.RequestJogStart(ManualMotionAxis.D, ManualMotionJogDirection.Positive, 904);
        harness.DriveUntil(
            snapshot => snapshot.ActiveOperations.Count == 4
                && snapshot.ActiveOperations.All(operation => operation.Phase == ManualMotionOperationPhase.Running),
            maximumSteps: 400);

        harness.Session.RequestClose();
        harness.DriveUntil(snapshot => snapshot.State == ManualMotionSessionState.Closed, maximumSteps: 600);

        Assert.Equal(ManualMotionCodes.JogStoppedSessionClosing, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(ManualMotionCodes.JogStoppedSessionClosing, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.Equal(ManualMotionCodes.JogStoppedSessionClosing, harness.Axis(ManualMotionAxis.C).LastOutcomeCode);
        Assert.Equal(ManualMotionCodes.JogStoppedSessionClosing, harness.Axis(ManualMotionAxis.D).LastOutcomeCode);

        // Exactly one typed Stop per axis, dispatched one at a time in fair A/B/C/D order.
        var stopAxes = harness.Port.ExecutedCommands
            .Where(command => command.Kind == ManualMotionPortCommandKind.Stop)
            .Select(command => command.Axis)
            .ToArray();
        Assert.Equal(new[] { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D }, stopAxes);

        Assert.Equal(1, harness.Port.OpenCount);
        Assert.Equal(1, harness.Port.CloseCount);
        Assert.True(harness.Snapshot.IsTerminal);
        Assert.Empty(harness.Snapshot.ActiveOperations);
    }

    [Fact]
    public void A_Local_Operation_Refusal_Never_Faults_The_Session_Or_Another_Axis()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        // A command-specific precondition failure on Axis A is local to A.
        harness.Session.RequestMove(ManualMotionAxis.A, -5000);
        harness.Drive(3);
        Assert.Equal(ManualMotionCodes.MoveRequiresHomed, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(ManualMotionSessionState.Ready, harness.Snapshot.State);
        Assert.DoesNotContain(harness.Snapshot.Log, entry => entry.Code == ManualMotionCodes.SessionFaulted);

        // Axis B keeps full use of its own operations.
        harness.Session.RequestBrakeRelease(ManualMotionAxis.B);
        harness.DriveUntil(snapshot => ActiveOf(snapshot, ManualMotionAxis.B) is null
            && snapshot.Axes[1].BrakeCommandState == ManualMotionBrakeCommandState.ReleaseCommanded);
        Assert.Equal(ManualMotionCodes.BrakeReleaseCommanded, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
    }

    [Fact]
    public void A_Communication_Fault_Faults_The_Session_Once_And_Terminates_Every_Active_Operation()
    {
        var harness = new Harness(new ManualMotionSimulationOptions
        {
            FaultOnCommand = ManualMotionPortCommandKind.Begin,
        });
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 951);
        harness.Session.RequestJogStart(ManualMotionAxis.B, ManualMotionJogDirection.Positive, 952);
        harness.DriveUntil(snapshot => snapshot.State == ManualMotionSessionState.Faulted, maximumSteps: 100);

        // Every active operation is terminally represented with the exact fault code, and the
        // session faults once even though two operations were active.
        Assert.Empty(harness.Snapshot.ActiveOperations);
        Assert.Equal(ManualMotionCodes.CommunicationFault, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(ManualMotionCodes.CommunicationFault, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.Single(harness.Snapshot.Log, entry => entry.Code == ManualMotionCodes.SessionFaulted);

        // Cleanup is deterministic and truthful: one protective Stop for the axis that began motion,
        // none for the axis that never began, no fabricated stop state and no automatic brake apply.
        Assert.Single(
            harness.Port.ExecutedCommands,
            command =>
                command.Kind == ManualMotionPortCommandKind.Stop
                && command.Axis == ManualMotionAxis.A);
        Assert.DoesNotContain(
            harness.Port.ExecutedCommands,
            command =>
                command.Kind == ManualMotionPortCommandKind.Stop
                && command.Axis == ManualMotionAxis.B);
        Assert.False(harness.Axis(ManualMotionAxis.A).StopConfirmed);
        Assert.False(harness.Axis(ManualMotionAxis.B).StopConfirmed);
        Assert.DoesNotContain(harness.Port.ExecutedCommands, command => command.Kind == ManualMotionPortCommandKind.BrakeOutputClear);

        // The deterministic close remains possible and closes the port once.
        harness.Session.RequestClose();
        harness.DriveUntil(snapshot => snapshot.State == ManualMotionSessionState.Closed, maximumSteps: 100);
        Assert.Equal(1, harness.Port.OpenCount);
        Assert.Equal(1, harness.Port.CloseCount);
    }

    [Fact]
    public void A_Fault_During_Stop_Supervision_Never_Repeats_The_Already_Attempted_Stop()
    {
        var clock = new TestClock(Instant);
        var stopAttempted = false;
        var port = new FakePort
        {
            ExecuteHandler = command =>
            {
                if (command.Kind == ManualMotionPortCommandKind.Stop)
                {
                    stopAttempted = true;
                }

                return new ManualMotionPortCommandResult(true, false, true, "FAKE_DISPATCHED", 1);
            },
            ReadHandler = read => stopAttempted
                ? new ManualMotionPortReadResult(false, true, 0m, "FAKE_FAULT", 1)
                : new ManualMotionPortReadResult(
                    true,
                    false,
                    read.Kind == ManualMotionReadKind.StatusWord ? 128m : 0m,
                    "FAKE_READ",
                    1),
        };
        var session = new ManualMotionSession(
            port,
            ManualMotionProfileResult.FromProfile(ManualMotionProfile.CreateSimulationProfile()),
            clock);

        session.Step();
        for (var index = 0; index < 16; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }

        session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 971);
        for (var index = 0;
             index < 40 && ActiveOf(session.Snapshot, ManualMotionAxis.A)?.Phase != ManualMotionOperationPhase.Running;
             index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }
        Assert.Equal(ManualMotionOperationPhase.Running, ActiveOf(session.Snapshot, ManualMotionAxis.A)?.Phase);

        // The supervised Stop dispatches once; the next observation then faults the shared
        // connection while the axis is still being supervised.
        session.RequestJogRelease(ManualMotionAxis.A, 971, ManualMotionJogEndReason.Released);
        for (var index = 0; index < 40 && session.Snapshot.State != ManualMotionSessionState.Faulted; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            session.Step();
        }

        Assert.Equal(ManualMotionSessionState.Faulted, session.Snapshot.State);
        Assert.Single(port.Executed, command => command.Kind == ManualMotionPortCommandKind.Stop);
        Assert.False(session.Snapshot.Axes[0].StopConfirmed);
    }

    private static bool QuadObservationsFresh(RecordingHarness harness)
    {
        foreach (var axis in new[] { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D })
        {
            if (!harness.Axis(axis).StatusWord.Fresh
                || !harness.Axis(axis).PositionTp.Fresh
                || !harness.Axis(axis).PositionRp.Fresh
                || !harness.Axis(axis).BrakeOutput.Fresh)
            {
                return false;
            }
        }

        return true;
    }

    private static void AssertCycleKinds(IEnumerable<ManualMotionReadKind> kinds)
    {
        // The kinds must be a contiguous slice of the repeating complete cycle Status, TP, RP,
        // brake output. A rebased or restarted cycle would break the repetition.
        var expected = new[]
        {
            ManualMotionReadKind.StatusWord,
            ManualMotionReadKind.PositionTp,
            ManualMotionReadKind.PositionRp,
            ManualMotionReadKind.BrakeOutputState,
        };
        var list = kinds.ToList();
        Assert.NotEmpty(list);
        var offset = Array.IndexOf(expected, list[0]);
        Assert.True(offset >= 0, "unexpected read kind in the cycle stream");
        for (var index = 0; index < list.Count; index++)
        {
            Assert.Equal(expected[(offset + index) % expected.Length], list[index]);
        }
    }
}
