using Wjss.Contracts;

namespace Wjss.Runtime.Core;

/// <summary>
/// The single append rule of the bounded Runtime trend window, shared by
/// synthetic evolution and by Delta application so both paths evict identically.
/// The published <see cref="TrendWindow"/> and its points are never mutated: the
/// rule returns a new window, and the oldest point is dropped first when the
/// window is at capacity.
/// </summary>
internal static class RuntimeTrendBuffer
{
    /// <summary>Appends one point, dropping the oldest point when the window is full.</summary>
    internal static TrendWindow Append(TrendWindow window, TrendPoint point)
    {
        var capacity = window.Capacity;
        var keepFrom = window.Points.Count >= capacity ? window.Points.Count - capacity + 1 : 0;
        var points = new List<TrendPoint>(window.Points.Count - keepFrom + 1);

        for (var index = keepFrom; index < window.Points.Count; index++)
        {
            points.Add(window.Points[index]);
        }

        points.Add(point);

        return new TrendWindow
        {
            Capacity = capacity,
            SeriesNames = window.SeriesNames,
            Points = points,
        };
    }
}
