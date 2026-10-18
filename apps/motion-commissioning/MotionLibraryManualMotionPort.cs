using Wjss.Adapters.MotionLibrary;
using Wjss.Runtime.Core.Motion.Manual;

namespace Wjss.MotionCommissioning;

/// <summary>
/// The HARDWARE-CAPABLE port: it maps the Application's closed typed commands and reads onto the
/// motion-library controller owner. It is a pure enum mapping: the typed Stop goes to the existing
/// typed A/B Stop session, every other command goes to the closed commissioning plan, and no text,
/// buffer, handle or native detail crosses this type in either direction.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionLibraryManualMotionPort : IManualMotionPort
{
    private readonly ManualMotionControllerOwner _owner;

    internal MotionLibraryManualMotionPort(ManualMotionControllerOwner owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <inheritdoc/>
    public ManualMotionPortMode Mode => ManualMotionPortMode.HardwareCapable;

    /// <inheritdoc/>
    public ManualMotionPortOpenResult Open()
    {
        var outcome = _owner.Open();
        var code = outcome.DetailCode.Length == 0 || string.Equals(outcome.DetailCode, outcome.Code, StringComparison.Ordinal)
            ? outcome.Code
            : string.Concat(outcome.Code, "/", outcome.DetailCode);
        return new ManualMotionPortOpenResult(outcome.Opened, code, outcome.Generation);
    }

    /// <inheritdoc/>
    public ManualMotionPortCommandResult Execute(ManualMotionPortCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!TryMapAxis(command.Axis, out var axis))
        {
            return new ManualMotionPortCommandResult(false, false, false, ManualMotionControllerCodes.AxisOutOfDomain, _owner.Generation);
        }

        ManualMotionDispatchResult result;
        if (command.Kind == ManualMotionPortCommandKind.Stop)
        {
            result = _owner.Stop(axis);
        }
        else
        {
            if (!TryMapKind(command.Kind, out var kind))
            {
                return new ManualMotionPortCommandResult(false, false, false, ManualMotionControllerCodes.CommandOutOfDomain, _owner.Generation);
            }

            var output = command.BrakeOutput switch
            {
                1 => ManualMotionBrakeOutput.Output1,
                2 => ManualMotionBrakeOutput.Output2,
                _ => ManualMotionBrakeOutput.None,
            };
            result = _owner.Dispatch(new ManualMotionPlanCommand(kind, axis, output, command.Operand));
        }

        return new ManualMotionPortCommandResult(result.Dispatched, result.Fault, result.NativeAttempted, result.Code, result.Generation);
    }

    /// <inheritdoc/>
    public ManualMotionPortReadResult Read(ManualMotionPortRead read)
    {
        ArgumentNullException.ThrowIfNull(read);

        if (!TryMapRead(read, out var target))
        {
            return new ManualMotionPortReadResult(false, false, 0m, ManualMotionControllerCodes.ReadTargetOutOfDomain, _owner.Generation);
        }

        var result = _owner.Read(target);
        return new ManualMotionPortReadResult(result.Succeeded, result.Fault, result.Value, result.Code, result.Generation);
    }

    /// <inheritdoc/>
    public ManualMotionPortCloseResult Close()
    {
        var outcome = _owner.Close();
        return new ManualMotionPortCloseResult(outcome.BoundariesInvalidated, outcome.HandleClosed, outcome.ModulesReleased, outcome.Code);
    }

    private static bool TryMapAxis(ManualMotionAxis axis, out ManualMotionPlanAxis mapped)
    {
        switch (axis)
        {
            case ManualMotionAxis.A:
                mapped = ManualMotionPlanAxis.A;
                return true;
            case ManualMotionAxis.B:
                mapped = ManualMotionPlanAxis.B;
                return true;
            default:
                mapped = default;
                return false;
        }
    }

    private static bool TryMapKind(ManualMotionPortCommandKind kind, out ManualMotionPlanCommandKind mapped)
    {
        switch (kind)
        {
            case ManualMotionPortCommandKind.BrakeOutputSet:
                mapped = ManualMotionPlanCommandKind.BrakeOutputSet;
                return true;
            case ManualMotionPortCommandKind.BrakeOutputClear:
                mapped = ManualMotionPlanCommandKind.BrakeOutputClear;
                return true;
            case ManualMotionPortCommandKind.JogSpeed:
                mapped = ManualMotionPlanCommandKind.JogSpeed;
                return true;
            case ManualMotionPortCommandKind.Begin:
                mapped = ManualMotionPlanCommandKind.Begin;
                return true;
            case ManualMotionPortCommandKind.HomeSpeed:
                mapped = ManualMotionPlanCommandKind.HomeSpeed;
                return true;
            case ManualMotionPortCommandKind.Home:
                mapped = ManualMotionPlanCommandKind.Home;
                return true;
            case ManualMotionPortCommandKind.DefinePositionZero:
                mapped = ManualMotionPlanCommandKind.DefinePositionZero;
                return true;
            case ManualMotionPortCommandKind.DefineDualZero:
                mapped = ManualMotionPlanCommandKind.DefineDualZero;
                return true;
            case ManualMotionPortCommandKind.Acceleration:
                mapped = ManualMotionPlanCommandKind.Acceleration;
                return true;
            case ManualMotionPortCommandKind.Speed:
                mapped = ManualMotionPlanCommandKind.Speed;
                return true;
            case ManualMotionPortCommandKind.Deceleration:
                mapped = ManualMotionPlanCommandKind.Deceleration;
                return true;
            case ManualMotionPortCommandKind.PositionAbsolute:
                mapped = ManualMotionPlanCommandKind.PositionAbsolute;
                return true;
            default:
                mapped = default;
                return false;
        }
    }

    private static bool TryMapRead(ManualMotionPortRead read, out ManualMotionReadTarget target)
    {
        target = (read.Kind, read.Axis) switch
        {
            (ManualMotionReadKind.StatusWord, ManualMotionAxis.A) => ManualMotionReadTarget.StatusA,
            (ManualMotionReadKind.StatusWord, ManualMotionAxis.B) => ManualMotionReadTarget.StatusB,
            (ManualMotionReadKind.PositionTp, ManualMotionAxis.A) => ManualMotionReadTarget.TpA,
            (ManualMotionReadKind.PositionTp, ManualMotionAxis.B) => ManualMotionReadTarget.TpB,
            (ManualMotionReadKind.PositionRp, ManualMotionAxis.A) => ManualMotionReadTarget.RpA,
            (ManualMotionReadKind.PositionRp, ManualMotionAxis.B) => ManualMotionReadTarget.RpB,
            (ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.A) when read.BrakeOutput == 1 => ManualMotionReadTarget.Output1,
            (ManualMotionReadKind.BrakeOutputState, ManualMotionAxis.B) when read.BrakeOutput == 2 => ManualMotionReadTarget.Output2,
            _ => default,
        };

        return target != default(ManualMotionReadTarget);
    }
}
