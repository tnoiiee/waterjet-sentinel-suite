using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Unit behaviour of the deterministic simulated Manual Motion port. The port performs no native
/// load, connection, socket or hardware output; these tests use an injected test clock only and
/// treat every value as synthetic simulation input, never as hardware evidence.
/// </summary>
public sealed class ManualMotionSimulatedPortTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    private static ManualMotionPortCommand Command(
        ManualMotionPortCommandKind kind,
        ManualMotionAxis axis = ManualMotionAxis.A,
        int operand = 0,
        int brakeOutput = 0) => new(kind, axis, operand, brakeOutput);

    private static decimal ReadTp(ManualMotionSimulatedPort port, ManualMotionAxis axis = ManualMotionAxis.A)
    {
        var result = port.Read(new ManualMotionPortRead(ManualMotionReadKind.PositionTp, axis, 0));
        Assert.True(result.Succeeded);
        return result.RawValue;
    }

    private static ManualMotionProfile ClearOutputProfile()
    {
        ManualMotionAxisProfile Axis(int brakeOutput) => new()
        {
            BrakeOutput = brakeOutput,
            BrakeReleaseAction = ManualMotionBrakeReleaseAction.ClearOutput,
            BrakeSettleMilliseconds = 100,
            JogSpeed = 2000,
            HomeSpeed = 1500,
            HomeTimeoutMilliseconds = 30000,
            MoveAcceleration = 20000,
            MoveSpeed = 4000,
            MoveDeceleration = 20000,
            MoveMinimumTarget = -20000,
            MoveMaximumTarget = 20000,
            PositionTolerance = 2,
            MotionTimeoutMilliseconds = 20000,
            StopTimeoutMilliseconds = 2000,
            MotionStatusBit = 6,
            MotionActiveValue = 1,
            HomeStatusBit = 3,
            HomeActiveValue = 1,
            DualZeroPermitted = true,
        };

        return new ManualMotionProfile
        {
            Source = ManualMotionProfileSource.SimulationBuiltIn,
            ControllerId = "WSB1",
            ObservationIntervalMilliseconds = 20,
            ObservationFreshnessMilliseconds = 600,
            JogHeartbeatTimeoutMilliseconds = 500,
            JogMaximumHoldMilliseconds = 15000,
            AxisA = Axis(1),
            AxisB = Axis(2),
        };
    }

    [Fact]
    public void The_Port_Opens_Once_And_Refuses_Work_When_Not_Open()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());

        var beforeOpen = port.Execute(Command(ManualMotionPortCommandKind.Stop));
        Assert.False(beforeOpen.Accepted);
        Assert.Equal(ManualMotionSimulatedPort.CodeNotOpen, beforeOpen.Code);

        Assert.True(port.Open().Opened);
        var secondOpen = port.Open();
        Assert.False(secondOpen.Opened);
        Assert.Equal(ManualMotionSimulatedPort.CodeOpenAlreadyPerformed, secondOpen.Code);
        Assert.Equal(2, port.OpenCount);

        Assert.True(port.Close().Attempted);
        var afterClose = port.Execute(Command(ManualMotionPortCommandKind.Stop));
        Assert.False(afterClose.Accepted);
        Assert.Equal(ManualMotionSimulatedPort.CodeNotOpen, afterClose.Code);
    }

    [Fact]
    public void Motion_Does_Not_Progress_Until_The_Brake_Is_Released_Set_Output_Polarity()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();

        port.Execute(Command(ManualMotionPortCommandKind.JogSpeed, operand: 2000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(12000m, ReadTp(port));

        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, brakeOutput: 1));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(14000m, ReadTp(port));
    }

    [Fact]
    public void Motion_Does_Not_Progress_Until_The_Brake_Is_Released_Clear_Output_Polarity()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ClearOutputProfile());
        port.Open();

        // For the clear-output release polarity the brake APPLY state is the SET output, so a
        // freshly opened port must already report the applied state, never a released brake.
        var brakeOutput = port.Read(new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.A, 1));
        Assert.True(brakeOutput.Succeeded);
        Assert.Equal(1m, brakeOutput.RawValue);

        port.Execute(Command(ManualMotionPortCommandKind.JogSpeed, operand: 2000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(12000m, ReadTp(port));

        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputClear, brakeOutput: 1));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(14000m, ReadTp(port));
    }

    [Fact]
    public void Stop_Halts_Simulated_Motion_Unless_Configured_Ignored()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, brakeOutput: 1));
        port.Execute(Command(ManualMotionPortCommandKind.JogSpeed, operand: 2000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(14000m, ReadTp(port));

        port.Execute(Command(ManualMotionPortCommandKind.Stop));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(14000m, ReadTp(port));

        var ignoringClock = new TestClock(Instant);
        var ignoring = new ManualMotionSimulatedPort(
            ignoringClock,
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { StopIgnored = true });
        ignoring.Open();
        ignoring.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, brakeOutput: 1));
        ignoring.Execute(Command(ManualMotionPortCommandKind.JogSpeed, operand: 2000));
        ignoring.Execute(Command(ManualMotionPortCommandKind.Begin));
        ignoringClock.Advance(TimeSpan.FromSeconds(1));
        ignoring.Execute(Command(ManualMotionPortCommandKind.Stop));
        ignoringClock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(ReadTp(ignoring) > 14000m);
    }

    [Fact]
    public void Home_Drive_Reaches_The_Simulated_Home_Switch()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.B, brakeOutput: 2));
        port.Execute(Command(ManualMotionPortCommandKind.HomeSpeed, ManualMotionAxis.B, operand: 1500));
        port.Execute(Command(ManualMotionPortCommandKind.Home, ManualMotionAxis.B));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.B));

        // Axis B starts at -8000 and homes toward the switch at 0 at 1500 counts per simulated second.
        clock.Advance(TimeSpan.FromSeconds(6));
        var status = port.Read(new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.B, 0));
        Assert.True(status.Succeeded);
        var homeBit = ((long)status.RawValue >> 3) & 1;
        Assert.Equal(1, homeBit);
    }

    [Fact]
    public void Zero_Definition_Changes_The_Reported_Position_Only_When_Not_Ignored()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();
        port.Execute(Command(ManualMotionPortCommandKind.DefinePositionZero));
        Assert.Equal(0m, ReadTp(port));

        var ignoring = new ManualMotionSimulatedPort(
            new TestClock(Instant),
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { ZeroDefinitionIgnored = true });
        ignoring.Open();
        ignoring.Execute(Command(ManualMotionPortCommandKind.DefinePositionZero));
        Assert.Equal(12000m, ReadTp(ignoring));
    }

    [Fact]
    public void Refused_And_Faulted_Commands_Are_Reported_Without_Effect()
    {
        var clock = new TestClock(Instant);
        var refusing = new ManualMotionSimulatedPort(
            clock,
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { RefuseCommand = ManualMotionPortCommandKind.Begin });
        refusing.Open();
        var refused = refusing.Execute(Command(ManualMotionPortCommandKind.Begin));
        Assert.False(refused.Accepted);
        Assert.False(refused.CommunicationFault);
        Assert.Equal(ManualMotionSimulatedPort.CodeRefused, refused.Code);

        var faulting = new ManualMotionSimulatedPort(
            new TestClock(Instant),
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { FaultOnCommand = ManualMotionPortCommandKind.Begin });
        faulting.Open();
        var faulted = faulting.Execute(Command(ManualMotionPortCommandKind.Begin));
        Assert.False(faulted.Accepted);
        Assert.True(faulted.CommunicationFault);
        Assert.Equal(ManualMotionSimulatedPort.CodeFault, faulted.Code);
    }

    [Fact]
    public void Faulted_And_Generation_Invalidated_Reads_Are_Explicit()
    {
        var faulting = new ManualMotionSimulatedPort(
            new TestClock(Instant),
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { FaultReadAfterCount = 1 });
        faulting.Open();
        Assert.True(faulting.Read(new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.A, 0)).Succeeded);
        var faulted = faulting.Read(new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.A, 0));
        Assert.False(faulted.Succeeded);
        Assert.True(faulted.CommunicationFault);
        Assert.Equal(0m, faulted.RawValue);

        var invalidating = new ManualMotionSimulatedPort(
            new TestClock(Instant),
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { InvalidateGenerationAfterCalls = 1 });
        var opened = invalidating.Open();
        Assert.True(opened.Opened);
        var read = invalidating.Read(new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.A, 0));
        Assert.NotEqual(opened.Generation, read.Generation);
    }

    [Fact]
    public void The_Simulation_Is_Deterministic_For_The_Same_Clock_And_Inputs()
    {
        decimal RunOnce()
        {
            var clock = new TestClock(Instant);
            var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
            port.Open();
            port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, brakeOutput: 1));
            port.Execute(Command(ManualMotionPortCommandKind.JogSpeed, operand: -2000));
            port.Execute(Command(ManualMotionPortCommandKind.Begin));
            clock.Advance(TimeSpan.FromMilliseconds(1375));
            port.Execute(Command(ManualMotionPortCommandKind.Stop));
            return ReadTp(port);
        }

        Assert.Equal(RunOnce(), RunOnce());
    }
}
