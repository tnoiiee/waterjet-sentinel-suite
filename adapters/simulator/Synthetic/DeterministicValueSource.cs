namespace Wjss.Adapters.Simulator;

/// <summary>
/// Deterministic pseudo-random value stream for the SIMULATOR profile
/// (splitmix64 over an explicit seed). The stream is integer-only and platform
/// independent: the same seed always produces the same sequence, on every host,
/// with no dependence on <c>System.Random</c> (whose internal algorithm is not a
/// contract) and no static mutable state anywhere.
///
/// The stream supplies synthetic presentation values only. It carries no
/// process meaning, no Production value and no timing constant.
/// </summary>
public sealed class DeterministicValueSource
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;

    /// <summary>2^-53: the reciprocal of the double mantissa radix, so NextUnit() lands in [0, 1).</summary>
    private const double TwoToTheMinus53 = 1.0 / 9007199254740992.0;

    private ulong _state;

    /// <summary>Creates a stream positioned at the given seed.</summary>
    public DeterministicValueSource(SyntheticSeed seed) => _state = seed.Value;

    /// <summary>Next 64-bit value of the stream.</summary>
    public ulong NextUInt64()
    {
        // Unsigned wrap is intended: it is the defining step of the generator.
        _state += Gamma;
        return Mix(_state);
    }

    /// <summary>Next value in [0, 1), with 53 bits of resolution.</summary>
    public double NextUnit() => (NextUInt64() >> 11) * TwoToTheMinus53;

    /// <summary>
    /// Stateless variant: the unit value of one indexed slot for a seed. Used
    /// where a value must depend on the slot identity rather than on stream
    /// position, so re-deriving it (a repeated composition, a test) returns the
    /// same value.
    /// </summary>
    public static double UnitFor(SyntheticSeed seed, int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must not be negative.");
        }

        var mixed = Mix(seed.Value ^ Mix((ulong)index));
        return (mixed >> 11) * TwoToTheMinus53;
    }

    private static ulong Mix(ulong value)
    {
        var mixed = value;
        mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
        mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
        return mixed ^ (mixed >> 31);
    }
}
