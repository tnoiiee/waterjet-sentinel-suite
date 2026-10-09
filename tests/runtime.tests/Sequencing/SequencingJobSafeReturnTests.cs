using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>CP-3c-2 measured-pressure and parallel Safe Return contract (SIMULATOR only).</summary>
public sealed class SequencingJobSafeReturnTests
{
    private static readonly string[] ExpectedSecondQueuedSensorIds =
    [
        "SYN-S02",
    ];
    private static readonly string[] ExpectedUpperAndLowerLimitFaults =
    [
        "UPPER_LIMIT_SENSOR_FAULT",
        "LOWER_LIMIT_SENSOR_FAULT",
    ];

    private static readonly DateTimeOffset Epoch = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int tick) => Epoch.AddSeconds(tick);
    private static readonly SequencingTopology Topology = new(
        [new("SYN-S01", "WJ1", "IV1"), new("SYN-S02", "WJ2", "IV2")], []);
    private static PressureSample Pump(double? bar, int tick, PressureQuality quality = PressureQuality.GOOD, bool stale = false) =>
        new(bar, quality, stale, At(tick), PressureSample.PumpOutletSource);
    private static PressureSample Valve(double? bar, int tick, string valveId = "IV1", PressureQuality quality = PressureQuality.GOOD) =>
        new(bar, quality, false, At(tick), PressureSample.ValveOutletSource(valveId));
    private static SequencingTransition Apply(SequencingState state, SequencingEvent e) => SequencingKernel.Apply(state, e);
    private static SequencingState Next(SequencingState state, SequencingEvent e)
    {
        var result = Apply(state, e);
        Assert.Equal(SequencingOutcome.APPLIED, result.Outcome);
        return result.State;
    }
    private static SequencingActiveJob Job(SequencingState state) => Assert.IsType<SequencingActiveJob>(state.ActiveJob);
    private static int Seq(SequencingTransition result, SafeReturnStep step) =>
        Assert.Single(result.Records, r => r.Step == step).Seq;

    private static SequencingState Dispatched()
    {
        var state = Next(SequencingKernel.Initial(), new StartAutoSequence(At(0)));
        state = Next(state, new AdmitQueueEntry(At(1), SequencingAdmissionSource.SCENARIO_PREPARED, "SYN-S01", "SYNTHETIC", 0, Topology));
        state = Next(state, new AdmitQueueEntry(At(2), SequencingAdmissionSource.SCENARIO_PREPARED, "SYN-S02", "SYNTHETIC", 0, Topology));
        return Next(state, new DispatchHead(At(3), Topology, PumpReady: true));
    }

    private static SequencingState PendingOpen(double pump = 16)
    {
        var state = Dispatched();
        state = Next(state, new PumpPressureObserved(At(4), Pump(pump, 4), new()));
        state = Next(state, new ValveLimitObserved(At(5), "IV1", false, true));
        state = Next(state, new AdvanceJobPreparation(At(6)));
        state = Next(state, new BeginCleaning(At(7)));
        Assert.False(Job(state).WaterOutputOn);
        Assert.Null(Job(state).VerifiedPhase);
        return Next(state, new ExecutionPhaseVerified(At(8), JobPhase.P1)); // OPEN command only
    }

    private static SequencingState Cleaning(bool upper = true, bool timeout = false)
    {
        var state = PendingOpen();
        return Next(state, new ValveSupervisionObserved(At(9), "IV1", upper, false, timeout, Valve(15, 9), new()));
    }

    private static SequencingState Completed(bool upper = true, bool timeout = false)
    {
        var state = Cleaning(upper, timeout);
        for (var n = 2; n <= 6; n++)
            state = Next(state, new ExecutionPhaseVerified(At(n + 8), (JobPhase)(n - 1)));
        Assert.Equal(JobPhase.P6, Job(state).VerifiedPhase);
        return state;
    }

    private static ValveSupervisionObserved Close(int tick, double? bar = 0.5, bool lower = true,
        bool timeout = false, PressureQuality quality = PressureQuality.GOOD, string valveId = "IV1") =>
        new(At(tick), valveId, false, lower, timeout, Valve(bar, tick, valveId, quality), new());

    [Fact]
    public void A_Dispatch_Boolean_Or_Another_Transmitter_Cannot_Ready_The_Pump()
    {
        var state = Dispatched();
        Assert.False(Job(state).PumpReady);
        Assert.Equal(SequencingOutcome.REFUSED, Apply(state, new ObservePumpReadiness(At(4), true)).Outcome);
        var wrongSource = Next(state, new PumpPressureObserved(At(4), Valve(100, 4), new()));
        Assert.False(Job(wrongSource).PumpReady);
        Assert.Null(Job(wrongSource).PumpPressure);
        Assert.False(Job(Next(state, new PumpPressureObserved(At(4), Pump(15, 4), new()))).PumpReady);
        Assert.True(Job(Next(state, new PumpPressureObserved(At(4), Pump(15.01, 4), new()))).PumpReady);
        Assert.False(Job(Next(state, new PumpPressureObserved(At(4), Pump(16, 4, PressureQuality.STALE), new()))).PumpReady);
        Assert.False(Job(Next(state, new PumpPressureObserved(At(4), Pump(double.NaN, 4), new()))).PumpReady);
        Assert.True(Job(Next(state, new PumpPressureObserved(At(4), Pump(16, 4), new(1, 100, 15)))).PumpReady);
    }

    [Fact]
    public void P1_Waits_For_The_Paired_Valve_Outlet_After_The_OPEN_Command()
    {
        var state = PendingOpen();
        Assert.True(Job(state).ValveOpenCommanded);
        Assert.False(Job(state).WaterOutputOn);
        Assert.Equal(SequencingOutcome.REFUSED, Apply(state, new ExecutionPhaseVerified(At(9), JobPhase.P2)).Outcome);
        var wrongSource = Apply(state, new ValveSupervisionObserved(At(9), "IV1", true, false, false, Pump(30, 9), new()));
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, Job(wrongSource.State).Lifecycle);
        var verified = Next(state, new ValveSupervisionObserved(At(9), "IV1", true, false, false, Valve(15, 9), new()));
        Assert.Equal(JobPhase.P1, Job(verified).VerifiedPhase);
        Assert.True(Job(verified).WaterOutputOn);
        Assert.Equal(ValveOpenResolution.OPEN_CONFIRMED, Job(verified).OpenResolution);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Close_And_Standby_Are_Independent_And_The_Second_Valid_Result_Releases(bool standbyFirst)
    {
        var triggered = Apply(Completed(), new RequestNormalCompletion(At(15)));
        var closeCommand = Seq(triggered, SafeReturnStep.SR2);
        var axisCommand = Seq(triggered, SafeReturnStep.SR4);
        Assert.True(closeCommand < axisCommand);
        Assert.Equal(JobLifecycle.SAFE_RETURN_VERIFY_STANDBY, Job(triggered.State).Lifecycle);
        var first = standbyFirst ? Apply(triggered.State, new AxisFeedbackObserved(At(16), AxisFeedbackState.AT_STANDBY))
            : Apply(triggered.State, Close(16));
        Assert.NotNull(first.State.ActiveJob);
        Assert.DoesNotContain(first.Records, e => e.Step == SafeReturnStep.SR6);
        var second = standbyFirst ? Apply(first.State, Close(17))
            : Apply(first.State, new AxisFeedbackObserved(At(17), AxisFeedbackState.AT_STANDBY));
        Assert.Equal(SequencingOutcome.APPLIED, second.Outcome);
        Assert.Null(second.State.ActiveJob);
        var resolutionSeq = standbyFirst ? Seq(second, SafeReturnStep.SR3) : Seq(first, SafeReturnStep.SR3);
        var standbySeq = standbyFirst ? Seq(first, SafeReturnStep.SR5) : Seq(second, SafeReturnStep.SR5);
        Assert.True(axisCommand < resolutionSeq);
        Assert.True(axisCommand < standbySeq);
        Assert.True(resolutionSeq < Seq(second, SafeReturnStep.SR6));
        Assert.True(standbySeq < Seq(second, SafeReturnStep.SR6));
        Assert.Equal(CleaningJobOutcome.COMPLETED, Assert.Single(second.Records, e => e.Step == SafeReturnStep.SR6).JobOutcome);
        Assert.Equal(ExpectedSecondQueuedSensorIds, second.State.Queue.Select(e => e.SensorId));
    }

    [Theory]
    [InlineData(1.0, ValveCloseResolution.LEAK_SUSPECTED)]
    [InlineData(15.0, ValveCloseResolution.NOT_FULLY_CLOSED)]
    public void Unsafe_Close_Retains_Job_Regardless_Of_Which_Branch_Arrives_First(double pressure, ValveCloseResolution resolution)
    {
        var state = Next(Completed(), new RequestNormalCompletion(At(15)));
        state = Next(state, new AxisFeedbackObserved(At(16), AxisFeedbackState.AT_STANDBY));
        var result = Apply(state, Close(17, pressure, lower: false, timeout: true));
        Assert.Equal(resolution, Job(result.State).CloseResolution);
        Assert.True(Job(result.State).AxisStandbyConfirmed);
        Assert.NotNull(result.State.ActiveJob);
        Assert.DoesNotContain(result.Records, r => r.Step is SafeReturnStep.SR6 or SafeReturnStep.SR7);
        Assert.Single(result.State.EquipmentFaults);
        Assert.Equal(SequencingOutcome.REFUSED, Apply(result.State, new DispatchHead(At(18), Topology, true)).Outcome);
    }

    [Fact]
    public void Pressure_Inference_After_Standby_Releases_Without_Inventing_Lower_Limit_Evidence()
    {
        var state = Next(Completed(), new RequestNormalCompletion(At(15)));
        state = Next(state, new AxisFeedbackObserved(At(16), AxisFeedbackState.AT_STANDBY));
        var released = Apply(state, Close(17, lower: false, timeout: true));
        Assert.Null(released.State.ActiveJob);
        var resolution = Assert.Single(released.Records, r => r.Step == SafeReturnStep.SR3);
        Assert.Equal(ValveCloseResolution.CLOSED_BY_PRESSURE, resolution.CloseResolution);
        Assert.Null(resolution.ValveFeedback);
        Assert.Contains(released.State.EquipmentFaults, f => f.Diagnosis == "LOWER_LIMIT_SENSOR_FAULT" && f.ModalOpen && f.NextDispatchBlocked);
        Assert.Equal(SequencingOutcome.REFUSED, Apply(released.State, new DispatchHead(At(18), Topology, true)).Outcome);
    }

    [Fact]
    public void Both_Limit_Faults_Survive_Release_Without_Reset_Retry_Or_Queue_Mutation()
    {
        var state = Completed(upper: false, timeout: true);
        Assert.Equal(ValveOpenResolution.OPEN_BY_PRESSURE, Job(state).OpenResolution);
        Assert.Contains(state.EquipmentFaults, f => f.Diagnosis == "UPPER_LIMIT_SENSOR_FAULT");
        state = Next(state, new RequestNormalCompletion(At(15)));
        state = Next(state, Close(16, lower: false, timeout: true));
        var released = Apply(state, new AxisFeedbackObserved(At(17), AxisFeedbackState.AT_STANDBY));
        Assert.Null(released.State.ActiveJob);
        Assert.Equal(2, released.State.EquipmentFaults.Count);
        Assert.Equal(ExpectedUpperAndLowerLimitFaults,
            released.State.EquipmentFaults.Select(f => f.Diagnosis));
        Assert.Equal(ExpectedSecondQueuedSensorIds, released.State.Queue.Select(e => e.SensorId));
        var blocked = Apply(released.State, new DispatchHead(At(18), Topology, true));
        Assert.Equal(SequencingOutcome.REFUSED, blocked.Outcome);
        Assert.Equal(released.State.QueueRevision, blocked.State.QueueRevision);
    }

    [Fact]
    public void Invalid_Close_And_Axis_Fault_Retain_The_Job_And_Only_Record_One_Outcome()
    {
        var state = Next(Cleaning(), new RequestAbort(At(10)));
        var invalid = Next(state, Close(11, double.NaN));
        Assert.Equal(ValveCloseResolution.PRESSURE_INPUT_INVALID, Job(invalid).CloseResolution);
        var standby = Next(invalid, new AxisFeedbackObserved(At(12), AxisFeedbackState.AT_STANDBY));
        Assert.NotNull(standby.ActiveJob);
        var failed = Next(standby, new AxisFeedbackObserved(At(13), AxisFeedbackState.FAULT));
        Assert.Equal(JobLifecycle.SAFE_RETURN_FAILED, Job(failed).Lifecycle);
        Assert.Null(failed.ActiveJob?.Ledger.ValveClosedSeq);
        Assert.Equal(SequencingOutcome.REFUSED, Apply(failed, new DispatchHead(At(14), Topology, true)).Outcome);
        var lateClose = Apply(failed, Close(15));
        Assert.Equal(SequencingOutcome.REFUSED, lateClose.Outcome);
        Assert.NotNull(lateClose.State.ActiveJob);
        var lateStandby = Apply(lateClose.State, new AxisFeedbackObserved(At(16), AxisFeedbackState.AT_STANDBY));
        Assert.Equal(SequencingOutcome.NO_OP, lateStandby.Outcome);
        Assert.NotNull(lateStandby.State.ActiveJob);
        Assert.DoesNotContain(lateStandby.Records, r => r.Step == SafeReturnStep.SR7);
    }
}
