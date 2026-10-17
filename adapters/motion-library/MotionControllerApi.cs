using System.Collections.Generic;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The canonical name and shape vocabulary of the B1 one-controller identity surface.
///
/// Vendor-neutral in every committed word except the exact native export names, which are
/// required so the boundary can resolve exactly those entries and nothing else. The export
/// names and result codes below are taken verbatim from the exact gclib 2.6.0 header and API
/// documentation (the same installed-header evidence class used by the Native Load / Version
/// Probe Software Gate for the version export):
///
///     void         gclib_force_gcaps(bool on);
///     gclib_result gclib_open(gclib_handle *h, const char *address);
///     gclib_result gclib_close(gclib_handle *h);
///     gclib_result gclib_connection_type(gclib_handle h, gclib_connection_t *type);
///     gclib_result gclib_revision_information(gclib_handle h, char *rev_info, size_t len);
///     gclib_result gclib_serial_number(gclib_handle h, uint32_t *serial_number);
///
///     typedef enum gclib_result {
///         GCLIB_SUCCESS, GCLIB_INVALID_ARGUMENT, GCLIB_INTERNAL_ERROR, GCLIB_TIMEOUT,
///         GCLIB_COMMAND_ERROR, GCLIB_BUFFER_TOO_SMALL, GCLIB_NOT_CONNECTED,
///         GCLIB_NOT_SUBSCRIBED
///     } gclib_result;
///
///     typedef enum gclib_connection_t {
///         GCLIB_ETHERNET = 1, GCLIB_SERIAL, GCLIB_PCI
///     } gclib_connection_t;
///
/// Connection contract, from the gclib 2.6.0 manual (Google Drive, gclib.pdf):
/// the gclib_open() entry (printed page 48) takes the controller ADDRESS — the address token the
/// library reports for a detected controller, never an option string. The "--direct"/
/// "--timeout" switches are documented for the deprecated legacy GOpen() parameter (printed
/// page 76), not for gclib_open(). Direct/no-gcaps operation is selected process-wide by
/// gclib_force_gcaps(false) (printed page 48, same section as gclib_open). The B1 session
/// therefore builds the open value in memory from the validated endpoint only and never persists
/// it.
///
/// Nothing here is a raw controller command: B1 issues no command string of any kind.
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public static class MotionControllerApi
{
    // ------------------------------------------------------------------- authorized exports

    /// <summary>Direct-mode enforcement. Documented as a process-wide, irreversible selection of direct operations with no gcaps.</summary>
    public const string ForceDirectModeExportName = "gclib_force_gcaps";

    /// <summary>Opens exactly one controller handle for the session.</summary>
    public const string OpenExportName = "gclib_open";

    /// <summary>Closes the controller handle; the handle is set to null by the library on success.</summary>
    public const string CloseExportName = "gclib_close";

    /// <summary>Reads the connection type of the open handle.</summary>
    public const string ConnectionTypeExportName = "gclib_connection_type";

    /// <summary>Reads the bounded revision-information response (the vendor-declared ^R^V query).</summary>
    public const string RevisionInformationExportName = "gclib_revision_information";

    /// <summary>Reads the controller serial number as an unsigned 32-bit value.</summary>
    public const string SerialNumberExportName = "gclib_serial_number";

    /// <summary>
    /// The complete B1 export allow-list, in fixed resolution order. The backing array is private
    /// and the returned read-only wrapper cannot be downcast to or used to mutate that array. No
    /// other export may be resolved or invoked by this boundary; Gate A's version export remains
    /// authorized for the probe surface only.
    /// </summary>
    private static readonly IReadOnlyList<string> AuthorizedControllerExportNames = Array.AsReadOnly(new[]
    {
        ForceDirectModeExportName,
        OpenExportName,
        CloseExportName,
        ConnectionTypeExportName,
        RevisionInformationExportName,
        SerialNumberExportName,
    });

    /// <summary>Read-only view of the fixed six-export B1 allow-list.</summary>
    public static IReadOnlyList<string> AuthorizedControllerExports => AuthorizedControllerExportNames;

    // ------------------------------------------------------------- result / enum constants

    /// <summary>gclib_result: GCLIB_SUCCESS.</summary>
    public const int ResultSuccess = 0;

    /// <summary>gclib_result: GCLIB_INVALID_ARGUMENT.</summary>
    public const int ResultInvalidArgument = 1;

    /// <summary>gclib_result: GCLIB_INTERNAL_ERROR.</summary>
    public const int ResultInternalError = 2;

    /// <summary>gclib_result: GCLIB_TIMEOUT.</summary>
    public const int ResultTimeout = 3;

    /// <summary>gclib_result: GCLIB_COMMAND_ERROR.</summary>
    public const int ResultCommandError = 4;

    /// <summary>gclib_result: GCLIB_BUFFER_TOO_SMALL.</summary>
    public const int ResultBufferTooSmall = 5;

    /// <summary>gclib_result: GCLIB_NOT_CONNECTED.</summary>
    public const int ResultNotConnected = 6;

    /// <summary>gclib_result: GCLIB_NOT_SUBSCRIBED.</summary>
    public const int ResultNotSubscribed = 7;

    /// <summary>gclib_connection_t: GCLIB_ETHERNET = 1.</summary>
    public const int ConnectionTypeEthernet = 1;

    /// <summary>gclib_connection_t: GCLIB_SERIAL.</summary>
    public const int ConnectionTypeSerial = 2;

    /// <summary>gclib_connection_t: GCLIB_PCI.</summary>
    public const int ConnectionTypePci = 3;

    // -------------------------------------------------------------------------- budgets

    /// <summary>The approved per-read budget: 2000 ms.</summary>
    public const int ReadBudgetMilliseconds = 2000;

    /// <summary>The approved total lifecycle budget: 10000 ms.</summary>
    public const int TotalLifecycleBudgetMilliseconds = 10000;

    /// <summary>The approved cleanup budget: 2000 ms.</summary>
    public const int CleanupBudgetMilliseconds = 2000;

    /// <summary>The bounded, zero-filled revision-information buffer size.</summary>
    public const int RevisionBufferLength = 256;

    /// <summary>
    /// Builds the one open value in memory from the opaque configuration produced by the endpoint
    /// resolver. Raw strings cannot enter this path from a public caller.
    ///
    /// The value is the validated canonical endpoint and NOTHING else: the manual's gclib_open()
    /// entry (printed page 48) documents its second parameter as the controller address, and the
    /// option switches belong to the deprecated legacy GOpen() parameter only (printed page 76).
    /// Direct mode is not an address option here: it is the process-wide gclib_force_gcaps(false)
    /// call the session makes before this value is used.
    /// </summary>
    internal static string BuildOpenAddress(MotionControllerEndpointConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.EndpointValue;
    }

    /// <summary>
    /// The SINGLE canonical classification of one gclib_open result, shared by the B1 identity
    /// session, B2 observation trial, and the isolated C1 Stop trial so the mapping cannot drift.
    /// These compositions remain separate; only the open-result vocabulary is shared. Each installed-header
    /// value keeps its own redacted refusal word; a value outside the header keeps its own word so
    /// an unexpected result is never confused with a known failure. The integer, the vendor error
    /// string and every other native detail stay inside the boundary.
    /// </summary>
    internal static string ClassifyOpenRefusal(int openResult) => openResult switch
    {
        ResultInvalidArgument => MotionControllerIdentityVocabulary.OpenInvalidArgument,
        ResultInternalError => MotionControllerIdentityVocabulary.OpenInternalError,
        ResultTimeout => MotionControllerIdentityVocabulary.ConnectionTimeout,
        ResultCommandError => MotionControllerIdentityVocabulary.OpenCommandError,
        ResultBufferTooSmall => MotionControllerIdentityVocabulary.OpenBufferTooSmall,
        ResultNotConnected => MotionControllerIdentityVocabulary.OpenNotConnected,
        ResultNotSubscribed => MotionControllerIdentityVocabulary.OpenNotSubscribed,
        _ => MotionControllerIdentityVocabulary.OpenResultUnrecognised,
    };
}
