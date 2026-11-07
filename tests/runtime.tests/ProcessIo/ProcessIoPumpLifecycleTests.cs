using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Tests.ProcessIo;

/// <summary>
/// PHASE 0.5C Pump command, feedback, lifecycle and Cleaning Path precondition.
///
/// These tests prove the CONTRACT and the STATE MACHINE only. Nothing here is a physical verification:
/// every transport is an in-memory double, no Hardware is contacted and no value is a field measurement.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public sealed class ProcessIoPumpLifecycleTests
{
    private const double TargetMilliamps = 4.0;

    private static readonly bool[] EnergisedThenDeEnergised = [true, false];

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(2);

    private const int CoilSelectOne = 0;
    private const int CoilSelectTwo = 1;
    private const int CoilEnable = 2;
    private const int CoilRun = 6;
    private const int CoilIvOne = 22;

    private const int InputRunning = 3;
    private const int InputFault = 4;

    private const int RegisterPumpInlet = 1;
    private const int RegisterPumpOutlet = 2;

    /// <summary>A raw word that converts to about 21.2 bar, strictly above the 15 bar setpoint.</summary>
    private const ushort RawAboveSetpoint = 17367;

    /// <summary>
    /// A raw word that converts to about 15.08 bar, strictly above the 15 bar setpoint. The setpoint sits at
    /// raw 12287.6 under the commissioned 4-20 mA / 0-40 bar conversion (15 / 40 x 32767), so the bracket
    /// pair is built around that word, not around the superseded two-stage boundary at raw 16383.5.
    /// </summary>
    private const ushort RawJustAboveSetpoint = 12352;

    /// <summary>A raw word that converts to about 14.92 bar, strictly below the 15 bar setpoint.</summary>
    private const ushort RawJustBelowSetpoint = 12223;

    // ---------------------------------------------------------------- 1 to 8. the analog conversion

    [Fact(DisplayName = "1. The Pump converter maps 0 mA to raw 0")]
    public void The_Pump_Converter_Maps_Zero_Milliamps_To_Raw_Zero()
    {
        Assert.True(PumpAnalogCommand.TryCreate(0.0, out PumpAnalogCommand command));
        Assert.True(command.IsSafeStopSentinel);
        Assert.True(PumpAnalogCommandContract.TryToRaw(command, out int raw));
        Assert.Equal(0, raw);
    }

    [Fact(DisplayName = "2. The Pump converter maps the 4 mA live minimum to raw 0")]
    public void The_Pump_Converter_Maps_The_Live_Minimum_To_Raw_Zero()
    {
        Assert.True(PumpAnalogCommand.TryCreate(4.0, out PumpAnalogCommand command));
        Assert.True(command.IsRunning);
        Assert.False(command.IsSafeStopSentinel);
        Assert.True(PumpAnalogCommandContract.TryToRaw(command, out int raw));
        Assert.Equal(0, raw);
    }

    [Fact(DisplayName = "3. The Pump converter maps 12 mA to 16384, the exact half-scale word")]
    public void The_Pump_Converter_Maps_Twelve_Milliamps_To_Half_Scale()
    {
        Assert.True(PumpAnalogCommand.TryCreate(12.0, out PumpAnalogCommand command));
        Assert.True(PumpAnalogCommandContract.TryToRaw(command, out int raw));
        Assert.InRange(raw, 16383, 16385);
    }

    [Fact(DisplayName = "4. The Pump converter maps 20 mA to raw full scale")]
    public void The_Pump_Converter_Maps_Twenty_Milliamps_To_Full_Scale()
    {
        Assert.True(PumpAnalogCommand.TryCreate(20.0, out PumpAnalogCommand command));
        Assert.True(PumpAnalogCommandContract.TryToRaw(command, out int raw));
        Assert.Equal(ProcessIoRepresentation.RawFullScale, raw);
    }

    [Fact(DisplayName = "5. The open 0 to 4 mA band is refused and never clamped")]
    public void The_Open_Zero_To_Four_Band_Is_Refused()
    {
        double[] refused = [0.001, 1.0, 2.0, 3.999];
        foreach (double value in refused)
        {
            Assert.False(PumpAnalogCommand.TryCreate(value, out _), $"Expected {value} mA to be refused.");
            Assert.False(PumpAnalogCommandContract.TryToRaw(value, out int raw));
            Assert.Equal(0, raw);
        }

        // And nothing in that band is inside the command domain.
        Assert.False(PumpAnalogCommandContract.IsInDomain(3.999));
        Assert.True(PumpAnalogCommandContract.IsInDomain(4.0));
        Assert.True(PumpAnalogCommandContract.IsInDomain(20.0));
        Assert.True(PumpAnalogCommandContract.IsInDomain(0.0));
    }

    [Fact(DisplayName = "6. A negative or above-20 mA command is refused and never clamped")]
    public void A_Negative_Or_Above_Twenty_Milliamps_Command_Is_Refused()
    {
        Assert.False(PumpAnalogCommand.TryCreate(-0.001, out _));
        Assert.False(PumpAnalogCommand.TryCreate(20.001, out _));
        Assert.False(PumpAnalogCommand.TryCreate(double.NaN, out _));
        Assert.False(PumpAnalogCommand.TryCreate(double.PositiveInfinity, out _));
        Assert.False(PumpAnalogCommand.TryCreate(double.NegativeInfinity, out _));

        // The exact fractional band just above 20 mA is refused, not rounded down to 20 mA.
        Assert.False(PumpAnalogCommandContract.TryToRaw(20.4, out _));
    }

    [Fact(DisplayName = "7. A running target below 4 mA is an invalid configuration")]
    public void A_Running_Target_Below_Four_Milliamps_Is_Invalid_Configuration()
    {
        var belowMinimum = new PumpSpeedRampConfiguration { TargetEngineeringMilliamps = 2.0 };
        Assert.False(belowMinimum.Valid);
        Assert.False(PumpAnalogCommand.TryCreateRunning(2.0, out _));

        var atMinimum = new PumpSpeedRampConfiguration { TargetEngineeringMilliamps = 4.0 };
        Assert.True(atMinimum.Valid);

        // The lifecycle refuses such a configuration before the first write.
        var configuration = ConfigurationFor(Commissioned()) with
        {
            Ramp = new PumpSpeedRampConfiguration { TargetEngineeringMilliamps = 2.0 },
        };
        Assert.False(configuration.Valid);

        Rig rig = Create(configuration);
        PumpLifecycleStartOutcome outcome = rig.Lifecycle.Start(Request());
        Assert.False(outcome.Started);
        Assert.Equal(PumpLifecycleState.COMMAND_ADMISSION_REFUSED, rig.Lifecycle.State);
        Assert.Empty(rig.Transport.CoilWrites);
        Assert.Empty(rig.Transport.RegisterWrites);
    }

    [Fact(DisplayName = "8. The ramp-down reaches the sentinel without ever emitting 0 to 4 mA")]
    public void The_Ramp_Down_Reaches_The_Sentinel_Without_Emitting_An_Invalid_Command()
    {
        var current = PumpAnalogCommand.SafeStopSentinel;
        Assert.True(PumpAnalogCommand.TryCreate(20.0, out current));

        int steps = 0;
        while (!PumpAnalogRamp.AtTarget(current, 0.0) && steps < 32)
        {
            Assert.True(PumpAnalogRamp.TryNext(current, 0.0, 2.0, out PumpAnalogCommand next));

            // Every emitted command is either the sentinel or a live 4 to 20 mA reference.
            Assert.True(next.Valid);
            Assert.True(
                next.IsSafeStopSentinel || PumpAnalogCommandContract.IsLive(next.Milliamps),
                "The down-ramp emitted an invalid command: " + next.Milliamps.ToString("R"));

            current = next;
            steps += 1;
        }

        Assert.True(current.IsSafeStopSentinel);
        Assert.Equal(0.0, current.Milliamps);

        // The 4 mA live minimum is stepped through and the 0 to 4 band never appears.
        var atMinimum = PumpAnalogCommand.TryCreate(4.0, out PumpAnalogCommand four) ? four : default;
        Assert.True(PumpAnalogRamp.TryNext(atMinimum, 0.0, 2.0, out PumpAnalogCommand fromMinimum));
        Assert.True(fromMinimum.IsSafeStopSentinel);
    }

    [Fact(DisplayName = "9. The ramp-up never emits a value outside the live running domain")]
    public void The_Ramp_Up_Stays_In_The_Live_Running_Domain()
    {
        var current = PumpAnalogCommand.SafeStopSentinel;
        int steps = 0;

        while (!PumpAnalogRamp.AtTarget(current, TargetMilliamps) && steps < 16)
        {
            Assert.True(PumpAnalogRamp.TryNext(current, TargetMilliamps, 2.0, out PumpAnalogCommand next));
            Assert.True(next.IsRunning);
            Assert.True(PumpAnalogCommandContract.IsLive(next.Milliamps));
            Assert.True(next.Milliamps <= TargetMilliamps);
            current = next;
            steps += 1;
        }

        Assert.True(PumpAnalogRamp.AtTarget(current, TargetMilliamps));
        Assert.Equal(TargetMilliamps, current.Milliamps);
    }

    [Fact(DisplayName = "10. The raw word is ambiguous at 0 and the engineering identity is retained")]
    public void The_Raw_Word_Is_Ambiguous_And_Identity_Is_Retained()
    {
        Assert.True(PumpAnalogCommand.TryCreate(0.0, out PumpAnalogCommand sentinel));
        Assert.True(PumpAnalogCommand.TryCreate(4.0, out PumpAnalogCommand liveMinimum));

        Assert.True(PumpAnalogCommandContract.TryToRaw(sentinel, out int sentinelRaw));
        Assert.True(PumpAnalogCommandContract.TryToRaw(liveMinimum, out int liveRaw));

        // One raw word, two meanings. Nothing may infer running or stopped from the raw output.
        Assert.Equal(0, sentinelRaw);
        Assert.Equal(0, liveRaw);
        Assert.NotEqual(sentinel.Kind, liveMinimum.Kind);
        Assert.NotEqual(sentinel.KindCode, liveMinimum.KindCode);
        Assert.Equal(PumpAnalogCommandContract.SafeStopSentinelCode, sentinel.KindCode);
        Assert.Equal(PumpAnalogCommandContract.RunningCode, liveMinimum.KindCode);

        // The general converter is NOT the Pump authority and keeps its own 0 to 20 mA contract.
        Assert.True(ProcessIoEngineering.TryPumpSpeedRaw(0.0, out int generalZero));
        Assert.Equal(0, generalZero);
        Assert.True(ProcessIoEngineering.TryPumpSpeedRaw(12.0, out int generalHalf));
        Assert.NotEqual(16384, generalHalf);
    }

    [Fact(DisplayName = "11. The analog contract states milliamps, and states no pressure")]
    public void The_Analog_Contract_States_Milliamps_And_No_Pressure()
    {
        Assert.Equal("mA", PumpAnalogCommandContract.EngineeringUnit);
        Assert.Equal(4.0, PumpAnalogCommandContract.LiveMinimumMilliamps);
        Assert.Equal(20.0, PumpAnalogCommandContract.LiveMaximumMilliamps);
        Assert.Equal(0.0, PumpAnalogCommandContract.SafeStopSentinelMilliamps);
        Assert.Equal(0.0, PumpAnalogCommandContract.SafeZeroEngineeringCommand);
        Assert.Equal(PumpCommandContract.SpeedReferenceTag, PumpAnalogCommandContract.Tag);
        Assert.Equal(1000, PumpSpeedRampConfiguration.OwnerInitialIntervalMilliseconds);
        Assert.Equal(2.0, PumpSpeedRampConfiguration.OwnerInitialStepMilliamps);
    }

    // ---------------------------------------------------------------- 12 to 15. configuration

    [Fact(DisplayName = "12. Both feedback polarities default to NOT_CONFIGURED")]
    public void Both_Feedback_Polarities_Default_To_Not_Configured()
    {
        PumpFeedbackConfiguration feedback = PumpFeedbackConfiguration.Uncommissioned;
        Assert.Equal(PumpFeedbackPolarity.NOT_CONFIGURED, feedback.RunningPolarity);
        Assert.Equal(PumpFeedbackPolarity.NOT_CONFIGURED, feedback.FaultPolarity);
        Assert.False(feedback.IsCommissioned);
        Assert.Equal(PumpFeedbackConfiguration.NotCommissionedCode, feedback.CommissioningCode);
    }

    [Fact(DisplayName = "13. A half-commissioned pair is not commissioned")]
    public void A_Half_Commissioned_Pair_Is_Not_Commissioned()
    {
        var half = new PumpFeedbackConfiguration
        {
            RunningPolarity = PumpFeedbackPolarity.ACTIVE_HIGH,
            FaultPolarity = PumpFeedbackPolarity.NOT_COMMISSIONED,
        };

        Assert.True(half.RunningCommissioned);
        Assert.False(half.FaultCommissioned);
        Assert.False(half.IsCommissioned);

        var both = new PumpFeedbackConfiguration
        {
            RunningPolarity = PumpFeedbackPolarity.ACTIVE_HIGH,
            FaultPolarity = PumpFeedbackPolarity.ACTIVE_LOW,
        };
        Assert.True(both.IsCommissioned);
    }

    [Fact(DisplayName = "14. An uncommissioned polarity refuses the command and writes nothing")]
    public void An_Uncommissioned_Polarity_Refuses_And_Writes_Nothing()
    {
        Rig rig = Create(ConfigurationFor(PumpFeedbackConfiguration.Uncommissioned));

        PumpLifecycleStartOutcome outcome = rig.Lifecycle.Start(Request());

        Assert.False(outcome.Started);
        Assert.Equal(PumpFeedbackConfiguration.NotCommissionedCode, outcome.Code);
        Assert.Equal(PumpLifecycleState.COMMAND_ADMISSION_REFUSED, rig.Lifecycle.State);
        Assert.False(rig.Ownership.IsHeld);
        Assert.Empty(rig.Transport.CoilWrites);
        Assert.Empty(rig.Transport.RegisterWrites);
    }

    [Fact(DisplayName = "15. A Production owner can never acquire the command channel")]
    public void A_Production_Owner_Can_Never_Acquire_The_Command_Channel()
    {
        var ownership = new PumpCommandOwnership();

        Assert.False(ownership.TryAcquire(
            PumpCommandOwnerKind.PRODUCTION, "operator", out string productionCode));
        Assert.Equal(PumpCommandRefusals.RefusedProductionOwner, productionCode);
        Assert.False(ownership.IsHeld);

        Assert.True(ownership.TryAcquire(
            PumpCommandOwnerKind.INTEGRATED_DRY_CYCLE, "dry", out string granted));
        Assert.Equal(PumpCommandRefusals.Granted, granted);

        // The same channel is refused to a second owner, and no fallback owner is invented.
        Assert.False(ownership.TryAcquire(
            PumpCommandOwnerKind.CLEANING_JOB, "another", out string conflictCode));
        Assert.Equal(PumpCommandRefusals.RefusedConflictingOwner, conflictCode);
    }

    // ---------------------------------------------------------------- 16 to 20. state machine

    [Fact(DisplayName = "16. Start withholds every command until the polarity is commissioned")]
    public void Start_Withholds_Every_Command_Until_The_Polarity_Is_Commissioned()
    {
        Rig rig = Create(ConfigurationFor(PumpFeedbackConfiguration.Uncommissioned));
        rig.Lifecycle.Start(Request());

        Assert.Equal(0, rig.Transport.NetworkAttempts);
        Assert.Empty(rig.Lifecycle.CleanupFaultCodes);
    }

    [Fact(DisplayName = "17. The Start order is DO-003 TRUE then DO-007 TRUE")]
    public void The_Start_Order_Is_Enable_Before_Run()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        Assert.True(rig.Lifecycle.Start(Request()).Started);

        int enableIndex = rig.Transport.CoilWrites.FindIndex(w => w.Address == CoilEnable && w.Value);
        int runIndex = rig.Transport.CoilWrites.FindIndex(w => w.Address == CoilRun && w.Value);

        Assert.True(enableIndex >= 0, "Enable Inverter must be energised.");
        Assert.True(runIndex >= 0, "Run Pump must be energised.");
        Assert.True(enableIndex < runIndex, "Enable Inverter must be energised BEFORE Run Pump.");

        // The selection pair precedes both, selected first.
        Assert.Equal((CoilSelectOne, true), (rig.Transport.CoilWrites[0].Address, rig.Transport.CoilWrites[0].Value));
        Assert.Equal((CoilSelectTwo, false), (rig.Transport.CoilWrites[1].Address, rig.Transport.CoilWrites[1].Value));
        Assert.Equal((CoilEnable, true), (rig.Transport.CoilWrites[2].Address, rig.Transport.CoilWrites[2].Value));
        Assert.Equal((CoilRun, true), (rig.Transport.CoilWrites[3].Address, rig.Transport.CoilWrites[3].Value));
    }

    [Fact(DisplayName = "18. Both Start outputs are maintained and are never treated as pulses")]
    public void Both_Start_Outputs_Are_Maintained()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Lifecycle.Start(Request());
        int afterStart = rig.Transport.CoilWrites.Count;

        // A long idle wait must not clear anything: no timer may clear a maintained command.
        for (int step = 0; step < 10; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }

        Assert.Equal(afterStart, rig.Transport.CoilWrites.Count);
        Assert.DoesNotContain(rig.Transport.CoilWrites, write => write.Address == CoilRun && !write.Value);
        Assert.True(GetCoil(rig, CoilRun, defaultValue: true), "Run Pump must remain energised.");
    }

    [Fact(DisplayName = "19. Selection is mutually exclusive and only Pump 1 and Pump 2 exist")]
    public void Selection_Is_Mutually_Exclusive()
    {
        IReadOnlyList<PumpCommandStep> one = PumpCommandContract.SelectionSequence(1);
        IReadOnlyList<PumpCommandStep> two = PumpCommandContract.SelectionSequence(2);

        Assert.Equal(
            new[] { PumpCommandContract.SelectPumpOneTag, PumpCommandContract.SelectPumpTwoTag },
            one.Select(step => step.Tag).ToArray());
        Assert.Equal(
            new[] { PumpCommandContract.SelectPumpTwoTag, PumpCommandContract.SelectPumpOneTag },
            two.Select(step => step.Tag).ToArray());

        // The selected contactor is energised first and the other is de-energised second.
        Assert.Equal(EnergisedThenDeEnergised, one.Select(step => step.Value).ToArray());
        Assert.Equal(EnergisedThenDeEnergised, two.Select(step => step.Value).ToArray());

        Assert.Empty(PumpCommandContract.SelectionSequence(0));
        Assert.Empty(PumpCommandContract.SelectionSequence(3));

        // An invalid ordinal refuses the Start outright and writes nothing.
        Rig rig = Create(ConfigurationFor(Commissioned()));
        PumpLifecycleStartOutcome outcome = rig.Lifecycle.Start(new PumpLifecycleStartRequest
        {
            Owner = PumpCommandOwnerKind.INTEGRATED_DRY_CYCLE,
            OwnerId = "owner",
            Selection = 3,
        });

        Assert.False(outcome.Started);
        Assert.Equal(PumpCommandRefusals.RefusedInvalidSelection, outcome.Code);
        Assert.Empty(rig.Transport.CoilWrites);
    }

    [Fact(DisplayName = "20. The lifecycle reaches every progression state in order and no other")]
    public void The_Lifecycle_Reaches_Every_Progression_State_In_Order()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));

        // The progression is driven by commissioned evidence, never by the command itself: the Running
        // input confirms before the ramp runs, and the Outlet register clears the 15 bar gate.
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;

        var seen = new List<PumpLifecycleState>();
        void Record()
        {
            if (seen.Count == 0 || seen[^1] != rig.Lifecycle.State) seen.Add(rig.Lifecycle.State);
        }

        Record();
        Assert.True(rig.Lifecycle.Start(Request()).Started);
        Record();

        for (int step = 0; step < 200 && rig.Lifecycle.Result is null; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
            Record();

            if (rig.Lifecycle.State == PumpLifecycleState.PUMP_READY)
            {
                rig.Transport.DiscreteInputs[InputRunning] = false;
                rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
            }
        }

        Assert.Equal(PumpLifecycleState.STOPPED, rig.Lifecycle.State);

        PumpLifecycleState[] expected =
        [
            PumpLifecycleState.IDLE,
            PumpLifecycleState.WAITING_FOR_RUNNING,
            PumpLifecycleState.RAMPING_OUTPUT,
            PumpLifecycleState.WAITING_FOR_AI003_READY,
            PumpLifecycleState.PUMP_READY,
            PumpLifecycleState.RAMPING_DOWN,
            PumpLifecycleState.CLEARING_RUN,
            PumpLifecycleState.DISABLING_INVERTER,
            PumpLifecycleState.WAITING_FOR_STOPPED,
            PumpLifecycleState.STOPPED,
        ];

        Assert.Equal(expected, seen);
        Assert.All(seen, state => Assert.True(PumpLifecycleStates.IsProgression(state)));
    }

    // ---------------------------------------------------------------- 21 to 26. readiness and faults

    [Fact(DisplayName = "21. AI-003 BAD or stale refuses readiness and never fabricates a bar value")]
    public void A_Bad_Or_Stale_Ai003_Refuses_Ready()
    {
        Rig bad = Create(ConfigurationFor(Commissioned()));
        bad.Transport.DiscreteInputs[InputRunning] = true;
        bad.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;
        bad.Transport.FaultingInputRegisters.Add(RegisterPumpOutlet);
        StartAndDrive(bad);

        Assert.Equal(PumpLifecycleState.AI003_INVALID, bad.Lifecycle.PrimaryFailure);
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, bad.Lifecycle.State);

        Rig stale = Create(ConfigurationFor(Commissioned()));
        stale.Transport.DiscreteInputs[InputRunning] = true;
        stale.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;
        stale.Transport.ReadDelay = TimeSpan.FromSeconds(5);
        StartAndDrive(stale);

        Assert.Equal(PumpLifecycleState.AI003_STALE, stale.Lifecycle.PrimaryFailure);
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, stale.Lifecycle.State);
    }

    [Fact(DisplayName = "22. Pump-ready requires strictly more than the configured setpoint")]
    public void Pump_Ready_Requires_Strictly_More_Than_The_Setpoint()
    {
        Rig below = Create(ConfigurationFor(Commissioned()));
        below.Transport.DiscreteInputs[InputRunning] = true;
        below.Transport.InputRegisters[RegisterPumpOutlet] = RawJustBelowSetpoint;
        StartAndDrive(below);

        Assert.Equal(PumpLifecycleState.AI003_TIMEOUT, below.Lifecycle.PrimaryFailure);
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, below.Lifecycle.State);

        Rig above = Create(ConfigurationFor(Commissioned()));
        above.Transport.DiscreteInputs[InputRunning] = true;
        above.Transport.InputRegisters[RegisterPumpOutlet] = RawJustAboveSetpoint;
        StarToReady(above);

        Assert.Equal(PumpLifecycleState.PUMP_READY, above.Lifecycle.State);
        Assert.True(above.Lifecycle.AllowsValveOpen);
    }

    [Fact(DisplayName = "23. The Pump Inlet row is never read as Pump-ready evidence")]
    public void The_Pump_Inlet_Row_Is_Never_Read_As_Pump_Ready_Evidence()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = true;

        // The Inlet register carries a high pressure. The Outlet register carries none.
        rig.Transport.InputRegisters[RegisterPumpInlet] = RawAboveSetpoint;

        StartAndDrive(rig);

        Assert.NotEqual(PumpLifecycleState.PUMP_READY, rig.Lifecycle.State);
        Assert.DoesNotContain(RegisterPumpInlet, rig.Transport.RegisterReads);
        Assert.Contains(RegisterPumpOutlet, rig.Transport.RegisterReads);
    }

    [Fact(DisplayName = "24. Run lost after readiness is a trip, not a completion")]
    public void Run_Lost_After_Readiness_Is_A_Trip()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;
        StarToReady(rig);

        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Time.Advance(Interval);
        rig.Lifecycle.Step();

        Assert.Equal(PumpLifecycleState.FAULT_ACTIVE, rig.Lifecycle.PrimaryFailure);
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, rig.Lifecycle.State);

        DriveToTerminal(rig);
        Assert.False(rig.Lifecycle.SafeToReportComplete);
    }

    [Fact(DisplayName = "25. An active configured Fault trips the Pump and blocks readiness")]
    public void An_Active_Configured_Fault_Trips_The_Pump()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;
        rig.Transport.DiscreteInputs[InputFault] = true;

        PumpLifecycleStartOutcome outcome = rig.Lifecycle.Start(Request());

        Assert.False(outcome.Started);
        Assert.Equal(PumpLifecycleState.FAULT_ACTIVE, rig.Lifecycle.State);
        Assert.Empty(rig.Transport.CoilWrites);
        Assert.False(rig.Ownership.IsHeld);
    }

    [Fact(DisplayName = "26. A lost Running feedback is a timeout, never an inferred stop")]
    public void A_Lost_Running_Feedback_Is_A_Timeout()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = false;

        Assert.True(rig.Lifecycle.Start(Request()).Started);
        DriveToTerminal(rig);

        Assert.Equal(PumpLifecycleState.RUNNING_FEEDBACK_TIMEOUT, rig.Lifecycle.PrimaryFailure);
        Assert.Equal(PumpLifecycleState.RUNNING_FEEDBACK_TIMEOUT, rig.Lifecycle.State);
        Assert.False(rig.Lifecycle.SafeToReportComplete);
    }

    // ---------------------------------------------------------------- 27 to 32. the corrected Stop

    [Fact(DisplayName = "27. The Stop order is DO-007 FALSE then DO-003 FALSE")]
    public void The_Stop_Order_Is_Run_Before_Enable()
    {
        Rig rig = Ready();

        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        DriveToTerminal(rig);

        int runIndex = rig.Transport.CoilWrites.FindIndex(w => w.Address == CoilRun && !w.Value);
        int enableIndex = rig.Transport.CoilWrites.FindIndex(w => w.Address == CoilEnable && !w.Value);

        Assert.True(runIndex >= 0, "Run Pump FALSE must be written.");
        Assert.True(enableIndex >= 0, "Enable Inverter FALSE must be written.");
        Assert.True(runIndex < enableIndex, "Run Pump must be de-energised BEFORE Enable Inverter.");
    }

    [Fact(DisplayName = "28. Each de-energisation happens exactly once")]
    public void Each_De_Energisation_Happens_Exactly_Once()
    {
        Rig rig = Ready();

        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        DriveToTerminal(rig);

        // Stepping well past the terminal must not re-issue anything.
        for (int step = 0; step < 20; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }

        Assert.Single(rig.Transport.CoilWrites, write => write.Address == CoilRun && !write.Value);
        Assert.Single(rig.Transport.CoilWrites, write => write.Address == CoilEnable && !write.Value);
    }

    [Fact(DisplayName = "29. Enable is never cleared before Run during the Stop path")]
    public void Enable_Is_Never_Cleared_Before_Run()
    {
        // The superseded order cleared Enable first. It must not be reachable at all.
        Assert.Equal(new[] { PumpCommandContract.RunPumpTag, PumpCommandContract.EnableInverterTag },
            PumpCommandContract.StopOrder);
        Assert.Equal(new[] { PumpCommandContract.EnableInverterTag, PumpCommandContract.RunPumpTag },
            PumpCommandContract.StartOrder);

        foreach (PumpStopReason reason in Enum.GetValues<PumpStopReason>())
        {
            Rig rig = Ready();
            rig.Transport.DiscreteInputs[InputRunning] = false;
            rig.Lifecycle.Stop(reason);
            DriveToTerminal(rig);

            int runIndex = rig.Transport.CoilWrites.FindIndex(w => w.Address == CoilRun && !w.Value);
            int enableIndex = rig.Transport.CoilWrites.FindIndex(w => w.Address == CoilEnable && !w.Value);

            Assert.True(runIndex >= 0 && enableIndex >= 0, "Both de-energisations must run for " + reason);
            Assert.True(runIndex < enableIndex, "The order must hold for " + reason);
        }
    }

    [Fact(DisplayName = "30. A ramp fault still attempts both de-energisations")]
    public void A_Ramp_Fault_Still_Attempts_Both_De_Energisations()
    {
        Rig rig = Ready();

        // The analog write now fails. The two digital de-energisations must still happen, in order.
        rig.Transport.ThrowOnAnalogWrite = true;
        rig.Transport.DiscreteInputs[InputRunning] = false;

        rig.Lifecycle.Stop(PumpStopReason.CANCELLATION);
        DriveToTerminal(rig);

        Assert.Contains(PumpLifecycleState.RAMP_DOWN_FAILED.ToString(), rig.Lifecycle.CleanupFaultCodes);

        int runIndex = rig.Transport.CoilWrites.FindIndex(w => w.Address == CoilRun && !w.Value);
        int enableIndex = rig.Transport.CoilWrites.FindIndex(w => w.Address == CoilEnable && !w.Value);

        Assert.True(runIndex >= 0, "A ramp fault must not skip Run Pump FALSE.");
        Assert.True(enableIndex >= 0, "A ramp fault must not skip Enable Inverter FALSE.");
        Assert.True(runIndex < enableIndex);
        Assert.False(rig.Lifecycle.SafeToReportComplete);
    }

    [Fact(DisplayName = "31. The Pump Stop ramp reaches the configured safe zero")]
    public void The_Pump_Stop_Ramp_Reaches_The_Safe_Zero()
    {
        Rig rig = Ready();

        // The upward ramp raised the reference. The downward ramp must retract it to the sentinel.
        Assert.True(rig.Lifecycle.SpeedCommandMilliamps > 0.0);

        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        DriveToTerminal(rig);

        Assert.Equal(PumpLifecycleState.STOPPED, rig.Lifecycle.State);
        Assert.True(rig.Lifecycle.SpeedCommand.IsSafeStopSentinel);
        Assert.Equal(0.0, rig.Lifecycle.SpeedCommandMilliamps);
        Assert.Equal((ushort)0, rig.Transport.RegisterWrites[^1].Value);

        // Every analog word written in the whole cycle is a valid Pump command.
        Assert.All(rig.Transport.RegisterWrites, write => Assert.True(write.Value <= ProcessIoRepresentation.RawFullScale));
    }

    [Fact(DisplayName = "32. Ownership is not released before a confirmed Stop")]
    public void Ownership_Is_Not_Released_Before_A_Confirmed_Stop()
    {
        Rig rig = Ready();
        Assert.True(rig.Ownership.IsHeld);

        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);

        Assert.Null(rig.Lifecycle.Result);
        Assert.True(rig.Ownership.IsHeld, "Ownership must be held while the Stop is unconfirmed.");

        // Drive to a terminal without ever confirming the stopped state.
        DriveToTerminal(rig);

        Assert.Equal(PumpLifecycleState.PHYSICAL_ABORT_REQUIRED, rig.Lifecycle.State);
        Assert.False(rig.Lifecycle.Result!.StoppedConfirmed);
        Assert.True(rig.Ownership.IsHeld, "An unknown stopped state must never release the channel.");
        Assert.False(rig.Lifecycle.SafeToReportComplete);
    }

    // ---------------------------------------------------------------- 33 to 36. cleanup reporting

    [Fact(DisplayName = "33. An unknown stopped state requires a physical abort")]
    public void An_Unknown_Stopped_State_Requires_A_Physical_Abort()
    {
        Rig rig = Ready();

        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        DriveToTerminal(rig);

        PumpLifecycleResult result = rig.Lifecycle.Result!;
        Assert.Equal(PumpLifecycleState.PHYSICAL_ABORT_REQUIRED, result.TerminalState);
        Assert.Equal(PumpCleanupStates.PhysicalAbortRequired, result.CleanupState);
        Assert.False(result.StoppedConfirmed);
        Assert.Contains(PumpLifecycleState.STOPPED_FEEDBACK_TIMEOUT.ToString(), result.CleanupFaultCodes);
    }

    [Fact(DisplayName = "34. The primary fault remains separate from the cleanup faults")]
    public void The_Primary_Fault_Remains_Separate_From_The_Cleanup_Faults()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;
        StarToReady(rig);

        // The inverter fault appears while the Pump is ready, and the stopped state stays unknown.
        rig.Transport.DiscreteInputs[InputFault] = true;
        rig.Time.Advance(Interval);
        rig.Lifecycle.Step();

        DriveToTerminal(rig);

        PumpLifecycleResult result = rig.Lifecycle.Result!;
        Assert.Equal(PumpLifecycleState.FAULT_ACTIVE, result.PrimaryFailure);
        Assert.Equal(PumpLifecycleState.PHYSICAL_ABORT_REQUIRED, result.TerminalState);
        Assert.Contains(PumpLifecycleState.STOPPED_FEEDBACK_TIMEOUT.ToString(), result.CleanupFaultCodes);
        Assert.False(rig.Lifecycle.SafeToReportComplete);
    }

    [Fact(DisplayName = "35. A confirmed, complete, normal Stop is the only safe completion")]
    public void Only_A_Confirmed_Complete_Normal_Stop_Is_A_Safe_Completion()
    {
        Rig normal = Ready();
        normal.Transport.DiscreteInputs[InputRunning] = false;
        normal.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        DriveToTerminal(normal);

        Assert.True(normal.Lifecycle.SafeToReportComplete);
        Assert.Equal(PumpCleanupStates.Complete, normal.Lifecycle.Result!.CleanupState);
        Assert.False(normal.Ownership.IsHeld);

        // A cleanup that completed with a fault is not a safe completion.
        Rig faulted = Ready();
        faulted.Transport.ThrowOnAnalogWrite = true;
        faulted.Transport.DiscreteInputs[InputRunning] = false;
        faulted.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        DriveToTerminal(faulted);

        Assert.False(faulted.Lifecycle.SafeToReportComplete);
    }

    [Fact(DisplayName = "36. A terminal lifecycle refuses to start again and never retries")]
    public void A_Terminal_Lifecycle_Refuses_To_Start_Again()
    {
        Rig rig = Ready();
        rig.Transport.DiscreteInputs[InputRunning] = false;
        rig.Lifecycle.Stop(PumpStopReason.NORMAL_COMPLETION);
        DriveToTerminal(rig);

        Assert.Equal(PumpLifecycleState.STOPPED, rig.Lifecycle.State);

        int writes = rig.Transport.CoilWrites.Count + rig.Transport.RegisterWrites.Count;
        PumpLifecycleStartOutcome again = rig.Lifecycle.Start(Request());

        Assert.False(again.Started);
        Assert.Equal(PumpCommandRefusals.RefusedTerminal, again.Code);
        Assert.Equal(writes, rig.Transport.CoilWrites.Count + rig.Transport.RegisterWrites.Count);
        Assert.DoesNotContain(rig.Transport.CoilWrites.Skip(writes), write => write.Value);
    }

    // ---------------------------------------------------------------- 37 to 40. the Cleaning Path

    [Fact(DisplayName = "37. An IV OPEN is refused before confirmed Pump readiness")]
    public void An_Iv_Open_Is_Refused_Before_Pump_Ready()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        var inner = new RecordingEquipmentBoundary();
        var gate = new CleaningPathPumpLifecycleGate(inner, rig.Lifecycle);

        gate.RequestValve(OpenRequest());

        Assert.Empty(inner.Requested);
        Assert.Single(gate.RefusedRequests);
        Assert.Contains(CleaningPathPumpLifecycleGate.RefusedValveOpenPumpNotReady, gate.RefusedRequests[0]);

        // And the Pump-ready evidence is withheld, so the composition gate cannot be satisfied either.
        Assert.Null(gate.ReadPumpOutletPressure());
    }

    [Fact(DisplayName = "38. An IV OPEN is delegated after confirmed Pump readiness")]
    public void An_Iv_Open_Is_Delegated_After_Pump_Ready()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;
        StarToReady(rig);

        var inner = new RecordingEquipmentBoundary();
        var gate = new CleaningPathPumpLifecycleGate(inner, rig.Lifecycle);

        gate.RequestValve(OpenRequest());

        Assert.Single(inner.Requested);
        Assert.Empty(gate.RefusedRequests);
    }

    [Fact(DisplayName = "39. An IV CLOSE is delegated in every Pump state, including cleanup")]
    public void An_Iv_Close_Is_Delegated_In_Every_Pump_State()
    {
        var inner = new RecordingEquipmentBoundary();

        // A lifecycle that never started: the gate still delegates every close.
        Rig idle = Create(ConfigurationFor(Commissioned()));
        var idleGate = new CleaningPathPumpLifecycleGate(inner, idle.Lifecycle);
        idleGate.RequestValve(CloseRequest());

        // A faulted lifecycle: still delegated.
        Rig failed = Create(ConfigurationFor(Commissioned()));
        failed.Lifecycle.Start(Request());
        DriveToTerminal(failed);
        Assert.True(PumpLifecycleStates.IsFailure(failed.Lifecycle.State));

        var failedGate = new CleaningPathPumpLifecycleGate(inner, failed.Lifecycle);
        failedGate.RequestValve(CloseRequest());

        Assert.Equal(2, inner.Requested.Count);
        Assert.All(inner.Requested, request => Assert.Equal(CleaningPathValveCommandKind.Close, request.Kind));
    }

    [Fact(DisplayName = "40. The gate never widens the equipment boundary contract")]
    public void The_Gate_Never_Widens_The_Equipment_Boundary_Contract()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        var gate = new CleaningPathPumpLifecycleGate(new RecordingEquipmentBoundary(), rig.Lifecycle);

        // The gate is the equipment boundary, so the coordinator cannot reach a Pump command through it.
        Assert.IsAssignableFrom<ICleaningPathEquipmentBoundary>(gate);
        Assert.Same(rig.Lifecycle, gate.Pump);

        // No member of the contract accepts a Pump COMMAND: the only Pump member is the pressure read.
        Assert.DoesNotContain(
            typeof(ICleaningPathEquipmentBoundary).GetMethods(),
            method => method.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains("Pump", StringComparison.Ordinal)
                && parameter.ParameterType.Name.Contains("Command", StringComparison.Ordinal)));
    }

    // ------------------------------------------------ 41 to 43. the currentness authority of feedback

    [Fact(DisplayName = "41. A stale Running feedback is refused: never a confirmation, only a timeout")]
    public void A_Stale_Running_Feedback_Is_Never_A_Confirmation()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = true;

        // Every digital read consumes five seconds against the two-second freshness budget, so the
        // session's own bracketing reports STALE. An asserted-but-stale Running bit never confirms
        // Running and never starts the ramp: currentness gates every verdict.
        rig.Transport.DiscreteReadDelay = TimeSpan.FromSeconds(5);

        Assert.True(rig.Lifecycle.Start(Request()).Started);
        DriveToTerminal(rig);

        Assert.Equal(PumpLifecycleState.RUNNING_FEEDBACK_TIMEOUT, rig.Lifecycle.PrimaryFailure);
        Assert.Empty(rig.Transport.RegisterWrites);
        Assert.NotEqual(PumpLifecycleState.PUMP_READY, rig.Lifecycle.State);
        Assert.False(rig.Lifecycle.SafeToReportComplete);

        // And still fail-closed on the way out: the not-running feedback is just as stale, so the Stop
        // is never confirmed, the channel is never released and a physical abort is required.
        PumpLifecycleResult result = rig.Lifecycle.Result!;
        Assert.Equal(PumpCleanupStates.PhysicalAbortRequired, result.CleanupState);
        Assert.False(result.StoppedConfirmed);
        Assert.True(rig.Ownership.IsHeld, "A stale feedback loop must never release the channel.");
    }

    [Fact(DisplayName = "42. A stale Fault feedback is refused: PUMP_READY is never tripped by it")]
    public void A_Stale_Fault_Feedback_Never_Trips_The_Pump()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;
        StarToReady(rig);

        int writesAtReady = rig.Transport.CoilWrites.Count;

        // A Fault bit that only arrives stale, and a Running bit that goes missing just as stalely:
        // neither is trusted, so neither trips anything. The refusal is silence, not reinterpretation.
        rig.Transport.DiscreteReadDelay = TimeSpan.FromSeconds(5);
        rig.Transport.DiscreteInputs[InputFault] = true;
        rig.Transport.DiscreteInputs[InputRunning] = false;

        for (int step = 0; step < 4; step += 1)
        {
            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }

        Assert.Equal(PumpLifecycleState.PUMP_READY, rig.Lifecycle.State);
        Assert.Null(rig.Lifecycle.PrimaryFailure);
        Assert.Equal(writesAtReady, rig.Transport.CoilWrites.Count);
    }

    [Fact(DisplayName = "43. Feedback usability is the acceptance rule: never generation, freshness or provenance-free")]
    public void Feedback_Usability_Is_The_Acceptance_Rule_Itself()
    {
        // The boundary sets PumpFeedbackRead.Usable from exactly one authority. This pins that
        // authority directly, so the lifecycle's guards cannot be satisfied by anything weaker: the
        // same physical observation is accepted only while it is GOOD, current, physical and live.
        DateTimeOffset observed = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        ProcessIoPhysicalObservation Build(
            int generation,
            bool fresh,
            ProcessIoObservationProvenance provenance) => new()
        {
            Tag = PumpCommandContract.RunningFeedbackTag,
            RawValue = 1,
            Quality = fresh ? ProcessIoObservationQuality.GOOD : ProcessIoObservationQuality.STALE,
            ObservedAt = observed,
            ReceivedAt = observed,
            SourceGeneration = generation,
            Fresh = fresh,
            Provenance = provenance,
            DiagnosticCode = fresh ? "OK" : "STALE",
        };

        ProcessIoPhysicalObservation live = Build(7, true, ProcessIoObservationProvenance.PHYSICAL_READ);
        Assert.True(ProcessIoEvidenceAcceptance.Accepts(live, 7, observed, Freshness));

        // A superseded generation is refused even when the sample is otherwise perfect.
        Assert.False(ProcessIoEvidenceAcceptance.Accepts(live, 8, observed, Freshness));

        // The same sample ages out: past the freshness budget it is a refusal, never a clamp.
        Assert.False(ProcessIoEvidenceAcceptance.Accepts(live, 7, observed + TimeSpan.FromSeconds(3), Freshness));

        // A simulated read is never physical evidence, at any age and any generation.
        ProcessIoPhysicalObservation simulated = Build(7, true, ProcessIoObservationProvenance.SIMULATED_READ);
        Assert.False(ProcessIoEvidenceAcceptance.Accepts(simulated, 7, observed, Freshness));

        // A stale-quality observation is refused on its own terms, not reinterpreted.
        ProcessIoPhysicalObservation stale = Build(7, false, ProcessIoObservationProvenance.PHYSICAL_READ);
        Assert.False(ProcessIoEvidenceAcceptance.Accepts(stale, 7, observed, Freshness));
    }

    // ---------------------------------------------------------------- helpers

    private static PumpFeedbackConfiguration Commissioned() => new()
    {
        RunningPolarity = PumpFeedbackPolarity.ACTIVE_HIGH,
        FaultPolarity = PumpFeedbackPolarity.ACTIVE_HIGH,
    };

    private static PumpLifecycleConfiguration ConfigurationFor(
        PumpFeedbackConfiguration feedback,
        double target = TargetMilliamps) => new()
        {
            PumpReadySetpointBar = PumpLifecycleConfiguration.OwnerInitialPumpReadySetpointBar,
            PressureFreshnessBudget = Freshness,
            RunningFeedbackTimeout = TimeSpan.FromSeconds(1),
            PumpReadyTimeout = TimeSpan.FromSeconds(1),
            StoppedFeedbackTimeout = TimeSpan.FromSeconds(1),
            Ramp = new PumpSpeedRampConfiguration
            {
                Interval = Interval,
                Step = 2.0,
                TargetEngineeringMilliamps = target,
            },
            Feedback = feedback,
        };

    private static PumpLifecycleStartRequest Request() => new()
    {
        Owner = PumpCommandOwnerKind.INTEGRATED_DRY_CYCLE,
        OwnerId = "cleaning-path-dry",
        Selection = 1,
    };

    private static CleaningPathValveCommandRequest OpenRequest() =>
        new("IV1", CleaningPathValveCommandKind.Open);

    private static CleaningPathValveCommandRequest CloseRequest() =>
        new("IV1", CleaningPathValveCommandKind.Close);

    private static bool GetCoil(Rig rig, int address, bool defaultValue) =>
        rig.Transport.Coils.TryGetValue(address, out bool value) ? value : defaultValue;

    private sealed class Rig
    {
        public required FakeTransport Transport { get; init; }
        public required FakeTime Time { get; init; }
        public required ProcessIoPumpLifecycle Lifecycle { get; init; }
        public required PumpCommandOwnership Ownership { get; init; }
    }

    private static Rig Create(PumpLifecycleConfiguration configuration)
    {
        var time = new FakeTime();
        var transport = new FakeTransport { Clock = time };
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(time.GetUtcNow());

        var boundary = new ProcessIoPumpCommandBoundary(
            session, BuildCatalogue(), Authorize(), configuration.Feedback, time, Freshness);
        var ownership = new PumpCommandOwnership();

        return new Rig
        {
            Transport = transport,
            Time = time,
            Ownership = ownership,
            Lifecycle = new ProcessIoPumpLifecycle(boundary, configuration, ownership, time),
        };
    }

    private static Rig Ready()
    {
        Rig rig = Create(ConfigurationFor(Commissioned()));
        rig.Transport.DiscreteInputs[InputRunning] = true;
        rig.Transport.InputRegisters[RegisterPumpOutlet] = RawAboveSetpoint;
        StarToReady(rig);
        return rig;
    }

    private static void StarToReady(Rig rig)
    {
        Assert.True(rig.Lifecycle.Start(Request()).Started);
        Drive(rig, 64, PumpLifecycleState.PUMP_READY);
        Assert.Equal(PumpLifecycleState.PUMP_READY, rig.Lifecycle.State);
    }

    private static void StartAndDrive(Rig rig)
    {
        Assert.True(rig.Lifecycle.Start(Request()).Started);
        DriveToTerminal(rig);
    }

    private static void DriveToTerminal(Rig rig) => Drive(rig, 128, null);

    private static void Drive(Rig rig, int steps, PumpLifecycleState? stopAt)
    {
        for (int step = 0; step < steps; step += 1)
        {
            if (rig.Lifecycle.Result is not null) return;
            if (stopAt is { } target && rig.Lifecycle.State == target) return;

            rig.Time.Advance(Interval);
            rig.Lifecycle.Step();
        }
    }

    /// <summary>Builds the adopted catalogue from the real binding table, as the composition root does.</summary>
    private static Dictionary<string, ProcessIoCatalogueRow> BuildCatalogue()
    {
        string[] tags =
        [
            PumpCommandContract.SelectPumpOneTag,
            PumpCommandContract.SelectPumpTwoTag,
            PumpCommandContract.EnableInverterTag,
            PumpCommandContract.RunPumpTag,
            PumpCommandContract.SpeedReferenceTag,
            PumpCommandContract.RunningFeedbackTag,
            PumpCommandContract.FaultFeedbackTag,
            PumpCommandContract.PumpOutletPressureTag,
            PumpCommandContract.PumpInletPressureTag,
        ];

        var rows = new Dictionary<string, ProcessIoCatalogueRow>(StringComparer.Ordinal);
        foreach (string tag in tags)
        {
            Assert.True(ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding));
            rows[tag] = new ProcessIoCatalogueRow
            {
                SourceRowOrdinal = 1,
                Tag = tag,
                Description = tag,
                Module = string.Empty,
                Slot = string.Empty,
                IoSlot = string.Empty,
                Channel = string.Empty,
                Terminal = string.Empty,
                From = string.Empty,
                To = string.Empty,
                Reference = string.Empty,
                Location = string.Empty,
                Note = string.Empty,
                SourceStatus = "USED",
                IoClass = binding.Area switch
                {
                    ProcessIoArea.Coil => "DO",
                    ProcessIoArea.DiscreteInput => "DI",
                    _ => "AI",
                },
                SourceClassification = "USED",
                ProvenanceArtifact = "test",
                ProvenanceSha256 = "test",
                Binding = binding,
                BindingState = ProcessIoBindingState.BOUND_LEGACY_EXACT,
                Address = null!,
                CommissioningState = ProcessIoCommissioningState.READ_ONLY_CANDIDATE,
            };
        }

        return rows;
    }

    private static ProcessIoWriteAuthorization Authorize()
    {
        Assert.True(ProcessIoDryCommandGate.TryAuthorize(
            DeviceProfile.TEST_HARDWARE_DRY,
            dryCommandMode: true,
            ownerLocalEndpoint: "dry-run.invalid:502",
            suppliedConsent: ProcessIoConsentPhrases.PhysicalDoSingle,
            out ProcessIoWriteAuthorization? authorization,
            out string code));
        Assert.Equal(ProcessIoDryCommandGate.Granted, code);
        return authorization!;
    }

    private sealed class RecordingEquipmentBoundary : ICleaningPathEquipmentBoundary
    {
        public List<CleaningPathValveCommandRequest> Requested { get; } = new();

        public void RequestValve(CleaningPathValveCommandRequest request) => Requested.Add(request);

        public PressureSample? ReadPumpOutletPressure() =>
            new(18.0, PressureQuality.GOOD, false, DateTimeOffset.UnixEpoch, PressureSample.PumpOutletSource);

        public PressureSample? ReadValveOutletPressure(string valveId) => null;

        public bool ReadUpperLimitDetected(string valveId) => false;

        public bool ReadLowerLimitDetected(string valveId) => false;
    }

    /// <summary>
    /// An in-memory transport. It records every attempt, never contacts anything and can be told to fault.
    /// </summary>
    private sealed class FakeTransport : IProcessIoTransport
    {
        public List<(string Device, int Address, bool Value)> CoilWrites { get; } = new();
        public Dictionary<int, bool> Coils { get; } = new();
        public List<(int Address, ushort Value)> RegisterWrites { get; } = new();
        public List<int> RegisterReads { get; } = new();
        public Dictionary<int, bool> DiscreteInputs { get; } = new();
        public Dictionary<int, ushort> InputRegisters { get; } = new();
        /// <summary>Registers whose read fails, so the session reports an unusable observation.</summary>
        public HashSet<int> FaultingInputRegisters { get; } = new();

        /// <summary>A read that consumes this long is reported STALE by the session's own bracketing.</summary>
        public TimeSpan ReadDelay { get; set; } = TimeSpan.Zero;

        /// <summary>The clock the transport advances while a slow read is in flight.</summary>
        public FakeTime? Clock { get; set; }

        public bool ThrowOnWrite { get; set; }
        public bool ThrowOnAnalogWrite { get; set; }

        public int NetworkAttempts => CoilWrites.Count + RegisterWrites.Count;

        /// <summary>
        /// A digital read that consumes this long is reported STALE by the session's own bracketing,
        /// independently of the analog delay, so feedback currentness is provable on the DI path too.
        /// </summary>
        public TimeSpan DiscreteReadDelay { get; set; } = TimeSpan.Zero;

        public bool ReadDiscreteInput(string deviceKey, int address)
        {
            if (DiscreteReadDelay > TimeSpan.Zero)
            {
                Clock?.Advance(DiscreteReadDelay);
            }

            return DiscreteInputs.TryGetValue(address, out bool value) && value;
        }

        public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity) =>
            Enumerable.Range(startAddress, quantity)
                .Select(address => Coils.TryGetValue(address, out bool value) && value)
                .ToList();

        public ushort ReadInputRegister(string deviceKey, int registerAddress)
        {
            RegisterReads.Add(registerAddress);

            if (ReadDelay > TimeSpan.Zero)
            {
                Clock?.Advance(ReadDelay);
            }

            if (FaultingInputRegisters.Contains(registerAddress))
            {
                throw new IOException("FAKE_ANALOG_READ_FAULT");
            }

            return InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;
        }

        public ushort ReadRegister(string deviceKey, int registerAddress)
        {
            RegisterReads.Add(registerAddress);
            return 0;
        }

        public bool ReadCoil(string deviceKey, int coilAddress) =>
            Coils.TryGetValue(coilAddress, out bool value) && value;

        public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity) =>
            Enumerable.Range(startAddress, quantity).Select(_ => (ushort)0).ToList();

        public void WriteCoil(string deviceKey, int coilAddress, bool value)
        {
            if (ThrowOnWrite) throw new InvalidOperationException("FAKE_TRANSPORT_FAULT");
            CoilWrites.Add((deviceKey, coilAddress, value));
            Coils[coilAddress] = value;
        }

        public void WriteSingleRegister(string deviceKey, int registerAddress, ushort value)
        {
            if (ThrowOnWrite || ThrowOnAnalogWrite) throw new InvalidOperationException("FAKE_TRANSPORT_FAULT");
            RegisterWrites.Add((registerAddress, value));
        }

        public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values)
        {
            if (ThrowOnWrite || ThrowOnAnalogWrite) throw new InvalidOperationException("FAKE_TRANSPORT_FAULT");
            for (int index = 0; index < values.Count; index += 1)
            {
                RegisterWrites.Add((startAddress + index, values[index]));
            }
        }
    }

    /// <summary>A manual clock that only advances when the test advances it.</summary>
    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
