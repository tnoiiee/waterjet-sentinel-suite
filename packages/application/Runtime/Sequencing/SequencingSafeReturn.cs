namespace Wjss.Runtime.Core.Sequencing;

/// <summary>
/// The Safe Return ledger of one Active Job. Each field holds the kernel
/// evidence sequence at which that Safe Return step was recorded, or null when
/// the step has not happened. Only the kernel writes these fields. A ledger
/// entry is never supplied from outside.
/// </summary>
public sealed record SafeReturnLedger(
    int? WaterOffSeq,
    int? ValveCloseRequestSeq,
    int? ValveClosedSeq,
    int? AxisReturnRequestSeq,
    int? AxisStandbySeq,
    int? FailureSeq)
{
    /// <summary>A ledger with no recorded step.</summary>
    public static SafeReturnLedger Empty { get; } = new(null, null, null, null, null, null);

    /// <summary>True when no Safe Return step has been recorded.</summary>
    public bool IsEmpty =>
        WaterOffSeq is null
        && ValveCloseRequestSeq is null
        && ValveClosedSeq is null
        && AxisReturnRequestSeq is null
        && AxisStandbySeq is null
        && FailureSeq is null;
}
