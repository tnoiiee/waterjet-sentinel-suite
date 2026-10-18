namespace Wjss.Runtime.Core.Motion.Manual;

/// <summary>
/// One typed command for a port. There is no text member: the hardware-capable port renders the
/// command through a closed plan inside the adapter, and the simulated port interprets the typed
/// value directly.
/// </summary>
/// <param name="Kind">The closed command kind.</param>
/// <param name="Axis">The closed A/B axis.</param>
/// <param name="Operand">The typed native integer operand; zero when the kind carries none.</param>
/// <param name="BrakeOutput">The configured brake output (1 or 2) for brake kinds; zero otherwise.</param>
public sealed record ManualMotionPortCommand(
    ManualMotionPortCommandKind Kind,
    ManualMotionAxis Axis,
    int Operand,
    int BrakeOutput);

/// <summary>One typed read for a port.</summary>
/// <param name="Kind">The closed read kind.</param>
/// <param name="Axis">The closed A/B axis.</param>
/// <param name="BrakeOutput">The configured brake output (1 or 2) for the brake-output read; zero otherwise.</param>
public sealed record ManualMotionPortRead(
    ManualMotionReadKind Kind,
    ManualMotionAxis Axis,
    int BrakeOutput);

/// <summary>
/// The redacted result of one port command. <see cref="Accepted"/> means only that one dispatch
/// completed without a classified refusal; it never means the commanded effect was observed.
/// </summary>
/// <param name="Accepted">True when the single dispatch completed without refusal.</param>
/// <param name="Fault">True when the port reported a communication or native fault.</param>
/// <param name="NativeAttempted">True when a native (or simulated) call was made.</param>
/// <param name="Code">A canonical redacted code.</param>
/// <param name="PortGeneration">The port generation the result belongs to.</param>
public sealed record ManualMotionPortCommandResult(
    bool Accepted,
    bool Fault,
    bool NativeAttempted,
    string Code,
    long PortGeneration);

/// <summary>
/// The redacted result of one port read. A refused read carries no value: nothing is fabricated.
/// </summary>
/// <param name="Succeeded">True when a bounded, validated value was read.</param>
/// <param name="Fault">True when the port reported a communication or native fault.</param>
/// <param name="Value">The native value; meaningful only when <see cref="Succeeded"/> is true.</param>
/// <param name="Code">A canonical redacted code.</param>
/// <param name="PortGeneration">The port generation the result belongs to.</param>
public sealed record ManualMotionPortReadResult(
    bool Succeeded,
    bool Fault,
    decimal Value,
    string Code,
    long PortGeneration);

/// <summary>The redacted result of the single port open.</summary>
/// <param name="Opened">True when the port is open.</param>
/// <param name="Code">A canonical redacted code.</param>
/// <param name="PortGeneration">The port generation after the open.</param>
public sealed record ManualMotionPortOpenResult(
    bool Opened,
    string Code,
    long PortGeneration);

/// <summary>The redacted result of the port close. Every flag is the measured truth.</summary>
/// <param name="BoundariesInvalidated">True when every command and read boundary was invalidated before the close.</param>
/// <param name="HandleClosed">True when the controller handle was closed (always true for the simulated port).</param>
/// <param name="ModulesReleased">True when every native module was released (always true for the simulated port).</param>
/// <param name="Code">A canonical redacted code.</param>
public sealed record ManualMotionPortCloseResult(
    bool BoundariesInvalidated,
    bool HandleClosed,
    bool ModulesReleased,
    string Code);

/// <summary>
/// The single seam between the pure Application session and a controller (or its simulation).
///
/// Contract:
///   - <see cref="Open"/> is called at most once per port instance; a second call is refused and
///     nothing reconnects;
///   - every member is called from one session driver thread, one call at a time;
///   - no member accepts text, a buffer, a handle or a command string;
///   - <see cref="Close"/> is idempotent and invalidates every boundary before any native close.
/// </summary>
public interface IManualMotionPort
{
    /// <summary>The unmistakable identity of the port.</summary>
    ManualMotionPortMode Mode { get; }

    /// <summary>Opens the port once.</summary>
    ManualMotionPortOpenResult Open();

    /// <summary>Executes exactly one typed command. Never retried by the port.</summary>
    ManualMotionPortCommandResult Execute(ManualMotionPortCommand command);

    /// <summary>Performs exactly one typed read. Never retried by the port.</summary>
    ManualMotionPortReadResult Read(ManualMotionPortRead read);

    /// <summary>Invalidates, closes and releases. Idempotent.</summary>
    ManualMotionPortCloseResult Close();
}
