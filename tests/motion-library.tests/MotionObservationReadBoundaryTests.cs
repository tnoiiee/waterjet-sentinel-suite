using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Focused defect-class coverage of the verified B2 read boundary, WITHOUT any controller, native
/// module or socket: the sixteen request literals are exact and immutable, only validated axis
/// letters and outputs resolve to an intent, every reply is bounded and normalized, malformed
/// replies are refused instead of repaired, the four candidates produce raw or unconfirmed
/// interpretations and never a verified one, a closed boundary can never issue again, and the
/// read path is not reachable from any public member.
///
/// The recording kernel here is a test double of the internal seam; the real native kernel is
/// never constructed with a live pointer in this environment and is covered by source-boundary
/// assertions plus the Owner-local run.
/// </summary>
public sealed class MotionObservationReadBoundaryTests
{
    private sealed class RecordingKernel : IObservationReadKernel
    {
        private readonly Queue<ObservationReadResult> _results = new();

        internal List<MotionObservationIntent> Issued { get; } = new();

        internal Action? BeforeIssue { get; set; }

        public bool IsInvalidated { get; private set; }

        internal RecordingKernel Returns(string valueText)
        {
            _results.Enqueue(ObservationReadResult.FromValue(valueText));
            return this;
        }

        internal RecordingKernel Fails(string refusalCode)
        {
            _results.Enqueue(ObservationReadResult.Failed(refusalCode));
            return this;
        }

        public ObservationReadResult Issue(MotionObservationIntent intent)
        {
            Issued.Add(intent);
            BeforeIssue?.Invoke();

            if (IsInvalidated)
            {
                return ObservationReadResult.NotAttempted(
                    MotionObservedStateVocabulary.ObservationKernelClosed);
            }

            return _results.Count > 0
                ? _results.Dequeue()
                : ObservationReadResult.NotAttempted(
                    MotionObservedStateVocabulary.ObservationKernelUnbound);
        }

        public void Dispose() => IsInvalidated = true;
    }

    private static MotionObservationRequest AxisRequest(string kind, string axisId) => new()
    {
        Kind = kind,
        AxisId = axisId,
    };

    private static MotionObservationRequest OutputRequest(int outputNumber) => new()
    {
        Kind = MotionObservedStateVocabulary.KindDigitalOutput,
        OutputNumber = outputNumber,
    };

    /// <summary>
    /// The exact typed intent the closed plan maps an output subject to. It mirrors the plan's
    /// own mapping so a test can prove WHICH intent was issued, not merely how many were issued.
    /// </summary>
    private static MotionObservationIntent ExpectedDigitalOutputIntent(int outputNumber) =>
        outputNumber switch
        {
            1 => MotionObservationIntent.DigitalOutput1,
            2 => MotionObservationIntent.DigitalOutput2,
            3 => MotionObservationIntent.DigitalOutput3,
            4 => MotionObservationIntent.DigitalOutput4,
            _ => throw new ArgumentOutOfRangeException(nameof(outputNumber)),
        };

    // ----------------------------------------------------------------------- the request plan

    [Fact]
    public void The_Plan_Is_Sixteen_Exact_Literals()
    {
        Assert.Equal(16, MotionObservationCommandPlan.Intents.Count);
        Assert.Equal(16, MotionObservationCommandPlan.Templates.Count);

        var expected = new (MotionObservationIntent Intent, string Request)[]
        {
            (MotionObservationIntent.AxisStatusA, "MG _TSA"),
            (MotionObservationIntent.AxisStatusB, "MG _TSB"),
            (MotionObservationIntent.AxisStatusC, "MG _TSC"),
            (MotionObservationIntent.AxisStatusD, "MG _TSD"),
            (MotionObservationIntent.CommandPositionA, "TP A"),
            (MotionObservationIntent.CommandPositionB, "TP B"),
            (MotionObservationIntent.CommandPositionC, "TP C"),
            (MotionObservationIntent.CommandPositionD, "TP D"),
            (MotionObservationIntent.ActualPositionA, "RP A"),
            (MotionObservationIntent.ActualPositionB, "RP B"),
            (MotionObservationIntent.ActualPositionC, "RP C"),
            (MotionObservationIntent.ActualPositionD, "RP D"),
            (MotionObservationIntent.DigitalOutput1, "MG @OUT[1]"),
            (MotionObservationIntent.DigitalOutput2, "MG @OUT[2]"),
            (MotionObservationIntent.DigitalOutput3, "MG @OUT[3]"),
            (MotionObservationIntent.DigitalOutput4, "MG @OUT[4]"),
        };

        foreach (var (intent, request) in expected)
        {
            Assert.True(MotionObservationCommandPlan.TryGetTemplate(intent, out var resolved));
            Assert.Equal(request, resolved);
        }
    }

    [Fact]
    public void Every_Literal_Matches_The_Approved_Read_Form_And_Fits_The_Fixed_Bound()
    {
        var approvedForm = new Regex(@"^(MG _TS[ABCD]|TP [ABCD]|RP [ABCD]|MG @OUT\[[1-4]\])$");

        foreach (var request in MotionObservationCommandPlan.Templates.Values)
        {
            Assert.Matches(approvedForm, request);
            Assert.True(request.Length <= MotionObservationCommandPlan.MaximumRequestLength);
            Assert.False(request.Contains(','));
            Assert.False(request.Contains('?'));
            Assert.False(request.Contains(';'));
        }
    }

    [Fact]
    public void The_Plan_Cannot_Be_Mutated_Or_Extended_At_Run_Time()
    {
        var plan = MotionObservationCommandPlan.Templates;

        Assert.IsType<ReadOnlyDictionary<MotionObservationIntent, string>>(plan);
        Assert.Same(plan, MotionObservationCommandPlan.Templates);

        var writable = Assert.IsAssignableFrom<IDictionary<MotionObservationIntent, string>>(plan);
        Assert.Throws<NotSupportedException>(() =>
            writable.Add((MotionObservationIntent)999, "UNKNOWN"));

        Assert.False(MotionObservationCommandPlan.TryGetTemplate((MotionObservationIntent)999, out var unresolved));
        Assert.Equal(string.Empty, unresolved);
    }

    [Fact]
    public void Only_Validated_Axis_Letters_And_Outputs_Resolve_To_An_Intent()
    {
        foreach (var kind in new[]
                 {
                     MotionObservedStateVocabulary.KindAxisStatus,
                     MotionObservedStateVocabulary.KindCommandPosition,
                     MotionObservedStateVocabulary.KindActualPosition,
                 })
        {
            foreach (var axisId in MotionObservedStateVocabulary.AxisIds)
            {
                Assert.True(MotionObservationCommandPlan.TryResolveIntent(kind, axisId, 0, out _));
            }

            foreach (var axisId in new[] { "Z", "a", string.Empty, "AA", " A" })
            {
                Assert.False(MotionObservationCommandPlan.TryResolveIntent(kind, axisId, 0, out _));
            }

            Assert.False(MotionObservationCommandPlan.TryResolveIntent(kind, "A", 1, out _));
        }

        for (var output = 1; output <= 4; output++)
        {
            Assert.True(MotionObservationCommandPlan.TryResolveIntent(
                MotionObservedStateVocabulary.KindDigitalOutput,
                MotionObservedStateVocabulary.NoAxis,
                output,
                out _));
        }

        foreach (var output in new[] { 0, -1, 5, int.MaxValue })
        {
            Assert.False(MotionObservationCommandPlan.TryResolveIntent(
                MotionObservedStateVocabulary.KindDigitalOutput,
                MotionObservedStateVocabulary.NoAxis,
                output,
                out _));
        }

        Assert.False(MotionObservationCommandPlan.TryResolveIntent("UNKNOWN_KIND", "A", 0, out _));
    }

    // ---------------------------------------------------------------------- the response parse

    [Fact]
    public void One_Prompt_Character_And_Surrounding_Whitespace_Are_Normalized_Once()
    {
        Assert.True(MotionObservationCommandPlan.TryNormalizeValueText(
            "  :200.0000\r", out var valueText, out var refusalCode));
        Assert.Equal("200.0000", valueText);
        Assert.Equal(string.Empty, refusalCode);
    }

    [Fact]
    public void A_Malformed_Reply_Is_Refused_Instead_Of_Repaired()
    {
        var cases = new (string Raw, string Expected)[]
        {
            (string.Empty, MotionObservedStateVocabulary.ObservationResponseEmpty),
            ("   ", MotionObservedStateVocabulary.ObservationResponseEmpty),
            ("12345678901234567", MotionObservedStateVocabulary.ObservationResponseTooLong),
            ("1.0000,2.0000", MotionObservedStateVocabulary.ObservationResponseMultiValue),
            ("?", MotionObservedStateVocabulary.ObservationResponseControllerError),
            ("$FFFFF6", MotionObservedStateVocabulary.ObservationResponseInvalid),
            ("abc", MotionObservedStateVocabulary.ObservationResponseInvalid),
            ("1.2.3", MotionObservedStateVocabulary.ObservationResponseInvalid),
            ("1.", MotionObservedStateVocabulary.ObservationResponseInvalid),
            ("+", MotionObservedStateVocabulary.ObservationResponseInvalid),
            ("12345678901", MotionObservedStateVocabulary.ObservationResponseInvalid),
            ("1.00000", MotionObservedStateVocabulary.ObservationResponseInvalid),
            ("- 1.0", MotionObservedStateVocabulary.ObservationResponseInvalid),
        };

        foreach (var (raw, expected) in cases)
        {
            Assert.False(MotionObservationCommandPlan.TryNormalizeValueText(raw, out var valueText, out var refusalCode));
            Assert.Equal(expected, refusalCode);
            Assert.Equal(string.Empty, valueText);
        }
    }

    [Fact]
    public void A_Signed_Decimal_Within_The_Bound_Is_Accepted()
    {
        foreach (var raw in new[] { "0", "1", "-1", "200.0000", "-2147483648.0000", "+10.5" })
        {
            Assert.True(MotionObservationCommandPlan.TryNormalizeValueText(raw, out var valueText, out _));
            Assert.Equal(raw, valueText);
        }
    }

    [Fact]
    public void An_Output_State_Is_Accepted_Only_When_It_Is_Exactly_Zero_Or_Exactly_One()
    {
        Assert.True(MotionObservationCommandPlan.TryParseOutputState("0", out var zero));
        Assert.True(MotionObservationCommandPlan.TryParseOutputState("1.0000", out var one));
        Assert.Equal(0, zero);
        Assert.Equal(1, one);

        foreach (var raw in new[] { "2.0000", "-1.0000", "0.5000" })
        {
            Assert.False(MotionObservationCommandPlan.TryParseOutputState(raw, out _));
        }
    }

    [Fact]
    public void A_Response_Buffer_Must_Be_Terminated_Printable_And_Non_Empty()
    {
        var terminated = new byte[MotionObservationCommandPlan.MaximumResponseBytes];
        "200.0000"u8.CopyTo(terminated);

        Assert.True(MotionObservationCommandPlan.TryExtractResponseText(
            terminated, terminated.Length, out var rawText, out _));
        Assert.Equal("200.0000", rawText);

        var unterminated = new byte[8];
        "12345678"u8.CopyTo(unterminated);
        Assert.False(MotionObservationCommandPlan.TryExtractResponseText(
            unterminated, unterminated.Length, out _, out var unterminatedCode));
        Assert.Equal(MotionObservedStateVocabulary.ObservationResponseUnterminated, unterminatedCode);

        var empty = new byte[MotionObservationCommandPlan.MaximumResponseBytes];
        Assert.False(MotionObservationCommandPlan.TryExtractResponseText(
            empty, empty.Length, out _, out var emptyCode));
        Assert.Equal(MotionObservedStateVocabulary.ObservationResponseEmpty, emptyCode);

        var notPrintable = new byte[MotionObservationCommandPlan.MaximumResponseBytes];
        notPrintable[0] = 0x01;
        notPrintable[1] = 0x00;
        Assert.False(MotionObservationCommandPlan.TryExtractResponseText(
            notPrintable, notPrintable.Length, out _, out var notPrintableCode));
        Assert.Equal(MotionObservedStateVocabulary.ObservationResponseNotPrintable, notPrintableCode);
    }

    // ------------------------------------------------------------------- the verified read path

    [Fact]
    public void Every_Axis_Candidate_Produces_A_Bounded_Native_Value_And_Never_A_Verified_One()
    {
        var expected = new (string Kind, string Unit, string Interpretation, string Detail)[]
        {
            (MotionObservedStateVocabulary.KindAxisStatus,
                MotionObservedStateVocabulary.NativeUnitStatusBits,
                MotionObservedStateVocabulary.InterpretationUnconfirmed,
                MotionObservedStateVocabulary.ObservationStatusWordRaw),
            (MotionObservedStateVocabulary.KindCommandPosition,
                MotionObservedStateVocabulary.NativeUnitControllerUnits,
                MotionObservedStateVocabulary.InterpretationRawObserved,
                MotionObservedStateVocabulary.ObservationPositionValueRaw),
            (MotionObservedStateVocabulary.KindActualPosition,
                MotionObservedStateVocabulary.NativeUnitControllerUnits,
                MotionObservedStateVocabulary.InterpretationRawObserved,
                MotionObservedStateVocabulary.ObservationPositionValueRaw),
        };

        foreach (var (kind, unit, interpretation, detail) in expected)
        {
            foreach (var axisId in MotionObservedStateVocabulary.AxisIds)
            {
                using var kernel = new RecordingKernel().Returns("128.0000");
                using var session = new MotionObservationSession(kernel);

                var outcome = session.Observe(AxisRequest(kind, axisId));

                Assert.True(outcome.Accepted);
                Assert.True(outcome.ObservationIssued);
                Assert.Equal(MotionObservedStateVocabulary.ObservationAccepted, outcome.Code);
                Assert.Equal(1L, outcome.Generation);

                var state = Assert.Single(outcome.States);
                Assert.Equal(kind, state.Kind);
                Assert.Equal(axisId, state.AxisId);
                Assert.Null(state.OutputNumber);
                Assert.Equal("128.0000", state.NativeValueText);
                Assert.Equal(unit, state.NativeUnit);
                Assert.Equal(interpretation, state.InterpretationState);
                Assert.Equal(detail, state.DetailCode);
                Assert.Equal(interpretation, outcome.InterpretationState);
                Assert.NotEqual(MotionObservedStateVocabulary.InterpretationVerified, state.InterpretationState);
                Assert.Contains(state.InterpretationState, MotionObservedStateVocabulary.ReachableInterpretationStates);
            }
        }
    }

    [Fact]
    public void The_Output_Candidate_Produces_A_Logical_State_With_No_Brake_Meaning()
    {
        for (var output = 1; output <= 4; output++)
        {
            using var kernel = new RecordingKernel().Returns("1.0000");
            using var session = new MotionObservationSession(kernel);

            var outcome = session.Observe(OutputRequest(output));

            Assert.True(outcome.Accepted);
            var state = Assert.Single(outcome.States);
            Assert.Equal(MotionObservedStateVocabulary.KindDigitalOutput, state.Kind);
            Assert.Equal(MotionObservedStateVocabulary.NoAxis, state.AxisId);
            Assert.Equal(output, state.OutputNumber);
            Assert.Equal("1.0000", state.NativeValueText);
            Assert.Equal(MotionObservedStateVocabulary.NativeUnitLogicalState, state.NativeUnit);
            Assert.Equal(MotionObservedStateVocabulary.InterpretationUnconfirmed, state.InterpretationState);
            Assert.Equal(
                MotionObservedStateVocabulary.ObservationOutputStateUnconfirmed,
                state.DetailCode);
            var issuedIntent = Assert.Single(kernel.Issued);
            Assert.Equal(ExpectedDigitalOutputIntent(output), issuedIntent);
        }
    }

    [Fact]
    public void The_Outcome_Carries_No_Request_Text_No_Export_Name_And_No_Path()
    {
        using var kernel = new RecordingKernel().Returns("200.0000");
        using var session = new MotionObservationSession(kernel);
        var outcome = session.Observe(
            AxisRequest(MotionObservedStateVocabulary.KindActualPosition, "D"));

        var state = Assert.Single(outcome.States);
        var combined = string.Concat(
            outcome.Code,
            outcome.Detail,
            outcome.InterpretationState,
            state.DetailCode,
            state.NativeUnit,
            state.NativeValueText);

        foreach (var forbidden in new[] { "MG ", "TP ", "RP ", "@OUT[", "gclib", "GCmd" })
        {
            Assert.DoesNotContain(forbidden, combined, StringComparison.Ordinal);
        }

        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", outcome.Detail);
        Assert.DoesNotContain("\\", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("/", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(":", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Issued_Intent_Is_The_One_Matching_The_Requested_Subject()
    {
        using var kernel = new RecordingKernel()
            .Returns("1.0000")
            .Returns("2.0000")
            .Returns("3.0000");
        using var session = new MotionObservationSession(kernel);

        _ = session.Observe(OutputRequest(3));
        _ = session.Observe(AxisRequest(MotionObservedStateVocabulary.KindAxisStatus, "B"));
        _ = session.Observe(AxisRequest(MotionObservedStateVocabulary.KindCommandPosition, "C"));

        Assert.Equal(
            new[]
            {
                MotionObservationIntent.DigitalOutput3,
                MotionObservationIntent.AxisStatusB,
                MotionObservationIntent.CommandPositionC,
            },
            kernel.Issued.ToArray());
    }

    [Fact]
    public void An_Output_Value_Outside_Zero_Or_One_Is_Refused_Without_A_Value()
    {
        foreach (var raw in new[] { "2.0000", "-1.0000" })
        {
            using var kernel = new RecordingKernel().Returns(raw);
            using var session = new MotionObservationSession(kernel);

            var outcome = session.Observe(OutputRequest(1));

            Assert.False(outcome.Accepted);
            Assert.True(outcome.ObservationIssued);
            Assert.Equal(MotionObservedStateVocabulary.ObservationOutputStateOutOfRange, outcome.Code);
            Assert.Equal(MotionObservedStateVocabulary.InterpretationUnavailable, outcome.InterpretationState);

            var state = Assert.Single(outcome.States);
            Assert.Null(state.NativeValueText);
            Assert.Equal(MotionObservedStateVocabulary.NativeUnitNone, state.NativeUnit);
        }
    }

    [Fact]
    public void An_Unparseable_Value_Is_Refused_Without_A_Value()
    {
        using var kernel = new RecordingKernel().Returns("not-a-number");
        using var session = new MotionObservationSession(kernel);

        var outcome = session.Observe(
            AxisRequest(MotionObservedStateVocabulary.KindCommandPosition, "A"));

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionObservedStateVocabulary.ObservationResponseInvalid, outcome.Code);
        Assert.Null(Assert.Single(outcome.States).NativeValueText);
    }

    [Fact]
    public void A_Refused_Boundary_Is_Reported_Without_A_Value_And_Without_A_Retry()
    {
        using var kernel = new RecordingKernel().Fails(MotionObservedStateVocabulary.ObservationCommandFailed);
        using var session = new MotionObservationSession(kernel);

        var outcome = session.Observe(
            AxisRequest(MotionObservedStateVocabulary.KindAxisStatus, "A"));

        Assert.False(outcome.Accepted);
        Assert.True(outcome.ObservationIssued);
        Assert.Equal(MotionObservedStateVocabulary.ObservationCommandFailed, outcome.Code);
        Assert.Equal(MotionObservedStateVocabulary.InterpretationRefused, outcome.InterpretationState);
        Assert.Empty(outcome.States);
        Assert.Single(kernel.Issued);
    }

    [Fact]
    public void A_Request_Refused_Before_The_Boundary_Issues_Nothing()
    {
        using var kernel = new RecordingKernel().Returns("1.0000");
        using var session = new MotionObservationSession(kernel);

        _ = session.Observe(new MotionObservationRequest { Kind = "UNKNOWN_KIND", AxisId = "A" });
        _ = session.Observe(AxisRequest(MotionObservedStateVocabulary.KindAxisStatus, "Z"));
        _ = session.Observe(OutputRequest(5));
        _ = session.Observe(new MotionObservationRequest
        {
            Kind = MotionObservedStateVocabulary.KindAxisStatus,
            AxisId = "A",
            OutputNumber = 2,
        });

        Assert.True(session.TryBeginObservation());
        _ = session.Observe(OutputRequest(1));
        session.EndObservation();

        session.Dispose();
        _ = session.Observe(OutputRequest(1));

        Assert.Empty(kernel.Issued);
    }

    // ------------------------------------------------------------------------- fence and close

    [Fact]
    public void A_Result_Is_Rejected_When_The_Generation_Advances_During_The_Read()
    {
        using var kernel = new RecordingKernel().Returns("200.0000");
        using var session = new MotionObservationSession(kernel);

        kernel.BeforeIssue = () => session.InvalidateGeneration();

        var outcome = session.Observe(
            AxisRequest(MotionObservedStateVocabulary.KindAxisStatus, "C"));

        Assert.False(outcome.Accepted);
        Assert.True(outcome.ObservationIssued);
        Assert.Equal(MotionObservedStateVocabulary.StaleGenerationResult, outcome.Code);
        Assert.Equal(MotionObservedStateVocabulary.InterpretationRefused, outcome.InterpretationState);
        Assert.Empty(outcome.States);
    }

    [Fact]
    public void Close_Invalidates_The_Boundary_And_No_Later_Read_Can_Reach_It()
    {
        var kernel = new RecordingKernel().Returns("1.0000");
        var session = new MotionObservationSession(kernel);

        Assert.False(session.ReadBoundaryInvalidated);

        session.Dispose();

        Assert.True(session.IsClosed);
        Assert.True(session.ReadBoundaryInvalidated);
        Assert.True(kernel.IsInvalidated);

        var outcome = session.Observe(OutputRequest(1));
        Assert.Equal(MotionObservedStateVocabulary.ObservationSessionClosed, outcome.Code);
        Assert.Empty(kernel.Issued);
    }

    [Fact]
    public void An_Unbound_Kernel_Issues_Nothing_And_Reports_It()
    {
        using var kernel = new BoundObservationReadKernel(IntPtr.Zero, IntPtr.Zero);

        Assert.False(kernel.IsBound);
        Assert.False(kernel.IsInvalidated);

        var unbound = kernel.Issue(MotionObservationIntent.DigitalOutput1);
        Assert.False(unbound.Attempted);
        Assert.False(unbound.Succeeded);
        Assert.Equal(MotionObservedStateVocabulary.ObservationKernelUnbound, unbound.RefusalCode);
        Assert.Equal(string.Empty, unbound.RawText);

        kernel.Dispose();
        kernel.Dispose();

        Assert.True(kernel.IsInvalidated);
        var closed = kernel.Issue(MotionObservationIntent.DigitalOutput1);
        Assert.False(closed.Attempted);
        Assert.Equal(MotionObservedStateVocabulary.ObservationKernelClosed, closed.RefusalCode);
    }

    // --------------------------------------------------------------------- public surface shape

    [Fact]
    public void The_Read_Path_Is_Not_Reachable_From_Any_Public_Member()
    {
        Assert.False(typeof(MotionObservationIntent).IsPublic);
        Assert.False(typeof(IObservationReadKernel).IsPublic);
        Assert.False(typeof(BoundObservationReadKernel).IsPublic);
        Assert.False(typeof(MotionObservationCommandPlan).IsPublic);
        Assert.False(typeof(MotionControllerCommandApi).IsPublic);
        Assert.True(typeof(MotionObservationIntent).IsNotPublic);
        Assert.True(typeof(IObservationReadKernel).IsNotPublic);

        var publicConstructors = typeof(MotionObservationSession).GetConstructors();
        Assert.Single(publicConstructors);
        Assert.Empty(publicConstructors[0].GetParameters());

        Assert.DoesNotContain(
            publicConstructors,
            constructor => constructor.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(IObservationReadKernel)));

        foreach (var method in typeof(MotionObservationSession).GetMethods())
        {
            Assert.NotEqual(typeof(IObservationReadKernel), method.ReturnType);
        }
    }

    [Fact]
    public void The_Verified_Subset_Is_Non_Empty_Complete_And_Free_Of_Blocked_Candidates()
    {
        Assert.NotEmpty(MotionObservedStateVocabulary.VerifiedForB2Candidates);
        Assert.Equal(
            MotionObservedStateVocabulary.CandidateIds,
            MotionObservedStateVocabulary.VerifiedForB2Candidates);
        Assert.Empty(MotionObservedStateVocabulary.BlockedCandidates);
        Assert.Equal(4, MotionObservedStateVocabulary.CandidateIds.Count);
    }

    [Fact]
    public void No_Candidate_Reaches_A_Verified_Interpretation()
    {
        foreach (var intent in MotionObservationCommandPlan.Intents)
        {
            Assert.NotEqual(MotionObservedStateVocabulary.InterpretationVerified, MotionObservationCommandPlan.InterpretationFor(intent));
            Assert.NotEqual(MotionObservedStateVocabulary.InterpretationStale, MotionObservationCommandPlan.InterpretationFor(intent));
            Assert.Contains(
                MotionObservationCommandPlan.InterpretationFor(intent),
                MotionObservedStateVocabulary.ReachableInterpretationStates);
        }
    }
}
