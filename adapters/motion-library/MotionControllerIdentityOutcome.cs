namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The redacted result of one B1 one-controller identity session.
///
/// One bounded outcome. Failures carry one refusal code from
/// <see cref="MotionControllerIdentityVocabulary"/> and a redacted <see cref="Detail"/> that
/// never contains an endpoint, a serial number, a native path, a hash or vendor error text. The
/// serial number is never present in any form; its status words are
/// <see cref="MotionControllerIdentityVocabulary.SerialObservedRedacted"/> and
/// <see cref="MotionControllerIdentityVocabulary.OwnerManualConfirmationRequired"/>. The
/// revision-information response is the observed identity evidence: it is included only after
/// being proven a bounded, terminated, printable string, and it carries no compatibility claim.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed record MotionControllerIdentityOutcome
{
    /// <summary>True only when the complete one-shot session passed every step.</summary>
    public required bool Accepted { get; init; }

    /// <summary>The success code or the single refusal code, in the canonical vocabulary.</summary>
    public required string Code { get; init; }

    /// <summary>Redacted narrative of the outcome. Never an endpoint, a serial, a path, a hash or exception internals.</summary>
    public required string Detail { get; init; }

    /// <summary>The expected logical identity this session was prepared for (MC-WSB1).</summary>
    public required string ExpectedLogicalIdentity { get; init; }

    /// <summary>Redacted logical-identity status; physical identity is confirmed only by the Owner.</summary>
    public required string LogicalIdentityStatus { get; init; }

    /// <summary>
    /// The observed bounded revision-information response (observe only). Null on any path that
    /// did not produce a validated response.
    /// </summary>
    public string? RevisionInformation { get; init; }

    /// <summary>Model comparison result: MODEL_MATCHED or MODEL_CONFIRMATION_REQUIRED.</summary>
    public string? ModelComparison { get; init; }

    /// <summary>Redacted serial status: SERIAL_OBSERVED_REDACTED. The serial value is never included.</summary>
    public string? SerialStatus { get; init; }

    /// <summary>Connection-type status label (for example GCLIB_ETHERNET) or null before a successful read.</summary>
    public string? ConnectionTypeStatus { get; init; }

    /// <summary>Firmware status: FIRMWARE_OBSERVED. No compatibility claim.</summary>
    public string? FirmwareStatus { get; init; }

    /// <summary>The connection generation the session ran on (one per session).</summary>
    public required long Generation { get; init; }

    /// <summary>True when the controller handle was closed and confirmed zeroed.</summary>
    public required bool ControllerHandleClosed { get; init; }

    /// <summary>True when every native module handle the session opened was released deterministically.</summary>
    public required bool NativeHandlesReleased { get; init; }

    /// <summary>
    /// Acceptance: proof complete, direct mode enforced, one open, three approved reads valid,
    /// model matched, handle closed and confirmed, module handles released.
    /// </summary>
    public static MotionControllerIdentityOutcome Complete(
        string revisionInformation,
        string connectionTypeStatus,
        string serialStatus,
        long generation,
        string detail) => new()
    {
        Accepted = true,
        Code = MotionControllerIdentityVocabulary.IdentitySessionComplete,
        Detail = detail,
        ExpectedLogicalIdentity = MotionControllerIdentityVocabulary.ExpectedLogicalIdentity,
        LogicalIdentityStatus = MotionControllerIdentityVocabulary.OwnerManualConfirmationRequired,
        RevisionInformation = revisionInformation,
        ModelComparison = MotionControllerIdentityVocabulary.ModelMatched,
        SerialStatus = serialStatus,
        ConnectionTypeStatus = connectionTypeStatus,
        FirmwareStatus = MotionControllerIdentityVocabulary.FirmwareObserved,
        Generation = generation,
        ControllerHandleClosed = true,
        NativeHandlesReleased = true,
    };

    /// <summary>Refusal: fail closed with the refusal code and a redacted detail.</summary>
    public static MotionControllerIdentityOutcome Refused(
        string code,
        string detail,
        long generation,
        bool controllerHandleClosed,
        bool nativeHandlesReleased,
        string? revisionInformation = null,
        string? modelComparison = null,
        string? serialStatus = null,
        string? connectionTypeStatus = null) => new()
    {
        Accepted = false,
        Code = code,
        Detail = detail,
        ExpectedLogicalIdentity = MotionControllerIdentityVocabulary.ExpectedLogicalIdentity,
        LogicalIdentityStatus = MotionControllerIdentityVocabulary.OwnerManualConfirmationRequired,
        RevisionInformation = revisionInformation,
        ModelComparison = modelComparison,
        SerialStatus = serialStatus,
        ConnectionTypeStatus = connectionTypeStatus,
        FirmwareStatus = revisionInformation is null ? null : MotionControllerIdentityVocabulary.FirmwareObserved,
        Generation = generation,
        ControllerHandleClosed = controllerHandleClosed,
        NativeHandlesReleased = nativeHandlesReleased,
    };
}
