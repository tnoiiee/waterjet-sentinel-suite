using System.Text.Json;
using System.Threading;
using Wjss.Contracts;

namespace Wjss.Runtime.Core;

/// <summary>One immutable reader-visible generation. History is newest first.</summary>
public sealed record RuntimePublication
{
    public required RuntimeState Current { get; init; }
    public required IReadOnlyList<RuntimeDelta> Deltas { get; init; }
    public required int HistoryCapacity { get; init; }
    public required long Generation { get; init; }
    public int? NewestDeltaRevision => Deltas.Count == 0 ? null : Deltas[0].Revision;
}

/// <summary>Explicit all-or-nothing publication outcome.</summary>
public sealed record RuntimePublicationResult(bool Accepted, string? Code, RuntimePublication Publication);

/// <summary>
/// Separate CP-3c-1 foundation. CP-3c-2 must replace both legacy Host sources with
/// a single Snapshot read; this store must never be wired alongside them.
/// </summary>
public sealed class RuntimePublicationStore
{
    private RuntimePublication _published;
    private int _writerIssued;
    private readonly object _writeGate = new();

    private RuntimePublicationStore(RuntimePublication initial) => _published = initial;

    public static RuntimePublicationStore Create(RuntimeState initial, int historyCapacity = RuntimeLimits.DefaultDeltaHistoryCapacity)
    {
        ArgumentNullException.ThrowIfNull(initial);
        if (historyCapacity < RuntimeLimits.MinimumDeltaHistoryCapacity || historyCapacity > RuntimeLimits.MaximumDeltaHistoryCapacity)
            throw new ArgumentOutOfRangeException(nameof(historyCapacity));
        RuntimeStateInvariants.RequireValid(initial);
        return new RuntimePublicationStore(new RuntimePublication
        {
            Current = FreezeState(initial),
            Deltas = Array.AsReadOnly(Array.Empty<RuntimeDelta>()),
            HistoryCapacity = historyCapacity,
            Generation = 0,
        });
    }

    /// <summary>Acquire ONCE and read both state and history from this generation.</summary>
    public RuntimePublication Snapshot => Volatile.Read(ref _published);

    public RuntimePublicationWriter CreateWriter()
    {
        if (Interlocked.CompareExchange(ref _writerIssued, 1, 0) != 0)
            throw new InvalidOperationException("Only one publication writer may be issued.");
        return new RuntimePublicationWriter(this);
    }

    internal RuntimePublicationResult Publish(int expectedRevision, RuntimeState candidate, RuntimeDelta delta)
    {
        lock (_writeGate)
        {
            return PublishLocked(expectedRevision, candidate, delta);
        }
    }

    private RuntimePublicationResult PublishLocked(int expectedRevision, RuntimeState candidate, RuntimeDelta delta)
    {
        // The sole writer serializes calls. Never change the published reference until
        // every check and copy has succeeded. Exceptions also leave it unchanged.
        var before = Snapshot;
        RuntimePublicationResult Refuse(string code) => new(false, code, before);
        if (expectedRevision != before.Current.Revision) return Refuse("PUBLICATION_STALE_REVISION");
        if (candidate is null || delta is null) return Refuse("PUBLICATION_NULL_CANDIDATE");
        if (candidate.Revision != expectedRevision + 1 || delta.PreviousRevision != expectedRevision || delta.Revision != candidate.Revision)
            return Refuse("PUBLICATION_REVISION_MISMATCH");
        if (!RuntimeStateInvariants.TryValidate(candidate, out var code, out _)) return Refuse(code);
        if (before.Deltas.Count > 0 && before.Deltas[0].Revision != expectedRevision)
            return Refuse("PUBLICATION_HISTORY_GAP");
        if (delta.Runtime is not null) return Refuse("PUBLICATION_RUNTIME_SECTION");

        try
        {
            // Projection comparison rejects missing, surplus and invented sections,
            // including three-state ActiveJob and paired previous Sensor values.
            var expected = RuntimeDeltaProjector.ProjectCandidate(before.Current, candidate);
            if (!Same(RuntimeDeltaProjector.ProjectWire(expected), RuntimeDeltaProjector.ProjectWire(delta))
                || !Same(expected.PreviousSensors, delta.PreviousSensors))
                return Refuse("PUBLICATION_DELTA_MISMATCH");
            var applied = RuntimeDeltaApply.Apply(before.Current, delta);
            if (!applied.Applied || applied.State is null || !Same(applied.State, candidate))
                return Refuse("PUBLICATION_REPLAY_MISMATCH");

            var frozenState = FreezeState(candidate);
            var frozenDelta = FreezeDelta(delta);
            var entries = new RuntimeDelta[Math.Min(before.HistoryCapacity, before.Deltas.Count + 1)];
            entries[0] = frozenDelta;
            for (var i = 1; i < entries.Length; i++)
                entries[i] = before.Deltas[i - 1];
            for (var i = 1; i < entries.Length; i++)
                if (entries[i - 1].PreviousRevision != entries[i].Revision)
                    return Refuse("PUBLICATION_HISTORY_GAP");
            var next = new RuntimePublication
            {
                Current = frozenState,
                Deltas = Array.AsReadOnly(entries),
                HistoryCapacity = before.HistoryCapacity,
                Generation = checked(before.Generation + 1),
            };
            Volatile.Write(ref _published, next);
            return new RuntimePublicationResult(true, null, next);
        }
        catch (InvalidOperationException)
        {
            return Refuse("PUBLICATION_INVALID_CANDIDATE");
        }
        catch (ArgumentException)
        {
            return Refuse("PUBLICATION_INVALID_CANDIDATE");
        }
    }

    private static bool Same<T>(T left, T right) =>
        JsonSerializer.Serialize(left, ContractJson.Options) == JsonSerializer.Serialize(right, ContractJson.Options);

    private static ActiveCleaningJobState? FreezeJob(ActiveCleaningJobState? job) => job is null ? null : job with
    {
        SafeReturn = job.SafeReturn is null ? null : job.SafeReturn with { Events = RuntimeCollections.Freeze(job.SafeReturn.Events) },
    };

    private static SequenceState FreezeSequence(SequenceState sequence) => sequence with
    {
        LastJobOutcome = sequence.LastJobOutcome is null ? null : sequence.LastJobOutcome with
        {
            Events = RuntimeCollections.Freeze(sequence.LastJobOutcome.Events),
        },
    };

    private static RuntimeState FreezeState(RuntimeState state)
    {
        var frozen = RuntimeStateFreezer.Freeze(state);
        return frozen with
        {
            ActiveJob = FreezeJob(state.ActiveJob),
            Sequence = FreezeSequence(state.Sequence),
        };
    }

    private static RuntimeDelta FreezeDelta(RuntimeDelta delta) => delta with
    {
        ChangedSensors = RuntimeCollections.Freeze(delta.ChangedSensors),
        PreviousSensors = RuntimeCollections.Freeze(delta.PreviousSensors),
        ChangedWalls = RuntimeCollections.Freeze(delta.ChangedWalls),
        Queue = delta.Queue is null ? null : delta.Queue with { Entries = RuntimeCollections.Freeze(delta.Queue.Entries) },
        Sequence = delta.Sequence is null ? null : FreezeSequence(delta.Sequence),
        Alarms = delta.Alarms is null ? null : delta.Alarms with { Items = RuntimeCollections.Freeze(delta.Alarms.Items) },
        Communication = delta.Communication is null ? null : delta.Communication with { Devices = RuntimeCollections.Freeze(delta.Communication.Devices) },
        ActiveJob = delta.ActiveJob with { Value = FreezeJob(delta.ActiveJob.Value) },
        TrendPoint = delta.TrendPoint is null ? null : delta.TrendPoint with { Series = (double?[])delta.TrendPoint.Series.Clone() },
    };
}

/// <summary>Exclusive capability for publishing a state and matching Delta.</summary>
public sealed class RuntimePublicationWriter
{
    private readonly RuntimePublicationStore _store;
    internal RuntimePublicationWriter(RuntimePublicationStore store) => _store = store;
    public RuntimePublicationResult Publish(int expectedRevision, RuntimeState candidate, RuntimeDelta delta) =>
        _store.Publish(expectedRevision, candidate, delta);
}
