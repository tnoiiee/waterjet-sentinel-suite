using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.ProcessIo;

// PUMP-COMMAND-PRODUCT-INTEGRATION: the typed Pump command boundary.
//
// This is the ONLY bridge between the Pump lifecycle and the adopted Process-Io rows. It adds no
// second catalogue, no second binding table and no second write lifecycle:
//
//   admission  ProcessIoSession.AdmitWrite(row, authorization) -> token
//   dispatch   ProcessIoSession.DispatchDigitalOutput / DispatchAnalogOutput(token, row, value)
//   evidence   ProcessIoSession.ObserveDigitalInput / ObserveAnalogInput
//
// The boundary cannot mint a token, cannot own a generation and cannot write around the session.
// Every command carries the tag, the DERIVED adopted address and the admission/dispatch codes, so a
// caller can never mistake a dispatched command for a confirmed physical effect.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

/// <summary>
/// One admitted Pump command. It reports admission and dispatch separately and never claims that a
/// field device moved.
/// </summary>
public sealed record PumpCommandOutcome
{
    public required string Tag { get; init; }

    /// <summary>The adopted write address, derived from the single binding table.</summary>
    public required int Address { get; init; }

    /// <summary>The maintained value requested. There is no pulse, so there is no duration.</summary>
    public required bool RequestedValue { get; init; }

    /// <summary>True only when the session admitted the write.</summary>
    public required bool Admitted { get; init; }

    /// <summary>True only when admission AND dispatch both succeeded.</summary>
    public required bool Dispatched { get; init; }

    public required string AdmissionCode { get; init; }
    public required string DispatchCode { get; init; }
    public required int SessionGeneration { get; init; }
    public required DateTimeOffset At { get; init; }

    /// <summary>True when the transport threw during dispatch; the exception is captured, never propagated.</summary>
    public bool TransportFaulted { get; init; }

    /// <summary>True only for a fully successful command.</summary>
    public bool Succeeded => Admitted && Dispatched && !TransportFaulted;
}

/// <summary>
/// One analog speed-reference outcome. The engineering command is milliamps, as commissioned; the raw
/// word is reported as the conversion produced it.
/// </summary>
public sealed record PumpAnalogCommandOutcome
{
    public required string Tag { get; init; }
    public required int Address { get; init; }
    public required double RequestedEngineering { get; init; }
    public required bool Admitted { get; init; }
    public required bool Dispatched { get; init; }
    public required string AdmissionCode { get; init; }
    public required string DispatchCode { get; init; }
    public required int SessionGeneration { get; init; }
    public required DateTimeOffset At { get; init; }
    public bool TransportFaulted { get; init; }
    public bool Succeeded => Admitted && Dispatched && !TransportFaulted;
}

/// <summary>
/// One interpreted digital feedback read. <see cref="Commissioned"/> false means the configured
/// polarity has not been established, so <see cref="Active"/> carries no meaning and must not be used.
/// </summary>
public sealed record PumpFeedbackRead
{
    public required string Tag { get; init; }
    public required bool Commissioned { get; init; }
    public required string Code { get; init; }

    /// <summary>The raw bit the transport returned. Meaningful only when the read was usable.</summary>
    public required bool RawBit { get; init; }

    /// <summary>The interpreted physical condition. Always false while uncommissioned.</summary>
    public required bool Active { get; init; }

    /// <summary>True when the observation was usable, current and from the live generation.</summary>
    public required bool Current { get; init; }

    public required int SessionGeneration { get; init; }
    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// One interpreted Pump Outlet pressure read. <see cref="Accepted"/> false means the observation may
/// not be used as Pump-ready evidence; <see cref="Quality"/> says why, so the lifecycle can keep the
/// refusal reasons distinct instead of collapsing them into "no value".
/// </summary>
public sealed record PumpPressureRead
{
    public required string Tag { get; init; }

    /// <summary>True only when the observation was usable, PHYSICAL_READ, current and in generation.</summary>
    public required bool Accepted { get; init; }

    /// <summary>"OK" when accepted; the observation diagnostic code otherwise.</summary>
    public required string Code { get; init; }

    /// <summary>The session quality verdict. Null when no observation was produced at all.</summary>
    public ProcessIoObservationQuality? Quality { get; init; }

    /// <summary>The typed sample, present only when accepted.</summary>
    public PressureSample? Sample { get; init; }

    public required int SessionGeneration { get; init; }
    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// The typed Pump command boundary the lifecycle consumes. It exposes four command groups, two
/// feedback inputs and one pressure observation. It exposes no transport, no raw word writer and no
/// arbitrary tag command.
/// </summary>
public interface IPumpCommandBoundary
{
    /// <summary>The commissioned feedback polarity the boundary interprets with.</summary>
    PumpFeedbackConfiguration Feedback { get; }

    /// <summary>The live session generation every observation must belong to.</summary>
    int SessionGeneration { get; }

    /// <summary>Energises one Pump selection and de-energises the other, in the commissioned order.</summary>
    IReadOnlyList<PumpCommandOutcome> SelectPump(int selection);

    /// <summary>Writes the maintained Enable Inverter output.</summary>
    PumpCommandOutcome SetEnableInverter(bool value);

    /// <summary>Writes the maintained Run Pump output.</summary>
    PumpCommandOutcome SetRunPump(bool value);

    /// <summary>Writes the analog speed reference in engineering milliamps.</summary>
    PumpAnalogCommandOutcome SetSpeedCommand(double engineeringMilliamps);

    /// <summary>Reads the Running input interpreted through the commissioned polarity.</summary>
    PumpFeedbackRead ReadRunning();

    /// <summary>Reads the Fault input interpreted through the commissioned polarity.</summary>
    PumpFeedbackRead ReadFault();

    /// <summary>
    /// Reads the Pump Outlet pressure as ready evidence. A refusal never fabricates a sample and never
    /// falls back to the Pump Inlet row.
    /// </summary>
    PumpPressureRead ReadPumpOutletPressure();
}

/// <summary>
/// The single acceptance rule for process evidence. One definition, used by the Pump boundary and by
/// the Cleaning Path equipment boundary, so the two can never disagree about what counts as current.
/// A simulated read is never acceptable: a dry command path must never present in-memory state as
/// physical evidence.
/// </summary>
public static class ProcessIoEvidenceAcceptance
{
    public static bool Accepts(
        ProcessIoPhysicalObservation? observation,
        int liveGeneration,
        DateTimeOffset now,
        TimeSpan freshnessBudget) =>
        observation is not null
        && observation.IsUsable
        && observation.Provenance == ProcessIoObservationProvenance.PHYSICAL_READ
        && observation.SourceGeneration == liveGeneration
        && observation.IsUsableAt(now, liveGeneration, freshnessBudget);
}

/// <summary>
/// The Process-Io implementation of <see cref="IPumpCommandBoundary"/>.
/// </summary>
public sealed class ProcessIoPumpCommandBoundary : IPumpCommandBoundary
{
    private readonly ProcessIoSession _session;
    private readonly IReadOnlyDictionary<string, ProcessIoCatalogueRow> _catalogue;
    private readonly ProcessIoWriteAuthorization _authorization;
    private readonly TimeProvider _time;
    private readonly TimeSpan _freshnessBudget;
    private readonly List<PumpCommandOutcome> _commandLog = new();

    public ProcessIoPumpCommandBoundary(
        ProcessIoSession session,
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        ProcessIoWriteAuthorization authorization,
        PumpFeedbackConfiguration feedback,
        TimeProvider? time = null,
        TimeSpan? freshnessBudget = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(feedback);

        _session = session;
        _catalogue = catalogue;
        _authorization = authorization;
        Feedback = feedback;
        _time = time ?? TimeProvider.System;
        _freshnessBudget = freshnessBudget ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>Every command outcome in issue order, for evidence.</summary>
    public IReadOnlyList<PumpCommandOutcome> CommandLog => _commandLog;

    public PumpFeedbackConfiguration Feedback { get; }

    public int SessionGeneration => _session.Generation;

    // ---------------------------------------------------------------- commands

    public IReadOnlyList<PumpCommandOutcome> SelectPump(int selection)
    {
        if (!PumpCommandContract.IsValidSelection(selection))
        {
            // An invalid selection is never resolved by falling back to Pump 1.
            return Array.Empty<PumpCommandOutcome>();
        }

        var outcomes = new List<PumpCommandOutcome>(2);
        foreach (PumpCommandStep step in PumpCommandContract.SelectionSequence(selection))
        {
            outcomes.Add(WriteCoil(step.Tag, step.Value));
        }

        return outcomes;
    }

    public PumpCommandOutcome SetEnableInverter(bool value) =>
        WriteCoil(PumpCommandContract.EnableInverterTag, value);

    public PumpCommandOutcome SetRunPump(bool value) =>
        WriteCoil(PumpCommandContract.RunPumpTag, value);

    /// <summary>Writes the analog speed reference. An out-of-domain value is refused, never clamped.</summary>
    public PumpAnalogCommandOutcome SetSpeedCommand(double engineeringMilliamps)
    {
        DateTimeOffset at = _time.GetUtcNow();
        int address = 0;
        bool bound = PumpCommandContract.TryRegisterAddress(PumpCommandContract.SpeedReferenceTag, out address);

        if (!bound)
        {
            return new PumpAnalogCommandOutcome
            {
                Tag = PumpCommandContract.SpeedReferenceTag,
                Address = 0,
                RequestedEngineering = engineeringMilliamps,
                Admitted = false,
                Dispatched = false,
                AdmissionCode = PumpCommandRefusals.RefusedWrongFunctionOrAddress,
                DispatchCode = PumpCommandRefusals.RefusedWrongFunctionOrAddress,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        if (!PumpAnalogCommandContract.IsInDomain(engineeringMilliamps))
        {
            return new PumpAnalogCommandOutcome
            {
                Tag = PumpCommandContract.SpeedReferenceTag,
                Address = address,
                RequestedEngineering = engineeringMilliamps,
                Admitted = false,
                Dispatched = false,
                AdmissionCode = PumpCommandRefusals.RefusedAnalogOutOfDomain,
                DispatchCode = PumpCommandRefusals.RefusedAnalogOutOfDomain,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        if (!_catalogue.TryGetValue(PumpCommandContract.SpeedReferenceTag, out ProcessIoCatalogueRow? row) || row.Binding is null)
        {
            return new PumpAnalogCommandOutcome
            {
                Tag = PumpCommandContract.SpeedReferenceTag,
                Address = address,
                RequestedEngineering = engineeringMilliamps,
                Admitted = false,
                Dispatched = false,
                AdmissionCode = ProcessIoRefusals.WriteRefusedUnbound,
                DispatchCode = ProcessIoRefusals.WriteRefusedUnbound,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        ProcessIoAdmission admission = _session.AdmitWrite(at, row, _authorization);
        if (!admission.Admitted || admission.Token is null)
        {
            return new PumpAnalogCommandOutcome
            {
                Tag = PumpCommandContract.SpeedReferenceTag,
                Address = address,
                RequestedEngineering = engineeringMilliamps,
                Admitted = false,
                Dispatched = false,
                AdmissionCode = admission.Code,
                DispatchCode = PumpCommandRefusals.RefusedNotAdmittedBySession,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        bool dispatched;
        bool faulted = false;
        try
        {
            dispatched = _session.DispatchAnalogOutput(at, admission.Token, row, engineeringMilliamps);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // The token is already spent inside the session, so exactly-once is preserved even here.
            // The exception is captured as evidence, never propagated into the lifecycle.
            dispatched = false;
            faulted = true;
        }

        return new PumpAnalogCommandOutcome
        {
            Tag = PumpCommandContract.SpeedReferenceTag,
            Address = address,
            RequestedEngineering = engineeringMilliamps,
            Admitted = true,
            Dispatched = dispatched,
            AdmissionCode = admission.Code,
            DispatchCode = dispatched ? PumpCommandRefusals.Granted : PumpCommandRefusals.RefusedDispatchFailed,
            SessionGeneration = _session.Generation,
            At = at,
            TransportFaulted = faulted,
        };
    }

    private PumpCommandOutcome WriteCoil(string tag, bool value)
    {
        DateTimeOffset at = _time.GetUtcNow();
        int address = 0;
        bool bound = PumpCommandContract.TryCoilAddress(tag, out address);

        if (!PumpCommandContract.IsPumpCommandTag(tag) || !bound)
        {
            // A tag that is not one of the four commissioned Pump command outputs is refused outright.
            var refused = new PumpCommandOutcome
            {
                Tag = tag,
                Address = address,
                RequestedValue = value,
                Admitted = false,
                Dispatched = false,
                AdmissionCode = PumpCommandRefusals.RefusedWrongTag,
                DispatchCode = PumpCommandRefusals.RefusedWrongFunctionOrAddress,
                SessionGeneration = _session.Generation,
                At = at,
            };
            _commandLog.Add(refused);
            return refused;
        }

        if (!_catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row) || row.Binding is null)
        {
            var unbound = new PumpCommandOutcome
            {
                Tag = tag,
                Address = address,
                RequestedValue = value,
                Admitted = false,
                Dispatched = false,
                AdmissionCode = ProcessIoRefusals.WriteRefusedUnbound,
                DispatchCode = ProcessIoRefusals.WriteRefusedUnbound,
                SessionGeneration = _session.Generation,
                At = at,
            };
            _commandLog.Add(unbound);
            return unbound;
        }

        ProcessIoAdmission admission = _session.AdmitWrite(at, row, _authorization);
        if (!admission.Admitted || admission.Token is null)
        {
            var notAdmitted = new PumpCommandOutcome
            {
                Tag = tag,
                Address = address,
                RequestedValue = value,
                Admitted = false,
                Dispatched = false,
                AdmissionCode = admission.Code,
                DispatchCode = PumpCommandRefusals.RefusedNotAdmittedBySession,
                SessionGeneration = _session.Generation,
                At = at,
            };
            _commandLog.Add(notAdmitted);
            return notAdmitted;
        }

        bool dispatched;
        bool faulted = false;
        try
        {
            dispatched = _session.DispatchDigitalOutput(at, admission.Token, row, value);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // The token is already spent inside the session, so exactly-once is preserved even here.
            dispatched = false;
            faulted = true;
        }

        var outcome = new PumpCommandOutcome
        {
            Tag = tag,
            Address = address,
            RequestedValue = value,
            Admitted = true,
            Dispatched = dispatched,
            AdmissionCode = admission.Code,
            DispatchCode = dispatched ? PumpCommandRefusals.Granted : PumpCommandRefusals.RefusedDispatchFailed,
            SessionGeneration = _session.Generation,
            At = at,
            TransportFaulted = faulted,
        };
        _commandLog.Add(outcome);
        return outcome;
    }

    // ---------------------------------------------------------------- feedback

    public PumpFeedbackRead ReadRunning() => ReadFeedback(PumpCommandContract.RunningFeedbackTag, Feedback.RunningPolarity);

    public PumpFeedbackRead ReadFault() => ReadFeedback(PumpCommandContract.FaultFeedbackTag, Feedback.FaultPolarity);

    private PumpFeedbackRead ReadFeedback(string tag, PumpFeedbackPolarity polarity)
    {
        DateTimeOffset at = _time.GetUtcNow();
        bool commissioned =
            polarity == PumpFeedbackPolarity.ACTIVE_HIGH || polarity == PumpFeedbackPolarity.ACTIVE_LOW;

        _catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row);
        ProcessIoPhysicalObservation observation = _session.ObserveDigitalInput(tag, row, _time, _freshnessBudget);
        bool current = ProcessIoEvidenceAcceptance.Accepts(
            observation, _session.Generation, _time.GetUtcNow(), _freshnessBudget);

        bool rawBit = observation.RawValue == 1;
        bool active = false;
        if (commissioned && current)
        {
            active = polarity == PumpFeedbackPolarity.ACTIVE_HIGH ? rawBit : !rawBit;
        }

        return new PumpFeedbackRead
        {
            Tag = tag,
            Commissioned = commissioned,
            Code = commissioned ? observation.DiagnosticCode : PumpFeedbackConfiguration.NotCommissionedCode,
            RawBit = rawBit,
            Active = active,
            Current = current,
            SessionGeneration = _session.Generation,
            At = at,
        };
    }

    // ---------------------------------------------------------------- pressure

    /// <summary>
    /// Reads the Pump Outlet Pressure (AI-003), the sole Pump-ready authority.
    ///
    /// Returns null unless the observation is usable, PHYSICAL_READ, from the live generation and
    /// inside the freshness budget. The Pump Inlet row (AI-002) is never read here: it is diagnostic
    /// and trend only and cannot alias, substitute or fall back for this value.
    /// </summary>
    public PumpPressureRead ReadPumpOutletPressure()
    {
        string tag = PumpCommandContract.PumpOutletPressureTag;
        _catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row);
        ProcessIoPhysicalObservation observation = _session.ObserveAnalogInput(tag, row, _time, _freshnessBudget);
        DateTimeOffset now = _time.GetUtcNow();

        if (!ProcessIoEvidenceAcceptance.Accepts(observation, _session.Generation, now, _freshnessBudget))
        {
            return new PumpPressureRead
            {
                Tag = tag,
                Accepted = false,
                Code = observation.DiagnosticCode,
                Quality = observation.Quality,
                Sample = null,
                SessionGeneration = _session.Generation,
                At = now,
            };
        }

        if (observation.EngineeringValue is not { } bar)
        {
            return new PumpPressureRead
            {
                Tag = tag,
                Accepted = false,
                Code = observation.DiagnosticCode,
                Quality = observation.Quality,
                Sample = null,
                SessionGeneration = _session.Generation,
                At = now,
            };
        }

        return new PumpPressureRead
        {
            Tag = tag,
            Accepted = true,
            Code = PumpCommandRefusals.Granted,
            Quality = observation.Quality,
            Sample = new PressureSample(
                bar,
                PressureQuality.GOOD,
                Stale: false,
                observation.ObservedAt.ToUniversalTime(),
                PressureSample.PumpOutletSource),
            SessionGeneration = _session.Generation,
            At = now,
        };
    }
}
