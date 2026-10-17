namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The closed axis domain of the C1 Stop-only software foundation: exactly Axis A (Horizontal) and
/// Axis B (Vertical), Owner-approved for both WSB1 axes.
///
/// The enumeration starts at 1 on purpose. A default-initialized value (0) is not a member, so an
/// empty or defaulted axis is refused instead of silently becoming Axis A. Every other value is
/// refused as well, including values that would represent Axis C, Axis D, a combined A+B value or
/// a multi-axis Stop. No Flags attribute is applied, so the enumeration cannot express a
/// combination.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal enum MotionStopAxis
{
    /// <summary>Axis A: the Horizontal axis.</summary>
    A = 1,

    /// <summary>Axis B: the Vertical axis.</summary>
    B = 2,
}

/// <summary>
/// The only place in the adapter where a C1 Stop command-language template exists.
///
/// Templates are chosen from the closed <see cref="MotionStopAxis"/> domain by a switch with no
/// fallback text. There is no method that accepts caller text, a suffix, a prefix, a separator or
/// a numeric axis code, so no other command text can be formed from this plan.
///
/// Evidence boundary. The template syntax follows the family-level B140 command-language evidence
/// (`ST nnnnnn` with the axis letter after a single space). It is not firmware-specific evidence
/// for DMC-B140-M or DMCB140 Rev 1.0c2, and it is not a claim that a real Controller accepted the
/// Stop. Dispatch success never implies a confirmed stop; see <see cref="MotionStopOutcomeCodes"/>.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal static class MotionStopCommandPlan
{
    /// <summary>The fixed size of the reply buffer for one native Stop dispatch.</summary>
    internal const int MaximumReplyBytes = 32;

    /// <summary>True only for the two members of the closed axis domain.</summary>
    internal static bool IsDefined(MotionStopAxis axis) =>
        axis == MotionStopAxis.A || axis == MotionStopAxis.B;

    /// <summary>
    /// Resolves the exact template for one axis. Returns false, with an empty template, for any
    /// value outside the closed domain.
    /// </summary>
    internal static bool TryGetTemplate(MotionStopAxis axis, out string template)
    {
        switch (axis)
        {
            case MotionStopAxis.A:
                template = "ST A";
                return true;
            case MotionStopAxis.B:
                template = "ST B";
                return true;
            default:
                template = string.Empty;
                return false;
        }
    }
}
