namespace Wjss.Contracts;

/// <summary>
/// Main Pump presentation projection. The Pump is a High Critical Device
/// (Owner critical Pump decision). Setpoint/ready-band VALUES are Published
/// Configuration (deployment data); in fixtures and tests they are always
/// labelled synthetic. This record carries identity and units only.
/// </summary>
public sealed record PumpState
{
    public required PumpRunState State { get; init; }

    public double? Pressure { get; init; }
    public required double Setpoint { get; init; }
    public required double ReadyBandLow { get; init; }
    public required double ReadyBandHigh { get; init; }
    public required bool Ready { get; init; }
    public string? StopRequestedAt { get; init; }
}
