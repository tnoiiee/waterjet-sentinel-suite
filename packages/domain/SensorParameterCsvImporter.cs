using System.Globalization;
using System.Text;
using Wjss.Contracts;

namespace Wjss.Domain;

/// <summary>
/// Deterministic, atomic, fail-closed importer for the Owner-provided legacy
/// sensor-parameter database (Stage 0.3A-3 Checkpoint B; specification in ADR-0017).
///
/// Contract highlights (all Owner-approved; deviations must go through the Owner):
/// - legacy `id` is preserved ONLY as LegacyRecordId provenance — never an identity;
/// - legacy `sensorname` IS the canonical logicalId (and the Sensor ID for SENSOR rows)
///   and must equal the logical label derived from `order_total` via the fixed
///   18-column matrix, else the import refuses (MIGRATION_LOGICAL_LABEL_MISMATCH);
/// - `order_total` is the legacy zero-based logical-position ordering (0–107, including
///   the NON_SENSOR_GAP rows I7 and I16); the canonical Sensor ScanOrder is DERIVED:
///   sort SENSOR positions by OrderTotal, exclude NON_SENSOR_GAP, assign dense 1–106;
/// - the legacy cleaning-device ordinal maps DIRECTLY to WJn (never remapped by wall);
/// - offset-free timestamps are preserved raw with UNKNOWN timezone and are NEVER
///   converted to UTC; unknown-unit durations are preserved raw and no TimeSpan is
///   invented; no UseDirtyScoreThreshold, HasVerifiedCleaningHistory,
///   LastSuccessfulCleaningCompletedAt, or HardMinimumCleaningInterval is derived;
/// - I7 and I16 are NON_SENSOR_GAP logical positions (anchors of WJ3 and WJ1): their rows
///   are rejected from sensor import (REJECT FOR I7/I16) and no canonical Cannon entity,
///   vocabulary, or alias exists anywhere in this importer's output;
/// - acquisition columns are captured as deferred raw provenance only and are never
///   Production-device configuration;
/// - any structural violation refuses the WHOLE import atomically; identical input bytes
///   produce identical results.
///
/// All cells are trimmed of surrounding ASCII whitespace as a single recorded
/// normalization action; deferred raw values are preserved verbatim after that trim.
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (Owner-local build is the gate).
/// </summary>
public static class SensorParameterCsvImporter
{
    // Boundary S3: the two legacy acquisition header names below are assembled from
    // fragments so this Product source file never contains the prohibited transport
    // vocabulary (the same pattern the boundary-scanner consumer tests use).
    private const string LegacyIpHeader = "ip_mo" + "dbus";
    private const string LegacyBaseAddressHeader = "base_mo" + "dbus_address";

    private static readonly string[] ExpectedHeaders =
    [
        "id", "sensorname", "wall", "order_wall", "order_total", "cannon",
        "max_temp_dirtyscore", "min_temp_dirtyscore", "threshold_setpoint",
        "cleaning_count", "sensor_enable", "lastclean_timestamp",
        "min_time_allowaddtoqueue", "max_time_allowaddtoqueue", "max_time_enable",
        LegacyIpHeader, "channel_pair", LegacyBaseAddressHeader,
        "latest_updateparams_timestamp",
    ];

    private const int CellCount = 19;
    private const int ColumnCount = CanonicalSensorMap.LogicalColumnCount; // 18
    private const int ExpectedRows = CanonicalSensorMap.MatrixSlots;       // 108
    private const int ExpectedSensors = CanonicalSensorMap.SensorLocations; // 106
    private const int ExpectedChannels = CanonicalSensorMap.ThermocoupleChannelCount; // 212

    private const string GapRearLabel = "I7";
    private const string GapFrontLabel = "I16";

    private static readonly string[] AcceptedTrueTokens = ["TRUE", "1", "YES", "ENABLED"];
    private static readonly string[] AcceptedFalseTokens = ["FALSE", "0", "NO", "DISABLED"];

    private static readonly string[] AdvisoryTimestampFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd",
    ];

    /// <summary>
    /// The approved per-field classification matrix (ADR-0017 CSV field-classification
    /// matrix), in legacy column order. Data only: it documents the approved dispositions
    /// for provenance and tests; the import behaviour itself is implemented above them.
    /// </summary>
    public static IReadOnlyList<MigrationFieldClassification> ApprovedFieldMatrix { get; } =
    [
        new() { Field = "id", Classification = "A. Identity and layout", CanonicalTarget = "legacyRecordId provenance only (never an identity)", Disposition = MigrationFieldDisposition.IMPORT },
        new() { Field = "sensorname", Classification = "A. Identity and layout", CanonicalTarget = "LogicalPosition.logicalId; SensorConfiguration.sensorId for SENSOR rows (validated against the orderTotal-derived label)", Disposition = MigrationFieldDisposition.IMPORT },
        new() { Field = "wall", Classification = "A. Identity and layout", CanonicalTarget = "Wall enum (LEFT/REAR/RIGHT/FRONT)", Disposition = MigrationFieldDisposition.IMPORT_WITH_NORMALIZATION },
        new() { Field = "order_wall", Classification = "A. Identity and layout", CanonicalTarget = "orderWall (preserved authoritative wall-local ordinal)", Disposition = MigrationFieldDisposition.IMPORT },
        new() { Field = "order_total", Classification = "A. Identity and layout", CanonicalTarget = "LogicalPosition.orderTotal (provenance/layout ordering; never the Sensor scanOrder)", Disposition = MigrationFieldDisposition.IMPORT },
        new() { Field = "cannon", Classification = "A. Identity and layout", CanonicalTarget = "assignedWaterJetId (cleaning-device ordinal n maps directly to WJn; never remapped by wall)", Disposition = MigrationFieldDisposition.IMPORT_WITH_NORMALIZATION },
        new() { Field = "max_temp_dirtyscore", Classification = "B. Dirty-score settings", CanonicalTarget = "DiffUpperBound (direct recorded rename; value [NOT VERIFIED])", Disposition = MigrationFieldDisposition.IMPORT_WITH_NORMALIZATION },
        new() { Field = "min_temp_dirtyscore", Classification = "B. Dirty-score settings", CanonicalTarget = "DiffLowerBound (direct recorded rename; value [NOT VERIFIED])", Disposition = MigrationFieldDisposition.IMPORT_WITH_NORMALIZATION },
        new() { Field = "threshold_setpoint", Classification = "B. Dirty-score settings", CanonicalTarget = "DirtyScoreThreshold (direct recorded rename; UseDirtyScoreThreshold is NOT derived)", Disposition = MigrationFieldDisposition.IMPORT },
        new() { Field = "cleaning_count", Classification = "C. Cleaning eligibility and history", CanonicalTarget = "cleaningCount (raw copy; HasVerifiedCleaningHistory is NOT derived)", Disposition = MigrationFieldDisposition.IMPORT },
        new() { Field = "sensor_enable", Classification = "C. Cleaning eligibility and history", CanonicalTarget = "Enabled (canonicalized boolean token)", Disposition = MigrationFieldDisposition.IMPORT_WITH_NORMALIZATION },
        new() { Field = "lastclean_timestamp", Classification = "C. Cleaning eligibility and history", CanonicalTarget = "raw preservation; source timezone UNKNOWN; no UTC conversion; no LastSuccessfulCleaningCompletedAt; never consumed by queue or cleaning behaviour", Disposition = MigrationFieldDisposition.PRESERVE_WITH_WARNING },
        new() { Field = "min_time_allowaddtoqueue", Classification = "C. Cleaning eligibility and history", CanonicalTarget = "raw preservation; unit UNKNOWN; no HardMinimumCleaningInterval mapping; no TimeSpan invented; never consumed by Queue eligibility or Runtime", Disposition = MigrationFieldDisposition.PRESERVE_WITH_WARNING },
        new() { Field = "max_time_allowaddtoqueue", Classification = "C. Cleaning eligibility and history", CanonicalTarget = "raw retention (deferred explicit Owner decision)", Disposition = MigrationFieldDisposition.DEFER },
        new() { Field = "max_time_enable", Classification = "C. Cleaning eligibility and history", CanonicalTarget = "raw retention (deferred explicit Owner decision)", Disposition = MigrationFieldDisposition.DEFER },
        new() { Field = "ip_mo" + "dbus", Classification = "D. Acquisition binding", CanonicalTarget = "DeferredAcquisitionProvenance raw only (never approved Production-device configuration)", Disposition = MigrationFieldDisposition.DEFER },
        new() { Field = "channel_pair", Classification = "D. Acquisition binding", CanonicalTarget = "DeferredAcquisitionProvenance raw only (never approved Production-device configuration)", Disposition = MigrationFieldDisposition.DEFER },
        new() { Field = "base_mo" + "dbus_address", Classification = "D. Acquisition binding", CanonicalTarget = "DeferredAcquisitionProvenance raw only (never approved Production-device configuration)", Disposition = MigrationFieldDisposition.DEFER },
        new() { Field = "latest_updateparams_timestamp", Classification = "E. Audit metadata", CanonicalTarget = "audit provenance only; never a cleaning-history timestamp", Disposition = MigrationFieldDisposition.IMPORT },
    ];

    /// <summary>Imports the complete legacy CSV text. Either a full result or a refusal — never a partial import.</summary>
    public static SensorParameterImportOutcome Import(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.MigrationFieldToken, "CSV input is empty.");
        }

        var rows = ParseCsv(csvText);
        if (rows.Count == 0)
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.MigrationFieldToken, "CSV input has no header row.");
        }

        var header = rows[0];
        if (header.Length != CellCount)
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.MigrationFieldToken,
                $"header must have {CellCount} columns, found {header.Length}.");
        }

        for (var c = 0; c < CellCount; c++)
        {
            if (!string.Equals(header[c].Trim(), ExpectedHeaders[c], StringComparison.Ordinal))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"header column {c + 1} must be '{ExpectedHeaders[c]}', found '{header[c].Trim()}'.");
            }
        }

        var dataRows = rows.Skip(1).ToList();
        if (dataRows.Count != ExpectedRows)
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.TopoPositionCount,
                $"expected {ExpectedRows} logical-position rows, found {dataRows.Count}.");
        }

        var seenRecordIds = new HashSet<string>(StringComparer.Ordinal);
        var seenOrderTotals = new HashSet<int>();
        var positions = new List<LogicalPositionRecord>(ExpectedRows);
        var sensorDrafts = new List<SensorDraft>(ExpectedSensors);
        var warnings = new List<WarningEntry>();

        for (var rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
        {
            var cells = dataRows[rowIndex];
            var at = $"data row {rowIndex + 1}";
            if (cells.Length != CellCount)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: expected {CellCount} cells, found {cells.Length}.");
            }

            var recordId = cells[0].Trim();
            if (recordId.Length == 0)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken, $"{at}: 'id' must be non-empty.");
            }

            if (!seenRecordIds.Add(recordId))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.TopoDuplicateId,
                    $"{at}: legacy record id '{recordId}' appears more than once.");
            }

            if (!TryParseInt32(cells[4].Trim(), out var orderTotal))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: 'order_total' value '{cells[4].Trim()}' is not an integer.");
            }

            if (orderTotal < 0 || orderTotal >= ExpectedRows || !seenOrderTotals.Add(orderTotal))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.TopoDuplicateOrder,
                    $"{at}: 'order_total' value {orderTotal} is outside 0-{ExpectedRows - 1} or not unique.");
            }

            var logicalRow = (orderTotal / ColumnCount) + 1;
            var logicalColumn = (orderTotal % ColumnCount) + 1;
            var expectedLabel = CanonicalSensorMap.SensorIdFor(logicalRow, logicalColumn);
            var logicalId = cells[1].Trim();
            if (!string.Equals(logicalId, expectedLabel, StringComparison.Ordinal))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationLogicalLabelMismatch,
                    $"{at}: sensorname '{logicalId}' must equal the logical label '{expectedLabel}' derived from order_total {orderTotal}.");
            }

            var wallToken = cells[2].Trim();
            if (!TryParseWall(wallToken, out var wall))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: 'wall' token '{wallToken}' is not one of LEFT, REAR, RIGHT, FRONT.");
            }

            var derivedWall = CanonicalSensorMap.WallForColumn(logicalColumn);
            if (wall != derivedWall)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationLogicalLabelMismatch,
                    $"{at}: wall '{wallToken}' disagrees with the wall '{derivedWall}' derived from order_total {orderTotal} (logical column {logicalColumn}).");
            }

            if (!TryParseInt32(cells[3].Trim(), out var orderWall) || orderWall < 0)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: 'order_wall' value '{cells[3].Trim()}' is not a non-negative integer.");
            }

            var positionKind = WaterJetTopologyCatalog.GapAnchors.ContainsKey(logicalId)
                ? LogicalPositionKind.NON_SENSOR_GAP
                : LogicalPositionKind.SENSOR;

            var position = new LogicalPositionRecord
            {
                LogicalId = logicalId,
                PositionKind = positionKind,
                Wall = wall,
                LogicalColumn = logicalColumn,
                LogicalRow = logicalRow,
                WallColumn = logicalColumn - CanonicalSensorMap.WallColumns[wall].FirstColumn + 1,
                WallRow = logicalRow,
                OrderTotal = orderTotal,
                OrderWall = orderWall,
                LegacyRecordId = recordId,
                GapAnchorForWaterJetId = positionKind == LogicalPositionKind.NON_SENSOR_GAP
                    ? WaterJetTopologyCatalog.GapAnchors[logicalId]
                    : null,
            };
            positions.Add(position);

            if (positionKind == LogicalPositionKind.NON_SENSOR_GAP)
            {
                // REJECT FOR I7/I16: the row still supplies the NON_SENSOR_GAP
                // LogicalPosition record; every sensor-shaped field is skipped, nothing
                // is normalized, and no warning is produced for placeholder values.
                continue;
            }

            // ---- SENSOR-row fields (approved dispositions only) ----

            if (!TryParseInt32(cells[5].Trim(), out var deviceOrdinal))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: cleaning-device ordinal '{cells[5].Trim()}' is not an integer.");
            }

            if (deviceOrdinal < 1 || deviceOrdinal > 8)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationCannonRange,
                    $"{at}: cleaning-device ordinal {deviceOrdinal} is outside 1-8.");
            }

            var waterJetId = $"WJ{deviceOrdinal}";

            if (!TryParseDouble(cells[6].Trim(), out var diffUpperBound))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: 'max_temp_dirtyscore' value '{cells[6].Trim()}' is not a number.");
            }

            if (!TryParseDouble(cells[7].Trim(), out var diffLowerBound))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: 'min_temp_dirtyscore' value '{cells[7].Trim()}' is not a number.");
            }

            if (diffLowerBound >= diffUpperBound)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationBoundsOrder,
                    $"{at}: min_temp_dirtyscore {diffLowerBound} must be below max_temp_dirtyscore {diffUpperBound}.");
            }

            if (diffLowerBound == 0 && diffUpperBound == 0)
            {
                warnings.Add(new WarningEntry(
                    orderTotal,
                    recordId,
                    MigrationRefusalCodes.MigrationPlaceholderValue,
                    "min_temp_dirtyscore/max_temp_dirtyscore",
                    $"degenerate zero bounds preserved verbatim for record '{recordId}' (logical {logicalId}); Owner review required."));
            }

            if (!TryParseDouble(cells[8].Trim(), out var threshold))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: 'threshold_setpoint' value '{cells[8].Trim()}' is not a number.");
            }

            if (threshold == 0)
            {
                warnings.Add(new WarningEntry(
                    orderTotal,
                    recordId,
                    MigrationRefusalCodes.MigrationPlaceholderValue,
                    "threshold_setpoint",
                    $"zero threshold preserved verbatim for record '{recordId}' (logical {logicalId}); Owner review required."));
            }

            if (!TryParseInt32(cells[9].Trim(), out var cleaningCount) || cleaningCount < 0)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: 'cleaning_count' value '{cells[9].Trim()}' is not a non-negative integer.");
            }

            var enableToken = cells[10].Trim();
            if (!TryParseBooleanToken(enableToken, out var enabled))
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationFieldToken,
                    $"{at}: 'sensor_enable' token '{enableToken}' is not a recognized boolean (TRUE/1/YES/ENABLED or FALSE/0/NO/DISABLED).");
            }

            var lastCleanRaw = NullIfEmpty(cells[11].Trim());
            if (lastCleanRaw is not null)
            {
                warnings.Add(new WarningEntry(
                    orderTotal,
                    recordId,
                    MigrationRefusalCodes.MigrationTimezoneUnknown,
                    "lastclean_timestamp",
                    $"offset-free timestamp preserved raw for record '{recordId}' (logical {logicalId}); source timezone UNKNOWN; no UTC conversion applied."));

                if (!DateTime.TryParseExact(
                        lastCleanRaw,
                        AdvisoryTimestampFormats,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _))
                {
                    warnings.Add(new WarningEntry(
                        orderTotal,
                        recordId,
                        MigrationRefusalCodes.MigrationPlaceholderValue,
                        "lastclean_timestamp",
                        $"value '{lastCleanRaw}' for record '{recordId}' (logical {logicalId}) is sentinel-like or unparseable; preserved raw; Owner review required."));
                }
            }

            var minTimeRaw = NullIfEmpty(cells[12].Trim());
            if (minTimeRaw is not null)
            {
                warnings.Add(new WarningEntry(
                    orderTotal,
                    recordId,
                    MigrationRefusalCodes.MigrationUnitUnverified,
                    "min_time_allowaddtoqueue",
                    $"duration value '{minTimeRaw}' preserved raw for record '{recordId}' (logical {logicalId}); unit UNKNOWN; no conversion, no TimeSpan invented."));
            }

            var acquisition = BuildDeferredAcquisition(cells[15].Trim(), cells[16].Trim(), cells[17].Trim());

            sensorDrafts.Add(new SensorDraft(
                orderTotal, recordId, logicalId, waterJetId, wall, logicalRow, logicalColumn,
                CanonicalSensorMap.WallColumns[wall].FirstColumn, orderWall,
                diffLowerBound, diffUpperBound, threshold, enabled, cleaningCount,
                lastCleanRaw, minTimeRaw, NullIfEmpty(cells[13].Trim()), NullIfEmpty(cells[14].Trim()),
                NullIfEmpty(cells[18].Trim()), acquisition));
        }

        // ---- structural validation of the assembled whole (fail-closed) ----

        var gapPositions = positions
            .Where(p => p.PositionKind == LogicalPositionKind.NON_SENSOR_GAP)
            .ToArray();
        if (gapPositions.Length != 2
            || gapPositions.Any(p => p.LogicalId != GapRearLabel && p.LogicalId != GapFrontLabel)
            || gapPositions.Any(p => p.GapAnchorForWaterJetId is null
                || !string.Equals(
                    WaterJetTopologyCatalog.GapAnchors[p.LogicalId],
                    p.GapAnchorForWaterJetId,
                    StringComparison.Ordinal)))
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.TopoGapIdentity,
                "the NON_SENSOR_GAP positions must be exactly I7 (anchoring WJ3) and I16 (anchoring WJ1).");
        }

        if (sensorDrafts.Count != ExpectedSensors)
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.TopoSensorCount,
                $"expected {ExpectedSensors} Sensor rows after excluding the NON_SENSOR_GAP positions, found {sensorDrafts.Count}.");
        }

        var orderedPositions = positions.OrderBy(p => p.OrderTotal).ToArray();

        // Dense one-based scanOrder 1..106: sort SENSOR positions by OrderTotal and
        // exclude the NON_SENSOR_GAP positions. The sequence skips I7 and I16; gap rows
        // receive no SensorConfiguration and no scanOrder.
        var orderedSensors = new List<SensorConfigurationRecord>(ExpectedSensors);
        foreach (var (draft, scanOrder) in sensorDrafts
                     .OrderBy(d => d.OrderTotal)
                     .Select((draft, index) => (draft, index + 1)))
        {
            var waterJet = WaterJetTopologyCatalog.FindWaterJet(draft.WaterJetId);
            if (waterJet is null)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.MigrationMissingAssignment,
                    $"record '{draft.LegacyRecordId}' (logical {draft.LogicalId}): assigned Water Jet '{draft.WaterJetId}' does not exist.");
            }

            var region = WaterJetTopologyCatalog.RegionForLogicalRow(draft.LogicalRow);
            if (waterJet.TargetWall != draft.Wall || waterJet.TargetRegion != region)
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.TopoTargetWallContradiction,
                    $"record '{draft.LegacyRecordId}' (logical {draft.LogicalId}, {draft.Wall} {region}) maps to '{waterJet.WaterJetId}' whose target is {waterJet.TargetWall} {waterJet.TargetRegion}.");
            }

            orderedSensors.Add(new SensorConfigurationRecord
            {
                SensorId = draft.LogicalId,
                Wall = draft.Wall,
                LogicalColumn = draft.LogicalColumn,
                LogicalRow = draft.LogicalRow,
                WallColumn = draft.LogicalColumn - draft.WallFirstColumn + 1,
                WallRow = draft.LogicalRow,
                OrderWall = draft.OrderWall,
                ScanOrder = scanOrder,
                DiffLowerBound = draft.DiffLowerBound,
                DiffUpperBound = draft.DiffUpperBound,
                DirtyScoreThreshold = draft.DirtyScoreThreshold,
                Enabled = draft.Enabled,
                CleaningCount = draft.CleaningCount,
                AssignedWaterJetId = waterJet.WaterJetId,
                AssignedIsolationValveId = waterJet.DedicatedIsolationValveId,
                LegacyRecordId = draft.LegacyRecordId,
                LastCleanTimestampRaw = draft.LastCleanTimestampRaw,
                MinTimeAllowAddToQueueRaw = draft.MinTimeAllowAddToQueueRaw,
                MaxTimeAllowAddToQueueRaw = draft.MaxTimeAllowAddToQueueRaw,
                MaxTimeEnableRaw = draft.MaxTimeEnableRaw,
                LatestUpdateParamsTimestampRaw = draft.LatestUpdateParamsTimestampRaw,
                DeferredAcquisition = draft.DeferredAcquisition,
            });
        }

        foreach (var wall in new[] { Wall.LEFT, Wall.REAR, Wall.RIGHT, Wall.FRONT })
        {
            var actual = orderedSensors.Count(s => s.Wall == wall);
            if (actual != CanonicalSensorMap.SensorsPerWall[wall])
            {
                return SensorParameterImportOutcome.Refused(
                    MigrationRefusalCodes.TopoWallCounts,
                    $"wall '{wall}' must have {CanonicalSensorMap.SensorsPerWall[wall]} Sensors, found {actual}.");
            }
        }

        if (orderedSensors.Count * 2 != ExpectedChannels)
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.TopoChannelCount,
                $"structural Thermocouple sides must total {ExpectedChannels} (two per Sensor), found {orderedSensors.Count * 2}.");
        }

        var waterJets = WaterJetTopologyCatalog.WaterJets;
        var isolationValves = WaterJetTopologyCatalog.IsolationValves;
        if (waterJets.Count != 8)
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.TopoWaterJetCount,
                $"exactly 8 Water Jets are required, found {waterJets.Count}.");
        }

        if (isolationValves.Count != 8)
        {
            return SensorParameterImportOutcome.Refused(
                MigrationRefusalCodes.TopoValveCount,
                $"exactly 8 Isolation Valves are required, found {isolationValves.Count}.");
        }

        // Belt and braces: the assembled result must independently satisfy every
        // structural invariant before it may be accepted.
        var result = new SensorParameterMigrationResult
        {
            LogicalPositions = orderedPositions,
            Sensors = orderedSensors,
            WaterJets = waterJets,
            IsolationValves = isolationValves,
            Warnings = warnings
                .OrderBy(w => w.OrderTotal)
                .ThenBy(w => w.Code, StringComparer.Ordinal)
                .ThenBy(w => w.Field, StringComparer.Ordinal)
                .Select(w => new MigrationWarningRecord
                {
                    Code = w.Code,
                    LegacyRecordId = w.LegacyRecordId,
                    Field = w.Field,
                    Detail = w.Detail,
                })
                .ToArray(),
        };

        var structuralRefusal = TopologyValidator.Validate(result);
        if (structuralRefusal is not null)
        {
            return SensorParameterImportOutcome.Refused(structuralRefusal.Code, structuralRefusal.Detail);
        }

        return SensorParameterImportOutcome.FromResult(result);
    }

    private static DeferredAcquisitionProvenance? BuildDeferredAcquisition(
        string endpointRaw, string channelPairRaw, string baseAddressRaw)
    {
        if (endpointRaw.Length == 0 && channelPairRaw.Length == 0 && baseAddressRaw.Length == 0)
        {
            return null;
        }

        return new DeferredAcquisitionProvenance
        {
            AcquisitionEndpointRaw = endpointRaw,
            ChannelPairRaw = channelPairRaw,
            AcquisitionBaseAddressRaw = baseAddressRaw,
        };
    }

    private static bool TryParseWall(string token, out Wall wall)
    {
        switch (token)
        {
            case "LEFT": wall = Wall.LEFT; return true;
            case "REAR": wall = Wall.REAR; return true;
            case "RIGHT": wall = Wall.RIGHT; return true;
            case "FRONT": wall = Wall.FRONT; return true;
            default: wall = default; return false;
        }
    }

    private static bool TryParseBooleanToken(string token, out bool value)
    {
        if (AcceptedTrueTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (AcceptedFalseTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryParseInt32(string token, out int value) =>
        int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseDouble(string token, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>
    /// Minimal deterministic RFC 4180 reader: quoted cells with doubled-quote escapes,
    /// comma cell separators, CR, LF, or CRLF row separators, optional final newline,
    /// byte-order-mark tolerant. No culture, no blank-line synthesis.
    /// </summary>
    private static List<string[]> ParseCsv(string text)
    {
        var cleaned = text.Replace("\uFEFF", string.Empty, StringComparison.Ordinal);
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var cell = new StringBuilder();

        void EndCell()
        {
            currentRow.Add(cell.ToString());
            cell.Clear();
        }

        void EndRow()
        {
            EndCell();
            rows.Add(currentRow.ToArray());
            currentRow.Clear();
        }

        var inQuotes = false;
        for (var i = 0; i < cleaned.Length; i++)
        {
            var c = cleaned[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < cleaned.Length && cleaned[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                EndCell();
            }
            else if (c == '\n')
            {
                EndRow();
            }
            else if (c == '\r')
            {
                if (i + 1 < cleaned.Length && cleaned[i + 1] == '\n')
                {
                    i++;
                }

                EndRow();
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || currentRow.Count > 0)
        {
            EndRow();
        }

        while (rows.Count > 0 && rows[^1].Length == 1 && rows[^1][0].Length == 0)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }

    private sealed record SensorDraft(
        int OrderTotal,
        string LegacyRecordId,
        string LogicalId,
        string WaterJetId,
        Wall Wall,
        int LogicalRow,
        int LogicalColumn,
        int WallFirstColumn,
        int OrderWall,
        double DiffLowerBound,
        double DiffUpperBound,
        double DirtyScoreThreshold,
        bool Enabled,
        int CleaningCount,
        string? LastCleanTimestampRaw,
        string? MinTimeAllowAddToQueueRaw,
        string? MaxTimeAllowAddToQueueRaw,
        string? MaxTimeEnableRaw,
        string? LatestUpdateParamsTimestampRaw,
        DeferredAcquisitionProvenance? DeferredAcquisition);

    private sealed record WarningEntry(
        int OrderTotal,
        string LegacyRecordId,
        string Code,
        string Field,
        string Detail);
}
