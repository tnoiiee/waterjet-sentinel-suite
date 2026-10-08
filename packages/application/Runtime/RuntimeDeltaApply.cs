using Wjss.Contracts;

namespace Wjss.Runtime.Core;

/// <summary>
/// Result of applying one Delta: either the reconstructed next revision, or a
/// refusal that applied nothing. A refusal that detected a revision gap sets
/// <see cref="ResnapshotRequired"/> — the machine-readable signal that the
/// consumer must request a fresh Snapshot and must not continue the Delta
/// stream. There is no retry, reconnection or resynchronization behaviour here:
/// applying is a pure function of the consumer state and one Delta.
/// </summary>
public sealed record DeltaApplyOutcome
{
    /// <summary>True when the Delta was applied in full.</summary>
    public required bool Applied { get; init; }

    /// <summary>Refusal code; null when applied.</summary>
    public string? Code { get; init; }

    /// <summary>Human-readable refusal detail; null when applied.</summary>
    public string? Reason { get; init; }

    /// <summary>True when the consumer revision cannot be advanced by this Delta and a fresh Snapshot is required.</summary>
    public required bool ResnapshotRequired { get; init; }

    /// <summary>The reconstructed revision; null when refused.</summary>
    public RuntimeState? State { get; init; }

    /// <summary>
    /// Builds the successful outcome. The factory is named <c>Success</c> because
    /// the outcome STATE is the <see cref="Applied"/> property: a factory sharing
    /// that identifier is a duplicate member declaration (CS0102). The refusal
    /// counterpart is <see cref="Refused"/>.
    /// </summary>
    public static DeltaApplyOutcome Success(RuntimeState state) => new()
    {
        Applied = true,
        ResnapshotRequired = false,
        State = state ?? throw new ArgumentNullException(nameof(state)),
    };

    /// <summary>Builds the refused outcome; nothing was applied in this path.</summary>
    public static DeltaApplyOutcome Refused(string code, string reason, bool resnapshotRequired = false) => new()
    {
        Applied = false,
        Code = code,
        Reason = reason,
        ResnapshotRequired = resnapshotRequired,
    };
}

/// <summary>
/// The strict Delta application path for tests and future consumers.
///
/// Applying is total or nothing. A Delta that does not continue the consumer's
/// revision is a GAP: nothing is applied, a fresh Snapshot is required, and the
/// stream must not be continued. A Delta that contradicts its own previous
/// records, rewrites configuration-derived Sensor identity, or disagrees with the
/// wall composition its Sensors imply is refused rather than normalized: the
/// reconstructed revision must validate under the same structural invariants the
/// store enforces, and no repair is attempted.
///
/// The instant of the reconstructed revision is the Delta's own accepted-tick
/// instant, so a consumer never supplies (or can mis-supply) a timestamp.
/// </summary>
public static class RuntimeDeltaApply
{
    /// <summary>Applies one Delta to the consumer's current revision.</summary>
    public static DeltaApplyOutcome Apply(RuntimeState current, RuntimeDelta delta)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(delta);

        if (delta.Revision != delta.PreviousRevision + 1)
        {
            return DeltaApplyOutcome.Refused(
                RuntimeRefusalCodes.RevisionNotNext,
                $"Refused malformed Delta revision {delta.Revision}: the revision must be previousRevision + 1. Nothing was applied.");
        }

        if (current.Revision != delta.PreviousRevision)
        {
            return DeltaApplyOutcome.Refused(
                RuntimeRefusalCodes.DeltaOutOfOrder,
                $"Detected a revision gap: the Delta starts at {delta.PreviousRevision} but the consumer holds "
                + $"{current.Revision}. Nothing was applied, no Delta was inferred, and the stream must not be continued; "
                + "a fresh Snapshot is required.",
                resnapshotRequired: true);
        }

        if (delta.GeneratedAtUtc <= current.GeneratedAtUtc)
        {
            return DeltaApplyOutcome.Refused(
                RuntimeRefusalCodes.EvolutionTickTime,
                $"Refused Delta {delta.Revision}: its instant is not strictly later than the consumer revision's instant. Nothing was applied.");
        }

        if (delta.ChangedSensors.Count != delta.PreviousSensors.Count)
        {
            return DeltaApplyOutcome.Refused(
                RuntimeRefusalCodes.StateInvalid,
                $"Refused Delta {delta.Revision}: it carries {delta.ChangedSensors.Count} new Sensor record(s) against "
                + $"{delta.PreviousSensors.Count} previous record(s). Nothing was applied.");
        }

        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < current.Sensors.Count; index++)
        {
            positions[current.Sensors[index].SensorId] = index;
        }

        var nextSensors = new SensorPresentationState[current.Sensors.Count];
        for (var index = 0; index < current.Sensors.Count; index++)
        {
            nextSensors[index] = current.Sensors[index];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < delta.ChangedSensors.Count; index++)
        {
            var replacement = delta.ChangedSensors[index];
            var expectedPrevious = delta.PreviousSensors[index];

            if (!positions.TryGetValue(replacement.SensorId, out var position))
            {
                return DeltaApplyOutcome.Refused(
                    RuntimeRefusalCodes.StateInvalid,
                    $"Refused Delta {delta.Revision}: Sensor {replacement.SensorId} does not exist in the consumer revision. Nothing was applied.");
            }

            if (!seen.Add(replacement.SensorId))
            {
                return DeltaApplyOutcome.Refused(
                    RuntimeRefusalCodes.StateInvalid,
                    $"Refused Delta {delta.Revision}: Sensor {replacement.SensorId} appears more than once. Nothing was applied.");
            }

            if (!current.Sensors[position].Equals(expectedPrevious))
            {
                return DeltaApplyOutcome.Refused(
                    RuntimeRefusalCodes.StateInvalid,
                    $"Refused Delta {delta.Revision}: the recorded previous record of Sensor {replacement.SensorId} "
                    + "does not match the consumer revision. Nothing was applied.");
            }

            if (!IdentityUnchanged(current.Sensors[position], replacement))
            {
                return DeltaApplyOutcome.Refused(
                    RuntimeRefusalCodes.SensorIdentity,
                    $"Refused Delta {delta.Revision}: Sensor {replacement.SensorId} rewrites configuration-derived identity. Nothing was applied.");
            }

            nextSensors[position] = replacement;
        }

        var expectedWalls = RuntimeWallSummaries.Recalculate(nextSensors);
        var replacements = new Dictionary<Wall, WallSummary>();
        foreach (var wall in delta.ChangedWalls)
        {
            if (!replacements.TryAdd(wall.Wall, wall))
            {
                return DeltaApplyOutcome.Refused(
                    RuntimeRefusalCodes.WallSummaryShape,
                    $"Refused Delta {delta.Revision}: wall {wall.Wall} is replaced more than once. Nothing was applied.");
            }
        }

        var nextWalls = new List<WallSummary>(expectedWalls.Count);
        for (var index = 0; index < expectedWalls.Count; index++)
        {
            var expected = expectedWalls[index];
            var merged = replacements.TryGetValue(expected.Wall, out var replacement)
                ? replacement
                : WallFrom(current.Walls, expected.Wall);

            if (merged is null || !merged.Equals(expected))
            {
                return DeltaApplyOutcome.Refused(
                    RuntimeRefusalCodes.WallSummaryMismatch,
                    $"Refused Delta {delta.Revision}: the wall summary of {expected.Wall} disagrees with the Sensor "
                    + "composition the Delta produces. Nothing was applied and nothing was normalized.");
            }

            nextWalls.Add(merged);
        }

        var activeJob = delta.ActiveJob.Encoding switch
        {
            DeltaJobEncoding.Present => delta.ActiveJob.Present,
            DeltaJobEncoding.Cleared => null,
            _ => current.ActiveJob,
        };

        var candidate = current with
        {
            Revision = delta.Revision,
            GeneratedAtUtc = delta.GeneratedAtUtc,
            Sensors = RuntimeCollections.Freeze(nextSensors),
            Walls = RuntimeCollections.Freeze(nextWalls),
            ActiveJob = activeJob,
            Pump = delta.Pump ?? current.Pump,
            Queue = delta.Queue ?? current.Queue,
            Sequence = delta.Sequence ?? current.Sequence,
            Alarms = delta.Alarms ?? current.Alarms,
            Communication = delta.Communication ?? current.Communication,
            Trend = delta.TrendPoint is null
                ? current.Trend
                : RuntimeTrendBuffer.Append(current.Trend, delta.TrendPoint),
        };

        if (!RuntimeStateInvariants.TryValidate(candidate, out var reasonCode, out var detail))
        {
            return DeltaApplyOutcome.Refused(
                reasonCode,
                $"Refused Delta {delta.Revision}: the reconstructed revision failed structural validation. {detail} Nothing was applied.");
        }

        return DeltaApplyOutcome.Success(candidate);
    }

    private static bool IdentityUnchanged(SensorPresentationState before, SensorPresentationState after) =>
        before.PositionKind == after.PositionKind
        && before.Wall == after.Wall
        && before.LogicalColumn == after.LogicalColumn
        && before.LogicalRow == after.LogicalRow
        && before.WallColumn == after.WallColumn
        && before.WallRow == after.WallRow
        && before.ScanOrder == after.ScanOrder
        && string.Equals(before.AssignedWaterJetId, after.AssignedWaterJetId, StringComparison.Ordinal)
        && string.Equals(before.AssignedIsolationValveId, after.AssignedIsolationValveId, StringComparison.Ordinal)
        && string.Equals(before.DeviceId, after.DeviceId, StringComparison.Ordinal)
        && string.Equals(before.TcFrontChannel, after.TcFrontChannel, StringComparison.Ordinal)
        && string.Equals(before.TcRearChannel, after.TcRearChannel, StringComparison.Ordinal);

    private static WallSummary? WallFrom(IReadOnlyList<WallSummary> walls, Wall wall)
    {
        for (var index = 0; index < walls.Count; index++)
        {
            if (walls[index].Wall == wall)
            {
                return walls[index];
            }
        }

        return null;
    }
}
