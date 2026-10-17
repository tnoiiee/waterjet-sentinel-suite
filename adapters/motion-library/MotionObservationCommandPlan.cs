using System.Collections.ObjectModel;
using System.Globalization;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The closed, typed set of B2 observation intents. One member per (kind, subject) pair of the
/// verified subset: three axis kinds over the four controller axes plus the four outputs of the
/// closed output namespace.
///
/// An intent is a TYPE, not text. A caller can only name one of these sixteen members, so no
/// caller-supplied string ever reaches the read boundary: there is no StringBuilder, no format
/// call and no concatenation on this path. The enum is the entire request surface.
///
/// This file is INTERNAL on purpose. The public surface keeps the typed request vocabulary of
/// <see cref="MotionObservationRequest"/>; the intent exists only between the session and the
/// command plan, so no consumer of the assembly can address a controller read directly.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal enum MotionObservationIntent
{
    /// <summary>Axis status word of controller axis A.</summary>
    AxisStatusA = 0,

    /// <summary>Axis status word of controller axis B.</summary>
    AxisStatusB = 1,

    /// <summary>Axis status word of controller axis C.</summary>
    AxisStatusC = 2,

    /// <summary>Axis status word of controller axis D.</summary>
    AxisStatusD = 3,

    /// <summary>First position value of controller axis A.</summary>
    CommandPositionA = 4,

    /// <summary>First position value of controller axis B.</summary>
    CommandPositionB = 5,

    /// <summary>First position value of controller axis C.</summary>
    CommandPositionC = 6,

    /// <summary>First position value of controller axis D.</summary>
    CommandPositionD = 7,

    /// <summary>Second position value of controller axis A.</summary>
    ActualPositionA = 8,

    /// <summary>Second position value of controller axis B.</summary>
    ActualPositionB = 9,

    /// <summary>Second position value of controller axis C.</summary>
    ActualPositionC = 10,

    /// <summary>Second position value of controller axis D.</summary>
    ActualPositionD = 11,

    /// <summary>Logical state of output 1 of the closed output namespace.</summary>
    DigitalOutput1 = 12,

    /// <summary>Logical state of output 2 of the closed output namespace.</summary>
    DigitalOutput2 = 13,

    /// <summary>Logical state of output 3 of the closed output namespace.</summary>
    DigitalOutput3 = 14,

    /// <summary>Logical state of output 4 of the closed output namespace.</summary>
    DigitalOutput4 = 15,
}

/// <summary>
/// The one result of one read attempt: whether the boundary was reached at all, whether a value
/// came back, the bounded value text, and the canonical refusal word when it did not.
///
/// <see cref="RefusalCode"/> never carries vendor text, an exception message, a path, an address
/// or a command. <see cref="RawText"/> is the validated normalized response token and never
/// carries the request.
/// </summary>
internal readonly record struct ObservationReadResult
{
    /// <summary>True when a read call was actually attempted against the native boundary.</summary>
    internal bool Attempted { get; private init; }

    /// <summary>True when a bounded, validated value text came back.</summary>
    internal bool Succeeded { get; private init; }

    /// <summary>The validated value text; empty unless <see cref="Succeeded"/>.</summary>
    internal string RawText { get; private init; }

    /// <summary>The canonical refusal word; empty when <see cref="Succeeded"/>.</summary>
    internal string RefusalCode { get; private init; }

    /// <summary>The boundary was refused before anything was sent (closed, unbound, unmapped).</summary>
    internal static ObservationReadResult NotAttempted(string refusalCode) => new()
    {
        Attempted = false,
        Succeeded = false,
        RawText = string.Empty,
        RefusalCode = refusalCode,
    };

    /// <summary>A read call was attempted and did not produce a usable response.</summary>
    internal static ObservationReadResult Failed(string refusalCode) => new()
    {
        Attempted = true,
        Succeeded = false,
        RawText = string.Empty,
        RefusalCode = refusalCode,
    };

    /// <summary>A read call was attempted and produced a bounded, validated value text.</summary>
    internal static ObservationReadResult FromValue(string rawText) => new()
    {
        Attempted = true,
        Succeeded = true,
        RawText = rawText,
        RefusalCode = string.Empty,
    };
}

/// <summary>
/// The only seam through which the B2 session can reach a controller read.
///
/// It takes a typed intent and returns a bounded result; it has no string parameter, no buffer
/// parameter, no handle parameter and no way to express write, control or motion. The interface
/// is internal, so an application, a route, a UI or a test fixture cannot supply an
/// implementation to production code.
/// </summary>
internal interface IObservationReadKernel : IDisposable
{
    /// <summary>True once this kernel has been invalidated and can never issue again.</summary>
    bool IsInvalidated { get; }

    /// <summary>Issues exactly one typed read and returns its bounded result.</summary>
    ObservationReadResult Issue(MotionObservationIntent intent);
}

/// <summary>
/// The immutable intent-to-request plan, the response-shape validator and the bounded numeric
/// parser of the B2 verified subset.
///
/// Every request is a LITERAL: sixteen fixed strings, one per intent, held in a read-only
/// dictionary that nothing can mutate or replace. There is no interpolation on this path, so a
/// value that is not a validated axis letter or a validated output number cannot appear in a
/// request even in principle.
///
/// Every response is bounded twice: the native buffer has a fixed size, and the accepted text
/// must be a single short numeric token. Anything else — an empty reply, an unterminated buffer,
/// a non-printable byte, a comma-separated multi-axis reply, a controller error marker, a
/// hexadecimal-format reply or an out-of-range output value — becomes a refusal word, never a
/// guessed value. The parser never falls back, never truncates and never repairs.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the validation of record).
/// </summary>
internal static class MotionObservationCommandPlan
{
    /// <summary>
    /// The longest accepted request. The longest planned request is ten characters; the bound is
    /// fixed here so a future edit cannot grow a request past the buffer without failing a test.
    /// </summary>
    internal const int MaximumRequestLength = 16;

    /// <summary>
    /// The fixed response buffer. A position in controller counts needs at most sixteen
    /// characters; thirty-two leaves room for the widest accepted numeric token and nothing more.
    /// </summary>
    internal const int MaximumResponseBytes = 32;

    /// <summary>The longest accepted value text, in characters (the buffer bound minus termination).</summary>
    internal const int MaximumValueTextLength = 16;

    /// <summary>The largest accepted magnitude, in controller counts.</summary>
    internal const decimal MaximumMagnitude = 10_000_000_000m;

    /// <summary>The most fraction digits any accepted value text may carry.</summary>
    private const int MaximumFractionDigits = 4;

    /// <summary>The most integer digits any accepted value text may carry.</summary>
    private const int MaximumIntegerDigits = 10;

    /// <summary>
    /// The complete and immutable request plan. The backing dictionary is private and the public
    /// view is a read-only wrapper, so no caller can add, remove or replace an entry.
    /// </summary>
    private static readonly ReadOnlyDictionary<MotionObservationIntent, string> RequestTemplates =
        new ReadOnlyDictionary<MotionObservationIntent, string>(
            new Dictionary<MotionObservationIntent, string>
            {
                [MotionObservationIntent.AxisStatusA] = "MG _TSA",
                [MotionObservationIntent.AxisStatusB] = "MG _TSB",
                [MotionObservationIntent.AxisStatusC] = "MG _TSC",
                [MotionObservationIntent.AxisStatusD] = "MG _TSD",
                [MotionObservationIntent.CommandPositionA] = "TP A",
                [MotionObservationIntent.CommandPositionB] = "TP B",
                [MotionObservationIntent.CommandPositionC] = "TP C",
                [MotionObservationIntent.CommandPositionD] = "TP D",
                [MotionObservationIntent.ActualPositionA] = "RP A",
                [MotionObservationIntent.ActualPositionB] = "RP B",
                [MotionObservationIntent.ActualPositionC] = "RP C",
                [MotionObservationIntent.ActualPositionD] = "RP D",
                [MotionObservationIntent.DigitalOutput1] = "MG @OUT[1]",
                [MotionObservationIntent.DigitalOutput2] = "MG @OUT[2]",
                [MotionObservationIntent.DigitalOutput3] = "MG @OUT[3]",
                [MotionObservationIntent.DigitalOutput4] = "MG @OUT[4]",
            });

    /// <summary>The complete intent set, in declaration order. Read-only; it cannot grow at run time.</summary>
    internal static IReadOnlyList<MotionObservationIntent> Intents { get; } = Array.AsReadOnly(new[]
    {
        MotionObservationIntent.AxisStatusA,
        MotionObservationIntent.AxisStatusB,
        MotionObservationIntent.AxisStatusC,
        MotionObservationIntent.AxisStatusD,
        MotionObservationIntent.CommandPositionA,
        MotionObservationIntent.CommandPositionB,
        MotionObservationIntent.CommandPositionC,
        MotionObservationIntent.CommandPositionD,
        MotionObservationIntent.ActualPositionA,
        MotionObservationIntent.ActualPositionB,
        MotionObservationIntent.ActualPositionC,
        MotionObservationIntent.ActualPositionD,
        MotionObservationIntent.DigitalOutput1,
        MotionObservationIntent.DigitalOutput2,
        MotionObservationIntent.DigitalOutput3,
        MotionObservationIntent.DigitalOutput4,
    });

    /// <summary>The complete request plan, read-only: one literal per intent and nothing else.</summary>
    internal static IReadOnlyDictionary<MotionObservationIntent, string> Templates => RequestTemplates;

    /// <summary>
    /// Resolves the one request literal for an intent. False for any value outside the closed
    /// sixteen-member set, so a cast from an arbitrary integer cannot invent a request.
    /// </summary>
    internal static bool TryGetTemplate(MotionObservationIntent intent, out string template)
    {
        if (RequestTemplates.TryGetValue(intent, out var resolved))
        {
            template = resolved;
            return true;
        }

        template = string.Empty;
        return false;
    }

    /// <summary>
    /// Maps an already validated request subject to its typed intent. Returns false when the
    /// subject is not part of the verified subset; the session refuses in that case and nothing
    /// is issued.
    /// </summary>
    internal static bool TryResolveIntent(
        string kind,
        string axisId,
        int outputNumber,
        out MotionObservationIntent intent)
    {
        if (string.Equals(kind, MotionObservedStateVocabulary.KindDigitalOutput, StringComparison.Ordinal))
        {
            switch (outputNumber)
            {
                case 1:
                    intent = MotionObservationIntent.DigitalOutput1;
                    return true;
                case 2:
                    intent = MotionObservationIntent.DigitalOutput2;
                    return true;
                case 3:
                    intent = MotionObservationIntent.DigitalOutput3;
                    return true;
                case 4:
                    intent = MotionObservationIntent.DigitalOutput4;
                    return true;
                default:
                    intent = default;
                    return false;
            }
        }

        // An axis subject never carries an output number; a mixed subject is not a plan member.
        if (outputNumber != 0)
        {
            intent = default;
            return false;
        }

        if (string.Equals(kind, MotionObservedStateVocabulary.KindAxisStatus, StringComparison.Ordinal))
        {
            return TryResolveAxisIntent(axisId, out intent, MotionObservationIntent.AxisStatusA);
        }

        if (string.Equals(kind, MotionObservedStateVocabulary.KindCommandPosition, StringComparison.Ordinal))
        {
            return TryResolveAxisIntent(axisId, out intent, MotionObservationIntent.CommandPositionA);
        }

        if (string.Equals(kind, MotionObservedStateVocabulary.KindActualPosition, StringComparison.Ordinal))
        {
            return TryResolveAxisIntent(axisId, out intent, MotionObservationIntent.ActualPositionA);
        }

        intent = default;
        return false;
    }

    /// <summary>
    /// Converts a validated integer index (0-3) into the matching member of one axis family.
    /// The caller supplies the first member of the family; the four members of a family are
    /// declared contiguously, and the offset is bounded to 0-3, so no other member is reachable.
    /// </summary>
    private static bool TryResolveAxisIntent(
        string axisId,
        out MotionObservationIntent intent,
        MotionObservationIntent firstOfFamily)
    {
        var offset = axisId switch
        {
            MotionObservedStateVocabulary.AxisA => 0,
            MotionObservedStateVocabulary.AxisB => 1,
            MotionObservedStateVocabulary.AxisC => 2,
            MotionObservedStateVocabulary.AxisD => 3,
            _ => -1,
        };

        if (offset < 0)
        {
            intent = default;
            return false;
        }

        intent = (MotionObservationIntent)((int)firstOfFamily + offset);
        return true;
    }

    /// <summary>True when the intent addresses a digital output rather than an axis.</summary>
    internal static bool IsDigitalOutput(MotionObservationIntent intent) =>
        intent >= MotionObservationIntent.DigitalOutput1
        && intent <= MotionObservationIntent.DigitalOutput4;

    /// <summary>
    /// Extracts the accepted text from a fixed native response buffer: it must be terminated
    /// inside the buffer, must not be empty, and every byte before the terminator must be a
    /// printable ASCII character or horizontal whitespace. No other byte is accepted.
    /// </summary>
    internal static bool TryExtractResponseText(
        byte[] responseBuffer,
        int length,
        out string rawText,
        out string refusalCode)
    {
        rawText = string.Empty;

        if (responseBuffer is null || length <= 0 || length > responseBuffer.Length)
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseEmpty;
            return false;
        }

        var terminator = Array.IndexOf(responseBuffer, (byte)0, 0, length);
        if (terminator < 0)
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseUnterminated;
            return false;
        }

        if (terminator == 0)
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseEmpty;
            return false;
        }

        for (var index = 0; index < terminator; index++)
        {
            var value = responseBuffer[index];
            var printable = (value >= 0x20 && value <= 0x7E) || value == 0x09 || value == 0x0D;
            if (!printable)
            {
                refusalCode = MotionObservedStateVocabulary.ObservationResponseNotPrintable;
                return false;
            }
        }

        rawText = System.Text.Encoding.ASCII.GetString(responseBuffer, 0, terminator);
        refusalCode = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates one response text into one bounded numeric token.
    ///
    /// Accepted shape: at most one leading sign, one to ten integer digits, an optional single
    /// dot with one to four fraction digits, and nothing else. A leading prompt character is
    /// removed once; surrounding horizontal whitespace is trimmed. A comma (multi-axis reply), a
    /// question mark (controller error marker), a dollar sign (hexadecimal format), any other
    /// character, an oversized text or an out-of-magnitude value is refused.
    /// </summary>
    internal static bool TryNormalizeValueText(
        string? rawText,
        out string valueText,
        out string refusalCode)
    {
        valueText = string.Empty;

        if (string.IsNullOrWhiteSpace(rawText))
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseEmpty;
            return false;
        }

        var candidate = rawText.Trim();

        // The controller echoes one prompt character on some transports; strip at most one.
        if (candidate.StartsWith(':') && candidate.Length > 1)
        {
            candidate = candidate[1..].Trim();
        }

        if (candidate.Length == 0)
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseEmpty;
            return false;
        }

        if (candidate.Length > MaximumValueTextLength)
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseTooLong;
            return false;
        }

        if (candidate.Contains(','))
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseMultiValue;
            return false;
        }

        if (candidate.Contains('?'))
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseControllerError;
            return false;
        }

        if (!HasAcceptedNumericShape(candidate))
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseInvalid;
            return false;
        }

        if (!decimal.TryParse(
                candidate,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var value)
            || value > MaximumMagnitude
            || value < -MaximumMagnitude)
        {
            refusalCode = MotionObservedStateVocabulary.ObservationResponseInvalid;
            return false;
        }

        valueText = candidate;
        refusalCode = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses an already normalized value text. Position kinds accept any accepted magnitude;
    /// the caller decides what the number may mean, and this boundary never converts a unit.
    /// </summary>
    internal static bool TryParseValue(string valueText, out decimal value) =>
        decimal.TryParse(
            valueText,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);

    /// <summary>
    /// Accepts an output state only when the reported logical value is exactly zero or exactly
    /// one. Any other number is refused: the boundary never rounds, clamps or reinterprets it.
    /// </summary>
    internal static bool TryParseOutputState(string valueText, out int state)
    {
        state = 0;

        if (!TryParseValue(valueText, out var value))
        {
            return false;
        }

        if (value == 0m)
        {
            state = 0;
            return true;
        }

        if (value == 1m)
        {
            state = 1;
            return true;
        }

        return false;
    }

    /// <summary>
    /// The interpretation word of an accepted intent. No member of the verified subset reaches
    /// <see cref="MotionObservedStateVocabulary.InterpretationVerified"/>: the two position reads
    /// keep their value with no meaning adopted, and the status word and the output state keep a
    /// value whose machine meaning is not commissioned.
    /// </summary>
    internal static string InterpretationFor(MotionObservationIntent intent) =>
        IsDigitalOutput(intent)
            ? MotionObservedStateVocabulary.InterpretationUnconfirmed
            : intent switch
            {
                MotionObservationIntent.AxisStatusA or
                MotionObservationIntent.AxisStatusB or
                MotionObservationIntent.AxisStatusC or
                MotionObservationIntent.AxisStatusD => MotionObservedStateVocabulary.InterpretationUnconfirmed,
                _ => MotionObservedStateVocabulary.InterpretationRawObserved,
            };

    /// <summary>The native unit descriptor of an accepted intent; never a converted unit.</summary>
    internal static string NativeUnitFor(MotionObservationIntent intent) =>
        IsDigitalOutput(intent)
            ? MotionObservedStateVocabulary.NativeUnitLogicalState
            : intent switch
            {
                MotionObservationIntent.AxisStatusA or
                MotionObservationIntent.AxisStatusB or
                MotionObservationIntent.AxisStatusC or
                MotionObservationIntent.AxisStatusD => MotionObservedStateVocabulary.NativeUnitStatusBits,
                _ => MotionObservedStateVocabulary.NativeUnitControllerUnits,
            };

    /// <summary>The truthful detail word of an accepted intent; it names the limit, not a meaning.</summary>
    internal static string AcceptedDetailFor(MotionObservationIntent intent) =>
        intent switch
        {
            MotionObservationIntent.AxisStatusA or
            MotionObservationIntent.AxisStatusB or
            MotionObservationIntent.AxisStatusC or
            MotionObservationIntent.AxisStatusD =>
                MotionObservedStateVocabulary.ObservationStatusWordRaw,
            MotionObservationIntent.DigitalOutput1 or
            MotionObservationIntent.DigitalOutput2 or
            MotionObservationIntent.DigitalOutput3 or
            MotionObservationIntent.DigitalOutput4 =>
                MotionObservedStateVocabulary.ObservationOutputStateUnconfirmed,
            _ => MotionObservedStateVocabulary.ObservationPositionValueRaw,
        };

    /// <summary>
    /// The exact accepted numeric shape. Implemented as a character scan rather than an
    /// expression or a regular expression so the accepted language is visible line by line.
    /// </summary>
    private static bool HasAcceptedNumericShape(string candidate)
    {
        var index = 0;
        var integerDigits = 0;
        var fractionDigits = 0;
        var hasDot = false;

        if (candidate[0] is '+' or '-')
        {
            index = 1;
        }

        for (; index < candidate.Length; index++)
        {
            var character = candidate[index];

            if (character >= '0' && character <= '9')
            {
                if (hasDot)
                {
                    fractionDigits++;
                    if (fractionDigits > MaximumFractionDigits)
                    {
                        return false;
                    }
                }
                else
                {
                    integerDigits++;
                    if (integerDigits > MaximumIntegerDigits)
                    {
                        return false;
                    }
                }

                continue;
            }

            if (character == '.' && !hasDot && integerDigits > 0)
            {
                hasDot = true;
                continue;
            }

            return false;
        }

        if (integerDigits == 0)
        {
            return false;
        }

        return !hasDot || fractionDigits > 0;
    }
}
