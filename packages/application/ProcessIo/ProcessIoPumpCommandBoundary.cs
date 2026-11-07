using Wjss.Contracts;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.ProcessIo;

// PUMP PLATFORM, PHASE 0.5C: the Pump command boundary and the Pump feedback interpretation.
//
// The boundary is the ONLY place a Pump command becomes a Process-Io dispatch. It owns no state
// machine, no ramp and no order: the lifecycle owns those, and the boundary executes exactly the one
// command it is handed. Every write still passes through ProcessIoSession admission, so the session
// remains the single write authority of the composition.
//
// The boundary is fed by ProcessIoPhysicalObservation evidence and applies the ONE acceptance rule
// (ProcessIoEvidenceAcceptance) that the Cleaning Path equipment boundary already applies, so the two
// can never disagree about what counts as a current physical reading.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA. Physical Hardware has NOT been executed and is NOT
// verified.

/// <summary>
/// The single acceptance rule for Process-Io evidence. One definition, used by the Pump boundary and by
/// the Cleaning Path equipment boundary, so a simulated or superseded read can never be mistaken for a
/// current physical one.
/// </summary>
public static class ProcessIoEvidenceAcceptance
{
    /// <summary>
    /// True only for a usable PHYSICAL_READ observation that belongs to the live session generation and
    /// is inside the configured freshness budget at <paramref name="now"/>.
    /// </summary>
    public static bool Accepts(
        ProcessIoPhysicalObservation? observation,
        int liveGeneration,
        DateTimeOffset now,
        TimeSpan freshnessBudget)
    {
        if (observation is null) return false;
        if (!observation.IsUsable) return false;
        if (observation.Provenance != ProcessIoObservationProvenance.PHYSICAL_READ) return false;
        if (observation.SourceGeneration != liveGeneration) return false;
        return observation.IsUsableAt(now, liveGeneration, freshnessBudget);
    }
}

/// <summary>
/// One dispatched Pump digital command. <see cref="Dispatched"/> true means the session accepted the
/// token and the transport call returned; it is never a claim that the field device actuated.
/// </summary>
public sealed record PumpCommandOutcome
{
    public required string Tag { get; init; }
    public required int CoilAddress { get; init; }
    public required bool Value { get; init; }
    public required bool Admitted { get; init; }
    public required bool Dispatched { get; init; }
    public required string Code { get; init; }
    public required int SessionGeneration { get; init; }
    public required DateTimeOffset At { get; init; }
    public bool TransportFaulted { get; init; }

    /// <summary>True only when the command was admitted and the write returned without a fault.</summary>
    public bool Succeeded => Admitted && Dispatched && !TransportFaulted;
}

/// <summary>
/// One analog reference command. <see cref="RequestedEngineeringMilliamps"/> is the typed command's
/// engineering value and <see cref="RequestedKind"/> is its identity, because the raw word is 0 for
/// both the 4 mA live minimum and the 0 mA safe-stop sentinel.
/// </summary>
public sealed record PumpAnalogCommandOutcome
{
    public required string Tag { get; init; }
    public required int RegisterAddress { get; init; }
    public required double RequestedEngineeringMilliamps { get; init; }

    /// <summary>The engineering identity of the requested command, never inferred from the raw word.</summary>
    public required string RequestedKind { get; init; }

    /// <summary>The raw word actually written, or 0 when nothing was written.</summary>
    public required int RawWord { get; init; }

    public required bool Admitted { get; init; }
    public required bool Dispatched { get; init; }
    public required string Code { get; init; }
    public required int SessionGeneration { get; init; }
    public required DateTimeOffset At { get; init; }
    public bool TransportFaulted { get; init; }

    public bool Succeeded => Admitted && Dispatched && !TransportFaulted;
}

/// <summary>
/// One interpreted feedback read. <see cref="Commissioned"/> false means the configured polarity has
/// not been established, so <see cref="Active"/> carries no meaning and must never be used.
/// </summary>
public sealed record PumpFeedbackRead
{
    public required string Tag { get; init; }
    public required bool Commissioned { get; init; }

    /// <summary>The physical bit the transport returned. Meaningful only when the read was accepted.</summary>
    public required bool RawBit { get; init; }

    /// <summary>The interpreted condition. Always false while uncommissioned or unaccepted.</summary>
    public required bool Active { get; init; }

    /// <summary>True only for an accepted, current, commissioned reading.</summary>
    public required bool Usable { get; init; }

    public required string Code { get; init; }
    public required int SessionGeneration { get; init; }
    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// One Pump Outlet pressure observation. <see cref="Accepted"/> false means no sample exists: a refusal
/// never fabricates a bar value and never falls back to the Pump Inlet row.
/// </summary>
public sealed record PumpPressureRead
{
    public required string Tag { get; init; }
    public required bool Accepted { get; init; }
    public required string Code { get; init; }

    /// <summary>
    /// True only when the observation existed but was outside its freshness budget. A stale reading is
    /// refused, and it is distinguished from an unusable one so the lifecycle can say which happened.
    /// </summary>
    public bool Stale { get; init; }

    /// <summary>The accepted engineering sample, or null when the read was refused.</summary>
    public PressureSample? Sample { get; init; }

    public required int SessionGeneration { get; init; }
    public required DateTimeOffset At { get; init; }
}

/// <summary>
/// The typed Pump command boundary the lifecycle consumes. It exposes the four command groups, the two
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

    /// <summary>Writes one typed analog speed reference.</summary>
    PumpAnalogCommandOutcome SetSpeedCommand(PumpAnalogCommand command);

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
/// The Process-Io implementation of <see cref="IPumpCommandBoundary"/>.
///
/// It holds no lifecycle state. It is constructed once per composition and is the only route from the
/// Pump lifecycle to the transport, always through the session.
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
        _freshnessBudget = freshnessBudget ?? TimeSpan.FromMilliseconds(
            PumpLifecycleConfiguration.OwnerInitialFreshnessBudgetMilliseconds);
    }

    /// <summary>Every digital command outcome in issue order, for evidence.</summary>
    public IReadOnlyList<PumpCommandOutcome> CommandLog => _commandLog;

    public PumpFeedbackConfiguration Feedback { get; }

    public int SessionGeneration => _session.Generation;

    // ---------------------------------------------------------------- commands

    /// <summary>
    /// Writes the exact two-write selection pair for one Pump ordinal: the selected output TRUE, then
    /// the other output FALSE. An ordinal that is not 1 or 2 is refused outright and no fallback to
    /// Pump 1 is ever substituted.
    /// </summary>
    public IReadOnlyList<PumpCommandOutcome> SelectPump(int selection)
    {
        IReadOnlyList<PumpCommandStep> order = PumpCommandContract.SelectionSequence(selection);
        if (order.Count == 0)
        {
            // An invalid ordinal produces no command at all, and never a fallback to Pump 1.
            return Array.Empty<PumpCommandOutcome>();
        }

        var outcomes = new List<PumpCommandOutcome>(order.Count);
        for (int index = 0; index < order.Count; index += 1)
        {
            outcomes.Add(WriteCoil(order[index].Tag, order[index].Value));
        }

        return outcomes;
    }

    public PumpCommandOutcome SetEnableInverter(bool value) =>
        WriteCoil(PumpCommandContract.EnableInverterTag, value);

    public PumpCommandOutcome SetRunPump(bool value) =>
        WriteCoil(PumpCommandContract.RunPumpTag, value);

    /// <summary>
    /// Writes one typed analog speed reference through the session. The command was already proven to be
    /// either the explicit 0 mA sentinel or a live 4 to 20 mA reference, so the only conversion that runs
    /// here is the commissioned Pump map. No value is clamped and no guessed command is substituted: an
    /// invalid command is refused before it can reach the transport.
    /// </summary>
    public PumpAnalogCommandOutcome SetSpeedCommand(PumpAnalogCommand command)
    {
        DateTimeOffset at = _time.GetUtcNow();

        if (!command.Valid || !PumpCommandContract.TryAnalogAddress(out int registerAddress))
        {
            return new PumpAnalogCommandOutcome
            {
                Tag = PumpCommandContract.SpeedReferenceTag,
                RegisterAddress = 0,
                RequestedEngineeringMilliamps = command.Milliamps,
                RequestedKind = PumpAnalogCommandContract.SafeStopSentinelCode,
                RawWord = 0,
                Admitted = false,
                Dispatched = false,
                Code = PumpCommandRefusals.RefusedAnalogOutOfDomain,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        // The conversion is proven BEFORE admission, so an invalid command consumes no token.
        if (!PumpAnalogCommandContract.TryToRaw(command, out int raw))
        {
            return new PumpAnalogCommandOutcome
            {
                Tag = PumpCommandContract.SpeedReferenceTag,
                RegisterAddress = registerAddress,
                RequestedEngineeringMilliamps = command.Milliamps,
                RequestedKind = command.KindCode,
                RawWord = 0,
                Admitted = false,
                Dispatched = false,
                Code = PumpCommandRefusals.RefusedAnalogOutOfDomain,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        if (!_catalogue.TryGetValue(PumpCommandContract.SpeedReferenceTag, out ProcessIoCatalogueRow? row)
            || row.Binding is null)
        {
            return new PumpAnalogCommandOutcome
            {
                Tag = PumpCommandContract.SpeedReferenceTag,
                RegisterAddress = registerAddress,
                RequestedEngineeringMilliamps = command.Milliamps,
                RequestedKind = command.KindCode,
                RawWord = 0,
                Admitted = false,
                Dispatched = false,
                Code = PumpCommandRefusals.RefusedUnbound,
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
                RegisterAddress = registerAddress,
                RequestedEngineeringMilliamps = command.Milliamps,
                RequestedKind = command.KindCode,
                RawWord = 0,
                Admitted = false,
                Dispatched = false,
                Code = PumpCommandRefusals.RefusedNotAdmitted,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        bool dispatched;
        bool faulted = false;
        try
        {
            dispatched = _session.DispatchPumpAnalogOutput(at, admission.Token, row, command);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // The token is already spent inside the session, so exactly-once is preserved even here.
            // The exception is captured as evidence, never propagated into the lifecycle.
            dispatched = false;
            faulted = true;
        }

        return new PumpAnalogCommandOutcome
        {
            Tag = PumpCommandContract.SpeedReferenceTag,
            RegisterAddress = registerAddress,
            RequestedEngineeringMilliamps = command.Milliamps,
            RequestedKind = command.KindCode,
            RawWord = dispatched ? raw : 0,
            Admitted = true,
            Dispatched = dispatched,
            Code = dispatched ? PumpCommandRefusals.Granted : PumpCommandRefusals.RefusedTransportFault,
            SessionGeneration = _session.Generation,
            At = at,
            TransportFaulted = faulted,
        };
    }

    // ---------------------------------------------------------------- feedback

    public PumpFeedbackRead ReadRunning() =>
        ReadFeedback(
            PumpCommandContract.RunningFeedbackTag,
            Feedback.RunningPolarity,
            Feedback.RunningCommissioned);

    public PumpFeedbackRead ReadFault() =>
        ReadFeedback(
            PumpCommandContract.FaultFeedbackTag,
            Feedback.FaultPolarity,
            Feedback.FaultCommissioned);

    /// <summary>
    /// Reads the Pump Outlet pressure. The row identity is AI-003, the sample source identity is the
    /// Pump Outlet source, and the sample is accepted only through the ONE acceptance rule. The Pump
    /// Inlet row is never substituted.
    /// </summary>
    public PumpPressureRead ReadPumpOutletPressure()
    {
        DateTimeOffset at = _time.GetUtcNow();

        if (!_catalogue.TryGetValue(PumpCommandContract.PumpOutletPressureTag, out ProcessIoCatalogueRow? row)
            || row.Binding is null)
        {
            return new PumpPressureRead
            {
                Tag = PumpCommandContract.PumpOutletPressureTag,
                Accepted = false,
                Code = PumpCommandRefusals.RefusedUnbound,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        ProcessIoPhysicalObservation observation =
            _session.ObserveAnalogInput(PumpCommandContract.PumpOutletPressureTag, row, _time, _freshnessBudget);

        if (!ProcessIoEvidenceAcceptance.Accepts(observation, _session.Generation, at, _freshnessBudget))
        {
            // A stale sample is reported as stale; anything else is unusable. Neither is ever converted
            // into an invented bar value, and the Pump Inlet row is never substituted.
            return new PumpPressureRead
            {
                Tag = PumpCommandContract.PumpOutletPressureTag,
                Accepted = false,
                Code = PumpCommandRefusals.RefusedPressure,
                Stale = observation.Quality == ProcessIoObservationQuality.STALE,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        double bar = ProcessIoEngineering.PumpOutletPressureBar(observation.RawValue);
        if (!double.IsFinite(bar))
        {
            return new PumpPressureRead
            {
                Tag = PumpCommandContract.PumpOutletPressureTag,
                Accepted = false,
                Code = PumpCommandRefusals.RefusedPressure,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        return new PumpPressureRead
        {
            Tag = PumpCommandContract.PumpOutletPressureTag,
            Accepted = true,
            Code = PumpCommandRefusals.Granted,
            Sample = new PressureSample(bar, PressureQuality.GOOD, false, at, PressureSample.PumpOutletSource),
            SessionGeneration = _session.Generation,
            At = at,
        };
    }

    // ---------------------------------------------------------------- internals

    private PumpFeedbackRead ReadFeedback(
        string tag,
        PumpFeedbackPolarity polarity,
        bool commissioned)
    {
        DateTimeOffset at = _time.GetUtcNow();
        bool polarityKnown = commissioned;
        string code = polarityKnown ? PumpCommandRefusals.Granted : PumpFeedbackConfiguration.NotCommissionedCode;

        if (!_catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row) || row.Binding is null)
        {
            return new PumpFeedbackRead
            {
                Tag = tag,
                Commissioned = polarityKnown,
                RawBit = false,
                Active = false,
                Usable = false,
                Code = PumpCommandRefusals.RefusedUnbound,
                SessionGeneration = _session.Generation,
                At = at,
            };
        }

        ProcessIoPhysicalObservation observation =
            _session.ObserveDigitalInput(tag, row, _time, _freshnessBudget);

        bool usable = ProcessIoEvidenceAcceptance.Accepts(
            observation, _session.Generation, at, _freshnessBudget);
        bool rawBit = usable && observation.RawValue != 0;

        // An uncommissioned polarity is never interpreted: Active stays false and the code says why.
        bool active = usable
            && polarityKnown
            && (polarity == PumpFeedbackPolarity.ACTIVE_HIGH ? rawBit : !rawBit);

        return new PumpFeedbackRead
        {
            Tag = tag,
            Commissioned = polarityKnown,
            RawBit = rawBit,
            Active = active,
            Usable = usable && polarityKnown,
            Code = usabilityCode(code, usable, polarityKnown),
            SessionGeneration = _session.Generation,
            At = at,
        };

        static string usabilityCode(string granted, bool usable, bool polarityKnown)
        {
            if (!usable) return PumpCommandRefusals.RefusedPressure;
            return polarityKnown ? granted : PumpFeedbackConfiguration.NotCommissionedCode;
        }
    }

    private PumpCommandOutcome WriteCoil(string tag, bool value)
    {
        DateTimeOffset at = _time.GetUtcNow();

        if (!PumpCommandContract.TryCoilAddress(tag, out int coilAddress))
        {
            // A tag that is not one of the four commissioned Pump command outputs is refused outright.
            var refused = new PumpCommandOutcome
            {
                Tag = tag,
                CoilAddress = 0,
                Value = value,
                Admitted = false,
                Dispatched = false,
                Code = PumpCommandRefusals.RefusedUnbound,
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
                CoilAddress = coilAddress,
                Value = value,
                Admitted = false,
                Dispatched = false,
                Code = PumpCommandRefusals.RefusedUnbound,
                SessionGeneration = _session.Generation,
                At = at,
            };
            _commandLog.Add(unbound);
            return unbound;
        }

        ProcessIoAdmission admission = _session.AdmitWrite(at, row, _authorization);
        if (!admission.Admitted || admission.Token is null)
        {
            var refused = new PumpCommandOutcome
            {
                Tag = tag,
                CoilAddress = coilAddress,
                Value = value,
                Admitted = false,
                Dispatched = false,
                Code = PumpCommandRefusals.RefusedNotAdmitted,
                SessionGeneration = _session.Generation,
                At = at,
            };
            _commandLog.Add(refused);
            return refused;
        }

        bool dispatched;
        bool faulted = false;
        try
        {
            dispatched = _session.DispatchDigitalOutput(at, admission.Token, row, value);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            dispatched = false;
            faulted = true;
        }

        var outcome = new PumpCommandOutcome
        {
            Tag = tag,
            CoilAddress = coilAddress,
            Value = value,
            Admitted = true,
            Dispatched = dispatched,
            Code = dispatched ? PumpCommandRefusals.Granted : PumpCommandRefusals.RefusedTransportFault,
            SessionGeneration = _session.Generation,
            At = at,
            TransportFaulted = faulted,
        };
        _commandLog.Add(outcome);
        return outcome;
    }
}
