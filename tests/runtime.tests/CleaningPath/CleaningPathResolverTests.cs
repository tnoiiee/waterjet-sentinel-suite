using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.Motion;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Phase 3A pure Cleaning Path resolver tests: canonical/default assignment selection, the
/// per-entry Effective-WJ override contract with provenance, topology-derived Isolation Valve /
/// WSB / axis / brake identities, fail-closed refusals without fallback, and immutability of the
/// Sensor configuration and the validated snapshot. Scenario values are synthetic (a recorded
/// default that contradicts canonical coverage stands in for a stale assignment); every derived
/// identity comes from the committed canonical catalogs. The "G+201" Sensor is LEFT/UPPER
/// (row 1, column 1) and is covered by WJ3 (target LEFT/UPPER; WSB2 / MC-WSB2 / C / D).
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public sealed class CleaningPathResolverTests
{
    private const string Sensor = "G+201";

    [Fact]
    public void Canonical_Assignment_Is_Used_When_No_Override_Is_Proposed()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ3", "IV3");

        var result = CleaningPathResolver.Resolve(
            snapshot,
            configuration,
            new CleaningPathResolutionRequest { SensorId = Sensor });

        Assert.True(result.Accepted);
        Assert.Equal(CleaningPathCodes.ResolutionAccepted, result.Code);
        var resolution = result.Resolution!;
        Assert.Equal("WJ3", resolution.EffectiveWaterJetId);
        Assert.Equal("WJ3", resolution.CanonicalAssignedWaterJetId);
        Assert.Null(resolution.RequestedOverrideWaterJetId);
        Assert.Equal(EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT, resolution.EffectiveWjProvenance);
    }

    [Fact]
    public void Valid_Override_Changes_Effective_Wj_Only()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var pathBefore = snapshot.PathFor(Sensor);
        var centreBefore = snapshot.Centre;

        // Correction scenario: the recorded default contradicts canonical coverage; the override
        // supplies the topology-compatible Water Jet. Synthetic test data only.
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ2", "IV2");

        var result = CleaningPathResolver.Resolve(
            snapshot,
            configuration,
            new CleaningPathResolutionRequest { SensorId = Sensor, RequestedOverrideWaterJetId = "WJ3" });

        Assert.True(result.Accepted);
        var resolution = result.Resolution!;
        Assert.Equal("WJ3", resolution.EffectiveWaterJetId);
        Assert.Equal("WJ2", resolution.CanonicalAssignedWaterJetId);
        Assert.Equal("WJ3", resolution.RequestedOverrideWaterJetId);
        Assert.Equal(EffectiveWaterJetProvenance.ENTRY_WJ_OVERRIDE, resolution.EffectiveWjProvenance);

        // Nothing but the routing identity changes: the Sensor-owned path and Centre are untouched.
        Assert.Same(pathBefore, resolution.Path);
        Assert.Same(centreBefore, resolution.Centre);
    }

    [Fact]
    public void Valid_Override_Does_Not_Change_Sensor_Owned_Path()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ2", "IV2");

        var withoutOverride = CleaningPathResolver.Resolve(
            snapshot, configuration, new CleaningPathResolutionRequest { SensorId = Sensor });
        var withOverride = CleaningPathResolver.Resolve(
            snapshot,
            configuration,
            new CleaningPathResolutionRequest { SensorId = Sensor, RequestedOverrideWaterJetId = "WJ3" });

        // The no-override resolution refuses (the synthetic default contradicts coverage); the
        // override resolution succeeds — and both leave the snapshot path identical.
        Assert.False(withoutOverride.Accepted);
        Assert.True(withOverride.Accepted);

        var snapshotPath = snapshot.PathFor(Sensor)!;
        var resolvedPath = withOverride.Resolution!.Path;
        Assert.Same(snapshotPath, resolvedPath);
        Assert.Equal("P1", resolvedPath.Points[0].PointId);
        Assert.Equal("P6", resolvedPath.Points[5].PointId);
        for (var index = 0; index < 6; index++)
        {
            Assert.Equal(snapshotPath.Points[index], resolvedPath.Points[index]);
        }
    }

    [Fact]
    public void Paired_Isolation_Valve_Is_Derived_From_Effective_Wj()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ2", "IV2");

        var result = CleaningPathResolver.Resolve(
            snapshot,
            configuration,
            new CleaningPathResolutionRequest { SensorId = Sensor, RequestedOverrideWaterJetId = "WJ3" });

        Assert.True(result.Accepted);
        Assert.Equal("IV3", result.Resolution!.DerivedIsolationValveId);
        Assert.Equal(
            WaterJetTopologyCatalog.FindWaterJet("WJ3")!.DedicatedIsolationValveId,
            result.Resolution!.DerivedIsolationValveId);
    }

    [Fact]
    public void Wsb_Axes_And_Brakes_Are_Derived_From_Canonical_Catalogs()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ3", "IV3");

        var result = CleaningPathResolver.Resolve(
            snapshot, configuration, new CleaningPathResolutionRequest { SensorId = Sensor });

        Assert.True(result.Accepted);
        var resolution = result.Resolution!;

        var binding = MotionTopologyCatalog.FindWaterJetBinding("WJ3");
        Assert.NotNull(binding);
        Assert.Equal(binding!.CabinetId, resolution.CabinetId);
        Assert.Equal(binding.ControllerId, resolution.ControllerId);
        Assert.Equal(binding.HorizontalAxisId, resolution.HorizontalAxisId);
        Assert.Equal(binding.VerticalAxisId, resolution.VerticalAxisId);

        var horizontalAxis = MotionTopologyCatalog.FindAxis(binding.ControllerId, binding.HorizontalAxisId)!;
        var verticalAxis = MotionTopologyCatalog.FindAxis(binding.ControllerId, binding.VerticalAxisId)!;
        Assert.Equal(horizontalAxis.BrakeOutputIdentity, resolution.HorizontalBrakeOutputId);
        Assert.Equal(verticalAxis.BrakeOutputIdentity, resolution.VerticalBrakeOutputId);

        // Canonical values of record for WJ3 (Owner-confirmed allocation).
        Assert.Equal("WSB2", resolution.CabinetId);
        Assert.Equal("MC-WSB2", resolution.ControllerId);
        Assert.Equal("C", resolution.HorizontalAxisId);
        Assert.Equal("D", resolution.VerticalAxisId);
        Assert.Equal("OUTPUT_3", resolution.HorizontalBrakeOutputId);
        Assert.Equal("OUTPUT_4", resolution.VerticalBrakeOutputId);
    }

    [Fact]
    public void Invalid_Wj_Override_Is_Refused_Without_Fallback()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ3", "IV3");

        var result = CleaningPathResolver.Resolve(
            snapshot,
            configuration,
            new CleaningPathResolutionRequest { SensorId = Sensor, RequestedOverrideWaterJetId = "WJ9" });

        Assert.False(result.Accepted);
        Assert.Null(result.Resolution);
        Assert.Equal(CleaningPathCodes.OverrideWaterJetUnknown, result.Code);
    }

    [Fact]
    public void Topology_Incompatible_Override_Is_Refused_Without_Fallback()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ3", "IV3");

        // WJ2 exists but its target coverage (FRONT/UPPER) does not include this Sensor (LEFT/UPPER).
        var result = CleaningPathResolver.Resolve(
            snapshot,
            configuration,
            new CleaningPathResolutionRequest { SensorId = Sensor, RequestedOverrideWaterJetId = "WJ2" });

        Assert.False(result.Accepted);
        Assert.Null(result.Resolution);
        Assert.Equal(CleaningPathCodes.OverrideTopologyIncompatible, result.Code);

        // No fallback: the canonical assignment is NOT silently used after a refused override.
        Assert.NotEqual(CleaningPathCodes.ResolutionAccepted, result.Code);
    }

    [Fact]
    public void Topology_Inconsistent_Canonical_Assignment_Without_Override_Is_Refused()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ2", "IV2");

        var result = CleaningPathResolver.Resolve(
            snapshot, configuration, new CleaningPathResolutionRequest { SensorId = Sensor });

        Assert.False(result.Accepted);
        Assert.Null(result.Resolution);
        Assert.Equal(CleaningPathCodes.AssignmentTopologyIncompatible, result.Code);
    }

    [Fact]
    public void Provenance_Is_Retained_For_Both_Selection_Paths()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var canonicalConfiguration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ3", "IV3");
        var overrideConfiguration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ2", "IV2");

        var canonical = CleaningPathResolver.Resolve(
            snapshot, canonicalConfiguration, new CleaningPathResolutionRequest { SensorId = Sensor });
        var overridden = CleaningPathResolver.Resolve(
            snapshot,
            overrideConfiguration,
            new CleaningPathResolutionRequest { SensorId = Sensor, RequestedOverrideWaterJetId = "WJ3" });

        Assert.True(canonical.Accepted);
        Assert.True(overridden.Accepted);
        Assert.Equal(EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT, canonical.Resolution!.EffectiveWjProvenance);
        Assert.Equal(EffectiveWaterJetProvenance.ENTRY_WJ_OVERRIDE, overridden.Resolution!.EffectiveWjProvenance);
    }

    [Fact]
    public void Sensor_Configuration_And_Snapshot_Are_Unchanged_By_Resolution()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ2", "IV2");
        var configurationBefore = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ2", "IV2");
        var pathBefore = snapshot.PathFor(Sensor);
        var pathsBefore = snapshot.PathsBySensorId;

        var result = CleaningPathResolver.Resolve(
            snapshot,
            configuration,
            new CleaningPathResolutionRequest { SensorId = Sensor, RequestedOverrideWaterJetId = "WJ3" });

        Assert.True(result.Accepted);
        Assert.Equal(configurationBefore, configuration);
        Assert.Same(pathsBefore, snapshot.PathsBySensorId);
        Assert.Same(pathBefore, snapshot.PathFor(Sensor));
    }

    [Fact]
    public void Unknown_Sensor_Mismatched_Configuration_And_Missing_Snapshot_Path_Are_Refused()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ3", "IV3");

        var unknownSensor = CleaningPathResolver.Resolve(
            snapshot, configuration, new CleaningPathResolutionRequest { SensorId = "X9" });
        Assert.False(unknownSensor.Accepted);
        Assert.Equal(CleaningPathCodes.SensorNotProductSensor, unknownSensor.Code);

        var mismatch = CleaningPathResolver.Resolve(
            snapshot,
            SyntheticCleaningPathStores.SensorConfiguration("G1", "WJ3", "IV3"),
            new CleaningPathResolutionRequest { SensorId = Sensor });
        Assert.False(mismatch.Accepted);
        Assert.Equal(CleaningPathCodes.SensorConfigurationIdMismatch, mismatch.Code);

        var partialSnapshot = CleaningPathSnapshot.Create(
            CleaningPathStoreParser.SchemaIdentifier,
            CleaningPathSourceKind.OwnerLocalFile,
            new[] { snapshot.PathFor("G1")! },
            null);
        var missing = CleaningPathResolver.Resolve(
            partialSnapshot, configuration, new CleaningPathResolutionRequest { SensorId = Sensor });
        Assert.False(missing.Accepted);
        Assert.Equal(CleaningPathCodes.SensorNotInSnapshot, missing.Code);
    }

    [Fact]
    public void Centre_Contract_Is_Fixed_In_The_Resolution()
    {
        var snapshot = SyntheticCleaningPathStores.ValidSnapshot();
        var configuration = SyntheticCleaningPathStores.SensorConfiguration(Sensor, "WJ3", "IV3");

        var result = CleaningPathResolver.Resolve(
            snapshot, configuration, new CleaningPathResolutionRequest { SensorId = Sensor });

        Assert.True(result.Accepted);
        Assert.Equal(125, result.Resolution!.Centre.HorizontalMillimetres);
        Assert.Equal(125, result.Resolution!.Centre.VerticalMillimetres);
        Assert.Equal(1250000, result.Resolution!.Centre.HorizontalNative);
        Assert.Equal(1250000, result.Resolution!.Centre.VerticalNative);
    }
}
