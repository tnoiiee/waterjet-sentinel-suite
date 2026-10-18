namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The only production entry to the isolated C1 Stop trial. It accepts exactly one fixed mode and
/// one typed axis token, consumes an exact-match Owner-local consent value before resolving any
/// configuration, and creates one single-use trial session. It accepts no command text, files,
/// stdin, native options, retry switch or arbitrary axis value.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal sealed class MotionStopTrialInvocation
{
    private readonly Func<string, string?> _readEnvironment;
    private readonly Action<string> _removeEnvironment;
    private readonly Func<MotionLibraryProbeConfiguration, MotionLibraryProbeOutcome> _gateAProbe;
    private readonly Func<IControllerKernel> _controllerKernelFactory;
    private readonly Func<IntPtr, IntPtr, IStopDispatchKernel> _stopDispatchKernelFactory;
    private int _started;

    /// <summary>The real, restricted production boundary used only by the console harness.</summary>
    internal MotionStopTrialInvocation()
        : this(
            Environment.GetEnvironmentVariable,
            name => Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process),
            configuration => new MotionLibraryProbeRunner(configuration).Probe(),
            () => new WindowsControllerKernel(),
            (handle, commandExport) => new BoundStopDispatchKernel(handle, commandExport))
    {
    }

    /// <summary>Fake-kernel seam for source tests; production has no caller-supplied native options.</summary>
    internal MotionStopTrialInvocation(
        Func<string, string?> readEnvironment,
        Action<string> removeEnvironment,
        Func<MotionLibraryProbeConfiguration, MotionLibraryProbeOutcome> gateAProbe,
        Func<IControllerKernel> controllerKernelFactory,
        Func<IntPtr, IntPtr, IStopDispatchKernel> stopDispatchKernelFactory)
    {
        _readEnvironment = readEnvironment ?? throw new ArgumentNullException(nameof(readEnvironment));
        _removeEnvironment = removeEnvironment ?? throw new ArgumentNullException(nameof(removeEnvironment));
        _gateAProbe = gateAProbe ?? throw new ArgumentNullException(nameof(gateAProbe));
        _controllerKernelFactory = controllerKernelFactory ?? throw new ArgumentNullException(nameof(controllerKernelFactory));
        _stopDispatchKernelFactory = stopDispatchKernelFactory ?? throw new ArgumentNullException(nameof(stopDispatchKernelFactory));
    }

    /// <summary>Consumes this invocation once. A second call on the same instance never re-reads consent.</summary>
    internal MotionStopTrialOutcome Run(string[]? arguments)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return MotionStopTrialOutcome.RefusedBeforeNative(MotionStopTrialVocabulary.TrialAlreadyRun);
        }

        var consentCode = ConsumeConsent();
        if (!string.Equals(consentCode, MotionStopTrialVocabulary.OwnerConsentAccepted, StringComparison.Ordinal))
        {
            return MotionStopTrialOutcome.RefusedBeforeNative(
                consentCode,
                consentCode: consentCode);
        }

        if (!TryParseInvocation(arguments, out var axis))
        {
            return MotionStopTrialOutcome.RefusedBeforeNative(
                MotionStopTrialVocabulary.InvalidInvocation,
                consentCode: consentCode,
                exitCode: 4);
        }

        MotionLibraryProbeConfiguration nativeConfiguration;
        string nativeConfigurationRefusal;
        try
        {
            if (!MotionLibraryConfigurationResolver.TryResolve(
                    _readEnvironment,
                    out nativeConfiguration,
                    out nativeConfigurationRefusal,
                    out _))
            {
                var refusal = string.IsNullOrEmpty(nativeConfigurationRefusal)
                    ? MotionStopTrialVocabulary.PreconditionEvaluationFailed
                    : nativeConfigurationRefusal;
                return MotionStopTrialOutcome.RefusedBeforeNative(
                    refusal,
                    consentCode: consentCode,
                    nativeConfigurationCode: refusal,
                    axis: AxisToken(axis));
            }
        }
        catch (Exception)
        {
            return MotionStopTrialOutcome.RefusedBeforeNative(
                MotionStopTrialVocabulary.PreconditionEvaluationFailed,
                consentCode: consentCode,
                nativeConfigurationCode: MotionStopTrialVocabulary.PreconditionEvaluationFailed,
                axis: AxisToken(axis));
        }

        MotionControllerEndpointConfiguration endpointConfiguration;
        string endpointRefusal;
        try
        {
            if (!MotionControllerEndpointResolver.TryResolve(
                    _readEnvironment,
                    out endpointConfiguration,
                    out endpointRefusal,
                    out _))
            {
                var refusal = string.IsNullOrEmpty(endpointRefusal)
                    ? MotionControllerIdentityVocabulary.EndpointRejected
                    : endpointRefusal;
                return MotionStopTrialOutcome.RefusedBeforeNative(
                    refusal,
                    consentCode: consentCode,
                    endpointCode: refusal,
                    nativeConfigurationCode: MotionLibraryProbeVocabulary.ConfigurationRedacted,
                    axis: AxisToken(axis));
            }
        }
        catch (Exception)
        {
            return MotionStopTrialOutcome.RefusedBeforeNative(
                MotionControllerIdentityVocabulary.EndpointRejected,
                consentCode: consentCode,
                endpointCode: MotionControllerIdentityVocabulary.EndpointRejected,
                nativeConfigurationCode: MotionLibraryProbeVocabulary.ConfigurationRedacted,
                axis: AxisToken(axis));
        }

        var session = new MotionStopTrialSession(
            nativeConfiguration,
            endpointConfiguration,
            axis,
            () => _gateAProbe(nativeConfiguration),
            _controllerKernelFactory,
            _stopDispatchKernelFactory);

        return session.Run();
    }

    /// <summary>
    /// Parses the whole invocation as one fixed mode plus exactly one uppercase A or B token.
    /// The resulting value is the closed <see cref="MotionStopAxis"/> domain, never a string axis.
    /// </summary>
    internal static bool TryParseInvocation(string[]? arguments, out MotionStopAxis axis)
    {
        axis = default;
        if (arguments is null || arguments.Length != 2 ||
            !string.Equals(arguments[0], MotionStopTrialVocabulary.ModeToken, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(arguments[1], "A", StringComparison.Ordinal))
        {
            axis = MotionStopAxis.A;
            return true;
        }

        if (string.Equals(arguments[1], "B", StringComparison.Ordinal))
        {
            axis = MotionStopAxis.B;
            return true;
        }

        return false;
    }

    private string ConsumeConsent()
    {
        string? supplied = null;
        var readSucceeded = true;
        var removeSucceeded = true;

        try
        {
            supplied = _readEnvironment(MotionStopTrialVocabulary.ConsentVariable);
        }
        catch (Exception)
        {
            readSucceeded = false;
        }

        try
        {
            _removeEnvironment(MotionStopTrialVocabulary.ConsentVariable);
        }
        catch (Exception)
        {
            removeSucceeded = false;
        }

        if (!readSucceeded || !removeSucceeded)
        {
            supplied = null;
            return MotionStopTrialVocabulary.OwnerConsentConsumptionFailed;
        }

        if (supplied is null)
        {
            return MotionStopTrialVocabulary.OwnerConsentNotConfigured;
        }

        var exactMatch = string.Equals(
            supplied,
            MotionStopTrialVocabulary.ExactConsentValue,
            StringComparison.Ordinal);
        supplied = null;
        return exactMatch
            ? MotionStopTrialVocabulary.OwnerConsentAccepted
            : MotionStopTrialVocabulary.OwnerConsentRejected;
    }

    private static string AxisToken(MotionStopAxis axis) =>
        axis == MotionStopAxis.A ? "A" : "B";
}
