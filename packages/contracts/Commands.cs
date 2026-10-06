namespace Wjss.Contracts;

/// <summary>
/// Control-request envelope: the UI submits REQUESTS only; validation and
/// execution authority live in the Runtime (later checkpoints). Requests are
/// fire-once from the consumer side: neither the UI nor the API queues or
/// replays them (accepted reconnect rule). The command vocabulary and the
/// authorization rules are NOT frozen here: accepted reason-code names exist
/// only for the refusal envelope below.
/// </summary>
public sealed record CommandRequest
{
    public required string Command { get; init; }

    /// <summary>Flat string parameters for presentation-level commands; null when the command takes none.</summary>
    public IReadOnlyDictionary<string, string>? Params { get; init; }
}

/// <summary>
/// Accept/refusal outcome envelope for one command submission. Refusal reason
/// codes are machine strings (e.g. ACTIVE_JOB_PRESENT, CRITICAL_SUSPENDED,
/// RUNTIME_NOT_IMPLEMENTED); the Production authority policy behind which
/// commands exist at all is an open Owner decision and is not encoded here.
/// </summary>
public sealed record CommandOutcome
{
    public required bool Accepted { get; init; }
    public required string Command { get; init; }

    /// <summary>Machine reason code when refused; null when accepted.</summary>
    public string? ReasonCode { get; init; }

    public string? Detail { get; init; }
}

/// <summary>
/// Shell close-guard evaluation (operational usability control; NEVER a safety
/// protection - fixed wording). Evaluation ownership is a Runtime concern in
/// later checkpoints; the shape is fixed now so the shell and the UI agree.
/// </summary>
public sealed record CloseRequestEvaluation
{
    public required bool Allowed { get; init; }

    /// <summary>Machine reasons, e.g. ACTIVE_JOB, PUMP_RUNNING.</summary>
    public required IReadOnlyList<string> Reasons { get; init; }

    public required string Note { get; init; }
}
