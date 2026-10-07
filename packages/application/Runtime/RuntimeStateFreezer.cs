using System.Collections.ObjectModel;
using Wjss.Contracts;

namespace Wjss.Runtime.Core;

/// <summary>
/// Deep copy of the mutable collection surface of one state, applied at the
/// single ingest point of the Runtime State Store (initial publish and every
/// commit). After freezing, the caller retains no reachable mutable state and a
/// reader of the published revision cannot mutate a collection even by casting
/// the read-only wrapper back to a mutable interface.
///
/// Internal by design: this is the store's implementation guarantee, not a
/// consumer API. The state records themselves are immutable, so only the lists
/// (and the trend point series arrays) are copied.
/// </summary>
internal static class RuntimeStateFreezer
{
    internal static RuntimeState Freeze(RuntimeState state) => state with
    {
        WallMap = RuntimeCollections.Freeze(state.WallMap),
        Sensors = RuntimeCollections.Freeze(state.Sensors),
        Walls = RuntimeCollections.Freeze(state.Walls),
        Queue = state.Queue with { Entries = RuntimeCollections.Freeze(state.Queue.Entries) },
        Alarms = state.Alarms with { Items = RuntimeCollections.Freeze(state.Alarms.Items) },
        Communication = state.Communication with { Devices = RuntimeCollections.Freeze(state.Communication.Devices) },
        Trend = state.Trend with
        {
            SeriesNames = RuntimeCollections.Freeze(state.Trend.SeriesNames),
            Points = FreezePoints(state.Trend.Points),
        },
    };

    private static ReadOnlyCollection<TrendPoint> FreezePoints(IReadOnlyList<TrendPoint> points)
    {
        var copies = new TrendPoint[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            copies[index] = point with { Series = (double?[])point.Series.Clone() };
        }

        return Array.AsReadOnly(copies);
    }
}
