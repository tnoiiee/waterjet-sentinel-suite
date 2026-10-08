using Wjss.Contracts;
using Wjss.Time;

namespace Wjss.Runtime.Core;

/// <summary>
/// Projects one accepted revision transition onto <c>wjss.delta/2</c>.
///
/// The projector is the only place a Delta envelope is produced, so envelope
/// identity (kind, schema, API version) and the three-state Active Job encoding
/// cannot drift between callers. It compares the previous committed revision with
/// the candidate revision the evolution produced and carries a section only when
/// that section's whole record changed — an omitted section means UNCHANGED, and
/// nothing is ever field-delta-encoded.
///
/// Two projections exist:
///   * <see cref="Project"/> — the Runtime-side, apply-safe Delta
///     (<see cref="RuntimeDelta"/>);
///   * <see cref="ProjectWire"/> — that Delta as the accepted wire DTO
///     (<see cref="OperationalDelta"/>).
///
/// The Runtime health block is deliberately absent from every Delta: uptime and
/// the store counters are ambient diagnostics, not part of the deterministic
/// state chain, so a Delta never carries a fabricated runtime block. They stay
/// Snapshot-delivered.
/// </summary>
public static class RuntimeDeltaProjector
{
    /// <summary>Envelope discriminator of the delta contract.</summary>
    public const string DeltaKind = "delta";

    /// <summary>
    /// Projects the transition from <paramref name="previous"/> to the candidate
    /// carried by <paramref name="evolution"/>. A transition that is not the
    /// immediate successor of the previous revision, or whose instant is not
    /// strictly later, is a programming error and is refused rather than encoded.
    /// </summary>
    public static RuntimeDelta Project(RuntimeState previous, SyntheticEvolutionResult evolution)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(evolution);

        var candidate = evolution.State;

        if (candidate.Revision != previous.Revision + 1)
        {
            throw new InvalidOperationException(
                $"[{RuntimeRefusalCodes.RevisionNotNext}] Refused to project a Delta from revision {previous.Revision} "
                + $"to {candidate.Revision}: a Delta is exactly one revision step. Nothing was projected.");
        }

        if (candidate.GeneratedAtUtc <= previous.GeneratedAtUtc)
        {
            throw new InvalidOperationException(
                $"[{RuntimeRefusalCodes.EvolutionTickTime}] Refused to project a Delta from revision {previous.Revision} "
                + "to an instant that is not strictly later. Nothing was projected.");
        }

        return new RuntimeDelta
        {
            PreviousRevision = previous.Revision,
            Revision = candidate.Revision,
            GeneratedAtUtc = candidate.GeneratedAtUtc,
            ChangedSensors = RuntimeCollections.Freeze(evolution.ChangedSensors),
            PreviousSensors = RuntimeCollections.Freeze(evolution.PreviousSensors),
            ChangedWalls = RuntimeCollections.Freeze(ChangedWalls(previous.Walls, candidate.Walls)),
            Pump = candidate.Pump.Equals(previous.Pump) ? null : candidate.Pump,
            Queue = candidate.Queue.Equals(previous.Queue) ? null : candidate.Queue,
            Sequence = candidate.Sequence.Equals(previous.Sequence) ? null : candidate.Sequence,
            Alarms = candidate.Alarms.Equals(previous.Alarms) ? null : candidate.Alarms,
            Communication = candidate.Communication.Equals(previous.Communication) ? null : candidate.Communication,
            TrendPoint = evolution.AppendedTrendPoint,
            ActiveJob = EncodeActiveJob(previous.ActiveJob, candidate.ActiveJob),
        };
    }

    /// <summary>
    /// Projects the apply-safe Delta onto the accepted wire DTO. Absent sections
    /// stay omitted (<c>null</c>) rather than being written as empty arrays, and
    /// the Active Job uses the contract's exact three-state encoding: absent key =
    /// unchanged, object = whole-record replacement, explicit null = cleared. No
    /// second clear flag exists anywhere in this path.
    /// </summary>
    public static OperationalDelta ProjectWire(RuntimeDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        return new OperationalDelta
        {
            Kind = DeltaKind,
            Schema = SchemaIds.Delta,
            ApiVersion = SchemaIds.ApiVersion,
            PreviousRevision = delta.PreviousRevision,
            Revision = delta.Revision,
            GeneratedAt = UtcTimestamps.Format(delta.GeneratedAtUtc),
            Config = null,
            Sensors = delta.ChangedSensors.Count == 0 ? null : delta.ChangedSensors,
            Walls = delta.ChangedWalls.Count == 0 ? null : delta.ChangedWalls,
            ActiveJob = EncodeOptional(delta.ActiveJob),
            Pump = delta.Pump,
            Queue = delta.Queue,
            Sequence = delta.Sequence,
            Alarms = delta.Alarms,
            Communication = delta.Communication,
            Runtime = null,
            TrendPoint = delta.TrendPoint,
        };
    }

    private static List<WallSummary> ChangedWalls(
        IReadOnlyList<WallSummary> previous, IReadOnlyList<WallSummary> candidate)
    {
        var changed = new List<WallSummary>();
        for (var index = 0; index < candidate.Count; index++)
        {
            var next = candidate[index];
            var before = index < previous.Count ? previous[index] : null;

            if (before is null || !before.Equals(next))
            {
                changed.Add(next);
            }
        }

        return changed;
    }

    private static DeltaJobState EncodeActiveJob(
        ActiveCleaningJobState? previous, ActiveCleaningJobState? candidate)
    {
        if (candidate is null)
        {
            return previous is null ? DeltaJobState.Unchanged() : DeltaJobState.Cleared();
        }

        return previous is null || !candidate.Equals(previous)
            ? DeltaJobState.Replaced(candidate)
            : DeltaJobState.Unchanged();
    }

    private static Optional<ActiveCleaningJobState> EncodeOptional(DeltaJobState job) => job.Encoding switch
    {
        DeltaJobEncoding.Present => Optional<ActiveCleaningJobState>.Present(job.Present),
        DeltaJobEncoding.Cleared => Optional<ActiveCleaningJobState>.Cleared,
        _ => Optional<ActiveCleaningJobState>.Absent,
    };
}
