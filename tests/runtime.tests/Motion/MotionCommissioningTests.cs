using System.Text.Json;
using Wjss.Contracts;
using Wjss.Runtime.Core.Motion;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// The manual motion commissioning foundation: the closed authorization gate, the refusal of every
/// motion intent on every axis, the CANONICAL Owner-confirmed REV2 axis allocation of all eight
/// Water Jets (WJ2 = A,B on WSB1 and WJ4 = C,D on WSB2 included; REV1 retained as historical
/// identity only), the Drawing rev1 discrepancy recorded as evidence only,
/// the published not-configured / not-commissioned state, and the deterministic canonical text.
///
/// These tests prove the SOFTWARE position of this revision. They prove nothing about hardware: no
/// controller is contacted here, and no test result may be read as device evidence.
/// </summary>
public sealed class MotionCommissioningTests
{
    /// <summary>The Owner-confirmed Water Jet axis allocation, in Water Jet ordinal order.</summary>
    private static readonly (string WaterJetId, string CabinetId, string Horizontal, string Vertical)[] ExpectedBindings =
    [
        ("WJ1", "WSB2", "A", "B"),
        ("WJ2", "WSB1", "A", "B"),
        ("WJ3", "WSB1", "C", "D"),
        ("WJ4", "WSB2", "C", "D"),
        ("WJ5", "WSB3", "A", "B"),
        ("WJ6", "WSB4", "A", "B"),
        ("WJ7", "WSB3", "C", "D"),
        ("WJ8", "WSB4", "C", "D"),
    ];

    /// <summary>The Owner-confirmed brake output allocation, in axis label order.</summary>
    private static readonly (string AxisId, string BrakeOutput)[] ExpectedBrakeOutputs =
    [
        ("A", "OUTPUT_1"),
        ("B", "OUTPUT_2"),
        ("C", "OUTPUT_3"),
        ("D", "OUTPUT_4"),
    ];

    /// <summary>The four WSB cabinet identities of the four motion controllers, in controller order.</summary>
    private static readonly string[] ExpectedCabinetIds = ["WSB1", "WSB2", "WSB3", "WSB4"];

    /// <summary>The three installed limit inputs of every axis, in Reverse, Home, Forward order.</summary>
    private static readonly string[] ExpectedLimitSignalRoles =
    [
        MotionVocabulary.SignalReverse,
        MotionVocabulary.SignalHome,
        MotionVocabulary.SignalForward,
    ];

    /// <summary>The eight Water Jets, in Water Jet ordinal order.</summary>
    private static readonly string[] ExpectedWaterJetIds = ["WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8"];

    /// <summary>The two Water Jets named by the Drawing rev1 discrepancy note, in Water Jet ordinal order.</summary>
    private static readonly string[] ExpectedDiscrepancyWaterJetIds = ["WJ2", "WJ4"];

    /// <summary>The two cabinets named by the Drawing rev1 discrepancy note, in Water Jet ordinal order.</summary>
    private static readonly string[] ExpectedDiscrepancyCabinetIds = ["WSB1", "WSB2"];

    [Fact]
    public void The_Authorization_Gate_Is_Closed_And_Carries_The_Gate_Reason()
    {
        Assert.Equal(MotionVocabulary.GateClosed, MotionAuthorizationGate.State);
        Assert.True(MotionAuthorizationGate.IsClosed);
        Assert.Equal(MotionVocabulary.GateRefusalReasons, MotionAuthorizationGate.Reasons);
        Assert.Equal(MotionVocabulary.RefusalMotionCommissioningGateNotOpen, MotionAuthorizationGate.Reasons[0]);
    }

    [Fact]
    public void Every_Motion_Intent_Is_Refused_On_Every_Axis_Before_Any_Hardware_Path()
    {
        var axes = MotionTopologyCatalog.Axes;
        Assert.Equal(16, axes.Count);

        foreach (var intent in MotionVocabulary.MotionIntents)
        {
            var evaluations = MotionCommandGate.EvaluateForAllAxes(intent);
            Assert.Equal(axes.Count, evaluations.Count);

            for (var index = 0; index < evaluations.Count; index++)
            {
                var evaluation = evaluations[index];
                Assert.Equal(intent, evaluation.Intent);
                Assert.Equal(axes[index].ControllerId, evaluation.ControllerId);
                Assert.Equal(axes[index].AxisId, evaluation.AxisId);
                Assert.False(evaluation.Authorized);
                Assert.Equal(MotionVocabulary.CommandStateRefused, evaluation.CommandState);
                Assert.False(evaluation.HardwareCommandIssued);
                Assert.Equal(MotionVocabulary.HardwareCommandStateNotIssued, evaluation.HardwareCommandState);
                // The gate reason leads the refusal list, and the standing preconditions follow it:
                // the preconditions are reported alongside the gate, never instead of it.
                Assert.Equal(MotionVocabulary.AllRefusalReasons, evaluation.RefusalReasons);
                Assert.Equal(MotionVocabulary.RefusalMotionCommissioningGateNotOpen, evaluation.RefusalReasons[0]);
            }
        }
    }

    [Fact]
    public void An_Unknown_Intent_Or_Axis_Is_A_Programming_Error_And_Never_A_Refused_Command()
    {
        var intent = ValidIntent("JOG", "MC-WSB1", "A");

        Assert.Throws<ArgumentOutOfRangeException>(() => MotionCommandGate.Evaluate(intent with { Intent = "SPIN" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => MotionCommandGate.Evaluate(intent with { ControllerId = "MC-WSB9" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => MotionCommandGate.Evaluate(intent with { AxisId = "Z" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => MotionCommandGate.EvaluateForAllAxes("SPIN"));
    }

    [Fact]
    public void No_Session_Can_Be_Opened_While_The_Gate_Is_Closed_And_Stop_Is_Refused_Too()
    {
        // The session is a STATIC type: while the gate is closed no session can be opened, so there
        // is no instance to construct and no instance state to carry. Every member is either a
        // constant of the closed gate or a delegation to the gate's evaluation, and every one
        // refuses. Nothing here invents a session record to look object oriented.
        Assert.Equal(MotionVocabulary.SessionStateNoSession, MotionCommissioningSession.State);
        Assert.False(MotionCommissioningSession.IsOpen);
        Assert.Equal(0, MotionCommissioningSession.OwnedAxisCount);
        Assert.Empty(MotionCommissioningSession.Evaluations);

        Assert.False(MotionCommissioningSession.TryOpen(out var refusalReasons));
        Assert.Equal(MotionVocabulary.AllRefusalReasons, refusalReasons);
        Assert.Equal(MotionVocabulary.RefusalMotionCommissioningGateNotOpen, refusalReasons[0]);

        // One intent evaluated through the session is refused exactly as it is through the gate: a
        // session is a precondition of execution, never a way around the gate.
        var evaluated = MotionCommissioningSession.Evaluate(ValidIntent(MotionVocabulary.IntentJog, "MC-WSB1", "A"));
        Assert.False(evaluated.Authorized);
        Assert.Equal(MotionVocabulary.CommandStateRefused, evaluated.CommandState);
        Assert.False(evaluated.HardwareCommandIssued);
        Assert.Equal(MotionVocabulary.HardwareCommandStateNotIssued, evaluated.HardwareCommandState);

        // The explicit Stop path exists independently of the gate, and it is refused here for the
        // same reason: no controller is connected, so no stop command can be delivered.
        var stops = MotionCommissioningSession.StopAllAxes();
        Assert.Equal(16, stops.Count);
        Assert.All(stops, stop => Assert.False(stop.Authorized));
        Assert.All(stops, stop => Assert.False(stop.HardwareCommandIssued));
        Assert.All(stops, stop => Assert.Equal(MotionVocabulary.IntentStop, stop.Intent));
    }

    [Fact]
    public void The_Projection_Carries_Four_Controllers_Sixteen_Axes_And_Eight_WaterJets()
    {
        var projection = MotionCommissioningProjector.Project();

        Assert.Equal(4, projection.Controllers.Count);
        Assert.Equal(16, projection.Axes.Count);
        Assert.Equal(8, projection.WaterJets.Count);
        Assert.Equal(MotionVocabulary.SectionId, projection.SectionId);
        Assert.Equal(MotionVocabulary.Source, projection.Source);
        Assert.Equal(MotionVocabulary.MotionTopologyRevision, projection.MotionTopologyRevision);

        // Canonical Topology Adoption: the current publication identity is REV2; REV1
        // survives only as the explicitly historical identity and is never published.
        Assert.Equal("OWNER_CONFIRMED_REV2", MotionVocabulary.MotionTopologyRevision);
        Assert.Equal("OWNER_CONFIRMED_REV2", projection.MotionTopologyRevision);
        Assert.Equal("OWNER_CONFIRMED_REV1", MotionVocabulary.HistoricalMotionTopologyRevisionRev1);
        Assert.NotEqual(
            MotionVocabulary.HistoricalMotionTopologyRevisionRev1,
            projection.MotionTopologyRevision);
        Assert.Contains("(OWNER_CONFIRMED_REV2)", projection.CanonicalText, StringComparison.Ordinal);

        var cabinets = projection.Controllers.Select(controller => controller.Identity.CabinetId).ToArray();
        Assert.Equal(ExpectedCabinetIds, cabinets);
        Assert.All(projection.Controllers, controller => Assert.Equal(4, controller.Identity.AxisCount));
        Assert.All(projection.Controllers, controller => Assert.Equal("DMC-B140-M", controller.Identity.ControllerModel));
        Assert.All(projection.Controllers,
            controller => Assert.Equal(MotionVocabulary.ControllerModelStatusDrawingConfirmed, controller.Identity.ControllerModelStatus));
        // The one-controller-per-cabinet reading is an inference from the confirmed allocation, not
        // a separate Owner confirmation: it must never be reported as confirmed.
        Assert.All(projection.Controllers,
            controller => Assert.Equal(MotionVocabulary.BasisInferred, controller.Identity.ControllerIdentityBasis));
    }

    [Fact]
    public void The_Owner_Confirmed_WaterJet_Axis_Allocation_Is_Published_Exactly()
    {
        var projection = MotionCommissioningProjector.Project();

        Assert.Equal(ExpectedBindings.Length, projection.WaterJets.Count);
        for (var index = 0; index < ExpectedBindings.Length; index++)
        {
            var expected = ExpectedBindings[index];
            var actual = projection.WaterJets[index];
            Assert.Equal(expected.WaterJetId, actual.WaterJetId);
            Assert.Equal(expected.CabinetId, actual.CabinetId);
            Assert.Equal(expected.Horizontal, actual.HorizontalAxisId);
            Assert.Equal(expected.Vertical, actual.VerticalAxisId);
            Assert.Equal($"MC-{expected.CabinetId}", actual.ControllerId);

            // Canonical provenance: every row, WJ2 and WJ4 included, is Owner-confirmed.
            Assert.Equal(MotionVocabulary.MappingConfirmedOwner, actual.MappingStatus);
            Assert.Equal(MotionVocabulary.BasisOwnerConfirmed, actual.MappingBasis);

            // Exactly one Horizontal Axis and exactly one Vertical Axis, per the topology catalog.
            var horizontal = MotionTopologyCatalog.FindAxis(actual.ControllerId, actual.HorizontalAxisId);
            var vertical = MotionTopologyCatalog.FindAxis(actual.ControllerId, actual.VerticalAxisId);
            Assert.NotNull(horizontal);
            Assert.NotNull(vertical);
            Assert.Equal(MotionVocabulary.AxisRoleHorizontal, horizontal!.AxisRole);
            Assert.Equal(MotionVocabulary.AxisRoleVertical, vertical!.AxisRole);
            Assert.NotEqual(actual.HorizontalAxisId, actual.VerticalAxisId);
        }

        // A and C are the Horizontal Axis; B and D are the Vertical Axis, on every controller.
        foreach (var axis in projection.Axes)
        {
            var role = axis.Identity.AxisId is "A" or "C" ? MotionVocabulary.AxisRoleHorizontal : MotionVocabulary.AxisRoleVertical;
            Assert.Equal(role, axis.Identity.AxisRole);
            Assert.Equal(MotionVocabulary.BasisOwnerConfirmed, axis.Identity.AxisRoleBasis);
        }

        // Brake outputs are allocated A-1, B-2, C-3, D-4 on every controller.
        foreach (var axis in projection.Axes)
        {
            var expected = ExpectedBrakeOutputs.Single(pair => pair.AxisId == axis.Identity.AxisId);
            Assert.Equal(expected.BrakeOutput, axis.Identity.BrakeOutputIdentity);
            Assert.Equal(MotionVocabulary.BasisOwnerConfirmed, axis.Identity.BrakeOutputMappingBasis);
        }
    }

    [Fact]
    public void No_Controller_Is_Configured_Connected_Or_Ready_And_No_Endpoint_Is_Carried()
    {
        var projection = MotionCommissioningProjector.Project();

        foreach (var controller in projection.Controllers)
        {
            var configuration = controller.Configuration;
            Assert.Equal(MotionVocabulary.MotionPortNotConfigured, configuration.MotionPortIdentity);
            Assert.Equal(MotionVocabulary.MotionPortStatusNotConfigured, configuration.MotionPortStatus);
            Assert.Equal(MotionVocabulary.EndpointNotConfigured, configuration.EndpointIdentity);
            Assert.Equal(MotionVocabulary.ControllerConfigurationNotConfigured, configuration.ControllerConfigurationStatus);
            Assert.Equal(MotionVocabulary.GclibStatusNotInstalled, configuration.LibraryInstallationStatus);
            Assert.Equal(MotionVocabulary.CommunicationNotConnected, configuration.CommunicationState);
            Assert.False(configuration.MotionDispatchReady);
            Assert.Equal(MotionVocabulary.RefusalMotionCommissioningGateNotOpen, configuration.MotionDispatchBlockedReason);
            Assert.Equal(MotionVocabulary.StatusNotConfigured, configuration.ParameterConfigurationState);
            Assert.Equal(MotionVocabulary.StatusNotCommissioned, configuration.SoftwareLimitConfigurationState);
            Assert.Equal(MotionVocabulary.PolarityUnconfirmed, configuration.LimitInputPolarityStatus);
            Assert.Equal(MotionVocabulary.StatusNotCommissioned, configuration.EndstopConfigurationStatus);
        }

        // The serialized section carries no network address: the only endpoint-shaped strings are the
        // two redacted identities, and no IPv4 literal of any kind appears in it.
        var json = JsonSerializer.Serialize(projection, ContractJson.Options);
        Assert.DoesNotMatch(@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b", json);
        Assert.Contains(MotionVocabulary.EndpointNotConfigured, json, StringComparison.Ordinal);
        Assert.Contains(MotionVocabulary.MotionPortNotConfigured, json, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Axis_Is_Homed_And_Every_Axis_Refuses_An_Absolute_Move()
    {
        var projection = MotionCommissioningProjector.Project();

        foreach (var axis in projection.Axes)
        {
            var state = axis.State;
            Assert.Equal(MotionVocabulary.CommissioningNotCommissioned, state.CommissioningState);
            Assert.Equal(MotionVocabulary.HomingNotHomed, state.HomingState);
            Assert.Equal(MotionVocabulary.SignalStateUnknown, state.HomingStateStatus);
            Assert.Equal(MotionVocabulary.BrakeCommandNone, state.BrakeCommandState);
            Assert.Equal(MotionVocabulary.BrakeStateUnknown, state.BrakeState);
            Assert.Equal(MotionVocabulary.StatusNotAvailable, state.BrakeStateStatus);
            Assert.Equal(MotionVocabulary.MotionStateUnknown, state.MotionState);
            Assert.Equal(MotionVocabulary.StatusNotAvailable, state.MotionStateStatus);
            Assert.Null(state.Sense.PositionDisplayValue);
            Assert.Equal(MotionVocabulary.PositionValueNotRead, state.Sense.PositionValueStatus);

            // Three installed limit inputs, in Reverse, Home, Forward order, all unconfirmed and unknown.
            var roles = state.LimitSignals.Select(signal => signal.SignalRole).ToArray();
            Assert.Equal(ExpectedLimitSignalRoles, roles);
            Assert.All(state.LimitSignals, signal => Assert.Equal(MotionVocabulary.PolarityUnconfirmed, signal.PolarityStatus));
            Assert.All(state.LimitSignals, signal => Assert.Equal(MotionVocabulary.SignalStateUnknown, signal.SignalState));
            Assert.All(state.LimitSignals, signal => Assert.Equal(MotionVocabulary.StatusNotAvailable, signal.SignalStateStatus));

            // No conversion is published: counts stay counts until a conversion is measured.
            Assert.Equal(MotionVocabulary.StatusUnresolved, state.Kinematic.KinematicConversionStatus);
            Assert.True(state.Kinematic.KinematicConversionIsPerAxis);
            Assert.Equal(MotionVocabulary.ProvenanceMeasuredDuringCommissioning, state.Kinematic.ConversionProvenanceRequirement);
            Assert.Equal(MotionVocabulary.SpeedUnitControllerUnits, state.Kinematic.PublishedUnit);

            // An absolute Move needs a valid Home and commissioned software limits. Neither exists.
            Assert.Equal(MotionVocabulary.CommandStateRefused, state.MoveAuthorization.MoveAuthorizationState);
            Assert.Equal(MotionVocabulary.AllRefusalReasons, state.MoveAuthorization.MoveRefusalReasons);
        }

        Assert.Empty(projection.AllowedMotionStates);
    }

    [Fact]
    public void The_Jog_Speed_Is_A_Display_Value_In_Controller_Units_And_Is_Never_Executable()
    {
        var projection = MotionCommissioningProjector.Project();

        Assert.Equal("JOG_SPEED", projection.JogSpeed.ParameterId);
        Assert.Equal("10", projection.JogSpeed.DisplayValue);
        Assert.Equal(MotionVocabulary.SpeedUnitControllerUnits, projection.JogSpeed.Unit);
        Assert.Equal(MotionVocabulary.ParameterStatusUnconfirmed, projection.JogSpeed.Status);
        Assert.False(projection.JogSpeed.Executable);

        var defaults = MotionCommandIntent.DefaultParameters();
        Assert.Equal("10", defaults.SpeedDisplayValue);
        Assert.Equal(MotionVocabulary.SpeedUnitControllerUnits, defaults.SpeedUnit);
        Assert.False(defaults.SpeedExecutable);
        Assert.False(defaults.TargetExecutable);
        Assert.Equal(MotionVocabulary.StatusNotCommissioned, defaults.TargetStatus);
    }

    /// <summary>
    /// The canonical allocation is EXPLICIT DATA, not a rule. The two Water Jets of WSB1 take
    /// different axis pairs and the two Water Jets of WSB2 take different axis pairs, so no
    /// cabinet-derived formula can produce the table; and the even Water Jets WJ2 and WJ4 take
    /// different axis pairs while the odd Water Jets WJ5 and WJ7 also take different axis pairs, so
    /// no odd/even parity formula can produce it either.
    /// </summary>
    [Fact]
    public void The_Canonical_Allocation_Is_Explicit_Data_And_No_Parity_Or_Cabinet_Formula_Produces_It()
    {
        var catalogue = MotionCommissioningProjector.Project();

        Assert.Equal(8, catalogue.WaterJets.Count);
        Assert.Equal(ExpectedWaterJetIds, catalogue.WaterJets.Select(binding => binding.WaterJetId).ToArray());

        // No cabinet-derived axis formula: every cabinet carries both axis pairs
        // (WSB1: WJ2/WJ3, WSB2: WJ1/WJ4, WSB3: WJ5/WJ7, WSB4: WJ6/WJ8 under REV2).
        Assert.NotEqual(HorizontalOf(catalogue, "WJ2"), HorizontalOf(catalogue, "WJ3"));
        Assert.NotEqual(HorizontalOf(catalogue, "WJ1"), HorizontalOf(catalogue, "WJ4"));
        Assert.NotEqual(HorizontalOf(catalogue, "WJ5"), HorizontalOf(catalogue, "WJ7"));
        Assert.NotEqual(HorizontalOf(catalogue, "WJ6"), HorizontalOf(catalogue, "WJ8"));

        // No odd/even parity formula: the even Water Jets disagree with each other, and so do the
        // odd ones.
        Assert.NotEqual(HorizontalOf(catalogue, "WJ2"), HorizontalOf(catalogue, "WJ4"));
        Assert.NotEqual(HorizontalOf(catalogue, "WJ6"), HorizontalOf(catalogue, "WJ8"));
        Assert.NotEqual(HorizontalOf(catalogue, "WJ1"), HorizontalOf(catalogue, "WJ3"));
        Assert.NotEqual(HorizontalOf(catalogue, "WJ5"), HorizontalOf(catalogue, "WJ7"));

        // Every Water Jet still carries exactly one Horizontal Axis and exactly one Vertical Axis.
        Assert.All(catalogue.WaterJets, binding =>
        {
            var horizontal = MotionTopologyCatalog.FindAxis(binding.ControllerId, binding.HorizontalAxisId);
            var vertical = MotionTopologyCatalog.FindAxis(binding.ControllerId, binding.VerticalAxisId);
            Assert.NotNull(horizontal);
            Assert.NotNull(vertical);
            Assert.Equal(MotionVocabulary.AxisRoleHorizontal, horizontal!.AxisRole);
            Assert.Equal(MotionVocabulary.AxisRoleVertical, vertical!.AxisRole);
        });
        Assert.Equal(8, catalogue.WaterJets.Count(binding => binding.HorizontalAxisId is "A" or "C"));
        Assert.Equal(8, catalogue.WaterJets.Count(binding => binding.VerticalAxisId is "B" or "D"));

        // And the canonical values the Owner ruled under REV2: WJ2 = A,B and WJ4 = C,D.
        Assert.Equal("A", HorizontalOf(catalogue, "WJ2"));
        Assert.Equal("B", VerticalOf(catalogue, "WJ2"));
        Assert.Equal("C", HorizontalOf(catalogue, "WJ4"));
        Assert.Equal("D", VerticalOf(catalogue, "WJ4"));
    }

    [Fact]
    public void The_WJ2_And_WJ4_Drawing_Discrepancy_Is_Recorded_As_Evidence_Only_And_Changes_No_Mapping()
    {
        var catalogue = MotionCommissioningProjector.Project();

        var note = Assert.Single(catalogue.MappingEvidenceNotes);
        Assert.Equal(MotionVocabulary.EvidenceNoteIdWj2Wj4, note.EvidenceId);
        Assert.Equal(ExpectedDiscrepancyWaterJetIds, note.WaterJetIds);
        Assert.Equal(ExpectedDiscrepancyCabinetIds, note.CabinetIds);
        Assert.Equal(MotionVocabulary.DrawingEvidenceRecorded, note.Status);
        Assert.Equal(MotionVocabulary.EvidenceClassificationEvidenceOnly, note.Classification);
        Assert.Equal(MotionVocabulary.PhysicalResolutionNotResolved, note.PhysicalResolution);
        Assert.False(string.IsNullOrWhiteSpace(note.Statement));
        Assert.False(string.IsNullOrWhiteSpace(note.Effect));

        // The statement is explicitly historical: the discrepancy belongs to the
        // superseded REV1 allocation, current authority is REV2, and the physical
        // installation is never claimed resolved.
        Assert.Contains("superseded OWNER_CONFIRMED_REV1 allocation", note.Statement, StringComparison.Ordinal);
        Assert.Contains("OWNER_CONFIRMED_REV2", note.Statement, StringComparison.Ordinal);
        Assert.Contains("NOT_PHYSICALLY_RESOLVED", note.Statement, StringComparison.Ordinal);

        // Evidence only: the note does not make any row ambiguous and does not override the Owner.
        Assert.All(catalogue.WaterJets, binding => Assert.Equal(MotionVocabulary.MappingConfirmedOwner, binding.MappingStatus));
        Assert.All(catalogue.WaterJets, binding => Assert.Equal(MotionVocabulary.BasisOwnerConfirmed, binding.MappingBasis));

        // No open-decision status survives anywhere in the published surface.
        Assert.False(MotionVocabulary.StatusText.ContainsKey("OWNER_DECISION_REQUIRED"));
        Assert.DoesNotContain("OWNER_DECISION_REQUIRED", catalogue.StatusLegend);
        Assert.DoesNotContain("OWNER_DECISION_REQUIRED", catalogue.CanonicalText);

        // The note is published, and it says the discrepancy is not physically resolved.
        Assert.Contains("EVIDENCE NOTES 1", catalogue.CanonicalText);
        Assert.Contains(MotionVocabulary.DrawingEvidenceRecorded, catalogue.CanonicalText);
        Assert.Contains(MotionVocabulary.EvidenceClassificationEvidenceOnly, catalogue.CanonicalText);
        Assert.Contains(MotionVocabulary.PhysicalResolutionNotResolved, catalogue.CanonicalText);
    }

    [Fact]
    public void The_Canonical_Text_And_Fingerprint_Are_Deterministic_And_The_Text_Changed_Changes_Them()
    {
        var first = MotionCommissioningProjector.Project();
        var second = MotionCommissioningProjector.Project();

        Assert.False(string.IsNullOrWhiteSpace(first.CanonicalText));
        Assert.Equal(first.CanonicalText, second.CanonicalText);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(64, first.Fingerprint.Length);
        Assert.Equal(first.Fingerprint, first.Fingerprint.ToLowerInvariant());
        Assert.Equal(first.Fingerprint, MotionCommissioningProjector.Fingerprint(first.CanonicalText));

        Assert.NotEqual(first.Fingerprint, MotionCommissioningProjector.Fingerprint(first.CanonicalText + "\n"));

        // The text states the gate, the counts and every allocation of this revision.
        Assert.Contains($"GATE {MotionVocabulary.GateClosed}", first.CanonicalText, StringComparison.Ordinal);
        Assert.Contains("CONTROLLERS 4", first.CanonicalText, StringComparison.Ordinal);
        Assert.Contains("AXES 16", first.CanonicalText, StringComparison.Ordinal);
        Assert.Contains("WATER JETS 8", first.CanonicalText, StringComparison.Ordinal);

        // The Drawing rev1 discrepancy is an EVIDENCE NOTE, never a canonical conflict: all eight
        // mappings stay Owner-confirmed, WJ2 and WJ4 included, so the text publishes one evidence
        // note and no conflict count at all.
        Assert.Contains("EVIDENCE NOTES 1", first.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("CONFLICTS", first.CanonicalText);
        var confirmedRow = $" - {MotionVocabulary.MappingConfirmedOwner} ({MotionVocabulary.BasisOwnerConfirmed})";
        var confirmedRows = 0;
        foreach (var line in first.CanonicalText.Split('\n'))
        {
            if (line.EndsWith(confirmedRow, StringComparison.Ordinal)) confirmedRows++;
        }

        Assert.Equal(ExpectedWaterJetIds.Length, confirmedRows);

        // And the two rows the discrepancy names, stated in the text exactly as the Owner ruled them.
        Assert.Contains($"W2 WJ2 cabinet WSB1 controller MC-WSB1 horizontal C vertical D{confirmedRow}",
            first.CanonicalText, StringComparison.Ordinal);
        Assert.Contains($"W4 WJ4 cabinet WSB2 controller MC-WSB2 horizontal A vertical B{confirmedRow}",
            first.CanonicalText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Projection_Publishes_Its_Not_Verified_Claims_And_A_Worded_Legend()
    {
        var projection = MotionCommissioningProjector.Project();

        Assert.Equal(MotionVocabulary.NotVerifiedClaims, projection.NotVerifiedClaims);
        Assert.Contains("NO_CONTROLLER_CONTACTED", projection.NotVerifiedClaims);
        Assert.Contains("NO_MOTION_COMMAND_ISSUED", projection.NotVerifiedClaims);
        Assert.Contains("NO_UNIT_CONVERSION_VERIFIED", projection.NotVerifiedClaims);

        // Every refusal code is worded, so no state is ever presented by colour alone.
        Assert.Equal(MotionVocabulary.StatusLegendOrder.Count, projection.StatusLegend.Count);
        foreach (var code in MotionVocabulary.AllRefusalReasons)
        {
            Assert.Contains($"{code} - ", projection.StatusLegend.First(line => line.StartsWith(code, StringComparison.Ordinal)), StringComparison.Ordinal);
        }

        Assert.Equal(0, projection.CommandTotals.Evaluated);
        Assert.Equal(0, projection.CommandTotals.Authorized);
        Assert.Equal(0, projection.CommandTotals.Refused);
        Assert.Equal(0, projection.CommandTotals.HardwareCommandsIssued);
        Assert.Equal(MotionVocabulary.JournalPersistenceNone, projection.CommandTotals.JournalPersistence);
        Assert.Equal(MotionVocabulary.SessionStateNoSession, projection.SessionState);
    }

    /// <summary>The canonical Horizontal Axis letter of one Water Jet, read from the catalogue.</summary>
    private static string HorizontalOf(MotionCommissioningCatalogue catalogue, string waterJetId) =>
        catalogue.WaterJets.Single(binding => binding.WaterJetId == waterJetId).HorizontalAxisId;

    /// <summary>The canonical Vertical Axis letter of one Water Jet, read from the catalogue.</summary>
    private static string VerticalOf(MotionCommissioningCatalogue catalogue, string waterJetId) =>
        catalogue.WaterJets.Single(binding => binding.WaterJetId == waterJetId).VerticalAxisId;

    private static MotionCommandIntent ValidIntent(string intent, string controllerId, string axisId) => new()
    {
        Intent = intent,
        ControllerId = controllerId,
        AxisId = axisId,
        Direction = MotionVocabulary.DirectionNone,
        RequestedBy = MotionVocabulary.RequestedByApplication,
        OwnerAuthorizationReference = MotionVocabulary.OwnerAuthorizationNone,
        Parameters = MotionCommandIntent.DefaultParameters(),
    };
}
