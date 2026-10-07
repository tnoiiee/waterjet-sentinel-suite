using System.Collections.ObjectModel;

namespace Wjss.Runtime.Core;

/// <summary>
/// Collection freezing used at the single ingest point of the Runtime State
/// Store. Every published collection is a fresh copy wrapped by
/// <see cref="ReadOnlyCollection{T}"/>: the caller keeps no reachable mutable
/// state, and a reader of the published revision cannot mutate the collection
/// even by casting. Internal by design - this is an implementation guarantee of
/// the store, not a consumer API.
/// </summary>
internal static class RuntimeCollections
{
    /// <summary>Copies the sequence and returns a read-only wrapper over the copy.</summary>
    internal static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> source)
    {
        var copy = new T[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            copy[index] = source[index];
        }

        return Array.AsReadOnly(copy);
    }
}
