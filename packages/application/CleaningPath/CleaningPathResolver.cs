using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.Motion;

namespace Wjss.Runtime.Core.CleaningPath;

/// <summary>
/// Where the Effective Water Jet of one resolution came from. The canonical assignment is the
/// existing Sensor authority (<c>SensorConfigurationRecord.AssignedWaterJetId</c>); a queue-entry
/// override may select the Effective Water Jet only and never rewrites the assignment, the Sensor
/// configuration or the Sensor-owned path.
/// </summary>
public enum EffectiveWaterJetProvenance
{
    CANONICAL_ASSIGNMENT = 1,
    ENTRY_WJ_OVERRIDE = 2,
}

/// <summary>
/// The pure resolver input contract: a canonical Sensor identity and an optional proposed
/// per-entry Water Jet override. The contract deliberately carries no Queue, session or
/// execution state.
/// </summary>
public sealed record CleaningPathResolutionRequest
{
    /// <summary>The canonical Sensor identity whose path is being resolved.</summary>
    public required string SensorId { get; init; }

    /// <summary>The optional proposed per-entry override Water Jet identity (for example "WJ4"), or null.</summary>
    public string? RequestedOverrideWaterJetId { get; init; }
}

/// <summary>
/// One immutable resolved Cleaning Path description: the Sensor-owned path, the fixed Centre, the
/// Effective Water Jet with its provenance, the paired Isolation Valve and the WSB / controller /
/// axis / brake-output identities derived from the canonical catalogs. No topology value here is
/// authored data: every identity is derived at resolution time and the external path store never
/// contains one.
/// </summary>
public sealed record ResolvedCleaningPath
{
    public required string SensorId { get; init; }

    /// <summary>The Sensor-owned validated P1–P6 path (never changed by resolution).</summary>
    public required SensorCleaningPath Path { get; init; }

    /// <summary>The fixed Safe Return / Centre contract (125 mm / 125 mm; 1250000 / 1250000 native).</summary>
    public required CleaningPathCentre Centre { get; init; }

    /// <summary>The canonical/default assigned Water Jet (the existing Sensor assignment authority).</summary>
    public required string CanonicalAssignedWaterJetId { get; init; }

    /// <summary>The requested per-entry override, or null when none was proposed.</summary>
    public string? RequestedOverrideWaterJetId { get; init; }

    /// <summary>The routing Water Jet: the valid override when present, otherwise the canonical assignment.</summary>
    public required string EffectiveWaterJetId { get; init; }

    public required EffectiveWaterJetProvenance EffectiveWjProvenance { get; init; }

    /// <summary>The paired Isolation Valve (WJn ↔ IVn) derived from the Effective Water Jet.</summary>
    public required string DerivedIsolationValveId { get; init; }

    /// <summary>The WSB cabinet identity, derived from the canonical motion topology.</summary>
    public required string CabinetId { get; init; }

    /// <summary>The motion controller identity, derived from the canonical motion topology.</summary>
    public required string ControllerId { get; init; }

    /// <summary>The Horizontal axis identity, derived from the canonical motion topology.</summary>
    public required string HorizontalAxisId { get; init; }

    /// <summary>The Vertical axis identity, derived from the canonical motion topology.</summary>
    public required string VerticalAxisId { get; init; }

    /// <summary>The Horizontal axis brake-output identity, derived from the canonical motion topology.</summary>
    public required string HorizontalBrakeOutputId { get; init; }

    /// <summary>The Vertical axis brake-output identity, derived from the canonical motion topology.</summary>
    public required string VerticalBrakeOutputId { get; init; }
}

/// <summary>
/// The resolution result. Refusal is fail-closed with a stable code: there is no fallback to
/// another Water Jet, Isolation Valve, WSB or axis, and a refused override never silently resolves
/// to the canonical assignment.
/// </summary>
public sealed class CleaningPathResolutionResult
{
    private CleaningPathResolutionResult(bool accepted, ResolvedCleaningPath? resolution, string code)
    {
        Accepted = accepted;
        Resolution = resolution;
        Code = code;
    }

    /// <summary>True only when resolution completed with every derivation valid.</summary>
    public bool Accepted { get; }

    /// <summary>The immutable resolution, or null when refused.</summary>
    public ResolvedCleaningPath? Resolution { get; }

    /// <summary>The stable result code (<see cref="CleaningPathCodes"/>).</summary>
    public string Code { get; }

    public static CleaningPathResolutionResult Refused(string code) => new(false, null, code);

    public static CleaningPathResolutionResult FromResolution(ResolvedCleaningPath resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return new CleaningPathResolutionResult(true, resolution, CleaningPathCodes.ResolutionAccepted);
    }
}

/// <summary>
/// The pure, read-only Cleaning Path resolver (Phase 3A). It combines the canonical Sensor
/// configuration, the Sensor-owned validated path from a snapshot, the canonical/default Water Jet
/// assignment, an optional per-entry Effective-WJ override (with provenance) and the canonical
/// Equipment / Motion topology catalogs into one immutable resolved description. The resolver
/// never mutates Sensor configuration, never mutates the snapshot, never writes an override back,
/// never uses the Legacy cleaning-device ordinal as an authority and never copies WSB / axis /
/// brake / Isolation-Valve mappings into the path store.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
public static class CleaningPathResolver
{
    /// <summary>
    /// Resolves one Sensor-owned Cleaning Path. The adopted assignment is validated against the
    /// existing topology rules (<see cref="WaterJetTopologyCatalog.TryRequireAssignment"/>): the
    /// canonical assignment when no override is proposed, the override when one is. Any failure
    /// refuses the resolution.
    /// </summary>
    public static CleaningPathResolutionResult Resolve(
        CleaningPathSnapshot snapshot,
        SensorConfigurationRecord sensorConfiguration,
        CleaningPathResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(sensorConfiguration);
        ArgumentNullException.ThrowIfNull(request);

        if (!CanonicalSensorMap.IsProductSensorId(request.SensorId))
        {
            return CleaningPathResolutionResult.Refused(CleaningPathCodes.SensorNotProductSensor);
        }

        if (!string.Equals(sensorConfiguration.SensorId, request.SensorId, StringComparison.Ordinal))
        {
            return CleaningPathResolutionResult.Refused(CleaningPathCodes.SensorConfigurationIdMismatch);
        }

        var path = snapshot.PathFor(request.SensorId);
        if (path is null)
        {
            return CleaningPathResolutionResult.Refused(CleaningPathCodes.SensorNotInSnapshot);
        }

        if (sensorConfiguration.LogicalRow is < 1 or > 6)
        {
            return CleaningPathResolutionResult.Refused(CleaningPathCodes.ResolutionDerivationFailed);
        }

        var sensorRegion = WaterJetTopologyCatalog.RegionForLogicalRow(sensorConfiguration.LogicalRow);

        string effectiveWaterJetId;
        EffectiveWaterJetProvenance provenance;

        if (request.RequestedOverrideWaterJetId is null)
        {
            if (!WaterJetTopologyCatalog.TryRequireAssignment(
                    request.SensorId,
                    sensorConfiguration.Wall,
                    sensorRegion,
                    sensorConfiguration.AssignedWaterJetId,
                    sensorConfiguration.AssignedIsolationValveId))
            {
                return CleaningPathResolutionResult.Refused(CleaningPathCodes.AssignmentTopologyIncompatible);
            }

            effectiveWaterJetId = sensorConfiguration.AssignedWaterJetId;
            provenance = EffectiveWaterJetProvenance.CANONICAL_ASSIGNMENT;
        }
        else
        {
            var overrideWaterJet = WaterJetTopologyCatalog.FindWaterJet(request.RequestedOverrideWaterJetId);
            if (overrideWaterJet is null)
            {
                return CleaningPathResolutionResult.Refused(CleaningPathCodes.OverrideWaterJetUnknown);
            }

            // Validate the override exactly like an assignment against the existing topology rules;
            // the paired valve is derived from the override itself and is never taken from the
            // Sensor configuration.
            if (!WaterJetTopologyCatalog.TryRequireAssignment(
                    request.SensorId,
                    sensorConfiguration.Wall,
                    sensorRegion,
                    overrideWaterJet.WaterJetId,
                    overrideWaterJet.DedicatedIsolationValveId))
            {
                return CleaningPathResolutionResult.Refused(CleaningPathCodes.OverrideTopologyIncompatible);
            }

            effectiveWaterJetId = overrideWaterJet.WaterJetId;
            provenance = EffectiveWaterJetProvenance.ENTRY_WJ_OVERRIDE;
        }

        // Every downstream identity is derived from the canonical catalogs. A derivation failure
        // refuses the resolution: there is never a fallback identity.
        var waterJet = WaterJetTopologyCatalog.FindWaterJet(effectiveWaterJetId);
        if (waterJet is null || string.IsNullOrWhiteSpace(waterJet.DedicatedIsolationValveId))
        {
            return CleaningPathResolutionResult.Refused(CleaningPathCodes.ResolutionDerivationFailed);
        }

        var binding = MotionTopologyCatalog.FindWaterJetBinding(effectiveWaterJetId);
        if (binding is null
            || string.IsNullOrWhiteSpace(binding.CabinetId)
            || string.IsNullOrWhiteSpace(binding.ControllerId)
            || string.IsNullOrWhiteSpace(binding.HorizontalAxisId)
            || string.IsNullOrWhiteSpace(binding.VerticalAxisId))
        {
            return CleaningPathResolutionResult.Refused(CleaningPathCodes.ResolutionDerivationFailed);
        }

        var horizontalAxis = MotionTopologyCatalog.FindAxis(binding.ControllerId, binding.HorizontalAxisId);
        var verticalAxis = MotionTopologyCatalog.FindAxis(binding.ControllerId, binding.VerticalAxisId);
        if (horizontalAxis is null
            || verticalAxis is null
            || string.IsNullOrWhiteSpace(horizontalAxis.BrakeOutputIdentity)
            || string.IsNullOrWhiteSpace(verticalAxis.BrakeOutputIdentity))
        {
            return CleaningPathResolutionResult.Refused(CleaningPathCodes.ResolutionDerivationFailed);
        }

        return CleaningPathResolutionResult.FromResolution(new ResolvedCleaningPath
        {
            SensorId = request.SensorId,
            Path = path,
            Centre = snapshot.Centre,
            CanonicalAssignedWaterJetId = sensorConfiguration.AssignedWaterJetId,
            RequestedOverrideWaterJetId = request.RequestedOverrideWaterJetId,
            EffectiveWaterJetId = effectiveWaterJetId,
            EffectiveWjProvenance = provenance,
            DerivedIsolationValveId = waterJet.DedicatedIsolationValveId,
            CabinetId = binding.CabinetId,
            ControllerId = binding.ControllerId,
            HorizontalAxisId = binding.HorizontalAxisId,
            VerticalAxisId = binding.VerticalAxisId,
            HorizontalBrakeOutputId = horizontalAxis.BrakeOutputIdentity,
            VerticalBrakeOutputId = verticalAxis.BrakeOutputIdentity,
        });
    }
}
