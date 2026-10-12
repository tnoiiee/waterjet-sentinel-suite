namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// One synthetic Sensor-to-Water-Jet assignment, paired with its Isolation Valve.
/// The pairing is WJn with IVn only.
/// </summary>
public sealed record SequencingSensorAssignment(string SensorId, string JetId, string ValveId);

/// <summary>
/// Explicit equipment eligibility input supplied with each admission, dispatch or
/// Sensor enablement change. There is no hidden configuration. Owner critical
/// topology correction (Stage 0.4C-K3): every logical position is a Product Sensor
/// (I7 and I16 included), so no "non-Sensor position" list exists any more; an
/// identity with no assignment is refused as SENSOR_UNKNOWN (fail closed). The
/// Enabled / Disabled state of a Sensor is kernel state
/// (<see cref="SequencingState.DisabledSensorIds"/>), not topology.
/// </summary>
public sealed record SequencingTopology(
    IReadOnlyList<SequencingSensorAssignment> SensorAssignments)
{
    private static readonly string[] WaterJetIds = ["WJ1", "WJ2", "WJ3", "WJ4", "WJ5", "WJ6", "WJ7", "WJ8"];

    private static readonly string[] IsolationValveIds = ["IV1", "IV2", "IV3", "IV4", "IV5", "IV6", "IV7", "IV8"];

    /// <summary>Returns the assignment for a Sensor, or null when the Sensor is not assigned.</summary>
    public static SequencingSensorAssignment? FindAssignment(SequencingTopology topology, string sensorId)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(sensorId);

        foreach (var assignment in topology.SensorAssignments)
        {
            if (string.Equals(assignment.SensorId, sensorId, StringComparison.Ordinal))
            {
                return assignment;
            }
        }

        return null;
    }

    /// <summary>
    /// Structural eligibility of a Sensor under this topology. Returns null when
    /// the Sensor is eligible; otherwise a SequencingCodes refusal code. The
    /// same check is used at admission and when the queue head is revalidated.
    /// </summary>
    public static string? StructuralRefusal(SequencingTopology topology, string sensorId)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(sensorId);

        if (string.IsNullOrWhiteSpace(sensorId))
        {
            return SequencingCodes.EntryInvalid;
        }

        var assignment = FindAssignment(topology, sensorId);
        if (assignment is null)
        {
            return SequencingCodes.SensorUnknown;
        }

        if (!IsPairedOrdinal(assignment.JetId, assignment.ValveId))
        {
            return SequencingCodes.EntryInvalid;
        }

        return null;
    }

    /// <summary>
    /// True only for a WJn paired with the same ordinal IVn, with n in 1..8. This is
    /// the single WJn/IVn pairing rule, shared by admission and state validation.
    /// </summary>
    internal static bool IsPairedOrdinal(string jetId, string valveId)
    {
        var jetIndex = Array.IndexOf(WaterJetIds, jetId);
        return jetIndex >= 0 && jetIndex == Array.IndexOf(IsolationValveIds, valveId);
    }
}
