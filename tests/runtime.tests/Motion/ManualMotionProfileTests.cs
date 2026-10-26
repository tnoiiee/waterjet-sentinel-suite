using Wjss.Runtime.Core.Motion.Manual;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// The Owner-local Manual Motion Commissioning profile: strict, fail-closed parsing, NOT_CONFIGURED
/// for absent values, the Owner-confirmed brake-output allocation, and the separation between the
/// synthetic simulation profile and an Owner-local file. Every value below is a synthetic test value
/// in native units; none is a production value.
/// </summary>
public sealed class ManualMotionProfileTests
{
    private const string CompleteProfile = """
        {
          "schema": "wjss.manual-motion-profile/1",
          "controller": "WSB1",
          "session": {
            "observationIntervalMilliseconds": 40,
            "observationFreshnessMilliseconds": 700,
            "jogHeartbeatTimeoutMilliseconds": 450,
            "jogMaximumHoldMilliseconds": 9000
          },
          "axes": {
            "A": {
              "brakeOutput": 1,
              "brakeReleaseAction": "CLEAR_OUTPUT",
              "brakeSettleMilliseconds": 150,
              "jogSpeed": 321,
              "homeSpeed": 123,
              "homeTimeoutMilliseconds": 45000,
              "moveAcceleration": 1000,
              "moveSpeed": 900,
              "moveDeceleration": 1100,
              "moveMinimumTarget": -5000,
              "moveMaximumTarget": 6000,
              "positionTolerance": 3,
              "motionTimeoutMilliseconds": 25000,
              "stopTimeoutMilliseconds": 2500,
              "motionStatusBit": 5,
              "motionActiveValue": 0,
              "homeStatusBit": 2,
              "homeActiveValue": 1,
              "dualZeroPermitted": true
            },
            "B": {
              "brakeOutput": 2
            }
          }
        }
        """;

    private static string ReplaceInProfile(string from, string to)
    {
        Assert.Contains(from, CompleteProfile, StringComparison.Ordinal);
        return CompleteProfile.Replace(from, to, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Complete_Profile_Is_Accepted_With_Every_Native_Value_Preserved()
    {
        var result = ManualMotionProfileParser.Parse(CompleteProfile);

        Assert.True(result.Accepted);
        Assert.Equal(ManualMotionCodes.ProfileAccepted, result.Code);
        var profile = Assert.IsType<ManualMotionProfile>(result.Profile);
        Assert.Equal(ManualMotionProfileSource.OwnerLocalFile, profile.Source);
        Assert.Equal("WSB1", profile.ControllerId);
        Assert.Equal(40, profile.ObservationIntervalMilliseconds);
        Assert.Equal(700, profile.ObservationFreshnessMilliseconds);
        Assert.Equal(450, profile.JogHeartbeatTimeoutMilliseconds);
        Assert.Equal(9000, profile.JogMaximumHoldMilliseconds);

        var axisA = Assert.IsType<ManualMotionAxisProfile>(profile.AxisA);
        Assert.Equal(1, axisA.BrakeOutput);
        Assert.Equal(ManualMotionBrakeReleaseAction.ClearOutput, axisA.BrakeReleaseAction);
        Assert.Equal(150, axisA.BrakeSettleMilliseconds);
        Assert.Equal(321, axisA.JogSpeed);
        Assert.Equal(123, axisA.HomeSpeed);
        Assert.Equal(-5000, axisA.MoveMinimumTarget);
        Assert.Equal(6000, axisA.MoveMaximumTarget);
        Assert.Equal(3, axisA.PositionTolerance);
        Assert.Equal(5, axisA.MotionStatusBit);
        Assert.Equal(0, axisA.MotionActiveValue);
        Assert.Equal(2, axisA.HomeStatusBit);
        Assert.Equal(1, axisA.HomeActiveValue);
        Assert.True(axisA.DualZeroPermitted);
        Assert.Empty(axisA.NotConfiguredItems());
    }

    [Fact]
    public void Absent_Values_Are_Not_Configured_And_Never_Defaulted()
    {
        var profile = Assert.IsType<ManualMotionProfile>(ManualMotionProfileParser.Parse(CompleteProfile).Profile);
        var axisB = Assert.IsType<ManualMotionAxisProfile>(profile.AxisB);

        Assert.Equal(2, axisB.BrakeOutput);
        Assert.Null(axisB.BrakeReleaseAction);
        Assert.Null(axisB.JogSpeed);
        Assert.Null(axisB.HomeSpeed);
        Assert.Null(axisB.MoveMinimumTarget);
        Assert.Null(axisB.MotionStatusBit);
        Assert.False(axisB.DualZeroPermitted);

        var missing = axisB.NotConfiguredItems();
        Assert.Contains("brakeReleaseAction", missing);
        Assert.Contains("jogSpeed", missing);
        Assert.Contains("motionStatusBit", missing);
        Assert.Contains("dualZeroPermitted", missing);
        Assert.DoesNotContain("brakeOutput", missing);
    }

    [Fact]
    public void Only_The_Listed_Axes_Are_Allowed()
    {
        // A self-contained one-axis profile (no multi-line excerpt of CompleteProfile, so this test
        // is line-ending invariant on any checkout).
        const string axisAOnly = """
            {
              "schema": "wjss.manual-motion-profile/1",
              "controller": "WSB1",
              "session": {},
              "axes": {
                "A": {
                  "brakeOutput": 1
                }
              }
            }
            """;

        var profile = Assert.IsType<ManualMotionProfile>(ManualMotionProfileParser.Parse(axisAOnly).Profile);

        Assert.NotNull(profile.For(ManualMotionAxis.A));
        Assert.Null(profile.For(ManualMotionAxis.B));
        Assert.Null(profile.For(ManualMotionAxis.C));
        Assert.Null(profile.For(ManualMotionAxis.D));
    }

    [Fact]
    public void An_Unknown_Property_Refuses_The_Whole_Profile()
    {
        var result = ManualMotionProfileParser.Parse(ReplaceInProfile("\"jogSpeed\": 321,", "\"jogSpeed\": 321,\n      \"rawCommand\": \"x\","));

        Assert.False(result.Accepted);
        Assert.Null(result.Profile);
        Assert.Equal(ManualMotionCodes.ProfilePropertyUnknown, result.Code);
    }

    [Fact]
    public void A_Duplicate_Property_Refuses_The_Whole_Profile()
    {
        var result = ManualMotionProfileParser.Parse(ReplaceInProfile("\"jogSpeed\": 321,", "\"jogSpeed\": 321,\n      \"jogSpeed\": 322,"));

        Assert.Equal(ManualMotionCodes.ProfilePropertyDuplicate, result.Code);
        Assert.Null(result.Profile);
    }

    [Fact]
    public void An_Unsupported_Schema_Controller_Or_Axis_Is_Refused()
    {
        Assert.Equal(
            ManualMotionCodes.ProfileSchemaUnsupported,
            ManualMotionProfileParser.Parse(ReplaceInProfile("manual-motion-profile/1", "manual-motion-profile/2")).Code);
        Assert.Equal(
            ManualMotionCodes.ProfileControllerUnsupported,
            ManualMotionProfileParser.Parse(ReplaceInProfile("\"controller\": \"WSB1\"", "\"controller\": \"WSB2\"")).Code);
        Assert.Equal(
            ManualMotionCodes.ProfileAxisUnsupported,
            ManualMotionProfileParser.Parse(ReplaceInProfile("\"B\": {", "\"E\": {")).Code);
    }

    [Fact]
    public void Axes_C_And_D_Are_Accepted_With_Their_Owner_Confirmed_Brake_Outputs()
    {
        // A self-contained C/D profile (line-ending invariant, like the one-axis profile above).
        const string axesCandD = """
            {
              "schema": "wjss.manual-motion-profile/1",
              "controller": "WSB1",
              "session": {},
              "axes": {
                "C": {
                  "brakeOutput": 3
                },
                "D": {
                  "brakeOutput": 4
                }
              }
            }
            """;

        var result = ManualMotionProfileParser.Parse(axesCandD);

        Assert.True(result.Accepted);
        var profile = Assert.IsType<ManualMotionProfile>(result.Profile);
        Assert.Equal(3, Assert.IsType<ManualMotionAxisProfile>(profile.AxisC).BrakeOutput);
        Assert.Equal(4, Assert.IsType<ManualMotionAxisProfile>(profile.AxisD).BrakeOutput);
        Assert.NotNull(profile.For(ManualMotionAxis.C));
        Assert.NotNull(profile.For(ManualMotionAxis.D));
        Assert.Null(profile.For(ManualMotionAxis.A));
        Assert.Null(profile.For(ManualMotionAxis.B));
    }

    [Fact]
    public void A_C_Or_D_Brake_Output_That_Differs_From_The_Allocation_Is_Refused()
    {
        const string axisCWrongOutput = """
            {
              "schema": "wjss.manual-motion-profile/1",
              "controller": "WSB1",
              "session": {},
              "axes": {
                "C": {
                  "brakeOutput": 2
                }
              }
            }
            """;
        const string axisDWrongOutput = """
            {
              "schema": "wjss.manual-motion-profile/1",
              "controller": "WSB1",
              "session": {},
              "axes": {
                "D": {
                  "brakeOutput": 3
                }
              }
            }
            """;

        Assert.Equal(
            ManualMotionCodes.ProfileBrakeOutputAllocationMismatch,
            ManualMotionProfileParser.Parse(axisCWrongOutput).Code);
        Assert.Equal(
            ManualMotionCodes.ProfileBrakeOutputAllocationMismatch,
            ManualMotionProfileParser.Parse(axisDWrongOutput).Code);
    }

    [Fact]
    public void A_Brake_Output_That_Differs_From_The_Owner_Confirmed_Allocation_Is_Refused()
    {
        var result = ManualMotionProfileParser.Parse(ReplaceInProfile("\"brakeOutput\": 1,", "\"brakeOutput\": 2,"));

        Assert.Equal(ManualMotionCodes.ProfileBrakeOutputAllocationMismatch, result.Code);
        Assert.Null(result.Profile);
    }

    [Theory]
    [InlineData("\"jogSpeed\": 321,", "\"jogSpeed\": 1.5,")]
    [InlineData("\"jogSpeed\": 321,", "\"jogSpeed\": -321,")]
    [InlineData("\"jogSpeed\": 321,", "\"jogSpeed\": \"321\",")]
    [InlineData("\"motionStatusBit\": 5,", "\"motionStatusBit\": 8,")]
    [InlineData("\"motionActiveValue\": 0,", "\"motionActiveValue\": 2,")]
    [InlineData("\"brakeReleaseAction\": \"CLEAR_OUTPUT\",", "\"brakeReleaseAction\": \"RELEASE\",")]
    [InlineData("\"moveMaximumTarget\": 6000,", "\"moveMaximumTarget\": -5000,")]
    [InlineData("\"homeStatusBit\": 2,", "\"homeStatusBit\": 5,")]
    [InlineData("\"positionTolerance\": 3,", "\"positionTolerance\": -1,")]
    [InlineData("\"dualZeroPermitted\": true", "\"dualZeroPermitted\": 1")]
    [InlineData("\"observationIntervalMilliseconds\": 40,", "\"observationIntervalMilliseconds\": 0,")]
    public void A_Wrongly_Typed_Or_Out_Of_Range_Value_Refuses_The_Whole_Profile(string from, string to)
    {
        var result = ManualMotionProfileParser.Parse(ReplaceInProfile(from, to));

        Assert.False(result.Accepted);
        Assert.Equal(ManualMotionCodes.ProfileValueInvalid, result.Code);
        Assert.Null(result.Profile);
    }

    [Fact]
    public void Malformed_Empty_Commented_Or_Oversized_Text_Is_Refused()
    {
        Assert.Equal(ManualMotionCodes.ProfileNotProvided, ManualMotionProfileParser.Parse(null).Code);
        Assert.Equal(ManualMotionCodes.ProfileNotProvided, ManualMotionProfileParser.Parse("   ").Code);
        Assert.Equal(ManualMotionCodes.ProfileJsonInvalid, ManualMotionProfileParser.Parse("{ \"schema\": ").Code);
        Assert.Equal(ManualMotionCodes.ProfileJsonInvalid, ManualMotionProfileParser.Parse("[]").Code);
        Assert.Equal(
            ManualMotionCodes.ProfileJsonInvalid,
            ManualMotionProfileParser.Parse(ReplaceInProfile("\"controller\": \"WSB1\",", "\"controller\": \"WSB1\", // note")).Code);
        Assert.Equal(
            ManualMotionCodes.ProfileTooLarge,
            ManualMotionProfileParser.Parse(new string(' ', ManualMotionProfileParser.MaximumProfileLength) + "{}").Code);
    }

    [Fact]
    public void A_Missing_Section_Is_Refused()
    {
        const string withoutAxes = """
            {
              "schema": "wjss.manual-motion-profile/1",
              "controller": "WSB1",
              "session": {}
            }
            """;

        Assert.Equal(ManualMotionCodes.ProfileSectionMissing, ManualMotionProfileParser.Parse(withoutAxes).Code);
    }

    [Fact]
    public void The_Simulation_Profile_Is_Labelled_As_Built_In_And_Complete()
    {
        var profile = ManualMotionProfile.CreateSimulationProfile();

        Assert.Equal(ManualMotionProfileSource.SimulationBuiltIn, profile.Source);
        Assert.Equal("WSB1", profile.ControllerId);
        Assert.Empty(Assert.IsType<ManualMotionAxisProfile>(profile.AxisA).NotConfiguredItems());
        Assert.Empty(Assert.IsType<ManualMotionAxisProfile>(profile.AxisB).NotConfiguredItems());
        Assert.Empty(Assert.IsType<ManualMotionAxisProfile>(profile.AxisC).NotConfiguredItems());
        Assert.Empty(Assert.IsType<ManualMotionAxisProfile>(profile.AxisD).NotConfiguredItems());
        Assert.Equal(ManualMotionTopology.ConfirmedBrakeOutput(ManualMotionAxis.A), profile.AxisA!.BrakeOutput);
        Assert.Equal(ManualMotionTopology.ConfirmedBrakeOutput(ManualMotionAxis.B), profile.AxisB!.BrakeOutput);
        Assert.Equal(ManualMotionTopology.ConfirmedBrakeOutput(ManualMotionAxis.C), profile.AxisC!.BrakeOutput);
        Assert.Equal(ManualMotionTopology.ConfirmedBrakeOutput(ManualMotionAxis.D), profile.AxisD!.BrakeOutput);
    }

    [Fact]
    public void The_Topology_Reads_Role_Wording_And_Brake_Allocation_From_The_Owner_Confirmed_Catalogue()
    {
        Assert.Equal("Horizontal", ManualMotionTopology.RoleLabel(ManualMotionAxis.A));
        Assert.Equal("Vertical", ManualMotionTopology.RoleLabel(ManualMotionAxis.B));
        Assert.Equal("Horizontal", ManualMotionTopology.RoleLabel(ManualMotionAxis.C));
        Assert.Equal("Vertical", ManualMotionTopology.RoleLabel(ManualMotionAxis.D));
        Assert.Equal(1, ManualMotionTopology.ConfirmedBrakeOutput(ManualMotionAxis.A));
        Assert.Equal(2, ManualMotionTopology.ConfirmedBrakeOutput(ManualMotionAxis.B));
        Assert.Equal(3, ManualMotionTopology.ConfirmedBrakeOutput(ManualMotionAxis.C));
        Assert.Equal(4, ManualMotionTopology.ConfirmedBrakeOutput(ManualMotionAxis.D));
        Assert.Equal("A", ManualMotionTopology.AxisLetter(ManualMotionAxis.A));
        Assert.Equal("B", ManualMotionTopology.AxisLetter(ManualMotionAxis.B));
        Assert.Equal("C", ManualMotionTopology.AxisLetter(ManualMotionAxis.C));
        Assert.Equal("D", ManualMotionTopology.AxisLetter(ManualMotionAxis.D));
    }

    [Fact]
    public void The_Topology_Projects_The_Canonical_Water_Jet_Identity_Of_Every_Axis()
    {
        // WSB1 Axis A/B serve WJ1 (installed LEFT/UPPER, targets RIGHT/UPPER); Axis C/D serve WJ2
        // (installed REAR/UPPER, targets FRONT/UPPER). No axis is ever WJ3.
        Assert.Equal("WSB1", ManualMotionTopology.CabinetId(ManualMotionAxis.A));
        Assert.Equal("WSB1", ManualMotionTopology.CabinetId(ManualMotionAxis.B));
        Assert.Equal("WSB1", ManualMotionTopology.CabinetId(ManualMotionAxis.C));
        Assert.Equal("WSB1", ManualMotionTopology.CabinetId(ManualMotionAxis.D));

        Assert.Equal("WJ1", ManualMotionTopology.WaterJetId(ManualMotionAxis.A));
        Assert.Equal("WJ1", ManualMotionTopology.WaterJetId(ManualMotionAxis.B));
        Assert.Equal("WJ2", ManualMotionTopology.WaterJetId(ManualMotionAxis.C));
        Assert.Equal("WJ2", ManualMotionTopology.WaterJetId(ManualMotionAxis.D));

        Assert.Equal("LEFT / UPPER", ManualMotionTopology.InstalledPosition(ManualMotionAxis.A));
        Assert.Equal("RIGHT / UPPER", ManualMotionTopology.TargetCoverage(ManualMotionAxis.A));
        Assert.Equal("LEFT / UPPER", ManualMotionTopology.InstalledPosition(ManualMotionAxis.B));
        Assert.Equal("RIGHT / UPPER", ManualMotionTopology.TargetCoverage(ManualMotionAxis.B));
        Assert.Equal("REAR / UPPER", ManualMotionTopology.InstalledPosition(ManualMotionAxis.C));
        Assert.Equal("FRONT / UPPER", ManualMotionTopology.TargetCoverage(ManualMotionAxis.C));
        Assert.Equal("REAR / UPPER", ManualMotionTopology.InstalledPosition(ManualMotionAxis.D));
        Assert.Equal("FRONT / UPPER", ManualMotionTopology.TargetCoverage(ManualMotionAxis.D));
    }
}
