namespace Wjss.Time;

/// <summary>
/// Injectable clock (ADR-0012 determinism rule). Every timing-sensitive Runtime
/// component must take <see cref="IClock"/> instead of reading wall time
/// directly, so 0.3A-2 tests can advance time deterministically.
/// </summary>
public interface IClock
{
    /// <summary>Current UTC instant.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production clock: the operating-system UTC time. No caching, no drift logic.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Adapts the one injected <see cref="IClock"/> to <see cref="TimeProvider"/> so a component that takes
/// a <see cref="TimeProvider"/> consumes the SAME time source as the composition that built it. There is
/// no second clock: this adapter reads the injected clock and never the wall clock.
/// </summary>
public sealed class ClockTimeProvider : TimeProvider
{
    private readonly IClock _clock;

    public ClockTimeProvider(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <summary>The injected instant. Never a cached value and never a wall-clock fallback.</summary>
    public override DateTimeOffset GetUtcNow() => _clock.UtcNow;
}
