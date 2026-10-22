using System.Globalization;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// The closed Manual Motion Commissioning command plan: every approved template renders exactly,
/// every out-of-domain shape refuses before any text exists, operands are bounded and rendered with
/// the invariant culture, and the plan cannot express a Stop, a relative move, an output-bank write
/// or any other command. Text is handled only inside this test of the internal renderer; no native
/// call is made and no hardware is contacted.
/// </summary>
public sealed class ManualMotionCommandPlanTests
{
    private static readonly string[] ExpectedPlanAxisNames = { "A", "B", "C", "D" };

    private static readonly string[] ExpectedBrakeOutputNames = { "None", "Output1", "Output2", "Output3", "Output4" };

    private static string Render(ManualMotionPlanCommand command)
    {
        Assert.True(ManualMotionCommandPlan.TryRender(command, out var text, out var refusalCode), refusalCode);
        Assert.Equal(string.Empty, refusalCode);
        return text;
    }

    private static string Refuse(ManualMotionPlanCommand command)
    {
        Assert.False(ManualMotionCommandPlan.TryRender(command, out var text, out var refusalCode));
        Assert.Equal(string.Empty, text);
        return refusalCode;
    }

    [Theory]
    [InlineData("A", 1)]
    [InlineData("B", 2)]
    [InlineData("C", 3)]
    [InlineData("D", 4)]
    public void Brake_Output_Allocation_Is_The_Owner_Confirmed_Mapping(string axis, int output)
    {
        var planAxis = axis switch
        {
            "A" => ManualMotionPlanAxis.A,
            "B" => ManualMotionPlanAxis.B,
            "C" => ManualMotionPlanAxis.C,
            "D" => ManualMotionPlanAxis.D,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
        };

        Assert.Equal(output, (int)ManualMotionCommandPlan.BrakeOutputOf(planAxis));
    }

    [Fact]
    public void Every_Approved_Template_Renders_Exactly()
    {
        Assert.Equal(
            "SB 1",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputSet, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.Output1, 0)));
        Assert.Equal(
            "CB 2",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputClear, ManualMotionPlanAxis.B, ManualMotionBrakeOutput.Output2, 0)));
        Assert.Equal(
            "JGA=2000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.JogSpeed, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 2000)));
        Assert.Equal(
            "JGB=-2000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.JogSpeed, ManualMotionPlanAxis.B, ManualMotionBrakeOutput.None, -2000)));
        Assert.Equal(
            "BG" + "A",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            "HVB=1500",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.HomeSpeed, ManualMotionPlanAxis.B, ManualMotionBrakeOutput.None, 1500)));
        Assert.Equal(
            "HM" + "A",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Home, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            "DPB=0",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.DefinePositionZero, ManualMotionPlanAxis.B, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            "DEA=0",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.DefineDualZero, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            "ACA=20000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Acceleration, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 20000)));
        Assert.Equal(
            "SPB=4000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Speed, ManualMotionPlanAxis.B, ManualMotionBrakeOutput.None, 4000)));
        Assert.Equal(
            "DCB=20000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Deceleration, ManualMotionPlanAxis.B, ManualMotionBrakeOutput.None, 20000)));
        Assert.Equal(
            "PAA=-3000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.PositionAbsolute, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, -3000)));
        Assert.Equal(
            "SB 3",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputSet, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.Output3, 0)));
        Assert.Equal(
            "CB 4",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputClear, ManualMotionPlanAxis.D, ManualMotionBrakeOutput.Output4, 0)));
        Assert.Equal(
            "JGC=2000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.JogSpeed, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.None, 2000)));
        Assert.Equal(
            "JGD=-2000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.JogSpeed, ManualMotionPlanAxis.D, ManualMotionBrakeOutput.None, -2000)));
        Assert.Equal(
            "BG" + "C",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            "HVD=1500",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.HomeSpeed, ManualMotionPlanAxis.D, ManualMotionBrakeOutput.None, 1500)));
        Assert.Equal(
            "HM" + "C",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Home, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            "DPD=0",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.DefinePositionZero, ManualMotionPlanAxis.D, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            "DEC=0",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.DefineDualZero, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            "ACC=20000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Acceleration, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.None, 20000)));
        Assert.Equal(
            "SPD=4000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Speed, ManualMotionPlanAxis.D, ManualMotionBrakeOutput.None, 4000)));
        Assert.Equal(
            "DCD=20000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Deceleration, ManualMotionPlanAxis.D, ManualMotionBrakeOutput.None, 20000)));
        Assert.Equal(
            "PAC=-3000",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.PositionAbsolute, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.None, -3000)));
    }

    [Fact]
    public void The_Kind_Set_Is_Closed_And_Contains_No_Stop_Or_Relative_Move()
    {
        var names = Enum.GetNames<ManualMotionPlanCommandKind>();

        Assert.Equal(12, names.Length);
        Assert.DoesNotContain("Stop", names);
        Assert.All(names, name => Assert.DoesNotContain("Relative", name, StringComparison.Ordinal));
        Assert.Equal(
            ExpectedPlanAxisNames,
            Enum.GetNames<ManualMotionPlanAxis>());
        Assert.Equal(
            ExpectedBrakeOutputNames,
            Enum.GetNames<ManualMotionBrakeOutput>());
    }

    [Fact]
    public void Out_Of_Domain_Axes_And_Kinds_Refuse_Before_Any_Text()
    {
        Assert.Equal(
            ManualMotionControllerCodes.AxisOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Begin, (ManualMotionPlanAxis)0, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.AxisOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Begin, (ManualMotionPlanAxis)5, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.CommandOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                (ManualMotionPlanCommandKind)0, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.CommandOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                (ManualMotionPlanCommandKind)99, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.CommandOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                (ManualMotionPlanCommandKind)5, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
    }

    [Fact]
    public void Brake_Commands_Accept_Only_The_Axis_Own_Output_And_No_Operand()
    {
        Assert.Equal(
            ManualMotionControllerCodes.OutputOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputSet, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.Output2, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.OutputOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputSet, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.OperandOutOfRange,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputClear, ManualMotionPlanAxis.B, ManualMotionBrakeOutput.Output2, 1)));
        Assert.Equal(
            ManualMotionControllerCodes.OperandOutOfRange,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.Output1, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.OutputOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputSet, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.Output4, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.OutputOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputSet, ManualMotionPlanAxis.D, ManualMotionBrakeOutput.Output3, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.OutputOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputClear, ManualMotionPlanAxis.C, ManualMotionBrakeOutput.Output1, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.OutputOutOfDomain,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.BrakeOutputClear, ManualMotionPlanAxis.D, ManualMotionBrakeOutput.None, 0)));
    }

    [Fact]
    public void Operand_Rules_Are_Enforced_Per_Kind()
    {
        // Jog requires a signed non-zero speed; the saturated negative bound is refused so the
        // Application's magnitude arithmetic can never overflow.
        Assert.Equal(
            ManualMotionControllerCodes.OperandOutOfRange,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.JogSpeed, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
        Assert.Equal(
            ManualMotionControllerCodes.OperandOutOfRange,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.JogSpeed, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, int.MinValue)));

        // Home speed and the Move profile values must be positive.
        foreach (var kind in new[]
        {
            ManualMotionPlanCommandKind.HomeSpeed,
            ManualMotionPlanCommandKind.Acceleration,
            ManualMotionPlanCommandKind.Speed,
            ManualMotionPlanCommandKind.Deceleration,
        })
        {
            Assert.Equal(
                ManualMotionControllerCodes.OperandOutOfRange,
                Refuse(new ManualMotionPlanCommand(kind, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
            Assert.Equal(
                ManualMotionControllerCodes.OperandOutOfRange,
                Refuse(new ManualMotionPlanCommand(kind, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, -5)));
        }

        // Zero-definition commands accept only the typed zero; the absolute target accepts any
        // bounded native integer except the saturated negative bound, zero included.
        Assert.Equal(
            ManualMotionControllerCodes.OperandOutOfRange,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.DefinePositionZero, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 5)));
        Assert.Equal(
            ManualMotionControllerCodes.OperandOutOfRange,
            Refuse(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.PositionAbsolute, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, int.MinValue)));
        Assert.Equal(
            "PAA=0",
            Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.PositionAbsolute, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0)));
    }

    [Fact]
    public void Operands_Render_With_The_Invariant_Culture_And_Within_The_Length_Bound()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var jog = Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.JogSpeed, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, -1234567));
            Assert.Equal("JGA=-1234567", jog);

            var longest = Render(new ManualMotionPlanCommand(
                ManualMotionPlanCommandKind.PositionAbsolute, ManualMotionPlanAxis.B, ManualMotionBrakeOutput.None, int.MinValue + 1));
            Assert.Equal("PAB=-2147483647", longest);
            Assert.True(longest.Length <= ManualMotionCommandPlan.MaximumCommandLength);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void The_Bound_Kernel_Refuses_Without_A_Native_Call()
    {
        // A refused render and an invalidated or unbound carrier never reach the native export:
        // these calls use inert pointer values and must return before any invocation.
        using var unbound = new BoundManualMotionCommandKernel(IntPtr.Zero, IntPtr.Zero);
        var unboundReply = unbound.Dispatch(new ManualMotionPlanCommand(
            ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0));
        Assert.False(unboundReply.Attempted);
        Assert.Equal(ManualMotionControllerCodes.BoundaryUnavailable, unboundReply.RefusalCode);

        using var bound = new BoundManualMotionCommandKernel(new IntPtr(1), new IntPtr(1));
        var refusedRender = bound.Dispatch(new ManualMotionPlanCommand(
            ManualMotionPlanCommandKind.Begin, (ManualMotionPlanAxis)9, ManualMotionBrakeOutput.None, 0));
        Assert.False(refusedRender.Attempted);
        Assert.Equal(ManualMotionControllerCodes.AxisOutOfDomain, refusedRender.RefusalCode);

        bound.Dispose();
        var afterInvalidate = bound.Dispatch(new ManualMotionPlanCommand(
            ManualMotionPlanCommandKind.Begin, ManualMotionPlanAxis.A, ManualMotionBrakeOutput.None, 0));
        Assert.False(afterInvalidate.Attempted);
        Assert.Equal(ManualMotionControllerCodes.BoundaryClosed, afterInvalidate.RefusalCode);
        Assert.True(bound.IsInvalidated);
    }
}
