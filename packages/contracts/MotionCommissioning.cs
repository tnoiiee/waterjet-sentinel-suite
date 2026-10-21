namespace Wjss.Contracts;

/// <summary>
/// Canonical vocabulary of the Application-owned manual motion commissioning foundation.
///
/// The foundation is the software surface that will later drive the four motion controllers that
/// move the eight Water Jets. In this revision it carries NO device access of any kind: no
/// controller endpoint is configured, no vendor library is installed, no controller is connected,
/// no axis is homed, no software limit is commissioned, no brake state is known and no motion
/// command is authorized. Every field therefore publishes an explicit not-configured,
/// not-commissioned or unknown value, and the authorization gate is CLOSED.
///
/// The four controllers are addressed ONLY as Owner-confirmed logical identities (the WSB cabinet
/// that holds each controller). No network address, no endpoint value and no vendor-specific
/// command string appears in any committed contract, projection, log or UI state: the only
/// endpoint statement any layer may carry is the redacted
/// <see cref="EndpointNotConfigured"/> identity.
///
/// Owner-confirmed facts encoded here (they are DATA, never a parity-derived assumption):
/// <list type="bullet">
///   <item>Axis A and C are Horizontal; axis B and D are Vertical. The Operator wording is
///   "Horizontal Axis" / "Vertical Axis"; nothing else is ever displayed.</item>
///   <item>Brake output allocation: axis A to output 1, B to output 2, C to output 3, D to output 4.</item>
///   <item>Water Jet axis allocation under the current canonical topology
///   (<see cref="MotionTopologyRevision"/>, OWNER_CONFIRMED_REV2, adopted by the Owner
///   Canonical Topology Adoption of 2026-10-21): WJ1/WSB2 = A,B; WJ2/WSB1 = A,B;
///   WJ3/WSB1 = C,D; WJ4/WSB2 = C,D; WJ5/WSB3 = A,B; WJ6/WSB4 = A,B; WJ7/WSB3 = C,D;
///   WJ8/WSB4 = C,D. This supersedes, for current allocation and publication only, the
///   superseded OWNER_CONFIRMED_REV1 allocation (WJ1/WSB1 = A,B; WJ2/WSB1 = C,D;
///   WJ3/WSB2 = C,D; WJ4/WSB2 = A,B; WJ5/WSB3 = C,D; WJ6/WSB3 = A,B; WJ7/WSB4 = A,B;
///   WJ8/WSB4 = C,D), which remains the historical identity of the earlier revision and
///   is never rewritten as if REV2 had always existed.</item>
///   <item>Each WSB cabinet holds one four-axis controller, so the controller identity is the WSB
///   cabinet identity. That one-controller-per-cabinet reading is INFERRED from the confirmed
///   Water Jet allocation above, not separately confirmed by the Owner.</item>
///   <item>Under REV2, WJ2 = A,B on WSB1 and WJ4 = C,D on WSB2, both CANONICAL and
///   <see cref="MappingConfirmedOwner"/>. The Drawing rev1 discrepancy recorded against
///   the superseded REV1 allocation for the same two Water Jets remains evidence
///   metadata only (<see cref="DrawingEvidenceRecorded"/>,
///   <see cref="EvidenceClassificationEvidenceOnly"/>); it is not canonical ambiguity, it does not
///   override the confirmed mapping, and it is <see cref="PhysicalResolutionNotResolved"/>:
///   the REV2 adoption does not assert which allocation the installed wiring matches.
///   The mapping is explicit data: no parity rule, no odd/even formula and no cabinet-derived
///   formula derives any row.</item>
/// </list>
///
/// The initial Jog Speed is carried as a DISPLAY value in controller units with status
/// <see cref="ParameterStatusUnconfirmed"/>. It is never an executable hardware parameter and is
/// never converted to millimetres, millimetres per second or any engineering unit: no conversion
/// is verified, so no conversion is published.
/// </summary>
public static class MotionVocabulary
{
    /// <summary>Identity of the motion commissioning Snapshot section.</summary>
    public const string SectionId = "MOTION_COMMISSIONING_FOUNDATION";

    /// <summary>Revision of this software foundation surface.</summary>
    public const int SectionRevision = 1;

    /// <summary>Provenance of the projection: composed by the Application model, never by a device read.</summary>
    public const string Source = "APPLICATION_OWNED_MOTION_COMMISSIONING_MODEL";

    /// <summary>
    /// Revision of the Owner-confirmed axis mapping carried by this section: the current
    /// canonical topology (Canonical Topology Adoption, 2026-10-21). Current validators,
    /// publication and projection require exactly this identity.
    /// </summary>
    public const string MotionTopologyRevision = "OWNER_CONFIRMED_REV2";

    /// <summary>
    /// HISTORICAL ONLY — the superseded motion topology revision. REV1 remains the
    /// historical identity of the earlier WJ / WSB / axis allocation and of the
    /// historical evidence recorded under it; it is superseded only for current
    /// allocation, publication and projection. No current Product path may publish or
    /// require this value, and historical evidence is never rewritten as if REV2 had
    /// always existed.
    /// </summary>
    public const string HistoricalMotionTopologyRevisionRev1 = "OWNER_CONFIRMED_REV1";

    // ------------------------------------------------------------------ axis identity and role

    public const string AxisA = "A";
    public const string AxisB = "B";
    public const string AxisC = "C";
    public const string AxisD = "D";

    public const string AxisRoleHorizontal = "HORIZONTAL";
    public const string AxisRoleVertical = "VERTICAL";

    /// <summary>The only two axis wordings the Operator may ever see.</summary>
    public const string HorizontalAxisWording = "Horizontal Axis";

    /// <summary>The only two axis wordings the Operator may ever see.</summary>
    public const string VerticalAxisWording = "Vertical Axis";

    // ------------------------------------------------------------------------------ motion sense

    /// <summary>The axis carries an encoder: the position sense is a real feedback device.</summary>
    public const string SenseEncoder = "ENCODER";

    /// <summary>A reverse / home / forward switch set is installed on the axis.</summary>
    public const string SenseInstalled = "INSTALLED";

    /// <summary>The position sense of this axis is not confirmed by any accepted evidence.</summary>
    public const string SenseUnconfirmed = "UNCONFIRMED";

    // ---------------------------------------------------------------------------- limit signals

    public const string SignalReverse = "REVERSE";
    public const string SignalHome = "HOME";
    public const string SignalForward = "FORWARD";

    public const string PolarityActiveHigh = "ACTIVE_HIGH";
    public const string PolarityActiveLow = "ACTIVE_LOW";

    /// <summary>No accepted evidence establishes the polarity of this input.</summary>
    public const string PolarityUnconfirmed = "UNCONFIRMED";

    /// <summary>The installed switches do not share one confirmed polarity across the axes.</summary>
    public const string PolarityMixedUnresolved = "MIXED_UNRESOLVED";

    /// <summary>The signal is not being read, so its electrical state is not known.</summary>
    public const string SignalStateUnknown = "UNKNOWN";

    // ----------------------------------------------------------------------------- configuration

    /// <summary>The only motion-port statement permitted while no Owner-local endpoint is configured.</summary>
    public const string MotionPortNotConfigured = "MOTION_PORT_NOT_CONFIGURED";

    public const string MotionPortStatusNotConfigured = "NOT_CONFIGURED";

    /// <summary>The only endpoint statement any committed layer may carry while nothing is configured.</summary>
    public const string EndpointNotConfigured = "ENDPOINT_NOT_CONFIGURED";

    public const string ControllerConfigurationNotConfigured = "NOT_CONFIGURED";

    /// <summary>The controller communication library is not installed in this software revision.</summary>
    public const string GclibStatusNotInstalled = "NOT_INSTALLED";

    public const string CommunicationNotConnected = "NOT_CONNECTED";

    // ----------------------------------------------------------------------------- per-axis state

    public const string CommissioningNotCommissioned = "NOT_COMMISSIONED";
    public const string HomingNotHomed = "NOT_HOMED";

    /// <summary>No axis motion state is claimed: nothing is read, so nothing is known.</summary>
    public const string MotionStateUnknown = "UNKNOWN";

    /// <summary>No brake command has ever been issued from this software.</summary>
    public const string BrakeCommandNone = "NONE";

    /// <summary>The brake state is not read and not inferred.</summary>
    public const string BrakeStateUnknown = "UNKNOWN";

    public const string VerificationNotVerified = "NOT_VERIFIED";
    public const string StatusUnresolved = "UNRESOLVED";
    public const string StatusNotCommissioned = "NOT_COMMISSIONED";
    public const string StatusNotConfigured = "NOT_CONFIGURED";

    /// <summary>The value cannot exist because the source that would produce it is unavailable.</summary>
    public const string StatusNotAvailable = "NOT_AVAILABLE";

    /// <summary>No library, mechanism or interface has been selected yet.</summary>
    public const string StatusNotSelected = "NOT_SELECTED";

    /// <summary>The component is not installed in this software revision.</summary>
    public const string StatusNotInstalled = "NOT_INSTALLED";

    /// <summary>The read or write was deliberately not attempted.</summary>
    public const string StatusNotAttempted = "NOT_ATTEMPTED";

    /// <summary>The value can only come from the Owner.</summary>
    public const string StatusOwnerInputPending = "OWNER_INPUT_PENDING";

    /// <summary>The interface that would supply the value is deliberately deferred to a later gate.</summary>
    public const string StatusAccessDeferred = "ACCESS_DEFERRED";

    /// <summary>The value exists in the Legacy system but the Legacy source is not available, so it is not carried over.</summary>
    public const string StatusBlockedNoLegacyEvidence = "BLOCKED_NO_LEGACY_EVIDENCE";

    // ----------------------------------------------------------------------------------- mapping

    /// <summary>The canonical Product mapping status of every Water Jet, WJ2 and WJ4 included.</summary>
    public const string MappingConfirmedOwner = "CONFIRMED_OWNER";

    /// <summary>
    /// The Drawing rev1 discrepancy for WJ2 / WJ4, recorded as EVIDENCE ONLY. It is not a canonical
    /// ambiguity: the Owner-confirmed mapping governs the Product.
    /// </summary>
    public const string DrawingEvidenceRecorded = "DRAWING_REV1_CONFLICT_RECORDED";

    /// <summary>The classification of a recorded Drawing discrepancy: evidence metadata, never canonical.</summary>
    public const string EvidenceClassificationEvidenceOnly = "EVIDENCE_ONLY_NOT_CANONICAL";

    /// <summary>The physical discrepancy has not been resolved on the installation: recorded, not closed.</summary>
    public const string PhysicalResolutionNotResolved = "NOT_PHYSICALLY_RESOLVED";

    public const string BasisOwnerConfirmed = "OWNER_CONFIRMED";

    /// <summary>The value follows from Owner-confirmed data but was not itself confirmed.</summary>
    public const string BasisInferred = "INFERRED_FROM_OWNER_CONFIRMED_MAPPING";

    public const string BasisDrawingRev1 = "DRAWING_REV1";
    public const string BasisUnresolved = "UNRESOLVED";

    // -------------------------------------------------------------------------------- parameters

    /// <summary>The only unit any motion parameter carries until a conversion is verified.</summary>
    public const string SpeedUnitControllerUnits = "controller units";

    /// <summary>Carried from the Legacy system as a display value; no evidence verifies it.</summary>
    public const string ParameterStatusUnconfirmed = "LEGACY_CANDIDATE_UNVERIFIED";

    /// <summary>The initial Jog Speed, as a display value only.</summary>
    public const string JogSpeedDisplayValue = "10";

    // --------------------------------------------------------------------------- gate and refusal

    public const string GateClosed = "CLOSED";

    /// <summary>The standing reason the gate is closed: the separate execution gate is not open.</summary>
    public const string RefusalMotionCommissioningGateNotOpen = "MOTION_COMMISSIONING_GATE_NOT_OPEN";

    public const string RefusalGclibNotInstalled = "GCLIB_NOT_INSTALLED";
    public const string RefusalControllerEndpointNotConfigured = "CONTROLLER_ENDPOINT_NOT_CONFIGURED";
    public const string RefusalControllerNotConnected = "CONTROLLER_NOT_CONNECTED";
    public const string RefusalMotionParametersNotConfigured = "MOTION_PARAMETERS_NOT_CONFIGURED";
    public const string RefusalSoftwareLimitsNotCommissioned = "SOFTWARE_LIMITS_NOT_COMMISSIONED";
    public const string RefusalAxisNotHomed = "AXIS_NOT_HOMED";
    public const string RefusalLimitStateUnknown = "LIMIT_STATE_UNKNOWN";
    public const string RefusalBrakeStateUnknown = "BRAKE_STATE_UNKNOWN";
    public const string RefusalHardwareCommandsNotAuthorized = "HARDWARE_COMMANDS_NOT_AUTHORIZED";

    // ----------------------------------------------------------------------------- command intent

    public const string IntentJog = "JOG";
    public const string IntentMove = "MOVE";
    public const string IntentHome = "HOME";
    public const string IntentStop = "STOP";
    public const string IntentReleaseBrake = "RELEASE_BRAKE";
    public const string IntentResetEncoder = "RESET_ENCODER";

    public const string CommandStateRefused = "REFUSED";

    /// <summary>No hardware command left this software: the refusal happens before any device path.</summary>
    public const string HardwareCommandStateNotIssued = "NOT_ISSUED";

    public const string DirectionReverse = "REVERSE";
    public const string DirectionForward = "FORWARD";
    public const string DirectionNone = "NONE";

    // ----------------------------------------------------------------------------- identities

    /// <summary>The brake output allocated to axis A (Owner-confirmed).</summary>
    public const string BrakeOutput1 = "OUTPUT_1";

    /// <summary>The brake output allocated to axis B (Owner-confirmed).</summary>
    public const string BrakeOutput2 = "OUTPUT_2";

    /// <summary>The brake output allocated to axis C (Owner-confirmed).</summary>
    public const string BrakeOutput3 = "OUTPUT_3";

    /// <summary>The brake output allocated to axis D (Owner-confirmed).</summary>
    public const string BrakeOutput4 = "OUTPUT_4";

    /// <summary>The controller model status: recorded by the Owner-provided cabinet drawing.</summary>
    public const string ControllerModelStatusDrawingConfirmed = "DRAWING_CONFIRMED";

    /// <summary>The one recorded Drawing discrepancy of this revision: the WJ2 / WJ4 axis allocation.</summary>
    public const string EvidenceNoteIdWj2Wj4 = "MOTION-EVIDENCE-01";

    /// <summary>
    /// Diagnostic wording of the physical controller axis letter. The Operator-facing axis identity is
    /// the role ("Horizontal Axis" / "Vertical Axis"); the physical letter is diagnostic identity only.
    /// </summary>
    public const string PhysicalAxisWording = "Physical axis";

    /// <summary>No position value exists because no position is read.</summary>
    public const string PositionValueNotRead = "NOT_READ";

    /// <summary>The only way a counts-to-millimetre relationship may enter the product: measured, never inferred.</summary>
    public const string ProvenanceMeasuredDuringCommissioning = "MEASURED_DURING_COMMISSIONING";

    /// <summary>Who raises a motion intent: the Application-owned page, never the browser directly.</summary>
    public const string RequestedByApplication = "APPLICATION_OWNED_MOTION_COMMISSIONING_PAGE";

    /// <summary>No separate Owner authorization exists for this revision.</summary>
    public const string OwnerAuthorizationNone = "NONE";

    // ---------------------------------------------------------------------------- session and log

    /// <summary>No commissioning session exists: the gate is closed, so none can be opened.</summary>
    public const string SessionStateNoSession = "NO_SESSION";

    /// <summary>Command history is session scoped and is never persisted.</summary>
    public const string JournalPersistenceNone = "NONE";

    public const string CommandTotalsStateNoCommands = "NO_COMMANDS";

    /// <summary>The six motion command intents the foundation knows. No other intent exists.</summary>
    public static IReadOnlyList<string> MotionIntents { get; } = Array.AsReadOnly(new[]
    {
        IntentJog, IntentMove, IntentHome, IntentStop, IntentReleaseBrake, IntentResetEncoder,
    });

    /// <summary>The reason the authorization gate is closed. One reason, stated once.</summary>
    public static IReadOnlyList<string> GateRefusalReasons { get; } = Array.AsReadOnly(new[]
    {
        RefusalMotionCommissioningGateNotOpen,
    });

    /// <summary>
    /// The standing preconditions that also refuse every motion intent today. They are preconditions
    /// of execution, not alternatives to the gate: the gate closes first and nothing reaches these
    /// while the gate is closed.
    /// </summary>
    public static IReadOnlyList<string> StandingRefusalReasons { get; } = Array.AsReadOnly(new[]
    {
        RefusalGclibNotInstalled,
        RefusalControllerEndpointNotConfigured,
        RefusalControllerNotConnected,
        RefusalMotionParametersNotConfigured,
        RefusalSoftwareLimitsNotCommissioned,
        RefusalAxisNotHomed,
        RefusalLimitStateUnknown,
        RefusalBrakeStateUnknown,
        RefusalHardwareCommandsNotAuthorized,
    });

    /// <summary>The complete refusal vocabulary, gate reason first, in the evaluation order.</summary>
    public static IReadOnlyList<string> AllRefusalReasons { get; } = Array.AsReadOnly(new[]
    {
        RefusalMotionCommissioningGateNotOpen,
        RefusalGclibNotInstalled,
        RefusalControllerEndpointNotConfigured,
        RefusalControllerNotConnected,
        RefusalMotionParametersNotConfigured,
        RefusalSoftwareLimitsNotCommissioned,
        RefusalAxisNotHomed,
        RefusalLimitStateUnknown,
        RefusalBrakeStateUnknown,
        RefusalHardwareCommandsNotAuthorized,
    });

    /// <summary>
    /// The claims this foundation deliberately does NOT make. Read-only, published with the state,
    /// so no surface can imply a measurement, a connection, a conversion or an authorization.
    /// </summary>
    public static IReadOnlyList<string> NotVerifiedClaims { get; } = Array.AsReadOnly(new[]
    {
        "NO_CONTROLLER_CONTACTED",
        "NO_ENDPOINT_CONFIGURED",
        "NO_LIBRARY_INSTALLED",
        "NO_AXIS_HOMED",
        "NO_SOFTWARE_LIMIT_COMMISSIONED",
        "NO_LIMIT_INPUT_POLARITY_CONFIRMED",
        "NO_BRAKE_STATE_READ",
        "NO_MOTION_COMMAND_ISSUED",
        "NO_UNIT_CONVERSION_VERIFIED",
        "NO_LEGACY_VALUE_PROMOTED_TO_CONFIGURATION",
        "NO_HARDWARE_VERIFICATION",
        "NO_FIELD_VERIFICATION",
    });

    /// <summary>
    /// Operator wording for every status this section publishes. Every status is written out as
    /// text wherever it is displayed: no state is ever carried by colour alone.
    ///
    /// One entry per distinct status VALUE. Several named constants deliberately share a value
    /// (for example the motion state, the brake state and a limit signal state are all UNKNOWN);
    /// the dictionary is keyed by value, so each of those is worded exactly once.
    /// </summary>
    public static IReadOnlyDictionary<string, string> StatusText { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AxisRoleHorizontal] = "Horizontal",
            [AxisRoleVertical] = "Vertical",
            [SenseEncoder] = "Encoder",
            [SenseInstalled] = "Installed",
            [SenseUnconfirmed] = "Unconfirmed",
            [SignalReverse] = "Reverse",
            [SignalHome] = "Home",
            [SignalForward] = "Forward",
            [PolarityActiveHigh] = "Active high",
            [PolarityActiveLow] = "Active low",
            [PolarityMixedUnresolved] = "Mixed, unresolved",
            [SignalStateUnknown] = "Unknown",
            [MotionPortStatusNotConfigured] = "Not configured",
            [EndpointNotConfigured] = "Not configured",
            [GclibStatusNotInstalled] = "Not installed",
            [CommunicationNotConnected] = "Not connected",
            [CommissioningNotCommissioned] = "Not commissioned",
            [HomingNotHomed] = "Not homed",
            [BrakeCommandNone] = "None",
            [VerificationNotVerified] = "Not verified",
            [StatusUnresolved] = "Unresolved",
            [StatusNotAvailable] = "Not available",
            [StatusNotSelected] = "Not selected",
            [StatusNotAttempted] = "Not attempted",
            [StatusOwnerInputPending] = "Owner input pending",
            [StatusAccessDeferred] = "Access deferred",
            [StatusBlockedNoLegacyEvidence] = "Blocked: no Legacy evidence",
            [MappingConfirmedOwner] = "Confirmed by the Owner",
            [DrawingEvidenceRecorded] = "Drawing rev1 discrepancy recorded (evidence only)",
            [EvidenceClassificationEvidenceOnly] = "Evidence only, not canonical",
            [PhysicalResolutionNotResolved] = "Not physically resolved",
            [BasisOwnerConfirmed] = "Owner confirmed",
            [BasisInferred] = "Inferred from confirmed mapping",
            [BasisDrawingRev1] = "Drawing rev1",
            [ParameterStatusUnconfirmed] = "Legacy candidate, unverified",
            [GateClosed] = "Closed",
            [RefusalMotionCommissioningGateNotOpen] = "The separate motion commissioning execution gate is not open",
            [RefusalGclibNotInstalled] = "The controller communication library is not installed",
            [RefusalControllerEndpointNotConfigured] = "No controller endpoint is configured",
            [RefusalControllerNotConnected] = "The controller is not connected",
            [RefusalMotionParametersNotConfigured] = "Motion parameters are not configured",
            [RefusalSoftwareLimitsNotCommissioned] = "Software limits are not commissioned",
            [RefusalAxisNotHomed] = "The axis is not homed",
            [RefusalLimitStateUnknown] = "The limit input state is unknown",
            [RefusalBrakeStateUnknown] = "The brake state is unknown",
            [RefusalHardwareCommandsNotAuthorized] = "Hardware commands are not authorized",
            [IntentJog] = "Jog",
            [IntentMove] = "Move",
            [IntentStop] = "Stop",
            [IntentReleaseBrake] = "Release brake",
            [IntentResetEncoder] = "Reset encoder",
            [CommandStateRefused] = "Refused",
            [HardwareCommandStateNotIssued] = "Not issued",
            [SessionStateNoSession] = "No session",
            [CommandTotalsStateNoCommands] = "No commands",
        };

    /// <summary>
    /// Operator wording for one status. An unknown status returns its own code: a missing wording
    /// must never silently become an empty string on an Operator screen.
    /// </summary>
    public static string TextFor(string status) =>
        StatusText.TryGetValue(status, out var text) ? text : status;

    /// <summary>
    /// The codes the status legend publishes, in a fixed order: the refusal vocabulary first, then
    /// the states, the mapping bases and the intents. The order is explicit because dictionary
    /// enumeration order is not contractual, and the legend is a published surface.
    /// </summary>
    public static IReadOnlyList<string> StatusLegendOrder { get; } = AllRefusalReasons.Concat(new[]
    {
        GateClosed,
        SessionStateNoSession,
        CommandTotalsStateNoCommands,
        CommandStateRefused,
        HardwareCommandStateNotIssued,
        JournalPersistenceNone,
        MotionPortStatusNotConfigured,
        EndpointNotConfigured,
        GclibStatusNotInstalled,
        CommunicationNotConnected,
        CommissioningNotCommissioned,
        HomingNotHomed,
        SignalStateUnknown,
        BrakeCommandNone,
        VerificationNotVerified,
        StatusUnresolved,
        StatusNotAvailable,
        StatusAccessDeferred,
        StatusOwnerInputPending,
        StatusBlockedNoLegacyEvidence,
        PolarityUnconfirmed,
        PolarityMixedUnresolved,
        SignalReverse,
        SignalHome,
        SignalForward,
        SenseEncoder,
        SenseInstalled,
        SenseUnconfirmed,
        AxisRoleHorizontal,
        AxisRoleVertical,
        MappingConfirmedOwner,
        DrawingEvidenceRecorded,
        EvidenceClassificationEvidenceOnly,
        PhysicalResolutionNotResolved,
        BasisOwnerConfirmed,
        BasisInferred,
        BasisDrawingRev1,
        ParameterStatusUnconfirmed,
        PositionValueNotRead,
        ProvenanceMeasuredDuringCommissioning,
        IntentJog,
        IntentMove,
        IntentStop,
        IntentReleaseBrake,
        IntentResetEncoder,
    }).ToArray();
}

/// <summary>
/// The Owner-confirmed identity of one of the four motion controllers.
///
/// The identity is logical only. It never carries an address: the controller is named by the WSB
/// cabinet that holds it, and the one-controller-per-cabinet reading is recorded as
/// <see cref="MotionVocabulary.BasisInferred"/> rather than as a confirmed fact.
/// </summary>
public sealed record MotionControllerIdentity
{
    /// <summary>Logical controller identity, derived from the WSB cabinet that holds it.</summary>
    public required string ControllerId { get; init; }

    /// <summary>The WSB cabinet that holds this controller.</summary>
    public required string CabinetId { get; init; }

    /// <summary>The number of axes this controller presents.</summary>
    public required int AxisCount { get; init; }

    /// <summary>The controller model the Owner-provided cabinet drawing records.</summary>
    public required string ControllerModel { get; init; }

    /// <summary>How <see cref="ControllerModel"/> is known: the Owner-provided cabinet drawing.</summary>
    public required string ControllerModelStatus { get; init; }

    /// <summary>How <see cref="ControllerId"/> is known: inferred from the confirmed axis allocation.</summary>
    public required string ControllerIdentityBasis { get; init; }
}

/// <summary>
/// The configuration state of one motion controller. Every member is an explicit
/// not-configured / not-connected / not-commissioned value: no member can express a ready
/// controller, and no member carries an endpoint value.
/// </summary>
public sealed record MotionControllerConfigurationState
{
    /// <summary>The only motion-port statement permitted while nothing is configured.</summary>
    public required string MotionPortIdentity { get; init; }

    public required string MotionPortStatus { get; init; }

    /// <summary>Redacted logical endpoint identity. The real endpoint never appears here.</summary>
    public required string EndpointIdentity { get; init; }

    public required string ControllerConfigurationStatus { get; init; }

    public required string LibraryInstallationStatus { get; init; }

    public required string CommunicationState { get; init; }

    /// <summary>False by construction: no controller can be ready while the gate is closed.</summary>
    public required bool MotionDispatchReady { get; init; }

    public required string ParameterConfigurationState { get; init; }

    public required string SoftwareLimitConfigurationState { get; init; }

    public required string LimitInputPolarityStatus { get; init; }

    public required string EndstopConfigurationStatus { get; init; }

    /// <summary>The reason this controller cannot dispatch motion. Always the closed gate reason.</summary>
    public required string MotionDispatchBlockedReason { get; init; }
}

/// <summary>
/// The Owner-confirmed identity of one controller axis: its role, its position sense and the brake
/// output allocated to it. The role is the only axis meaning the Operator ever sees.
/// </summary>
public sealed record MotionAxisIdentity
{
    public required string ControllerId { get; init; }

    /// <summary>One of A, B, C or D as labelled on the controller.</summary>
    public required string AxisId { get; init; }

    /// <summary>HORIZONTAL or VERTICAL (Owner-confirmed: A and C are Horizontal, B and D are Vertical).</summary>
    public required string AxisRole { get; init; }

    public required string AxisRoleBasis { get; init; }

    /// <summary>How position is sensed on this axis: ENCODER, INSTALLED or UNCONFIRMED.</summary>
    public required string MotionSenseKind { get; init; }

    public required string MotionSenseBasis { get; init; }

    /// <summary>The brake output allocated to this axis, as a logical identity (never a hardware write).</summary>
    public required string BrakeOutputIdentity { get; init; }

    public required string BrakeOutputMappingBasis { get; init; }
}

/// <summary>
/// The state of one installed limit input of one axis: its role (Reverse, Home or Forward), the
/// kind of device behind it, the polarity status and the signal state.
///
/// The polarity of these inputs is deliberately UNCONFIRMED. The switches are installed and their
/// behaviour on the existing hardware is real, but the exact electrical meaning is only established
/// by inspecting the Legacy implementation and the Drawing against the physical installation, which
/// has not happened. An unconfirmed polarity is therefore never treated as active high or active low.
/// </summary>
public sealed record MotionLimitSignalState
{
    public required string SignalRole { get; init; }

    /// <summary>The kind of device behind the input: ENCODER or INSTALLED.</summary>
    public required string SignalKind { get; init; }

    /// <summary>The polarity status: UNCONFIRMED until the Legacy implementation and the Drawing are read.</summary>
    public required string PolarityStatus { get; init; }

    /// <summary>Always UNKNOWN: no input is read in this revision.</summary>
    public required string SignalState { get; init; }

    public required string SignalStateStatus { get; init; }

    public required string SenseBasis { get; init; }
}

/// <summary>
/// The motion sense of one axis: what reads position and whether that reading is available.
/// </summary>
public sealed record MotionAxisSenseState
{
    public required string MotionSenseKind { get; init; }

    public required string MotionSenseBasis { get; init; }

    /// <summary>NOT_AVAILABLE: no controller is connected, so no channel is read.</summary>
    public required string SenseChannelStatus { get; init; }

    /// <summary>NOT_READ: no position, encoder or limit value is available.</summary>
    public required string PositionValueStatus { get; init; }

    /// <summary>Always null: a position is never invented.</summary>
    public string? PositionDisplayValue { get; init; }

    /// <summary>The only unit a position could carry. No conversion is verified, so none is published.</summary>
    public string? PositionUnit { get; init; }
}

/// <summary>
/// The motion-local protection state of one axis. These are motion-local protections, NOT the
/// Cleaning-process interlocks: they protect the axis and the commissioning operator, and they are
/// reported separately from any process interlock.
/// </summary>
public sealed record MotionAxisSafetyState
{
    /// <summary>NOT_AVAILABLE: no controller is connected, so no stop input is visible here.</summary>
    public required string EmergencyStopVisibilityStatus { get; init; }

    /// <summary>NOT_VERIFIED: the emergency stop circuit has not been verified in this software revision.</summary>
    public required string StopCircuitVerificationStatus { get; init; }

    /// <summary>UNRESOLVED: the controlled-stop behaviour on communication loss is unresolved.</summary>
    public required string ControlledStopCapabilityStatus { get; init; }

    public required string ControlledStopVerificationStatus { get; init; }

    /// <summary>NOT_VERIFIED: pump, valve and process isolation are not verified and are out of scope here.</summary>
    public required string IsolationVerificationStatus { get; init; }

    /// <summary>NOT_VERIFIED: the bounded speed, direction blocking and stop path are not yet verified.</summary>
    public required string LocalProtectionStatus { get; init; }

    /// <summary>NOT_VERIFIED and never executed: no encoder reset has been performed or authorized.</summary>
    public required string ResetEncoderVerificationStatus { get; init; }
}

/// <summary>
/// The unit-conversion state of one axis. No kinematic conversion is published: counts and
/// controller units stay counts and controller units until a conversion is measured and verified
/// during commissioning.
/// </summary>
public sealed record MotionAxisKinematicState
{
    /// <summary>UNRESOLVED: no counts-to-millimetre relationship is established.</summary>
    public required string KinematicConversionStatus { get; init; }

    /// <summary>True: the conversion, once it exists, is per axis and never shared between axes.</summary>
    public required bool KinematicConversionIsPerAxis { get; init; }

    /// <summary>The unit every motion value of this axis carries today.</summary>
    public required string PublishedUnit { get; init; }

    /// <summary>How the conversion will be established: measured during commissioning, never inferred.</summary>
    public required string ConversionProvenanceRequirement { get; init; }
}

/// <summary>
/// Whether this axis may execute an absolute Move, and the exact reasons it may not.
/// An absolute Move requires a valid Home and commissioned software limits; neither exists.
/// </summary>
public sealed record MotionAxisMoveAuthorization
{
    /// <summary>REFUSED: no Move may leave this software.</summary>
    public required string MoveAuthorizationState { get; init; }

    /// <summary>The complete refusal list, gate reason first then the standing preconditions.</summary>
    public required IReadOnlyList<string> MoveRefusalReasons { get; init; }
}

/// <summary>
/// The published commissioning state of one controller axis. Every member is an explicit
/// not-commissioned, not-homed or unknown value: no member can express a homed axis, a known brake
/// or an available motion state.
/// </summary>
public sealed record MotionAxisCommissioningState
{
    public required string CommissioningState { get; init; }

    /// <summary>NOT_HOMED: no homing sequence has run from this software.</summary>
    public required string HomingState { get; init; }

    /// <summary>UNKNOWN: the homing state is not read from any device, so it is not confirmed.</summary>
    public required string HomingStateStatus { get; init; }

    /// <summary>NONE: no brake command has ever been issued from this software.</summary>
    public required string BrakeCommandState { get; init; }

    /// <summary>UNKNOWN: the brake is neither read nor inferred.</summary>
    public required string BrakeState { get; init; }

    public required string BrakeStateStatus { get; init; }

    /// <summary>UNKNOWN: no motion state is claimed.</summary>
    public required string MotionState { get; init; }

    /// <summary>NOT_AVAILABLE: the motion state cannot be read while nothing is connected.</summary>
    public required string MotionStateStatus { get; init; }

    /// <summary>The three installed limit inputs of this axis, in Reverse, Home, Forward order.</summary>
    public required IReadOnlyList<MotionLimitSignalState> LimitSignals { get; init; }

    public required MotionAxisSenseState Sense { get; init; }

    public required MotionAxisSafetyState Safety { get; init; }

    public required MotionAxisKinematicState Kinematic { get; init; }

    public required MotionAxisMoveAuthorization MoveAuthorization { get; init; }
}

/// <summary>
/// One controller axis as published: its confirmed identity together with its commissioning state.
/// </summary>
public sealed record MotionAxisProjection
{
    public required MotionAxisIdentity Identity { get; init; }

    public required MotionAxisCommissioningState State { get; init; }
}

/// <summary>
/// One motion controller as published: its logical identity together with its configuration state.
/// </summary>
public sealed record MotionControllerProjection
{
    public required MotionControllerIdentity Identity { get; init; }

    public required MotionControllerConfigurationState Configuration { get; init; }
}

/// <summary>
/// The CANONICAL Owner-confirmed axis allocation of one Water Jet: which controller drives it and
/// which physical axis is the Horizontal Axis and which is the Vertical Axis.
///
/// The allocation is EXPLICIT DATA. No parity rule, no odd/even Water Jet formula and no
/// cabinet-derived axis formula is permitted to derive a row: WJ2 = A,B and WJ4 = C,D (REV2) are
/// confirmed values, the same as every other row.
///
/// The physical axis letter is the diagnostic identity of the controller axis. The Operator-facing
/// identity is the role: "Horizontal Axis" / "Vertical Axis". The letter never replaces the role.
/// </summary>
public sealed record MotionWaterJetAxisBinding
{
    public required string WaterJetId { get; init; }

    /// <summary>The WSB cabinet this Water Jet belongs to.</summary>
    public required string CabinetId { get; init; }

    public required string ControllerId { get; init; }

    public required string HorizontalAxisId { get; init; }

    public required string VerticalAxisId { get; init; }

    /// <summary>
    /// CONFIRMED_OWNER for all eight Water Jets. The WJ2 / WJ4 Drawing rev1 discrepancy is recorded
    /// separately as an evidence note; it does not make any row ambiguous.
    /// </summary>
    public required string MappingStatus { get; init; }

    /// <summary>OWNER_CONFIRMED: the mapping provenance of every row is the Owner's confirmation.</summary>
    public required string MappingBasis { get; init; }
}

/// <summary>
/// A recorded Drawing discrepancy, carried as EVIDENCE METADATA ONLY.
///
/// It is not canonical ambiguity and it is not an open decision: the Owner-confirmed mapping is
/// canonical and governs the Product. The note exists so the discrepancy is not silently lost, and
/// it states explicitly that the discrepancy has NOT been physically resolved on the installation.
/// </summary>
public sealed record MotionMappingEvidenceNote
{
    public required string EvidenceId { get; init; }

    /// <summary>The Water Jets the discrepancy concerns.</summary>
    public required IReadOnlyList<string> WaterJetIds { get; init; }

    /// <summary>The WSB cabinets those Water Jets belong to.</summary>
    public required IReadOnlyList<string> CabinetIds { get; init; }

    /// <summary>What the Drawing expectation was, stated as evidence.</summary>
    public required string Statement { get; init; }

    /// <summary>The evidence status: <see cref="MotionVocabulary.DrawingEvidenceRecorded"/>.</summary>
    public required string Status { get; init; }

    /// <summary>The classification: <see cref="MotionVocabulary.EvidenceClassificationEvidenceOnly"/>.</summary>
    public required string Classification { get; init; }

    /// <summary>What the note does and does not change for the Product.</summary>
    public required string Effect { get; init; }

    /// <summary><see cref="MotionVocabulary.PhysicalResolutionNotResolved"/>: recorded, not closed.</summary>
    public required string PhysicalResolution { get; init; }
}

/// <summary>
/// One motion parameter as carried by the software: a display value, its unit, its provenance status
/// and whether it is executable. Nothing here is a hardware parameter: a parameter whose status is
/// not commissioned can never be sent to a controller.
/// </summary>
public sealed record MotionParameterState
{
    public required string ParameterId { get; init; }

    /// <summary>The display value. A string, never a number that could be mistaken for an engineering value.</summary>
    public required string DisplayValue { get; init; }

    /// <summary>The unit of the display value. Controller units until a conversion is verified.</summary>
    public required string Unit { get; init; }

    public required string Status { get; init; }

    public required string ProvenanceBasis { get; init; }

    /// <summary>False: the value is display only and is never sent to a controller.</summary>
    public required bool Executable { get; init; }
}

/// <summary>
/// The command totals of the session. Every count is zero and the state is explicit: no command has
/// been evaluated against hardware, none was authorized, none was issued, and the command record is
/// session scoped and never persisted.
/// </summary>
public sealed record MotionCommandTotals
{
    public required int Evaluated { get; init; }

    public required int Authorized { get; init; }

    public required int Refused { get; init; }

    /// <summary>Zero: no hardware command of any kind has left this software.</summary>
    public required int HardwareCommandsIssued { get; init; }

    public required string CommandState { get; init; }

    /// <summary>NONE: the command record lives in the session only and is never persisted.</summary>
    public required string JournalPersistence { get; init; }
}

/// <summary>
/// The read-only STATIC motion commissioning catalogue: the Owner-confirmed topology, the standing
/// configuration and commissioning statuses, the provenance, the evidence notes, the candidate
/// values and the authorization gate.
///
/// It is composed entirely by the Application model from Owner-confirmed data. No member is read
/// from a device, no member carries an address, and no member can express an authorized motion
/// command. It is static: it is never carried by the Runtime Snapshot or by a Runtime Delta, and it
/// is served by the dedicated read-only catalogue route.
/// </summary>
public sealed record MotionCommissioningCatalogue
{
    public required string SectionId { get; init; }

    public required int SectionRevision { get; init; }

    public required string Source { get; init; }

    public required string MotionTopologyRevision { get; init; }

    /// <summary>CLOSED. Every motion intent is refused before any hardware path is reached.</summary>
    public required string AuthorizationGateState { get; init; }

    public required IReadOnlyList<string> AuthorizationGateReasons { get; init; }

    /// <summary>The six motion intents the foundation knows.</summary>
    public required IReadOnlyList<string> MotionIntents { get; init; }

    /// <summary>Empty: no axis is in a state in which motion is allowed.</summary>
    public required IReadOnlyList<string> AllowedMotionStates { get; init; }

    public required string SessionState { get; init; }

    public required MotionCommandTotals CommandTotals { get; init; }

    /// <summary>The four motion controllers, one per WSB cabinet.</summary>
    public required IReadOnlyList<MotionControllerProjection> Controllers { get; init; }

    /// <summary>The sixteen controller axes (four controllers x four axes).</summary>
    public required IReadOnlyList<MotionAxisProjection> Axes { get; init; }

    /// <summary>The eight Water Jet axis allocations.</summary>
    public required IReadOnlyList<MotionWaterJetAxisBinding> WaterJets { get; init; }

    /// <summary>Recorded Drawing discrepancies. Evidence metadata only; never canonical ambiguity.</summary>
    public required IReadOnlyList<MotionMappingEvidenceNote> MappingEvidenceNotes { get; init; }

    /// <summary>The initial Jog Speed: a display value in controller units, never executable.</summary>
    public required MotionParameterState JogSpeed { get; init; }

    public required IReadOnlyList<string> NotVerifiedClaims { get; init; }

    /// <summary>Every status and refusal code with its wording, so no state is shown by colour alone.</summary>
    public required IReadOnlyList<string> StatusLegend { get; init; }

    /// <summary>The whole projection as one deterministic text block: the parity surface.</summary>
    public required string CanonicalText { get; init; }

    /// <summary>SHA-256 of <see cref="CanonicalText"/>, lowercase hex: the parity fingerprint.</summary>
    public required string Fingerprint { get; init; }
}
