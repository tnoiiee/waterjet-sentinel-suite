namespace Wjss.Contracts;

/// <summary>
/// One bounded trend point. A null series value is a DATA GAP and must render
/// as a gap (never interpolated). Point count and window are bounded by the
/// Snapshot-delivered capacity; older points fall off the head.
/// </summary>
public sealed record TrendPoint
{
    /// <summary>Unix epoch seconds.</summary>
    public required long T { get; init; }

    /// <summary>Fixed-width series tuple (four series in the accepted 0.2.1A presentation baseline).</summary>
    public required double?[] Series { get; init; }

    public required double Setpoint { get; init; }
    public required bool JobActive { get; init; }
    public required bool AlarmActive { get; init; }
}

/// <summary>Bounded trend window (Snapshot-delivered; Deltas append one point at a time).</summary>
public sealed record TrendWindow
{
    public required int Capacity { get; init; }
    public required IReadOnlyList<string> SeriesNames { get; init; }
    public required IReadOnlyList<TrendPoint> Points { get; init; }
}
