using Wjss.Contracts;

namespace Wjss.Runtime.Core.Motion;

/// <summary>
/// The motion authorization gate of this software revision.
///
/// The gate is CLOSED. It is the single authority that decides whether a motion intent may leave
/// the Application, and in this revision it refuses every intent on every axis of every controller,
/// before any hardware path is reached. The gate reason is
/// <see cref="MotionVocabulary.RefusalMotionCommissioningGateNotOpen"/>: the separate motion
/// commissioning execution gate (the G2/G3 hardware authorization) is not open.
///
/// The gate is deliberately NOT a permissive component with a flag that a caller may set. There is
/// no open method, no override, no environment switch and no configuration that opens it: opening
/// belongs to the later execution gate and to the Owner's separate authorization.
/// </summary>
public static class MotionAuthorizationGate
{
    /// <summary>The state of the gate. CLOSED in this revision.</summary>
    public static string State => MotionVocabulary.GateClosed;

    /// <summary>True while the gate refuses every motion intent. Always true in this revision.</summary>
    public static bool IsClosed => true;

    /// <summary>The reason the gate is closed, in the canonical refusal vocabulary.</summary>
    public static IReadOnlyList<string> Reasons => MotionVocabulary.GateRefusalReasons;
}

/// <summary>
/// The parameters one motion intent carries.
///
/// A parameter whose status is not commissioned is display only: it is never converted and never
/// sent to a controller. The Jog Speed therefore carries its display value in controller units and
/// <see cref="Executable"/> false, and a Move target carries no value at all.
/// </summary>
public sealed record MotionCommandIntentParameters
{
    /// <summary>The speed as displayed to the Operator. A string, so it can never be mistaken for an engineering value.</summary>
    public required string SpeedDisplayValue { get; init; }

    /// <summary>The unit of <see cref="SpeedDisplayValue"/>: controller units until a conversion is verified.</summary>
    public required string SpeedUnit { get; init; }

    public required string SpeedStatus { get; init; }

    /// <summary>False while the value is an unverified candidate: it is never sent to a controller.</summary>
    public required bool SpeedExecutable { get; init; }

    /// <summary>The Move target as displayed. NONE while no software limit and no Home exist.</summary>
    public required string TargetDisplayValue { get; init; }

    public required string TargetUnit { get; init; }

    public required string TargetStatus { get; init; }

    /// <summary>False: an absolute Move requires a valid Home and commissioned software limits.</summary>
    public required bool TargetExecutable { get; init; }
}

/// <summary>
/// One motion command intent: what the Operator asked for, on which controller axis, with which
/// parameters, and under which Owner authorization.
///
/// An intent is a REQUEST for evaluation. It is not a command and it is never a hardware command
/// string: no intent member is sent to a controller, and the Application never exposes a raw
/// command surface to any UI.
/// </summary>
public sealed record MotionCommandIntent
{
    /// <summary>One of the six motion intents of <see cref="MotionVocabulary.MotionIntents"/>.</summary>
    public required string Intent { get; init; }

    public required string ControllerId { get; init; }

    public required string AxisId { get; init; }

    /// <summary>REVERSE, FORWARD or NONE. No other direction exists.</summary>
    public required string Direction { get; init; }

    /// <summary>Always the Application-owned page: the browser never owns a motion command.</summary>
    public required string RequestedBy { get; init; }

    /// <summary>The separate Owner authorization this intent runs under. NONE in this revision.</summary>
    public required string OwnerAuthorizationReference { get; init; }

    public required MotionCommandIntentParameters Parameters { get; init; }

    /// <summary>The default parameters of this revision: the Legacy Jog Speed as a display value, no Move target.</summary>
    public static MotionCommandIntentParameters DefaultParameters() => new()
    {
        SpeedDisplayValue = MotionVocabulary.JogSpeedDisplayValue,
        SpeedUnit = MotionVocabulary.SpeedUnitControllerUnits,
        SpeedStatus = MotionVocabulary.ParameterStatusUnconfirmed,
        SpeedExecutable = false,
        TargetDisplayValue = MotionVocabulary.OwnerAuthorizationNone,
        TargetUnit = MotionVocabulary.SpeedUnitControllerUnits,
        TargetStatus = MotionVocabulary.StatusNotCommissioned,
        TargetExecutable = false,
    };
}

/// <summary>
/// The result of evaluating one motion intent: whether it is authorized, the exact refusal reasons
/// when it is not, and the state of the hardware command (never issued while refused).
/// </summary>
public sealed record MotionCommandEvaluation
{
    public required string Intent { get; init; }

    public required string ControllerId { get; init; }

    public required string AxisId { get; init; }

    /// <summary>False in this revision for every intent on every axis.</summary>
    public required bool Authorized { get; init; }

    /// <summary>REFUSED in this revision.</summary>
    public required string CommandState { get; init; }

    /// <summary>The complete refusal list: the gate reason first, then the standing preconditions.</summary>
    public required IReadOnlyList<string> RefusalReasons { get; init; }

    /// <summary>NOT ISSUED: the refusal happens before any hardware path is reached.</summary>
    public required string HardwareCommandState { get; init; }

    /// <summary>False: no hardware command of any kind leaves the Application while the gate is closed.</summary>
    public required bool HardwareCommandIssued { get; init; }
}

/// <summary>
/// Evaluates a motion intent against the gate and the standing execution preconditions.
///
/// The evaluation is a pure function: the same intent produces the same evaluation every time, it
/// reads no device and it has no side effect. While the gate is closed no intent can be authorized,
/// so the standing preconditions are reported alongside the gate reason rather than instead of it:
/// the Operator sees everything that must change before motion becomes possible, in a fixed order.
/// </summary>
public static class MotionCommandGate
{
    /// <summary>
    /// Evaluates one intent on one axis. An unknown intent or an unknown controller axis is a
    /// programming error, not a refused command: it is rejected by exception so it can never be
    /// mistaken for an evaluated refusal.
    /// </summary>
    public static MotionCommandEvaluation Evaluate(MotionCommandIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!MotionVocabulary.MotionIntents.Contains(intent.Intent, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(intent), intent.Intent, "Unknown motion intent.");
        }

        var axis = MotionTopologyCatalog.FindAxis(intent.ControllerId, intent.AxisId);
        if (axis is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intent), $"{intent.ControllerId}/{intent.AxisId}", "Unknown controller axis.");
        }

        var reasons = new List<string>(MotionVocabulary.AllRefusalReasons.Count + 1);
        reasons.AddRange(MotionVocabulary.GateRefusalReasons);
        reasons.AddRange(MotionVocabulary.StandingRefusalReasons);

        return new MotionCommandEvaluation
        {
            Intent = intent.Intent,
            ControllerId = intent.ControllerId,
            AxisId = intent.AxisId,
            Authorized = false,
            CommandState = MotionVocabulary.CommandStateRefused,
            RefusalReasons = reasons,
            HardwareCommandState = MotionVocabulary.HardwareCommandStateNotIssued,
            HardwareCommandIssued = false,
        };
    }

    /// <summary>
    /// Evaluates one intent on every axis of every controller (sixteen evaluations), in catalog
    /// order. Used by the whole-controller Stop and Abort paths, which in this revision are refused
    /// exactly as any other intent is.
    /// </summary>
    public static IReadOnlyList<MotionCommandEvaluation> EvaluateForAllAxes(string intent)
    {
        if (!MotionVocabulary.MotionIntents.Contains(intent, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown motion intent.");
        }

        var axes = MotionTopologyCatalog.Axes;
        var evaluations = new MotionCommandEvaluation[axes.Count];
        for (var index = 0; index < axes.Count; index++)
        {
            evaluations[index] = Evaluate(new MotionCommandIntent
            {
                Intent = intent,
                ControllerId = axes[index].ControllerId,
                AxisId = axes[index].AxisId,
                Direction = MotionVocabulary.DirectionNone,
                RequestedBy = MotionVocabulary.RequestedByApplication,
                OwnerAuthorizationReference = MotionVocabulary.OwnerAuthorizationNone,
                Parameters = MotionCommandIntent.DefaultParameters(),
            });
        }

        return Array.AsReadOnly(evaluations);
    }
}
