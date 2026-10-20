namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The only production entry to the hardware-capable WSB1 Manual Motion Commissioning path.
///
/// Order, fail-closed at every step and before any native load:
///   1. consume the exact-match Owner-local consent value (read, then removed from this process);
///   2. resolve the existing Owner-local native-library configuration (the Gate A variables);
///   3. resolve the existing Owner-local controller endpoint;
///   4. create one controller owner. The owner proves Gate A before it loads, opens or binds anything.
///
/// The consent value is an explicit local confirmation, not a credential; it is never printed, logged
/// or stored. No value, path, hash, endpoint or version string leaves this type: only canonical codes.
/// Building, testing or merging this source authorizes no hardware use; a separate Owner Scope Gate
/// governs the first hardware commissioning session.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal static class ManualMotionControllerInvocation
{
    /// <summary>Creates the owner from the process environment, consuming the consent value.</summary>
    internal static bool TryCreateFromProcessEnvironment(out ManualMotionControllerOwner? owner, out string refusalCode) =>
        TryCreate(
            Environment.GetEnvironmentVariable,
            name => Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process),
            ManualMotionControllerOwner.CreateProduction,
            out owner,
            out refusalCode);

    /// <summary>Fake seam for focused tests; production has no caller-supplied native options.</summary>
    internal static bool TryCreate(
        Func<string, string?> readEnvironment,
        Action<string> removeEnvironment,
        Func<MotionLibraryProbeConfiguration, MotionControllerEndpointConfiguration, ManualMotionControllerOwner> ownerFactory,
        out ManualMotionControllerOwner? owner,
        out string refusalCode)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(removeEnvironment);
        ArgumentNullException.ThrowIfNull(ownerFactory);

        owner = null;
        var consentCode = ConsumeConsent(readEnvironment, removeEnvironment);
        if (!string.Equals(consentCode, ManualMotionControllerCodes.ConsentAccepted, StringComparison.Ordinal))
        {
            refusalCode = consentCode;
            return false;
        }

        MotionLibraryProbeConfiguration nativeConfiguration;
        try
        {
            if (!MotionLibraryConfigurationResolver.TryResolve(readEnvironment, out nativeConfiguration, out var nativeRefusal, out _))
            {
                refusalCode = nativeRefusal.Length == 0 ? ManualMotionControllerCodes.PreconditionEvaluationFailed : nativeRefusal;
                return false;
            }
        }
        catch (Exception)
        {
            refusalCode = ManualMotionControllerCodes.PreconditionEvaluationFailed;
            return false;
        }

        MotionControllerEndpointConfiguration endpointConfiguration;
        try
        {
            if (!MotionControllerEndpointResolver.TryResolve(readEnvironment, out endpointConfiguration, out var endpointRefusal, out _))
            {
                refusalCode = endpointRefusal.Length == 0 ? MotionControllerIdentityVocabulary.EndpointRejected : endpointRefusal;
                return false;
            }
        }
        catch (Exception)
        {
            refusalCode = MotionControllerIdentityVocabulary.EndpointRejected;
            return false;
        }

        owner = ownerFactory(nativeConfiguration, endpointConfiguration);
        refusalCode = string.Empty;
        return true;
    }

    private static string ConsumeConsent(Func<string, string?> readEnvironment, Action<string> removeEnvironment)
    {
        string? supplied = null;
        var readSucceeded = true;
        var removeSucceeded = true;

        try
        {
            supplied = readEnvironment(ManualMotionControllerCodes.ConsentVariable);
        }
        catch (Exception)
        {
            readSucceeded = false;
        }

        try
        {
            removeEnvironment(ManualMotionControllerCodes.ConsentVariable);
        }
        catch (Exception)
        {
            removeSucceeded = false;
        }

        if (!readSucceeded || !removeSucceeded)
        {
            return ManualMotionControllerCodes.ConsentConsumptionFailed;
        }

        if (supplied is null)
        {
            return ManualMotionControllerCodes.ConsentNotConfigured;
        }

        return string.Equals(supplied, ManualMotionControllerCodes.ExactConsentValue, StringComparison.Ordinal)
            ? ManualMotionControllerCodes.ConsentAccepted
            : ManualMotionControllerCodes.ConsentRejected;
    }
}
