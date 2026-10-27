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

    [Fact]
    public void The_Simulated_Controller_Reports_The_Confirmed_Two_To_One_Pulse_Domain_Relationship()
    {
        var port = new ManualMotionSimulatedPort(new TestClock(Instant), ManualMotionProfile.CreateSimulationProfile());
        Assert.True(port.Open().Opened);

        foreach (var axis in new[] { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D })
        {
            var theoretical = port.Read(new ManualMotionPortRead(ManualMotionReadKind.PositionTp, axis, 0));
            var reference = port.Read(new ManualMotionPortRead(ManualMotionReadKind.PositionRp, axis, 0));

            Assert.True(theoretical.Succeeded);
            Assert.True(reference.Succeeded);

            // Encoder (theoretical) counts are exactly twice the drive (reference) counts on every axis.
            Assert.Equal(
                reference.Value * ManualMotionPulseDomain.EncoderCountsPerDriveCount,
                theoretical.Value);

            // Normalizing the theoretical reading reproduces the reference reading exactly.
            Assert.Equal(reference.Value, ManualMotionPulseDomain.NormalizeTheoretical(theoretical.Value));
        }
    }

    /// <summary>
    /// The simulated axis's theoretical reading expressed in the reference (drive) domain, which is the
    /// domain the position assertions in this file are written in. The raw encoder-domain value is the
    /// confirmed 2:1 multiple of it; that relationship is pinned separately below.
    /// </summary>
    private static decimal ReadTpInReferenceDomain(ManualMotionSimulatedPort port, ManualMotionAxis axis = ManualMotionAxis.A)
    {
        var result = port.Read(new ManualMotionPortRead(ManualMotionReadKind.PositionTp, axis, 0));
        Assert.True(result.Succeeded);
        return ManualMotionPulseDomain.NormalizeTheoretical(result.Value);
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
            AxisC = Axis(3),
            AxisD = Axis(4),
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

        var closed = port.Close();
        Assert.True(closed.BoundariesInvalidated);
        Assert.True(closed.HandleClosed);
        Assert.True(closed.ModulesReleased);
        Assert.Equal(1, port.CloseCount);
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
        Assert.Equal(12000m, ReadTpInReferenceDomain(port));

        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, brakeOutput: 1));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(14000m, ReadTpInReferenceDomain(port));
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
        Assert.Equal(1m, brakeOutput.Value);

        port.Execute(Command(ManualMotionPortCommandKind.JogSpeed, operand: 2000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(12000m, ReadTpInReferenceDomain(port));

        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputClear, brakeOutput: 1));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(14000m, ReadTpInReferenceDomain(port));
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
        Assert.Equal(14000m, ReadTpInReferenceDomain(port));

        port.Execute(Command(ManualMotionPortCommandKind.Stop));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(14000m, ReadTpInReferenceDomain(port));

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
        Assert.True(ReadTpInReferenceDomain(ignoring) > 14000m);
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
        var homeBit = ((long)status.Value >> 3) & 1;
        Assert.Equal(1, homeBit);
    }

    [Fact]
    public void Zero_Definition_Changes_The_Reported_Position_Only_When_Not_Ignored()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();
        port.Execute(Command(ManualMotionPortCommandKind.DefinePositionZero));
        Assert.Equal(0m, ReadTpInReferenceDomain(port));

        var ignoring = new ManualMotionSimulatedPort(
            new TestClock(Instant),
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { ZeroDefinitionIgnored = true });
        ignoring.Open();
        ignoring.Execute(Command(ManualMotionPortCommandKind.DefinePositionZero));
        Assert.Equal(12000m, ReadTpInReferenceDomain(ignoring));
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
        Assert.False(refused.Fault);
        Assert.Equal(ManualMotionSimulatedPort.CodeRefused, refused.Code);

        var faulting = new ManualMotionSimulatedPort(
            new TestClock(Instant),
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { FaultOnCommand = ManualMotionPortCommandKind.Begin });
        faulting.Open();
        var faulted = faulting.Execute(Command(ManualMotionPortCommandKind.Begin));
        Assert.False(faulted.Accepted);
        Assert.True(faulted.Fault);
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
        Assert.True(faulted.Fault);
        Assert.Equal(0m, faulted.Value);

        var invalidating = new ManualMotionSimulatedPort(
            new TestClock(Instant),
            ManualMotionProfile.CreateSimulationProfile(),
            new ManualMotionSimulationOptions { InvalidateGenerationAfterCalls = 1 });
        var opened = invalidating.Open();
        Assert.True(opened.Opened);
        var read = invalidating.Read(new ManualMotionPortRead(ManualMotionReadKind.StatusWord, ManualMotionAxis.A, 0));
        Assert.NotEqual(opened.PortGeneration, read.PortGeneration);
    }

    [Fact]
    public void Axes_C_And_D_Move_Independently_With_Their_Own_Brake_Outputs()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();

        // Synthetic start positions: A 12000, B -8000, C 5000, D -3000.
        Assert.Equal(12000m, ReadTpInReferenceDomain(port, ManualMotionAxis.A));
        Assert.Equal(-8000m, ReadTpInReferenceDomain(port, ManualMotionAxis.B));
        Assert.Equal(5000m, ReadTpInReferenceDomain(port, ManualMotionAxis.C));
        Assert.Equal(-3000m, ReadTpInReferenceDomain(port, ManualMotionAxis.D));

        // Releasing the Axis C brake (output 3) changes only output 3.
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.C, brakeOutput: 3));
        var output3 = port.Read(new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.C, 3));
        var output4 = port.Read(new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.D, 4));
        Assert.True(output3.Succeeded);
        Assert.True(output4.Succeeded);
        Assert.Equal(1m, output3.Value);
        Assert.Equal(0m, output4.Value);

        // A C jog moves only C.
        port.Execute(Command(ManualMotionPortCommandKind.JogSpeed, ManualMotionAxis.C, operand: 2000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.C));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(7000m, ReadTpInReferenceDomain(port, ManualMotionAxis.C));
        Assert.Equal(12000m, ReadTpInReferenceDomain(port, ManualMotionAxis.A));
        Assert.Equal(-8000m, ReadTpInReferenceDomain(port, ManualMotionAxis.B));
        Assert.Equal(-3000m, ReadTpInReferenceDomain(port, ManualMotionAxis.D));

        // Stop C, then a negative D jog moves only D.
        port.Execute(Command(ManualMotionPortCommandKind.Stop, ManualMotionAxis.C));
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.D, brakeOutput: 4));
        port.Execute(Command(ManualMotionPortCommandKind.JogSpeed, ManualMotionAxis.D, operand: -2000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.D));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(-5000m, ReadTpInReferenceDomain(port, ManualMotionAxis.D));
        Assert.Equal(7000m, ReadTpInReferenceDomain(port, ManualMotionAxis.C));
        Assert.Equal(12000m, ReadTpInReferenceDomain(port, ManualMotionAxis.A));
        Assert.Equal(-8000m, ReadTpInReferenceDomain(port, ManualMotionAxis.B));
    }

    [Fact]
    public void An_Out_Of_Domain_Axis_Or_Brake_Output_Is_Refused_By_The_Simulated_Port()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();

        var command = port.Execute(Command(ManualMotionPortCommandKind.Begin, (ManualMotionAxis)99));
        Assert.False(command.Accepted);
        Assert.Equal(ManualMotionSimulatedPort.CodeRefused, command.Code);

        var brake = port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.C, brakeOutput: 5));
        Assert.False(brake.Accepted);
        Assert.Equal(ManualMotionSimulatedPort.CodeRefused, brake.Code);

        var read = port.Read(new ManualMotionPortRead(ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.C, 5));
        Assert.False(read.Succeeded);

        var axisRead = port.Read(new ManualMotionPortRead(ManualMotionReadKind.PositionTp, (ManualMotionAxis)99, 0));
        Assert.False(axisRead.Succeeded);
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
            return ReadTpInReferenceDomain(port);
        }

        Assert.Equal(RunOnce(), RunOnce());
    }

    [Fact]
    public void Move_A_And_Move_B_Progress_During_The_Same_Interval_With_Axis_Specific_Positions()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.A, brakeOutput: 1));
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.B, brakeOutput: 2));

        // Two bounded absolute moves armed and begun on their own axes.
        port.Execute(Command(ManualMotionPortCommandKind.Acceleration, ManualMotionAxis.A, operand: 10000));
        port.Execute(Command(ManualMotionPortCommandKind.Speed, ManualMotionAxis.A, operand: 4000));
        port.Execute(Command(ManualMotionPortCommandKind.Deceleration, ManualMotionAxis.A, operand: 10000));
        port.Execute(Command(ManualMotionPortCommandKind.PositionAbsolute, ManualMotionAxis.A, operand: -11000));
        port.Execute(Command(ManualMotionPortCommandKind.Acceleration, ManualMotionAxis.B, operand: 10000));
        port.Execute(Command(ManualMotionPortCommandKind.Speed, ManualMotionAxis.B, operand: 4000));
        port.Execute(Command(ManualMotionPortCommandKind.Deceleration, ManualMotionAxis.B, operand: 10000));
        port.Execute(Command(ManualMotionPortCommandKind.PositionAbsolute, ManualMotionAxis.B, operand: -6000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.A));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.B));

        // At 4000 units/s, 250 ms moves each axis 1000 units, proving that both axes progress
        // concurrently while remaining strictly between start and target (A: 12000 -> 11000 toward
        // -11000; B: -8000 -> -7000 toward -6000). A 500 ms window would land Axis B exactly on its
        // -6000 target, which is completion, not in-flight progress.
        clock.Advance(TimeSpan.FromMilliseconds(250));
        var tpA = ReadTpInReferenceDomain(port, ManualMotionAxis.A);
        var tpB = ReadTpInReferenceDomain(port, ManualMotionAxis.B);

        // Both axes moved during the same interval, each toward its own target.
        Assert.True(tpA < 12000m && tpA > -11000m, "Axis A must progress during the shared interval");
        Assert.True(tpB > -8000m && tpB < -6000m, "Axis B must progress during the shared interval");
        Assert.NotEqual(tpA, tpB);
    }

    [Fact]
    public void Home_A_And_Jog_B_Progress_Independently_And_Stop_A_Never_Stops_B()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.A, brakeOutput: 1));
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.B, brakeOutput: 2));

        port.Execute(Command(ManualMotionPortCommandKind.HomeSpeed, ManualMotionAxis.A, operand: 1500));
        port.Execute(Command(ManualMotionPortCommandKind.Home, ManualMotionAxis.A));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.A));
        port.Execute(Command(ManualMotionPortCommandKind.JogSpeed, ManualMotionAxis.B, operand: 2000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.B));

        clock.Advance(TimeSpan.FromSeconds(1));
        var tpA = ReadTpInReferenceDomain(port, ManualMotionAxis.A);
        Assert.Equal(10500m, tpA);
        Assert.Equal(-6000m, ReadTpInReferenceDomain(port, ManualMotionAxis.B));

        // Stop A halts only A; the Jog on B keeps running.
        port.Execute(Command(ManualMotionPortCommandKind.Stop, ManualMotionAxis.A));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(10500m, ReadTpInReferenceDomain(port, ManualMotionAxis.A));
        Assert.Equal(-4000m, ReadTpInReferenceDomain(port, ManualMotionAxis.B));
    }

    [Fact]
    public void Move_C_And_Move_D_Progress_Independently_And_Stop_C_Never_Stops_D()
    {
        var clock = new TestClock(Instant);
        var port = new ManualMotionSimulatedPort(clock, ManualMotionProfile.CreateSimulationProfile());
        port.Open();
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.C, brakeOutput: 3));
        port.Execute(Command(ManualMotionPortCommandKind.BrakeOutputSet, ManualMotionAxis.D, brakeOutput: 4));

        port.Execute(Command(ManualMotionPortCommandKind.Acceleration, ManualMotionAxis.C, operand: 10000));
        port.Execute(Command(ManualMotionPortCommandKind.Speed, ManualMotionAxis.C, operand: 4000));
        port.Execute(Command(ManualMotionPortCommandKind.Deceleration, ManualMotionAxis.C, operand: 10000));
        port.Execute(Command(ManualMotionPortCommandKind.PositionAbsolute, ManualMotionAxis.C, operand: 2000));
        port.Execute(Command(ManualMotionPortCommandKind.Acceleration, ManualMotionAxis.D, operand: 10000));
        port.Execute(Command(ManualMotionPortCommandKind.Speed, ManualMotionAxis.D, operand: 4000));
        port.Execute(Command(ManualMotionPortCommandKind.Deceleration, ManualMotionAxis.D, operand: 10000));
        port.Execute(Command(ManualMotionPortCommandKind.PositionAbsolute, ManualMotionAxis.D, operand: -1000));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.C));
        port.Execute(Command(ManualMotionPortCommandKind.Begin, ManualMotionAxis.D));

        clock.Advance(TimeSpan.FromMilliseconds(250));
        port.Execute(Command(ManualMotionPortCommandKind.Stop, ManualMotionAxis.C));
        clock.Advance(TimeSpan.FromMilliseconds(750));

        // Axis C froze at its stop position; Axis D continued to its own target on its own output.
        Assert.Equal(4000m, ReadTpInReferenceDomain(port, ManualMotionAxis.C));
        Assert.Equal(-1000m, ReadTpInReferenceDomain(port, ManualMotionAxis.D));
    }
}
