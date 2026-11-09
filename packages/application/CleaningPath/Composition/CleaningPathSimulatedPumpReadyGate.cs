using Wjss.Runtime.Core.Sequencing;
using Wjss.Time;

namespace Wjss.Runtime.Core.CleaningPath.Composition;

// PUMP PLATFORM, PHASE 0.5D: the SECOND, EXPLICITLY LABELLED Pump-ready evidence source.
//
// This decorator exists for ONE evidence class only: a bench trial in which the Owner attests that the
// high-pressure Pump is STOPPED at the panel, declares the Pump Outlet Pressure value the path would have
// seen, and wants the paired Isolation Valve and the whole Cleaning Path to be exercised for real on top
// of that DECLARED value. It is the runbook Stage D simulated variant.
//
// It is NOT a simulator of the machine. Exactly two things change relative to the physical gate:
//
//   1. ReadPumpOutletPressure serves the Owner-declared bar as a PressureSample whose SourceId names the
//      declaration ("AI-003+SOFTWARE_SIMULATED"). Nothing reads a transport for that number, and the
//      sample can never be mistaken for a measurement because its identity is carried inside it.
//   2. There is no Pump lifecycle behind it: this composition never selects a Pump, never energises an
//      enable, never writes an analog value and never reads a Running or Fault input, because the Pump is
//      attested stopped. Evidence must therefore state physical_pump_verified=FALSE.
//
// Everything else is delegated UNCHANGED to the real boundary: every paired-IV OPEN and CLOSE is a physical
// write through the authorized Process-Io session, and every Valve Outlet Pressure, Upper limit and Lower
// limit observation is read from the hardware. So the valve command path, the limit-switch supervision and
// the Mandatory Safe Return cleanup are all genuinely under test; only the Pump-ready precondition is
// declared rather than measured.
//
// An OPEN is admitted only after the composition has begun, which mirrors the ordering guarantee the
// physical gate provides: no valve may be opened before the readiness source for this run has been admitted
// by the composition root. A CLOSE is always delegated, because a cleanup close must never be blocked.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA. Physical Hardware has NOT been executed and is NOT
// verified.

/// <summary>
/// Serves one Owner-declared Pump-ready value as the Pump precondition of an otherwise fully physical
/// Cleaning Path composition, and labels every sample it produces as declared rather than measured.
/// </summary>
public sealed class CleaningPathSimulatedPumpReadyGate : ICleaningPathEquipmentBoundary
{
    /// <summary>
    /// The SourceId carried by every sample this gate serves. It is part of the evidence line, not a
    /// comment: a reader of the transcript sees the pressure identity and the declaration together.
    /// </summary>
    public const string DeclaredPressureSourceId = "AI-003+SOFTWARE_SIMULATED";

    /// <summary>The evidence kind this gate forces on its composition.</summary>
    public const string EvidenceKind = "SOFTWARE_SIMULATED";

    /// <summary>Recorded when an IV OPEN is attempted before the composition has begun.</summary>
    public const string RefusedValveOpenBeforeBegin = "IV_OPEN_REFUSED_SIMULATED_RUN_NOT_BEGUN";

    private readonly ICleaningPathEquipmentBoundary _inner;
    private readonly IClock _clock;
    private readonly double _declaredBar;
    private readonly double _pumpReadySetpointBar;
    private readonly List<string> _refusedRequests = new();
    private bool _begun;

    /// <summary>
    /// Builds the gate. The declared value must be strictly greater than the Pump-ready setpoint, which is
    /// the same strict rule the physical lifecycle applies to a measured sample: a declared value at or
    /// below the setpoint is not readiness, and it is refused at composition time rather than silently
    /// producing an unadmitted path.
    /// </summary>
    public CleaningPathSimulatedPumpReadyGate(
        ICleaningPathEquipmentBoundary inner,
        IClock clock,
        double declaredPumpOutletBar,
        double pumpReadySetpointBar)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(clock);

        if (!double.IsFinite(declaredPumpOutletBar) || declaredPumpOutletBar < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(declaredPumpOutletBar),
                "The declared Pump Outlet Pressure must be a finite non-negative number of bar.");
        }

        if (!double.IsFinite(pumpReadySetpointBar) || pumpReadySetpointBar < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pumpReadySetpointBar),
                "The Pump-ready setpoint must be a finite non-negative number of bar.");
        }

        if (declaredPumpOutletBar <= pumpReadySetpointBar)
        {
            throw new ArgumentOutOfRangeException(
                nameof(declaredPumpOutletBar),
                "A declared Pump Outlet Pressure at or below the Pump-ready setpoint is not readiness; the "
                + "simulated variant refuses it instead of loosening the strict comparison.");
        }

        _inner = inner;
        _clock = clock;
        _declaredBar = declaredPumpOutletBar;
        _pumpReadySetpointBar = pumpReadySetpointBar;
    }

    /// <summary>The declared value, exposed so the evidence line can print what it actually asserts.</summary>
    public double DeclaredPumpOutletBar => _declaredBar;

    /// <summary>The setpoint the declaration was compared against.</summary>
    public double PumpReadySetpointBar => _pumpReadySetpointBar;

    /// <summary>False always: no physical Pump command was verified in this evidence class.</summary>
    public bool PhysicalPumpVerified => false;

    /// <summary>The evidence kind this composition must report.</summary>
    public string PumpEvidenceKind => EvidenceKind;

    /// <summary>True once the composition has begun, which is what admits a paired-IV OPEN.</summary>
    public bool RunBegun => _begun;

    /// <summary>Requests this gate refused, in order, as typed codes. Evidence, never a command.</summary>
    public IReadOnlyList<string> RefusedRequests => _refusedRequests;

    /// <summary>
    /// Called once by the composition root when the run begins. It carries no value and commands nothing.
    /// </summary>
    public void Begin() => _begun = true;

    /// <summary>
    /// Delegates a CLOSE always, and an OPEN only while this run has begun. The valve write itself is the
    /// inner boundary's, with its authorization, provenance and session rules untouched.
    /// </summary>
    public void RequestValve(CleaningPathValveCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind == CleaningPathValveCommandKind.Open && !_begun)
        {
            _refusedRequests.Add(RefusedValveOpenBeforeBegin + ":" + request.ValveId);
            return;
        }

        _inner.RequestValve(request);
    }

    /// <summary>
    /// Serves the Owner-declared Pump Outlet Pressure as a GOOD, non-stale sample stamped with this gate's
    /// declared-source identity. It is never read from the transport and it is never presented as a
    /// measurement.
    /// </summary>
    public PressureSample? ReadPumpOutletPressure() =>
        new PressureSample(_declaredBar, PressureQuality.GOOD, false, _clock.UtcNow, DeclaredPressureSourceId);

    public PressureSample? ReadValveOutletPressure(string valveId) => _inner.ReadValveOutletPressure(valveId);

    public bool ReadUpperLimitDetected(string valveId) => _inner.ReadUpperLimitDetected(valveId);

    public bool ReadLowerLimitDetected(string valveId) => _inner.ReadLowerLimitDetected(valveId);
}
