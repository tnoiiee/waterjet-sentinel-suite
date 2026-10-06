using System.Text.Json;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Equality-operator coverage for <see cref="Optional{T}"/> added with the
/// CA2231 correction (Owner-local build of 2026-10-07). The operators must
/// be exactly the existing <c>Equals</c> semantics: same behaviour, no wire,
/// hash, or three-state presence change.
/// </summary>
public sealed class OptionalPresenceTests
{
    private static Optional<string> Present(string v) => Optional<string>.Present(v);

    [Fact]
    public void Absent_Equals_Absent()
    {
        Assert.True(Optional<string>.Absent == Optional<string>.Absent);
        Assert.True(default(Optional<string>) == Optional<string>.Absent);
        Assert.False(Optional<string>.Absent != Optional<string>.Absent);
    }

    [Fact]
    public void Absent_NotEquals_Cleared()
    {
        Assert.True(Optional<string>.Absent != Optional<string>.Cleared);
        Assert.False(Optional<string>.Absent == Optional<string>.Cleared);
    }

    [Fact]
    public void Cleared_Equals_Cleared()
    {
        Assert.True(Optional<string>.Cleared == Optional<string>.Cleared);
        Assert.False(Optional<string>.Cleared != Optional<string>.Cleared);
    }

    [Fact]
    public void Present_Equal_Value_Is_Equal()
    {
        // Round-5 semantics: Present payloads delegate to
        // EqualityComparer<T>.Default - value equality for string (and records).
        Assert.True(Present("A") == Present("A"));
        Assert.True(Present("A").Equals(Present("A")));
        Assert.False(Present("A") != Present("A"));
        Assert.True(Present("A") != Present("B"));
    }

    [Fact]
    public void Present_NotEquals_Cleared()
    {
        Assert.True(Present("A") != Optional<string>.Cleared);
        Assert.False(Present("A") == Optional<string>.Cleared);
        // Present is never equal to Absent either (three distinct states).
        Assert.True(Present("A") != Optional<string>.Absent);
    }

    [Fact]
    public void Operators_Mirror_Equals_For_All_Nine_State_Pairs()
    {
        var absent = Optional<string>.Absent;
        var cleared = Optional<string>.Cleared;
        var a = Present("A");
        var a2 = Present("A");

        var values = new[] { absent, cleared, a, a2 };
        foreach (var left in values)
        {
            foreach (var right in values)
            {
                Assert.Equal(left.Equals(right), left == right);
                Assert.Equal(!left.Equals(right), left != right);
            }
        }

        // Present vs Present across distinguishable values is the only
        // asymmetric-with-state pair and must still agree:
        var b = Present("B");
        Assert.Equal(a.Equals(b), a == b);
    }

    [Fact]
    public void Contract_Record_Payload_Compares_By_Value_Equality()
    {
        // Two independently deserialized (reference-distinct) but equal
        // ActiveCleaningJobState payloads compare equal: Equals delegates to
        // EqualityComparer<T>.Default, which honours the record's value
        // equality - and the hash codes match accordingly.
        const string JobJson = """
            {"jobId":"J-1","targetSensorId":"H7","jetId":"SYN-JET-01","valveId":"SYN-VLV-01",
             "phase":"P1","phaseLabel":"P1","phaseIndex":1,
             "startedAt":"2026-10-07T00:00:00.000Z","phaseStartedAt":"2026-10-07T00:00:00.000Z",
             "phaseProgress":0.0,"lifecycle":"RUNNING","cleaningPhase":"IN_PROGRESS",
             "dispatch":{"dispatchId":"D-1","queueRevisionBefore":0,"queueRevisionAfter":1,
               "queueEntryId":"Q-1","positionBefore":1,"sensorId":"H7","sourceReason":"DIRTY_SCORE",
               "jobId":"J-1","origin":"FIXTURE","dispatchedAt":"2026-10-07T00:00:00.000Z"},
             "safeReturn":null}
            """;
        var job1 = JsonSerializer.Deserialize<ActiveCleaningJobState>(JobJson, ContractJson.Options)!;
        var job2 = JsonSerializer.Deserialize<ActiveCleaningJobState>(JobJson, ContractJson.Options)!;

        Assert.NotSame(job1, job2);
        Assert.True(Optional<ActiveCleaningJobState>.Present(job1) == Optional<ActiveCleaningJobState>.Present(job2));
        Assert.Equal(
            Optional<ActiveCleaningJobState>.Present(job1).GetHashCode(),
            Optional<ActiveCleaningJobState>.Present(job2).GetHashCode());
        Assert.True(Optional<ActiveCleaningJobState>.Present(job1) != Optional<ActiveCleaningJobState>.Cleared);
        Assert.True(Optional<ActiveCleaningJobState>.Present(job1) != Optional<ActiveCleaningJobState>.Absent);
    }

    [Fact]
    public void Present_Delegates_To_Default_Comparer_For_Reference_Types()
    {
        // A payload type with no equality overrides keeps the Default comparer's
        // reference semantics: same instance -> equal, distinct instances ->
        // not equal. Optional adds no graph comparison of its own.
        var shared = new object();
        Assert.True(Optional<object>.Present(shared) == Optional<object>.Present(shared));
        Assert.True(Optional<object>.Present(new object()) != Optional<object>.Present(new object()));
    }

    [Fact]
    public void HashCode_Conventions_Hold_For_Equal_Values()
    {
        // Equal (per the new operators) implies equal hash codes; the three
        // states keep the pre-existing hash scheme untouched.
        Assert.Equal(Present("A").GetHashCode(), Present("A").GetHashCode());
        Assert.Equal(Optional<string>.Absent.GetHashCode(), default(Optional<string>).GetHashCode());
        Assert.NotEqual(Optional<string>.Absent.GetHashCode(), Optional<string>.Cleared.GetHashCode());
    }
}
