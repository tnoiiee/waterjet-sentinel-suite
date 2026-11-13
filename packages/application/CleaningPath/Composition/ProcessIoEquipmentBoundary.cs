using Wjss.Contracts;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.CleaningPath.Composition;

// ACCELERATED-CLEANING-INTEGRATION-01: the typed Process-Io equipment boundary.
//
// This adapter is the ONLY bridge between the existing Cleaning Path composition coordinator and the
// adopted Process-Io rows. It implements the existing ICleaningPathEquipmentBoundary contract, so the
// composition coordinator, its state machine, its evidence sequence and its Mandatory Safe Return
// handoff are all reused unchanged. No second state machine, no second I/O catalogue and no second
// topology authority is introduced here.
//
// WRITE AUTHORITY
//   Every command goes through the existing ProcessIoSession lifecycle: AdmitWrite(row, authorization)
//   produces the admission token, and DispatchDigitalOutput(token, row, value) spends it exactly once.
//   This type does not mint tokens, does not own a generation and cannot write around the session.
//   There is no parallel grant authority: ProcessIoDryCommandGate returns the EXISTING
//   ProcessIoWriteAuthorization record, and the session remains the sole write-lifecycle authority.
//
// COMMAND MODEL (exact Legacy authority)
//   OpenValveWC(n):  IsOpen = true  -> FC05 WriteSingleCoil(coil 21+n, true)
//   CloseValveWC(n): IsOpen = false -> FC05 WriteSingleCoil(coil 21+n, false)
//   Maintained single-coil state, not a pulse. Safe cleanup writes false and retains it.
//
// EVIDENCE MODEL (three separate kinds, never merged)
//   1. command admission + dispatch  - typed ProcessIoCommandOutcome
//   2. FC01 process image read-back  - reported, NEVER field confirmation
//   3. field feedback                - corrected Upper/open + Lower/closed limits and paired IV pressure
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).

/// <summary>
/// Structured cleanup result. It records what was attempted and what is still unknown, so a cleanup
/// failure can never overwrite or hide the primary failure it ran after.
/// </summary>
public sealed record ProcessIoCleanupResult
{
    /// <summary>Valve ordinals this boundary attempted to close.</summary>
    public IReadOnlyList<string> AttemptedValveIds { get; init; } = Array.Empty<string>();

    /// <summary>Valves whose close command was admitted by the session.</summary>
    public IReadOnlyList<string> AdmittedValveIds { get; init; } = Array.Empty<string>();

    /// <summary>Valves whose de-energising close command was dispatched and returned.</summary>
    public IReadOnlyList<string> ClosedValveIds { get; init; } = Array.Empty<string>();

    /// <summary>Valves whose close was refused by the session, with the typed refusal code.</summary>
    public IReadOnlyDictionary<string, string> RefusedValves { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Valves whose close threw on the transport. The exception is captured, never propagated.</summary>
    public IReadOnlyDictionary<string, string> TransportFailures { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Valves whose final de-energised state could not be established. This is the honest "unknown"
    /// set: cleanup reports it rather than claiming success.
    /// </summary>
    public IReadOnlyList<string> RemainingUnknownState { get; init; } = Array.Empty<string>();

    /// <summary>True only when every attempted valve is known to be de-energised.</summary>
    public bool Complete =>
        TransportFailures.Count == 0
        && RefusedValves.Count == 0
        && RemainingUnknownState.Count == 0
        && ClosedValveIds.Count == AttemptedValveIds.Count;
}

/// <summary>
/// Fail-closed authorization for the dry command composition. It issues the EXISTING
/// <see cref="ProcessIoWriteAuthorization"/> record; it never creates a token, generation or session.
/// </summary>
public static class ProcessIoDryCommandGate
{
    public const string RefusedProfileNotDry = "DRY_COMMAND_REFUSED_PROFILE_NOT_TEST_HARDWARE_DRY";
    public const string RefusedEndpointMissing = "DRY_COMMAND_REFUSED_ENDPOINT_MISSING";
    public const string RefusedConsentMissing = "DRY_COMMAND_REFUSED_CONSENT_MISSING";
    public const string RefusedConsentMismatch = "DRY_COMMAND_REFUSED_CONSENT_MISMATCH";
    public const string RefusedProductionRequested = "DRY_COMMAND_REFUSED_PRODUCTION_NOT_AUTHORIZED";
    public const string RefusedSessionNotOpen = "DRY_COMMAND_REFUSED_SESSION_NOT_OPEN";

    /// <summary>Refused because the simulated Pump-ready input was not explicitly selected.</summary>
    public const string RefusedSimulatedInputNotSelected = "DRY_COMMAND_REFUSED_SIMULATED_INPUT_NOT_SELECTED";

    /// <summary>Refused because the Owner Pump-stopped attestation was absent or not the exact literal.</summary>
    public const string RefusedPumpStoppedAttestationMissing = "DRY_COMMAND_REFUSED_PUMP_STOPPED_ATTESTATION_MISSING";

    public const string Granted = "DRY_COMMAND_AUTHORIZED";

    /// <summary>
    /// Issues a write authorization for the dry profile, or refuses with a typed code. The gate
    /// requires the dry profile, a resolved Owner-local endpoint and the exact dry consent phrase.
    /// It never substitutes a default endpoint and never authorizes PRODUCTION.
    /// </summary>
    public static bool TryAuthorize(
        DeviceProfile profile,
        bool dryCommandMode,
        string? ownerLocalEndpoint,
        string? suppliedConsent,
        out ProcessIoWriteAuthorization? authorization,
        out string code)
    {
        authorization = null;

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

        if (!string.Equals(suppliedConsent, ProcessIoConsentPhrases.PhysicalDoSingle, StringComparison.Ordinal))
        {
            code = RefusedConsentMismatch;
            return false;
        }

        authorization = new ProcessIoWriteAuthorization
        {
            PhysicalWriteAuthorized = true,
            Basis = $"TEST_HARDWARE_DRY/{EndpointFingerprint(ownerLocalEndpoint)}",
        };
        code = Granted;
        return true;
    }

    /// <summary>
    /// Issues a write authorization for the isolated IV Hardware trial, or refuses with a typed code.
    ///
    /// This is the same gate, and the same authorization type, as the integrated dry composition: it
    /// exists here so exactly one place in the Product mints a write authorization. It differs in one
    /// respect only, which the caller must prove before calling it: the IV trial is selected by its
    /// own explicit trial selector and carries the Owner Pump-stopped attestation, because the
    /// integrated dry coverage variable is deliberately NOT set for that trial.
    ///
    /// It never authorizes PRODUCTION, never authorizes a non-dry profile, never substitutes an
    /// endpoint and never accepts a missing consent or attestation.
    /// </summary>
    public static bool TryAuthorizeIvTrial(
        DeviceProfile profile,
        bool simulatedAi003Selected,
        string? ownerLocalEndpoint,
        string? suppliedConsent,
        bool pumpStoppedAttested,
        out ProcessIoWriteAuthorization? authorization,
        out string code)
    {
        authorization = null;

        if (profile == DeviceProfile.PRODUCTION)
        {
            code = RefusedProductionRequested;
            return false;
        }

        if (profile != DeviceProfile.TEST_HARDWARE_DRY)
        {
            code = RefusedProfileNotDry;
            return false;
        }

        // The simulated Pump-ready input is only ever authorized as an explicitly selected trial input.
        if (!simulatedAi003Selected)
        {
            code = RefusedSimulatedInputNotSelected;
            return false;
        }

        if (!pumpStoppedAttested)
        {
            code = RefusedPumpStoppedAttestationMissing;
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

        if (!string.Equals(suppliedConsent, ProcessIoConsentPhrases.PhysicalDoSingle, StringComparison.Ordinal))
        {
            code = RefusedConsentMismatch;
            return false;
        }

        authorization = new ProcessIoWriteAuthorization
        {
            PhysicalWriteAuthorized = true,
            Basis = $"TEST_HARDWARE_DRY/IV_TRIAL/{EndpointFingerprint(ownerLocalEndpoint)}",
        };
        code = Granted;
        return true;
    }

    /// <summary>
    /// Stable, non-reversible redaction of the endpoint for provenance. The endpoint itself is never
    /// stored, logged or committed; only a digest prefix is exposed.
    /// </summary>
    public static string EndpointFingerprint(string endpoint)
    {
        byte[] digest = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(endpoint));
        return "endpoint-sha256-prefix:" + Convert.ToHexString(digest, 0, 6).ToLowerInvariant();
    }
}

/// <summary>
/// The typed Process-Io equipment boundary for one dry composition. Reads and writes the adopted
/// catalogue rows exclusively through <see cref="ProcessIoSession"/>.
/// </summary>
public sealed class ProcessIoEquipmentBoundary : ICleaningPathEquipmentBoundary
{
    private readonly ProcessIoSession _session;
    private readonly IReadOnlyDictionary<string, ProcessIoCatalogueRow> _catalogue;
    private readonly ProcessIoWriteAuthorization _authorization;
    private readonly TimeProvider _time;
    private readonly TimeSpan _freshnessBudget;
    private readonly Dictionary<int, ProcessIoCommandOutcome> _commands = new();
    private readonly List<ProcessIoCommandOutcome> _commandLog = new();

    public ProcessIoEquipmentBoundary(
        ProcessIoSession session,
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        ProcessIoWriteAuthorization authorization,
        TimeProvider? time = null,
        TimeSpan? freshnessBudget = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(authorization);

        _session = session;
        _catalogue = catalogue;
        _authorization = authorization;
        _time = time ?? TimeProvider.System;
        _freshnessBudget = freshnessBudget ?? DefaultFreshnessBudget;
    }

    /// <summary>Default freshness budget for one observation. Development value, not certified.</summary>
    public static readonly TimeSpan DefaultFreshnessBudget = TimeSpan.FromSeconds(2);

    /// <summary>The session this boundary reads and writes through.</summary>
    public ProcessIoSession Session => _session;

    /// <summary>Every command outcome in issue order, for evidence.</summary>
    public IReadOnlyList<ProcessIoCommandOutcome> CommandLog => _commandLog;

    // ---------------------------------------------------------------- commands

    /// <summary>Issues exactly one typed command for exactly one Isolation Valve.</summary>
    public void RequestValve(CleaningPathValveCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProcessIoIsolationValves.TryParseValveId(request.ValveId, out int ordinal))
        {
            // An unidentified valve is never resolved by ordinal guessing, alias or fallback.
            _commandLog.Add(Unresolved(request.ValveId, "IV_COMMAND_REFUSED_VALVE_IDENTITY_INVALID"));
            return;
        }

        bool open = request.Kind == CleaningPathValveCommandKind.Open;
        bool close = request.Kind == CleaningPathValveCommandKind.Close;
        if (!open && !close)
        {
            _commandLog.Add(Unresolved(request.ValveId, "IV_COMMAND_REFUSED_KIND_INVALID"));
            return;
        }

        string tag = ProcessIoIsolationValves.CommandTag(ordinal);
        if (!_catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row) || row.Binding is null)
        {
            _commandLog.Add(Unresolved(request.ValveId, ProcessIoRefusals.WriteRefusedUnbound));
            return;
        }

        ProcessIoAdmission admission = _session.AdmitWrite(_time.GetUtcNow(), row, _authorization);
        if (!admission.Admitted || string.IsNullOrEmpty(admission.Token))
        {
            _commandLog.Add(Outcome(request.ValveId, row, open, dispatched: false,
                admission.Code, "NOT_DISPATCHED", faulted: false, generation: _session.Generation));
            return;
        }

        bool coilValue = ProcessIoIsolationValves.CoilValueFor(open);
        bool dispatched;
        bool faulted = false;
        string dispatchCode;

        try
        {
            dispatched = _session.DispatchDigitalOutput(_time.GetUtcNow(), admission.Token, row, coilValue);
            dispatchCode = dispatched ? "DISPATCHED" : "DISPATCH_REFUSED";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // The token is already spent inside the session, so exactly-once is preserved even here.
            dispatched = false;
            faulted = true;
            dispatchCode = ProcessIoRefusals.ObservationTransportFault;
        }

        ProcessIoCommandOutcome outcome = Outcome(
            request.ValveId, row, open, dispatched, admission.Code, dispatchCode, faulted, _session.Generation);
        _commandLog.Add(outcome);
        _commands[ordinal] = outcome;
    }

    private ProcessIoCommandOutcome Unresolved(string valveId, string code) => new()
    {
        ValveId = valveId,
        Tag = string.Empty,
        CoilAddress = -1,
        CoilValue = false,
        Dispatched = false,
        AdmissionCode = code,
        DispatchCode = "NOT_DISPATCHED",
        SessionGeneration = _session.Generation,
        At = _time.GetUtcNow(),
    };

    private ProcessIoCommandOutcome Outcome(
        string valveId,
        ProcessIoCatalogueRow row,
        bool open,
        bool dispatched,
        string admissionCode,
        string dispatchCode,
        bool faulted,
        int generation) => new()
    {
        ValveId = valveId,
        Tag = row.Tag,
        CoilAddress = row.Binding?.Address ?? -1,
        CoilValue = ProcessIoIsolationValves.CoilValueFor(open),
        Dispatched = dispatched,
        AdmissionCode = admissionCode,
        DispatchCode = dispatchCode,
        SessionGeneration = generation,
        At = _time.GetUtcNow(),
        TransportFaulted = faulted,
    };

    // ---------------------------------------------------------------- observations

    /// <summary>
    /// Reads the Pump Outlet Pressure (AI-003), the sole Pump-ready authority.
    ///
    /// Returns null unless the value is a usable PHYSICAL observation from the live generation. The
    /// null is what makes the Pump-ready gate refuse: this method never fabricates a GOOD sample, and
    /// it never falls back to AI-002 or to a simulated value.
    /// </summary>
    public PressureSample? ReadPumpOutletPressure()
    {
        ProcessIoPhysicalObservation observation = Observe(ProcessIoIsolationValves.PumpOutletPressureTag);
        if (!Accepts(observation)) return null;

        return new PressureSample(
            observation.EngineeringValue,
            PressureQuality.GOOD,
            Stale: false,
            observation.ObservedAt.ToUniversalTime(),
            PressureSample.PumpOutletSource);
    }

    /// <summary>Reads one valve's outlet pressure (AI-004+ .. AI-011+), under the same rules.</summary>
    public PressureSample? ReadValveOutletPressure(string valveId)
    {
        if (!ProcessIoIsolationValves.TryParseValveId(valveId, out int ordinal)) return null;

        ProcessIoPhysicalObservation observation =
            Observe(ProcessIoIsolationValves.OutletPressureTag(ordinal));
        if (!Accepts(observation)) return null;

        return new PressureSample(
            observation.EngineeringValue,
            PressureQuality.GOOD,
            Stale: false,
            observation.ObservedAt.ToUniversalTime(),
            PressureSample.ValveOutletSource(ProcessIoIsolationValves.ValveId(ordinal)));
    }

    /// <summary>Corrected Upper limit: IVn OPEN-position feedback (WSB Upper Limit Switch #n).</summary>
    public bool ReadUpperLimitDetected(string valveId)
        => ProcessIoIsolationValves.TryParseValveId(valveId, out int ordinal)
           && ReadLimit(ProcessIoIsolationValves.OpenFeedbackTag(ordinal));

    /// <summary>Corrected Lower limit: IVn CLOSED-position feedback (WSB Lower Limit Switch #n).</summary>
    public bool ReadLowerLimitDetected(string valveId)
        => ProcessIoIsolationValves.TryParseValveId(valveId, out int ordinal)
           && ReadLimit(ProcessIoIsolationValves.ClosedFeedbackTag(ordinal));

    /// <summary>A limit bit counts only when the observation is usable; anything else reads false.</summary>
    private bool ReadLimit(string tag)
    {
        ProcessIoPhysicalObservation observation = Observe(tag);
        return Accepts(observation) && observation.RawValue == 1;
    }

    /// <summary>
    /// Observes one catalogue row through the session.
    ///
    /// The clock is handed to the session rather than sampled here. C# evaluates arguments before the
    /// call, so any receive instant captured in this method would precede the transport read, and a slow
    /// read would be recorded with identical timestamps and accepted as current — including the Pump
    /// Outlet pressure, every IV outlet pressure, and the Upper and Lower field limits that decide field
    /// confirmation. The session brackets the read with this same clock instead: observed before the
    /// read, received after it. The timestamps are never adjusted afterwards.
    /// </summary>
    public ProcessIoPhysicalObservation Observe(string tag)
    {
        _catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row);
        return IsAnalogTag(tag)
            ? _session.ObserveAnalogInput(tag, row, _time, _freshnessBudget)
            : _session.ObserveDigitalInput(tag, row, _time, _freshnessBudget);
    }

    /// <summary>
    /// The single acceptance rule for process evidence. A simulated read is never acceptable here,
    /// because a dry command path must never present in-memory state as physical evidence.
    /// </summary>
    private bool Accepts(ProcessIoPhysicalObservation observation) =>
        ProcessIoEvidenceAcceptance.Accepts(
            observation, _session.Generation, _time.GetUtcNow(), _freshnessBudget);

    private static bool IsAnalogTag(string tag) =>
        tag.StartsWith("AI-", StringComparison.Ordinal);

    // ---------------------------------------------------------------- separated observation

    /// <summary>
    /// The separated observation for one valve: command outcome, FC01 process image (reported only)
    /// and the two field limits. Field confirmation is derived from the field limits only.
    /// </summary>
    public ProcessIoValveObservation ObserveValve(string valveId)
    {
        if (!ProcessIoIsolationValves.TryParseValveId(valveId, out int ordinal))
        {
            return new ProcessIoValveObservation
            {
                CommandDispatched = false,
                UpperLimitDetected = false,
                LowerLimitDetected = false,
                FieldConfirmed = false,
                AssessmentCode = "IV_OBSERVATION_REFUSED_VALVE_IDENTITY_INVALID",
            };
        }

        bool upper = ReadUpperLimitDetected(valveId);
        bool lower = ReadLowerLimitDetected(valveId);
        PressureSample? pressure = ReadValveOutletPressure(valveId);

        bool hasCommand = _commands.TryGetValue(ordinal, out ProcessIoCommandOutcome? command);
        bool? processImage = ReadProcessImage(ordinal);

        if (!hasCommand || command is null || !command.Dispatched)
        {
            return new ProcessIoValveObservation
            {
                CommandDispatched = false,
                ProcessImageReadBack = processImage,
                UpperLimitDetected = upper,
                LowerLimitDetected = lower,
                OutletPressure = pressure,
                FieldConfirmed = false,
                AssessmentCode = "IV_OBSERVATION_NO_DISPATCHED_COMMAND",
            };
        }

        if (upper && lower)
        {
            return new ProcessIoValveObservation
            {
                CommandDispatched = true,
                ProcessImageReadBack = processImage,
                UpperLimitDetected = upper,
                LowerLimitDetected = lower,
                OutletPressure = pressure,
                FieldConfirmed = false,
                AssessmentCode = "IV_FEEDBACK_CONTRADICTORY_BOTH_LIMITS",
            };
        }

        bool commandedOpen = command.CoilValue;
        bool confirmed = commandedOpen ? upper && !lower : lower && !upper;

        return new ProcessIoValveObservation
        {
            CommandDispatched = true,
            ProcessImageReadBack = processImage,
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
    /// FC01 output read-back. Deliberately surfaced as its own value and never folded into
    /// FieldConfirmed: the process image is what the output module believes, not what the valve did.
    /// </summary>
    private bool? ReadProcessImage(int ordinal)
    {
        string tag = ProcessIoIsolationValves.CommandTag(ordinal);
        if (!_catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row) || row.Binding is null)
        {
            return null;
        }

        ProcessIoPhysicalObservation observation = Observe(tag);
        return Accepts(observation) ? observation.RawValue == 1 : null;
    }

    // ---------------------------------------------------------------- cleanup

    /// <summary>
    /// Safe cleanup: writes the CLOSED command (false) to every valve this boundary dispatched OPEN.
    ///
    /// Every transport and session failure is captured and represented in the returned record, so the
    /// caller can keep the primary failure separate. This method does not throw for transport faults;
    /// it reports them.
    /// </summary>
    public ProcessIoCleanupResult SafeCleanupCloseAll()
    {
        var attempted = new List<string>();
        var admitted = new List<string>();
        var closed = new List<string>();
        var refused = new Dictionary<string, string>(StringComparer.Ordinal);
        var failed = new Dictionary<string, string>(StringComparer.Ordinal);
        var unknown = new List<string>();

        var openOrdinals = _commands
            .Where(entry => entry.Value.Dispatched && entry.Value.CoilValue)
            .Select(entry => entry.Key)
            .OrderBy(ordinal => ordinal)
            .ToArray();

        foreach (int ordinal in openOrdinals)
        {
            string valveId = ProcessIoIsolationValves.ValveId(ordinal);
            attempted.Add(valveId);

            string tag = ProcessIoIsolationValves.CommandTag(ordinal);
            if (!_catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row) || row.Binding is null)
            {
                refused[valveId] = ProcessIoRefusals.WriteRefusedUnbound;
                unknown.Add(valveId);
                continue;
            }

            ProcessIoAdmission admission;
            try
            {
                admission = _session.AdmitWrite(_time.GetUtcNow(), row, _authorization);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                refused[valveId] = ProcessIoRefusals.SessionNotOpen;
                unknown.Add(valveId);
                continue;
            }

            if (!admission.Admitted || string.IsNullOrEmpty(admission.Token))
            {
                refused[valveId] = admission.Code;
                unknown.Add(valveId);
                continue;
            }

            admitted.Add(valveId);

            try
            {
                if (_session.DispatchDigitalOutput(_time.GetUtcNow(), admission.Token, row, false))
                {
                    closed.Add(valveId);
                    _commands[ordinal] = _commands[ordinal] with { CoilValue = false };
                }
                else
                {
                    refused[valveId] = "DISPATCH_REFUSED";
                    unknown.Add(valveId);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                failed[valveId] = ex.GetType().Name;
                unknown.Add(valveId);
            }
        }

        return new ProcessIoCleanupResult
        {
            AttemptedValveIds = attempted,
            AdmittedValveIds = admitted,
            ClosedValveIds = closed,
            RefusedValves = refused,
            TransportFailures = failed,
            RemainingUnknownState = unknown,
        };
    }
}

/// <summary>
/// Separated observation of one Isolation Valve. The three evidence kinds occupy distinct members on
/// purpose, so no caller can mistake process-image read-back for field confirmation.
/// </summary>
public sealed record ProcessIoValveObservation
{
    /// <summary>True only when the session admitted AND dispatched the command coil write.</summary>
    public required bool CommandDispatched { get; init; }

    /// <summary>FC01 output read-back: process image only, never field confirmation.</summary>
    public bool? ProcessImageReadBack { get; init; }

    /// <summary>Corrected Upper limit, IVn OPEN-position field feedback.</summary>
    public required bool UpperLimitDetected { get; init; }

    /// <summary>Corrected Lower limit, IVn CLOSED-position field feedback.</summary>
    public required bool LowerLimitDetected { get; init; }

    /// <summary>Paired IVn outlet pressure (AI-004+ .. AI-011+), or null when unusable.</summary>
    public PressureSample? OutletPressure { get; init; }

    /// <summary>True only when the field limits agree with the dispatched command.</summary>
    public required bool FieldConfirmed { get; init; }

    public required string AssessmentCode { get; init; }
}
