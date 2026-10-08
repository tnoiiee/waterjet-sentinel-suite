namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// One synthetic Sensor-to-Water-Jet assignment, paired with its Isolation Valve.
/// The pairing is WJn with IVn only.
/// </summary>
public sealed record SequencingSensorAssignment(string SensorId, string JetId, string ValveId);

/// <summary>
/// Explicit equipment eligibility input supplied with each admission or dispatch.
/// There is no hidden configuration. <see cref="NonSensorPositionIds"/> lists
/// logical positions that are not Sensors (for example I7 and I16); they are
/// refused at admission as NOT_A_SENSOR.
/// </summary>
public sealed record SequencingTopology(
    IReadOnlyList<SequencingSensorAssignment> SensorAssignments,
    IReadOnlyList<string> NonSensorPositionIds)
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
            return topology.NonSensorPositionIds.Contains(sensorId, StringComparer.Ordinal)
                ? SequencingCodes.NotASensor
                : SequencingCodes.SensorUnknown;
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
