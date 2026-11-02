using Wjss.Contracts;
using Wjss.Runtime;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Focused coverage for Trial A, the isolated READ-ONLY physical Pump-ready trial.
///
/// Every test here is configuration, refusal, observation or classification evidence. The only transport
/// is a deterministic in-memory double, so no test contacts a device, opens a socket, starts a Pump or
/// commands anything. The physical run itself is a separate, separately authorized Owner-local activity.
///
/// The values are synthetic: a non-routable placeholder host name and the published consent constant. No
/// Owner-local endpoint, no committed address and no secret appears in this file.
/// </summary>
public sealed class ProcessIoPumpReadyReadTrialTests
{
    private const string BenchHost = "bench-host.local";

    /// <summary>Raw word whose Product conversion is about 29 bar, strictly above the 15 bar setpoint.</summary>
    private const int RawAbove = 24000;

    /// <summary>Raw word whose Product conversion is about 14.6 bar, below the 15 bar setpoint.</summary>
    private const int RawBelow = 12000;

    /// <summary>A raw word outside the adopted representation domain (full scale is 32767).</summary>
    private const int RawOutOfDomain = 40000;

    private static readonly SequencingPressureThresholds Thresholds = new(1.0, 15.0, 15.0);

    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(2);

    // ================================================================== selector

    [Fact]
    public void An_Absent_Selector_Does_Not_Select_Trial_A()
    {
        Assert.True(ProcessIoPumpReadyReadTrial.TryReadMode(
            Read(new Dictionary<string, string?>()), out bool selected, out string code, out _));

        Assert.False(selected);
        Assert.Empty(code);
    }

    [Fact]
    public void Only_The_Exact_Selector_Literal_Selects_Trial_A()
    {
        var environment = Read(new Dictionary<string, string?>
        {
            [ProcessIoPumpReadyReadTrial.ModeVariable] = ProcessIoPumpReadyReadTrial.ModeAi003Only,
        });

        Assert.True(ProcessIoPumpReadyReadTrial.TryReadMode(environment, out bool selected, out _, out _));
        Assert.True(selected);
        Assert.Equal("AI-003_ONLY", ProcessIoPumpReadyReadTrial.ModeAi003Only);
    }

    [Theory]
    [InlineData("ai-003_only")]
    [InlineData("AI-003-ONLY")]
    [InlineData("AI003_ONLY")]
    [InlineData("TEST_HARDWARE_DRY")]
    [InlineData("SOFTWARE_SIMULATED_AI003")]
    [InlineData(" AI-003_ONLY")]
    [InlineData("AI-003_ONLY ")]
    public void A_Differently_Spelled_Selector_Refuses(string literal)
    {
        var environment = Read(new Dictionary<string, string?>
        {
            [ProcessIoPumpReadyReadTrial.ModeVariable] = literal,
        });

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadMode(environment, out bool selected, out string code, out _));
        Assert.False(selected);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedModeInvalid, code);
    }

    [Fact]
    public void A_Missing_Selector_Refuses_A_Direct_Configuration_Read()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(ProcessIoPumpReadyReadTrial.ModeVariable);

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedModeNotSelected, code);
    }

    // ================================================================== profile and exclusivity

    [Fact]
    public void Simulator_Can_Never_Select_Trial_A()
    {
        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.SIMULATOR, Read(ValidEnvironment()), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedProfileNotHardware, code);
    }

    [Fact]
    public void Production_Can_Never_Select_Trial_A()
    {
        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.PRODUCTION, Read(ValidEnvironment()), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedProduction, code);
    }

    [Fact]
    public void The_Dry_Profile_Is_Refused_Because_This_Trial_Is_Not_A_Dry_Composition()
    {
        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_DRY, Read(ValidEnvironment()), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedProfileNotHardware, code);
    }

    [Fact]
    public void The_Read_Only_Tc_Profile_Is_Not_Reused_For_This_Trial()
    {
        // TEST_HARDWARE_READ_ONLY is committed to the one-Node TC read path; this trial never borrows it.
        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE_READ_ONLY, Read(ValidEnvironment()), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedProfileNotHardware, code);
    }

    [Fact]
    public void The_Integrated_Dry_Selector_Is_Refused()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[ProcessIoEndpointConfiguration.DryModeVariable] = nameof(DeviceProfile.TEST_HARDWARE_DRY);

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedIntegratedDrySelected, code);
    }

    [Fact]
    public void The_Iv_Trial_Selector_Is_Refused()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[DryIvTrial.ModeVariable] = DryIvTrial.ModeSoftwareSimulatedAi003;

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedIvTrialSelected, code);
    }

    // ================================================================== endpoint and consent

    [Fact]
    public void A_Missing_Endpoint_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(ProcessIoEndpointConfiguration.HostVariable);

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoEndpointConfiguration.NotConfigured, code);
    }

    [Fact]
    public void A_Rejected_Endpoint_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[ProcessIoEndpointConfiguration.PortVariable] = "70000";

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoEndpointConfiguration.Rejected, code);
    }

    [Fact]
    public void A_Missing_Consent_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(ProcessIoEndpointConfiguration.ConsentVariable);

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedConsent, code);
    }

    [Fact]
    public void The_Write_Consent_Phrase_Never_Authorizes_This_Read_Trial()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[ProcessIoEndpointConfiguration.ConsentVariable] = ProcessIoConsentPhrases.PhysicalDoSingle;

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedConsent, code);
    }

    [Fact]
    public void The_Read_Only_Consent_Phrase_Is_Accepted_And_Never_Carried()
    {
        Assert.True(ProcessIoPumpReadyReadTrial.TryReadReadOnlyConsent(
            Read(ValidEnvironment()), out string? consentCode, out _, out _));

        Assert.NotNull(consentCode);
        Assert.StartsWith("consent-sha256-prefix:", consentCode);
        Assert.DoesNotContain(ProcessIoConsentPhrases.PhysicalDiAiRead, consentCode);
    }

    // ================================================================== catalogue

    [Fact]
    public void A_Missing_Catalogue_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(ProcessIoPumpReadyReadTrial.CatalogueVariable);

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedCatalogue, code);
    }

    [Fact]
    public void A_Relative_Catalogue_Path_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[ProcessIoPumpReadyReadTrial.CatalogueVariable] = "docs/evidence/process-io-inventory-2026-10-22.csv";

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedCatalogue, code);
    }

    [Fact]
    public void A_Missing_Catalogue_File_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[ProcessIoPumpReadyReadTrial.CatalogueVariable] =
            Path.Combine(RepoRoot(), "docs", "evidence", "no-such-catalogue.csv");

        Assert.False(ProcessIoPumpReadyReadTrial.TryLoadCatalogue(
            Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedCatalogue, code);
    }

    // ================================================================== thresholds and freshness

    [Fact]
    public void A_Missing_Setpoint_Refuses_And_Is_Never_Defaulted()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(CleaningPathDryComposition.PumpReadySetpointVariable);

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedThresholds, code);
    }

    [Fact]
    public void A_Non_Finite_Setpoint_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[CleaningPathDryComposition.PumpReadySetpointVariable] = "NaN";

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedThresholds, code);
    }

    [Fact]
    public void An_Inverted_Threshold_Pair_Refuses_With_The_Existing_Code()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[CleaningPathDryComposition.PressureLowVariable] = "20";
        environment[CleaningPathDryComposition.PressureHighVariable] = "10";

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(SequencingPressureThresholds.InvalidCode, code);
    }

    [Fact]
    public void A_Missing_Freshness_Budget_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment.Remove(ProcessIoPumpReadyReadTrial.FreshnessVariable);

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedFreshness, code);
    }

    [Fact]
    public void An_Out_Of_Range_Freshness_Budget_Refuses()
    {
        Dictionary<string, string?> environment = ValidEnvironment();
        environment[ProcessIoPumpReadyReadTrial.FreshnessVariable] = "0";

        Assert.False(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE, Read(environment), out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedFreshness, code);
    }

    [Fact]
    public void A_Valid_Configuration_Is_Accepted_Without_Contacting_Anything()
    {
        Assert.True(ProcessIoPumpReadyReadTrial.TryReadConfiguration(
            DeviceProfile.TEST_HARDWARE,
            Read(ValidEnvironment()),
            out PumpReadyReadConfiguration? configuration,
            out string code,
            out _));

        Assert.Empty(code);
        Assert.NotNull(configuration);
        Assert.Equal(15.0, configuration!.Thresholds.PumpReadySetpointBar);
        Assert.Equal(TimeSpan.FromSeconds(2), configuration.Freshness);
        Assert.Equal(ProcessIoPumpReadyReadTrial.ModeAi003Only, configuration.ModeLiteral);
        Assert.Equal(ProcessIoIsolationValves.PumpOutletPressureTag, configuration.PumpRow.Tag);
        Assert.DoesNotContain(ProcessIoConsentPhrases.PhysicalDiAiRead, configuration.ConsentCode);
        Assert.StartsWith("endpoint-sha256-prefix:", configuration.Endpoint.Fingerprint);
    }

    // ================================================================== the AI-003+ binding

    [Fact]
    public void A_Missing_Ai003_Row_Refuses_And_No_Other_Row_Is_Substituted()
    {
        Dictionary<string, ProcessIoCatalogueRow> catalogue = BuildCatalogue();
        catalogue.Remove(ProcessIoIsolationValves.PumpOutletPressureTag);

        Assert.False(ProcessIoPumpReadyReadTrial.TryValidateAi003Binding(catalogue, out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAi003Binding, code);
    }

    [Fact]
    public void An_Ai003_Row_That_Is_Not_A_Read_Input_Register_Refuses()
    {
        Dictionary<string, ProcessIoCatalogueRow> catalogue = BuildCatalogue();
        ProcessIoCatalogueRow row = catalogue[ProcessIoIsolationValves.PumpOutletPressureTag];
        Assert.NotNull(row.Binding);
        catalogue[ProcessIoIsolationValves.PumpOutletPressureTag] = row with
        {
            Binding = row.Binding! with { Area = ProcessIoArea.HoldingRegister, Direction = "WRITE" },
        };

        Assert.False(ProcessIoPumpReadyReadTrial.TryValidateAi003Binding(catalogue, out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAi003Binding, code);
    }

    [Fact]
    public void An_Unbound_Ai003_Row_Refuses()
    {
        Dictionary<string, ProcessIoCatalogueRow> catalogue = BuildCatalogue();
        catalogue[ProcessIoIsolationValves.PumpOutletPressureTag] =
            catalogue[ProcessIoIsolationValves.PumpOutletPressureTag] with
            {
                Binding = null,
                BindingState = ProcessIoBindingState.UNBOUND,
            };

        Assert.False(ProcessIoPumpReadyReadTrial.TryValidateAi003Binding(catalogue, out _, out string code, out _));
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAi003Binding, code);
    }

    [Fact]
    public void The_Adopted_Ai003_Row_Is_A_Physical_Read_Input_Register_On_The_Rack()
    {
        Assert.True(ProcessIoBindings.TryGet(ProcessIoIsolationValves.PumpOutletPressureTag, out ProcessIoPhysicalBinding binding));
        Assert.Equal(ProcessIoArea.InputRegister, binding.Area);
        Assert.Equal("READ", binding.Direction);
        Assert.Equal(ProcessIoDevices.RackT8, binding.DeviceKey);
        Assert.Equal("AI-003+", ProcessIoIsolationValves.PumpOutletPressureTag);
    }

    // ================================================================== acceptance and refusal

    [Fact]
    public void A_Pressure_Strictly_Above_The_Setpoint_Is_Accepted_As_Physical_Pump_Ready()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.True(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.Accepted, outcome.Code);
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal("AI-003+", outcome.Ai003.Tag);
    }

    [Fact]
    public void The_Raw_Word_Is_Reported_Unchanged()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.Equal(RawAbove, outcome.Ai003.RawValue);
    }

    [Fact]
    public void The_Engineering_Value_Is_The_Existing_Conversion_Of_The_Raw_Word()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        // No new formula: the reported bar is the Product's own conversion of the same raw word.
        Assert.Equal(ProcessIoEngineering.PumpOutletPressureBar(RawAbove), outcome.Ai003.EngineeringValue!.Value, 9);
        Assert.Equal("bar", outcome.Ai003.Unit);
    }

    [Fact]
    public void Quality_Freshness_And_Timestamps_Are_Reported_Truthfully()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.Equal(ProcessIoObservationQuality.GOOD, outcome.Ai003.Quality);
        Assert.Equal(ProcessIoObservationProvenance.PHYSICAL_READ, outcome.Ai003.Provenance);
        Assert.True(outcome.Ai003.Fresh);
        Assert.InRange(outcome.Ai003.ObservedAt, clock.UtcNow, clock.UtcNow);
        Assert.True(outcome.Ai003.ReceivedAt >= outcome.Ai003.ObservedAt);
    }

    [Fact]
    public void A_Slow_Read_Is_Stale_And_Is_Refused_Rather_Than_Accepted()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        // The transport consumes 60 ms of the injected clock; the budget is 20 ms.
        transport.DuringRead = () => clock.Advance(TimeSpan.FromMilliseconds(60));

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(
            Request(clock) with { Freshness = TimeSpan.FromMilliseconds(20) }, session);

        Assert.False(outcome.PumpReady);
        Assert.False(outcome.Ai003.Fresh);
        Assert.Equal(ProcessIoObservationQuality.STALE, outcome.Ai003.Quality);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedStale, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void Evidence_Stamped_Before_The_Judging_Clock_Is_Accepted_And_Future_Evidence_Is_Refused()
    {
        var clock = new TestClock();
        var request = Request(clock);

        PumpReadyReadOutcome current = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow.AddMilliseconds(-100), clock.UtcNow), null, 1, clock.UtcNow, request);
        Assert.True(current.PumpReady);

        PumpReadyReadOutcome future = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow.AddSeconds(5), clock.UtcNow), null, 1, clock.UtcNow, request);
        Assert.False(future.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedFuture, future.Code);
        Assert.Equal(2, future.ExitCode);
    }

    [Fact]
    public void Evidence_From_A_Superseded_Generation_Is_Refused()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow, clock.UtcNow) with { SourceGeneration = 9 },
            null,
            1,
            clock.UtcNow,
            Request(clock));

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedGeneration, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void The_Live_Session_Generation_Is_Reported_And_Matches_The_Observation()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.Equal(session.Generation, outcome.SessionGeneration);
        Assert.Equal(session.Generation, outcome.Ai003.SourceGeneration);
    }

    [Fact]
    public void A_Raw_Word_Outside_The_Representation_Domain_Is_Bad_And_Refused()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawOutOfDomain;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.False(outcome.PumpReady);
        Assert.Equal(RawOutOfDomain, outcome.Ai003.RawValue);
        Assert.Equal(ProcessIoObservationQuality.BAD, outcome.Ai003.Quality);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedNotUsable, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void A_Sample_Equal_To_The_Setpoint_Is_Refused_Because_The_Rule_Is_Strict()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(15.0, clock.UtcNow, clock.UtcNow), null, 1, clock.UtcNow, Request(clock));

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAtOrBelowSetpoint, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(15.0, outcome.Ai003.EngineeringValue!.Value, 6);
    }

    [Fact]
    public void A_Pressure_Below_The_Setpoint_Is_Refused()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawBelow;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAtOrBelowSetpoint, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
        Assert.True(outcome.Ai003.EngineeringValue < 15.0);
    }

    [Fact]
    public void An_Engineering_Value_Outside_The_Validity_Rule_Is_Refused_As_Invalid()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(-1.0, clock.UtcNow, clock.UtcNow), null, 1, clock.UtcNow, Request(clock));

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedInvalidSample, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void A_Simulated_Read_Is_Never_Physical_Evidence()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow, clock.UtcNow) with { Provenance = ProcessIoObservationProvenance.SIMULATED_READ },
            null,
            1,
            clock.UtcNow,
            Request(clock));

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedWrongProvenance, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void A_Disabled_Observation_Is_Refused_And_Never_Becomes_A_Reading()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(0.0, clock.UtcNow, clock.UtcNow) with
            {
                Quality = ProcessIoObservationQuality.DISABLED,
                Fresh = false,
                EngineeringValue = null,
            },
            null,
            1,
            clock.UtcNow,
            Request(clock));

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedNotUsable, outcome.Code);
        Assert.Null(outcome.Ai003.EngineeringValue);
    }

    // ================================================================== AI-002+ separation

    [Fact]
    public void Ai002_Cannot_Make_The_Trial_Pass_When_Ai003_Is_Below_The_Setpoint()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawBelow;
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpInletPressureTag)] = RawAbove;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAtOrBelowSetpoint, outcome.Code);
        Assert.NotNull(outcome.Ai002Diagnostic);
        Assert.True(outcome.Ai002Diagnostic!.EngineeringValue > 15.0);
    }

    [Fact]
    public void Ai002_Cannot_Substitute_For_A_Missing_Ai003_Reading()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpInletPressureTag)] = RawAbove;
        transport.ThrowOnRead = true;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.ReadTimeout, outcome.Code);
        Assert.Equal(3, outcome.ExitCode);
    }

    [Fact]
    public void Ai002_Is_Reported_As_Diagnostic_Only_And_Is_Not_The_Decision_Input()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpInletPressureTag)] = RawBelow;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.True(outcome.PumpReady);
        Assert.Equal("DIAGNOSTIC_ONLY_NOT_USED_FOR_DECISION", outcome.Ai002Role);
        Assert.NotNull(outcome.Ai002Diagnostic);
        Assert.Equal(ProcessIoIsolationValves.PumpInletPressureTag, outcome.Ai002Diagnostic!.Tag);

        // The two records are separate members and never share an identity.
        Assert.NotEqual(outcome.Ai003.Tag, outcome.Ai002Diagnostic.Tag);
    }

    [Fact]
    public void The_Decision_Uses_No_Source_Other_Than_The_Pump_Outlet_Identity()
    {
        Assert.Equal("AI-003+", ProcessIoIsolationValves.PumpOutletPressureTag);
        Assert.NotEqual(ProcessIoIsolationValves.PumpInletPressureTag, ProcessIoIsolationValves.PumpOutletPressureTag);
        Assert.Equal("PUMP_OUTLET", PressureSample.PumpOutletSource);

        // The existing rule refuses any other source identity, which is why the inlet can never decide.
        var wrongSource = new PressureSample(30.0, PressureQuality.GOOD, false, DateTimeOffset.UtcNow, "AI-002+");
        Assert.False(SequencingPressure.PumpReady(wrongSource, Thresholds));
    }

    // ================================================================== zero write

    [Fact]
    public void The_Trial_Contains_No_Write_Authority_Or_Write_Call()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        foreach (string forbidden in new[]
                 {
                     "ProcessIoWriteAuthorization", "AdmitWrite", "DispatchDigitalOutput",
                     "WriteCoil", "WriteSingleRegister", "WriteRegisters", "RequestValve",
                     "SafeCleanupCloseAll", "ProcessIoEquipmentBoundary", "ProcessIoDryCommandGate",
                 })
        {
            Assert.DoesNotContain(forbidden, trial);
        }
    }

    [Fact]
    public void A_Read_Trial_Writes_Nothing_To_The_Adopted_Rack()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.True(outcome.PumpReady);
        Assert.Empty(transport.CoilWrites);
        Assert.Empty(transport.RegisterWrites);
        Assert.DoesNotContain(session.AuditTrail, entry => entry.Contains("DISPATCH", StringComparison.Ordinal));
    }

    [Fact]
    public void No_Isolation_Valve_Output_Is_Touched_By_A_Read_Trial()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.DoesNotContain(transport.CoilWrites, write => write.Address >= ProcessIoIsolationValves.CommandCoilAddress(1));
    }

    // ================================================================== zero motion and no simulation

    [Fact]
    public void The_Trial_Composes_No_Motion_Queue_Or_Simulation()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        foreach (string forbidden in new[]
                 {
                     "MotionCompositionRoot", "ManualMotion", "MotionLibrary", "NativeKernel",
                     "DllImport", "LibraryImport", "NativeLibrary", "gclib",
                     "SimulatorRuntime", "SimulatedProcessIoTransport", "Queue", "TimeDelay", "Home",
                     "Application.Run", "MessageBox",
                 })
        {
            Assert.DoesNotContain(forbidden, trial);
        }
    }

    [Fact]
    public void No_Simulation_Fallback_Exists_When_The_Physical_Read_Faults()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.ThrowOnRead = true;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        // The fault never becomes a simulated or a defaulted GOOD value.
        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoObservationProvenance.NOT_OBSERVED, outcome.Ai003.Provenance);
        Assert.Null(outcome.Ai003.EngineeringValue);
        Assert.Equal(ProcessIoPumpReadyReadTrial.ReadTimeout, outcome.Code);
    }

    [Fact]
    public void A_Transport_Fault_Is_A_Typed_Bounded_Timeout_And_Not_A_Reading()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.ThrowOnRead = true;

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal(ProcessIoObservationQuality.BAD, outcome.Ai003.Quality);
        Assert.Equal(ProcessIoRefusals.ObservationTransportFault, outcome.Ai003.DiagnosticCode);
    }

    // ================================================================== lifecycle

    [Fact]
    public void A_Closed_Session_Is_Refused_Before_Any_Read()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        session.Close(clock.UtcNow);

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedSessionNotOpen, outcome.Code);
        Assert.Equal(4, outcome.ExitCode);
        Assert.Equal(0, transport.ReadAttempts);
    }

    [Fact]
    public void The_Observation_Leaves_The_Session_Open_For_The_Caller_To_Close_Last()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.Equal("OPEN", session.State);
    }

    [Fact]
    public void The_Run_Lifecycle_Closes_The_Session_And_Disposes_The_Transport_Afterwards()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        Assert.Equal(1, CountOccurrences(trial, "transport.Dispose()"));
        Assert.Equal(1, CountOccurrences(trial, "session.Close("));
        Assert.True(
            trial.IndexOf("session.Close(", StringComparison.Ordinal)
            < trial.IndexOf("transport.Dispose()", StringComparison.Ordinal),
            "the transport must be released after the session closes");
    }

    [Fact]
    public void A_Configuration_Refusal_Never_Constructs_A_Transport()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        // The construction site appears exactly once, and it is after the whole configuration gate.
        Assert.Equal(1, CountOccurrences(trial, "new ProcessIoPhysicalTransport("));
        Assert.True(
            trial.IndexOf("TryReadConfiguration(", StringComparison.Ordinal)
            < trial.IndexOf("new ProcessIoPhysicalTransport(", StringComparison.Ordinal),
            "configuration must be validated before a transport exists");
    }

    // ================================================================== host routing and surface

    [Fact]
    public void The_Host_Routes_The_Read_Trial_And_Keeps_The_Other_Paths_Intact()
    {
        string program = ReadSource("Program.cs");

        int readTrial = program.IndexOf("return ProcessIoPumpReadyReadTrial.Run(", StringComparison.Ordinal);
        int ivTrial = program.IndexOf("return DryIvTrial.Run(", StringComparison.Ordinal);
        int dry = program.IndexOf("return CleaningPathDryTrial.Run(", StringComparison.Ordinal);
        int simulator = program.IndexOf("SimulatorRuntime.Create(options", StringComparison.Ordinal);

        Assert.True(readTrial > 0, "the read-only Pump-ready trial must be routed by the host");
        Assert.True(ivTrial > 0 && ivTrial < readTrial, "the IV trial keeps its own earlier branch");
        Assert.True(dry > readTrial, "the dry branch keeps its own later branch");
        Assert.True(simulator > dry, "the dry branch must still return before the simulator composition");
        Assert.Contains("if (pumpReadyReadSelected)", program);
        Assert.Contains("ProcessIoPumpReadyReadTrial.TryReadMode", program);
    }

    [Fact]
    public void The_Read_Trial_Adds_No_Route_No_Listener_And_No_Kiosk_Control()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        foreach (string forbidden in new[] { "MapPost", "MapPut", "MapPatch", "MapDelete", "CreateSlimBuilder" })
        {
            Assert.DoesNotContain(forbidden, trial);
        }
    }

    [Fact]
    public void The_Trial_Declares_The_Published_Evidence_Contract()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        foreach (string required in new[]
                 {
                     "PHYSICAL_PUMP_READY_READ_ONLY", "PHYSICAL",
                     "DIAGNOSTIC_ONLY_NOT_USED_FOR_DECISION",
                     "physical_pump_verified", "physical_ai003_verified", "pump_ready",
                     "raw_value", "engineering_bar", "observed_at", "session_generation",
                     "configured_setpoint_bar", "strict_comparison",
                     "iv_command_executed", "motion_executed", "waterjet_command_executed",
                 })
        {
            Assert.Contains(required, trial);
        }
    }

    [Fact]
    public void Physical_Verification_Is_Never_Claimed_On_A_Failed_Run()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawBelow;

        PumpReadyReadOutcome failed = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);
        Assert.False(failed.PumpReady);
        Assert.Equal(2, failed.ExitCode);

        // The report derives both verification flags from the verdict, never from the run having happened.
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");
        Assert.Contains("physical_pump_verified=\" + Flag(outcome.PumpReady)", trial);
        Assert.Contains("physical_ai003_verified=\" + Flag(outcome.PumpReady)", trial);
    }

    // ================================================================== helpers

    private static Func<string, string?> Read(Dictionary<string, string?> values) =>
        key => values.GetValueOrDefault(key);

    private static Dictionary<string, string?> ValidEnvironment() => new(StringComparer.Ordinal)
    {
        [ProcessIoPumpReadyReadTrial.ModeVariable] = ProcessIoPumpReadyReadTrial.ModeAi003Only,
        [ProcessIoEndpointConfiguration.HostVariable] = BenchHost,
        [ProcessIoEndpointConfiguration.PortVariable] = "502",
        [ProcessIoEndpointConfiguration.UnitIdVariable] = "1",
        [ProcessIoEndpointConfiguration.ConsentVariable] = ProcessIoConsentPhrases.PhysicalDiAiRead,
        [ProcessIoPumpReadyReadTrial.CatalogueVariable] = CataloguePath(),
        [CleaningPathDryComposition.PressureLowVariable] = "1",
        [CleaningPathDryComposition.PressureHighVariable] = "15",
        [CleaningPathDryComposition.PumpReadySetpointVariable] = "15",
        [ProcessIoPumpReadyReadTrial.FreshnessVariable] = "2000",
    };

    private static PumpReadyReadRequest Request(TestClock clock)
    {
        Dictionary<string, ProcessIoCatalogueRow> catalogue = BuildCatalogue();
        return new PumpReadyReadRequest
        {
            PumpRow = catalogue[ProcessIoIsolationValves.PumpOutletPressureTag],
            Catalogue = catalogue,
            Thresholds = Thresholds,
            Freshness = Freshness,
            Time = clock,
        };
    }

    private static (ProcessIoSession Session, FakeTransport Transport) CreateSession()
    {
        var transport = new FakeTransport();
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
        session.Open(DateTimeOffset.UtcNow);
        return (session, transport);
    }

    /// <summary>
    /// The committed, generator-owned catalogue, loaded by the existing loader. Using the real file means
    /// the tests exercise the adopted binding rather than a hand-written one.
    /// </summary>
    private static Dictionary<string, ProcessIoCatalogueRow> BuildCatalogue()
    {
        IReadOnlyList<ProcessIoCatalogueRow> rows = ProcessIoCatalogueLoader.Load(File.ReadAllText(CataloguePath()));
        var index = new Dictionary<string, ProcessIoCatalogueRow>(StringComparer.Ordinal);
        foreach (ProcessIoCatalogueRow row in rows)
        {
            index[row.Tag] = row;
        }

        return index;
    }

    private static string CataloguePath() =>
        Path.Combine(RepoRoot(), "docs", "evidence", "process-io-inventory-2026-10-22.csv");

    private static int AddressOf(string tag) =>
        ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding)
            ? binding.Address
            : throw new InvalidOperationException($"No adopted binding exists for {tag}.");

    /// <summary>
    /// A synthetic, already-classified physical observation. It exists so the pure decision can be driven
    /// with a value or an instant that a real transport cannot be asked to produce on demand.
    /// </summary>
    private static ProcessIoPhysicalObservation Observe(
        int rawWord,
        DateTimeOffset observedAt,
        DateTimeOffset receivedAt) => new()
        {
            Tag = ProcessIoIsolationValves.PumpOutletPressureTag,
            RawValue = rawWord,
            EngineeringValue = ProcessIoEngineering.PumpOutletPressureBar(rawWord),
            Unit = "bar",
            Quality = ProcessIoObservationQuality.GOOD,
            ObservedAt = observedAt,
            ReceivedAt = receivedAt,
            SourceGeneration = 1,
            Fresh = true,
            Provenance = ProcessIoObservationProvenance.PHYSICAL_READ,
            DiagnosticCode = "OK",
        };

    /// <summary>An observation whose engineering value is stated directly, for boundary values.</summary>
    private static ProcessIoPhysicalObservation Observe(
        double engineeringBar,
        DateTimeOffset observedAt,
        DateTimeOffset receivedAt) => Observe(0, observedAt, receivedAt) with
        {
            EngineeringValue = engineeringBar,
        };

    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "apps", "runtime", fileName));

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count += 1;
            index += value.Length;
        }

        return count;
    }

    private static string RepoRoot()
    {
        var location = typeof(ProcessIoPumpReadyReadTrialTests).Assembly.Location;
        var startDirectory = string.IsNullOrEmpty(location)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(location)!;

        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Repository root (WaterJetSentinelSuite.sln) not found above the test assembly directory.");
    }

    /// <summary>A clock the test moves on purpose, so freshness is a fact rather than a timing race.</summary>
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;

        public void Advance(TimeSpan delta) => UtcNow += delta;
    }

    /// <summary>
    /// Deterministic in-memory double for the adopted bench transport. It is not the Product simulation
    /// transport: a physical read must never be backed by one, so this stands in for the field device and
    /// records every attempt instead.
    /// </summary>
    private sealed class FakeTransport : IProcessIoTransport
    {
        public List<(string Device, int Address, bool Value)> CoilWrites { get; } = new();

        public List<(string Device, int Address, ushort Value)> RegisterWrites { get; } = new();

        public Dictionary<int, bool> DiscreteInputs { get; } = new();

        public Dictionary<int, ushort> InputRegisters { get; } = new();

        public Dictionary<int, bool> Coils { get; } = new();

        public bool ThrowOnRead { get; set; }

        /// <summary>Runs inside the read, so a test can consume clock time where a real device would.</summary>
        public Action? DuringRead { get; set; }

        public int ReadAttempts { get; private set; }

        public int NetworkAttempts => ReadAttempts + CoilWrites.Count + RegisterWrites.Count;

        public bool ReadDiscreteInput(string deviceKey, int address)
        {
            OnRead();
            return DiscreteInputs.TryGetValue(address, out bool value) && value;
        }

        public bool ReadCoil(string deviceKey, int coilAddress)
        {
            OnRead();
            return Coils.TryGetValue(coilAddress, out bool value) && value;
        }

        public IReadOnlyList<bool> ReadCoilsBulk(string deviceKey, int startAddress, int quantity)
        {
            OnRead();
            return Enumerable.Range(startAddress, quantity)
                .Select(address => Coils.TryGetValue(address, out bool value) && value)
                .ToList();
        }

        public ushort ReadInputRegister(string deviceKey, int registerAddress)
        {
            OnRead();
            return InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;
        }

        public ushort ReadRegister(string deviceKey, int registerAddress)
        {
            OnRead();
            return InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;
        }

        public IReadOnlyList<ushort> ReadHoldingRegistersBulk(string deviceKey, int startAddress, int quantity)
        {
            OnRead();
            return Enumerable.Range(startAddress, quantity)
                .Select(address => InputRegisters.TryGetValue(address, out ushort value) ? value : (ushort)0)
                .ToList();
        }

        public void WriteCoil(string deviceKey, int coilAddress, bool value)
        {
            CoilWrites.Add((deviceKey, coilAddress, value));
            Coils[coilAddress] = value;
        }

        public void WriteSingleRegister(string deviceKey, int registerAddress, ushort value) =>
            RegisterWrites.Add((deviceKey, registerAddress, value));

        public void WriteRegisters(string deviceKey, int startAddress, IReadOnlyList<ushort> values)
        {
            for (int index = 0; index < values.Count; index += 1)
            {
                RegisterWrites.Add((deviceKey, startAddress + index, values[index]));
            }
        }

        private void OnRead()
        {
            ReadAttempts += 1;
            if (ThrowOnRead)
            {
                throw new IOException("FAKE_READ_TIMEOUT");
            }

            DuringRead?.Invoke();
        }
    }
}
