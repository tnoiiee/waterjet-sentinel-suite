namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The one-shot native lifecycle for the isolated C1 Stop trial. It proves Gate A first, loads
/// only the two already-approved module paths through the restricted loader, proves both loaded
/// paths, resolves only direct-mode/open/close plus the already-approved command export, opens one
/// handle, and asks the existing typed Stop owner for one A-or-B dispatch at most. Cleanup always
/// invalidates the Stop boundary before close and unload. This session performs no identity reads,
/// retry, reconnect, replay, confirmation read or second-axis operation.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopTrialSession
{
    private readonly MotionLibraryProbeConfiguration _nativeConfiguration;
    private readonly MotionControllerEndpointConfiguration _endpointConfiguration;
    private readonly MotionStopAxis _axis;
    private readonly Func<MotionLibraryProbeOutcome> _gateAProbe;
    private readonly Func<IControllerKernel> _controllerKernelFactory;
    private readonly Func<IntPtr, IntPtr, IStopDispatchKernel> _stopDispatchKernelFactory;
    private int _started;

    internal MotionStopTrialSession(
        MotionLibraryProbeConfiguration nativeConfiguration,
        MotionControllerEndpointConfiguration endpointConfiguration,
        MotionStopAxis axis,
        Func<MotionLibraryProbeOutcome> gateAProbe,
        Func<IControllerKernel> controllerKernelFactory,
        Func<IntPtr, IntPtr, IStopDispatchKernel> stopDispatchKernelFactory)
    {
        _nativeConfiguration = nativeConfiguration ?? throw new ArgumentNullException(nameof(nativeConfiguration));
        _endpointConfiguration = endpointConfiguration ?? throw new ArgumentNullException(nameof(endpointConfiguration));
        _gateAProbe = gateAProbe ?? throw new ArgumentNullException(nameof(gateAProbe));
        _controllerKernelFactory = controllerKernelFactory ?? throw new ArgumentNullException(nameof(controllerKernelFactory));
        _stopDispatchKernelFactory = stopDispatchKernelFactory ?? throw new ArgumentNullException(nameof(stopDispatchKernelFactory));
        _axis = axis;
    }

    /// <summary>Runs this session at most once. A later call is a refusal and makes no native call.</summary>
    internal MotionStopTrialOutcome Run()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return MotionStopTrialOutcome.RefusedBeforeNative(
                MotionStopTrialVocabulary.TrialAlreadyRun,
                consentCode: MotionStopTrialVocabulary.OwnerConsentAccepted,
                endpointCode: MotionControllerIdentityVocabulary.EndpointConfigured,
                nativeConfigurationCode: MotionLibraryProbeVocabulary.ConfigurationRedacted,
                axis: AxisToken(_axis));
        }

        if (!MotionStopCommandPlan.IsDefined(_axis))
        {
            return MotionStopTrialOutcome.RefusedBeforeNative(
                MotionStopOutcomeCodes.AxisOutOfDomain,
                consentCode: MotionStopTrialVocabulary.OwnerConsentAccepted,
                endpointCode: MotionControllerIdentityVocabulary.EndpointConfigured,
                nativeConfigurationCode: MotionLibraryProbeVocabulary.ConfigurationRedacted);
        }

        return RunCore();
    }

    private MotionStopTrialOutcome RunCore()
    {
        var outcome = MotionStopTrialOutcome.AcceptedPreconditions(_axis);
        IControllerKernel? controllerKernel = null;
        MotionStopOwner? owner = null;
        IntPtr companionModule = IntPtr.Zero;
        IntPtr primaryModule = IntPtr.Zero;
        IntPtr controllerHandle = IntPtr.Zero;
        IntPtr closeExport = IntPtr.Zero;
        var gateAHandlesReleased = false;
        var untrackedModuleLoadPossible = false;
        var primaryModuleReleased = true;
        var companionModuleReleased = true;
        var handleClosed = true;
        var stopSessionInvalidated = true;

        try
        {
            // Gate A is the first native step. It proves the Owner-local file identities, x64
            // images, approved loaded paths and version; it resolves/invokes only gclib_version.
            MotionLibraryProbeOutcome? gateAOutcome;
            try
            {
                gateAOutcome = _gateAProbe();
            }
            catch (Exception)
            {
                gateAHandlesReleased = false;
                outcome.NativeProofCode = MotionStopTrialVocabulary.NativeProofRefused;
                Refuse(outcome, MotionStopTrialVocabulary.TrialAborted);
                return outcome;
            }

            if (gateAOutcome is null)
            {
                gateAHandlesReleased = false;
                outcome.NativeProofCode = MotionStopTrialVocabulary.NativeProofRefused;
                Refuse(outcome, MotionStopTrialVocabulary.TrialAborted);
                return outcome;
            }

            gateAHandlesReleased = gateAOutcome.NativeHandlesReleased;
            if (!gateAOutcome.Accepted || !gateAOutcome.NativeHandlesReleased)
            {
                var probeCode = SafeProbeCode(gateAOutcome.Code);
                outcome.NativeProofCode = probeCode;
                Refuse(
                    outcome,
                    gateAOutcome.Accepted
                        ? MotionLibraryProbeVocabulary.NativeUnloadFailed
                        : probeCode);
                return outcome;
            }

            controllerKernel = _controllerKernelFactory();
            if (controllerKernel is null)
            {
                outcome.NativeProofCode = MotionStopTrialVocabulary.NativeProofRefused;
                Refuse(outcome, MotionStopTrialVocabulary.TrialAborted);
                return outcome;
            }

            // Companion first, then primary: this mirrors the existing restricted B2 lifecycle.
            // If a load call throws before returning a handle, do not guess that no module loaded.
            try
            {
                untrackedModuleLoadPossible = true;
                companionModule = controllerKernel.LoadApprovedModule(_nativeConfiguration.CompanionModulePath);
                untrackedModuleLoadPossible = false;
            }
            catch (Exception)
            {
                outcome.NativeProofCode = MotionStopTrialVocabulary.NativeProofRefused;
                Refuse(outcome, MotionStopTrialVocabulary.NativeProofRefused);
                return outcome;
            }

            if (companionModule == IntPtr.Zero)
            {
                outcome.NativeProofCode = MotionLibraryProbeVocabulary.NativeLoadFailed;
                Refuse(outcome, MotionLibraryProbeVocabulary.NativeLoadFailed);
                return outcome;
            }

            if (!ProveLoadedModulePath(
                    controllerKernel,
                    companionModule,
                    _nativeConfiguration.CompanionModulePath))
            {
                outcome.NativeProofCode = MotionLibraryProbeVocabulary.NativeModulePathMismatch;
                Refuse(outcome, MotionLibraryProbeVocabulary.NativeModulePathMismatch);
                return outcome;
            }

            try
            {
                untrackedModuleLoadPossible = true;
                primaryModule = controllerKernel.LoadApprovedModule(_nativeConfiguration.PrimaryModulePath);
                untrackedModuleLoadPossible = false;
            }
            catch (Exception)
            {
                outcome.NativeProofCode = MotionStopTrialVocabulary.NativeProofRefused;
                Refuse(outcome, MotionStopTrialVocabulary.NativeProofRefused);
                return outcome;
            }

            if (primaryModule == IntPtr.Zero)
            {
                outcome.NativeProofCode = MotionLibraryProbeVocabulary.NativeLoadFailed;
                Refuse(outcome, MotionLibraryProbeVocabulary.NativeLoadFailed);
                return outcome;
            }

            if (!ProveLoadedModulePath(
                    controllerKernel,
                    primaryModule,
                    _nativeConfiguration.PrimaryModulePath))
            {
                outcome.NativeProofCode = MotionLibraryProbeVocabulary.NativeModulePathMismatch;
                Refuse(outcome, MotionLibraryProbeVocabulary.NativeModulePathMismatch);
                return outcome;
            }

            // Resolve only the required lifecycle exports and the existing fixed-plan Stop export.
            // No B1 identity-read or B2 observation export is resolved here.
            var forceDirectExport = controllerKernel.ResolveExport(
                primaryModule,
                MotionControllerApi.ForceDirectModeExportName);
            var openExport = controllerKernel.ResolveExport(primaryModule, MotionControllerApi.OpenExportName);
            closeExport = controllerKernel.ResolveExport(primaryModule, MotionControllerApi.CloseExportName);
            if (forceDirectExport == IntPtr.Zero || openExport == IntPtr.Zero || closeExport == IntPtr.Zero)
            {
                outcome.NativeProofCode = MotionLibraryProbeVocabulary.NativeExportNotFound;
                Refuse(outcome, MotionLibraryProbeVocabulary.NativeExportNotFound);
                return outcome;
            }

            var commandExport = MotionControllerCommandApi.ResolveCommandExport(controllerKernel, primaryModule);
            if (commandExport == IntPtr.Zero)
            {
                outcome.NativeProofCode = MotionStopTrialVocabulary.NativeProofAccepted;
                Refuse(outcome, MotionStopOutcomeCodes.BoundaryUnavailable);
                return outcome;
            }

            outcome.NativeProofCode = MotionStopTrialVocabulary.NativeProofAccepted;

            // Direct/no-gcaps mode is process-wide and must precede the one open. It cannot be
            // turned into an endpoint option, bypassed, retried or reselected by this harness.
            try
            {
                controllerKernel.InvokeForceDirectMode(forceDirectExport, directOnly: 0);
            }
            catch (Exception)
            {
                Refuse(outcome, MotionControllerIdentityVocabulary.DirectModeNotEnforced);
                return outcome;
            }

            var address = MotionControllerApi.BuildOpenAddress(_endpointConfiguration);
            var addressBuffer = ToAddressBuffer(address);
            outcome.OpenAttempted = true;

            int openResult;
            try
            {
                openResult = controllerKernel.InvokeOpen(openExport, addressBuffer, out controllerHandle);
            }
            catch (Exception)
            {
                outcome.OpenClassification = MotionControllerIdentityVocabulary.ConnectionFailed;
                Refuse(outcome, MotionControllerIdentityVocabulary.ConnectionFailed);
                AttachOwnerIfHandleExists(
                    controllerKernel,
                    commandExport,
                    primaryModule,
                    closeExport,
                    ref controllerHandle,
                    ref owner,
                    outcome);
                return outcome;
            }

            if (openResult == MotionControllerApi.ResultSuccess)
            {
                outcome.OpenClassification = MotionStopTrialVocabulary.OpenSucceeded;
            }
            else
            {
                outcome.OpenClassification = MotionControllerApi.ClassifyOpenRefusal(openResult);
            }

            AttachOwnerIfHandleExists(
                controllerKernel,
                commandExport,
                primaryModule,
                closeExport,
                ref controllerHandle,
                ref owner,
                outcome);

            if (openResult != MotionControllerApi.ResultSuccess)
            {
                Refuse(outcome, outcome.OpenClassification);
                return outcome;
            }

            if (controllerHandle == IntPtr.Zero)
            {
                Refuse(outcome, MotionControllerIdentityVocabulary.ConnectionHandleInvalid);
                return outcome;
            }

            if (owner is null)
            {
                Refuse(outcome, MotionStopTrialVocabulary.TrialAborted);
                return outcome;
            }

            // One typed A/B Stop request. MotionStopSession owns the single operation slot and the
            // BoundStopDispatchKernel consumes at most one fixed plan template. There is no loop.
            MotionStopOutcome stopOutcome;
            try
            {
                stopOutcome = owner.Session.Stop(_axis);
            }
            catch (Exception)
            {
                Refuse(outcome, MotionStopTrialVocabulary.TrialAborted);
                return outcome;
            }

            outcome.StopCode = stopOutcome.Code;
            outcome.NativeDispatchAttempted = stopOutcome.NativeDispatchAttempted;
            outcome.StopDispatched = stopOutcome.StopDispatched;
            if (stopOutcome.StopDispatched && !stopOutcome.NativeDispatchAttempted)
            {
                outcome.StopCode = MotionStopOutcomeCodes.DispatchFailed;
                outcome.StopDispatched = false;
                Refuse(outcome, MotionStopOutcomeCodes.DispatchFailed);
            }
            else if (!stopOutcome.StopDispatched)
            {
                Refuse(
                    outcome,
                    string.IsNullOrEmpty(stopOutcome.Code)
                        ? MotionStopTrialVocabulary.TrialAborted
                        : stopOutcome.Code);
            }
        }
        catch (Exception)
        {
            if (!string.Equals(
                    outcome.NativeProofCode,
                    MotionStopTrialVocabulary.NativeProofAccepted,
                    StringComparison.Ordinal)
                && !string.Equals(
                    outcome.NativeProofCode,
                    MotionStopTrialVocabulary.NativeProofRefused,
                    StringComparison.Ordinal))
            {
                outcome.NativeProofCode = MotionStopTrialVocabulary.NativeProofRefused;
            }

            Refuse(outcome, MotionStopTrialVocabulary.TrialAborted);
        }
        finally
        {
            // The existing Stop owner enforces invalidate -> close -> primary unload. The
            // companion is released only after that owner has returned from its close attempt.
            if (owner is not null)
            {
                try
                {
                    var closeOutcome = owner.Close();
                    stopSessionInvalidated = closeOutcome.SessionInvalidated;
                    handleClosed = closeOutcome.HandleClosed;
                    primaryModuleReleased = closeOutcome.ModuleReleased;
                    primaryModule = IntPtr.Zero; // owner made the one release attempt; never retry.

                    if (closeOutcome.SessionInvalidated)
                    {
                        companionModuleReleased = TryReleaseModule(controllerKernel, ref companionModule);
                    }
                    else
                    {
                        // An uninvalidated dispatch boundary must not be followed by another
                        // native lifetime operation. Leave the companion loaded and report it.
                        companionModuleReleased = false;
                        companionModule = IntPtr.Zero;
                    }
                }
                catch (Exception)
                {
                    stopSessionInvalidated = false;
                    handleClosed = false;
                    primaryModuleReleased = false;
                    companionModuleReleased = false;
                    // Do not close/unload after invalidation itself failed. No second attempt.
                    primaryModule = IntPtr.Zero;
                    companionModule = IntPtr.Zero;
                    controllerHandle = IntPtr.Zero;
                }
            }
            else
            {
                if (controllerHandle != IntPtr.Zero)
                {
                    handleClosed = TryCloseHandle(
                        controllerKernel,
                        closeExport,
                        ref controllerHandle);
                }

                // No Stop owner/boundary exists on these early refusal paths. Close is still
                // attempted before either approved module is released, and failures are not retried.
                primaryModuleReleased = TryReleaseModule(controllerKernel, ref primaryModule);
                companionModuleReleased = TryReleaseModule(controllerKernel, ref companionModule);
            }

            outcome.StopSessionInvalidated = stopSessionInvalidated;
            outcome.ControllerHandleClosed = handleClosed;
            outcome.NativeModulesReleased = gateAHandlesReleased
                && !untrackedModuleLoadPossible
                && primaryModuleReleased
                && companionModuleReleased;

            if (outcome.StopSessionInvalidated
                && outcome.ControllerHandleClosed
                && outcome.NativeModulesReleased)
            {
                outcome.CleanupCode = MotionStopTrialVocabulary.CleanupComplete;
                outcome.CleanupFailureCode = string.Empty;
            }
            else
            {
                outcome.CleanupCode = MotionStopTrialVocabulary.CleanupIncomplete;
                outcome.CleanupFailureCode = !outcome.StopSessionInvalidated || !outcome.ControllerHandleClosed
                    ? MotionControllerIdentityVocabulary.CleanupUnproven
                    : MotionControllerIdentityVocabulary.NativeUnloadFailed;
                Refuse(outcome, outcome.CleanupFailureCode);
            }

            outcome.ExitCode = outcome.StopDispatched
                && string.Equals(
                    outcome.CleanupCode,
                    MotionStopTrialVocabulary.CleanupComplete,
                    StringComparison.Ordinal)
                    ? 0
                    : 2;
        }

        return outcome;
    }

    private void AttachOwnerIfHandleExists(
        IControllerKernel controllerKernel,
        IntPtr commandExport,
        IntPtr ownerModule,
        IntPtr closeExport,
        ref IntPtr controllerHandle,
        ref MotionStopOwner? owner,
        MotionStopTrialOutcome outcome)
    {
        if (controllerHandle == IntPtr.Zero)
        {
            return;
        }

        IStopDispatchKernel? dispatchKernel = null;
        try
        {
            dispatchKernel = _stopDispatchKernelFactory(controllerHandle, commandExport);
            owner = new MotionStopOwner(
                controllerKernel,
                dispatchKernel,
                ownerModule,
                controllerHandle,
                closeExport);
            dispatchKernel = null; // the owner now owns invalidation of this boundary.
        }
        catch (Exception)
        {
            try
            {
                dispatchKernel?.Dispose();
            }
            catch (Exception)
            {
                // No native call is made by invalidation; retain the redacted refusal only.
            }

            Refuse(outcome, MotionStopTrialVocabulary.TrialAborted);
        }
    }

    private static bool ProveLoadedModulePath(
        IControllerKernel kernel,
        IntPtr module,
        string approvedPath)
    {
        var loadedPath = kernel.GetLoadedModulePath(module);
        return loadedPath is not null
            && string.Equals(
                Path.GetFullPath(loadedPath),
                approvedPath,
                StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] ToAddressBuffer(string address)
    {
        var buffer = new byte[System.Text.Encoding.ASCII.GetByteCount(address) + 1];
        _ = System.Text.Encoding.ASCII.GetBytes(address, 0, address.Length, buffer, 0);
        return buffer;
    }

    private static bool TryCloseHandle(
        IControllerKernel? kernel,
        IntPtr closeExport,
        ref IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return true;
        }

        if (kernel is null || closeExport == IntPtr.Zero)
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
    }

    private static bool TryReleaseModule(IControllerKernel? kernel, ref IntPtr module)
    {
        if (module == IntPtr.Zero)
        {
            return true;
        }

        if (kernel is null)
        {
            return false;
        }

        var handle = module;
        module = IntPtr.Zero; // one attempt only, including when the kernel throws.
        try
        {
            return kernel.ReleaseModule(handle);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string SafeProbeCode(string? code) => code switch
    {
        MotionLibraryProbeVocabulary.NativeLibDirNotConfigured => MotionLibraryProbeVocabulary.NativeLibDirNotConfigured,
        MotionLibraryProbeVocabulary.NativeLibDirRejected => MotionLibraryProbeVocabulary.NativeLibDirRejected,
        MotionLibraryProbeVocabulary.NativeFileNotFound => MotionLibraryProbeVocabulary.NativeFileNotFound,
        MotionLibraryProbeVocabulary.NativeFileIdentityMismatch => MotionLibraryProbeVocabulary.NativeFileIdentityMismatch,
        MotionLibraryProbeVocabulary.NativeFileReadFailed => MotionLibraryProbeVocabulary.NativeFileReadFailed,
        MotionLibraryProbeVocabulary.NativeArchitectureMismatch => MotionLibraryProbeVocabulary.NativeArchitectureMismatch,
        MotionLibraryProbeVocabulary.NativeLoadFailed => MotionLibraryProbeVocabulary.NativeLoadFailed,
        MotionLibraryProbeVocabulary.NativeExportNotFound => MotionLibraryProbeVocabulary.NativeExportNotFound,
        MotionLibraryProbeVocabulary.NativeVersionCallFailed => MotionLibraryProbeVocabulary.NativeVersionCallFailed,
        MotionLibraryProbeVocabulary.NativeVersionMismatch => MotionLibraryProbeVocabulary.NativeVersionMismatch,
        MotionLibraryProbeVocabulary.NativeModulePathMismatch => MotionLibraryProbeVocabulary.NativeModulePathMismatch,
        MotionLibraryProbeVocabulary.NativeUnloadFailed => MotionLibraryProbeVocabulary.NativeUnloadFailed,
        MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform => MotionLibraryProbeVocabulary.NativeProbeUnsupportedPlatform,
        _ => MotionStopTrialVocabulary.NativeProofRefused,
    };

    private static void Refuse(MotionStopTrialOutcome outcome, string refusalCode)
    {
        if (string.IsNullOrEmpty(outcome.RefusalCode))
        {
            outcome.RefusalCode = refusalCode;
        }
    }

    private static string? AxisToken(MotionStopAxis axis) => axis switch
    {
        MotionStopAxis.A => "A",
        MotionStopAxis.B => "B",
        _ => null,
    };
}
