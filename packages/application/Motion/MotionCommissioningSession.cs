using Wjss.Contracts;

namespace Wjss.Runtime.Core.Motion;

/// <summary>
/// One manual motion commissioning session.
///
/// A session is the only context in which a motion intent may be evaluated against hardware. While
/// the authorization gate is closed no session can be opened, so the session of this revision is
/// always in <see cref="MotionVocabulary.SessionStateNoSession"/>: it owns no controller, it holds
/// no command in flight and it carries no command record.
///
/// The command record is session scoped and is never persisted: this revision records nothing, so
/// <see cref="Evaluations"/> is empty. The later execution gate owns the decision to record, and it
/// owns the serialization rule (at most one motion command in flight per controller) that this
/// session will enforce once commands can be authorized.
/// </summary>
public sealed class MotionCommissioningSession
{
    /// <summary>The session state. NO SESSION: the gate is closed, so none was opened.</summary>
    public string State => MotionVocabulary.SessionStateNoSession;

    /// <summary>False while the gate is closed.</summary>
    public bool IsOpen => false;

    /// <summary>The axes this session owns. None: no session is open.</summary>
    public int OwnedAxisCount => 0;

    /// <summary>
    /// The command record of this session. Empty and session scoped: nothing is persisted, so a
    /// restart carries no history and no history can be replayed as authorization.
    /// </summary>
    public IReadOnlyList<MotionCommandEvaluation> Evaluations => Array.Empty<MotionCommandEvaluation>();

    /// <summary>
    /// Attempts to open a session. Always refused while the gate is closed; the refusal carries the
    /// gate reason first and then the standing preconditions, in the canonical order.
    /// </summary>
    public bool TryOpen(out IReadOnlyList<string> refusalReasons)
    {
        refusalReasons = MotionVocabulary.AllRefusalReasons;
        return false;
    }

    /// <summary>
    /// Evaluates one motion intent inside this session. While the gate is closed the intent is
    /// refused exactly as it is outside a session: an open session is a precondition of execution,
    /// never a way around the gate.
    /// </summary>
    public MotionCommandEvaluation Evaluate(MotionCommandIntent intent) => MotionCommandGate.Evaluate(intent);

    /// <summary>
    /// Evaluates Stop on every axis of every controller (sixteen evaluations). The explicit Stop
    /// path exists independently of the gate and is refused here for the same reason: no controller
    /// is connected, so no stop command can be delivered. It is never reported as success.
    /// </summary>
    public IReadOnlyList<MotionCommandEvaluation> StopAllAxes() =>
        MotionCommandGate.EvaluateForAllAxes(MotionVocabulary.IntentStop);
}
