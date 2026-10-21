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
            DriveUntil(snapshot => snapshot.ActiveOperation is null && snapshot.Axes[(int)axis - 1].HomeState == ManualMotionHomeState.Homed);
        }
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
    /// A FakePort session that records every read (with its fake-clock time) and models just enough
    /// controller behaviour for the observation-cycle tests: Begin makes the status motion bit
    /// active, Stop makes it inactive, and the brake-output read follows the brake commands.
    /// </summary>
    private sealed class RecordingHarness
    {
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
                        ManualMotionReadKind.StatusWord => Moving ? 128m : 0m,
                        ManualMotionReadKind.BrakeOutputState => BrakeReleased ? 1m : 0m,
                        _ => PositionValue,
                    };
                    return new ManualMotionPortReadResult(true, false, value, "FAKE_READ", 1);
                },
                ExecuteHandler = command =>
                {
                    if (command.Kind == ManualMotionPortCommandKind.Begin)
                    {
                        Moving = true;
                    }
                    else if (command.Kind == ManualMotionPortCommandKind.Stop)
                    {
                        Moving = false;
                        ReadsAtLastStop = Reads.Count;
                    }
                    else if (command.Kind == ManualMotionPortCommandKind.BrakeOutputSet)
                    {
                        BrakeReleased = true;
                    }
                    else if (command.Kind == ManualMotionPortCommandKind.BrakeOutputClear)
                    {
                        BrakeReleased = false;
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

        internal bool Moving { get; private set; }

        internal bool BrakeReleased { get; private set; }

        internal int ReadsAtLastStop { get; private set; } = -1;

        internal decimal PositionValue { get; set; }

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
        Assert.True(harness.Axis(ManualMotionAxis.A).AllowedByProfile);
        Assert.True(harness.Axis(ManualMotionAxis.A).PositionTp.Fresh);
        Assert.Equal(12000m, harness.Axis(ManualMotionAxis.A).PositionTp.Value);
        Assert.Equal(-8000m, harness.Axis(ManualMotionAxis.B).PositionTp.Value);
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
            && snapshot.Axes[0].BrakeCommandState == ManualMotionBrakeCommandState.ReleaseCommanded);

        Assert.Equal(ManualMotionCodes.BrakeReleaseCommanded, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        var release = Assert.Single(harness.Port.ExecutedCommands);
        Assert.Equal(ManualMotionPortCommandKind.BrakeOutputSet, release.Kind);
        Assert.Equal(1, release.BrakeOutput);

        harness.Session.RequestBrakeApply(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
            && snapshot.Axes[0].BrakeCommandState == ManualMotionBrakeCommandState.ApplyCommanded);

        Assert.Equal(ManualMotionCodes.BrakeApplyCommanded, harness.Axis(ManualMotionAxis.A).LastOutcomeCode);
        Assert.Equal(
            new[] { ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionPortCommandKind.BrakeOutputClear },
            harness.Port.ExecutedCommands.Select(command => command.Kind).ToArray());
    }

    [Fact]
    public void A_Second_Operation_Is_Refused_While_One_Is_Active()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 41);
        harness.Drive(2);
        Assert.NotNull(harness.Snapshot.ActiveOperation);

        harness.Session.RequestBrakeRelease(ManualMotionAxis.B);
        harness.Drive(2);

        Assert.Equal(ManualMotionCodes.OperationInProgress, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        Assert.Equal(ManualMotionAxis.A, harness.Snapshot.ActiveOperation?.Axis);

        harness.Session.RequestJogRelease(ManualMotionAxis.A, 41, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null);
    }

    [Fact]
    public void Jog_Positive_Then_Release_Stops_And_Applies_The_Brake_In_Order()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 7);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation?.Phase == ManualMotionOperationPhase.Running);
        for (var index = 0; index < 6; index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.A, 7);
            harness.Drive(1, 50);
        }

        harness.Session.RequestJogRelease(ManualMotionAxis.A, 7, ManualMotionJogEndReason.Released);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null);

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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation?.Phase == ManualMotionOperationPhase.Running);
        harness.Session.RequestJogHeartbeat(ManualMotionAxis.B, 9);
        harness.Drive(2, 50);

        harness.Session.RequestJogRelease(ManualMotionAxis.B, 9, ManualMotionJogEndReason.CaptureLost);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null);

        Assert.Equal(ManualMotionCodes.JogStoppedCaptureLost, harness.Axis(ManualMotionAxis.B).LastOutcomeCode);
        var jogSpeed = harness.Port.ExecutedCommands.Single(command => command.Kind == ManualMotionPortCommandKind.JogSpeed);
        Assert.Equal(-2000, jogSpeed.Operand);
        Assert.True(harness.Axis(ManualMotionAxis.B).PositionTp.Value < -8000m);
    }

    [Fact]
    public void Jog_Heartbeat_Expiry_Stops_Without_A_Release()
    {
        var harness = new Harness();
        harness.OpenAndObserve();

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 11);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation?.Phase == ManualMotionOperationPhase.Running);

        // No further heartbeat: the configured heartbeat timeout ends the Jog through the Stop path.
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null, maximumSteps: 200, millisecondsPerStep: 50);

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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation?.Phase == ManualMotionOperationPhase.Running);
        for (var index = 0; index < 300 && harness.Snapshot.ActiveOperation is not null; index++)
        {
            harness.Session.RequestJogHeartbeat(ManualMotionAxis.A, 13);
            harness.Drive(1, 25);
        }

        // Reaching idle while heartbeats continue can only be the maximum-hold stop (or a later
        // settling step); drive out any brake-settling remainder before asserting the outcome.
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null);
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation?.Phase == ManualMotionOperationPhase.Running);
        harness.Session.RequestStop(ManualMotionAxis.A);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null);

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

        // Wait until the queued Home has actually begun; ActiveOperation is already null before it.
        harness.DriveUntil(snapshot => snapshot.ActiveOperation?.Phase == ManualMotionOperationPhase.Running);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null, maximumSteps: 900, millisecondsPerStep: 100);

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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation?.Phase == ManualMotionOperationPhase.Running);

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
    public void A_Fresh_Other_Axis_Moving_Observation_Refuses_New_Motion()
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

        Assert.Equal(ManualMotionCodes.OtherAxisMovingObserved, session.Snapshot.Axes[0].LastOutcomeCode);
        Assert.Empty(port.Executed);
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
    public void Two_Allowed_Axes_Cycle_In_Deterministic_Order_With_Status_First_Per_Axis()
    {
        var harness = new RecordingHarness(ManualMotionProfile.CreateSimulationProfile());
        harness.OpenSession();
        harness.Drive(8, 25);

        Assert.Equal(
            new[]
            {
                new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionTp, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionRp, ManualMotionAxis.A, 0),
                new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.A, 1),
                new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.B, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionTp, ManualMotionAxis.B, 0),
                new ManualMotionPortRead(ManualMotionReadKind.PositionRp, ManualMotionAxis.B, 0),
                new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.B, 2),
            },
            harness.Reads.Take(8).ToArray());
    }

    [Fact]
    public void Jog_At_The_Approved_Timing_Never_Hits_A_Stale_Status_Window()
    {
        var harness = new RecordingHarness(TrialLikeProfile());
        harness.OpenSession();
        harness.Drive(50);

        harness.Session.RequestJogStart(ManualMotionAxis.A, ManualMotionJogDirection.Positive, 71);
        harness.DriveUntil(snapshot => snapshot.ActiveOperation?.Phase == ManualMotionOperationPhase.Running);

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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null);

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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
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
        harness.DriveUntil(snapshot => snapshot.ActiveOperation is null
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
}
