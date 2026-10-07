namespace Wjss.Runtime.Core;

/// <summary>
/// Bounded-buffer and window limits of the Runtime state foundation. Every
/// in-memory buffer in the Runtime is bounded by a constant declared here, so
/// "no unbounded buffers" is a single reviewable place rather than a property
/// scattered across components. These are implementation bounds of the
/// presentation/diagnostics state; they are not Production process values.
/// </summary>
public static class RuntimeLimits
{
    /// <summary>Revision-activity entries retained per Runtime State Store instance.</summary>
    public const int DefaultRevisionHistoryCapacity = 64;

    /// <summary>Smallest accepted revision-history capacity.</summary>
    public const int MinimumRevisionHistoryCapacity = 1;

    /// <summary>Largest accepted revision-history capacity (a bounded buffer cannot be unbounded by request).</summary>
    public const int MaximumRevisionHistoryCapacity = 1024;

    /// <summary>Trend points retained in the Runtime trend window.</summary>
    public const int DefaultTrendCapacity = 600;

    /// <summary>Largest accepted trend capacity.</summary>
    public const int MaximumTrendCapacity = 3600;

    /// <summary>
    /// Fixed series width of one trend point (accepted 0.2.1A presentation
    /// baseline: four series). A point with a different width is refused, so the
    /// UI never sees a ragged window.
    /// </summary>
    public const int TrendSeriesCount = 4;
}
