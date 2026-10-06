namespace Wjss.Time;

/// <summary>
/// Manual clock for deterministic tests. Time advances only when the test
/// advances it. Not thread-safe by contract: single-writer tests call it from
/// one thread; concurrent use is a test bug, not a supported mode.
/// </summary>
public sealed class TestClock : IClock
{
    private DateTimeOffset _now;

    public TestClock()
        : this(new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero))
    {
    }

    public TestClock(DateTimeOffset start) => _now = start;

    public DateTimeOffset UtcNow => _now;

    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "TestClock never moves backwards.");
        }

        _now = _now.Add(delta);
    }

    public void Set(DateTimeOffset instant) => _now = instant;
}
