namespace Wjss.Contracts;

/// <summary>
/// Alarm presentation projection. Counting and item shape only: the alarm
/// catalogue, thresholds, blocking scopes and routing are Production policy,
/// excluded from Stage 0.3A and pending in ALARM_MODEL.md §10.
/// </summary>
public sealed record AlarmItem
{
    public required string AlarmId { get; init; }
    public required string Code { get; init; }
    public required string Text { get; init; }
    public required AlarmSeverity Severity { get; init; }

    /// <summary>ACTIVE_UNACK | ACTIVE_ACK | CLEARED_UNACK (NONE items do not exist here).</summary>
    public required string State { get; init; }

    public string? SensorId { get; init; }
    public string? DeviceId { get; init; }
    public required string RaisedAt { get; init; }
}

/// <summary>Alarm counters plus the presented items.</summary>
public sealed record AlarmSummary
{
    public required int ActiveUnack { get; init; }
    public required int ActiveAck { get; init; }
    public required int ClearedUnack { get; init; }
    public required IReadOnlyList<AlarmItem> Items { get; init; }
}
