using System.Text.Json;
using System.Text.Json.Nodes;
using Wjss.Contracts;

namespace Wjss.FixtureEmission.Tests;

/// <summary>
/// Builds the public-safe examples in config/examples/ from the same canonical
/// structures as the fixtures, with the explicit synthetic labelling required
/// by ADR-0011 and PUBLIC_REPOSITORY_BOUNDARY.md. These files document shape;
/// they are incomplete by design and never a deployment starting point.
/// </summary>
internal static class ExampleFiles
{
    private const string Status = "PUBLIC-SAFE EXAMPLE - SYNTHETIC - INCOMPLETE BY DESIGN - NOT FOR DEPLOYMENT";

    public static string PublishedConfig()
    {
        var snapshot = FixtureGenerator.BuildSnapshot();
        var root = new JsonObject
        {
            ["_label"] = Status,
            ["deviceProfile"] = "SIMULATOR",
            ["configuration"] = new JsonObject
            {
                ["revision"] = snapshot.Config.Revision,
                ["publishedAt"] = snapshot.Config.PublishedAt,
                ["label"] = "SYNTHETIC EXAMPLE - NOT A PRODUCTION VALUE",
                ["dirtyThreshold"] = snapshot.Config.DirtyThreshold,
                ["staleThresholdMs"] = snapshot.Config.StaleThresholdMs,
            },
            ["notes"] = new JsonArray
            {
                "Shape example only. The publication/validation pipeline is Stage 0.3A-F.",
                "Production values must never be committed to this repository.",
            },
            ["fixtureStatus"] = FixtureGenerator.FixtureStatus,
        };

        return root.ToJsonString(ContractJson.Options);
    }

    public static string SensorMap()
    {
        var slots = new JsonArray();
        var scan = 0;

        // Owner critical topology correction (Stage 0.4C-K3): every one of the 108 logical
        // positions is a Product Sensor (I7 and I16 included); no gap slot, no gap-anchor key.
        // tools/sensor-map-example/generate.mjs is the byte-identical Node mirror of this method.
        for (var row = 1; row <= CanonicalSensorMap.LogicalRowCount; row++)
        {
            for (var col = 1; col <= CanonicalSensorMap.LogicalColumnCount; col++)
            {
                var wall = CanonicalSensorMap.WallForColumn(col);
                var (first, _) = CanonicalSensorMap.WallColumns[wall];

                scan++;
                var deviceId = FixtureGenerator.DeviceIdFor(scan);
                var idx = FixtureGenerator.IndexOnDevice(scan);
                var slot = new JsonObject
                {
                    ["slotId"] = $"SLOT-R{row}-C{col:D2}",
                    ["positionKind"] = "SENSOR",
                    ["wall"] = wall.ToString(),
                    ["logicalColumn"] = col,
                    ["logicalRow"] = row,
                    ["wallColumn"] = col - first + 1,
                    ["wallRow"] = row,
                    ["sensorId"] = CanonicalSensorMap.SensorIdFor(row, col),
                    ["scanOrderSynthetic"] = scan,
                    // tcChannels: structured JSON array of EXACTLY two channel strings — never
                    // a comma-delimited scalar. Index 0 = lower channel index (front),
                    // index 1 = higher (rear).
                    ["tcChannels"] = new JsonArray(
                        JsonValue.Create($"{deviceId}:CH{2 * idx:D2}"),
                        JsonValue.Create($"{deviceId}:CH{2 * idx + 1:D2}")),
                };

                slots.Add(slot);
            }
        }

        // Pre-serialization self-validation (Owner round 6): the generated mapping must pass
        // the same TcChannelRules the example tests enforce — 108 slots / 108 sensors /
        // exactly 2 channels per sensor / 216 globally unique channels / central topology invariants.
        TcChannelRules.RequireValidMapping(slots
            .Select(s => JsonSerializer.Deserialize<SensorMapSlotExample>(s!.ToJsonString(), ContractJson.Options)!)
            .ToArray());

        var root = new JsonObject
        {
            ["_label"] = Status,
            ["logicalMatrix"] = new JsonObject
            {
                ["columns"] = CanonicalSensorMap.LogicalColumnCount,
                ["rows"] = CanonicalSensorMap.LogicalRowCount,
                ["sensorLocations"] = CanonicalSensorMap.SensorLocations,
                ["thermocoupleChannels"] = CanonicalSensorMap.ThermocoupleChannelCount,
                ["sensorsPerWall"] = new JsonObject
                {
                    ["LEFT"] = CanonicalSensorMap.SensorsPerWall[Wall.LEFT],
                    ["REAR"] = CanonicalSensorMap.SensorsPerWall[Wall.REAR],
                    ["RIGHT"] = CanonicalSensorMap.SensorsPerWall[Wall.RIGHT],
                    ["FRONT"] = CanonicalSensorMap.SensorsPerWall[Wall.FRONT],
                },
                ["slots"] = slots,
            },
            ["notes"] = new JsonArray
            {
                "scanOrderSynthetic and tcChannels are SYNTHETIC example assignments (device distribution); tcChannels is an exact two-entry array (front = lower channel index, rear = higher), not Production data.",
                "Owner critical topology correction: I7 and I16 are normal Product Sensors. Every one of the 108 logical positions is a Sensor with exactly two thermocouple channels; no position is a gap or a placement anchor.",
                "The logical matrix and wall distribution are the Owner-confirmed structure (108 locations, 216 channels).",
            },
            ["fixtureStatus"] = FixtureGenerator.FixtureStatus,
        };

        return root.ToJsonString(ContractJson.Options);
    }
}
