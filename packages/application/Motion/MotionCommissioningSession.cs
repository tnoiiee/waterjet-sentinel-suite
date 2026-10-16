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
/// The type is STATIC because that is the truth of this revision, not a convenience. A session that
/// can never be opened has no instance state to carry: every member below is either a constant of
/// the closed gate or a delegation to <see cref="MotionCommandGate"/>, so there is no field, no
/// constructor and no record for an instance to hold. No instance state was invented here to make
/// the shape look object oriented, and none may be: a session that recorded evaluations would
/// contradict the boundary above, because this revision records nothing and a restart must carry no
/// history that could be replayed as authorization. The static shape is also the shape the rest of
/// the closed-gate surface already uses: <see cref="MotionAuthorizationGate"/> and
/// <see cref="MotionCommandGate"/> are static types.
///
/// When the later execution gate opens, a session becomes a real object that owns axes and a command
/// record. That revision instantiates this type again, under the separate G2/G3 authorization, and
/// it owns the serialization rule (at most one motion command in flight per controller) that the
/// session will enforce once commands can be authorized. The fail-closed behaviour does not change
/// until then: every member refuses, and nothing is opened, recorded or issued.
/// </summary>
public static class MotionCommissioningSession
{
    /// <summary>The session state. NO SESSION: the gate is closed, so none was opened.</summary>
    public static string State => MotionVocabulary.SessionStateNoSession;

    /// <summary>False while the gate is closed.</summary>
    public static bool IsOpen => false;

    /// <summary>The axes this session owns. None: no session is open.</summary>
    public static int OwnedAxisCount => 0;

    /// <summary>
    /// The command record of this session. Empty and session scoped: nothing is persisted, so a
    /// restart carries no history and no history can be replayed as authorization.
    /// </summary>
    public static IReadOnlyList<MotionCommandEvaluation> Evaluations => Array.Empty<MotionCommandEvaluation>();

    /// <summary>
    /// Attempts to open a session. Always refused while the gate is closed; the refusal carries the
    /// gate reason first and then the standing preconditions, in the canonical order.
    /// </summary>
    public static bool TryOpen(out IReadOnlyList<string> refusalReasons)
    {
        refusalReasons = MotionVocabulary.AllRefusalReasons;
        return false;
    }

    /// <summary>
    /// Evaluates one motion intent inside this session. While the gate is closed the intent is
    /// refused exactly as it is outside a session: an open session is a precondition of execution,
    /// never a way around the gate.
    /// </summary>
    public static MotionCommandEvaluation Evaluate(MotionCommandIntent intent) => MotionCommandGate.Evaluate(intent);

    /// <summary>
    /// Evaluates Stop on every axis of every controller (sixteen evaluations). The explicit Stop
    /// path exists independently of the gate and is refused here for the same reason: no controller
    /// is connected, so no stop command can be delivered. It is never reported as success.
    /// </summary>
    public static IReadOnlyList<MotionCommandEvaluation> StopAllAxes() =>
        MotionCommandGate.EvaluateForAllAxes(MotionVocabulary.IntentStop);
}
