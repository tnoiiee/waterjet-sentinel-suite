using Wjss.Contracts;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.CleaningPath.Composition;

// ACCELERATED-CLEANING-INTEGRATION-01: the typed, authorization-gated Process-Io equipment boundary.
//
// This adapter is the ONLY bridge between the existing Cleaning Path composition coordinator and the
// adopted Process-Io rows. It implements the existing ICleaningPathEquipmentBoundary contract, so
// the composition coordinator, its state machine, its evidence sequence and its Mandatory Safe
// Return handoff are all reused unchanged. No second state machine, no second I/O catalogue and no
// second topology authority is introduced here.
//
// Authority model
//   The adapter can only be constructed with a live ProcessIoDryCommandGrant. A grant is issued by
//   ProcessIoDryCommandGate, which refuses unless the run is explicitly in TEST_HARDWARE_DRY and the
//   Owner-local endpoint and consent are present. The adapter never creates a grant itself, never
//   falls back to a simulated transport, and never promotes to Production.
//
// Command model (exact Legacy authority)
//   OpenValveWC(n): IsOpen = true  -> FC05 WriteSingleCoil(coil 21+n, true)
//   CloseValveWC(n): IsOpen = false -> FC05 WriteSingleCoil(coil 21+n, false)
//   Maintained single-coil state, not a pulse. Safe cleanup writes false and retains it.
//
// Evidence model (three separate evidence kinds, never merged)
//   1. command acceptance   - the write was admitted and the coil value was sent
//   2. process-image read-back (FC01) - what the output image says; NEVER field confirmation
//   3. field feedback       - corrected Upper/open + Lower/closed limits and paired IV pressure
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).

/// <summary>
/// The single typed authorization artifact for dry command composition. It is created only by
/// <see cref="ProcessIoDryCommandGate"/>; the constructor is private so a caller can neither mint nor
/// forge one, and every instance records the exact profile and consent it was issued under.
/// </summary>
public sealed class ProcessIoDryCommandGrant
{
    private int _consumed;

    private ProcessIoDryCommandGrant(string consentPhrase, string endpointFingerprint, DateTimeOffset issuedAt)
    {
        ConsentPhrase = consentPhrase;
        EndpointFingerprint = endpointFingerprint;
        IssuedAt = issuedAt;
    }

    /// <summary>The exact consent phrase the grant was issued under (never a credential).</summary>
    public string ConsentPhrase { get; }

    /// <summary>A redacted fingerprint of the Owner-local endpoint. The endpoint itself is never stored.</summary>
    public string EndpointFingerprint { get; }

    public DateTimeOffset IssuedAt { get; }

    /// <summary>True until the grant has been consumed by one adapter instance.</summary>
    public bool IsLive => Volatile.Read(ref _consumed) == 0;

    /// <summary>Consumes the grant exactly once. A second adapter can never reuse it.</summary>
    internal bool TryConsume() => Interlocked.CompareExchange(ref _consumed, 1, 0) == 0;

    internal static ProcessIoDryCommandGrant Issue(string consentPhrase, string endpointFingerprint, DateTimeOffset issuedAt)
        => new ProcessIoDryCommandGrant(consentPhrase, endpointFingerprint, issuedAt);
}

/// <summary>
/// Fail-closed issuance and refusal vocabulary for dry command composition. Nothing here touches a
/// device: the gate decides authority only, and every refusal is a typed machine code.
/// </summary>
public static class ProcessIoDryCommandGate
{
    public const string RefusedProfileNotDry = "DRY_COMMAND_REFUSED_PROFILE_NOT_TEST_HARDWARE_DRY";
    public const string RefusedEndpointMissing = "DRY_COMMAND_REFUSED_ENDPOINT_MISSING";
    public const string RefusedConsentMissing = "DRY_COMMAND_REFUSED_CONSENT_MISSING";
    public const string RefusedConsentMismatch = "DRY_COMMAND_REFUSED_CONSENT_MISMATCH";
    public const string RefusedProductionRequested = "DRY_COMMAND_REFUSED_PRODUCTION_NOT_AUTHORIZED";
    public const string Granted = "DRY_COMMAND_GRANTED";

    /// <summary>
    /// Issues a one-use grant for dry command composition, or refuses with a typed code.
    ///
    /// The gate requires: the dry profile, a resolved Owner-local endpoint, and the exact dry consent
    /// phrase. It never substitutes a default endpoint, never accepts a PRODUCTION request, and never
    /// issues a grant to a caller that asked for physical command authority.
    /// </summary>
    public static bool TryIssue(
        DeviceProfile profile,
        bool dryCommandMode,
        string? ownerLocalEndpoint,
        string? suppliedConsent,
        DateTimeOffset at,
        out ProcessIoDryCommandGrant? grant,
        out string code)
    {
        grant = null;

        if (profile == DeviceProfile.PRODUCTION)
        {
            code = RefusedProductionRequested;
            return false;
        }

        if (profile != DeviceProfile.TEST_HARDWARE_DRY || !dryCommandMode)
        {
            code = RefusedProfileNotDry;
            return false;
        }

        if (string.IsNullOrWhiteSpace(ownerLocalEndpoint))
        {
            code = RefusedEndpointMissing;
            return false;
        }

        if (string.IsNullOrWhiteSpace(suppliedConsent))
        {
            code = RefusedConsentMissing;
            return false;
        }

        string expected = ProcessIoConsentPhrases.PhysicalDoSingle;
        if (!string.Equals(suppliedConsent, expected, StringComparison.Ordinal))
        {
            code = RefusedConsentMismatch;
            return false;
        }

        grant = ProcessIoDryCommandGrant.Issue(expected, Fingerprint(ownerLocalEndpoint), at);
        code = Granted;
        return true;
    }

    /// <summary>
    /// A stable, non-reversible redaction of the endpoint for provenance. Only the length and a short
    /// digest prefix are exposed, so a committed or logged report never carries the address.
    /// </summary>
    public static string Fingerprint(string endpoint)
    {
        byte[] digest = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(endpoint));
        return "endpoint-sha256-prefix:" + Convert.ToHexString(digest, 0, 6).ToLowerInvariant();
    }
}

/// <summary>One admitted command, with the exact coil value that was sent.</summary>
public sealed record ProcessIoValveCommandAdmission(
    string ValveId,
    CleaningPathValveCommandKind Kind,
    string CommandTag,
    int CoilAddress,
    bool CoilValue,
    DateTimeOffset At);

/// <summary>
/// Separated observation of one Isolation Valve. The three evidence kinds are kept in distinct
/// members on purpose: a caller can never mistake process-image read-back for field confirmation.
/// </summary>
public sealed record ProcessIoValveObservation
{
    /// <summary>FC05 command acceptance only.</summary>
    public required bool CommandAccepted { get; init; }

    /// <summary>FC01 output read-back: process image only, NEVER field confirmation.</summary>
    public bool? ProcessImageReadBack { get; init; }

    /// <summary>Corrected Upper limit, IVn OPEN-position field feedback.</summary>
    public required bool UpperLimitDetected { get; init; }

    /// <summary>Corrected Lower limit, IVn CLOSED-position field feedback.</summary>
    public required bool LowerLimitDetected { get; init; }

    /// <summary>Paired IVn outlet pressure observation (AI-004+ .. AI-011+).</summary>
    public PressureSample? OutletPressure { get; init; }

    /// <summary>
    /// True only when field evidence agrees with the commanded state. Process-image read-back is
    /// deliberately excluded from this decision.
    /// </summary>
    public required bool FieldConfirmed { get; init; }

    public required string AssessmentCode { get; init; }
}

/// <summary>
/// The typed Process-Io equipment boundary for one dry composition. Implements the existing
/// <see cref="ICleaningPathEquipmentBoundary"/> so the composition coordinator is reused unchanged.
/// </summary>
public sealed class ProcessIoEquipmentBoundary : ICleaningPathEquipmentBoundary
{
    private readonly IProcessIoTransport _transport;
    private readonly TimeProvider _time;
    private readonly ProcessIoDryCommandGrant _grant;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, bool> _commandedState = new();
    private int _writeAttempts;
    private string? _lastRefusal;

    private ProcessIoEquipmentBoundary(
        IProcessIoTransport transport,
        ProcessIoDryCommandGrant grant,
        TimeProvider time)
    {
        _transport = transport;
        _grant = grant;
        _time = time;
    }

    /// <summary>
    /// Creates the boundary, or refuses. The grant is consumed on success so no second boundary can
    /// be built from it; a refusal never touches the transport.
    /// </summary>
    public static bool TryCreate(
        IProcessIoTransport? transport,
        ProcessIoDryCommandGrant? grant,
        TimeProvider? time,
        out ProcessIoEquipmentBoundary? boundary,
        out string code)
    {
        boundary = null;

        if (transport is null)
        {
            code = ProcessIoDryCommandGate.RefusedEndpointMissing;
            return false;
        }

        if (transport is SimulatedProcessIoTransport)
        {
            // A dry command path must never be backed by the Simulation transport: that would make a
            // physical command appear to succeed with no device and no evidence.
            code = ProcessIoDryCommandGate.RefusedProfileNotDry;
            return false;
        }

        if (grant is null || !grant.IsLive)
        {
            code = ProcessIoDryCommandGate.RefusedConsentMissing;
            return false;
        }

        if (!grant.TryConsume())
        {
            code = ProcessIoDryCommandGate.RefusedConsentMismatch;
            return false;
        }

        boundary = new ProcessIoEquipmentBoundary(transport, grant, time ?? TimeProvider.System);
        code = ProcessIoDryCommandGate.Granted;
        return true;
    }

    /// <summary>Number of FC05 writes actually attempted. Used to prove zero writes on refusal.</summary>
    public int WriteAttempts => Volatile.Read(ref _writeAttempts);

    /// <summary>The last typed refusal code, or null.</summary>
    public string? LastRefusal => Volatile.Read(ref _lastRefusal);

    /// <summary>The maintained command state retained for safe cleanup (IVn -> open).</summary>
    public IReadOnlyDictionary<int, bool> CommandedState => _commandedState;

    /// <summary>Issues exactly one typed command for exactly one Isolation Valve.</summary>
    public void RequestValve(CleaningPathValveCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProcessIoIsolationValves.TryParseValveId(request.ValveId, out int ordinal))
        {
            // An unidentified valve is never resolved by ordinal guessing or by an alias.
            Volatile.Write(ref _lastRefusal, "IV_COMMAND_REFUSED_VALVE_IDENTITY_INVALID");
            return;
        }

        string tag = ProcessIoIsolationValves.CommandTag(ordinal);
        int coil = ProcessIoIsolationValves.CommandCoilAddress(ordinal);

        bool open = request.Kind == CleaningPathValveCommandKind.Open;
        if (request.Kind is not CleaningPathValveCommandKind.Open and not CleaningPathValveCommandKind.Close)
        {
            Volatile.Write(ref _lastRefusal, "IV_COMMAND_REFUSED_KIND_INVALID");
            return;
        }

        bool coilValue = ProcessIoIsolationValves.CoilValueFor(open);

        Interlocked.Increment(ref _writeAttempts);
        _transport.WriteCoil(ProcessIoDevices.RackT8, coil, coilValue);
        _commandedState[ordinal] = open;
        Volatile.Write(ref _lastRefusal, null);
        LastAdmission = new ProcessIoValveCommandAdmission(
            ProcessIoIsolationValves.ValveId(ordinal), request.Kind, tag, coil, coilValue, _time.GetUtcNow());
    }

    /// <summary>The most recent admitted command, or null.</summary>
    public ProcessIoValveCommandAdmission? LastAdmission { get; private set; }

    /// <summary>Reads the Pump Outlet Pressure (AI-003), the sole Pump-ready authority.</summary>
    public PressureSample? ReadPumpOutletPressure()
    {
        if (!TryReadRegister("AI-003+", out ushort raw))
        {
            return null;
        }

        double bar = ProcessIoEngineering.PumpOutletPressureBar(raw);
        return new PressureSample(bar, PressureQuality.GOOD, false, _time.GetUtcNow(), PressureSample.PumpOutletSource);
    }

    /// <summary>Reads one valve's outlet pressure (AI-004+ .. AI-011+).</summary>
    public PressureSample? ReadValveOutletPressure(string valveId)
    {
        if (!ProcessIoIsolationValves.TryParseValveId(valveId, out int ordinal))
        {
            return null;
        }

        if (!TryReadRegister(ProcessIoIsolationValves.OutletPressureTag(ordinal), out ushort raw))
        {
            return null;
        }

        double bar = ProcessIoEngineering.WcPressureBar(raw);
        return new PressureSample(
            bar, PressureQuality.GOOD, false, _time.GetUtcNow(),
            PressureSample.ValveOutletSource(ProcessIoIsolationValves.ValveId(ordinal)));
    }

    /// <summary>Corrected Upper limit: IVn OPEN-position feedback (WSB Upper Limit Switch #n).</summary>
    public bool ReadUpperLimitDetected(string valveId)
        => ProcessIoIsolationValves.TryParseValveId(valveId, out int ordinal)
           && TryReadDiscrete(ProcessIoIsolationValves.OpenFeedbackTag(ordinal));

    /// <summary>Corrected Lower limit: IVn CLOSED-position feedback (WSB Lower Limit Switch #n).</summary>
    public bool ReadLowerLimitDetected(string valveId)
        => ProcessIoIsolationValves.TryParseValveId(valveId, out int ordinal)
           && TryReadDiscrete(ProcessIoIsolationValves.ClosedFeedbackTag(ordinal));

    /// <summary>
    /// Composes the separated observation for one valve. Field confirmation requires the field
    /// evidence only: an OPEN command is confirmed by the Upper limit, a CLOSE command by the Lower
    /// limit. FC01 read-back is reported but never used to decide confirmation.
    /// </summary>
    public ProcessIoValveObservation Observe(string valveId)
    {
        if (!ProcessIoIsolationValves.TryParseValveId(valveId, out int ordinal))
        {
            return new ProcessIoValveObservation
            {
                CommandAccepted = false,
                UpperLimitDetected = false,
                LowerLimitDetected = false,
                FieldConfirmed = false,
                AssessmentCode = "IV_OBSERVATION_REFUSED_VALVE_IDENTITY_INVALID",
            };
        }

        bool upper = ReadUpperLimitDetected(valveId);
        bool lower = ReadLowerLimitDetected(valveId);
        PressureSample? pressure = ReadValveOutletPressure(valveId);

        bool hasCommand = _commandedState.TryGetValue(ordinal, out bool commandedOpen);
        bool? readBack = TryReadCoil(ProcessIoIsolationValves.CommandTag(ordinal), out bool coil) ? coil : null;

        bool confirmed = hasCommand && (commandedOpen
            ? upper && !lower
            : lower && !upper);

        if (!hasCommand)
        {
            return new ProcessIoValveObservation
            {
                CommandAccepted = false,
                ProcessImageReadBack = readBack,
                UpperLimitDetected = upper,
                LowerLimitDetected = lower,
                OutletPressure = pressure,
                FieldConfirmed = false,
                AssessmentCode = "IV_OBSERVATION_NO_COMMANDED_STATE",
            };
        }

        if (upper && lower)
        {
            // Contradictory field evidence is never resolved in favour of the command.
            return new ProcessIoValveObservation
            {
                CommandAccepted = true,
                ProcessImageReadBack = readBack,
                UpperLimitDetected = upper,
                LowerLimitDetected = lower,
                OutletPressure = pressure,
                FieldConfirmed = false,
                AssessmentCode = "IV_FEEDBACK_CONTRADICTORY_BOTH_LIMITS",
            };
        }

        return new ProcessIoValveObservation
        {
            CommandAccepted = true,
            ProcessImageReadBack = readBack,
            UpperLimitDetected = upper,
            LowerLimitDetected = lower,
            OutletPressure = pressure,
            FieldConfirmed = confirmed,
            AssessmentCode = confirmed
                ? (commandedOpen ? "IV_OPEN_FIELD_CONFIRMED" : "IV_CLOSED_FIELD_CONFIRMED")
                : "IV_FIELD_NOT_CONFIRMED",
        };
    }

    /// <summary>
    /// Safe cleanup: writes false to every valve this boundary ever commanded open and retains the
    /// de-energised CLOSED command state. Never throws, never touches a valve it did not command.
    /// </summary>
    public IReadOnlyList<string> SafeCleanupCloseAll()
    {
        var closed = new List<string>();
        foreach (KeyValuePair<int, bool> entry in _commandedState)
        {
            if (!entry.Value)
            {
                continue;
            }

            string tag = ProcessIoIsolationValves.CommandTag(entry.Key);
            if (!ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding))
            {
                continue;
            }

            Interlocked.Increment(ref _writeAttempts);
            _transport.WriteCoil(ProcessIoDevices.RackT8, binding.Address, false);
            _commandedState[entry.Key] = false;
            closed.Add(ProcessIoIsolationValves.ValveId(entry.Key));
        }

        return closed;
    }

    private bool TryReadRegister(string tag, out ushort raw)
    {
        raw = 0;
        if (!ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding))
        {
            return false;
        }

        raw = _transport.ReadInputRegister(ProcessIoDevices.RackT8, binding.Address);
        return true;
    }

    private bool TryReadDiscrete(string tag)
    {
        if (!ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding))
        {
            return false;
        }

        return _transport.ReadDiscreteInput(ProcessIoDevices.RackT8, binding.Address);
    }

    private bool TryReadCoil(string tag, out bool value)
    {
        value = false;
        if (!ProcessIoBindings.TryGet(tag, out ProcessIoPhysicalBinding binding))
        {
            return false;
        }

        value = _transport.ReadCoil(ProcessIoDevices.RackT8, binding.Address);
        return true;
    }
}
