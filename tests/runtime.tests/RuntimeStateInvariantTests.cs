using Wjss.Adapters.Simulator;
using Wjss.Contracts;
using Wjss.Domain;
using Xunit;

namespace Wjss.Runtime.Core.Tests;

/// <summary>
/// Protected-baseline invariants (Owner critical topology correction, Stage 0.4C-K3):
/// 108 logical slots, 108 Sensor locations (I7 and I16 are normal Product Sensors; no
/// NON_SENSOR_GAP position exists), 216 assigned Thermocouple channels, wall counts
/// 24 / 30 / 24 / 30, one Active Job at most, bounded queue and bounded trend.
/// Each refusal is asserted through its machine reason code, not through prose.
/// </summary>
public sealed class RuntimeStateInvariantTests
{
    /// <summary>
    /// The exact member names of the TC acquisition records, held in Ordinal order so each assertion
    /// compares like-for-like with the Ordinal-sorted reflection projection of the same record. The
    /// projection is never re-ordered to fit the expectation: a missing, extra or duplicated member
    /// still fails the comparison deterministically.
    /// </summary>
    private static readonly string[] ExpectedTcAcquisitionStatePropertyNames =
    [
        "AcquiredAtUtc",
        "AcquisitionState",
        "CabinetId",
        "CopiedWordCount",
        "DecodeStatus",
        "DiagnosticState",
        "MappingStatus",
        "NodeId",
        "NotVerified",
        "Physical",
        "PhysicalEvidence",
        "Provenance",
        "Quality",
        "Quarantined",
        "RawWords",
    ];

    private static readonly string[] ExpectedTcPhysicalReadOnlyEvidencePropertyNames =
    [
        "CommissioningState",
        "EndpointIdentity",
        "MappingEvidenceState",
        "ReadProfile",
    ];

    private static readonly string[] ExpectedTcReadProfileIdentityPropertyNames =
    [
        "FunctionCode",
        "Port",
        "Quantity",
        "RegisterArea",
        "StartAddress",
        "UnitIdentifier",
    ];

    public enum Tamper
    {
        Profile,
        SensorCount,
        SensorOrder,
        SensorIdentity,
        SensorChannels,
        UnknownPositionKind,
        FormerGapShape,
        WallSummary,
        ActiveTarget,
        QueueShape,
        TrendBounds,
        AlarmCounters,
        CommunicationDevices,
    }

    [Fact]
    public void Composed_State_Satisfies_The_Protected_Counts()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        Assert.True(
            RuntimeStateInvariants.TryValidate(state, out var reasonCode, out var detail),
            $"composed state must be valid: [{reasonCode}] {detail}");

        var slotCount = state.WallMap.Count;
        var uniqueSlotCount = state.WallMap.Select(slot => slot.SlotId).Distinct(StringComparer.Ordinal).Count();
        var sensorSlotCount = state.WallMap.Count(slot => slot.PositionKind == LogicalPositionKind.SENSOR);
        var sensorProjectionCount = state.Sensors.Count;
        var channels = state.Sensors
            .SelectMany(sensor => new[] { sensor.TcFrontChannel, sensor.TcRearChannel })
            .ToArray();
        var channelCount = channels.Length;
        var uniqueChannelCount = channels.Distinct(StringComparer.Ordinal).Count();

        Assert.Equal(CanonicalSensorMap.MatrixSlots, slotCount);
        Assert.Equal(CanonicalSensorMap.MatrixSlots, uniqueSlotCount);
        Assert.Equal(CanonicalSensorMap.SensorLocations, sensorSlotCount);
        Assert.Equal(CanonicalSensorMap.SensorLocations, sensorProjectionCount);
        Assert.Equal(CanonicalSensorMap.ThermocoupleChannelCount, channelCount);
        Assert.Equal(CanonicalSensorMap.ThermocoupleChannelCount, uniqueChannelCount);
    }

    [Fact]
    public void I7_And_I16_Are_Ordinary_Sensor_Slots_And_No_Gap_Position_Exists()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        Assert.All(state.WallMap, slot => Assert.Equal(LogicalPositionKind.SENSOR, slot.PositionKind));
        Assert.All(state.WallMap, slot => Assert.False(string.IsNullOrWhiteSpace(slot.SensorId)));

        var i7Slot = state.WallMap.Single(slot => slot.LogicalRow == 5 && slot.LogicalColumn == 7);
        Assert.Equal("I7", i7Slot.SensorId);
        Assert.Equal(Wall.REAR, i7Slot.Wall);
        var i16Slot = state.WallMap.Single(slot => slot.LogicalRow == 5 && slot.LogicalColumn == 16);
        Assert.Equal("I16", i16Slot.SensorId);
        Assert.Equal(Wall.FRONT, i16Slot.Wall);

        var i7 = Assert.Single(state.Sensors, sensor => sensor.SensorId == "I7");
        var i16 = Assert.Single(state.Sensors, sensor => sensor.SensorId == "I16");
        Assert.Equal(79, state.Sensors.ToList().IndexOf(i7) + 1);
        Assert.Equal(88, state.Sensors.ToList().IndexOf(i16) + 1);
        Assert.False(string.IsNullOrWhiteSpace(i7.TcFrontChannel));
        Assert.False(string.IsNullOrWhiteSpace(i7.TcRearChannel));
        Assert.False(string.IsNullOrWhiteSpace(i16.TcFrontChannel));
        Assert.False(string.IsNullOrWhiteSpace(i16.TcRearChannel));
        Assert.Equal("WJ1", i7.AssignedWaterJetId);
        Assert.Equal("WJ3", i16.AssignedWaterJetId);

        Assert.Equal(24, state.Sensors.Count(sensor => sensor.Wall == Wall.LEFT));
        Assert.Equal(30, state.Sensors.Count(sensor => sensor.Wall == Wall.REAR));
        Assert.Equal(24, state.Sensors.Count(sensor => sensor.Wall == Wall.RIGHT));
        Assert.Equal(30, state.Sensors.Count(sensor => sensor.Wall == Wall.FRONT));
    }

    [Fact]
    public void Wall_Summaries_Are_Recalculated_From_The_Sensors()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        Assert.Equal(RuntimeWallSummaries.Recalculate(state.Sensors), state.Walls);
        Assert.Equal(
            new[] { Wall.LEFT, Wall.REAR, Wall.RIGHT, Wall.FRONT },
            state.Walls.Select(summary => summary.Wall));

        var totals = state.Walls.ToDictionary(summary => summary.Wall, summary => summary.Total);
        Assert.Equal(24, totals[Wall.LEFT]);
        Assert.Equal(30, totals[Wall.REAR]);
        Assert.Equal(24, totals[Wall.RIGHT]);
        Assert.Equal(30, totals[Wall.FRONT]);
        Assert.Equal(CanonicalSensorMap.SensorLocations, totals.Values.Sum());

        foreach (var summary in state.Walls)
        {
            Assert.Equal(summary.Total, summary.Dirty + summary.Cleaner + summary.NotClassified);
        }
    }

    [Theory]
    [InlineData(Tamper.Profile, RuntimeRefusalCodes.ProfileNotSimulator)]
    [InlineData(Tamper.SensorCount, RuntimeRefusalCodes.SensorCount)]
    [InlineData(Tamper.SensorOrder, RuntimeRefusalCodes.SensorOrder)]
    [InlineData(Tamper.SensorIdentity, RuntimeRefusalCodes.SensorIdentity)]
    [InlineData(Tamper.SensorChannels, RuntimeRefusalCodes.SensorChannels)]
    [InlineData(Tamper.UnknownPositionKind, RuntimeRefusalCodes.WallMapNonSensorPosition)]
    [InlineData(Tamper.FormerGapShape, RuntimeRefusalCodes.WallMapSensorBinding)]
    [InlineData(Tamper.WallSummary, RuntimeRefusalCodes.WallSummaryMismatch)]
    [InlineData(Tamper.ActiveTarget, RuntimeRefusalCodes.SensorActiveTarget)]
    [InlineData(Tamper.QueueShape, RuntimeRefusalCodes.QueueShape)]
    [InlineData(Tamper.TrendBounds, RuntimeRefusalCodes.TrendBounds)]
    [InlineData(Tamper.AlarmCounters, RuntimeRefusalCodes.AlarmCounters)]
    [InlineData(Tamper.CommunicationDevices, RuntimeRefusalCodes.DeviceHealthSet)]
    public void Tampered_State_Is_Refused_With_Its_Machine_Code(Tamper tamper, string expectedCode)
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var tampered = Apply(state, tamper);

        Assert.False(RuntimeStateInvariants.TryValidate(tampered, out var reasonCode, out _));
        Assert.Equal(expectedCode, reasonCode);
    }

    [Fact]
    public void RequireValid_Throws_With_The_Machine_Code()
    {
        var state = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var tampered = Apply(state, Tamper.Profile);

        var thrown = Assert.Throws<InvalidOperationException>(() => RuntimeStateInvariants.RequireValid(tampered));

        Assert.Contains(RuntimeRefusalCodes.ProfileNotSimulator, thrown.Message, StringComparison.Ordinal);
    }

    private static TcAcquisitionState ValidDeterministicTc() => new()
    {
        NodeId = DeterministicTcVocabulary.NodeId,
        CabinetId = DeterministicTcVocabulary.CabinetId,
        Provenance = DeterministicTcVocabulary.Provenance,
        Physical = false,
        Quarantined = true,
        AcquisitionState = DeterministicTcVocabulary.Complete,
        AcquiredAtUtc = RuntimeTestFixture.Instant,
        CopiedWordCount = DeterministicTcVocabulary.WordCount,
        RawWords = Enumerable.Range(100, 40).Select(n => (ushort)n).ToArray(),
        Quality = DeterministicTcVocabulary.Quality,
        DiagnosticState = DeterministicTcVocabulary.NoDiagnostic,
        MappingStatus = DeterministicTcVocabulary.MappingNotVerified,
        DecodeStatus = DeterministicTcVocabulary.DecodeNotVerified,
        NotVerified = DeterministicTcVocabulary.NotVerified.ToArray(),
    };

    [Fact]
    public void One_Node_Tc_State_Requires_Exact_Deterministic_Words_And_No_Physical_Claim()
    {
        var initial = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        Assert.Null(initial.TcAcquisition);
        var valid = ValidDeterministicTc();
        Assert.True(RuntimeStateInvariants.TryValidate(initial with { TcAcquisition = valid }, out var reason, out var detail),
            $"[{reason}] {detail}");

        var shorter = valid.RawWords.Take(39).ToArray();
        var changed = valid.RawWords.ToArray();
        changed[23] = 999;

        // Each case carries the machine reason code it must produce. Every deterministic-shaped
        // tamper below keeps Physical=false and is therefore refused by the deterministic validator
        // (STATE_INVALID). The single exception declares Physical=true: that record is no longer
        // deterministic-shaped, so the profile invariant refuses it first - a SIMULATOR revision must
        // never carry a physical acquisition record, and the two variants are never blended.
        var invalidCases = new (string Label, TcAcquisitionState State, string Code)[]
        {
            ("foreign node", valid with { NodeId = "TC-NODE-02" }, RuntimeRefusalCodes.StateInvalid),
            ("foreign cabinet", valid with { CabinetId = "CAB2" }, RuntimeRefusalCodes.StateInvalid),
            ("physical provenance", valid with { Provenance = "PHYSICAL" }, RuntimeRefusalCodes.StateInvalid),
            ("physical claim", valid with { Physical = true }, RuntimeRefusalCodes.PhysicalTcProfileMismatch),
            ("not quarantined", valid with { Quarantined = false }, RuntimeRefusalCodes.StateInvalid),
            ("pending acquisition", valid with { AcquisitionState = "PENDING" }, RuntimeRefusalCodes.StateInvalid),
            ("good quality", valid with { Quality = "GOOD" }, RuntimeRefusalCodes.StateInvalid),
            ("unverified diagnostic", valid with { DiagnosticState = "UNVERIFIED" }, RuntimeRefusalCodes.StateInvalid),
            ("verified mapping", valid with { MappingStatus = "VERIFIED" }, RuntimeRefusalCodes.StateInvalid),
            ("verified decode", valid with { DecodeStatus = "VERIFIED" }, RuntimeRefusalCodes.StateInvalid),
            ("default timestamp", valid with { AcquiredAtUtc = default }, RuntimeRefusalCodes.StateInvalid),
            ("non-UTC timestamp", valid with { AcquiredAtUtc = RuntimeTestFixture.Instant.ToOffset(TimeSpan.FromHours(7)) }, RuntimeRefusalCodes.StateInvalid),
            ("short count", valid with { CopiedWordCount = 39 }, RuntimeRefusalCodes.StateInvalid),
            ("short words", valid with { RawWords = shorter }, RuntimeRefusalCodes.StateInvalid),
            ("changed word", valid with { RawWords = changed }, RuntimeRefusalCodes.StateInvalid),
            ("short not-verified", valid with { NotVerified = valid.NotVerified.Take(4).ToArray() }, RuntimeRefusalCodes.StateInvalid),
            ("reversed not-verified", valid with { NotVerified = valid.NotVerified.Reverse().ToArray() }, RuntimeRefusalCodes.StateInvalid),
        };

        foreach (var (label, invalid, expectedCode) in invalidCases)
        {
            var accepted = RuntimeStateInvariants.TryValidate(initial with { TcAcquisition = invalid },
                out var code, out var detail);
            Assert.False(accepted, label);
            Assert.Equal(expectedCode, code);
            Assert.NotEqual(string.Empty, detail);
        }
    }

    [Fact]
    public void Non_Simulator_Profiles_Are_Refused_At_Composition()
    {
        var config = RuntimeTestFixture.Config();
        var wallMap = SyntheticSensorMap.BuildWallMap();
        var sensors = SyntheticSensorMap.BuildInitialSensors(config, SyntheticSeed.DefaultSeed, RuntimeTestFixture.Instant);

        foreach (var profile in new[] { DeviceProfile.TEST_HARDWARE, DeviceProfile.PRODUCTION })
        {
            var refused = Assert.Throws<InvalidOperationException>(() =>
            {
                _ = RuntimeStateComposer.ComposeInitial(
                    profile,
                    config,
                    wallMap,
                    sensors,
                    WaterJetTopologyCatalog.WaterJets,
                    WaterJetTopologyCatalog.IsolationValves,
                    RuntimeTestFixture.Instant);
            });

            Assert.Contains(ProfileStartPolicy.RefusalCode, refused.Message, StringComparison.Ordinal);
        }
    }

    // --- One-Node physical read-only variant (Owner decision R1) ----------------------------------
    //
    // The physical record is a SEPARATE variant carried under its own profile. These tests prove the
    // three properties that matter: the variant is admissible only with its own profile, the two
    // variants can never be blended in either direction, and a counterfeit physical record — one that
    // claims an accepted, commissioned, verified or differently-profiled read — is refused.

    /// <summary>The authorized logical read-profile identity, built here without any app assembly.</summary>
    private static TcReadProfileIdentity AuthorizedProfile() => new()
    {
        FunctionCode = PhysicalTcVocabulary.FunctionCodeHoldingRegisters,
        RegisterArea = PhysicalTcVocabulary.RegisterAreaHoldingRegisters,
        Port = PhysicalTcVocabulary.RegisterPort,
        UnitIdentifier = PhysicalTcVocabulary.UnitIdentifier,
        StartAddress = PhysicalTcVocabulary.StartAddress,
        Quantity = PhysicalTcVocabulary.WordCount,
    };

    /// <summary>A complete, honest physical read-only record. Every field is the declared value.</summary>
    private static TcAcquisitionState PhysicalRecord() => new()
    {
        NodeId = PhysicalTcVocabulary.NodeId,
        CabinetId = PhysicalTcVocabulary.CabinetId,
        Provenance = PhysicalTcVocabulary.Provenance,
        Physical = true,
        Quarantined = true,
        AcquisitionState = PhysicalTcVocabulary.Complete,
        AcquiredAtUtc = RuntimeTestFixture.Instant,
        CopiedWordCount = PhysicalTcVocabulary.WordCount,
        // Raw device evidence, including both legacy sentinel candidates. Nothing is decoded here.
        RawWords = Array.AsReadOnly(BuildPhysicalWords()),
        Quality = PhysicalTcVocabulary.Quality,
        DiagnosticState = PhysicalTcVocabulary.NoDiagnostic,
        MappingStatus = PhysicalTcVocabulary.MappingStatus,
        DecodeStatus = PhysicalTcVocabulary.DecodeStatus,
        NotVerified = Array.AsReadOnly(PhysicalTcVocabulary.NotVerified.ToArray()),
        PhysicalEvidence = new TcPhysicalReadOnlyEvidence
        {
            EndpointIdentity = PhysicalTcVocabulary.EndpointConfigured,
            ReadProfile = AuthorizedProfile(),
            MappingEvidenceState = PhysicalTcVocabulary.MappingEvidenceDrawingInternalConflict,
            CommissioningState = PhysicalTcVocabulary.CommissioningState,
        },
    };

    private static ushort[] BuildPhysicalWords()
    {
        var words = new ushort[PhysicalTcVocabulary.WordCount];
        words[0] = 0x7FFF;   // legacy wire-break candidate
        words[1] = 0xF830;   // legacy reverse-polarity candidate
        for (var index = 2; index < words.Length; index++) words[index] = (ushort)index;
        return words;
    }

    private static TcAcquisitionState DeterministicRecord() => new()
    {
        NodeId = DeterministicTcVocabulary.NodeId,
        CabinetId = DeterministicTcVocabulary.CabinetId,
        Provenance = DeterministicTcVocabulary.Provenance,
        Physical = false,
        Quarantined = true,
        AcquisitionState = DeterministicTcVocabulary.Complete,
        AcquiredAtUtc = RuntimeTestFixture.Instant,
        CopiedWordCount = DeterministicTcVocabulary.WordCount,
        RawWords = Array.AsReadOnly(Enumerable.Range(0, DeterministicTcVocabulary.WordCount)
            .Select(DeterministicTcVocabulary.WordAt).ToArray()),
        Quality = DeterministicTcVocabulary.Quality,
        DiagnosticState = DeterministicTcVocabulary.NoDiagnostic,
        MappingStatus = DeterministicTcVocabulary.MappingNotVerified,
        DecodeStatus = DeterministicTcVocabulary.DecodeNotVerified,
        NotVerified = Array.AsReadOnly(DeterministicTcVocabulary.NotVerified.ToArray()),
    };

    private static RuntimeState WithPhysical(RuntimeState state) => state with
    {
        DeviceProfile = DeviceProfile.TEST_HARDWARE_READ_ONLY,
        TcAcquisition = PhysicalRecord(),
    };

    [Fact]
    public void Physical_Record_Is_Admissible_Only_Under_Its_Own_Read_Only_Profile()
    {
        var simulator = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);

        // The honest combination: the read-only profile carrying its quarantined physical record.
        Assert.True(
            RuntimeStateInvariants.TryValidate(WithPhysical(simulator), out var code, out var detail),
            $"[{code}] {detail}");

        // Between composition and the single publication the record is legitimately absent; the
        // profile is still valid, and no record is ever invented for it.
        var awaiting = simulator with { DeviceProfile = DeviceProfile.TEST_HARDWARE_READ_ONLY };
        Assert.True(RuntimeStateInvariants.TryValidate(awaiting, out code, out detail), $"[{code}] {detail}");

        // Neither profile may carry the other variant, and the standing refusals are unchanged.
        Assert.False(RuntimeStateInvariants.TryValidate(
            simulator with { TcAcquisition = PhysicalRecord() }, out code, out _));
        Assert.Equal(RuntimeRefusalCodes.PhysicalTcProfileMismatch, code);

        Assert.False(RuntimeStateInvariants.TryValidate(
            WithPhysical(simulator) with { TcAcquisition = DeterministicRecord() }, out code, out _));
        Assert.Equal(RuntimeRefusalCodes.PhysicalTcProfileMismatch, code);

        foreach (var profile in new[] { DeviceProfile.TEST_HARDWARE, DeviceProfile.PRODUCTION })
        {
            Assert.False(RuntimeStateInvariants.TryValidate(
                WithPhysical(simulator) with { DeviceProfile = profile }, out code, out _));
            Assert.Equal(RuntimeRefusalCodes.ProfileNotSimulator, code);

            Assert.False(RuntimeStateInvariants.TryValidate(
                simulator with { DeviceProfile = profile }, out code, out _));
            Assert.Equal(RuntimeRefusalCodes.ProfileNotSimulator, code);
        }
    }

    [Fact]
    public void A_Counterfeit_Physical_Record_Is_Refused_Whole()
    {
        var simulator = RuntimeTestFixture.ComposeInitial(SyntheticSeed.DefaultSeed);
        var honest = PhysicalRecord();

        // Each counterfeit claims something the read-only slice can never establish: acceptance,
        // commissioning, a verified decode, a different read profile or a wrong identity.
        static TcAcquisitionState WithEvidence(
            TcAcquisitionState state, Func<TcPhysicalReadOnlyEvidence, TcPhysicalReadOnlyEvidence> mutate) =>
            state with { PhysicalEvidence = mutate(state.PhysicalEvidence!) };

        // Each counterfeit carries the machine reason code it must produce. The variant-blending
        // refusal is produced by the profile invariant, which is checked first; every other
        // counterfeit is refused by the physical record validator itself.
        var counterfeits = new (string Label, TcAcquisitionState State, string Code)[]
        {
            ("not quarantined", honest with { Quarantined = false }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("not physical", honest with { Physical = false }, RuntimeRefusalCodes.PhysicalTcProfileMismatch),
            ("accepted", honest with { AcquisitionState = "ACCEPTED" }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("verified quality", honest with { Quality = "VERIFIED" }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("simulated quality", honest with { Quality = DeterministicTcVocabulary.Quality }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("decoded", honest with { DecodeStatus = "DECODED" }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("mapping verified", honest with { MappingStatus = "MAPPING_VERIFIED" }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("deterministic provenance", honest with { Provenance = DeterministicTcVocabulary.Provenance }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("foreign node", honest with { NodeId = "TC-NODE-02" }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("foreign cabinet", honest with { CabinetId = "CAB2" }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("short read", honest with { CopiedWordCount = PhysicalTcVocabulary.WordCount - 1 }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("short words", honest with { RawWords = Array.AsReadOnly(BuildPhysicalWords()[..^1]) }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("default timestamp", honest with { AcquiredAtUtc = default }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("unspecified timestamp", honest with { AcquiredAtUtc = DateTimeOffset.MinValue }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("dropped claim", honest with
            {
                NotVerified = Array.AsReadOnly(PhysicalTcVocabulary.NotVerified.Take(PhysicalTcVocabulary.NotVerified.Count - 1).ToArray()),
            }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("reordered claims", honest with
            {
                NotVerified = Array.AsReadOnly(PhysicalTcVocabulary.NotVerified.Reverse().ToArray()),
            }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("local-time instant", honest with
            {
                AcquiredAtUtc = new DateTimeOffset(2026, 10, 7, 7, 0, 0, TimeSpan.FromHours(7)),
            }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("missing evidence", honest with { PhysicalEvidence = null }, RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("commissioned", WithEvidence(honest, e => e with { CommissioningState = "COMMISSIONED" }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("endpoint stated as absent", WithEvidence(honest, e => e with { EndpointIdentity = PhysicalTcVocabulary.EndpointNotConfigured }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("endpoint stated as an address", WithEvidence(honest, e => e with { EndpointIdentity = "CONFIGURED" }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("unknown mapping evidence", WithEvidence(honest, e => e with { MappingEvidenceState = "PHYSICAL_PARITY_CONFIRMED" }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("write function code", WithEvidence(honest, e => e with { ReadProfile = e.ReadProfile with { FunctionCode = "FC16" } }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("input registers", WithEvidence(honest, e => e with { ReadProfile = e.ReadProfile with { RegisterArea = "INPUT_REGISTERS" } }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("wrong port", WithEvidence(honest, e => e with { ReadProfile = e.ReadProfile with { Port = 5020 } }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("wrong unit", WithEvidence(honest, e => e with { ReadProfile = e.ReadProfile with { UnitIdentifier = 2 } }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("wrong start", WithEvidence(honest, e => e with { ReadProfile = e.ReadProfile with { StartAddress = 1 } }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
            ("wrong quantity", WithEvidence(honest, e => e with { ReadProfile = e.ReadProfile with { Quantity = 39 } }), RuntimeRefusalCodes.PhysicalTcRecordInvalid),
        };

        foreach (var (label, state, expectedCode) in counterfeits)
        {
            var candidate = WithPhysical(simulator) with { TcAcquisition = state };
            Assert.False(RuntimeStateInvariants.TryValidate(candidate, out var code, out var detail), label);
            Assert.Equal(expectedCode, code);
            Assert.NotEqual(string.Empty, detail);
        }

        // The honest record is still valid, so the refusals above are not vacuous.
        Assert.True(RuntimeStateInvariants.TryValidate(WithPhysical(simulator), out var okCode, out var okDetail),
            $"[{okCode}] {okDetail}");
    }

    [Fact]
    public void The_Physical_Record_Is_Immutable_And_Carries_No_Address()
    {
        var physical = PhysicalRecord();

        // Every collection is a read-only wrapper over a private copy, and the record shape has no
        // address-typed member at all.
        Assert.IsAssignableFrom<IReadOnlyList<ushort>>(physical.RawWords);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(physical.NotVerified);
        Assert.Throws<NotSupportedException>(() => ((IList<ushort>)physical.RawWords)[0] = 0);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)physical.NotVerified)[0] = "X");

        // The member sets are asserted EXACTLY, with both sides in Ordinal order. A record can
        // therefore never grow an endpoint, host, address or credential field without failing this
        // test, and the read profile may legitimately carry only the zero-based register start offset
        // and the requested word quantity.
        Assert.Equal(
            ExpectedTcAcquisitionStatePropertyNames,
            typeof(TcAcquisitionState).GetProperties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            ExpectedTcPhysicalReadOnlyEvidencePropertyNames,
            typeof(TcPhysicalReadOnlyEvidence).GetProperties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            ExpectedTcReadProfileIdentityPropertyNames,
            typeof(TcReadProfileIdentity).GetProperties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));

        // The only endpoint statement anywhere is the redacted logical identity, and it is not an address.
        Assert.Equal(PhysicalTcVocabulary.EndpointConfigured, physical.PhysicalEvidence!.EndpointIdentity);
        Assert.DoesNotContain(physical.PhysicalEvidence.EndpointIdentity, c => char.IsDigit(c));
        Assert.DoesNotContain(physical.PhysicalEvidence.EndpointIdentity, c => c == '.');
    }

    private static RuntimeState Apply(RuntimeState state, Tamper tamper) => tamper switch
    {
        Tamper.Profile => state with { DeviceProfile = DeviceProfile.TEST_HARDWARE },
        Tamper.SensorCount => state with { Sensors = state.Sensors.Take(state.Sensors.Count - 1).ToArray() },
        Tamper.SensorOrder => state with { Sensors = SwapFirstTwo(state.Sensors) },
        Tamper.SensorIdentity => state with
        {
            Sensors = ReplaceFirst(state.Sensors, state.Sensors[0] with { SensorId = "NOT-A-SENSOR" }),
        },
        Tamper.SensorChannels => state with
        {
            Sensors = ReplaceFirst(state.Sensors, state.Sensors[0] with { TcFrontChannel = state.Sensors[1].TcRearChannel }),
        },
        // An unknown position kind (the enum holds SENSOR only; a superseded gap value can
        // only arrive as an out-of-vocabulary integer) is refused — fail closed.
        Tamper.UnknownPositionKind => state with
        {
            WallMap = ReplaceFirst(
                state.WallMap,
                state.WallMap[0] with { PositionKind = (LogicalPositionKind)99 }),
        },
        // The former "I7 gap" shape (Sensor slot without a Sensor identity) is refused.
        Tamper.FormerGapShape => state with
        {
            WallMap = state.WallMap
                .Select(slot => slot.LogicalRow == 5 && slot.LogicalColumn == 7 ? slot with { SensorId = null } : slot)
                .ToArray(),
        },
        Tamper.WallSummary => state with
        {
            Walls = ReplaceFirst(state.Walls, state.Walls[0] with { Dirty = state.Walls[0].Dirty + 1 }),
        },
        Tamper.ActiveTarget => state with
        {
            Sensors = ReplaceFirst(
                state.Sensors,
                state.Sensors[0] with { IsActiveJobTarget = true, QueueState = QueueState.ACTIVE }),
        },
        Tamper.QueueShape => state with { Queue = state.Queue with { TotalQueued = 1 } },
        Tamper.TrendBounds => state with { Trend = state.Trend with { Capacity = RuntimeLimits.MaximumTrendCapacity + 1 } },
        Tamper.AlarmCounters => state with { Alarms = state.Alarms with { ActiveUnack = 1 } },
        Tamper.CommunicationDevices => state with
        {
            Communication = new CommunicationHealth
            {
                Devices = Array.AsReadOnly(new[]
                {
                    new DeviceHealth
                    {
                        DeviceId = "SYN-TC-99",
                        State = DeviceLinkState.ONLINE,
                        ConsecutiveTimeouts = 0,
                        LastSuccessAt = null,
                        LastLatencyMs = null,
                        PollsOk = 0,
                        PollsFailed = 0,
                    },
                }),
            },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(tamper), tamper, "Unhandled tamper case."),
    };

    private static SensorPresentationState[] SwapFirstTwo(IReadOnlyList<SensorPresentationState> sensors)
    {
        var copy = sensors.ToArray();
        (copy[0], copy[1]) = (copy[1], copy[0]);
        return copy;
    }

    private static SensorPresentationState[] ReplaceFirst(
        IReadOnlyList<SensorPresentationState> sensors,
        SensorPresentationState replacement)
    {
        var copy = sensors.ToArray();
        copy[0] = replacement;
        return copy;
    }

    private static WallMapSlot[] ReplaceFirst(IReadOnlyList<WallMapSlot> slots, WallMapSlot replacement)
    {
        var copy = slots.ToArray();
        copy[0] = replacement;
        return copy;
    }

    private static WallSummary[] ReplaceFirst(IReadOnlyList<WallSummary> summaries, WallSummary replacement)
    {
        var copy = summaries.ToArray();
        copy[0] = replacement;
        return copy;
    }
}
