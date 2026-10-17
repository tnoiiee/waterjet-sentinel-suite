using System.Reflection;
using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// Focused B2 defect-class coverage of the read-only observed-state foundation, WITHOUT any
/// controller, native module or socket: every reachable outcome is UNAVAILABLE or REFUSED, the
/// typed allow-list cannot be widened at run time, one session owns exactly one observation slot,
/// the generation fence rejects stale and replayed results, close is deterministic, and the
/// surface carries no command, no address and no fabricated meaning.
///
/// The four read candidates are BLOCKED at this checkpoint (no authorized read surface), so the
/// suite proves the model's honesty — it cannot claim a value it never obtained — rather than a
/// read that is not authorized. No test here contacts anything.
/// </summary>
public sealed class MotionObservationSessionTests
{
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

    // ------------------------------------------------------------------ reachable state model

    [Fact]
    public void Every_Axis_Kind_Reports_Unavailable_For_Every_Controller_Axis()
    {
        var axisKinds = new[]
        {
            MotionObservedStateVocabulary.KindAxisStatus,
            MotionObservedStateVocabulary.KindCommandPosition,
            MotionObservedStateVocabulary.KindActualPosition,
        };

        using var session = new MotionObservationSession();

        foreach (var kind in axisKinds)
        {
            foreach (var axisId in MotionObservedStateVocabulary.AxisIds)
            {
                var outcome = session.Observe(AxisRequest(kind, axisId));

                Assert.False(outcome.Accepted);
                Assert.False(outcome.ObservationIssued);
                Assert.Equal(MotionObservedStateVocabulary.ObservationSurfaceNotAuthorized, outcome.Code);
                Assert.Equal(MotionObservedStateVocabulary.InterpretationUnavailable, outcome.InterpretationState);
                Assert.Equal(1L, outcome.Generation);

                var state = Assert.Single(outcome.States);
                Assert.Equal(kind, state.Kind);
                Assert.Equal(axisId, state.AxisId);
                Assert.Null(state.OutputNumber);
                Assert.Equal(MotionObservedStateVocabulary.InterpretationUnavailable, state.InterpretationState);
                Assert.Equal(MotionObservedStateVocabulary.NativeUnitNone, state.NativeUnit);
                Assert.Null(state.NativeValueText);
                Assert.Equal(outcome.Code, state.DetailCode);
            }
        }
    }

    [Fact]
    public void The_Digital_Output_Candidate_Reports_Unavailable_Inside_The_Closed_Namespace()
    {
        using var session = new MotionObservationSession();

        for (var outputNumber = MotionObservedStateVocabulary.MinimumOutputNumber;
             outputNumber <= MotionObservedStateVocabulary.MaximumOutputNumber;
             outputNumber++)
        {
            var outcome = session.Observe(OutputRequest(outputNumber));

            Assert.False(outcome.Accepted);
            Assert.Equal(MotionObservedStateVocabulary.ObservationSurfaceNotAuthorized, outcome.Code);
            Assert.Equal(MotionObservedStateVocabulary.InterpretationUnavailable, outcome.InterpretationState);

            var state = Assert.Single(outcome.States);
            Assert.Equal(MotionObservedStateVocabulary.KindDigitalOutput, state.Kind);
            Assert.Equal(MotionObservedStateVocabulary.NoAxis, state.AxisId);
            Assert.Equal(outputNumber, state.OutputNumber);
            Assert.Null(state.NativeValueText);
        }
    }

    [Fact]
    public void The_Four_Kinds_Are_The_Complete_And_Closed_Allow_List()
    {
        Assert.Equal(
            new[]
            {
                MotionObservedStateVocabulary.KindAxisStatus,
                MotionObservedStateVocabulary.KindCommandPosition,
                MotionObservedStateVocabulary.KindActualPosition,
                MotionObservedStateVocabulary.KindDigitalOutput,
            },
            MotionObservedStateVocabulary.ObservationKinds);

        Assert.Equal(4, MotionObservedStateVocabulary.CandidateIds.Count);
        Assert.Equal(
            new[] { "B2-OBS-1", "B2-OBS-2", "B2-OBS-3", "B2-OBS-4" },
            MotionObservedStateVocabulary.CandidateIds);

        var callerView = Assert.IsAssignableFrom<IList<string>>(MotionObservedStateVocabulary.ObservationKinds);
        Assert.Throws<NotSupportedException>(() => callerView[0] = "UNKNOWN_KIND");
        Assert.Equal(4, MotionObservedStateVocabulary.ObservationKinds.Count);
    }

    [Fact]
    public void Only_Unavailable_And_Refused_Are_Reachable_Interpretation_States()
    {
        Assert.Equal(6, MotionObservedStateVocabulary.InterpretationStates.Count);
        Assert.Equal(
            new[]
            {
                MotionObservedStateVocabulary.InterpretationUnavailable,
                MotionObservedStateVocabulary.InterpretationRefused,
            },
            MotionObservedStateVocabulary.ReachableInterpretationStates);

        var reached = new List<string>();
        using var session = new MotionObservationSession();

        foreach (var kind in MotionObservedStateVocabulary.ObservationKinds)
        {
            var requests = string.Equals(kind, MotionObservedStateVocabulary.KindDigitalOutput, StringComparison.Ordinal)
                ? new[] { OutputRequest(1), OutputRequest(0) }
                : new[] { AxisRequest(kind, "A"), AxisRequest(kind, "Z") };

            foreach (var request in requests)
            {
                var outcome = session.Observe(request);
                reached.Add(outcome.InterpretationState);
                Assert.False(outcome.Accepted);
                Assert.False(outcome.ObservationIssued);
                Assert.Contains(outcome.InterpretationState, MotionObservedStateVocabulary.ReachableInterpretationStates);
                Assert.DoesNotContain(
                    MotionObservedStateVocabulary.InterpretationVerified, outcome.InterpretationState, StringComparison.Ordinal);
                Assert.DoesNotContain(
                    MotionObservedStateVocabulary.InterpretationRawObserved, outcome.InterpretationState, StringComparison.Ordinal);
                Assert.DoesNotContain(
                    MotionObservedStateVocabulary.InterpretationUnconfirmed, outcome.InterpretationState, StringComparison.Ordinal);
                Assert.DoesNotContain(
                    MotionObservedStateVocabulary.InterpretationStale, outcome.InterpretationState, StringComparison.Ordinal);
            }
        }

        Assert.Contains(MotionObservedStateVocabulary.InterpretationUnavailable, reached);
        Assert.Contains(MotionObservedStateVocabulary.InterpretationRefused, reached);
    }

    [Fact]
    public void No_Success_Outcome_Exists_Across_The_Whole_Allow_List()
    {
        using var session = new MotionObservationSession();

        for (var observation = 0; observation < 40; observation++)
        {
            var outcome = session.Observe(OutputRequest((observation % 6) + 1));
            Assert.False(outcome.Accepted);
            Assert.False(outcome.ObservationIssued);
            Assert.Equal(0, outcome.States.Count(state => state.NativeValueText is not null));
            Assert.Equal(0, outcome.States.Count(state => state.NativeUnit != MotionObservedStateVocabulary.NativeUnitNone));
        }
    }

    // ------------------------------------------------------------------------ request refusals

    [Fact]
    public void An_Unknown_Kind_Is_Refused_Without_A_State_Or_A_Command()
    {
        using var session = new MotionObservationSession();

        foreach (var kind in new[] { "UNKNOWN_KIND", string.Empty, " " })
        {
            var outcome = session.Observe(new MotionObservationRequest { Kind = kind, AxisId = "A" });

            Assert.False(outcome.Accepted);
            Assert.False(outcome.ObservationIssued);
            Assert.Equal(MotionObservedStateVocabulary.ObservationKindUnknown, outcome.Code);
            Assert.Equal(MotionObservedStateVocabulary.InterpretationRefused, outcome.InterpretationState);
            Assert.Empty(outcome.States);
        }
    }

    [Fact]
    public void An_Unknown_Axis_Is_Refused()
    {
        using var session = new MotionObservationSession();

        foreach (var axisId in new[] { "Z", "X", string.Empty, MotionObservedStateVocabulary.NoAxis })
        {
            var outcome = session.Observe(AxisRequest(MotionObservedStateVocabulary.KindAxisStatus, axisId));

            Assert.Equal(MotionObservedStateVocabulary.ObservationAxisUnknown, outcome.Code);
            Assert.Equal(MotionObservedStateVocabulary.InterpretationRefused, outcome.InterpretationState);
            Assert.Empty(outcome.States);
        }
    }

    [Fact]
    public void An_Output_Number_Outside_The_Closed_Namespace_Is_Refused()
    {
        using var session = new MotionObservationSession();

        foreach (var outputNumber in new[] { 0, -1, 5, int.MaxValue })
        {
            var outcome = session.Observe(OutputRequest(outputNumber));

            Assert.Equal(MotionObservedStateVocabulary.ObservationSubjectInvalid, outcome.Code);
            Assert.Empty(outcome.States);
        }
    }

    [Fact]
    public void An_Axis_And_An_Output_Together_Are_Refused_As_An_Invalid_Subject()
    {
        using var session = new MotionObservationSession();

        var withAxis = session.Observe(new MotionObservationRequest
        {
            Kind = MotionObservedStateVocabulary.KindDigitalOutput,
            AxisId = "A",
            OutputNumber = 1,
        });
        var withOutput = session.Observe(new MotionObservationRequest
        {
            Kind = MotionObservedStateVocabulary.KindAxisStatus,
            AxisId = "A",
            OutputNumber = 1,
        });

        Assert.Equal(MotionObservedStateVocabulary.ObservationSubjectInvalid, withAxis.Code);
        Assert.Equal(MotionObservedStateVocabulary.ObservationSubjectInvalid, withOutput.Code);
        Assert.Empty(withAxis.States);
        Assert.Empty(withOutput.States);
    }

    // ----------------------------------------------------------------- one owner, one outstanding

    [Fact]
    public void Only_One_Observation_May_Be_Outstanding_And_Nothing_Is_Queued()
    {
        using var session = new MotionObservationSession();

        Assert.True(session.TryBeginObservation());
        Assert.True(session.ObservationInFlight);

        var busy = session.Observe(AxisRequest(MotionObservedStateVocabulary.KindAxisStatus, "A"));
        Assert.Equal(MotionObservedStateVocabulary.OperationBusy, busy.Code);
        Assert.Equal(MotionObservedStateVocabulary.InterpretationRefused, busy.InterpretationState);
        Assert.Empty(busy.States);

        session.EndObservation();
        Assert.False(session.ObservationInFlight);

        var available = session.Observe(AxisRequest(MotionObservedStateVocabulary.KindAxisStatus, "A"));
        Assert.Equal(MotionObservedStateVocabulary.ObservationSurfaceNotAuthorized, available.Code);
        Assert.Single(available.States);
    }

    [Fact]
    public void Two_Sessions_Share_No_State_And_Disposing_One_Does_Not_Close_The_Other()
    {
        using var first = new MotionObservationSession();
        using var second = new MotionObservationSession();

        first.Dispose();

        Assert.True(first.IsClosed);
        Assert.False(second.IsClosed);
        Assert.Equal(MotionObservedStateVocabulary.ObservationSessionClosed, first.Observe(OutputRequest(1)).Code);
        Assert.Equal(
            MotionObservedStateVocabulary.ObservationSurfaceNotAuthorized,
            second.Observe(OutputRequest(1)).Code);
        Assert.Equal(second.CurrentGeneration, second.Observe(OutputRequest(2)).Generation);
    }

    // ------------------------------------------------------------------------ generation fence

    [Fact]
    public void A_Result_From_A_Previous_Generation_Is_Rejected_As_Stale()
    {
        using var session = new MotionObservationSession();
        var firstGeneration = session.CurrentGeneration;

        var next = session.InvalidateGeneration();

        Assert.True(next > firstGeneration);
        Assert.False(session.TryAcceptResult(firstGeneration));
        Assert.Equal(MotionObservedStateVocabulary.StaleGenerationResult, session.LastRefusalCode);
        Assert.True(session.TryAcceptResult(next));
        Assert.Equal(string.Empty, session.LastRefusalCode);
    }

    [Fact]
    public void A_Result_For_The_Current_Generation_Cannot_Be_Replayed()
    {
        using var session = new MotionObservationSession();
        var generation = session.CurrentGeneration;

        Assert.True(session.TryAcceptResult(generation));
        Assert.False(session.TryAcceptResult(generation));
        Assert.Equal(MotionObservedStateVocabulary.ResultReplayed, session.LastRefusalCode);
    }

    [Fact]
    public void A_New_Generation_Accepts_Exactly_One_New_Result()
    {
        using var session = new MotionObservationSession();
        Assert.True(session.TryAcceptResult(session.CurrentGeneration));

        var next = session.InvalidateGeneration();
        Assert.True(session.TryAcceptResult(next));
        Assert.False(session.TryAcceptResult(next));
        Assert.Equal(MotionObservedStateVocabulary.ResultReplayed, session.LastRefusalCode);
    }

    [Fact]
    public void A_Closed_Session_Rejects_Every_Result_And_Is_Idempotent()
    {
        var session = new MotionObservationSession();
        var generation = session.CurrentGeneration;
        session.Dispose();
        session.Dispose();

        Assert.False(session.TryAcceptResult(generation));
        Assert.Equal(MotionObservedStateVocabulary.ObservationSessionClosed, session.LastRefusalCode);
        Assert.True(session.IsClosed);
        Assert.False(session.ObservationInFlight);
    }

    [Fact]
    public void A_Closed_Session_Refuses_Observe_Even_For_A_Valid_Subject()
    {
        var session = new MotionObservationSession();
        session.Dispose();

        var outcome = session.Observe(AxisRequest(MotionObservedStateVocabulary.KindActualPosition, "D"));

        Assert.False(outcome.Accepted);
        Assert.Equal(MotionObservedStateVocabulary.ObservationSessionClosed, outcome.Code);
        Assert.Equal(MotionObservedStateVocabulary.InterpretationRefused, outcome.InterpretationState);
        Assert.Empty(outcome.States);
    }

    // ------------------------------------------------------------------- model honesty (shape)

    [Fact]
    public void The_State_Model_Has_No_Public_Constructor_And_No_Public_Factory()
    {
        Assert.Empty(typeof(MotionObservedState).GetConstructors());

        var publicFactories = typeof(MotionObservedState)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.DeclaringType == typeof(MotionObservedState))
            .ToArray();
        Assert.Empty(publicFactories);

        var publicOutcomeFactories = typeof(MotionObservationOutcome)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.DeclaringType == typeof(MotionObservationOutcome))
            .ToArray();
        Assert.Empty(publicOutcomeFactories);
    }

    [Fact]
    public void The_State_Model_Exposes_No_Position_Limit_Home_Brake_Fault_Or_Safe_Member()
    {
        var names = typeof(MotionObservedState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "AxisId",
                "DetailCode",
                "InterpretationState",
                "Kind",
                "NativeUnit",
                "NativeValueText",
                "OutputNumber",
            },
            names);
    }

    [Fact]
    public void A_Valid_Outcome_Detail_Carries_No_Address_Value_Or_Path()
    {
        using var session = new MotionObservationSession();
        var outcome = session.Observe(AxisRequest(MotionObservedStateVocabulary.KindCommandPosition, "B"));

        Assert.DoesNotMatch(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", outcome.Detail);
        Assert.DoesNotContain("\\", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("/", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(":", outcome.Detail, StringComparison.Ordinal);
    }
}
