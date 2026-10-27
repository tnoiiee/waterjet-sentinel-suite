namespace Wjss.Runtime.Core.Motion.Manual;

/// <summary>
/// One typed command for a port. There is no text member: the hardware-capable port renders the
/// command through a closed plan inside the adapter, and the simulated port interprets the typed
/// value directly.
/// </summary>
/// <param name="Kind">The closed command kind.</param>
/// <param name="Axis">The closed A/B/C/D axis.</param>
/// <param name="Operand">The typed native integer operand; zero when the kind carries none.</param>
/// <param name="BrakeOutput">The configured brake output (1 to 4) for brake kinds; zero otherwise.</param>
public sealed record ManualMotionPortCommand(
    ManualMotionPortCommandKind Kind,
    ManualMotionAxis Axis,
    int Operand,
    int BrakeOutput);

/// <summary>One typed read for a port.</summary>
/// <param name="Kind">The closed read kind.</param>
/// <param name="Axis">The closed A/B/C/D axis.</param>
/// <param name="BrakeOutput">The configured brake output (1 to 4) for the brake-output read; zero otherwise.</param>
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

/// <summary>
/// The confirmed physical pulse-domain relationship between the two position readbacks of one axis,
/// and the single normalization the Product applies before any comparison.
///
/// Owner-confirmed (2026-10-27): the physical encoder resolution is 10,000 pulses per revolution and
/// the stepping-drive pulses-per-revolution setting is 5,000 pulses per revolution, so the theoretical
/// (encoder) readback has exactly twice the count density of the reference (drive) readback:
/// RawTP = 2 x RawRP. The commissioned Legacy function divided TP by two for exactly this reason, so
/// the division it performed is the normalization this type makes explicit.
///
/// The normalization is the exact rational relationship between the two physical values, never a
/// literal divisor: <c>rawTheoretical * DrivePulsesPerRevolution / EncoderPulsesPerRevolution</c>. A raw
/// observation is never overwritten or mutated; a caller passes a raw value and receives a derived one,
/// so the encoder-domain value and the reference-domain value remain independently available.
/// </summary>
public static class ManualMotionPulseDomain
{
    /// <summary>The Owner-confirmed physical encoder resolution, in pulses per revolution.</summary>
    public const int EncoderPulsesPerRevolution = 10_000;

    /// <summary>The Owner-confirmed stepping-drive pulses-per-revolution setting.</summary>
    public const int DrivePulsesPerRevolution = 5_000;

    /// <summary>
    /// The encoder-domain count density relative to the reference (drive) domain. It is derived from
    /// the two physical values, so it can never drift away from them.
    /// </summary>
    public const int EncoderCountsPerDriveCount = EncoderPulsesPerRevolution / DrivePulsesPerRevolution;

    /// <summary>
    /// Normalizes one raw theoretical (encoder-domain) reading into the reference (drive) domain that
    /// the comparison target and the comparison tolerance are expressed in.
    /// </summary>
    public static decimal NormalizeTheoretical(decimal rawTheoretical) =>
        rawTheoretical * DrivePulsesPerRevolution / EncoderPulsesPerRevolution;

    /// <summary>
    /// Normalizes one raw reference (drive-domain) reading. The identity is written out explicitly so
    /// that "the reference reading is already in the reference domain" is a stated fact of the code
    /// rather than an omission a later reader has to assume.
    /// </summary>
    public static decimal NormalizeReference(decimal rawReference) => rawReference;

    /// <summary>
    /// True when the two normalized values agree with EACH OTHER within the tolerance. This is a
    /// separate question from whether either agrees with the commanded target, and the two are reported
    /// as separate outcomes.
    /// </summary>
    public static bool DomainsAgree(decimal normalizedTheoretical, decimal normalizedReference, int tolerance) =>
        Math.Abs(normalizedTheoretical - normalizedReference) <= tolerance;
}
