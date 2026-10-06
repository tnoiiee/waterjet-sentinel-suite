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
    private static JsonObject SerializeToObject<T>(T value) =>
        JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(value, ContractJson.Options))!;

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

        var json = SerializeToObject(slot);
        Assert.Equal("SLOT-R5-C07", (string?)json["slotId"]);
        Assert.Equal("CANNON", (string?)json["slotType"]);
        Assert.Equal("REAR", (string?)json["wall"]);
        Assert.True(json.ContainsKey("sensorId"));
        Assert.Equal(JsonValueKind.Null, json["sensorId"]!.GetValueKind());
    }

    [Fact]
    public void Delta_RequiredTest1_ActiveJob_Absent_Serializes_To_No_Key()
    {
        // Required test 1 (Owner review): field untouched => the JSON has NO
        // "activeJob" key at all (never "activeJob": null, never empty).
        var delta = BareDelta(); // ActiveJob left at Optional.Absent

        var json = SerializeToObject(delta);

        // Envelope keys always present:
        Assert.Equal("delta", (string?)json["kind"]);
        Assert.Equal("wjss.delta/1", (string?)json["schema"]);
        Assert.Equal(1, (int?)json["apiVersion"]);
        Assert.Equal(10, (int?)json["previousRevision"]);
        Assert.Equal(11, (int?)json["revision"]);

        // Everything untouched must be ABSENT (never null, never empty):
        Assert.False(json.ContainsKey("sensors"));
        Assert.False(json.ContainsKey("pump"));
        Assert.False(json.ContainsKey("queue"));
        Assert.False(json.ContainsKey("sequence"));
        Assert.False(json.ContainsKey("activeJob"));

        // Required test 5: the wall map is static and Snapshot-only; it may
        // never appear in a Delta key set.
        Assert.False(json.ContainsKey("wallMap"));
    }

    [Fact]
    public void Delta_RequiredTest2_ActiveJob_Object_Serializes_To_Replacement()
    {
        // Required test 2: replacement => JSON carries the full activeJob object.
        var delta = BareDelta() with { ActiveJob = Optional<ActiveCleaningJobState>.Present(BuildJob()) };

        var json = SerializeToObject(delta);

        Assert.True(json.ContainsKey("activeJob"));
        var job = Assert.IsType<JsonObject>(json["activeJob"]);
        Assert.Equal("J-300", (string?)job["jobId"]);
        Assert.Equal("H7", (string?)job["targetSensorId"]);
        Assert.Equal("RUNNING", (string?)job["lifecycle"]);
    }

    [Fact]
    public void Delta_RequiredTest3_ActiveJob_Cleared_Serializes_To_Explicit_Null()
    {
        // Required test 3: clear => JSON carries an explicit "activeJob": null.
        var delta = BareDelta() with { ActiveJob = Optional<ActiveCleaningJobState>.Cleared };

        var json = SerializeToObject(delta);

        Assert.True(json.ContainsKey("activeJob")); // present, unlike Absent
        Assert.Equal(JsonValueKind.Null, json["activeJob"]!.GetValueKind());

        // And the raw serialized text carries the exact encoding the mirror expects:
        var raw = JsonSerializer.Serialize(delta, ContractJson.Options);
        Assert.Contains("\"activeJob\": null", raw, StringComparison.Ordinal);
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

        Assert.True(absent.ActiveJob.IsAbsent);
        Assert.True(cleared.ActiveJob.IsCleared);
        Assert.False(cleared.ActiveJob.IsPresent);
        Assert.True(replaced.ActiveJob.IsPresent);
        Assert.Equal("J-300", replaced.ActiveJob.Value.JobId);

        // Re-serialization is stable across the round-trip:
        Assert.False(SerializeToObject(absent).ContainsKey("activeJob"));
        Assert.Equal(JsonValueKind.Null, SerializeToObject(cleared)["activeJob"]!.GetValueKind());
        Assert.NotNull(SerializeToObject(replaced)["activeJob"]);
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
        // explicit null - "no Active Job" must be visible, not absent.
        var payload = new { activeJob = (object?)null };
        var json = JsonSerializer.Serialize(payload, ContractJson.Options);
        Assert.Contains("\"activeJob\": null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Routes_Are_Frozen_For_The_Health_Stub()
    {
        Assert.Equal("/health/live", ApiRoutes.HealthLive);
        Assert.Equal("/health/ready", ApiRoutes.HealthReady);
    }
}
