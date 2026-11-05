using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.CleaningPath.Composition;

// PUMP PLATFORM, PHASE 0.5C: the Cleaning Path Pump precondition.
//
// The composition coordinator is NOT modified, and ICleaningPathEquipmentBoundary is NOT widened: it
// still has no Pump command member, by design. This decorator sits between the coordinator and the real
// Process-Io equipment boundary and makes two rules structural instead of advisory:
//
//   1. Pump-ready evidence is served only while the Pump lifecycle is at confirmed readiness AND holds the
//      command channel. A pressure reading taken while the Pump is idle, stopping, faulted or unknown is
//      never readiness, so an Isolation Valve can never be opened on a measured pressure that no
//      commanded Pump produced.
//   2. A new Isolation Valve OPEN is refused unless the Pump lifecycle permits it. A CLOSE is always
//      delegated, because a cleanup close must never be blocked by a Pump state.
//
// The decorator adds no second state machine: the lifecycle remains the one implementation and the
// coordinator remains the one composition state machine.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA. Physical Hardware has NOT been executed and is NOT
// verified.

/// <summary>
/// Wraps one <see cref="ICleaningPathEquipmentBoundary"/> and gates the Pump precondition on the Pump
/// lifecycle. Every valve observation and every close command is passed through unchanged.
/// </summary>
public sealed class CleaningPathPumpLifecycleGate : ICleaningPathEquipmentBoundary
{
    /// <summary>The typed refusal recorded when an IV OPEN is attempted before confirmed Pump readiness.</summary>
    public const string RefusedValveOpenPumpNotReady = "IV_OPEN_REFUSED_PUMP_NOT_READY";

    private readonly ICleaningPathEquipmentBoundary _inner;
    private readonly ProcessIoPumpLifecycle _pump;
    private readonly List<string> _refusedRequests = new();

    public CleaningPathPumpLifecycleGate(ICleaningPathEquipmentBoundary inner, ProcessIoPumpLifecycle pump)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(pump);

        _inner = inner;
        _pump = pump;
    }

    /// <summary>Requests this gate refused, in order, as typed codes. Evidence, never a command.</summary>
    public IReadOnlyList<string> RefusedRequests => _refusedRequests;

    /// <summary>The Pump lifecycle this gate consults. There is no other authority behind the precondition.</summary>
    public ProcessIoPumpLifecycle Pump => _pump;

    /// <summary>
    /// Delegates a CLOSE always, and an OPEN only while the Pump lifecycle permits a new valve OPEN.
    /// </summary>
    public void RequestValve(CleaningPathValveCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind == CleaningPathValveCommandKind.Open && !_pump.AllowsValveOpen)
        {
            _refusedRequests.Add(RefusedValveOpenPumpNotReady + ":" + request.ValveId);
            return;
        }

        _inner.RequestValve(request);
    }

    /// <summary>
    /// Serves the Pump Outlet Pressure only while the Pump lifecycle is at confirmed readiness. The
    /// underlying boundary still applies its own quality, provenance, generation and freshness rules.
    /// </summary>
    public PressureSample? ReadPumpOutletPressure() =>
        _pump.AllowsValveOpen ? _inner.ReadPumpOutletPressure() : null;

    public PressureSample? ReadValveOutletPressure(string valveId) =>
        _inner.ReadValveOutletPressure(valveId);

    public bool ReadUpperLimitDetected(string valveId) => _inner.ReadUpperLimitDetected(valveId);

    public bool ReadLowerLimitDetected(string valveId) => _inner.ReadLowerLimitDetected(valveId);
}
