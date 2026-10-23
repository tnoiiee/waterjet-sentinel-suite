using Wjss.Contracts;
using Wjss.Domain;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Deterministic, PUBLIC-SAFE synthetic Cleaning Path store builder for the Phase 3A runtime
/// tests. Every coordinate is formula-generated synthetic data (never an Owner-local or
/// Actual-Run value): point Pk of every Sensor carries Horizontal = k and
/// Vertical = 10 + k. The builder produces only complete, valid stores (all 108 canonical
/// Sensors) so that resolver and loader tests start from an accepted snapshot.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal static class SyntheticCleaningPathStores
{
    internal const string Schema = "wjss.cleaning-path-store/1";

    internal static string ValidStoreJson()
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("{\"schema\":\"").Append(Schema).Append("\",\"paths\":[");
        var first = true;
        foreach (var sensorId in CanonicalSensorMap.SensorSequence)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(PathJson(sensorId));
        }

        builder.Append("]}");
        return builder.ToString();
    }

    internal static CleaningPathSnapshot ValidSnapshot(DateTimeOffset? loadedAtUtc = null) =>
        CleaningPathStoreParser.Parse(ValidStoreJson(), loadedAtUtc).Snapshot!;

    internal static CleaningPathStoreResult ValidStoreResult(DateTimeOffset? loadedAtUtc = null) =>
        CleaningPathStoreParser.Parse(ValidStoreJson(), loadedAtUtc);

    private static string PathJson(string sensorId)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("{\"sensorId\":\"").Append(sensorId).Append("\",\"points\":[");
        for (var ordinal = 1; ordinal <= 6; ordinal++)
        {
            if (ordinal > 1)
            {
                builder.Append(',');
            }

            // Synthetic formula values only.
            var horizontal = ordinal;
            var vertical = 10 + ordinal;
            builder.Append("{\"pointId\":\"P").Append(ordinal).Append("\",\"horizontalMillimetres\":").Append(horizontal).Append(",\"verticalMillimetres\":").Append(vertical).Append('}');
        }

        builder.Append("]}");
        return builder.ToString();
    }

    /// <summary>Builds one synthetic Sensor configuration record for resolver tests.</summary>
    internal static SensorConfigurationRecord SensorConfiguration(
        string sensorId,
        string assignedWaterJetId,
        string assignedIsolationValveId)
    {
        var index = CanonicalSensorMap.SensorSequence.ToList().IndexOf(sensorId);
        if (index < 0)
        {
            throw new ArgumentException("not a canonical Sensor identity", nameof(sensorId));
        }

        var logicalColumn = (index % CanonicalSensorMap.LogicalColumnCount) + 1;
        var logicalRow = (index / CanonicalSensorMap.LogicalColumnCount) + 1;

        return new SensorConfigurationRecord
        {
            SensorId = sensorId,
            Wall = CanonicalSensorMap.WallForColumn(logicalColumn),
            LogicalColumn = logicalColumn,
            LogicalRow = logicalRow,
            WallColumn = 1,
            WallRow = logicalRow,
            OrderWall = 1,
            ScanOrder = index + 1,
            DiffLowerBound = 0,
            DiffUpperBound = 0,
            DirtyScoreThreshold = 0,
            Enabled = true,
            CleaningCount = 0,
            AssignedWaterJetId = assignedWaterJetId,
            AssignedIsolationValveId = assignedIsolationValveId,
            LegacyRecordId = "SYN-RECORD",
        };
    }
}
