using System.Text.Json;
using Wjss.Contracts;
using Wjss.Domain;

namespace Wjss.Runtime.Core.Motion.Manual;

/// <summary>Where a profile came from. The built-in simulation profile can never configure the hardware-capable path.</summary>
public enum ManualMotionProfileSource
{
    /// <summary>An Owner-local profile file, kept outside the repository. Its values are never committed or displayed.</summary>
    OwnerLocalFile = 1,

    /// <summary>The built-in, clearly synthetic simulation profile. SIMULATED MODE ONLY.</summary>
    SimulationBuiltIn = 2,
}

/// <summary>
/// The Owner-local commissioning values of one axis, in NATIVE controller units only. Every value is
/// optional: an absent or null value is NOT_CONFIGURED and refuses every operation that needs it. No
/// default exists for any motion-producing value, and no Legacy value is promoted here.
/// </summary>
public sealed class ManualMotionAxisProfile
{
    /// <summary>The brake output number (1 for Axis A, 2 for Axis B, 3 for Axis C, 4 for Axis D; must equal the Owner-confirmed allocation).</summary>
    public int? BrakeOutput { get; init; }

    /// <summary>Which output action releases the brake. NOT_CONFIGURED refuses every brake release.</summary>
    public ManualMotionBrakeReleaseAction? BrakeReleaseAction { get; init; }

    /// <summary>The wait after a brake release before any motion command, in milliseconds.</summary>
    public int? BrakeSettleMilliseconds { get; init; }

    /// <summary>The jog speed magnitude in native units; the direction supplies the sign.</summary>
    public int? JogSpeed { get; init; }

    /// <summary>The home speed in native units.</summary>
    public int? HomeSpeed { get; init; }

    /// <summary>The Home supervision timeout in milliseconds.</summary>
    public int? HomeTimeoutMilliseconds { get; init; }

    /// <summary>The move acceleration in native units.</summary>
    public int? MoveAcceleration { get; init; }

    /// <summary>The move speed in native units.</summary>
    public int? MoveSpeed { get; init; }

    /// <summary>The move deceleration in native units.</summary>
    public int? MoveDeceleration { get; init; }

    /// <summary>The smallest commissioned absolute target in native units.</summary>
    public int? MoveMinimumTarget { get; init; }

    /// <summary>The largest commissioned absolute target in native units.</summary>
    public int? MoveMaximumTarget { get; init; }

    /// <summary>The position tolerance in native units, used for zero and move verification.</summary>
    public int? PositionTolerance { get; init; }

    /// <summary>The Move supervision timeout in milliseconds.</summary>
    public int? MotionTimeoutMilliseconds { get; init; }

    /// <summary>The time allowed, after a Stop, for an observation that shows the axis not moving, in milliseconds.</summary>
    public int? StopTimeoutMilliseconds { get; init; }

    /// <summary>The Owner-attested status bit (0-7) that reports motion.</summary>
    public int? MotionStatusBit { get; init; }

    /// <summary>The value (0 or 1) of <see cref="MotionStatusBit"/> that means moving.</summary>
    public int? MotionActiveValue { get; init; }

    /// <summary>The Owner-attested status bit (0-7) that reports the home input.</summary>
    public int? HomeStatusBit { get; init; }

    /// <summary>The value (0 or 1) of <see cref="HomeStatusBit"/> that means the home input is active.</summary>
    public int? HomeActiveValue { get; init; }

    /// <summary>True only when the Owner-local profile explicitly permits the dual-zero definition on this axis.</summary>
    public bool DualZeroPermitted { get; init; }

    /// <summary>The profile property names that are NOT_CONFIGURED for this axis, in schema order.</summary>
    public IReadOnlyList<string> NotConfiguredItems()
    {
        var missing = new List<string>();
        AddIfMissing(missing, BrakeOutput, "brakeOutput");
        if (BrakeReleaseAction is null)
        {
            missing.Add("brakeReleaseAction");
        }

        AddIfMissing(missing, BrakeSettleMilliseconds, "brakeSettleMilliseconds");
        AddIfMissing(missing, JogSpeed, "jogSpeed");
        AddIfMissing(missing, HomeSpeed, "homeSpeed");
        AddIfMissing(missing, HomeTimeoutMilliseconds, "homeTimeoutMilliseconds");
        AddIfMissing(missing, MoveAcceleration, "moveAcceleration");
        AddIfMissing(missing, MoveSpeed, "moveSpeed");
        AddIfMissing(missing, MoveDeceleration, "moveDeceleration");
        AddIfMissing(missing, MoveMinimumTarget, "moveMinimumTarget");
        AddIfMissing(missing, MoveMaximumTarget, "moveMaximumTarget");
        AddIfMissing(missing, PositionTolerance, "positionTolerance");
        AddIfMissing(missing, MotionTimeoutMilliseconds, "motionTimeoutMilliseconds");
        AddIfMissing(missing, StopTimeoutMilliseconds, "stopTimeoutMilliseconds");
        AddIfMissing(missing, MotionStatusBit, "motionStatusBit");
        AddIfMissing(missing, MotionActiveValue, "motionActiveValue");
        AddIfMissing(missing, HomeStatusBit, "homeStatusBit");
        AddIfMissing(missing, HomeActiveValue, "homeActiveValue");
        if (!DualZeroPermitted)
        {
            missing.Add("dualZeroPermitted");
        }

        return missing;
    }

    private static void AddIfMissing(List<string> missing, int? value, string name)
    {
        if (value is null)
        {
            missing.Add(name);
        }
    }
}

/// <summary>
/// The Owner-local Manual Motion Commissioning profile for WSB1. It names the controller, the
/// allowed axes (only the axes present are allowed) and the session timing values. Values are native
/// controller units and milliseconds; no engineering-unit conversion exists.
/// </summary>
public sealed class ManualMotionProfile
{
    /// <summary>Where the profile came from.</summary>
    public required ManualMotionProfileSource Source { get; init; }

    /// <summary>The cabinet identity of the controller (WSB1 only in this scope).</summary>
    public required string ControllerId { get; init; }

    /// <summary>The interval between two observation reads, in milliseconds.</summary>
    public int? ObservationIntervalMilliseconds { get; init; }

    /// <summary>The age beyond which an observation is no longer current, in milliseconds.</summary>
    public int? ObservationFreshnessMilliseconds { get; init; }

    /// <summary>The time without a hold-to-run heartbeat after which Jog is stopped, in milliseconds.</summary>
    public int? JogHeartbeatTimeoutMilliseconds { get; init; }

    /// <summary>The maximum duration of one hold-to-run Jog, in milliseconds.</summary>
    public int? JogMaximumHoldMilliseconds { get; init; }

    /// <summary>The Axis A (Horizontal) profile, or null when Axis A is not allowed.</summary>
    public ManualMotionAxisProfile? AxisA { get; init; }

    /// <summary>The Axis B (Vertical) profile, or null when Axis B is not allowed.</summary>
    public ManualMotionAxisProfile? AxisB { get; init; }

    /// <summary>The Axis C (Horizontal) profile, or null when Axis C is not allowed.</summary>
    public ManualMotionAxisProfile? AxisC { get; init; }

    /// <summary>The Axis D (Vertical) profile, or null when Axis D is not allowed.</summary>
    public ManualMotionAxisProfile? AxisD { get; init; }

    /// <summary>The profile of one axis, or null when the axis is not allowed by this profile.</summary>
    public ManualMotionAxisProfile? For(ManualMotionAxis axis) => axis switch
    {
        ManualMotionAxis.A => AxisA,
        ManualMotionAxis.B => AxisB,
        ManualMotionAxis.C => AxisC,
        ManualMotionAxis.D => AxisD,
        _ => null,
    };

    /// <summary>
    /// The built-in SIMULATION profile. Every value is synthetic and chosen only so the simulated
    /// lifecycle can be reviewed: the status-bit layout in particular is deliberately arbitrary and is
    /// NOT derived from any controller documentation or Legacy decode. It can never configure the
    /// hardware-capable path (the session refuses it there).
    /// </summary>
    public static ManualMotionProfile CreateSimulationProfile() => new()
    {
        Source = ManualMotionProfileSource.SimulationBuiltIn,
        ControllerId = ManualMotionProfileParser.SupportedController,
        ObservationIntervalMilliseconds = 20,
        ObservationFreshnessMilliseconds = 600,
        JogHeartbeatTimeoutMilliseconds = 500,
        JogMaximumHoldMilliseconds = 15000,
        AxisA = SimulationAxis(1),
        AxisB = SimulationAxis(2),
        AxisC = SimulationAxis(3),
        AxisD = SimulationAxis(4),
    };

    private static ManualMotionAxisProfile SimulationAxis(int brakeOutput) => new()
    {
        BrakeOutput = brakeOutput,
        BrakeReleaseAction = ManualMotionBrakeReleaseAction.SetOutput,
        BrakeSettleMilliseconds = 100,
        JogSpeed = 2000,
        HomeSpeed = 1500,
        HomeTimeoutMilliseconds = 30000,
        MoveAcceleration = 10000,
        MoveSpeed = 4000,
        MoveDeceleration = 10000,
        MoveMinimumTarget = -20000,
        MoveMaximumTarget = 20000,
        PositionTolerance = 2,
        MotionTimeoutMilliseconds = 20000,
        StopTimeoutMilliseconds = 2000,
        MotionStatusBit = 6,
        MotionActiveValue = 1,
        HomeStatusBit = 3,
        HomeActiveValue = 1,
        DualZeroPermitted = true,
    };
}

/// <summary>The result of reading a profile. A refused profile carries no values.</summary>
public sealed class ManualMotionProfileResult
{
    private ManualMotionProfileResult(bool accepted, ManualMotionProfile? profile, string code)
    {
        Accepted = accepted;
        Profile = profile;
        Code = code;
    }

    /// <summary>True when the profile was accepted.</summary>
    public bool Accepted { get; }

    /// <summary>The accepted profile, or null when refused.</summary>
    public ManualMotionProfile? Profile { get; }

    /// <summary>The canonical code of the result.</summary>
    public string Code { get; }

    /// <summary>A refused result with its canonical code.</summary>
    public static ManualMotionProfileResult Refused(string code) => new(false, null, code);

    /// <summary>An accepted result for one profile.</summary>
    public static ManualMotionProfileResult FromProfile(ManualMotionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new ManualMotionProfileResult(true, profile, ManualMotionCodes.ProfileAccepted);
    }
}

/// <summary>
/// WSB1 topology facts this slice needs, read from the Owner-confirmed motion catalogue rather than
/// restated: the controller identity, the role wording and the brake-output allocation.
/// </summary>
public static class ManualMotionTopology
{
    /// <summary>The controller identity of WSB1 in the motion catalogue.</summary>
    public const string ControllerId = "MC-WSB1";

    /// <summary>The diagnostic controller axis letter of one axis.</summary>
    public static string AxisLetter(ManualMotionAxis axis) => axis switch
    {
        ManualMotionAxis.A => MotionVocabulary.AxisA,
        ManualMotionAxis.B => MotionVocabulary.AxisB,
        ManualMotionAxis.C => MotionVocabulary.AxisC,
        ManualMotionAxis.D => MotionVocabulary.AxisD,
        _ => string.Empty,
    };

    /// <summary>The Operator role wording of one axis ("Horizontal" or "Vertical"), from the catalogue.</summary>
    public static string RoleLabel(ManualMotionAxis axis)
    {
        var identity = MotionTopologyCatalog.FindAxis(ControllerId, AxisLetter(axis));
        if (identity is null)
        {
            return string.Empty;
        }

        return MotionVocabulary.StatusText.TryGetValue(identity.AxisRole, out var label) ? label : string.Empty;
    }

    /// <summary>The Owner-confirmed brake output number of one axis (1 to 4), or 0 when none is allocated.</summary>
    public static int ConfirmedBrakeOutput(ManualMotionAxis axis)
    {
        var identity = MotionTopologyCatalog.FindAxis(ControllerId, AxisLetter(axis));
        if (identity is null)
        {
            return 0;
        }

        if (string.Equals(identity.BrakeOutputIdentity, MotionVocabulary.BrakeOutput1, StringComparison.Ordinal))
        {
            return 1;
        }

        if (string.Equals(identity.BrakeOutputIdentity, MotionVocabulary.BrakeOutput2, StringComparison.Ordinal))
        {
            return 2;
        }

        if (string.Equals(identity.BrakeOutputIdentity, MotionVocabulary.BrakeOutput3, StringComparison.Ordinal))
        {
            return 3;
        }

        if (string.Equals(identity.BrakeOutputIdentity, MotionVocabulary.BrakeOutput4, StringComparison.Ordinal))
        {
            return 4;
        }

        return 0;
    }

    /// <summary>The cabinet identity (WSB) of one axis, from the motion catalogue binding.</summary>
    public static string CabinetId(ManualMotionAxis axis) => BindingFor(axis)?.CabinetId ?? string.Empty;

    /// <summary>The canonical Water Jet identity (for example WJ2 or WJ3) of one axis, from the motion catalogue binding.</summary>
    public static string WaterJetId(ManualMotionAxis axis) => BindingFor(axis)?.WaterJetId ?? string.Empty;

    /// <summary>
    /// The canonical installed position wording of the Water Jet of one axis (wall / tier), projected
    /// from the canonical Water Jet topology. Diagnostic identity only; never an authorization.
    /// </summary>
    public static string InstalledPosition(ManualMotionAxis axis)
    {
        var waterJet = WaterJetFor(axis);
        return waterJet is null
            ? string.Empty
            : string.Concat(waterJet.InstalledWall.ToString(), " / ", waterJet.InstalledRegion.ToString());
    }

    /// <summary>
    /// The canonical target coverage wording of the Water Jet of one axis (wall / tier), projected
    /// from the canonical Water Jet topology. Diagnostic identity only; never an authorization.
    /// </summary>
    public static string TargetCoverage(ManualMotionAxis axis)
    {
        var waterJet = WaterJetFor(axis);
        return waterJet is null
            ? string.Empty
            : string.Concat(waterJet.TargetWall.ToString(), " / ", waterJet.TargetRegion.ToString());
    }

    private static MotionWaterJetAxisBinding? BindingFor(ManualMotionAxis axis)
    {
        var letter = AxisLetter(axis);
        if (letter.Length == 0)
        {
            return null;
        }

        var bindings = MotionTopologyCatalog.WaterJetBindings;
        for (var index = 0; index < bindings.Count; index++)
        {
            var binding = bindings[index];
            if (string.Equals(binding.ControllerId, ControllerId, StringComparison.Ordinal)
                && (string.Equals(binding.HorizontalAxisId, letter, StringComparison.Ordinal)
                    || string.Equals(binding.VerticalAxisId, letter, StringComparison.Ordinal)))
            {
                return binding;
            }
        }

        return null;
    }

    private static WaterJetConfiguration? WaterJetFor(ManualMotionAxis axis)
    {
        var waterJetId = WaterJetId(axis);
        return waterJetId.Length == 0 ? null : WaterJetTopologyCatalog.FindWaterJet(waterJetId);
    }
}

/// <summary>
/// Strict, fail-closed reader of the Owner-local profile text. The caller reads the file; this type
/// performs no I/O. Any malformed, unknown, duplicate, wrongly typed or out-of-range value refuses
/// the whole profile, so nothing is dispatched on a partially understood profile. An absent or null
/// value is NOT_CONFIGURED and refuses only the operations that need it.
/// </summary>
public static class ManualMotionProfileParser
{
    /// <summary>The only supported schema identifier.</summary>
    public const string SchemaIdentifier = "wjss.manual-motion-profile/1";

    /// <summary>The only supported controller (cabinet) identity.</summary>
    public const string SupportedController = "WSB1";

    /// <summary>The largest accepted profile text, in characters.</summary>
    public const int MaximumProfileLength = 65536;

    private const string SchemaProperty = "schema";
    private const string ControllerProperty = "controller";
    private const string SessionProperty = "session";
    private const string AxesProperty = "axes";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8,
    };

    private static readonly HashSet<string> TopLevelProperties = new(StringComparer.Ordinal)
    {
        SchemaProperty,
        ControllerProperty,
        SessionProperty,
        AxesProperty,
    };

    private static readonly HashSet<string> SessionProperties = new(StringComparer.Ordinal)
    {
        "observationIntervalMilliseconds",
        "observationFreshnessMilliseconds",
        "jogHeartbeatTimeoutMilliseconds",
        "jogMaximumHoldMilliseconds",
    };

    private static readonly HashSet<string> AxisProperties = new(StringComparer.Ordinal)
    {
        "brakeOutput",
        "brakeReleaseAction",
        "brakeSettleMilliseconds",
        "jogSpeed",
        "homeSpeed",
        "homeTimeoutMilliseconds",
        "moveAcceleration",
        "moveSpeed",
        "moveDeceleration",
        "moveMinimumTarget",
        "moveMaximumTarget",
        "positionTolerance",
        "motionTimeoutMilliseconds",
        "stopTimeoutMilliseconds",
        "motionStatusBit",
        "motionActiveValue",
        "homeStatusBit",
        "homeActiveValue",
        "dualZeroPermitted",
    };

    /// <summary>Reads one Owner-local profile text. Never throws for malformed input.</summary>
    public static ManualMotionProfileResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileNotProvided);
        }

        if (json.Length > MaximumProfileLength)
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileTooLarge);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException)
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileJsonInvalid);
        }

        using (document)
        {
            return ParseRoot(document.RootElement);
        }
    }

    private static ManualMotionProfileResult ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileJsonInvalid);
        }

        var shapeRefusal = CheckProperties(root, TopLevelProperties);
        if (shapeRefusal is not null)
        {
            return ManualMotionProfileResult.Refused(shapeRefusal);
        }

        if (!root.TryGetProperty(SchemaProperty, out var schema)
            || schema.ValueKind != JsonValueKind.String
            || !string.Equals(schema.GetString(), SchemaIdentifier, StringComparison.Ordinal))
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileSchemaUnsupported);
        }

        if (!root.TryGetProperty(ControllerProperty, out var controller)
            || controller.ValueKind != JsonValueKind.String
            || !string.Equals(controller.GetString(), SupportedController, StringComparison.Ordinal))
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileControllerUnsupported);
        }

        if (!root.TryGetProperty(SessionProperty, out var session) || session.ValueKind != JsonValueKind.Object)
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileSectionMissing);
        }

        if (!root.TryGetProperty(AxesProperty, out var axes) || axes.ValueKind != JsonValueKind.Object)
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileSectionMissing);
        }

        var sessionRefusal = CheckProperties(session, SessionProperties);
        if (sessionRefusal is not null)
        {
            return ManualMotionProfileResult.Refused(sessionRefusal);
        }

        if (!TryReadMilliseconds(session, "observationIntervalMilliseconds", out var interval)
            || !TryReadMilliseconds(session, "observationFreshnessMilliseconds", out var freshness)
            || !TryReadMilliseconds(session, "jogHeartbeatTimeoutMilliseconds", out var heartbeat)
            || !TryReadMilliseconds(session, "jogMaximumHoldMilliseconds", out var maximumHold))
        {
            return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileValueInvalid);
        }

        ManualMotionAxisProfile? axisA = null;
        ManualMotionAxisProfile? axisB = null;
        ManualMotionAxisProfile? axisC = null;
        ManualMotionAxisProfile? axisD = null;
        var seenAxes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in axes.EnumerateObject())
        {
            if (!seenAxes.Add(property.Name))
            {
                return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfilePropertyDuplicate);
            }

            ManualMotionAxis axis;
            if (string.Equals(property.Name, MotionVocabulary.AxisA, StringComparison.Ordinal))
            {
                axis = ManualMotionAxis.A;
            }
            else if (string.Equals(property.Name, MotionVocabulary.AxisB, StringComparison.Ordinal))
            {
                axis = ManualMotionAxis.B;
            }
            else if (string.Equals(property.Name, MotionVocabulary.AxisC, StringComparison.Ordinal))
            {
                axis = ManualMotionAxis.C;
            }
            else if (string.Equals(property.Name, MotionVocabulary.AxisD, StringComparison.Ordinal))
            {
                axis = ManualMotionAxis.D;
            }
            else
            {
                return ManualMotionProfileResult.Refused(ManualMotionCodes.ProfileAxisUnsupported);
            }

            var axisRefusal = TryParseAxis(property.Value, axis, out var axisProfile);
            if (axisRefusal is not null || axisProfile is null)
            {
                return ManualMotionProfileResult.Refused(axisRefusal ?? ManualMotionCodes.ProfileValueInvalid);
            }

            switch (axis)
            {
                case ManualMotionAxis.A:
                    axisA = axisProfile;
                    break;
                case ManualMotionAxis.B:
                    axisB = axisProfile;
                    break;
                case ManualMotionAxis.C:
                    axisC = axisProfile;
                    break;
                default:
                    axisD = axisProfile;
                    break;
            }
        }

        return ManualMotionProfileResult.FromProfile(new ManualMotionProfile
        {
            Source = ManualMotionProfileSource.OwnerLocalFile,
            ControllerId = SupportedController,
            ObservationIntervalMilliseconds = interval,
            ObservationFreshnessMilliseconds = freshness,
            JogHeartbeatTimeoutMilliseconds = heartbeat,
            JogMaximumHoldMilliseconds = maximumHold,
            AxisA = axisA,
            AxisB = axisB,
            AxisC = axisC,
            AxisD = axisD,
        });
    }

    private static string? TryParseAxis(JsonElement element, ManualMotionAxis axis, out ManualMotionAxisProfile? profile)
    {
        profile = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return ManualMotionCodes.ProfileValueInvalid;
        }

        var shapeRefusal = CheckProperties(element, AxisProperties);
        if (shapeRefusal is not null)
        {
            return shapeRefusal;
        }

        if (!TryReadInt(element, "brakeOutput", 1, 4, out var brakeOutput)
            || !TryReadReleaseAction(element, out var releaseAction)
            || !TryReadMilliseconds(element, "brakeSettleMilliseconds", out var settle)
            || !TryReadInt(element, "jogSpeed", 1, int.MaxValue, out var jogSpeed)
            || !TryReadInt(element, "homeSpeed", 1, int.MaxValue, out var homeSpeed)
            || !TryReadMilliseconds(element, "homeTimeoutMilliseconds", out var homeTimeout)
            || !TryReadInt(element, "moveAcceleration", 1, int.MaxValue, out var acceleration)
            || !TryReadInt(element, "moveSpeed", 1, int.MaxValue, out var speed)
            || !TryReadInt(element, "moveDeceleration", 1, int.MaxValue, out var deceleration)
            || !TryReadInt(element, "moveMinimumTarget", -int.MaxValue, int.MaxValue, out var minimumTarget)
            || !TryReadInt(element, "moveMaximumTarget", -int.MaxValue, int.MaxValue, out var maximumTarget)
            || !TryReadInt(element, "positionTolerance", 0, int.MaxValue, out var tolerance)
            || !TryReadMilliseconds(element, "motionTimeoutMilliseconds", out var motionTimeout)
            || !TryReadMilliseconds(element, "stopTimeoutMilliseconds", out var stopTimeout)
            || !TryReadInt(element, "motionStatusBit", 0, 7, out var motionBit)
            || !TryReadInt(element, "motionActiveValue", 0, 1, out var motionActive)
            || !TryReadInt(element, "homeStatusBit", 0, 7, out var homeBit)
            || !TryReadInt(element, "homeActiveValue", 0, 1, out var homeActive)
            || !TryReadBoolean(element, "dualZeroPermitted", out var dualZeroPermitted))
        {
            return ManualMotionCodes.ProfileValueInvalid;
        }

        if (brakeOutput is not null && brakeOutput.Value != ManualMotionTopology.ConfirmedBrakeOutput(axis))
        {
            return ManualMotionCodes.ProfileBrakeOutputAllocationMismatch;
        }

        if (minimumTarget is not null && maximumTarget is not null && minimumTarget.Value >= maximumTarget.Value)
        {
            return ManualMotionCodes.ProfileValueInvalid;
        }

        if (motionBit is not null && homeBit is not null && motionBit.Value == homeBit.Value)
        {
            return ManualMotionCodes.ProfileValueInvalid;
        }

        profile = new ManualMotionAxisProfile
        {
            BrakeOutput = brakeOutput,
            BrakeReleaseAction = releaseAction,
            BrakeSettleMilliseconds = settle,
            JogSpeed = jogSpeed,
            HomeSpeed = homeSpeed,
            HomeTimeoutMilliseconds = homeTimeout,
            MoveAcceleration = acceleration,
            MoveSpeed = speed,
            MoveDeceleration = deceleration,
            MoveMinimumTarget = minimumTarget,
            MoveMaximumTarget = maximumTarget,
            PositionTolerance = tolerance,
            MotionTimeoutMilliseconds = motionTimeout,
            StopTimeoutMilliseconds = stopTimeout,
            MotionStatusBit = motionBit,
            MotionActiveValue = motionActive,
            HomeStatusBit = homeBit,
            HomeActiveValue = homeActive,
            DualZeroPermitted = dualZeroPermitted,
        };

        return null;
    }

    private static string? CheckProperties(JsonElement element, HashSet<string> allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                return ManualMotionCodes.ProfilePropertyUnknown;
            }

            if (!seen.Add(property.Name))
            {
                return ManualMotionCodes.ProfilePropertyDuplicate;
            }
        }

        return null;
    }

    private static bool TryReadMilliseconds(JsonElement element, string name, out int? value) =>
        TryReadInt(element, name, 1, int.MaxValue, out value);

    private static bool TryReadInt(JsonElement element, string name, int minimum, int maximum, out int? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var parsed))
        {
            return false;
        }

        if (parsed < minimum || parsed > maximum)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryReadBoolean(JsonElement element, string name, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        return property.ValueKind == JsonValueKind.False;
    }

    private static bool TryReadReleaseAction(JsonElement element, out ManualMotionBrakeReleaseAction? value)
    {
        value = null;
        if (!element.TryGetProperty("brakeReleaseAction", out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = property.GetString();
        if (string.Equals(text, "SET_OUTPUT", StringComparison.Ordinal))
        {
            value = ManualMotionBrakeReleaseAction.SetOutput;
            return true;
        }

        if (string.Equals(text, "CLEAR_OUTPUT", StringComparison.Ordinal))
        {
            value = ManualMotionBrakeReleaseAction.ClearOutput;
            return true;
        }

        return false;
    }
}
