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
    {
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../docs/evidence/process-io-inventory-2026-10-22.csv"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void Physical_Transport_Refuses_Without_Configured_Endpoint()
    {
        using var transport = new ProcessIoPhysicalTransport(new Dictionary<string, ProcessIoEndpointConfig>());
        Assert.Throws<InvalidOperationException>(() => transport.ReadDiscreteInput(ProcessIoDevices.RackT8, 0));
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
        Assert.Equal("40", report.Summary["DI_READS_PERFORMED"]);
        Assert.Equal("36", report.Summary["AI_READS_PERFORMED"]);
        Assert.Equal("31", report.Summary["DO_CHANNELS_TESTED"]);
        Assert.Equal("1", report.Summary["AO_CHANNELS_TESTED"]);
        Assert.Equal(32, report.CleanupAudit.Count);
    }

    [Fact]
    public void Bench_Runner_Physical_Mode_Refuses_Without_Exact_Consent_Phrase()
    {
        string csv = LoadCommittedCatalogueCsv();
        var config = new ProcessIoBenchConfig
        {
            Mode = ProcessIoBenchMode.PHYSICAL_TEST,
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
            Mode = ProcessIoBenchMode.PHYSICAL_TEST,
            ConsentPhrase = ProcessIoBenchRunner.ConsentFull,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>()
        };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.False(report.Accepted);
        Assert.Equal("ENDPOINTS_UNCONFIGURED", report.OutcomeCode);
    }
}
