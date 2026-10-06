namespace Wjss.Contracts;

/// <summary>
/// The single authoritative presentation projection for one Sensor location. The
/// Runtime folds two Thermocouple channels into this record; the UI never folds
/// and never derives state. Field identities are the accepted 0.2.1A baseline;
/// the VALUES of device/channel/scanOrder fields come from Published
/// Configuration (a later stage) - never from the UI, and never invented here.
/// </summary>
public sealed record SensorPresentationState
{
    public required string SensorId { get; init; }

    /// <summary>Always SENSOR for this record type. A Cannon is never a Sensor.</summary>
    public required SlotType SlotType { get; init; }

    public required Wall Wall { get; init; }
    public required int LogicalColumn { get; init; }
    public required int LogicalRow { get; init; }
    public required int WallColumn { get; init; }
    public required int WallRow { get; init; }

    /// <summary>Configuration-derived scan ordinal (deployment data; synthetic in fixtures/tests only).</summary>
    public required int ScanOrder { get; init; }

    /// <summary>Configuration-derived device identity (synthetic in fixtures/tests only).</summary>
    public required string DeviceId { get; init; }

    /// <summary>Folded Thermocouple source channels (identity strings; configuration-derived).</summary>
    public required string TcFrontChannel { get; init; }

    public required string TcRearChannel { get; init; }

    /// <summary>Current Dirty Score; null when no current value exists.</summary>
    public double? DirtyScore { get; init; }

    /// <summary>Value kept for UNCERTAIN quality: the last validated classification basis.</summary>
    public double? LastValidatedScore { get; init; }

    public string? LastValidatedAt { get; init; }

    public required Classification Classification { get; init; }
    public required ClassificationBasis ClassificationBasis { get; init; }
    public required Quality Quality { get; init; }

    /// <summary>Machine reason code when Quality is not GOOD; presentation maps it to text.</summary>
    public string? QualityReason { get; init; }

    public string? SourceTimestamp { get; init; }

    public required QueueState QueueState { get; init; }
    public required bool IsActiveJobTarget { get; init; }

    public required AlarmState AlarmState { get; init; }
    public AlarmSeverity? AlarmSeverity { get; init; }
}

/// <summary>Per-wall counts for the U-map summaries. No coordinates, no addresses.</summary>
public sealed record WallSummary
{
    public required Wall Wall { get; init; }
    public required int Total { get; init; }
    public required int Dirty { get; init; }
    public required int Cleaner { get; init; }
    public required int NotClassified { get; init; }
    public required int Uncertain { get; init; }
    public double? MaxScore { get; init; }
}
