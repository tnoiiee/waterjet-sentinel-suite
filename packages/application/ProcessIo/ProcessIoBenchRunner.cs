using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    public bool CliConsentSupplied { get; init; }
    public string ReadProfile { get; init; } = ProcessIoReadProfiles.WorkbookCanonical;
    public int? HoldMs { get; init; }
    public ushort? AoTestRaw { get; init; }
    public string? AoWriteFunction { get; init; }
    public Dictionary<string, ProcessIoEndpointConfig> Endpoints { get; init; } = new(StringComparer.Ordinal);
    public List<string> AllowDoWrites { get; init; } = new();
    public List<string> AllowAoWrites { get; init; } = new();
}

public sealed record ProcessIoBenchReport
{
    public required string Mode { get; init; }
    public required string ReadProfile { get; init; }
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
    public const string ConsentRead = ProcessIoConsentPhrases.PhysicalDiAiRead;
    public const string ConsentDoSingle = ProcessIoConsentPhrases.PhysicalDoSingle;
    public const string ConsentAoSingle = ProcessIoConsentPhrases.PhysicalAoSingle;
    public const string ConsentFull = ProcessIoConsentPhrases.PhysicalFullSelectedSequence;

    public static ProcessIoBenchReport Run(string catalogueCsv, ProcessIoBenchConfig config)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var cleanupAudit = new List<string>();
        var summary = new Dictionary<string, string>(StringComparer.Ordinal);

        if (config.Mode == ProcessIoBenchMode.PHYSICAL_TEST)
        {
            return new ProcessIoBenchReport
            {
                Mode = config.Mode.ToString(),
                ReadProfile = config.ReadProfile,
                Accepted = false,
                OutcomeCode = "MODE_DEPRECATED",
                Detail = "Mode PHYSICAL_TEST is deprecated and refused. Use explicit physical modes: PHYSICAL_DI_AI_READ, PHYSICAL_DO_SINGLE, PHYSICAL_AO_SINGLE, PHYSICAL_FULL_SELECTED_SEQUENCE.",
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                Summary = summary,
                CleanupAudit = cleanupAudit,
            };
        }

        bool isPhysical = config.Mode != ProcessIoBenchMode.SIMULATION;

        if (isPhysical && config.CliConsentSupplied)
        {
            return new ProcessIoBenchReport
            {
                Mode = config.Mode.ToString(),
                ReadProfile = config.ReadProfile,
                Accepted = false,
                OutcomeCode = "CLI_CONSENT_REFUSED_USE_ENV",
                Detail = "Consent for physical modes must be provided via WJSS_PROCESS_IO_CONSENT environment variable, not command-line arguments, to prevent exposure in process table and shell history.",
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                Summary = summary,
                CleanupAudit = cleanupAudit,
            };
        }

        if (isPhysical)
        {
            string expectedConsent = config.Mode switch
            {
                ProcessIoBenchMode.PHYSICAL_DI_AI_READ => ConsentRead,
                ProcessIoBenchMode.PHYSICAL_DO_SINGLE => ConsentDoSingle,
                ProcessIoBenchMode.PHYSICAL_AO_SINGLE => ConsentAoSingle,
                ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE => ConsentFull,
                _ => string.Empty,
            };

            if (string.IsNullOrEmpty(expectedConsent) || config.ConsentPhrase != expectedConsent)
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    ReadProfile = config.ReadProfile,
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
                    ReadProfile = config.ReadProfile,
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

            foreach (var (devKey, ep) in config.Endpoints)
            {
                if (ep.UnitId < ProcessIoTransportShape.MinUnitId || ep.UnitId > ProcessIoTransportShape.MaxUnitId)
                {
                    return new ProcessIoBenchReport
                    {
                        Mode = config.Mode.ToString(),
                        ReadProfile = config.ReadProfile,
                        Accepted = false,
                        OutcomeCode = "UNIT_ID_INVALID",
                        Detail = $"Endpoint {devKey} specifies Unit ID {ep.UnitId}, which is outside valid physical range ({ProcessIoTransportShape.MinUnitId}-{ProcessIoTransportShape.MaxUnitId}).",
                        StartedAt = startedAt,
                        CompletedAt = DateTimeOffset.UtcNow,
                        Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                        Summary = summary,
                        CleanupAudit = cleanupAudit,
                    };
                }
            }
        }

        if (config.ReadProfile != ProcessIoReadProfiles.WorkbookCanonical &&
            config.ReadProfile != ProcessIoReadProfiles.LegacyProductionCompat)
        {
            return new ProcessIoBenchReport
            {
                Mode = config.Mode.ToString(),
                ReadProfile = config.ReadProfile,
                Accepted = false,
                OutcomeCode = "INVALID_READ_PROFILE",
                Detail = $"Read profile '{config.ReadProfile}' is not recognized. Valid profiles: {ProcessIoReadProfiles.WorkbookCanonical}, {ProcessIoReadProfiles.LegacyProductionCompat}.",
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                Summary = summary,
                CleanupAudit = cleanupAudit,
            };
        }

        if (config.Mode == ProcessIoBenchMode.PHYSICAL_DO_SINGLE)
        {
            if (config.HoldMs == null)
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    ReadProfile = config.ReadProfile,
                    Accepted = false,
                    OutcomeCode = "HOLD_MS_REQUIRED",
                    Detail = "PHYSICAL_DO_SINGLE mode requires HoldMs parameter (100-2000 ms).",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                    Summary = summary,
                    CleanupAudit = cleanupAudit,
                };
            }

            if (config.HoldMs.Value < ProcessIoTransportShape.MinDoHoldMs || config.HoldMs.Value > ProcessIoTransportShape.MaxDoHoldMs)
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    ReadProfile = config.ReadProfile,
                    Accepted = false,
                    OutcomeCode = "HOLD_MS_INVALID",
                    Detail = $"HoldMs value {config.HoldMs.Value} is outside valid range ({ProcessIoTransportShape.MinDoHoldMs}-{ProcessIoTransportShape.MaxDoHoldMs} ms).",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                    Summary = summary,
                    CleanupAudit = cleanupAudit,
                };
            }
        }

        if (config.Mode == ProcessIoBenchMode.PHYSICAL_AO_SINGLE)
        {
            if (config.AoTestRaw == null)
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    ReadProfile = config.ReadProfile,
                    Accepted = false,
                    OutcomeCode = "AO_RAW_REQUIRED",
                    Detail = "PHYSICAL_AO_SINGLE mode requires AoTestRaw parameter (0-32767).",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                    Summary = summary,
                    CleanupAudit = cleanupAudit,
                };
            }

            if (config.AoTestRaw.Value > 32767)
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    ReadProfile = config.ReadProfile,
                    Accepted = false,
                    OutcomeCode = "AO_RAW_INVALID",
                    Detail = $"AoTestRaw value {config.AoTestRaw.Value} is outside valid range (0-32767).",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                    Summary = summary,
                    CleanupAudit = cleanupAudit,
                };
            }

            if (string.IsNullOrEmpty(config.AoWriteFunction))
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    ReadProfile = config.ReadProfile,
                    Accepted = false,
                    OutcomeCode = "AO_WRITE_FUNCTION_REQUIRED",
                    Detail = "PHYSICAL_AO_SINGLE mode requires AoWriteFunction parameter (FC06 or FC16).",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Ledger = Array.Empty<ProcessIoTransportLedgerEntry>(),
                    Summary = summary,
                    CleanupAudit = cleanupAudit,
                };
            }

            string fc = config.AoWriteFunction.ToUpperInvariant();
            if (fc != "FC06" && fc != "FC16")
            {
                return new ProcessIoBenchReport
                {
                    Mode = config.Mode.ToString(),
                    ReadProfile = config.ReadProfile,
                    Accepted = false,
                    OutcomeCode = "AO_WRITE_FUNCTION_INVALID",
                    Detail = $"AoWriteFunction value '{config.AoWriteFunction}' is invalid. Allowed: FC06, FC16.",
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
            bool shouldRead = config.Mode == ProcessIoBenchMode.SIMULATION ||
                              config.Mode == ProcessIoBenchMode.PHYSICAL_DI_AI_READ ||
                              config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE;

            if (shouldRead)
            {
                if (config.ReadProfile == ProcessIoReadProfiles.WorkbookCanonical)
                {
                    int diReadCount = 0;
                    int aiReadCount = 0;
                    int doReadBackCount = 0;
                    int aoReadBackCount = 0;

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

                    for (int addr = 512; addr <= 543; addr++)
                    {
                        transport.ReadCoil(ProcessIoDevices.RackT8, addr);
                        doReadBackCount++;
                    }
                    for (int addr = 512; addr <= 513; addr++)
                    {
                        transport.ReadRegister(ProcessIoDevices.RackT8, addr);
                        aoReadBackCount++;
                    }

                    summary["DI_READS_PERFORMED"] = diReadCount.ToString();
                    summary["AI_READS_PERFORMED"] = aiReadCount.ToString();
                    summary["DO_READBACKS_PERFORMED"] = doReadBackCount.ToString();
                    summary["AO_READBACKS_PERFORMED"] = aoReadBackCount.ToString();
                }
                else if (config.ReadProfile == ProcessIoReadProfiles.LegacyProductionCompat)
                {
                    transport.ReadCoilsBulk(ProcessIoDevices.RackT8, 0, 100);
                    transport.ReadHoldingRegistersBulk(ProcessIoDevices.RackT8, 0, 100);

                    summary["LEGACY_COILS_READ"] = "100";
                    summary["LEGACY_HOLDING_REGISTERS_READ"] = "100";
                }
            }

            // Phase 2: Sequential DO Trials
            int doTestedCount = 0;
            bool shouldTestDo = config.Mode == ProcessIoBenchMode.SIMULATION ||
                               config.Mode == ProcessIoBenchMode.PHYSICAL_DO_SINGLE ||
                               config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE;

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
                else if (config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE)
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

                    int holdMs = config.HoldMs ?? 100;
                    if (isPhysical)
                    {
                        transport.WriteCoil(row.Binding!.DeviceKey, row.Binding.Address, true);
                        transport.ReadCoil(row.Binding.DeviceKey, 512 + row.Binding.Address);

                        var swHold = System.Diagnostics.Stopwatch.StartNew();
                        Thread.Sleep(holdMs);
                        swHold.Stop();
                        summary["REQUESTED_HOLD_MS"] = holdMs.ToString();
                        summary["OBSERVED_HOLD_MS"] = ((int)swHold.ElapsedMilliseconds).ToString();

                        transport.WriteCoil(row.Binding.DeviceKey, row.Binding.Address, false);
                        transport.ReadCoil(row.Binding.DeviceKey, 512 + row.Binding.Address);
                    }
                    else
                    {
                        session.DispatchDigitalOutput(DateTimeOffset.UtcNow, admission.Token, row, true);
                        if (config.HoldMs != null)
                        {
                            summary["REQUESTED_HOLD_MS"] = holdMs.ToString();
                            summary["OBSERVED_HOLD_MS"] = holdMs.ToString();
                        }
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
                               config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE;

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
                else if (config.Mode == ProcessIoBenchMode.PHYSICAL_FULL_SELECTED_SEQUENCE)
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

                    ushort rawVal = config.AoTestRaw ?? 0;
                    string aoFc = config.AoWriteFunction?.ToUpperInvariant() ?? "FC06";

                    if (isPhysical)
                    {
                        if (aoFc == "FC16")
                            transport.WriteRegisters(row.Binding!.DeviceKey, row.Binding.Address, new ushort[] { rawVal });
                        else
                            transport.WriteSingleRegister(row.Binding!.DeviceKey, row.Binding.Address, rawVal);

                        transport.ReadRegister(row.Binding.DeviceKey, 512 + row.Binding.Address);

                        if (aoFc == "FC16")
                            transport.WriteRegisters(row.Binding.DeviceKey, row.Binding.Address, new ushort[] { 0 });
                        else
                            transport.WriteSingleRegister(row.Binding.DeviceKey, row.Binding.Address, 0);

                        transport.ReadRegister(row.Binding.DeviceKey, 512 + row.Binding.Address);
                    }
                    else
                    {
                        session.DispatchAnalogOutput(DateTimeOffset.UtcNow, admission.Token, row, rawVal);
                        transport.WriteRegisters(row.Binding!.DeviceKey, row.Binding.Address, new ushort[] { 0 });
                    }

                    summary["AO_WRITE_FUNCTION"] = aoFc;
                    summary["AO_TEST_RAW"] = rawVal.ToString();
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
                ReadProfile = config.ReadProfile,
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
                ReadProfile = config.ReadProfile,
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
