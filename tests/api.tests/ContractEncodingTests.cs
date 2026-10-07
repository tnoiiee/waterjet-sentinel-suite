using System.Text.Json;
using System.Text.Json.Nodes;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Encodes the JSON contract expectations on the .NET side so an Owner-local
/// run proves the serializer behaves exactly as the golden fixtures and the
/// TypeScript mirror assume: camelCase keys, UPPER_SNAKE enum strings, sparse
/// Deltas (absent key = unchanged), explicit nulls in Snapshots, and the
/// accepted three-state <c>activeJob</c> Delta encoding
/// (absent = unchanged / object = replace / null = clear).
/// </summary>
public sealed class ContractEncodingTests
{
    // Wire-token assertions go through JsonDocument/JsonElement: a JSON null
    // materializes as a CLR-null JsonNode reference in the JsonNode model, so
    // presence + token kind must be read from the document, never dereferenced
    // off a JsonNode indexer (Owner-local round 5: NRE class).
    private static JsonDocument SerializeToDocument<T>(T value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, ContractJson.Options));

    /// <summary>A complete, fixture-flavoured Active Job (synthetic values only).</summary>
    private const string ActiveJobJson = """
        {
          "jobId": "J-300",
          "targetSensorId": "H7",
          "jetId": "SYN-JET-05",
          "valveId": "SYN-VLV-05",
          "phase": "P4",
          "phaseLabel": "PHASE-4 (synthetic fixture)",
          "phaseIndex": 4,
          "startedAt": "2026-10-07T00:00:00.000Z",
          "phaseStartedAt": "2026-10-07T00:00:00.000Z",
          "phaseProgress": 0.5,
          "lifecycle": "RUNNING",
          "cleaningPhase": "IN_PROGRESS",
          "dispatch": {
            "dispatchId": "D-100",
            "queueRevisionBefore": 4,
            "queueRevisionAfter": 5,
            "queueEntryId": "Q-200",
            "positionBefore": 1,
            "sensorId": "H7",
            "sourceReason": "DIRTY_SCORE",
            "jobId": "J-300",
            "origin": "FIXTURE",
            "dispatchedAt": "2026-10-07T00:00:00.000Z"
          },
          "safeReturn": null
        }
        """;

    private static ActiveCleaningJobState BuildJob() =>
        JsonSerializer.Deserialize<ActiveCleaningJobState>(ActiveJobJson, ContractJson.Options)!;

    private static OperationalDelta BareDelta() => new()
    {
        Kind = "delta",
        Schema = SchemaIds.Delta,
        ApiVersion = SchemaIds.ApiVersion,
        PreviousRevision = 10,
        Revision = 11,
        GeneratedAt = "2026-10-07T00:00:00.000Z",
    };

    [Fact]
    public void Wall_Map_Slot_Serializes_CamelCase_And_Explicit_Nulls()
    {
        var slot = new WallMapSlot
        {
            SlotId = "SLOT-R5-C07",
            SlotType = SlotType.CANNON,
            Wall = Wall.REAR,
            LogicalColumn = 7,
            LogicalRow = 5,
            WallColumn = 3,
            WallRow = 5,
            SensorId = null,
            EquipmentId = "CANNON_REAR",
        };

        using var doc = SerializeToDocument(slot);
        var root = doc.RootElement;
        Assert.Equal("SLOT-R5-C07", root.GetProperty("slotId").GetString());
        Assert.Equal("CANNON", root.GetProperty("slotType").GetString());
        Assert.Equal("REAR", root.GetProperty("wall").GetString());
        // (B) explicit null: key present, token kind Null - no JsonNode dereference.
        Assert.True(root.TryGetProperty("sensorId", out var sensorId));
        Assert.Equal(JsonValueKind.Null, sensorId.ValueKind);
    }

    [Fact]
    public void Delta_RequiredTest1_ActiveJob_Absent_Serializes_To_No_Key()
    {
        // Required test 1 (Owner review): field untouched => the JSON has NO
        // "activeJob" key at all (never "activeJob": null, never empty).
        var delta = BareDelta(); // ActiveJob left at Optional.Absent

        using var doc = SerializeToDocument(delta);
        var root = doc.RootElement;

        // Envelope keys always present:
        Assert.Equal("delta", root.GetProperty("kind").GetString());
        Assert.Equal("wjss.delta/1", root.GetProperty("schema").GetString());
        Assert.Equal(1, root.GetProperty("apiVersion").GetInt32());
        Assert.Equal(10, root.GetProperty("previousRevision").GetInt32());
        Assert.Equal(11, root.GetProperty("revision").GetInt32());

        // (A) Everything untouched must be ABSENT (never null, never empty):
        Assert.False(root.TryGetProperty("sensors", out _));
        Assert.False(root.TryGetProperty("pump", out _));
        Assert.False(root.TryGetProperty("queue", out _));
        Assert.False(root.TryGetProperty("sequence", out _));
        Assert.False(root.TryGetProperty("activeJob", out _));

        // Required test 5: the wall map is static and Snapshot-only; it may
        // never appear in a Delta key set.
        Assert.False(root.TryGetProperty("wallMap", out _));
    }

    [Fact]
    public void Delta_RequiredTest2_ActiveJob_Object_Serializes_To_Replacement()
    {
        // Required test 2: replacement => JSON carries the full activeJob object.
        var delta = BareDelta() with { ActiveJob = Optional<ActiveCleaningJobState>.Present(BuildJob()) };

        using var doc = SerializeToDocument(delta);

        // (C) replacement: key present, token kind Object, full payload inside.
        Assert.True(doc.RootElement.TryGetProperty("activeJob", out var job));
        Assert.Equal(JsonValueKind.Object, job.ValueKind);
        Assert.Equal("J-300", job.GetProperty("jobId").GetString());
        Assert.Equal("H7", job.GetProperty("targetSensorId").GetString());
        Assert.Equal("RUNNING", job.GetProperty("lifecycle").GetString());
    }

    [Fact]
    public void Delta_RequiredTest3_ActiveJob_Cleared_Serializes_To_Explicit_Null()
    {
        // Required test 3: clear => JSON carries an explicit "activeJob": null.
        var delta = BareDelta() with { ActiveJob = Optional<ActiveCleaningJobState>.Cleared };

        using var doc = SerializeToDocument(delta);

        // (B) clear: key PRESENT with token kind Null - distinct from Absent (A),
        // read from the document so the JSON null never dereferences a JsonNode.
        Assert.True(doc.RootElement.TryGetProperty("activeJob", out var clearedToken));
        Assert.Equal(JsonValueKind.Null, clearedToken.ValueKind);

        // (Round 6c: a serialized-text Contains follow-up was removed here. Presence +
        // JsonValueKind.Null above is the same claim, proven without depending on the
        // indented writer's whitespace.)
    }

    [Fact]
    public void Delta_RequiredTest4_Deserialization_Distinguishes_All_Three_States()
    {
        // Required test 4: round-trip keeps absent / null / object apart.
        var envelope = (JsonObject)JsonNode.Parse(
            """
            {"kind":"delta","schema":"wjss.delta/1","apiVersion":1,
             "previousRevision":10,"revision":11,"generatedAt":"2026-10-07T00:00:00.000Z"}
            """)!;

        string absentJson = envelope.ToJsonString();

        var clearedNode = (JsonObject)envelope.DeepClone();
        clearedNode["activeJob"] = null;
        string clearedJson = clearedNode.ToJsonString();

        var replacedNode = (JsonObject)envelope.DeepClone();
        replacedNode["activeJob"] = JsonNode.Parse(ActiveJobJson);
        string replacedJson = replacedNode.ToJsonString();

        var absent = JsonSerializer.Deserialize<OperationalDelta>(absentJson, ContractJson.Options)!;
        var cleared = JsonSerializer.Deserialize<OperationalDelta>(clearedJson, ContractJson.Options)!;
        var replaced = JsonSerializer.Deserialize<OperationalDelta>(replacedJson, ContractJson.Options)!;

        // Deserialized state distinction...
        Assert.True(absent.ActiveJob.IsAbsent);
        Assert.True(cleared.ActiveJob.IsCleared);
        Assert.False(cleared.ActiveJob.IsPresent);
        Assert.True(replaced.ActiveJob.IsPresent);
        Assert.Equal("J-300", replaced.ActiveJob.Value.JobId);

        // ...and re-serialization stability across the round-trip, all three
        // states proven by wire presence + token kind (no JsonNode dereference):
        using (var absentDoc = SerializeToDocument(absent))
        {
            Assert.False(absentDoc.RootElement.TryGetProperty("activeJob", out _)); // A
        }

        using (var clearedDoc = SerializeToDocument(cleared))
        {
            Assert.True(clearedDoc.RootElement.TryGetProperty("activeJob", out var t)); // B
            Assert.Equal(JsonValueKind.Null, t.ValueKind);
        }

        using (var replacedDoc = SerializeToDocument(replaced))
        {
            Assert.True(replacedDoc.RootElement.TryGetProperty("activeJob", out var t)); // C
            Assert.Equal(JsonValueKind.Object, t.ValueKind);
            Assert.Equal("J-300", t.GetProperty("jobId").GetString());
        }
    }

    [Fact]
    public void Delta_ActiveJob_Present_Rejects_Null_Construction()
    {
        // Present(null) is a programming error, not a third wire state: it
        // must throw at construction, keeping the encoding unambiguous.
        Assert.Throws<ArgumentNullException>(
            () => Optional<ActiveCleaningJobState>.Present(null!));
    }

    [Fact]
    public void Snapshot_Keeps_Explicit_Nulls_For_Cleared_Legacy_Encoding()
    {
        // In a Snapshot (global Never-ignore), a null member is written as an
        // explicit null - "no Active Job" must be visible, not absent. Proven
        // structurally from the parsed document (round 6c): the serialized-text
        // Contains this test used to rely on matched formatting whitespace, not
        // contract meaning.
        var payload = new { activeJob = (object?)null };
        using var doc = SerializeToDocument(payload);
        Assert.True(doc.RootElement.TryGetProperty("activeJob", out var token));
        Assert.Equal(JsonValueKind.Null, token.ValueKind);
    }

    [Fact]
    public void Api_Routes_Are_Frozen_For_The_Health_Stub()
    {
        Assert.Equal("/health/live", ApiRoutes.HealthLive);
        Assert.Equal("/health/ready", ApiRoutes.HealthReady);
    }
}
