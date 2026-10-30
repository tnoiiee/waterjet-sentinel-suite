using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Wjss.Contracts;
using Wjss.Runtime.Core.ProcessIo;

namespace Wjss.Runtime.Core.Tests.ProcessIo;

public sealed class ProcessIoBenchRunnerTests
{
    private static string LoadCommittedCatalogueCsv()
        => File.ReadAllText(ProcessIoTestPaths.CommittedCataloguePath);

    [Fact]
    public void Committed_Catalogue_Path_Resolves_To_Repository_Docs_Evidence()
    {
        string root = ProcessIoTestPaths.RepoRoot();
        string path = ProcessIoTestPaths.CommittedCataloguePath;
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');

        Assert.True(File.Exists(path), $"committed catalogue evidence file missing: {path}");
        Assert.Equal("docs/evidence/process-io-inventory-2026-10-22.csv", ProcessIoTestPaths.CommittedCatalogueRelativePath);
        Assert.Equal("docs/evidence/process-io-inventory-2026-10-22.csv", relative);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "docs", "evidence", "process-io-inventory-2026-10-22.csv")), path);

        // Resolved under the repository root, never outside it.
        Assert.False(relative.StartsWith("..", StringComparison.Ordinal), $"catalogue escapes the repository root: {path}");

        // Never the test tree: the evidence lives in the repository, not in tests/docs.
        string testTree = Path.Combine(root, "tests") + Path.DirectorySeparatorChar;
        Assert.False(relative.StartsWith("tests/", StringComparison.Ordinal), $"catalogue resolved inside the test tree: {path}");
        Assert.False(relative.Contains("tests/docs", StringComparison.Ordinal), $"catalogue resolved under tests/docs: {path}");
        Assert.False(path.StartsWith(testTree, StringComparison.Ordinal), $"catalogue resolved under the test tree: {path}");
    }

    [Fact]
    public void Physical_Transport_Refuses_Without_Configured_Endpoint()
    {
        using var transport = new ProcessIoPhysicalTransport(new Dictionary<string, ProcessIoEndpointConfig>());
        Assert.Throws<InvalidOperationException>(() => transport.ReadDiscreteInput(ProcessIoDevices.RackT8, 0));
    }

    [Fact]
    public void Physical_Transport_Refuses_When_Disposed_With_ObjectDisposedException()
    {
        var transport = new ProcessIoPhysicalTransport(new Dictionary<string, ProcessIoEndpointConfig>
        {
            [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502, UnitId = 1 }
        });
        transport.Dispose();
        Assert.Throws<ObjectDisposedException>(() => transport.ReadDiscreteInput(ProcessIoDevices.RackT8, 0));
        Assert.Equal(0, transport.NetworkAttempts);
    }

    [Fact]
    public void Physical_Transport_Refuses_Invalid_Unit_Id_With_ArgumentOutOfRangeException_Before_Network_Io()
    {
        using var transportZero = new ProcessIoPhysicalTransport(new Dictionary<string, ProcessIoEndpointConfig>
        {
            [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502, UnitId = 0 }
        });
        var exZero = Assert.Throws<ArgumentOutOfRangeException>(() => transportZero.ReadDiscreteInput(ProcessIoDevices.RackT8, 0));
        Assert.Equal("deviceKey", exZero.ParamName);
        Assert.Equal((byte)0, exZero.ActualValue);
        Assert.Contains("1-247", exZero.Message);
        Assert.Contains(ProcessIoDevices.RackT8, exZero.Message);
        Assert.Equal(0, transportZero.NetworkAttempts);

        using var transportHigh = new ProcessIoPhysicalTransport(new Dictionary<string, ProcessIoEndpointConfig>
        {
            [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502, UnitId = 248 }
        });
        var exHigh = Assert.Throws<ArgumentOutOfRangeException>(() => transportHigh.ReadDiscreteInput(ProcessIoDevices.RackT8, 0));
        Assert.Equal("deviceKey", exHigh.ParamName);
        Assert.Equal((byte)248, exHigh.ActualValue);
        Assert.Contains("1-247", exHigh.Message);
        Assert.Contains(ProcessIoDevices.RackT8, exHigh.Message);
        Assert.Equal(0, transportHigh.NetworkAttempts);
    }

    [Fact]
    public void Bench_Runner_Simulation_Mode_Completes_With_Cleanup_Audit()
    {
        string csv = LoadCommittedCatalogueCsv();
        var config = new ProcessIoBenchConfig { Mode = ProcessIoBenchMode.SIMULATION };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.True(report.Accepted);
        Assert.Equal("SIMULATION", report.Mode);
        Assert.Equal("SUCCESS", report.OutcomeCode);
        Assert.Equal("WORKBOOK_CANONICAL", report.ReadProfile);
        Assert.Equal("40", report.Summary["DI_READS_PERFORMED"]);
        Assert.Equal("36", report.Summary["AI_READS_PERFORMED"]);
        Assert.Equal("31", report.Summary["DO_CHANNELS_TESTED"]);
        Assert.Equal("1", report.Summary["AO_CHANNELS_TESTED"]);
        Assert.Equal(32, report.CleanupAudit.Count);
    }

    [Fact]
    public void Bench_Runner_Still_Reads_Spare_Physical_Addresses_For_Commissioning()
    {
        string csv = LoadCommittedCatalogueCsv();
        var catalogue = ProcessIoCatalogueLoader.Load(csv);
        var spareRows = catalogue.Where(r => r.SourceClassification == "SPARE" && r.Binding is not null).ToList();

        // The Product observation gate must not remove the Workbook-derived physical mapping:
        // the bench still covers every bound channel, including the spare channels.
        Assert.Equal(4, spareRows.Count);
        Assert.Contains(spareRows, r => r.Tag == "AI-036" && r.Binding!.Address == 35);
        Assert.Contains(spareRows, r => r.Tag == "DI-038" && r.Binding!.Address == 37);
        Assert.Contains(spareRows, r => r.Tag == "DI-039" && r.Binding!.Address == 38);
        Assert.Contains(spareRows, r => r.Tag == "DI-040" && r.Binding!.Address == 39);

        var report = ProcessIoBenchRunner.Run(csv, new ProcessIoBenchConfig { Mode = ProcessIoBenchMode.SIMULATION });
        Assert.True(report.Accepted);
        Assert.Equal("40", report.Summary["DI_READS_PERFORMED"]);
        Assert.Equal("36", report.Summary["AI_READS_PERFORMED"]);
        Assert.Equal("32", report.Summary["DO_READBACKS_PERFORMED"]);
        Assert.Equal("2", report.Summary["AO_READBACKS_PERFORMED"]);
    }

    [Fact]
    public void Bench_Runner_Simulation_Mode_Supports_Explicit_Read_Profiles()
    {
        string csv = LoadCommittedCatalogueCsv();
        var configCanonical = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.SIMULATION,
            ReadProfile = ProcessIoReadProfiles.WorkbookCanonical,
        };
        var reportCanonical = ProcessIoBenchRunner.Run(csv, configCanonical);
        Assert.True(reportCanonical.Accepted);
        Assert.Equal("WORKBOOK_CANONICAL", reportCanonical.ReadProfile);
        Assert.Equal("40", reportCanonical.Summary["DI_READS_PERFORMED"]);

        var configLegacy = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.SIMULATION,
            ReadProfile = ProcessIoReadProfiles.LegacyProductionCompat,
        };
        var reportLegacy = ProcessIoBenchRunner.Run(csv, configLegacy);
        Assert.True(reportLegacy.Accepted);
        Assert.Equal("LEGACY_PRODUCTION_COMPAT", reportLegacy.ReadProfile);
        Assert.Equal("100", reportLegacy.Summary["LEGACY_COILS_READ"]);
        Assert.Equal("100", reportLegacy.Summary["LEGACY_HOLDING_REGISTERS_READ"]);
    }

    [Fact]
    public void Bench_Runner_Refuses_Deprecated_Physical_Test_Mode()
    {
        string csv = LoadCommittedCatalogueCsv();
        var config = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_TEST,
            ConsentPhrase = ProcessIoBenchRunner.ConsentFull,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>
            {
                [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502 }
            }
        };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.False(report.Accepted);
        Assert.Equal("MODE_DEPRECATED", report.OutcomeCode);
    }

    [Fact]
    public void Bench_Runner_Physical_Mode_Refuses_CLI_Consent()
    {
        string csv = LoadCommittedCatalogueCsv();
        var config = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_DI_AI_READ,
            ConsentPhrase = ProcessIoBenchRunner.ConsentRead,
            CliConsentSupplied = true,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>
            {
                [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502 }
            }
        };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.False(report.Accepted);
        Assert.Equal("CLI_CONSENT_REFUSED_USE_ENV", report.OutcomeCode);
    }

    [Fact]
    public void Bench_Runner_Physical_Mode_Refuses_Without_Exact_Consent_Phrase()
    {
        string csv = LoadCommittedCatalogueCsv();
        var config = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE,
            ConsentPhrase = "INVALID_CONSENT",
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>
            {
                [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502 }
            }
        };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.False(report.Accepted);
        Assert.Equal("CONSENT_REFUSED", report.OutcomeCode);
    }

    [Fact]
    public void Bench_Runner_Physical_Mode_Refuses_Without_Configured_Endpoints()
    {
        string csv = LoadCommittedCatalogueCsv();
        var config = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE,
            ConsentPhrase = ProcessIoBenchRunner.ConsentFull,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>()
        };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.False(report.Accepted);
        Assert.Equal("ENDPOINTS_UNCONFIGURED", report.OutcomeCode);
    }

    [Fact]
    public void Bench_Runner_Physical_Mode_Refuses_Invalid_Unit_Id()
    {
        string csv = LoadCommittedCatalogueCsv();
        var config = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_DI_AI_READ,
            ConsentPhrase = ProcessIoBenchRunner.ConsentRead,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>
            {
                [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502, UnitId = 0 }
            }
        };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.False(report.Accepted);
        Assert.Equal("UNIT_ID_INVALID", report.OutcomeCode);
    }

    [Fact]
    public void Bench_Runner_Physical_Do_Single_Requires_Hold_Ms_In_Range()
    {
        string csv = LoadCommittedCatalogueCsv();
        var configMissing = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_DO_SINGLE,
            ConsentPhrase = ProcessIoBenchRunner.ConsentDoSingle,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>
            {
                [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502 }
            },
            AllowDoWrites = new List<string> { "DO-001" },
        };
        var reportMissing = ProcessIoBenchRunner.Run(csv, configMissing);
        Assert.False(reportMissing.Accepted);
        Assert.Equal("HOLD_MS_REQUIRED", reportMissing.OutcomeCode);

        var configTooShort = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_DO_SINGLE,
            ConsentPhrase = ProcessIoBenchRunner.ConsentDoSingle,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>
            {
                [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502 }
            },
            AllowDoWrites = new List<string> { "DO-001" },
            HoldMs = 50,
        };
        var reportTooShort = ProcessIoBenchRunner.Run(csv, configTooShort);
        Assert.False(reportTooShort.Accepted);
        Assert.Equal("HOLD_MS_INVALID", reportTooShort.OutcomeCode);
    }

    [Fact]
    public void Bench_Runner_Physical_Ao_Single_Requires_Test_Raw_And_Write_Function()
    {
        string csv = LoadCommittedCatalogueCsv();
        var configMissingRaw = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_AO_SINGLE,
            ConsentPhrase = ProcessIoBenchRunner.ConsentAoSingle,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>
            {
                [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502 }
            },
            AllowAoWrites = new List<string> { "AO-001+" },
            AoWriteFunction = "FC06",
        };
        var reportMissingRaw = ProcessIoBenchRunner.Run(csv, configMissingRaw);
        Assert.False(reportMissingRaw.Accepted);
        Assert.Equal("AO_RAW_REQUIRED", reportMissingRaw.OutcomeCode);

        var configMissingFc = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_AO_SINGLE,
            ConsentPhrase = ProcessIoBenchRunner.ConsentAoSingle,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>
            {
                [ProcessIoDevices.RackT8] = new() { TargetAddress = "127.0.0.1", Port = 502 }
            },
            AllowAoWrites = new List<string> { "AO-001+" },
            AoTestRaw = 6553,
        };
        var reportMissingFc = ProcessIoBenchRunner.Run(csv, configMissingFc);
        Assert.False(reportMissingFc.Accepted);
        Assert.Equal("AO_WRITE_FUNCTION_REQUIRED", reportMissingFc.OutcomeCode);
    }
}
