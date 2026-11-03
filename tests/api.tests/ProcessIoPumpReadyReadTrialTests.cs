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
    private const ushort RawAbove = 24000;

    /// <summary>Raw word whose Product conversion is about 14.6 bar, below the 15 bar setpoint.</summary>
    private const ushort RawBelow = 12000;

    /// <summary>A raw word outside the adopted representation domain (full scale is 32767).</summary>
    private const ushort RawOutOfDomain = 40000;

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

        Assert.True(outcome.PhysicalAi003Verified);
        Assert.True(outcome.PumpReady);
        Assert.Equal("AI-003+", outcome.Ai003.Tag);
        Assert.Equal(ProcessIoPumpReadyReadTrial.Accepted, outcome.Code);
        Assert.Equal(0, outcome.ExitCode);

        // Verification and Pump-readiness agree only here, because the pressure is above the setpoint.
        Assert.True(outcome.PhysicalAi003Verified && outcome.PumpReady);
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

        // A read that consumed no clock time is GOOD and fresh, and its two instants are the same.
        Assert.Equal(ProcessIoObservationQuality.GOOD, outcome.Ai003.Quality);
        Assert.Equal(ProcessIoObservationProvenance.PHYSICAL_READ, outcome.Ai003.Provenance);
        Assert.True(outcome.Ai003.Fresh);
        Assert.Equal(clock.UtcNow, outcome.Ai003.ObservedAt);
        Assert.Equal(clock.UtcNow, outcome.Ai003.ReceivedAt);
        Assert.True(outcome.Ai003.ReceivedAt >= outcome.Ai003.ObservedAt);
        Assert.Equal(TimeSpan.Zero, outcome.Ai003.ReceivedAt - outcome.Ai003.ObservedAt);
    }

    [Fact]
    public void A_Fast_Read_Inside_The_Budget_Brackets_The_Transport_Call()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        // 5 ms elapsed inside the transport read, against a 2 s budget.
        transport.DuringRead = () => clock.Advance(TimeSpan.FromMilliseconds(5));

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.True(outcome.Ai003.Fresh);
        Assert.Equal(ProcessIoObservationQuality.GOOD, outcome.Ai003.Quality);
        Assert.Equal(TimeSpan.FromMilliseconds(5), outcome.Ai003.ReceivedAt - outcome.Ai003.ObservedAt);
        Assert.True(outcome.PhysicalAi003Verified);
        Assert.True(outcome.PumpReady);
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

        // The elapsed read time is now visible in the record, not collapsed to zero.
        Assert.Equal(TimeSpan.FromMilliseconds(60), outcome.Ai003.ReceivedAt - outcome.Ai003.ObservedAt);

        Assert.False(outcome.Ai003.Fresh);
        Assert.Equal(ProcessIoObservationQuality.STALE, outcome.Ai003.Quality);
        Assert.Equal(ProcessIoRefusals.ObservationStale, outcome.Ai003.DiagnosticCode);
        Assert.False(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedStale, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void The_Freshness_Budget_Is_Inclusive_At_Its_Exact_Boundary()
    {
        // The accepted contract is "received - observed <= budget", so exactly at the boundary is fresh.
        // Only the acquisition read is slowed, so the diagnostic read cannot move the judging instant.
        int pumpOutletAddress = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);

        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[pumpOutletAddress] = RawAbove;
        transport.DuringRegisterRead = address =>
        {
            if (address == pumpOutletAddress)
            {
                clock.Advance(TimeSpan.FromMilliseconds(20));
            }
        };

        PumpReadyReadOutcome atBoundary = ProcessIoPumpReadyReadTrial.Observe(
            Request(clock) with { Freshness = TimeSpan.FromMilliseconds(20) }, session);

        Assert.Equal(TimeSpan.FromMilliseconds(20), atBoundary.Ai003.ReceivedAt - atBoundary.Ai003.ObservedAt);
        Assert.True(atBoundary.Ai003.Fresh);
        Assert.Equal(ProcessIoObservationQuality.GOOD, atBoundary.Ai003.Quality);
        Assert.True(atBoundary.PhysicalAi003Verified);
        Assert.True(atBoundary.PumpReady);

        // One millisecond beyond it is stale, with no special case in between.
        var beyondClock = new TestClock();
        (ProcessIoSession beyondSession, FakeTransport beyondTransport) = CreateSession();
        beyondTransport.InputRegisters[pumpOutletAddress] = RawAbove;
        beyondTransport.DuringRegisterRead = address =>
        {
            if (address == pumpOutletAddress)
            {
                beyondClock.Advance(TimeSpan.FromMilliseconds(21));
            }
        };

        PumpReadyReadOutcome beyond = ProcessIoPumpReadyReadTrial.Observe(
            Request(beyondClock) with { Freshness = TimeSpan.FromMilliseconds(20) }, beyondSession);

        Assert.False(beyond.Ai003.Fresh);
        Assert.Equal(ProcessIoObservationQuality.STALE, beyond.Ai003.Quality);
        Assert.False(beyond.PhysicalAi003Verified);
        Assert.False(beyond.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedStale, beyond.Code);
        Assert.Equal(2, beyond.ExitCode);
    }

    [Fact]
    public void The_Session_Generation_Stays_Truthful_Across_A_Slow_Read()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;
        transport.DuringRead = () => clock.Advance(TimeSpan.FromMilliseconds(60));

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(
            Request(clock) with { Freshness = TimeSpan.FromMilliseconds(20) }, session);

        // Staleness is a freshness verdict, not a generation verdict: both still name the live session.
        Assert.False(outcome.Ai003.Fresh);
        Assert.Equal(session.Generation, outcome.Ai003.SourceGeneration);
        Assert.Equal(session.Generation, outcome.SessionGeneration);
    }

    [Fact]
    public void Evidence_Stamped_Before_The_Judging_Clock_Is_Accepted_And_Future_Evidence_Is_Refused()
    {
        var clock = new TestClock();
        var request = Request(clock);

        PumpReadyReadOutcome current = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow.AddMilliseconds(-100), clock.UtcNow), null, 1, clock.UtcNow, request);
        Assert.True(current.PhysicalAi003Verified);
        Assert.True(current.PumpReady);

        PumpReadyReadOutcome future = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow.AddSeconds(5), clock.UtcNow), null, 1, clock.UtcNow, request);
        Assert.False(future.PhysicalAi003Verified);
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
        Assert.False(outcome.PhysicalAi003Verified);
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
        Assert.False(outcome.PhysicalAi003Verified);
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

        // The acquisition is verified; the strict comparison is not satisfied. These are two facts.
        Assert.True(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.False(outcome.PhysicalAi003Verified && outcome.PumpReady);
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

        // A correctly acquired low reading is still a verified acquisition. It is not Pump-ready.
        Assert.True(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.False(outcome.PhysicalAi003Verified && outcome.PumpReady);
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

        // The acquisition chain completed (a finite value arrived from the wire), so the acquisition is
        // verified; the value then fails the existing sample rule, so Pump-ready is not claimed.
        Assert.True(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.False(outcome.PhysicalAi003Verified && outcome.PumpReady);
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
        Assert.False(outcome.PhysicalAi003Verified);
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
        Assert.False(outcome.PhysicalAi003Verified);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedNotUsable, outcome.Code);
        Assert.Null(outcome.Ai003.EngineeringValue);
    }

    // ================================================================== separated evidence verdicts

    [Fact]
    public void A_Wrong_Source_Tag_Is_Never_Verified_Physical_Ai003_Evidence()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow, clock.UtcNow) with { Tag = "AI-004+" },
            null,
            1,
            clock.UtcNow,
            Request(clock));

        Assert.False(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedWrongSourceTag, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void A_Missing_Engineering_Value_Is_Never_Verified_Physical_Ai003_Evidence()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow, clock.UtcNow) with { EngineeringValue = null, Unit = null },
            null,
            1,
            clock.UtcNow,
            Request(clock));

        // Everything else about the record is sound; without a value in bar there is nothing verified.
        Assert.False(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedNotUsable, outcome.Code);
    }

    [Fact]
    public void A_Non_Finite_Engineering_Value_Is_Never_Verified_Physical_Ai003_Evidence()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow, clock.UtcNow) with { EngineeringValue = double.NaN },
            null,
            1,
            clock.UtcNow,
            Request(clock));

        Assert.False(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedNotUsable, outcome.Code);
    }

    [Fact]
    public void Verification_Does_Not_Depend_On_The_Setpoint_Only_On_The_Acquisition()
    {
        var clock = new TestClock();

        // The same, sound observation is verified identically whatever the Owner's setpoint is.
        foreach (double setpoint in new[] { 0.0, 5.0, 15.0, 30.0 })
        {
            PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
                Observe(RawAbove, clock.UtcNow, clock.UtcNow),
                null,
                1,
                clock.UtcNow,
                Request(clock) with { Thresholds = new SequencingPressureThresholds(1.0, setpoint + 2, setpoint) });

            Assert.True(outcome.PhysicalAi003Verified);
        }
    }

    [Fact]
    public void Ai002_Never_Affects_Any_Of_The_Three_Flags()
    {
        var clock = new TestClock();

        PumpReadyReadOutcome withHighInlet = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow, clock.UtcNow),
            InletObservation(RawAbove, clock.UtcNow),
            1,
            clock.UtcNow,
            Request(clock));

        PumpReadyReadOutcome withLowInlet = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawAbove, clock.UtcNow, clock.UtcNow),
            InletObservation(RawBelow, clock.UtcNow),
            1,
            clock.UtcNow,
            Request(clock));

        // The inlet differs; none of the three verdicts may.
        Assert.Equal(withHighInlet.PhysicalAi003Verified, withLowInlet.PhysicalAi003Verified);
        Assert.Equal(withHighInlet.PumpReady, withLowInlet.PumpReady);
        Assert.Equal(
            withHighInlet.PhysicalAi003Verified && withHighInlet.PumpReady,
            withLowInlet.PhysicalAi003Verified && withLowInlet.PumpReady);

        // And a high inlet cannot lift a below-setpoint outlet.
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawBelow;
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpInletPressureTag)] = RawAbove;
        PumpReadyReadOutcome outletBelow = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.True(outletBelow.PhysicalAi003Verified);
        Assert.False(outletBelow.PumpReady);
        Assert.False(outletBelow.PhysicalAi003Verified && outletBelow.PumpReady);
    }

    [Fact]
    public void The_Exit_Code_Still_Follows_Acquisition_And_Not_Verification_Alone()
    {
        var clock = new TestClock();
        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Classify(
            Observe(RawBelow, clock.UtcNow, clock.UtcNow),
            null,
            1,
            clock.UtcNow,
            Request(clock));

        // Verified acquisition with an unmet setpoint is exit 2, never 0.
        Assert.True(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAtOrBelowSetpoint, outcome.Code);
    }

    [Fact]
    public void Diagnostic_Freshness_Never_Changes_The_Three_Verdicts()
    {
        // The 60 ms diagnostic delay must be measured against a budget it can actually exceed, so both
        // runs use the narrow budget and the difference under test is real.
        var fastClock = new TestClock();
        (ProcessIoSession fastSession, FakeTransport fastTransport) = CreateSession();
        fastTransport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;
        fastTransport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpInletPressureTag)] = RawBelow;
        PumpReadyReadOutcome fastInlet =
            ProcessIoPumpReadyReadTrial.Observe(Request(fastClock, NarrowBudget), fastSession);

        var slowClock = new TestClock();
        (ProcessIoSession slowSession, FakeTransport slowTransport) = CreateSession();
        slowTransport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;
        slowTransport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpInletPressureTag)] = RawBelow;
        slowTransport.DuringRegisterRead = SlowOnly(slowClock, AddressOf(ProcessIoIsolationValves.PumpInletPressureTag), 60);
        PumpReadyReadOutcome slowInlet =
            ProcessIoPumpReadyReadTrial.Observe(Request(slowClock, NarrowBudget), slowSession);

        // The diagnostic reading genuinely differs in freshness; none of the three verdicts may move.
        Assert.True(fastInlet.Ai002Diagnostic!.Fresh);
        Assert.False(slowInlet.Ai002Diagnostic!.Fresh);
        Assert.Equal(fastInlet.PhysicalAi003Verified, slowInlet.PhysicalAi003Verified);
        Assert.Equal(fastInlet.PumpReady, slowInlet.PumpReady);
        Assert.Equal(
            fastInlet.PhysicalAi003Verified && fastInlet.PumpReady,
            slowInlet.PhysicalAi003Verified && slowInlet.PumpReady);
        Assert.Equal(fastInlet.Code, slowInlet.Code);
        Assert.Equal(fastInlet.ExitCode, slowInlet.ExitCode);
    }

    // ================================================================== the session overload contract

    [Fact]
    public void The_Timestamp_Overload_Retains_Its_Caller_Supplied_Contract()
    {
        // The pre-existing overload must keep behaving exactly as before for every current caller: the
        // verdict is derived only from the two instants the caller states, with no clock consulted.
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;
        ProcessIoCatalogueRow row = BuildCatalogue()[ProcessIoIsolationValves.PumpOutletPressureTag];
        var at = new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);

        ProcessIoPhysicalObservation same = session.ObserveAnalogInput(
            ProcessIoIsolationValves.PumpOutletPressureTag, row, at, at, TimeSpan.FromSeconds(2));
        Assert.True(same.Fresh);
        Assert.Equal(ProcessIoObservationQuality.GOOD, same.Quality);
        Assert.Equal("OK", same.DiagnosticCode);
        Assert.Equal(at, same.ObservedAt);
        Assert.Equal(at, same.ReceivedAt);

        ProcessIoPhysicalObservation slow = session.ObserveAnalogInput(
            ProcessIoIsolationValves.PumpOutletPressureTag,
            row,
            at,
            at.AddMilliseconds(60),
            TimeSpan.FromMilliseconds(20));
        Assert.False(slow.Fresh);
        Assert.Equal(ProcessIoObservationQuality.STALE, slow.Quality);
        Assert.Equal(ProcessIoRefusals.ObservationStale, slow.DiagnosticCode);
        Assert.Equal(at, slow.ObservedAt);
        Assert.Equal(at.AddMilliseconds(60), slow.ReceivedAt);

        // A refused row is still refused identically, and the caller's instants are still the record's.
        ProcessIoPhysicalObservation refused = session.ObserveAnalogInput(
            ProcessIoIsolationValves.PumpOutletPressureTag,
            null,
            at,
            at.AddMilliseconds(5),
            TimeSpan.FromSeconds(2));
        Assert.False(refused.Fresh);
        Assert.Equal(ProcessIoObservationQuality.BAD, refused.Quality);
        Assert.Equal(ProcessIoObservationProvenance.NOT_OBSERVED, refused.Provenance);
        Assert.Equal(ProcessIoRefusals.ObservationRefusedUnbound, refused.DiagnosticCode);
        Assert.Equal(at, refused.ObservedAt);
        Assert.Equal(at.AddMilliseconds(5), refused.ReceivedAt);
    }

    [Fact]
    public void The_Clock_Overload_Rejects_A_Missing_Clock_Before_Any_Read()
    {
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        ProcessIoCatalogueRow row = BuildCatalogue()[ProcessIoIsolationValves.PumpOutletPressureTag];

        Assert.Throws<ArgumentNullException>(() => session.ObserveAnalogInput(
            ProcessIoIsolationValves.PumpOutletPressureTag, row, (TimeProvider)null!, TimeSpan.FromSeconds(2)));
        Assert.Equal(0, transport.ReadAttempts);
    }

    [Fact]
    public void The_Trial_Hands_The_Clock_To_The_Session_Rather_Than_Sampling_Timestamps()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        // Both observations - the acquisition and the diagnostic - use the clock overload.
        Assert.Equal(2, CountOccurrences(trial, "session.ObserveAnalogInput("));
        Assert.Equal(2, CountOccurrences(trial, "request.Time,"));

        // Exactly one instant is sampled by the trial itself: the judging instant, after the reads.
        Assert.Equal(1, CountOccurrences(trial, "request.Time.GetUtcNow()"));
    }

    // ================================================================== diagnostic-path freshness

    [Fact]
    public void A_Fast_Diagnostic_Read_Has_Truthful_Timestamps()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        int outlet = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);
        int inlet = AddressOf(ProcessIoIsolationValves.PumpInletPressureTag);
        transport.InputRegisters[outlet] = RawAbove;
        transport.InputRegisters[inlet] = RawBelow;
        transport.DuringRegisterRead = SlowOnly(clock, inlet, 5);

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        ProcessIoPhysicalObservation diagnostic = outcome.Ai002Diagnostic!;
        Assert.True(diagnostic.Fresh);
        Assert.Equal(ProcessIoObservationQuality.GOOD, diagnostic.Quality);
        Assert.Equal(TimeSpan.FromMilliseconds(5), diagnostic.ReceivedAt - diagnostic.ObservedAt);
        Assert.True(diagnostic.ObservedAt <= diagnostic.ReceivedAt);
        Assert.Equal(ProcessIoObservationProvenance.PHYSICAL_READ, diagnostic.Provenance);
        Assert.Equal(session.Generation, diagnostic.SourceGeneration);
    }

    [Fact]
    public void A_Diagnostic_Read_Inside_A_Wide_Budget_Is_Fresh()
    {
        // 60 ms of diagnostic latency against a 2 s budget is comfortably inside the contract, so the
        // reading is fresh. Expecting stale here would be a test defect, not a Product defect.
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        int outlet = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);
        int inlet = AddressOf(ProcessIoIsolationValves.PumpInletPressureTag);
        transport.InputRegisters[outlet] = RawAbove;
        transport.InputRegisters[inlet] = RawBelow;
        transport.DuringRegisterRead = SlowOnly(clock, inlet, 60);

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.NotNull(outcome.Ai002Diagnostic);
        ProcessIoPhysicalObservation diagnostic = outcome.Ai002Diagnostic!;
        Assert.True(diagnostic.Fresh);
        Assert.Equal(ProcessIoObservationQuality.GOOD, diagnostic.Quality);
        Assert.Equal("OK", diagnostic.DiagnosticCode);
        Assert.Equal(TimeSpan.FromMilliseconds(60), diagnostic.ReceivedAt - diagnostic.ObservedAt);
    }

    [Fact]
    public void A_Diagnostic_Read_Beyond_A_Narrow_Budget_Is_Stale()
    {
        // The same 60 ms against a 20 ms budget is outside it, so the diagnostic reports its own stale
        // span and its own code.
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        int outlet = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);
        int inlet = AddressOf(ProcessIoIsolationValves.PumpInletPressureTag);
        transport.InputRegisters[outlet] = RawAbove;
        transport.InputRegisters[inlet] = RawBelow;
        transport.DuringRegisterRead = SlowOnly(clock, inlet, 60);

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(
            Request(clock, NarrowBudget), session);

        Assert.NotNull(outcome.Ai002Diagnostic);
        ProcessIoPhysicalObservation diagnostic = outcome.Ai002Diagnostic!;
        Assert.False(diagnostic.Fresh);
        Assert.Equal(ProcessIoObservationQuality.STALE, diagnostic.Quality);
        Assert.Equal(ProcessIoRefusals.ObservationStale, diagnostic.DiagnosticCode);
        Assert.Equal(TimeSpan.FromMilliseconds(60), diagnostic.ReceivedAt - diagnostic.ObservedAt);
    }

    [Fact]
    public void A_Stale_Diagnostic_Cannot_Make_A_Fast_Ai003_Stale()
    {
        // The discriminating case. AI-003 is fast and above the setpoint; the optional diagnostic read
        // then burns 60 ms against a 20 ms budget. If the decision were still taken after that read, the
        // acquisition would be aged out and refused. Deciding first keeps it accepted.
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        int outlet = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);
        int inlet = AddressOf(ProcessIoIsolationValves.PumpInletPressureTag);
        transport.InputRegisters[outlet] = RawAbove;
        transport.InputRegisters[inlet] = RawBelow;
        transport.DuringRegisterRead = SlowOnly(clock, inlet, 60);

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(
            Request(clock, NarrowBudget), session);

        Assert.True(outcome.PhysicalAi003Verified);
        Assert.True(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.Accepted, outcome.Code);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.Ai003.Fresh);

        // The diagnostic itself is stale, and reporting that is the whole point of reading it.
        Assert.False(outcome.Ai002Diagnostic!.Fresh);
    }

    [Fact]
    public void Diagnostic_Freshness_Never_Changes_Any_Decision_Field()
    {
        int outlet = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);
        int inlet = AddressOf(ProcessIoIsolationValves.PumpInletPressureTag);

        // Fast diagnostic, narrow budget.
        var fastClock = new TestClock();
        (ProcessIoSession fastSession, FakeTransport fastTransport) = CreateSession();
        fastTransport.InputRegisters[outlet] = RawAbove;
        fastTransport.InputRegisters[inlet] = RawBelow;
        PumpReadyReadOutcome fast =
            ProcessIoPumpReadyReadTrial.Observe(Request(fastClock, NarrowBudget), fastSession);

        // Stale diagnostic, same narrow budget and the same AI-003 reading.
        var slowClock = new TestClock();
        (ProcessIoSession slowSession, FakeTransport slowTransport) = CreateSession();
        slowTransport.InputRegisters[outlet] = RawAbove;
        slowTransport.InputRegisters[inlet] = RawBelow;
        slowTransport.DuringRegisterRead = SlowOnly(slowClock, inlet, 60);
        PumpReadyReadOutcome slow =
            ProcessIoPumpReadyReadTrial.Observe(Request(slowClock, NarrowBudget), slowSession);

        Assert.True(fast.Ai002Diagnostic!.Fresh);
        Assert.False(slow.Ai002Diagnostic!.Fresh);

        // The four decision fields and the reported generation are identical.
        Assert.Equal(fast.PhysicalAi003Verified, slow.PhysicalAi003Verified);
        Assert.Equal(fast.PumpReady, slow.PumpReady);
        Assert.Equal(fast.Code, slow.Code);
        Assert.Equal(fast.ExitCode, slow.ExitCode);
        Assert.Equal(fast.SessionGeneration, slow.SessionGeneration);
        Assert.Equal(
            fast.PhysicalAi003Verified && fast.PumpReady,
            slow.PhysicalAi003Verified && slow.PumpReady);
    }

    [Fact]
    public void A_Below_Setpoint_Ai003_Stays_Refused_Whatever_The_Diagnostic_Does()
    {
        int outlet = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);
        int inlet = AddressOf(ProcessIoIsolationValves.PumpInletPressureTag);

        var fastClock = new TestClock();
        (ProcessIoSession fastSession, FakeTransport fastTransport) = CreateSession();
        fastTransport.InputRegisters[outlet] = RawBelow;
        fastTransport.InputRegisters[inlet] = RawAbove; // a high inlet must not promote it
        PumpReadyReadOutcome fast =
            ProcessIoPumpReadyReadTrial.Observe(Request(fastClock, NarrowBudget), fastSession);

        var slowClock = new TestClock();
        (ProcessIoSession slowSession, FakeTransport slowTransport) = CreateSession();
        slowTransport.InputRegisters[outlet] = RawBelow;
        slowTransport.InputRegisters[inlet] = RawAbove;
        slowTransport.DuringRegisterRead = SlowOnly(slowClock, inlet, 60);
        PumpReadyReadOutcome slow =
            ProcessIoPumpReadyReadTrial.Observe(Request(slowClock, NarrowBudget), slowSession);

        // The acquisition is verified in both; the unmet setpoint refuses in both.
        Assert.True(fast.PhysicalAi003Verified);
        Assert.True(slow.PhysicalAi003Verified);
        Assert.False(fast.PumpReady);
        Assert.False(slow.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAtOrBelowSetpoint, fast.Code);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedAtOrBelowSetpoint, slow.Code);
        Assert.Equal(2, fast.ExitCode);
        Assert.Equal(2, slow.ExitCode);
    }

    [Fact]
    public void A_Diagnostic_Transport_Fault_Does_Not_Disturb_An_Accepted_Acquisition()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        int outlet = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);
        int inlet = AddressOf(ProcessIoIsolationValves.PumpInletPressureTag);
        transport.InputRegisters[outlet] = RawAbove;
        transport.InputRegisters[inlet] = RawBelow;

        // Only the diagnostic read faults. The acquisition succeeds.
        transport.ThrowOnRegister.Add(inlet);

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        // The AI-003 verdict is untouched by the diagnostic failure.
        Assert.True(outcome.PhysicalAi003Verified);
        Assert.True(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.Accepted, outcome.Code);
        Assert.Equal(0, outcome.ExitCode);

        // The fault is retained as the diagnostic's own result, never as the acquisition's.
        Assert.NotNull(outcome.Ai002Diagnostic);
        Assert.Equal(ProcessIoRefusals.ObservationTransportFault, outcome.Ai002Diagnostic!.DiagnosticCode);
        Assert.Equal(ProcessIoObservationProvenance.NOT_OBSERVED, outcome.Ai002Diagnostic.Provenance);
        Assert.Null(outcome.Ai002Diagnostic.EngineeringValue);
        Assert.Equal("AI-003+", outcome.Ai003.Tag);
        Assert.Equal(ProcessIoObservationProvenance.PHYSICAL_READ, outcome.Ai003.Provenance);
    }

    [Fact]
    public void An_Absent_Diagnostic_Row_Leaves_The_Decision_And_The_Member_Alone()
    {
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;

        // The catalogue carries no AI-002+ row at all.
        PumpReadyReadRequest request = Request(clock);
        var withoutInlet = new Dictionary<string, ProcessIoCatalogueRow>(request.Catalogue, StringComparer.Ordinal);
        Assert.True(withoutInlet.Remove(ProcessIoIsolationValves.PumpInletPressureTag));

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(
            request with { Catalogue = withoutInlet }, session);

        Assert.True(outcome.PhysicalAi003Verified);
        Assert.True(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.Accepted, outcome.Code);
        Assert.Equal(0, outcome.ExitCode);
        Assert.Null(outcome.Ai002Diagnostic);
        Assert.Equal(ProcessIoPumpReadyReadTrial.Ai002Role, outcome.Ai002Role);

        // It was never read, because there was no row to read.
        Assert.Equal(1, transport.ReadAttempts);
    }

    [Fact]
    public void A_Slow_Ai003_Refuses_Even_When_The_Diagnostic_Read_Is_Fast()
    {
        // The mirror image of the discriminating case: a stale acquisition is refused on its own merits,
        // and a healthy diagnostic reading cannot rescue it.
        var clock = new TestClock();
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        int outlet = AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag);
        int inlet = AddressOf(ProcessIoIsolationValves.PumpInletPressureTag);
        transport.InputRegisters[outlet] = RawAbove;
        transport.InputRegisters[inlet] = RawBelow;
        transport.DuringRegisterRead = SlowOnly(clock, outlet, 60);

        PumpReadyReadOutcome outcome = ProcessIoPumpReadyReadTrial.Observe(
            Request(clock, NarrowBudget), session);

        Assert.False(outcome.Ai003.Fresh);
        Assert.False(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
        Assert.Equal(ProcessIoPumpReadyReadTrial.RefusedStale, outcome.Code);
        Assert.Equal(2, outcome.ExitCode);

        Assert.NotNull(outcome.Ai002Diagnostic);
        Assert.True(outcome.Ai002Diagnostic!.Fresh);
    }

    [Fact]
    public void A_Diagnostic_Value_On_Either_Side_Of_The_Threshold_Changes_Nothing()
    {
        var outcomes = new List<PumpReadyReadOutcome>();

        foreach (ushort inletRaw in new ushort[]
                 {
                     0, RawBelow, RawAbove, 32000,
                 })
        {
            var runClock = new TestClock();
            (ProcessIoSession session, FakeTransport transport) = CreateSession();
            transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawAbove;
            transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpInletPressureTag)] = inletRaw;

            outcomes.Add(ProcessIoPumpReadyReadTrial.Observe(Request(runClock), session));
        }

        PumpReadyReadOutcome first = outcomes[0];
        Assert.All(outcomes, outcome =>
        {
            Assert.True(outcome.PhysicalAi003Verified);
            Assert.True(outcome.PumpReady);
            Assert.Equal(first.Code, outcome.Code);
            Assert.Equal(first.ExitCode, outcome.ExitCode);
            Assert.NotNull(outcome.Ai002Diagnostic);
        });

        // The diagnostic values genuinely differed, so the invariance above is not vacuous.
        Assert.True(outcomes[2].Ai002Diagnostic!.EngineeringValue > outcomes[1].Ai002Diagnostic!.EngineeringValue);
    }

    [Fact]
    public void The_Ai003_Decision_Is_Taken_Before_The_Diagnostic_Read_Starts()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        int classify = trial.IndexOf("PumpReadyReadOutcome outcome = Classify(", StringComparison.Ordinal);
        int diagnostic = trial.IndexOf("ObserveDiagnosticInlet(request, session)", StringComparison.Ordinal);

        Assert.True(classify > 0, "the AI-003 decision call must exist");
        Assert.True(diagnostic > 0, "the diagnostic read must exist");
        Assert.True(classify < diagnostic, "the AI-003 verdict must be fixed before AI-002 is read");

        // The verdict is not recomputed afterwards: Classify is called exactly once in the runner.
        Assert.Equal(1, CountOccurrences(trial, "PumpReadyReadOutcome outcome = Classify("));

        // And the diagnostic cannot be fed into the decision, because none is passed to it.
        Assert.Contains("inletDiagnostic: null", trial);
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
        Assert.False(outcome.PhysicalAi003Verified);
        Assert.False(outcome.PumpReady);
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
    public void The_Report_Keeps_Acquisition_Verification_And_Pump_Ready_Separate()
    {
        string trial = ReadSource("ProcessIoPumpReadyReadTrial.cs");

        // Acquisition verification is its own member, never the Pump-ready verdict.
        Assert.Contains("physical_ai003_verified=\" + Flag(outcome.PhysicalAi003Verified)", trial);

        // Pump-readiness is the existing rule's verdict.
        Assert.Contains("pump_ready=\" + Flag(outcome.PumpReady)", trial);

        // Physical Pump verification is the conjunction: verified acquisition AND Pump-ready.
        Assert.Contains("physical_pump_verified=\" + Flag(outcome.PhysicalAi003Verified && outcome.PumpReady)", trial);

        // The collapsed derivation must not survive anywhere in the source.
        Assert.DoesNotContain("physical_ai003_verified=\" + Flag(outcome.PumpReady)", trial);
        Assert.DoesNotContain("physical_pump_verified=\" + Flag(outcome.PumpReady)", trial);
    }

    [Fact]
    public void A_Refused_Run_Can_Never_Claim_Physical_Pump_Verification()
    {
        var clock = new TestClock();

        // A below-setpoint acquisition is verified evidence but is not a physical Pump verification.
        (ProcessIoSession session, FakeTransport transport) = CreateSession();
        transport.InputRegisters[AddressOf(ProcessIoIsolationValves.PumpOutletPressureTag)] = RawBelow;
        PumpReadyReadOutcome belowSetpoint = ProcessIoPumpReadyReadTrial.Observe(Request(clock), session);

        Assert.True(belowSetpoint.PhysicalAi003Verified);
        Assert.False(belowSetpoint.PumpReady);
        Assert.False(belowSetpoint.PhysicalAi003Verified && belowSetpoint.PumpReady);
        Assert.Equal(2, belowSetpoint.ExitCode);

        // A failed acquisition can claim neither.
        (ProcessIoSession faultedSession, FakeTransport faulted) = CreateSession();
        faulted.ThrowOnRead = true;
        PumpReadyReadOutcome faultedRun = ProcessIoPumpReadyReadTrial.Observe(Request(clock), faultedSession);

        Assert.False(faultedRun.PhysicalAi003Verified);
        Assert.False(faultedRun.PhysicalAi003Verified && faultedRun.PumpReady);
        Assert.Equal(3, faultedRun.ExitCode);
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

    private static PumpReadyReadRequest Request(TestClock clock) => Request(clock, Freshness);

    private static PumpReadyReadRequest Request(TestClock clock, TimeSpan freshness)
    {
        Dictionary<string, ProcessIoCatalogueRow> catalogue = BuildCatalogue();
        return new PumpReadyReadRequest
        {
            PumpRow = catalogue[ProcessIoIsolationValves.PumpOutletPressureTag],
            Catalogue = catalogue,
            Thresholds = Thresholds,
            Freshness = freshness,
            Time = clock,
        };
    }

    /// <summary>A budget narrower than the delays the diagnostic tests inject, so stale is reachable.</summary>
    private static readonly TimeSpan NarrowBudget = TimeSpan.FromMilliseconds(20);

    /// <summary>Makes exactly one register read consume clock time, leaving every other read fast.</summary>
    private static Action<int> SlowOnly(TestClock clock, int registerAddress, int milliseconds) =>
        address =>
        {
            if (address == registerAddress)
            {
                clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
            }
        };

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

    /// <summary>
    /// The Pump Inlet observation, carrying its own identity and the Product's own inlet conversion. It
    /// exists so the diagnostic-only member can be populated without a transport.
    /// </summary>
    private static ProcessIoPhysicalObservation InletObservation(int rawWord, DateTimeOffset at) =>
        Observe(rawWord, at, at) with
        {
            Tag = ProcessIoIsolationValves.PumpInletPressureTag,
            EngineeringValue = ProcessIoEngineering.WcPressureBar(rawWord),
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

        /// <summary>Registers whose read throws, so one tag can fault while another succeeds.</summary>
        public HashSet<int> ThrowOnRegister { get; } = new();

        /// <summary>Runs inside every read, so a test can consume clock time where a real device would.</summary>
        public Action? DuringRead { get; set; }

        /// <summary>
        /// Runs inside one input-register read with that register's address, so a test can make exactly
        /// one tag slow — for example a slow AI-002 diagnostic beside a fast AI-003 acquisition.
        /// </summary>
        public Action<int>? DuringRegisterRead { get; set; }

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
            OnRead(registerAddress);
            return InputRegisters.TryGetValue(registerAddress, out ushort value) ? value : (ushort)0;
        }

        public ushort ReadRegister(string deviceKey, int registerAddress)
        {
            OnRead(registerAddress);
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

        private void OnRead(int? registerAddress = null)
        {
            ReadAttempts += 1;
            if (ThrowOnRead || (registerAddress is int faulting && ThrowOnRegister.Contains(faulting)))
            {
                throw new IOException("FAKE_READ_TIMEOUT");
            }

            DuringRead?.Invoke();

            if (registerAddress is int address)
            {
                DuringRegisterRead?.Invoke(address);
            }
        }
    }
}
