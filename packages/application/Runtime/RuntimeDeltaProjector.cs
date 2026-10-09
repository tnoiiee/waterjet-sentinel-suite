using System.Text.Json;
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

        RequireNextStep(previous, candidate);


        return new RuntimeDelta
        {
            PreviousRevision = previous.Revision,
            Revision = candidate.Revision,
            GeneratedAtUtc = candidate.GeneratedAtUtc,
            ChangedSensors = RuntimeCollections.Freeze(evolution.ChangedSensors),
            PreviousSensors = RuntimeCollections.Freeze(evolution.PreviousSensors),
            ChangedWalls = RuntimeCollections.Freeze(ChangedWalls(previous.Walls, candidate.Walls)),
            Pump = SameContent(candidate.Pump, previous.Pump) ? null : candidate.Pump,
            Queue = SameContent(candidate.Queue, previous.Queue) ? null : candidate.Queue,
            Sequence = SameContent(candidate.Sequence, previous.Sequence) ? null : candidate.Sequence,
            Alarms = SameContent(candidate.Alarms, previous.Alarms) ? null : candidate.Alarms,
            Communication = SameContent(candidate.Communication, previous.Communication) ? null : candidate.Communication,
            TrendPoint = evolution.AppendedTrendPoint,
            ActiveJob = EncodeActiveJob(previous.ActiveJob, candidate.ActiveJob),
        };
    }

    /// <summary>
    /// CP-3b pure candidate path: projects the transition from <paramref name="previous"/> to a candidate revision
    /// built by the caller, without a synthetic evolution. The Sensors must keep the same identities in the same
    /// order, because a Sensor topology change is not a Delta and is refused. No trend point is appended, so
    /// <c>TrendPoint</c> is absent. The whole-record rules are the same as in <see cref="Project"/>.
    /// </summary>
    public static RuntimeDelta ProjectCandidate(RuntimeState previous, RuntimeState candidate)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);
        RequireNextStep(previous, candidate);

        if (previous.Sensors.Count != candidate.Sensors.Count)
        {
            throw new InvalidOperationException(
                "Refused to project a Delta: the Sensor identity set changed. Nothing was projected.");
        }

        var changedSensors = new List<SensorPresentationState>();
        var previousSensors = new List<SensorPresentationState>();
        for (var index = 0; index < candidate.Sensors.Count; index++)
        {
            var before = previous.Sensors[index];
            var next = candidate.Sensors[index];
            if (!string.Equals(before.SensorId, next.SensorId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refused to project a Delta: the Sensor order changed. Nothing was projected.");
            }

            if (!SameContent(before, next))
            {
                changedSensors.Add(next);
                previousSensors.Add(before);
            }
        }

        return new RuntimeDelta
        {
            PreviousRevision = previous.Revision,
            Revision = candidate.Revision,
            GeneratedAtUtc = candidate.GeneratedAtUtc,
            ChangedSensors = RuntimeCollections.Freeze(changedSensors),
            PreviousSensors = RuntimeCollections.Freeze(previousSensors),
            ChangedWalls = RuntimeCollections.Freeze(ChangedWalls(previous.Walls, candidate.Walls)),
            Pump = SameContent(candidate.Pump, previous.Pump) ? null : candidate.Pump,
            Queue = SameContent(candidate.Queue, previous.Queue) ? null : candidate.Queue,
            Sequence = SameContent(candidate.Sequence, previous.Sequence) ? null : candidate.Sequence,
            Alarms = SameContent(candidate.Alarms, previous.Alarms) ? null : candidate.Alarms,
            Communication = SameContent(candidate.Communication, previous.Communication) ? null : candidate.Communication,
            TrendPoint = AppendedTrendPoint(previous.Trend, candidate.Trend),
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

    /// <summary>
    /// Carries one appended candidate trend point: below capacity, one append with the existing history intact;
    /// at capacity, exactly one oldest eviction followed by one append with the retained suffix intact.
    /// An unchanged window carries nothing. All other changes are refused.
    /// </summary>
    private static TrendPoint? AppendedTrendPoint(TrendWindow previous, TrendWindow candidate)
    {
        if (previous.Capacity != candidate.Capacity || !SameContent(previous.SeriesNames, candidate.SeriesNames))
        {
            throw new InvalidOperationException("Refused to project a Delta: the trend window identity changed. Nothing was projected.");
        }

        var before = previous.Points.Count;
        var after = candidate.Points.Count;
        if (after == before && SameContent(previous.Points, candidate.Points))
        {
            return null;
        }

        if (before < candidate.Capacity)
        {
            if (after != before + 1)
            {
                throw new InvalidOperationException("Refused to project a Delta: expected exactly one trend append.");
            }
            for (var index = 0; index < before; index++)
            {
                if (!SameContent(previous.Points[index], candidate.Points[index]))
                {
                    throw new InvalidOperationException("Refused to project a Delta: trend history changed.");
                }
            }
            return candidate.Points[before];
        }

        if (after != before)
        {
            throw new InvalidOperationException("Refused to project a Delta: full trend window requires one eviction and one append.");
        }
        for (var index = 1; index < before; index++)
        {
            if (!SameContent(previous.Points[index], candidate.Points[index - 1]))
            {
                throw new InvalidOperationException("Refused to project a Delta: full trend window did not shift exactly once.");
            }
        }
        // A full window can be unchanged; that case returned above. A single-slot window
        // has no retained predecessor to compare, but its replacement is still one append.
        return candidate.Points[after - 1];
    }

    private static void RequireNextStep(RuntimeState previous, RuntimeState candidate)
    {
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
    }

    /// <summary>
    /// Content equality on the canonical contract JSON. Record Equals compares collection members by reference, so a
    /// rebuilt but identical Queue, Sequence or Active Job would otherwise emit a spurious section.
    /// </summary>
    private static bool SameContent<T>(T left, T right) =>
        JsonSerializer.Serialize(left, ContractJson.Options) == JsonSerializer.Serialize(right, ContractJson.Options);

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

        return previous is null || !SameContent(candidate, previous)
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
