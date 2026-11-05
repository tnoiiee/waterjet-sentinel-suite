using Wjss.Contracts;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime.Core.CleaningPath.Composition;

// PUMP-COMMAND-PRODUCT-INTEGRATION: the Cleaning Path Pump precondition.
//
// The composition coordinator is NOT modified. This decorator sits between it and the existing
// Process-Io equipment boundary and makes two rules structural instead of advisory:
//
//   1. Pump-ready evidence is served only while the Pump lifecycle is at confirmed readiness AND
//      holds the command channel. A pressure reading taken while the Pump is idle, stopping, faulted
//      or unknown is never readiness, so the existing coordinator gate cannot be satisfied by one.
//   2. A NEW Isolation Valve OPEN is refused unless the Pump lifecycle permits it. A CLOSE is always
//      delegated, because a cleanup close must never be blocked by a Pump state.
//
// The decorator adds no second state machine: the lifecycle remains the one implementation, and the
// coordinator remains the one composition state machine.
//
// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).

/// <summary>
/// Wraps one <see cref="ICleaningPathEquipmentBoundary"/> and gates the Pump precondition on the Pump
/// lifecycle. Every valve observation and every close command is passed through unchanged.
/// </summary>
public sealed class CleaningPathPumpLifecycleGate : ICleaningPathEquipmentBoundary
{
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

    /// <summary>Requests refused by this gate, in order, as typed codes. Evidence, never a command.</summary>
    public IReadOnlyList<string> RefusedRequests => _refusedRequests;

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
