using System.Globalization;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The closed axis domain of the WSB1 Manual Motion Commissioning plan: Axis A (Horizontal) and Axis B
/// (Vertical). The enumeration starts at 1, so a defaulted value is refused. Axis C, Axis D and any
/// combined or multi-axis value cannot be expressed.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal enum ManualMotionPlanAxis
{
    /// <summary>Axis A: the Horizontal Axis.</summary>
    A = 1,

    /// <summary>Axis B: the Vertical Axis.</summary>
    B = 2,
}

/// <summary>
/// The closed brake-output domain of the plan. The Owner-confirmed allocation is fixed: Axis A uses
/// output 1 and Axis B uses output 2. No other output, and no generic output number, can be expressed.
/// </summary>
internal enum ManualMotionBrakeOutput
{
    /// <summary>No output (every non-brake command).</summary>
    None = 0,

    /// <summary>Output 1: the brake output of Axis A.</summary>
    Output1 = 1,

    /// <summary>Output 2: the brake output of Axis B.</summary>
    Output2 = 2,
}

/// <summary>
/// The closed command kinds of the plan. The typed Stop is deliberately absent: Stop is dispatched
/// only through the existing typed A/B Stop plan and session, never through this plan.
/// </summary>
internal enum ManualMotionPlanCommandKind
{
    /// <summary>Set one brake output (logical state). The release/apply meaning is Owner-local configuration.</summary>
    BrakeOutputSet = 1,

    /// <summary>Clear one brake output (logical state).</summary>
    BrakeOutputClear = 2,

    /// <summary>Load a signed, non-zero native jog speed.</summary>
    JogSpeed = 3,

    /// <summary>Begin the motion loaded for one axis.</summary>
    Begin = 4,

    /// <summary>Load a positive native home speed.</summary>
    HomeSpeed = 6,

    /// <summary>Arm the controller home sequence.</summary>
    Home = 7,

    /// <summary>Define position zero (zero is the only operand).</summary>
    DefinePositionZero = 8,

    /// <summary>Define dual zero (zero is the only operand).</summary>
    DefineDualZero = 9,

    /// <summary>Load a positive native acceleration.</summary>
    Acceleration = 10,

    /// <summary>Load a positive native speed.</summary>
    Speed = 11,

    /// <summary>Load a positive native deceleration.</summary>
    Deceleration = 12,

    /// <summary>Load an absolute native target.</summary>
    PositionAbsolute = 13,
}

/// <summary>One typed plan command. It has no text member and no public constructor path to text.</summary>
/// <param name="Kind">The closed command kind.</param>
/// <param name="Axis">The closed A/B axis.</param>
/// <param name="Output">The closed brake output for brake kinds; <see cref="ManualMotionBrakeOutput.None"/> otherwise.</param>
/// <param name="Operand">The typed native integer operand; zero when the kind carries none.</param>
internal readonly record struct ManualMotionPlanCommand(
    ManualMotionPlanCommandKind Kind,
    ManualMotionPlanAxis Axis,
    ManualMotionBrakeOutput Output,
    int Operand);

/// <summary>
/// The only place in the adapter where a Manual Motion Commissioning command-language template exists.
///
/// Approved construction rule (scoped amendment of the intake protocol for this plan only): a fixed
/// template may carry ONE typed, bounded integer operand. The mnemonic, the axis letter and the output
/// number come only from closed enumerations; the operand is rendered with the invariant culture;
/// the rendered length is bounded; and every rendered form is pinned by the plan tests. There is no
/// method that accepts caller text, a prefix, a suffix, a separator, a terminator or a template.
///
/// Template shapes follow the Legacy executable command shapes recorded in
/// docs/evidence/LEGACY_MOTION_COMMISSIONING_BASELINE.md. Legacy usage is parity evidence, not vendor
/// semantic verification: the vendor-evidence status of every mnemonic stays pending in
/// docs/evidence/GCLIB_2_6_0_API_USAGE_BASELINE.md, and nothing here claims a physical effect.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal static class ManualMotionCommandPlan
{
    /// <summary>The longest accepted rendered command. The longest possible form is fifteen characters.</summary>
    internal const int MaximumCommandLength = 24;

    /// <summary>The fixed size of the reply buffer for one native dispatch.</summary>
    internal const int MaximumReplyBytes = 32;

    /// <summary>True only for the two members of the closed axis domain.</summary>
    internal static bool IsDefined(ManualMotionPlanAxis axis) =>
        axis == ManualMotionPlanAxis.A || axis == ManualMotionPlanAxis.B;

    /// <summary>The Owner-confirmed brake output of one axis.</summary>
    internal static ManualMotionBrakeOutput BrakeOutputOf(ManualMotionPlanAxis axis) => axis switch
    {
        ManualMotionPlanAxis.A => ManualMotionBrakeOutput.Output1,
        ManualMotionPlanAxis.B => ManualMotionBrakeOutput.Output2,
        _ => ManualMotionBrakeOutput.None,
    };

    /// <summary>
    /// Renders one typed command. Returns false, with an empty text and a canonical refusal code,
    /// for anything outside the closed domain or the structural operand bounds.
    /// </summary>
    internal static bool TryRender(ManualMotionPlanCommand command, out string text, out string refusalCode)
    {
        text = string.Empty;
        if (!IsDefined(command.Axis))
        {
            refusalCode = ManualMotionControllerCodes.AxisOutOfDomain;
            return false;
        }

        var axis = command.Axis == ManualMotionPlanAxis.A ? "A" : "B";
        string rendered;
        switch (command.Kind)
        {
            case ManualMotionPlanCommandKind.BrakeOutputSet:
            case ManualMotionPlanCommandKind.BrakeOutputClear:
                if (command.Output == ManualMotionBrakeOutput.None || command.Output != BrakeOutputOf(command.Axis))
                {
                    refusalCode = ManualMotionControllerCodes.OutputOutOfDomain;
                    return false;
                }

                if (command.Operand != 0)
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                var output = command.Output == ManualMotionBrakeOutput.Output1 ? "1" : "2";
                rendered = command.Kind == ManualMotionPlanCommandKind.BrakeOutputSet
                    ? string.Concat("SB ", output)
                    : string.Concat("CB ", output);
                break;

            case ManualMotionPlanCommandKind.JogSpeed:
                if (command.Operand == 0 || command.Operand == int.MinValue || command.Output != ManualMotionBrakeOutput.None)
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("JG", axis, "=", Operand(command.Operand));
                break;

            case ManualMotionPlanCommandKind.Begin:
                if (!NoOperand(command))
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("BG", axis);
                break;

            case ManualMotionPlanCommandKind.HomeSpeed:
                if (!PositiveOperand(command))
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("HV", axis, "=", Operand(command.Operand));
                break;

            case ManualMotionPlanCommandKind.Home:
                if (!NoOperand(command))
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("HM", axis);
                break;

            case ManualMotionPlanCommandKind.DefinePositionZero:
                if (!NoOperand(command))
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("DP", axis, "=0");
                break;

            case ManualMotionPlanCommandKind.DefineDualZero:
                if (!NoOperand(command))
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("DE", axis, "=0");
                break;

            case ManualMotionPlanCommandKind.Acceleration:
                if (!PositiveOperand(command))
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("AC", axis, "=", Operand(command.Operand));
                break;

            case ManualMotionPlanCommandKind.Speed:
                if (!PositiveOperand(command))
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("SP", axis, "=", Operand(command.Operand));
                break;

            case ManualMotionPlanCommandKind.Deceleration:
                if (!PositiveOperand(command))
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("DC", axis, "=", Operand(command.Operand));
                break;

            case ManualMotionPlanCommandKind.PositionAbsolute:
                if (command.Operand == int.MinValue || command.Output != ManualMotionBrakeOutput.None)
                {
                    refusalCode = ManualMotionControllerCodes.OperandOutOfRange;
                    return false;
                }

                rendered = string.Concat("PA", axis, "=", Operand(command.Operand));
                break;

            default:
                refusalCode = ManualMotionControllerCodes.CommandOutOfDomain;
                return false;
        }

        if (rendered.Length == 0 || rendered.Length > MaximumCommandLength)
        {
            refusalCode = ManualMotionControllerCodes.CommandTooLong;
            return false;
        }

        text = rendered;
        refusalCode = string.Empty;
        return true;
    }

    private static string Operand(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool NoOperand(ManualMotionPlanCommand command) =>
        command.Operand == 0 && command.Output == ManualMotionBrakeOutput.None;

    private static bool PositiveOperand(ManualMotionPlanCommand command) =>
        command.Operand > 0 && command.Output == ManualMotionBrakeOutput.None;
}
