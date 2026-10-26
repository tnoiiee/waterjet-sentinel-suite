using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Wjss.Contracts.ProcessIo;
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
        Assert.Throws<InvalidOperationException>(() => transport.ReadCoil(ProcessIoDevices.PumpRack, 0));
    }

    [Fact]
    public void Bench_Runner_Simulation_Mode_Completes_With_Cleanup_Audit()
    {
        string csv = LoadCommittedCatalogueCsv();
        var config = new ProcessIoBenchConfig { Mode = ProcessIoBenchMode.SIMULATION };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.True(report.Accepted);
        Assert.Equal("SIMULATION", report.Mode);
        Assert.Equal("TRIAL_ACCEPTED", report.OutcomeCode);
        Assert.Equal("22", report.Summary["DI_READS_PERFORMED"]);
        Assert.Equal("14", report.Summary["AI_READS_PERFORMED"]);
        Assert.Equal("15", report.Summary["DO_CHANNELS_TESTED"]);
        Assert.Equal(16, report.CleanupAudit.Count);
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
                [ProcessIoDevices.PumpRack] = new() { TargetAddress = "127.0.0.1", Port = 502 }
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
            ConsentPhrase = ProcessIoBenchRunner.RequiredConsentPhrase,
            Endpoints = new Dictionary<string, ProcessIoEndpointConfig>()
        };
        var report = ProcessIoBenchRunner.Run(csv, config);

        Assert.False(report.Accepted);
        Assert.Equal("ENDPOINTS_UNCONFIGURED", report.OutcomeCode);
    }
}
