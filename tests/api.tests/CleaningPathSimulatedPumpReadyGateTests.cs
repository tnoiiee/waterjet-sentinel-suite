using Wjss.Runtime;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Coverage for the second, explicitly labelled Pump-ready evidence source
/// (CleaningPathSimulatedPumpReadyGate). Everything here is decorator semantics: no transport, no
/// device, no socket and no native module. The point of the class is that it declares ONE number and
/// passes every valve command and every other observation through to the real boundary untouched, so
/// the tests prove both halves of that sentence.
/// </summary>
public sealed class CleaningPathSimulatedPumpReadyGateTests
{
    private const double Setpoint = 15.0;
    private const double Declared = 16.64;

    private static CleaningPathSimulatedPumpReadyGate Gate(FakeBoundary inner) =>
        new(inner, new SystemClock(), Declared, Setpoint);

    // ------------------------------------------------------------ the declared value

    [Fact]
    public void The_Declared_Value_Is_Served_As_A_Good_Non_Stale_Sample_That_Names_Itself()
    {
        var inner = new FakeBoundary();
        CleaningPathSimulatedPumpReadyGate gate = Gate(inner);

        PressureSample? sample = gate.ReadPumpOutletPressure();

        Assert.NotNull(sample);
        Assert.Equal(Declared, sample!.Bar);
        Assert.Equal(PressureQuality.GOOD, sample.Quality);
        Assert.False(sample.Stale);
        Assert.NotEqual(default, sample.At);
        // The identity is the whole point: a sample from this source can never be read as AI-003+.
        Assert.Equal(CleaningPathSimulatedPumpReadyGate.DeclaredPressureSourceId, sample.SourceId);
        Assert.NotEqual(PressureSample.PumpOutletSource, sample.SourceId);
        Assert.Equal(0, inner.PumpPressureReads);
    }

    [Fact]
    public void A_Declared_Value_At_The_Setpoint_Is_Refused_Instead_Of_Loosening_The_Rule()
    {
        var inner = new FakeBoundary();

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CleaningPathSimulatedPumpReadyGate(inner, new SystemClock(), Setpoint, Setpoint));

        Assert.Contains("is not readiness", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, inner.ValveRequests.Count);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1.0)]
    public void A_Declared_Value_That_Is_Not_A_Finite_Non_Negative_Bar_Is_Refused(double bad)
    {
        var inner = new FakeBoundary();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CleaningPathSimulatedPumpReadyGate(inner, new SystemClock(), bad, Setpoint));
    }

    [Fact]
    public void A_Non_Finite_Setpoint_Is_Refused_Rather_Than_Compared_Against()
    {
        var inner = new FakeBoundary();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CleaningPathSimulatedPumpReadyGate(inner, new SystemClock(), Declared, double.NaN));
    }

    [Fact]
    public void The_Gate_Never_Claims_A_Physical_Pump()
    {
        CleaningPathSimulatedPumpReadyGate gate = Gate(new FakeBoundary());

        Assert.False(gate.PhysicalPumpVerified);
        Assert.Equal("SOFTWARE_SIMULATED", gate.PumpEvidenceKind);
        Assert.Equal(CleaningPathSimulatedPumpReadyGate.EvidenceKind, gate.PumpEvidenceKind);
        Assert.Equal(Declared, gate.DeclaredPumpOutletBar);
        Assert.Equal(Setpoint, gate.PumpReadySetpointBar);
    }

    // ------------------------------------------------------------ the valve precondition

    [Fact]
    public void An_Iv_Open_Is_Refused_Before_The_Run_Has_Begun()
    {
        var inner = new FakeBoundary();
        CleaningPathSimulatedPumpReadyGate gate = Gate(inner);

        gate.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        Assert.Empty(inner.ValveRequests);
        Assert.Equal(
            new[] { CleaningPathSimulatedPumpReadyGate.RefusedValveOpenBeforeBegin + ":IV1" },
            gate.RefusedRequests);
        Assert.False(gate.RunBegun);
    }

    [Fact]
    public void A_Close_Is_Always_Delegated_Because_A_Cleanup_Must_Not_Be_Blocked()
    {
        var inner = new FakeBoundary();
        CleaningPathSimulatedPumpReadyGate gate = Gate(inner);

        gate.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Close));

        Assert.Equal(
            new[] { "Close:IV1" },
            inner.ValveRequests);
        Assert.Empty(gate.RefusedRequests);
    }

    [Fact]
    public void An_Iv_Open_Is_Delegated_Untouched_Once_The_Run_Has_Begun()
    {
        var inner = new FakeBoundary();
        CleaningPathSimulatedPumpReadyGate gate = Gate(inner);
        gate.Begin();

        gate.RequestValve(new CleaningPathValveCommandRequest("IV1", CleaningPathValveCommandKind.Open));

        Assert.True(gate.RunBegun);
        Assert.Equal(new[] { "Open:IV1" }, inner.ValveRequests);
        Assert.Empty(gate.RefusedRequests);
        // The gate never touches a coil itself: it has no session and no catalogue reference.
        Assert.Equal(0, inner.PumpPressureReads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Every_Other_Observation_Is_Delegated_Unchanged(bool upper)
    {
        var inner = new FakeBoundary { Upper = upper, Lower = !upper, ValveBar = 3.5 };
        CleaningPathSimulatedPumpReadyGate gate = Gate(inner);

        Assert.Equal(upper, gate.ReadUpperLimitDetected("IV1"));
        Assert.Equal(!upper, gate.ReadLowerLimitDetected("IV1"));
        PressureSample? valve = gate.ReadValveOutletPressure("IV1");
        Assert.NotNull(valve);
        Assert.Equal(3.5, valve!.Bar);
        Assert.Equal(1, inner.UpperReads);
        Assert.Equal(1, inner.LowerReads);
        Assert.Equal(1, inner.ValvePressureReads);
    }

    // ------------------------------------------------------------ the published vocabulary

    [Fact]
    public void The_Selector_And_Its_Literals_Are_The_Ones_The_Field_Surface_Prints()
    {
        Assert.Equal("WJSS_DRY_SIMULATED_PUMP_READY", CleaningPathDryComposition.SimulatedPumpReadyVariable);
        Assert.Equal("SOFTWARE_SIMULATED_AI003", CleaningPathDryComposition.SimulatedPumpReadyLiteral);
        Assert.Equal("WJSS_DRY_SIMULATED_AI003_BAR", CleaningPathDryComposition.SimulatedPumpBarVariable);
        Assert.Equal("WJSS_DRY_SIMULATED_PUMP_STOPPED", CleaningPathDryComposition.SimulatedPumpStoppedVariable);
        Assert.Equal("PUMP_STOPPED_OWNER_ATTESTED", CleaningPathDryComposition.SimulatedPumpStoppedLiteral);
        Assert.Equal("WJSS_DRY_WET_SEQUENCE_ACK", CleaningPathDryComposition.WetSequenceAckVariable);
        Assert.Equal("WATER_AT_NOZZLE_AUTHORIZED", CleaningPathDryComposition.WetSequenceAckLiteral);
    }

    [Fact]
    public void The_Physical_Path_Keeps_Its_Own_Separate_Refusal_Codes()
    {
        Assert.Equal("DRY_COMPOSITION_REFUSED_SIMULATED_PUMP_SELECTOR", CleaningPathDryComposition.RefusedSimulatedPumpSelector);
        Assert.Equal("DRY_COMPOSITION_REFUSED_SIMULATED_PUMP_BAR", CleaningPathDryComposition.RefusedSimulatedPumpBar);
        Assert.Equal("DRY_COMPOSITION_REFUSED_SIMULATED_PUMP_ATTESTATION", CleaningPathDryComposition.RefusedSimulatedPumpAttestation);
        Assert.Equal("DRY_COMPOSITION_REFUSED_WET_SEQUENCE_NOT_ACKNOWLEDGED", CleaningPathDryComposition.RefusedWetSequenceAcknowledgement);

        // The physical refusal code is unchanged and is still the one the pump gate prints.
        Assert.Equal("IV_OPEN_REFUSED_PUMP_NOT_READY", CleaningPathPumpLifecycleGate.RefusedValveOpenPumpNotReady);
    }

    [Fact]
    public void The_Source_States_The_Label_And_The_Unverified_Pump_Explicitly()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", "CleaningPathDryTrial.cs"));

        Assert.Contains("pump_evidence_kind=", text, StringComparison.Ordinal);
        Assert.Contains("physical_pump_verified=", text, StringComparison.Ordinal);
        Assert.Contains("simulated_ai003_bar=", text, StringComparison.Ordinal);
        Assert.Contains("DECLARED_ABOVE_SETPOINT", text, StringComparison.Ordinal);
        Assert.Contains("DECLARED PUMP-READY EVIDENCE", text, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    /// <summary>Records what was asked for. It commands nothing and holds no device reference.</summary>
    private sealed class FakeBoundary : ICleaningPathEquipmentBoundary
    {
        public List<string> ValveRequests { get; } = new();
        public int PumpPressureReads { get; private set; }
        public int ValvePressureReads { get; private set; }
        public int UpperReads { get; private set; }
        public int LowerReads { get; private set; }
        public bool Upper { get; init; }
        public bool Lower { get; init; }
        public double ValveBar { get; init; }

        public void RequestValve(CleaningPathValveCommandRequest request) =>
            ValveRequests.Add(request.Kind.ToString() + ":" + request.ValveId);

        public PressureSample? ReadPumpOutletPressure()
        {
            PumpPressureReads += 1;
            return null;
        }

        public PressureSample? ReadValveOutletPressure(string valveId)
        {
            ValvePressureReads += 1;
            return new PressureSample(ValveBar, PressureQuality.GOOD, false, DateTimeOffset.UnixEpoch, valveId);
        }

        public bool ReadUpperLimitDetected(string valveId)
        {
            UpperReads += 1;
            return Upper;
        }

        public bool ReadLowerLimitDetected(string valveId)
        {
            LowerReads += 1;
            return Lower;
        }
    }
}
