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
/// The per-axis pulse-domain and mechanical configuration of ONE axis, and the single conversion the
/// Product applies before any comparison.
///
/// NOTHING here is a Product constant. Every value comes from the axis profile of the Owner-local
/// Motion Configuration, because they are flexible per-axis engineering settings: two axes of the same
/// cabinet may legitimately differ. A missing or nonsensical value makes the axis's conversion
/// unavailable, and an axis whose conversion is unavailable refuses every motion operation that would
/// require a comparison rather than assuming a value.
///
/// The dimensional model, in the exact terms the commissioned Legacy Source used:
///
///   millimetresPerMotorRevolution = mechanicalGearRatio x pitchMillimetresPerRevolution
///   paCommandUnitsPerMillimetre   = drivePulsesPerRevolution / millimetresPerMotorRevolution
///   rpFeedbackUnitsPerMillimetre  = drivePulsesPerRevolution / millimetresPerMotorRevolution
///   tpFeedbackUnitsPerMillimetre  = encoderPulsesPerRevolution / millimetresPerMotorRevolution
///   tpNormalizationRatio          = drivePulsesPerRevolution / encoderPulsesPerRevolution
///
/// The Legacy formulas are these quantities, restated:
///   command = mm / (ratio / pulsesRev) / pitch   is  mm x paCommandUnitsPerMillimetre
///   rpMm    = counts x (ratio / pulsesRev) x pitch is  counts / rpFeedbackUnitsPerMillimetre
///   tpMm    = counts x (ratio / pulsesRev) x pitch / 2 is counts / tpFeedbackUnitsPerMillimetre
///
/// The theoretical (encoder) readback carries a different count density from the reference (drive)
/// readback, so it is normalized into the reference domain before it is ever compared. Raw
/// observations are never overwritten or mutated: a caller passes a raw value and receives a derived
/// one, so both domains stay independently available.
/// </summary>
public readonly record struct ManualMotionPulseDomain
{
    private ManualMotionPulseDomain(
        int encoderPulsesPerRevolution,
        int drivePulsesPerRevolution,
        decimal mechanicalGearRatio,
        decimal pitchMillimetresPerRevolution)
    {
        EncoderPulsesPerRevolution = encoderPulsesPerRevolution;
        DrivePulsesPerRevolution = drivePulsesPerRevolution;
        MechanicalGearRatio = mechanicalGearRatio;
        PitchMillimetresPerRevolution = pitchMillimetresPerRevolution;
    }

    /// <summary>The encoder counts produced by one motor revolution, from the axis profile.</summary>
    public int EncoderPulsesPerRevolution { get; }

    /// <summary>The drive pulses consumed by one motor revolution, from the axis profile.</summary>
    public int DrivePulsesPerRevolution { get; }

    /// <summary>
    /// The mechanical reduction expressed as OUTPUT revolutions per MOTOR revolution, from the axis
    /// profile. 0.1 is a 1:10 reduction: ten motor revolutions per one output revolution.
    /// </summary>
    public decimal MechanicalGearRatio { get; }

    /// <summary>The travel, in millimetres, produced by one OUTPUT revolution, from the axis profile.</summary>
    public decimal PitchMillimetresPerRevolution { get; }

    /// <summary>Travel in millimetres produced by one motor revolution.</summary>
    public decimal MillimetresPerMotorRevolution => MechanicalGearRatio * PitchMillimetresPerRevolution;

    /// <summary>The derived read-only ratio that normalizes a theoretical reading into the drive domain.</summary>
    public decimal TpNormalizationRatio => (decimal)DrivePulsesPerRevolution / EncoderPulsesPerRevolution;

    /// <summary>The derived command unit count a millimetre of travel corresponds to.</summary>
    public decimal PaCommandUnitsPerMillimetre => DrivePulsesPerRevolution / MillimetresPerMotorRevolution;

    /// <summary>The derived reference (drive) feedback count a millimetre of travel corresponds to.</summary>
    public decimal RpFeedbackUnitsPerMillimetre => DrivePulsesPerRevolution / MillimetresPerMotorRevolution;

    /// <summary>The derived theoretical (encoder) feedback count a millimetre of travel corresponds to.</summary>
    public decimal TpFeedbackUnitsPerMillimetre => EncoderPulsesPerRevolution / MillimetresPerMotorRevolution;

    /// <summary>
    /// True when the command unit and the reference feedback unit are the same count density. When they
    /// are, the commanded target needs no further conversion to be compared against the reference
    /// readback. When they are not, the two densities differ and the difference is reported rather than
    /// silently absorbed by a multiplier.
    /// </summary>
    public bool CommandAndReferenceShareCountDensity => PaCommandUnitsPerMillimetre == RpFeedbackUnitsPerMillimetre;

    /// <summary>
    /// The factor that converts a commanded count into a reference-feedback count. It is 1 whenever the
    /// two densities agree, and it is derived from the profile, never guessed and never literal.
    /// </summary>
    public decimal ReferenceCountsPerCommandCount =>
        PaCommandUnitsPerMillimetre == 0m ? 0m : RpFeedbackUnitsPerMillimetre / PaCommandUnitsPerMillimetre;

    /// <summary>
    /// Normalizes one raw theoretical (encoder-domain) reading into the reference (drive) domain that
    /// the comparison target and the comparison tolerance are expressed in.
    /// </summary>
    public decimal NormalizeTheoretical(decimal rawTheoretical) =>
        rawTheoretical * DrivePulsesPerRevolution / EncoderPulsesPerRevolution;

    /// <summary>
    /// Normalizes one raw reference (drive-domain) reading. The identity is written out explicitly so
    /// that "the reference reading is already in the reference domain" is a stated fact of the code
    /// rather than an omission a later reader has to assume.
    /// </summary>
    public static decimal NormalizeReference(decimal rawReference) => rawReference;

    /// <summary>True when the target of a command count converts into the reference domain exactly.</summary>
    public bool TryConvertCommandTargetToReference(int commandTarget, out decimal referenceTarget)
    {
        referenceTarget = commandTarget * ReferenceCountsPerCommandCount;
        return CommandAndReferenceShareCountDensity;
    }

    /// <summary>
    /// Builds the domain of one axis from its profile values. Any missing, zero, negative or
    /// nonsensical combination refuses the whole conversion: the caller then refuses the motion
    /// operation that would have needed it.
    /// </summary>
    public static bool TryCreate(
        int? encoderPulsesPerRevolution,
        int? drivePulsesPerRevolution,
        decimal? mechanicalGearRatio,
        decimal? pitchMillimetresPerRevolution,
        out ManualMotionPulseDomain domain)
    {
        domain = default;

        if (encoderPulsesPerRevolution is not > 0
            || drivePulsesPerRevolution is not > 0
            || mechanicalGearRatio is not > 0m
            || pitchMillimetresPerRevolution is not > 0m)
        {
            return false;
        }

        var millimetresPerMotorRevolution = mechanicalGearRatio.Value * pitchMillimetresPerRevolution.Value;
        if (millimetresPerMotorRevolution <= 0m)
        {
            return false;
        }

        domain = new ManualMotionPulseDomain(
            encoderPulsesPerRevolution.Value,
            drivePulsesPerRevolution.Value,
            mechanicalGearRatio.Value,
            pitchMillimetresPerRevolution.Value);
        return true;
    }

    /// <summary>
    /// True when the two normalized values agree with EACH OTHER within the tolerance. This is a
    /// separate question from whether either agrees with the commanded target, and the two are reported
    /// as separate outcomes.
    /// </summary>
    public static bool DomainsAgree(decimal normalizedTheoretical, decimal normalizedReference, decimal tolerance) =>
        Math.Abs(normalizedTheoretical - normalizedReference) <= tolerance;
}
