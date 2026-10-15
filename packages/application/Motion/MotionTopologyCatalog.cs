using Wjss.Contracts;

namespace Wjss.Runtime.Core.Motion;

/// <summary>
/// The Owner-confirmed motion topology: four motion controllers (one per WSB cabinet), the sixteen
/// controller axes, the eight Water Jet axis allocations and the one open mapping conflict.
///
/// Every entry is DATA received from the Owner or read from the Owner-provided cabinet drawing.
/// Nothing here is derived from a device read, from the Legacy source (which is not available to
/// this repository), or from a parity assumption between layers.
///
/// Public-repository boundary: the catalog carries NO network address and NO endpoint. A controller
/// is named by the WSB cabinet that holds it; the motion port of every controller is
/// <see cref="MotionVocabulary.MotionPortNotConfigured"/> until the Owner configures it locally.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA.
/// </summary>
public static class MotionTopologyCatalog
{
    /// <summary>The controller model recorded by the Owner-provided WSB cabinet drawing.</summary>
    public const string ControllerModel = "DMC-B140-M";

    /// <summary>The four axes every controller of this model presents, in controller label order.</summary>
    public static IReadOnlyList<string> AxisIds { get; } = [MotionVocabulary.AxisA, MotionVocabulary.AxisB, MotionVocabulary.AxisC, MotionVocabulary.AxisD];

    /// <summary>The four motion controllers, one per WSB cabinet, in cabinet order.</summary>
    public static IReadOnlyList<MotionControllerIdentity> Controllers { get; } =
    [
        Controller("MC-WSB1", "WSB1"),
        Controller("MC-WSB2", "WSB2"),
        Controller("MC-WSB3", "WSB3"),
        Controller("MC-WSB4", "WSB4"),
    ];

    /// <summary>The sixteen controller axes (four controllers x four axes), controller-major.</summary>
    public static IReadOnlyList<MotionAxisIdentity> Axes { get; } =
    [
        // A and C are the Horizontal Axis; B and D are the Vertical Axis (Owner-confirmed).
        // Position sense: axis A is read through an encoder; axes B, C and D carry the installed
        // switch set. Brake outputs are allocated A-1, B-2, C-3, D-4 (Owner-confirmed).
        Axis("MC-WSB1", MotionVocabulary.AxisA, MotionVocabulary.AxisRoleHorizontal, MotionVocabulary.SenseEncoder, MotionVocabulary.BrakeOutput1),
        Axis("MC-WSB1", MotionVocabulary.AxisB, MotionVocabulary.AxisRoleVertical, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput2),
        Axis("MC-WSB1", MotionVocabulary.AxisC, MotionVocabulary.AxisRoleHorizontal, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput3),
        Axis("MC-WSB1", MotionVocabulary.AxisD, MotionVocabulary.AxisRoleVertical, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput4),
        Axis("MC-WSB2", MotionVocabulary.AxisA, MotionVocabulary.AxisRoleHorizontal, MotionVocabulary.SenseEncoder, MotionVocabulary.BrakeOutput1),
        Axis("MC-WSB2", MotionVocabulary.AxisB, MotionVocabulary.AxisRoleVertical, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput2),
        Axis("MC-WSB2", MotionVocabulary.AxisC, MotionVocabulary.AxisRoleHorizontal, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput3),
        Axis("MC-WSB2", MotionVocabulary.AxisD, MotionVocabulary.AxisRoleVertical, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput4),
        Axis("MC-WSB3", MotionVocabulary.AxisA, MotionVocabulary.AxisRoleHorizontal, MotionVocabulary.SenseEncoder, MotionVocabulary.BrakeOutput1),
        Axis("MC-WSB3", MotionVocabulary.AxisB, MotionVocabulary.AxisRoleVertical, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput2),
        Axis("MC-WSB3", MotionVocabulary.AxisC, MotionVocabulary.AxisRoleHorizontal, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput3),
        Axis("MC-WSB3", MotionVocabulary.AxisD, MotionVocabulary.AxisRoleVertical, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput4),
        Axis("MC-WSB4", MotionVocabulary.AxisA, MotionVocabulary.AxisRoleHorizontal, MotionVocabulary.SenseEncoder, MotionVocabulary.BrakeOutput1),
        Axis("MC-WSB4", MotionVocabulary.AxisB, MotionVocabulary.AxisRoleVertical, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput2),
        Axis("MC-WSB4", MotionVocabulary.AxisC, MotionVocabulary.AxisRoleHorizontal, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput3),
        Axis("MC-WSB4", MotionVocabulary.AxisD, MotionVocabulary.AxisRoleVertical, MotionVocabulary.SenseInstalled, MotionVocabulary.BrakeOutput4),
    ];

    /// <summary>The eight Water Jet axis allocations, in Water Jet ordinal order.</summary>
    public static IReadOnlyList<MotionWaterJetAxisBinding> WaterJetBindings { get; } =
    [
        Binding("WJ1", "WSB1", "MC-WSB1", MotionVocabulary.AxisA, MotionVocabulary.AxisB, MotionVocabulary.MappingConfirmedOwner),
        Binding("WJ2", "WSB1", "MC-WSB1", MotionVocabulary.AxisC, MotionVocabulary.AxisD, MotionVocabulary.MappingDrawingConflict),
        Binding("WJ3", "WSB2", "MC-WSB2", MotionVocabulary.AxisC, MotionVocabulary.AxisD, MotionVocabulary.MappingConfirmedOwner),
        Binding("WJ4", "WSB2", "MC-WSB2", MotionVocabulary.AxisA, MotionVocabulary.AxisB, MotionVocabulary.MappingDrawingConflict),
        Binding("WJ5", "WSB3", "MC-WSB3", MotionVocabulary.AxisC, MotionVocabulary.AxisD, MotionVocabulary.MappingConfirmedOwner),
        Binding("WJ6", "WSB3", "MC-WSB3", MotionVocabulary.AxisA, MotionVocabulary.AxisB, MotionVocabulary.MappingConfirmedOwner),
        Binding("WJ7", "WSB4", "MC-WSB4", MotionVocabulary.AxisA, MotionVocabulary.AxisB, MotionVocabulary.MappingConfirmedOwner),
        Binding("WJ8", "WSB4", "MC-WSB4", MotionVocabulary.AxisC, MotionVocabulary.AxisD, MotionVocabulary.MappingConfirmedOwner),
    ];

    /// <summary>
    /// The one open mapping conflict: the Owner-confirmed WJ2 / WJ4 axis allocation disagrees with
    /// the Drawing rev1 expectation for the same two Water Jets. It is published, not suppressed:
    /// the affected allocation must not be used for motion until the Owner rules.
    /// </summary>
    public static IReadOnlyList<MotionMappingConflict> MappingConflicts { get; } =
    [
        new MotionMappingConflict
        {
            ConflictId = MotionVocabulary.ConflictIdWj2Wj4,
            WaterJetIds = ["WJ2", "WJ4"],
            CabinetIds = ["WSB1", "WSB2"],
            Description = "The Owner-confirmed allocation gives WJ2 the C,D axis pair on WSB1 and WJ4 the A,B axis pair on WSB2. The Drawing rev1 expectation for those two Water Jets is the other axis pair on the same cabinet. Both cannot be correct.",
            Status = MotionVocabulary.ConflictStatusOwnerDecisionRequired,
            Effect = "The WJ2 and WJ4 allocation must not be used for motion until the Owner rules. The conflict does not block the other six Water Jets and it does not open the authorization gate.",
        },
    ];

    /// <summary>Finds one controller by identity, or null when no controller carries it.</summary>
    public static MotionControllerIdentity? FindController(string controllerId)
    {
        for (var index = 0; index < Controllers.Count; index++)
        {
            if (string.Equals(Controllers[index].ControllerId, controllerId, StringComparison.Ordinal))
            {
                return Controllers[index];
            }
        }

        return null;
    }

    /// <summary>Finds one axis by controller identity and axis label, or null when none matches.</summary>
    public static MotionAxisIdentity? FindAxis(string controllerId, string axisId)
    {
        for (var index = 0; index < Axes.Count; index++)
        {
            var axis = Axes[index];
            if (string.Equals(axis.ControllerId, controllerId, StringComparison.Ordinal)
                && string.Equals(axis.AxisId, axisId, StringComparison.Ordinal))
            {
                return axis;
            }
        }

        return null;
    }

    /// <summary>Finds the axis allocation of one Water Jet, or null when the Water Jet is unknown.</summary>
    public static MotionWaterJetAxisBinding? FindWaterJetBinding(string waterJetId)
    {
        for (var index = 0; index < WaterJetBindings.Count; index++)
        {
            if (string.Equals(WaterJetBindings[index].WaterJetId, waterJetId, StringComparison.Ordinal))
            {
                return WaterJetBindings[index];
            }
        }

        return null;
    }

    private static MotionControllerIdentity Controller(string controllerId, string cabinetId) => new()
    {
        ControllerId = controllerId,
        CabinetId = cabinetId,
        AxisCount = AxisIds.Count,
        ControllerModel = ControllerModel,
        ControllerModelStatus = MotionVocabulary.ControllerModelStatusDrawingConfirmed,
        // The cabinet identity is Owner-confirmed; one four-axis controller per cabinet follows from
        // the confirmed Water Jet allocation and is recorded as an inference, not as a confirmation.
        ControllerIdentityBasis = MotionVocabulary.BasisInferred,
    };

    private static MotionAxisIdentity Axis(
        string controllerId, string axisId, string axisRole, string motionSenseKind, string brakeOutputIdentity) => new()
    {
        ControllerId = controllerId,
        AxisId = axisId,
        AxisRole = axisRole,
        AxisRoleBasis = MotionVocabulary.BasisOwnerConfirmed,
        MotionSenseKind = motionSenseKind,
        MotionSenseBasis = MotionVocabulary.BasisOwnerConfirmed,
        BrakeOutputIdentity = brakeOutputIdentity,
        BrakeOutputMappingBasis = MotionVocabulary.BasisOwnerConfirmed,
    };

    private static MotionWaterJetAxisBinding Binding(
        string waterJetId, string cabinetId, string controllerId, string horizontalAxisId, string verticalAxisId, string mappingStatus) => new()
    {
        WaterJetId = waterJetId,
        CabinetId = cabinetId,
        ControllerId = controllerId,
        HorizontalAxisId = horizontalAxisId,
        VerticalAxisId = verticalAxisId,
        MappingStatus = mappingStatus,
        MappingBasis = mappingStatus == MotionVocabulary.MappingConfirmedOwner
            ? MotionVocabulary.BasisOwnerConfirmed
            : MotionVocabulary.BasisDrawingRev1,
    };
}
