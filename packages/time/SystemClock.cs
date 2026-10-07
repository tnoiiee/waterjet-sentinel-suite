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
