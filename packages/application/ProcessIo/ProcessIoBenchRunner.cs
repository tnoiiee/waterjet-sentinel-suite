using System;
using System.Collections.Generic;
using System.Linq;
using Wjss.Contracts;

namespace Wjss.Runtime.Core.ProcessIo;

public enum ProcessIoBenchMode
{
    SIMULATION,
    PHYSICAL_DI_AI_READ,
    PHYSICAL_DO_SINGLE,
    PHYSICAL_AO_SINGLE,
    PHYSICAL_FULL_SELECTED_SEQUENCE,
    PHYSICAL_TEST,
}

public sealed record ProcessIoBenchConfig
{
    public ProcessIoBenchMode Mode { get; init; } = ProcessIoBenchMode.SIMULATION;
    public string? ConsentPhrase { get; init; }
    public Dictionary<string, ProcessIoEndpointConfig> Endpoints { get; init; } = new(StringComparer.Ordinal);
    public List<string> AllowDoWrites { get; init; } = new();
    public List<string> AllowAoWrites { get; init; } = new();
}

public sealed record ProcessIoBenchReport
{
    public required string Mode { get; init; }
    public required bool Accepted { get; init; }
    public required string OutcomeCode { get; init; }
    public required string Detail { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required IReadOnlyList<ProcessIoTransportLedgerEntry> Ledger { get; init; }
    public required IReadOnlyDictionary<string, string> Summary { get; init; }
    public required IReadOnlyList<string> CleanupAudit { get; init; }
}

/// <summary>
/// Application-owned Hardware bench runner for Process-I/O commissioning.
/// Supports deterministic SIMULATION and explicit physical commissioning modes.
/// Requires explicit one-use Owner consent for all physical modes.
/// Guaranteed fail-closed safe-return cleanup on both success and failure.
/// </summary>
public static class ProcessIoBenchRunner
{
    public const string ConsentRead = "AUTHORIZE_PHYSICAL_READ_ONLY_COMMISSIONING";
    public const string ConsentDoSingle = "AUTHORIZE_PHYSICAL_DO_SINGLE_COMMISSIONING";
    public const string ConsentAoSingle = "AUTHORIZE_PHYSICAL_AO_SINGLE_COMMISSIONING";
    public const string ConsentFull = "AUTHORIZE_PHYSICAL_BENCH_COMMISSIONING_DISCONNECTED_OUTPUTS";

    public static ProcessIoBenchReport Run(string catalogueCsv, ProcessIoBenchConfig config)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var cleanupAudit = new List<string>();
        var summary = new Dictionary<string, string>(StringComparer.Ordinal);

        bool isPhysical = config.Mode != ProcessIoBenchMode.SIMULATION;

        if (isPhysical)
        {
            string expectedConsent = config.Mode switch
            {
                ProcessIoBenchMode.PHYSICAL_DI_AI_READ => ConsentRead,
                ProcessIoBenchMode.PHYSICAL_DO_SINGLE => ConsentDoSingle,
                ProcessIoBenchMode.PHYSICAL_AO_SINGLE => ConsentAoSingle,
                ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE => ConsentFull,
                ProcessIoBenchMode.PHYSICAL_TEST => ConsentFull,
                _ => string.Empty,
            };

            if (string.IsNullOrEmpty(expectedConsent) || config.ConsentPhrase != expectedConsent)
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    Accepted = false,
                    OutcomeCode = "CONSENT_REFUSED",
                    Detail = $"Physical bench execution for mode {config.Mode} requires exact Owner consent phrase.",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                    Summary = summary,
                    CleanupAudit = cleanupAudit,
                };
            }

            if (config.Endpoints.Count == 0)
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    Accepted = false,
                    OutcomeCode = "ENDPOINTS_UNCONFIGURED",
                    Detail = "Physical bench execution requires configured target endpoints.",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                    Summary = summary,
                    CleanupAudit = cleanupAudit,
                };
            }
        }

        var catalogue = ProcessIoCatalogueLoader.Load(catalogueCsv);
        IProcessIoTransport transport;
        ProcessIoPhysicalTransport? physicalTransport = null;

        if (isPhysical)
        {
            physicalTransport = new ProcessIoPhysicalTransport(config.Endpoints);
            transport = physicalTransport;
        }
        else
        {
            transport = new SimulatedProcessIoTransport();
        }

        try
        {
            var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);
            session.Open(startedAt);

            // Phase 1: Cyclic Reads
            int diReadCount = 0;
            int aiReadCount = 0;

            bool shouldRead = config.Mode == ProcessIoBenchMode.SIMULATION ||
                              config.Mode == ProcessIoBenchMode.PHYSICAL_DI_AI_READ ||
                              config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE ||
                              config.Mode == ProcessIoBenchMode.PHYSICAL_TEST;

            if (shouldRead)
            {
                foreach (var row in catalogue.Where(r => r.BindingState != ProcessIoBindingState.UNBOUND && r.Binding != null))
                {
                    if (row.Binding!.Direction == "READ")
                    {
                        if (row.Binding.Area == ProcessIoArea.DiscreteInput || row.IoClass == "DI")
                        {
                            bool bit = transport.ReadDiscreteInput(row.Binding.DeviceKey, row.Binding.Address);
                            diReadCount++;
                        }
                        else if (row.Binding.Area == ProcessIoArea.InputRegister || row.IoClass == "AI")
                        {
                            ushort word = transport.ReadInputRegister(row.Binding.DeviceKey, row.Binding.Address);
                            aiReadCount++;
                        }
                    }
                }
            }
            summary["DI_READS_PERFORMED"] = diReadCount.ToString();
            summary["AI_READS_PERFORMED"] = aiReadCount.ToString();

            // Phase 2: Sequential DO Trials
            int doTestedCount = 0;
            bool shouldTestDo = config.Mode == ProcessIoBenchMode.SIMULATION ||
                               config.Mode == ProcessIoBenchMode.PHYSICAL_DO_SINGLE ||
                               config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE ||
                               config.Mode == ProcessIoBenchMode.PHYSICAL_TEST;

            if (shouldTestDo)
            {
                var doCandidates = catalogue.Where(r => r.BindingState != ProcessIoBindingState.UNBOUND
                                                       && r.Binding != null
                                                       && r.Binding.Direction == "WRITE"
                                                       && (r.Binding.Area == ProcessIoArea.Coil || r.IoClass == "DO")).ToList();

                if (config.Mode == ProcessIoBenchMode.PHYSICAL_DO_SINGLE)
                {
                    if (config.AllowDoWrites.Count != 1)
                        throw new InvalidOperationException("PHYSICAL_DO_SINGLE mode requires exactly one allowed DO tag in AllowDoWrites.");
                    doCandidates = doCandidates.Where(r => config.AllowDoWrites.Contains(r.Tag)).ToList();
                    if (doCandidates.Count == 0)
                        throw new InvalidOperationException($"Allowed DO tag '{config.AllowDoWrites[0]}' is not a writable DO channel.");
                }
                else if (config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE || config.Mode == ProcessIoBenchMode.PHYSICAL_TEST)
                {
                    if (config.AllowDoWrites.Count == 0)
                        doCandidates.Clear();
                    else
                        doCandidates = doCandidates.Where(r => config.AllowDoWrites.Contains(r.Tag)).ToList();
                }

                foreach (var row in doCandidates)
                {
                    var auth = new ProcessIoWriteAuthorization { PhysicalWriteAuthorized = true, Basis = "BENCH_TRIAL" };
                    var admission = session.AdmitWrite(DateTimeOffset.UtcNow, row, auth);
                    if (!admission.Admitted || admission.Token == null)
                    {
                        throw new InvalidOperationException($"Write admission refused for DO row {row.Tag}: {admission.Code}");
                    }

                    if (isPhysical)
                    {
                        transport.WriteCoil(row.Binding!.DeviceKey, row.Binding.Address, true);
                        transport.ReadCoil(row.Binding.DeviceKey, 512 + row.Binding.Address);
                        transport.WriteCoil(row.Binding.DeviceKey, row.Binding.Address, false);
                        transport.ReadCoil(row.Binding.DeviceKey, 512 + row.Binding.Address);
                    }
                    else
                    {
                        session.DispatchDigitalOutput(DateTimeOffset.UtcNow, admission.Token, row, true);
                        transport.WriteCoil(row.Binding!.DeviceKey, row.Binding.Address, false);
                    }

                    cleanupAudit.Add($"DO_CLEANUP_SAFE_RETURN:{row.Tag}:{row.Binding!.DeviceKey}:{row.Binding.Address}:false");
                    doTestedCount++;
                }
            }
            summary["DO_CHANNELS_TESTED"] = doTestedCount.ToString();

            // Phase 3: AO Trial
            int aoTestedCount = 0;
            bool shouldTestAo = config.Mode == ProcessIoBenchMode.SIMULATION ||
                               config.Mode == ProcessIoBenchMode.PHYSICAL_AO_SINGLE ||
                               config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE ||
                               config.Mode == ProcessIoBenchMode.PHYSICAL_TEST;

            if (shouldTestAo)
            {
                var aoCandidates = catalogue.Where(r => r.BindingState != ProcessIoBindingState.UNBOUND
                                                       && r.Binding != null
                                                       && r.Binding.Direction == "WRITE"
                                                       && (r.Binding.Area == ProcessIoArea.HoldingRegister || r.IoClass == "AO")).ToList();

                if (config.Mode == ProcessIoBenchMode.PHYSICAL_AO_SINGLE)
                {
                    if (config.AllowAoWrites.Count != 1)
                        throw new InvalidOperationException("PHYSICAL_AO_SINGLE mode requires exactly one allowed AO tag in AllowAoWrites.");
                    aoCandidates = aoCandidates.Where(r => config.AllowAoWrites.Contains(r.Tag)).ToList();
                    if (aoCandidates.Count == 0)
                        throw new InvalidOperationException($"Allowed AO tag '{config.AllowAoWrites[0]}' is not a writable AO channel.");
                }
                else if (config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE || config.Mode == ProcessIoBenchMode.PHYSICAL_TEST)
                {
                    if (config.AllowAoWrites.Count == 0)
                        aoCandidates.Clear();
                    else
                        aoCandidates = aoCandidates.Where(r => config.AllowAoWrites.Contains(r.Tag)).ToList();
                }

                foreach (var row in aoCandidates)
                {
                    var auth = new ProcessIoWriteAuthorization { PhysicalWriteAuthorized = true, Basis = "BENCH_TRIAL" };
                    var admission = session.AdmitWrite(DateTimeOffset.UtcNow, row, auth);
                    if (!admission.Admitted || admission.Token == null)
                    {
                        throw new InvalidOperationException($"Write admission refused for AO row {row.Tag}: {admission.Code}");
                    }

                    if (isPhysical)
                    {
                        transport.WriteSingleRegister(row.Binding!.DeviceKey, row.Binding.Address, 6553);
                        transport.ReadRegister(row.Binding.DeviceKey, 512 + row.Binding.Address);
                        transport.WriteSingleRegister(row.Binding.DeviceKey, row.Binding.Address, 0);
                        transport.ReadRegister(row.Binding.DeviceKey, 512 + row.Binding.Address);
                    }
                    else
                    {
                        session.DispatchAnalogOutput(DateTimeOffset.UtcNow, admission.Token, row, 4.0);
                        transport.WriteRegisters(row.Binding!.DeviceKey, row.Binding.Address, new ushort[] { 0 });
                    }

                    cleanupAudit.Add($"AO_CLEANUP_SAFE_RETURN:{row.Tag}:{row.Binding!.DeviceKey}:{row.Binding.Address}:0");
                    aoTestedCount++;
                }
            }
            summary["AO_CHANNELS_TESTED"] = aoTestedCount.ToString();

            session.Close(DateTimeOffset.UtcNow);

            var ledger = physicalTransport?.GetLedger() ?? Array.Empty<ProcessIoTransportLedgerEntry>();

            return new ProcessIoBenchReport
            {
                Mode = config.Mode.ToString(),
                Accepted = true,
                OutcomeCode = "SUCCESS",
                Detail = $"Process-I/O hardware bench completed successfully in {config.Mode} mode.",
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Ledger = ledger,
                Summary = summary,
                CleanupAudit = cleanupAudit,
            };
        }
        catch (Exception ex)
        {
            return new ProcessIoBenchReport
            {
                Mode = config.Mode.ToString(),
                Accepted = false,
                OutcomeCode = "EXECUTION_FAILED",
                Detail = ex.Message,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Ledger = physicalTransport?.GetLedger() ?? Array.Empty<ProcessIoTransportLedgerEntry>(),
                Summary = summary,
                CleanupAudit = cleanupAudit,
            };
        }
        finally
        {
            physicalTransport?.Dispose();
        }
    }
}
