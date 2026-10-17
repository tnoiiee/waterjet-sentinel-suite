namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The canonical vocabulary of the B1 one-controller identity session.
///
/// This surface proves — only inside the isolated Owner-invoked harness — that one direct
/// connection can be opened to one Owner-configured controller endpoint, that the three approved
/// identity reads answer with bounded printable responses, and that the handle and the native
/// module handles are released deterministically. It issues NO raw controller command, reads NO
/// axis state, performs NO motion and performs NO discovery.
///
/// Public-repository boundary: this file commits SHAPE ONLY — vocabulary words, the expected
/// model token already committed by the Motion topology catalogue, and redaction rules. The
/// endpoint value and the serial number are Owner-local external values: they never appear in
/// this repository, in a test, fixture, log line, UI state, document or outcome.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public static class MotionControllerIdentityVocabulary
{
    // ------------------------------------------------------------- identity policy (shape)

    /// <summary>The expected logical identity of the first-contact target (Owner decision).</summary>
    public const string ExpectedLogicalIdentity = "MC-WSB1";

    /// <summary>The expected controller model token (Owner-declared; matches the committed topology catalogue).</summary>
    public const string ExpectedControllerModel = "DMC-B140-M";

    // ---------------------------------------------------------------- redacted status words

    /// <summary>The only endpoint statement any output may carry.</summary>
    public const string EndpointConfigured = "ENDPOINT_CONFIGURED";

    /// <summary>Serial observed and withheld; Owner manual confirmation happens at the hardware gate.</summary>
    public const string SerialObservedRedacted = "SERIAL_OBSERVED_REDACTED";

    /// <summary>A redacted fact that still requires Owner manual confirmation.</summary>
    public const string OwnerManualConfirmationRequired = "OWNER_MANUAL_CONFIRMATION_REQUIRED";

    /// <summary>The bounded revision response contains the exact expected model token.</summary>
    public const string ModelMatched = "MODEL_MATCHED";

    /// <summary>Firmware observed; no compatibility claim is made.</summary>
    public const string FirmwareObserved = "FIRMWARE_OBSERVED";

    /// <summary>Success: proof, direct-mode posture, one open, three approved reads, close and unload all passed.</summary>
    public const string IdentitySessionComplete = "IDENTITY_SESSION_COMPLETE";

    // ----------------------------------------------------------------------- refusal vocabulary

    /// <summary>The endpoint variable is absent or blank. Nothing was loaded and nothing was contacted.</summary>
    public const string EndpointNotConfigured = "MOTION_ENDPOINT_NOT_CONFIGURED";

    /// <summary>A present endpoint value was rejected by shape rules. Nothing was loaded and nothing was contacted.</summary>
    public const string EndpointRejected = "MOTION_ENDPOINT_REJECTED";

    /// <summary>The Gate A native load / version proof refused; no connection was attempted.</summary>
    public const string NativeProofRefused = "NATIVE_PROOF_REFUSED";

    /// <summary>The direct/no-gcaps posture could not be enforced before open; no open was attempted after the failure.</summary>
    public const string DirectModeNotEnforced = "DIRECT_MODE_NOT_ENFORCED";

    /// <summary>The injected precondition evaluation detected a concurrent controller authority. Nothing was opened.</summary>
    public const string ConcurrentControllerAuthorityDetected = "CONCURRENT_CONTROLLER_AUTHORITY_DETECTED";

    /// <summary>
    /// A non-open failure on the identity path: an identity read refused, or the single open call
    /// threw before a library result could be classified. A classified open result never uses this
    /// word; it uses the exact open-result code below.
    /// </summary>
    public const string ConnectionFailed = "CONNECTION_FAILED";

    /// <summary>gclib_open reported success but returned a zero controller handle; no identity read was attempted.</summary>
    public const string ConnectionHandleInvalid = "CONNECTION_HANDLE_INVALID";

    /// <summary>The single open attempt timed out (library timeout 2000 ms).</summary>
    public const string ConnectionTimeout = "CONNECTION_TIMEOUT";

    // ------------------------------------------------------- open-result classification

    /// <summary>
    /// The canonical classification of the single gclib_open result, in the installed gclib 2.6.0
    /// header order. Each installed value keeps its own word so the Owner-local diagnosis can tell
    /// an invalid argument from an internal fault, a timeout, a command fault, a buffer fault, a
    /// not-connected state and a not-subscribed state without the raw integer, a vendor error
    /// string or any other native detail. Any other non-zero result is
    /// <see cref="OpenResultUnrecognised"/>.
    /// </summary>
    /// <remarks>gclib_open result GCLIB_INVALID_ARGUMENT (installed header value 1).</remarks>
    public const string OpenInvalidArgument = "OPEN_INVALID_ARGUMENT";

    /// <summary>gclib_open result GCLIB_INTERNAL_ERROR (installed header value 2).</summary>
    public const string OpenInternalError = "OPEN_INTERNAL_ERROR";

    /// <summary>gclib_open result GCLIB_COMMAND_ERROR (installed header value 4).</summary>
    public const string OpenCommandError = "OPEN_COMMAND_ERROR";

    /// <summary>gclib_open result GCLIB_BUFFER_TOO_SMALL (installed header value 5).</summary>
    public const string OpenBufferTooSmall = "OPEN_BUFFER_TOO_SMALL";

    /// <summary>gclib_open result GCLIB_NOT_CONNECTED (installed header value 6).</summary>
    public const string OpenNotConnected = "OPEN_NOT_CONNECTED";

    /// <summary>gclib_open result GCLIB_NOT_SUBSCRIBED (installed header value 7).</summary>
    public const string OpenNotSubscribed = "OPEN_NOT_SUBSCRIBED";

    /// <summary>
    /// A non-zero gclib_open result outside the installed header order. It is neither successful
    /// nor silently folded into a known failure; the value itself is never exposed.
    /// </summary>
    public const string OpenResultUnrecognised = "OPEN_RESULT_UNRECOGNISED";

    /// <summary>The open connection type was not the expected Ethernet type.</summary>
    public const string ConnectionTypeMismatch = "CONNECTION_TYPE_MISMATCH";

    /// <summary>The revision response was not a bounded, terminated, printable string.</summary>
    public const string RevisionResponseInvalid = "REVISION_RESPONSE_INVALID";

    /// <summary>The serial-number read failed or returned zero.</summary>
    public const string SerialResponseInvalid = "SERIAL_RESPONSE_INVALID";

    /// <summary>
    /// The revision response is bounded and printable but does not contain the exact expected
    /// model token. Because the exact installed response format is not proven, the session
    /// neither accepts nor rejects physical identity: Owner confirmation is required.
    /// </summary>
    public const string ModelConfirmationRequired = "MODEL_CONFIRMATION_REQUIRED";

    /// <summary>
    /// Reserved: a proven logical-identity contradiction. Never raised on absence of evidence
    /// (that is MODEL_CONFIRMATION_REQUIRED) and never invented.
    /// </summary>
    public const string IdentityMismatch = "IDENTITY_MISMATCH";

    /// <summary>The total lifecycle budget elapsed before the session completed.</summary>
    public const string LifecycleBudgetExceeded = "LIFECYCLE_BUDGET_EXCEEDED";

    /// <summary>A result arrived for a previous connection generation and was rejected.</summary>
    public const string StaleGenerationResult = "STALE_GENERATION_RESULT";

    /// <summary>A second native operation was requested while one was outstanding; fail-fast refusal.</summary>
    public const string OperationBusy = "OPERATION_BUSY";

    /// <summary>The controller handle close returned a failure result.</summary>
    public const string CloseFailed = "CLOSE_FAILED";

    /// <summary>A native call could not be confirmed cleaned up (close threw with a live handle).</summary>
    public const string CleanupUnproven = "CLEANUP_UNPROVEN";

    /// <summary>A native module handle could not be released deterministically.</summary>
    public const string NativeUnloadFailed = "NATIVE_UNLOAD_FAILED";

    /// <summary>Outcome details must never carry an endpoint, a serial, a native path, a hash or vendor error text.</summary>
    public const string DetailRedactionRequirement =
        "Outcome Detail members must never contain an endpoint, a host, a serial number, a " +
        "directory, a path, a hash, an expected version value or managed exception internals.";
}
