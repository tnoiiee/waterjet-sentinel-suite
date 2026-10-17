namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The result of the injected precondition evaluation that runs before anything is loaded.
/// The production harness reports the workstation posture through the Owner-local runbook; this
/// hook exists so the concurrent-authority refusal is a real, testable code path and so no
/// in-process machine inspection is invented here.
/// </summary>
public sealed record ControllerAuthorityPrecondition
{
    /// <summary>True when the session may proceed.</summary>
    public required bool Allowed { get; init; }

    /// <summary>Refusal code when not allowed.</summary>
    public required string RefusalCode { get; init; }

    /// <summary>Redacted refusal detail when not allowed.</summary>
    public required string RefusalDetail { get; init; }

    /// <summary>The allowed result.</summary>
    public static ControllerAuthorityPrecondition Allow() => new()
    {
        Allowed = true,
        RefusalCode = string.Empty,
        RefusalDetail = string.Empty,
    };
}

/// <summary>
/// The B1 one-controller identity session: one Owner-invoked, one-shot, fail-closed run that
/// proves the Gate A native boundary, enforces the direct/no-gcaps posture, opens exactly ONE
/// controller handle against the Owner-local endpoint, performs exactly the three approved
/// identity reads (connection type, revision information, serial number) in a fixed order,
/// classifies the identity evidence redactedly, and closes/releases everything deterministically.
///
/// Hard rules encoded here:
///   - exactly one open attempt; no retry, no reconnect, no polling, no replay;
///   - NO raw controller command of any kind is sent, and none can be constructed from here;
///   - no axis read, no motion, no controller configuration, no discovery, no gcaps;
///   - one connection generation; results are tagged and late results are rejected;
///   - one outstanding native operation with fail-fast busy refusal;
///   - the controller handle is closed on EVERY post-open path and the outcome tells the truth
///     about the final cleanup state;
///   - the total lifecycle budget is 10000 ms (a native call cannot be interrupted; containment
///     is the isolated one-shot process, and an exceeded budget is refused, never hidden).
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
public sealed class MotionControllerIdentitySession
{
    private readonly MotionLibraryProbeConfiguration _probeConfiguration;
    private readonly MotionControllerEndpointConfiguration _endpointConfiguration;
    private readonly Func<MotionLibraryProbeOutcome> _gateAProbe;
    private readonly Func<IControllerKernel> _kernelFactory;
    private readonly Func<ControllerAuthorityPrecondition> _evaluatePreconditions;
    private readonly Func<long> _elapsedMilliseconds;

    private long _generation = 1;
    private int _nativeOperationInFlight;
    private int _started;

    private static readonly System.Diagnostics.Stopwatch LifecycleStopwatch = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// The production boundary: the real probe runner over the real kernel, the real controller
    /// kernel, the runbook-posture precondition and a stopwatch lifecycle clock.
    /// </summary>
    public MotionControllerIdentitySession(
        MotionLibraryProbeConfiguration probeConfiguration,
        MotionControllerEndpointConfiguration endpointConfiguration)
        : this(
            probeConfiguration,
            endpointConfiguration,
            () => new MotionLibraryProbeRunner(probeConfiguration).Probe(),
            () => new WindowsControllerKernel(),
            ControllerAuthorityPrecondition.Allow,
            StopwatchElapsedMilliseconds)
    {
    }

    /// <summary>Test seam (InternalsVisibleTo): exercises the complete B1 defect class without any native module.</summary>
    internal MotionControllerIdentitySession(
        MotionLibraryProbeConfiguration probeConfiguration,
        MotionControllerEndpointConfiguration endpointConfiguration,
        Func<MotionLibraryProbeOutcome> gateAProbe,
        Func<IControllerKernel> kernelFactory,
        Func<ControllerAuthorityPrecondition> evaluatePreconditions,
        Func<long> elapsedMilliseconds)
    {
        _probeConfiguration = probeConfiguration ?? throw new ArgumentNullException(nameof(probeConfiguration));
        _endpointConfiguration = endpointConfiguration ?? throw new ArgumentNullException(nameof(endpointConfiguration));
        _gateAProbe = gateAProbe ?? throw new ArgumentNullException(nameof(gateAProbe));
        _kernelFactory = kernelFactory ?? throw new ArgumentNullException(nameof(kernelFactory));
        _evaluatePreconditions = evaluatePreconditions ?? throw new ArgumentNullException(nameof(evaluatePreconditions));
        _elapsedMilliseconds = elapsedMilliseconds ?? throw new ArgumentNullException(nameof(elapsedMilliseconds));
    }

    private static long StopwatchElapsedMilliseconds() => LifecycleStopwatch.ElapsedMilliseconds;

    /// <summary>The current connection generation (one per session; incremented on invalidation).</summary>
    internal long CurrentGeneration => Interlocked.Read(ref _generation);

    /// <summary>Invalidates the current generation so any in-flight result becomes stale.</summary>
    internal void InvalidateGeneration() => Interlocked.Increment(ref _generation);

    /// <summary>Fail-fast one-outstanding gate: true when the caller now owns the single native slot.</summary>
    internal bool TryBeginNativeOperation() => Interlocked.CompareExchange(ref _nativeOperationInFlight, 1, 0) == 0;

    /// <summary>Releases the single native slot.</summary>
    internal void EndNativeOperation() => Volatile.Write(ref _nativeOperationInFlight, 0);

    /// <summary>Executes the one-shot session exactly once. A second call is refused, never retried.</summary>
    public MotionControllerIdentityOutcome Run()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return MotionControllerIdentityOutcome.Refused(
                MotionControllerIdentityVocabulary.OperationBusy,
                "This identity session has already run. A session is one-shot: create a new session " +
                "for a new connection generation instead of retrying.",
                CurrentGeneration,
                controllerHandleClosed: true,
                nativeHandlesReleased: true);
        }

        return RunCore();
    }

    private MotionControllerIdentityOutcome RunCore()
    {
        IControllerKernel? kernel = null;
        IntPtr companionModule = IntPtr.Zero;
        IntPtr primaryModule = IntPtr.Zero;
        IntPtr closeExport = IntPtr.Zero;
        IntPtr handle = IntPtr.Zero;
        var opened = false;
        MotionControllerIdentityOutcome? finalOutcome = null;

        MotionControllerIdentityOutcome ExecuteIdentityFlow()
        {
            // Early returns are provisional; RunCore owns cleanup and its single final return.
            MotionControllerIdentityOutcome? outcome = null;

            bool BudgetExceeded() => _elapsedMilliseconds() > MotionControllerApi.TotalLifecycleBudgetMilliseconds;

            MotionControllerIdentityOutcome Stale() => MotionControllerIdentityOutcome.Refused(
                MotionControllerIdentityVocabulary.StaleGenerationResult,
                "A result arrived for a previous connection generation and was rejected as stale. No " +
                "value from the stale generation is delivered as current.",
                CurrentGeneration,
                controllerHandleClosed: false,
                nativeHandlesReleased: false);

            MotionControllerIdentityOutcome RefusedBudget() => MotionControllerIdentityOutcome.Refused(
                MotionControllerIdentityVocabulary.LifecycleBudgetExceeded,
                "The total lifecycle budget (10000 ms) elapsed before the session completed. A native " +
                "call cannot be interrupted by this boundary: containment is the isolated one-shot " +
                "process, and an exceeded budget is refused, never hidden.",
                CurrentGeneration,
                controllerHandleClosed: false,
                nativeHandlesReleased: false);

            // Phase 0: injected precondition posture (before anything is loaded).
            var precondition = _evaluatePreconditions();
            if (precondition is null || !precondition.Allowed)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.ConcurrentControllerAuthorityDetected,
                    "The precondition evaluation reports a concurrent controller authority. Nothing " +
                    "was loaded and nothing was contacted; the session requires exclusive ownership " +
                    "of the one controller session.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: true);
                return outcome;
            }

            if (BudgetExceeded())
            {
                outcome = RefusedBudget();
                return outcome;
            }

            // Phase 1: Gate A native identity/version proof. No connection attempt may precede it.
            var probeOutcome = _gateAProbe();
            if (probeOutcome is null || !probeOutcome.Accepted)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "The Gate A native load / version proof refused (refusal code recorded by the " +
                    "probe surface). No connection was attempted and nothing was contacted.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: probeOutcome?.NativeHandlesReleased ?? true);
                return outcome;
            }

            if (BudgetExceeded())
            {
                outcome = RefusedBudget();
                return outcome;
            }

            // Phase 2: load the approved modules (companion first), prove module paths, resolve
            // exactly the authorized B1 exports from the primary module.
            kernel = _kernelFactory();
            companionModule = kernel.LoadApprovedModule(_probeConfiguration.CompanionModulePath);
            if (companionModule == IntPtr.Zero)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "The approved companion module could not be loaded from its approved location " +
                    "with the restricted search set. Nothing was contacted.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: true);
                return outcome;
            }

            if (!ProveLoadedModulePath(kernel, companionModule, _probeConfiguration.CompanionModulePath))
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "The loaded companion module path could not be proven equal to the approved " +
                    "file. The boundary never reports the substituted location; nothing was contacted.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: false);
                return outcome;
            }

            primaryModule = kernel.LoadApprovedModule(_probeConfiguration.PrimaryModulePath);
            if (primaryModule == IntPtr.Zero)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "The approved primary module could not be loaded from its approved location " +
                    "with the restricted search set. Nothing was contacted.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: false);
                return outcome;
            }

            if (!ProveLoadedModulePath(kernel, primaryModule, _probeConfiguration.PrimaryModulePath))
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.NativeProofRefused,
                    "The loaded primary module path could not be proven equal to the approved file. " +
                    "Nothing was contacted.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: false);
                return outcome;
            }

            var exports = new IntPtr[MotionControllerApi.AuthorizedControllerExports.Count];
            for (var index = 0; index < MotionControllerApi.AuthorizedControllerExports.Count; index++)
            {
                var exportName = MotionControllerApi.AuthorizedControllerExports[index];
                var export = kernel.ResolveExport(primaryModule, exportName);
                if (export == IntPtr.Zero)
                {
                    outcome = MotionControllerIdentityOutcome.Refused(
                        MotionControllerIdentityVocabulary.NativeProofRefused,
                        $"The authorized export {exportName} is absent from the loaded approved module. " +
                        "No other export is ever resolved by this boundary and nothing was contacted.",
                        CurrentGeneration,
                        controllerHandleClosed: true,
                        nativeHandlesReleased: false);
                    return outcome;
                }

                exports[index] = export;
            }

            // Allow-list resolution order: [0] force-direct, [1] open, [2] close,
            // [3] connection type, [4] revision information, [5] serial number.
            var forceExport = exports[0];
            var openExport = exports[1];
            closeExport = exports[2];
            var connectionTypeExport = exports[3];
            var revisionExport = exports[4];
            var serialExport = exports[5];

            if (BudgetExceeded())
            {
                outcome = RefusedBudget();
                return outcome;
            }

            // Phase 3: enforce the direct/no-gcaps posture BEFORE any open. The call is
            // process-wide and irreversible by design (documented vendor behavior). The direct
            // flag byte is zero: gcaps must never be used.
            var capturedForce = CurrentGeneration;
            if (!TryBeginNativeOperation())
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.OperationBusy,
                    "A native operation was already outstanding when the direct-mode posture call " +
                    "was attempted. Nothing was opened.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: false);
                return outcome;
            }

            try
            {
                kernel.InvokeForceDirectMode(forceExport, directOnly: 0);
            }
            catch (Exception)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.DirectModeNotEnforced,
                    "The direct/no-gcaps posture call did not complete. No open was attempted after " +
                    "the failure and nothing was contacted.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: false);
                return outcome;
            }
            finally
            {
                EndNativeOperation();
            }

            if (capturedForce != CurrentGeneration)
            {
                outcome = Stale();
                return outcome;
            }

            // Phase 4: exactly one open attempt with the in-memory direct open-address text.
            var addressBuffer = ToAsciiBuffer(
                MotionControllerApi.BuildDirectConnectionAddress(_endpointConfiguration));
            var capturedOpen = CurrentGeneration;
            if (!TryBeginNativeOperation())
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.OperationBusy,
                    "A native operation was already outstanding when the single open was attempted. " +
                    "No open was performed.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: false);
                return outcome;
            }

            int openResult;
            try
            {
                openResult = kernel.InvokeOpen(openExport, addressBuffer, out handle);
            }
            catch (Exception)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.ConnectionFailed,
                    "The single open attempt failed before a result could be classified. There is no " +
                    "retry and no reconnect; the session is over.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }
            finally
            {
                EndNativeOperation();
            }

            if (capturedOpen != CurrentGeneration)
            {
                opened = handle != IntPtr.Zero;
                outcome = Stale();
                return outcome;
            }

            opened = handle != IntPtr.Zero;

            if (openResult != MotionControllerApi.ResultSuccess)
            {
                var refusalCode = ClassifyOpenRefusal(openResult);
                var refusalDetail =
                    openResult == MotionControllerApi.ResultTimeout
                        ? "The single open attempt timed out within the library timeout (2000 ms). " +
                          "There is no retry and no reconnect; the session is over."
                        : "The single open attempt was refused and classified with the canonical " +
                          "open-result word carried by this code, in the installed library result " +
                          "order. There is no retry, no reconnect and no vendor error text; the " +
                          "session is over.";
                outcome = MotionControllerIdentityOutcome.Refused(
                    refusalCode,
                    refusalDetail,
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }

            if (handle == IntPtr.Zero)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.ConnectionHandleInvalid,
                    "gclib_open reported success but returned no controller handle. No identity reads " +
                    "were attempted; the session is over with no retry or reconnect.",
                    CurrentGeneration,
                    controllerHandleClosed: true,
                    nativeHandlesReleased: false);
                return outcome;
            }

            opened = true;

            if (BudgetExceeded())
            {
                outcome = RefusedBudget();
                return outcome;
            }

            // Phase 5: read the connection type. Expected: the documented Ethernet type.
            var capturedType = CurrentGeneration;
            if (!TryBeginNativeOperation())
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.OperationBusy,
                    "A native operation was already outstanding during the connection-type read.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }

            int typeResult;
            int connectionType;
            try
            {
                typeResult = kernel.InvokeConnectionType(connectionTypeExport, handle, out connectionType);
            }
            catch (Exception)
            {
                connectionType = 0;
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.ConnectionFailed,
                    "The connection-type read failed before a result could be classified.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }
            finally
            {
                EndNativeOperation();
            }

            if (capturedType != CurrentGeneration)
            {
                outcome = Stale();
                return outcome;
            }

            if (typeResult != MotionControllerApi.ResultSuccess)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.ConnectionFailed,
                    "The connection-type read did not return a success result.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }

            if (connectionType != MotionControllerApi.ConnectionTypeEthernet)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.ConnectionTypeMismatch,
                    "The open connection did not report the expected Ethernet connection type. The " +
                    "session refuses rather than reinterpret the connection.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false,
                    connectionTypeStatus: ConnectionTypeLabel(connectionType));
                return outcome;
            }

            if (BudgetExceeded())
            {
                outcome = RefusedBudget();
                return outcome;
            }

            // Phase 6: read revision information into a bounded, zero-filled buffer.
            var revisionBuffer = new byte[MotionControllerApi.RevisionBufferLength];
            var capturedRevision = CurrentGeneration;
            if (!TryBeginNativeOperation())
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.OperationBusy,
                    "A native operation was already outstanding during the revision-information read.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }

            int revisionResult;
            try
            {
                revisionResult = kernel.InvokeRevisionInformation(revisionExport, handle, revisionBuffer);
            }
            catch (Exception)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.RevisionResponseInvalid,
                    "The revision-information read failed before a result could be classified.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }
            finally
            {
                EndNativeOperation();
            }

            if (capturedRevision != CurrentGeneration)
            {
                outcome = Stale();
                return outcome;
            }

            if (revisionResult != MotionControllerApi.ResultSuccess
                || !TryReadPrintableResponse(revisionBuffer, out var revisionInformation))
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.RevisionResponseInvalid,
                    "The revision-information response was not a bounded, terminated, printable " +
                    "string within the approved buffer.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }

            if (BudgetExceeded())
            {
                outcome = RefusedBudget();
                return outcome;
            }

            // Phase 7: read the serial number. Nonzero required; the value itself is never kept.
            var capturedSerial = CurrentGeneration;
            if (!TryBeginNativeOperation())
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.OperationBusy,
                    "A native operation was already outstanding during the serial-number read.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }

            int serialResult;
            uint serialNumber;
            try
            {
                serialResult = kernel.InvokeSerialNumber(serialExport, handle, out serialNumber);
            }
            catch (Exception)
            {
                serialNumber = 0;
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.SerialResponseInvalid,
                    "The serial-number read failed before a result could be classified.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false);
                return outcome;
            }
            finally
            {
                EndNativeOperation();
            }

            if (capturedSerial != CurrentGeneration)
            {
                outcome = Stale();
                return outcome;
            }

            if (serialResult != MotionControllerApi.ResultSuccess || serialNumber == 0)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.SerialResponseInvalid,
                    "The serial-number read did not return a nonzero serial. A zero serial is a " +
                    "refusal, never an acceptance; the value itself is never rendered.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false,
                    revisionInformation: revisionInformation);
                return outcome;
            }

            // Phase 8: classify the identity evidence. The exact installed response format is not
            // proven, so absence of the expected model token is MODEL_CONFIRMATION_REQUIRED —
            // never a false accept and never a false reject. The serial value is discarded here
            // and never enters the outcome.
            var modelMatched = ContainsOrdinal(
                revisionInformation, MotionControllerIdentityVocabulary.ExpectedControllerModel);
            if (!modelMatched)
            {
                outcome = MotionControllerIdentityOutcome.Refused(
                    MotionControllerIdentityVocabulary.ModelConfirmationRequired,
                    "The bounded revision response does not contain the exact expected model token. " +
                    "Because the exact installed response format is not proven, physical identity is " +
                    "neither accepted nor rejected: Owner confirmation is required.",
                    CurrentGeneration,
                    controllerHandleClosed: false,
                    nativeHandlesReleased: false,
                    revisionInformation: revisionInformation,
                    modelComparison: MotionControllerIdentityVocabulary.ModelConfirmationRequired,
                    serialStatus: MotionControllerIdentityVocabulary.SerialObservedRedacted,
                    connectionTypeStatus: ConnectionTypeLabel(connectionType));
                return outcome;
            }

            if (BudgetExceeded())
            {
                outcome = RefusedBudget();
                return outcome;
            }

            outcome = MotionControllerIdentityOutcome.Complete(
                revisionInformation,
                ConnectionTypeLabel(connectionType),
                MotionControllerIdentityVocabulary.SerialObservedRedacted,
                CurrentGeneration,
                "One direct connection to the Owner-configured endpoint was opened once; the three " +
                "approved identity reads answered within budget; the model token matched; the " +
                "serial was observed and redacted; Owner manual confirmation remains required.");
            return outcome;
        }

        try
        {
            finalOutcome = ExecuteIdentityFlow();
        }
        finally
        {
            // Deterministic cleanup on EVERY post-open path: close the controller handle first,
            // then release the module handles in reverse load order. The outcome must tell the
            // truth about the final state: a failed or unconfirmed cleanup is reported as such and
            // never masks the primary refusal code.
            var handleWasOpen = opened;
            var closeConfirmed = false;
            if (handleWasOpen && kernel is not null && closeExport != IntPtr.Zero)
            {
                closeConfirmed = TryCloseHandle(kernel, closeExport, ref handle);
            }

            var modulesWereHeld = primaryModule != IntPtr.Zero || companionModule != IntPtr.Zero;
            var released = true;
            if (kernel is not null && modulesWereHeld)
            {
                released = ReleaseModules(kernel, ref primaryModule, ref companionModule);
            }

            if (handleWasOpen)
            {
                InvalidateGeneration();
            }

            if (finalOutcome is not null)
            {
                var finalHandleClosed = !handleWasOpen || closeConfirmed;
                var finalReleased = modulesWereHeld ? released : finalOutcome.NativeHandlesReleased;

                if (finalOutcome.Accepted)
                {
                    if (!finalHandleClosed)
                    {
                        var detail =
                            "Every identity read passed, but the controller handle close could not be " +
                            "confirmed. The session does not report success with an unproven cleanup; " +
                            "the isolated process containment applies.";
                        if (!finalReleased)
                        {
                            detail += " Deterministic cleanup of the loaded native handles could not " +
                                      "be fully confirmed.";
                        }

                        finalOutcome = MotionControllerIdentityOutcome.Refused(
                            MotionControllerIdentityVocabulary.CleanupUnproven,
                            detail,
                            finalOutcome.Generation,
                            controllerHandleClosed: false,
                            nativeHandlesReleased: finalReleased,
                            revisionInformation: finalOutcome.RevisionInformation,
                            modelComparison: finalOutcome.ModelComparison,
                            serialStatus: finalOutcome.SerialStatus,
                            connectionTypeStatus: finalOutcome.ConnectionTypeStatus);
                    }
                    else if (!finalReleased)
                    {
                        finalOutcome = MotionControllerIdentityOutcome.Refused(
                            MotionControllerIdentityVocabulary.NativeUnloadFailed,
                            "Every identity step passed, but a native module handle could not be " +
                            "released deterministically. The session does not report success with an " +
                            "unreleased handle.",
                            finalOutcome.Generation,
                            controllerHandleClosed: true,
                            nativeHandlesReleased: false,
                            revisionInformation: finalOutcome.RevisionInformation,
                            modelComparison: finalOutcome.ModelComparison,
                            serialStatus: finalOutcome.SerialStatus,
                            connectionTypeStatus: finalOutcome.ConnectionTypeStatus);
                    }
                    else
                    {
                        finalOutcome = finalOutcome with
                        {
                            ControllerHandleClosed = true,
                            NativeHandlesReleased = true,
                        };
                    }
                }
                else
                {
                    var detail = finalOutcome.Detail;
                    if (!finalHandleClosed)
                    {
                        detail += " The controller handle close could not be confirmed; cleanup is " +
                                  "unproven and the isolated process containment applies.";
                    }
                    if (modulesWereHeld && !released)
                    {
                        detail += " Deterministic cleanup of the loaded native handles could not be " +
                                  "fully confirmed.";
                    }

                    finalOutcome = finalOutcome with
                    {
                        Detail = detail,
                        ControllerHandleClosed = finalHandleClosed,
                        NativeHandlesReleased = finalReleased,
                    };
                }
            }
        }

        return finalOutcome ?? throw new InvalidOperationException("The identity flow produced no outcome.");
    }

    /// <summary>Closes the controller handle through the resolved close export; true only when closed and confirmed.</summary>
    private bool TryCloseHandle(IControllerKernel kernel, IntPtr closeExport, ref IntPtr handle)
    {
        if (!TryBeginNativeOperation())
        {
            return false;
        }

        try
        {
            var result = kernel.InvokeClose(closeExport, ref handle);
            return result == MotionControllerApi.ResultSuccess && handle == IntPtr.Zero;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            EndNativeOperation();
        }
    }

    private static bool ReleaseModules(IControllerKernel kernel, ref IntPtr primaryModule, ref IntPtr companionModule)
    {
        var ok = true;
        if (primaryModule != IntPtr.Zero)
        {
            ok = kernel.ReleaseModule(primaryModule) && ok;
            primaryModule = IntPtr.Zero;
        }

        if (companionModule != IntPtr.Zero)
        {
            ok = kernel.ReleaseModule(companionModule) && ok;
            companionModule = IntPtr.Zero;
        }

        return ok;
    }

    private static bool ProveLoadedModulePath(IControllerKernel kernel, IntPtr module, string approvedPath)
    {
        var loadedPath = kernel.GetLoadedModulePath(module);
        if (loadedPath is null)
        {
            return false;
        }

        var normalizedLoaded = Path.GetFullPath(loadedPath);
        return string.Equals(normalizedLoaded, approvedPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Accepts only a bounded, NUL-terminated, printable-ASCII response within the buffer. The
    /// response is taken as observed; no semantics are parsed beyond the model-token comparison.
    /// </summary>
    private static bool TryReadPrintableResponse(byte[] buffer, out string response)
    {
        response = string.Empty;
        var terminator = Array.IndexOf(buffer, (byte)0);
        if (terminator <= 0 || terminator >= buffer.Length)
        {
            return false;
        }

        for (var index = 0; index < terminator; index++)
        {
            if (buffer[index] < 0x20 || buffer[index] > 0x7E)
            {
                return false;
            }
        }

        response = System.Text.Encoding.ASCII.GetString(buffer, 0, terminator);
        return true;
    }

    private static bool ContainsOrdinal(string text, string token) =>
        text.Contains(token, StringComparison.Ordinal);

    private static byte[] ToAsciiBuffer(string value)
    {
        var bytes = new byte[System.Text.Encoding.ASCII.GetByteCount(value) + 1];
        _ = System.Text.Encoding.ASCII.GetBytes(value, 0, value.Length, bytes, 0);
        return bytes;
    }

    private static string ConnectionTypeLabel(int connectionType) => connectionType switch
    {
        MotionControllerApi.ConnectionTypeEthernet => "GCLIB_ETHERNET",
        MotionControllerApi.ConnectionTypeSerial => "GCLIB_SERIAL",
        MotionControllerApi.ConnectionTypePci => "GCLIB_PCI",
        _ => "CONNECTION_TYPE_UNRECOGNISED",
    };

    /// <summary>
    /// Maps one installed-header gclib_result value from the single open attempt to its canonical
    /// redacted refusal code. The numeric constants are the already-committed ones and match the
    /// installed gclib 2.6.0 enum order; the helper never returns the integer, never carries a
    /// vendor string and never interprets a read or a cleanup result. A value outside the header
    /// keeps its own word so an unexpected result is never confused with a known failure.
    /// </summary>
    private static string ClassifyOpenRefusal(int openResult) => openResult switch
    {
        MotionControllerApi.ResultInvalidArgument => MotionControllerIdentityVocabulary.OpenInvalidArgument,
        MotionControllerApi.ResultInternalError => MotionControllerIdentityVocabulary.OpenInternalError,
        MotionControllerApi.ResultTimeout => MotionControllerIdentityVocabulary.ConnectionTimeout,
        MotionControllerApi.ResultCommandError => MotionControllerIdentityVocabulary.OpenCommandError,
        MotionControllerApi.ResultBufferTooSmall => MotionControllerIdentityVocabulary.OpenBufferTooSmall,
        MotionControllerApi.ResultNotConnected => MotionControllerIdentityVocabulary.OpenNotConnected,
        MotionControllerApi.ResultNotSubscribed => MotionControllerIdentityVocabulary.OpenNotSubscribed,
        _ => MotionControllerIdentityVocabulary.OpenResultUnrecognised,
    };
}
