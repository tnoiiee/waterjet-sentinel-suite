namespace Wjss.Adapters.Simulator;

/// <summary>
/// Explicit determinism seed of the SIMULATOR source. A seed is an identity of
/// the synthetic value stream, not a process value: two runs with the same seed,
/// the same clock instant and the same committed events produce identical
/// synthetic values, which is what makes the SIMULATOR profile reviewable.
///
/// The seed is passed explicitly by the composition root; nothing in the
/// simulator reads a hidden global seed, so no state is shared between tests.
/// </summary>
public readonly record struct SyntheticSeed(ulong Value)
{
    /// <summary>
    /// The seed the Runtime uses when no other seed is supplied. It is a
    /// synthetic identity chosen for the 0.3A-2 foundation, not a Production
    /// constant.
    /// </summary>
    public static SyntheticSeed DefaultSeed { get; } = new(20261007UL);
}
