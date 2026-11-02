using System.Globalization;
using Wjss.Contracts;
using Wjss.Runtime.Core.ProcessIo;
using Wjss.Runtime.Core.Sequencing;

namespace Wjss.Runtime;

/// <summary>
/// TRIAL A: the isolated, read-only physical Pump-ready trial.
///
/// It reads ONE physical analogue input, <c>AI-003+</c>, the sole Pump-ready pressure authority, through
/// the existing Product transport, the existing <see cref="ProcessIoSession"/> observation authority and
/// the existing engineering conversion, and it hands the resulting sample to the existing
/// <c>SequencingPressure.PumpReady</c> rule.
/// Nothing is commanded.
///
/// Zero writes are possible on this path, and that is structural rather than asserted: the runner never
/// constructs a write-authorization record, never holds an equipment boundary, and contains no write
/// admission, no digital-output dispatch, no coil write and no register write of any kind. It references
/// no motion, queue or Production type either. A read needs no authority, and the total absence of a
/// write authority is what keeps this trial read-only rather than merely well behaved.
///
/// <c>AI-002+</c> (Pump Inlet Pressure) is diagnostic only. It is observed separately and reported as NOT
/// USED FOR THE DECISION, so the separation is a fact in the record rather than a claim. It is never read
/// as an alias of <c>AI-003+</c>, never used as a fallback and never able to make the trial pass.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA. The Owner-local build and the separately authorized
/// physical run are the validation of record.
///
/// Exit codes: 0 the physical <c>AI-003+</c> observation is valid, current and strictly above the
/// configured Pump-ready setpoint; 2 the observation completed but Pump-ready was not accepted (invalid
/// quality, stale or future-dated, wrong source identity, superseded generation, or at/below the
/// setpoint); 3 bounded physical read timeout or transport fault; 4 invocation, selector, profile,
/// endpoint, consent, catalogue or configuration refusal before any physical acquisition begins.
/// </summary>
public static class ProcessIoPumpReadyReadTrial
{
    /// <summary>The published command identity of this trial. Nothing else selects it.</summary>
    internal const string Token = "--pump-ready-read-trial";

    /// <summary>The dedicated Trial A selector. No other selector reaches this runner.</summary>
    public const string ModeVariable = "WJSS_PUMP_READY_READ_TRIAL";

    /// <summary>The only accepted selector literal. It is compared exactly, never normalized.</summary>
    public const string ModeAi003Only = "AI-003_ONLY";

    /// <summary>The evidence identity printed by every run of this trial.</summary>
    public const string TrialIdentity = "PHYSICAL_PUMP_READY_READ_ONLY";

    /// <summary>The evidence class of an accepted physical read. Never claimed for anything else.</summary>
    public const string EvidenceKindPhysical = "PHYSICAL";

    /// <summary>The evidence class of a run that never obtained a physical reading.</summary>
    public const string EvidenceKindNotEstablished = "NOT_ESTABLISHED";

    /// <summary>The role of <c>AI-002+</c> in this trial: reported, never decisive.</summary>
    public const string Ai002Role = "DIAGNOSTIC_ONLY_NOT_USED_FOR_DECISION";

    /// <summary>The Owner-local freshness budget for the single observation. Never defaulted.</summary>
    public const string FreshnessVariable = "WJSS_PUMP_READY_FRESHNESS_MS";

    /// <summary>The Owner-local catalogue location, shared with the integrated dry composition.</summary>
    public const string CatalogueVariable = CleaningPathDryComposition.CatalogueVariable;

    /// <summary>The largest catalogue this trial will read, matching the composition root's own bound.</summary>
    private const long MaximumCatalogueBytes = 1048576;

    // ------------------------------------------------------------------ refusals before acquisition

    /// <summary>No selector was present, so this trial was never asked for.</summary>
    public const string RefusedModeNotSelected = "PUMP_READY_TRIAL_REFUSED_MODE_NOT_SELECTED";

    /// <summary>A selector was present but is not the exact accepted literal.</summary>
    public const string RefusedModeInvalid = "PUMP_READY_TRIAL_REFUSED_MODE_INVALID";

    /// <summary>The integrated dry composition was also selected, so no evidence class is chosen.</summary>
    public const string RefusedIntegratedDrySelected = "PUMP_READY_TRIAL_REFUSED_INTEGRATED_DRY_SELECTED";

    /// <summary>The IV Hardware trial was also selected, so no evidence class is chosen.</summary>
    public const string RefusedIvTrialSelected = "PUMP_READY_TRIAL_REFUSED_IV_TRIAL_SELECTED";

    /// <summary>PRODUCTION is never startable by this trial.</summary>
    public const string RefusedProduction = "PUMP_READY_TRIAL_REFUSED_PRODUCTION_NOT_AUTHORIZED";

    /// <summary>The profile is not the authorized physical Hardware profile.</summary>
    public const string RefusedProfileNotHardware = "PUMP_READY_TRIAL_REFUSED_PROFILE_NOT_TEST_HARDWARE";

    /// <summary>The endpoint is missing or was rejected.</summary>
    public const string RefusedEndpoint = "PUMP_READY_TRIAL_REFUSED_ENDPOINT";

    /// <summary>The read-only consent is missing or does not match the published read-only phrase.</summary>
    public const string RefusedConsent = "PUMP_READY_TRIAL_REFUSED_READ_ONLY_CONSENT";

    /// <summary>The catalogue is missing, is not fully qualified, or was refused by the loader.</summary>
    public const string RefusedCatalogue = "PUMP_READY_TRIAL_REFUSED_CATALOGUE";

    /// <summary>The <c>AI-003+</c> row is absent, unbound, or not an adopted READ Input Register.</summary>
    public const string RefusedAi003Binding = "PUMP_READY_TRIAL_REFUSED_AI003_BINDING";

    /// <summary>The Pump-ready setpoint or the pressure band is missing or invalid.</summary>
    public const string RefusedThresholds = "PUMP_READY_TRIAL_REFUSED_THRESHOLDS";

    /// <summary>The freshness budget is missing or outside the accepted bound.</summary>
    public const string RefusedFreshness = "PUMP_READY_TRIAL_REFUSED_FRESHNESS";

    /// <summary>The session was not open, so no read was attempted.</summary>
    public const string RefusedSessionNotOpen = "PUMP_READY_TRIAL_REFUSED_SESSION_NOT_OPEN";

    // ------------------------------------------------------------------ outcome classifications

    /// <summary>A valid, current physical reading strictly above the configured setpoint.</summary>
    public const string Accepted = "PUMP_READY_ACCEPTED_PHYSICAL";

    /// <summary>The observation completed but cannot be used as current process evidence.</summary>
    public const string RefusedNotUsable = "PUMP_READY_REFUSED_OBSERVATION_NOT_USABLE";

    /// <summary>The value did not come from a physical read, so it is not physical evidence.</summary>
    public const string RefusedWrongProvenance = "PUMP_READY_REFUSED_NOT_A_PHYSICAL_READ";

    /// <summary>The evidence was outside its freshness budget when it was judged.</summary>
    public const string RefusedStale = "PUMP_READY_REFUSED_EVIDENCE_STALE";

    /// <summary>The evidence was stamped after the judging clock, so it is refused, never clamped.</summary>
    public const string RefusedFuture = "PUMP_READY_REFUSED_EVIDENCE_FUTURE_DATED";

    /// <summary>The evidence belongs to a superseded session generation.</summary>
    public const string RefusedGeneration = "PUMP_READY_REFUSED_GENERATION_NOT_LIVE";

    /// <summary>The reading is at or below the configured Pump-ready setpoint.</summary>
    public const string RefusedAtOrBelowSetpoint = "PUMP_READY_REFUSED_AT_OR_BELOW_SETPOINT";

    /// <summary>The sample did not satisfy the existing validity rule at all.</summary>
    public const string RefusedInvalidSample = "PUMP_READY_REFUSED_SAMPLE_INVALID";

    /// <summary>The bounded physical read failed: a timeout or a transport fault, never a reading.</summary>
    public const string ReadTimeout = "PUMP_READY_READ_TIMEOUT_OR_TRANSPORT_FAULT";

    // ------------------------------------------------------------------ selector

    /// <summary>
    /// Reads the Trial A selector only. Absent means the trial was NOT chosen and the host continues down
    /// its existing path; any present value other than the exact literal is a refusal.
    /// </summary>
    public static bool TryReadMode(
        Func<string, string?> readEnvironment,
        out bool trialSelected,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        trialSelected = false;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(ModeVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!string.Equals(raw, ModeAi003Only, StringComparison.Ordinal))
        {
            refusalCode = RefusedModeInvalid;
            refusalDetail =
                $"{ModeVariable} must be exactly '{ModeAi003Only}' when present. No other value selects the "
                + "read-only Pump-ready trial and nothing is substituted.";
            return false;
        }

        trialSelected = true;
        return true;
    }

    // ------------------------------------------------------------------ read-only consent

    /// <summary>
    /// Reads the read-only consent. It is consumed from the existing published consent variable, but it is
    /// validated against the published READ-ONLY phrase, never the single-output write phrase, because a
    /// read-only acquisition must not be authorized by write consent.
    /// </summary>
    public static bool TryReadReadOnlyConsent(
        Func<string, string?> readEnvironment,
        out string? consentCode,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        consentCode = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(ProcessIoEndpointConfiguration.ConsentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            refusalCode = RefusedConsent;
            refusalDetail =
                $"{ProcessIoEndpointConfiguration.ConsentVariable} must carry the published read-only Process-I/O "
                + "consent phrase. It is read from the environment only, so it never appears in a process table "
                + "or in shell history. No value is defaulted.";
            return false;
        }

        if (!string.Equals(raw, ProcessIoConsentPhrases.PhysicalDiAiRead, StringComparison.Ordinal))
        {
            refusalCode = RefusedConsent;
            refusalDetail =
                $"{ProcessIoEndpointConfiguration.ConsentVariable} does not match the published read-only "
                + "Process-I/O consent phrase. The single-output write phrase never authorizes a read trial.";
            return false;
        }

        // The phrase itself is never carried, printed, logged or persisted: only a stable digest code is.
        consentCode = ConsentCode(raw);
        return true;
    }

    // ------------------------------------------------------------------ configuration

    /// <summary>
    /// Validates the complete Trial A configuration without contacting anything: selector, exclusions,
    /// profile, endpoint, read-only consent, catalogue, the <c>AI-003+</c> binding, the Pump-ready
    /// setpoint and the freshness budget. Every value is Owner local, nothing is defaulted, and every
    /// other Product selector is refused rather than composed alongside this trial.
    /// </summary>
    public static bool TryReadConfiguration(
        DeviceProfile profile,
        Func<string, string?> readEnvironment,
        out PumpReadyReadConfiguration? configuration,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        configuration = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        // PRODUCTION is refused before any other value is read.
        if (profile == DeviceProfile.PRODUCTION)
        {
            refusalCode = RefusedProduction;
            refusalDetail = "PRODUCTION is never startable by this trial. Nothing was read and nothing was contacted.";
            return false;
        }

        if (!TryReadMode(readEnvironment, out bool trialSelected, out refusalCode, out refusalDetail))
        {
            return false;
        }

        if (!trialSelected)
        {
            refusalCode = RefusedModeNotSelected;
            refusalDetail =
                $"The read-only Pump-ready trial requires {ModeVariable}='{ModeAi003Only}'. No mode is inferred "
                + "from the profile, the endpoint or any other variable.";
            return false;
        }

        // The other two physical trials are separate evidence classes and are never composed together.
        if (!ProcessIoEndpointConfiguration.TryReadDryMode(
                readEnvironment, out bool integratedDrySelected, out string dryCode, out string dryDetail))
        {
            refusalCode = dryCode;
            refusalDetail = dryDetail;
            return false;
        }

        if (integratedDrySelected)
        {
            refusalCode = RefusedIntegratedDrySelected;
            refusalDetail =
                $"{ProcessIoEndpointConfiguration.DryModeVariable} selects the integrated dry composition, which "
                + "commands valves and moves axes. This trial is read-only and never composes beside it.";
            return false;
        }

        if (!DryIvTrial.TryReadMode(readEnvironment, out bool ivTrialSelected, out string ivCode, out string ivDetail))
        {
            refusalCode = ivCode;
            refusalDetail = ivDetail;
            return false;
        }

        if (ivTrialSelected)
        {
            refusalCode = RefusedIvTrialSelected;
            refusalDetail =
                $"{DryIvTrial.ModeVariable} selects the IV Hardware trial, which commands a valve and rests on a "
                + "simulated AI-003 value. This trial is physical, read-only, and never composed beside it.";
            return false;
        }

        // TEST_HARDWARE is the only authorized profile. TEST_HARDWARE_DRY commands, TEST_HARDWARE_READ_ONLY
        // is committed to the one-Node TC read path, SIMULATOR has no physical transmitter and PRODUCTION is
        // already refused above.
        if (profile != DeviceProfile.TEST_HARDWARE)
        {
            refusalCode = RefusedProfileNotHardware;
            refusalDetail =
                $"The read-only Pump-ready trial composes only for TEST_HARDWARE; the profile is '{profile}'. No "
                + "profile is substituted and no simulated runtime stands in for a physical transmitter.";
            return false;
        }

        if (!ProcessIoEndpointConfiguration.TryResolveEndpoint(
                readEnvironment, out ProcessIoEndpointHandle? endpoint, out refusalCode, out refusalDetail)
            || endpoint is null)
        {
            refusalCode = refusalCode.Length == 0 ? RefusedEndpoint : refusalCode;
            return false;
        }

        if (!TryReadReadOnlyConsent(readEnvironment, out string? consentCode, out refusalCode, out refusalDetail)
            || consentCode is null)
        {
            return false;
        }

        if (!TryLoadCatalogue(readEnvironment, out IReadOnlyDictionary<string, ProcessIoCatalogueRow>? catalogue, out refusalCode, out refusalDetail)
            || catalogue is null)
        {
            return false;
        }

        if (!TryValidateAi003Binding(catalogue, out ProcessIoCatalogueRow? pumpRow, out refusalCode, out refusalDetail)
            || pumpRow is null)
        {
            return false;
        }

        if (!TryReadThresholds(readEnvironment, out SequencingPressureThresholds? thresholds, out refusalCode, out refusalDetail)
            || thresholds is null)
        {
            return false;
        }

        if (!TryReadFreshness(readEnvironment, out TimeSpan freshness, out refusalCode, out refusalDetail))
        {
            return false;
        }

        configuration = new PumpReadyReadConfiguration
        {
            Profile = profile,
            ModeLiteral = ModeAi003Only,
            Endpoint = endpoint,
            ConsentCode = consentCode,
            Catalogue = catalogue,
            PumpRow = pumpRow,
            Thresholds = thresholds,
            Freshness = freshness,
        };
        return true;
    }

    /// <summary>
    /// Loads the Owner-local catalogue through the existing loader, which is the only catalogue authority.
    /// The file must be fully qualified, present, inside the accepted size bound and accepted verbatim by
    /// the loader; a refusal here happens before any transport exists.
    /// </summary>
    public static bool TryLoadCatalogue(
        Func<string, string?> readEnvironment,
        out IReadOnlyDictionary<string, ProcessIoCatalogueRow>? catalogue,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        catalogue = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? location = readEnvironment(CatalogueVariable);
        if (string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location))
        {
            refusalCode = RefusedCatalogue;
            refusalDetail = $"{CatalogueVariable} must name one fully qualified Owner-local catalogue file.";
            return false;
        }

        try
        {
            var info = new FileInfo(location);
            if (!info.Exists || info.Length > MaximumCatalogueBytes)
            {
                refusalCode = RefusedCatalogue;
                refusalDetail = "The Owner-local catalogue file is missing or larger than the accepted one megabyte.";
                return false;
            }

            IReadOnlyList<ProcessIoCatalogueRow> rows = ProcessIoCatalogueLoader.Load(File.ReadAllText(location));
            var index = new Dictionary<string, ProcessIoCatalogueRow>(StringComparer.Ordinal);
            foreach (ProcessIoCatalogueRow row in rows)
            {
                index[row.Tag] = row;
            }

            catalogue = index;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            refusalCode = RefusedCatalogue;
            refusalDetail = "The Owner-local catalogue was refused: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Proves the Pump Outlet Pressure row is present, adopted, and is exactly an adopted READ Input
    /// Register on the Process-I/O rack. A row with the wrong area or direction is refused rather than
    /// read through the wrong transport call, and no other row is ever substituted for it.
    /// </summary>
    public static bool TryValidateAi003Binding(
        IReadOnlyDictionary<string, ProcessIoCatalogueRow> catalogue,
        out ProcessIoCatalogueRow? pumpRow,
        out string refusalCode,
        out string refusalDetail)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        pumpRow = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string tag = ProcessIoIsolationValves.PumpOutletPressureTag;
        if (!catalogue.TryGetValue(tag, out ProcessIoCatalogueRow? row))
        {
            refusalCode = RefusedAi003Binding;
            refusalDetail = $"The adopted catalogue has no row for {tag}; no other row is substituted for it.";
            return false;
        }

        if (row.Binding is null || row.BindingState == ProcessIoBindingState.UNBOUND)
        {
            refusalCode = RefusedAi003Binding;
            refusalDetail = $"{tag} has no adopted binding; it is never read through an inferred address.";
            return false;
        }

        if (row.Binding.Area != ProcessIoArea.InputRegister
            || !string.Equals(row.Binding.Direction, "READ", StringComparison.Ordinal))
        {
            refusalCode = RefusedAi003Binding;
            refusalDetail =
                $"{tag} must be an adopted READ Input Register; the catalogue row resolves to "
                + $"{row.Binding.Area}/{row.Binding.Direction}. No other function code is used for it.";
            return false;
        }

        if (!string.Equals(row.Binding.DeviceKey, ProcessIoDevices.RackT8, StringComparison.Ordinal))
        {
            refusalCode = RefusedAi003Binding;
            refusalDetail = $"{tag} must bind to the adopted Process-I/O rack; it resolves to another device.";
            return false;
        }

        pumpRow = row;
        return true;
    }

    // ------------------------------------------------------------------ the run

    /// <summary>
    /// Runs one bounded read-only observation and returns the process exit code. Nothing is retried, and
    /// nothing is substituted on refusal.
    /// </summary>
    public static int Run(
        DeviceProfile profile,
        Func<string, string?> readEnvironment,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryReadConfiguration(profile, readEnvironment, out PumpReadyReadConfiguration? configuration, out string code, out string detail)
            || configuration is null)
        {
            error.WriteLine($"WJSS Pump-ready read trial refused [{code}]: {detail} Nothing was bound and nothing was contacted.");
            return 4;
        }

        PumpReadyReadConfiguration trial = configuration;

        var endpoints = new Dictionary<string, ProcessIoEndpointConfig>(StringComparer.Ordinal)
        {
            [ProcessIoDevices.RackT8] = new ProcessIoEndpointConfig
            {
                TargetAddress = trial.Endpoint.Host,
                Port = trial.Endpoint.Port,
                UnitId = trial.Endpoint.UnitId,
                TimeoutMs = ProcessIoTransportShape.ReadTimeoutMs,
            },
        };

        output.WriteLine("WJSS read-only physical Pump-ready trial");
        output.WriteLine("  TRIAL: " + TrialIdentity);
        output.WriteLine("  trial=" + Token);
        output.WriteLine("  selector=" + ModeVariable + "=" + ModeAi003Only);
        output.WriteLine("  surface=CONSOLE_ONLY_NO_PORT_NO_ROUTE_NO_KIOSK_CONTROL");
        output.WriteLine("  endpoint=" + trial.Endpoint.Fingerprint);
        output.WriteLine("  consent=" + trial.ConsentCode);
        output.WriteLine("  pump_ready_source_tag=" + ProcessIoIsolationValves.PumpOutletPressureTag);
        output.WriteLine("  motion=NONE");
        output.WriteLine("  iv_command=NONE");
        output.WriteLine("  queue=NONE");

        ProcessIoPhysicalTransport transport = new ProcessIoPhysicalTransport(endpoints);
        var session = new ProcessIoSession(ProcessIoDevices.RackT8, transport);

        try
        {
            session.Open(DateTimeOffset.UtcNow);

            if (!string.Equals(session.State, "OPEN", StringComparison.Ordinal))
            {
                error.WriteLine($"WJSS Pump-ready read trial refused [{RefusedSessionNotOpen}]: the session did not open. Nothing was read.");
                return 4;
            }

            PumpReadyReadOutcome outcome;
            try
            {
                outcome = Observe(
                    new PumpReadyReadRequest
                    {
                        PumpRow = trial.PumpRow,
                        Catalogue = trial.Catalogue,
                        Thresholds = trial.Thresholds,
                        Freshness = trial.Freshness,
                    },
                    session);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                // A transport fault outside the observation's own capture is a bounded read failure,
                // never a fabricated reading and never a reason to retry.
                error.WriteLine($"WJSS Pump-ready read trial [{ReadTimeout}]: {ex.Message}");
                error.WriteLine("  no value was produced, nothing was written and no retry follows.");
                return 3;
            }

            ReportOutcome(outcome, trial, output);
            return outcome.ExitCode;
        }
        finally
        {
            // The session closes after the observation, and the transport is released afterwards.
            if (!string.Equals(session.State, "CLOSED", StringComparison.Ordinal))
            {
                session.Close(DateTimeOffset.UtcNow);
            }

            transport.Dispose();

            // These two lines are printed only after the lifecycle is complete, so the record states a
            // fact rather than an intention.
            output.WriteLine("  session_state=" + session.State);
            output.WriteLine("  transport_released=TRUE");
        }
    }

    // ------------------------------------------------------------------ the observation

    /// <summary>
    /// The bounded single-use observation over an already open session. It reads, evaluates and reports;
    /// it never writes, never commands and never opens a motion session.
    /// </summary>
    public static PumpReadyReadOutcome Observe(
        PumpReadyReadRequest request,
        ProcessIoSession session)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);

        if (!string.Equals(session.State, "OPEN", StringComparison.Ordinal))
        {
            return new PumpReadyReadOutcome
            {
                Ai003 = RefusedObservation(ProcessIoIsolationValves.PumpOutletPressureTag),
                Ai002Role = ProcessIoPumpReadyReadTrial.Ai002Role,
                SessionGeneration = session.Generation,
                Code = RefusedSessionNotOpen,
                ExitCode = 4,
            };
        }

        // Both timestamps come from the same clock and bracket the transport call, so a slow read is
        // reported as stale rather than silently accepted.
        DateTimeOffset observedAt = request.Time.GetUtcNow();
        ProcessIoPhysicalObservation observation = session.ObserveAnalogInput(
            ProcessIoIsolationValves.PumpOutletPressureTag,
            request.PumpRow,
            observedAt,
            request.Time.GetUtcNow(),
            request.Freshness);

        int generation = session.Generation;

        // AI-002+ is observed separately and only to record that it was NOT used. It is never read as an
        // alias, never a fallback, and it cannot reach the decision below.
        ProcessIoPhysicalObservation? inlet = null;
        if (!string.Equals(ProcessIoIsolationValves.PumpInletPressureTag, ProcessIoIsolationValves.PumpOutletPressureTag, StringComparison.Ordinal)
            && request.Catalogue.TryGetValue(ProcessIoIsolationValves.PumpInletPressureTag, out ProcessIoCatalogueRow? inletRow)
            && inletRow.Binding is not null)
        {
            DateTimeOffset inletAt = request.Time.GetUtcNow();
            inlet = session.ObserveAnalogInput(
                ProcessIoIsolationValves.PumpInletPressureTag, inletRow, inletAt, request.Time.GetUtcNow(), request.Freshness);
        }

        return Classify(observation, inlet, generation, request.Time.GetUtcNow(), request);
    }

    /// <summary>
    /// The single Trial A decision. It is a pure function of one observation, the live generation, the
    /// judging clock and the configured rule, so the decision can be replayed from a record without
    /// contacting anything. It adds no rule of its own: acceptance is exactly
    /// <c>SequencingPressure.PumpReady</c> over a
    /// sample built from a physical read, and every refusal is a classified reason.
    /// </summary>
    public static PumpReadyReadOutcome Classify(
        ProcessIoPhysicalObservation observation,
        ProcessIoPhysicalObservation? inletDiagnostic,
        int sessionGeneration,
        DateTimeOffset now,
        PumpReadyReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(request);

        var outcome = new PumpReadyReadOutcome
        {
            Ai003 = observation,
            Ai002Diagnostic = inletDiagnostic,
            Ai002Role = ProcessIoPumpReadyReadTrial.Ai002Role,
            SessionGeneration = sessionGeneration,
            Code = RefusedNotUsable,
            ExitCode = 2,
        };

        // A transport fault is a bounded read failure, not a low reading: it exits 3 and never reaches the
        // Pump-ready rule at all.
        if (string.Equals(observation.DiagnosticCode, ProcessIoRefusals.ObservationTransportFault, StringComparison.Ordinal))
        {
            return outcome with { Code = ReadTimeout, ExitCode = 3 };
        }

        // A simulated or absent read is never physical evidence, whatever its value.
        if (observation.Provenance != ProcessIoObservationProvenance.PHYSICAL_READ)
        {
            return outcome with { Code = RefusedWrongProvenance };
        }

        // Evidence stamped after the judging clock is not evidence of the present. It is refused, not
        // clamped, and the existing time-aware rule performs the refusal.
        if (observation.ObservedAt > now)
        {
            return outcome with { Code = RefusedFuture };
        }

        if (!observation.IsUsable)
        {
            return outcome with
            {
                Code = observation.Quality == ProcessIoObservationQuality.STALE ? RefusedStale : RefusedNotUsable,
            };
        }

        if (observation.SourceGeneration != sessionGeneration)
        {
            return outcome with { Code = RefusedGeneration };
        }

        // The value must still be current at the moment of judgement, not only at the moment of the read.
        if (!observation.IsUsableAt(now, sessionGeneration, request.Freshness))
        {
            return outcome with { Code = RefusedStale };
        }

        double? engineering = observation.EngineeringValue;
        if (engineering is null)
        {
            return outcome with { Code = RefusedNotUsable };
        }

        // The sample carries the Product's own Pump Outlet identity and a GOOD quality only because every
        // check above passed. Nothing here fabricates quality or substitutes another source.
        var sample = new PressureSample(
            engineering.Value,
            PressureQuality.GOOD,
            false,
            observation.ObservedAt.ToUniversalTime(),
            PressureSample.PumpOutletSource);

        if (!SequencingPressure.PumpReady(sample, request.Thresholds, now, TimeSpan.Zero))
        {
            return outcome with
            {
                Code = !SequencingPressure.Valid(sample, now, TimeSpan.Zero)
                    ? RefusedInvalidSample
                    : RefusedAtOrBelowSetpoint,
            };
        }

        return outcome with
        {
            PumpReady = true,
            Code = Accepted,
            ExitCode = 0,
        };
    }

    /// <summary>A refused observation with no raw value, no engineering value and no quality claim.</summary>
    private static ProcessIoPhysicalObservation RefusedObservation(string tag)
    {
        DateTimeOffset at = DateTimeOffset.UtcNow;
        return new ProcessIoPhysicalObservation
        {
            Tag = tag,
            RawValue = 0,
            EngineeringValue = null,
            Unit = null,
            Quality = ProcessIoObservationQuality.DISABLED,
            ObservedAt = at,
            ReceivedAt = at,
            SourceGeneration = 0,
            Fresh = false,
            Provenance = ProcessIoObservationProvenance.NOT_OBSERVED,
            DiagnosticCode = ProcessIoRefusals.SessionNotOpen,
        };
    }

    /// <summary>
    /// A stable, non-reversible digest of the supplied consent, so a record can state WHICH consent
    /// authorized the trial without ever carrying the phrase.
    /// </summary>
    private static string ConsentCode(string consent)
    {
        byte[] digest = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(consent));
        return "consent-sha256-prefix:" + Convert.ToHexString(digest, 0, 6).ToLowerInvariant();
    }

    // ------------------------------------------------------------------ configuration helpers

    private static bool TryReadThresholds(
        Func<string, string?> readEnvironment,
        out SequencingPressureThresholds? thresholds,
        out string refusalCode,
        out string refusalDetail)
    {
        thresholds = null;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        if (!TryReadDouble(readEnvironment, CleaningPathDryComposition.PumpReadySetpointVariable, out double setpoint))
        {
            refusalCode = RefusedThresholds;
            refusalDetail =
                $"{CleaningPathDryComposition.PumpReadySetpointVariable} must carry one finite bar value. The "
                + "Pump-ready setpoint is never defaulted, because a defaulted setpoint would fabricate a decision.";
            return false;
        }

        // The Pump-ready rule depends on the setpoint alone. Low and High are carried unchanged when the
        // Owner supplies them, and are never invented as Owner values when they are absent.
        double low = 0;
        double high = 1;
        string? rawLow = readEnvironment(CleaningPathDryComposition.PressureLowVariable);
        string? rawHigh = readEnvironment(CleaningPathDryComposition.PressureHighVariable);
        if (!string.IsNullOrWhiteSpace(rawLow)
            && !TryReadDouble(readEnvironment, CleaningPathDryComposition.PressureLowVariable, out low))
        {
            refusalCode = RefusedThresholds;
            refusalDetail = $"{CleaningPathDryComposition.PressureLowVariable} is present but is not a finite number.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rawHigh)
            && !TryReadDouble(readEnvironment, CleaningPathDryComposition.PressureHighVariable, out high))
        {
            refusalCode = RefusedThresholds;
            refusalDetail = $"{CleaningPathDryComposition.PressureHighVariable} is present but is not a finite number.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rawHigh))
        {
            high = Math.Max(low + 1, setpoint + 1);
        }

        var candidate = new SequencingPressureThresholds(low, high, setpoint);
        if (!candidate.Valid)
        {
            refusalCode = SequencingPressureThresholds.InvalidCode;
            refusalDetail =
                "The Owner-local pressure thresholds are not valid: Low must be at least zero, High must "
                + "exceed Low, and the Pump-ready setpoint must be at least zero.";
            return false;
        }

        thresholds = candidate;
        return true;
    }

    private static bool TryReadFreshness(
        Func<string, string?> readEnvironment,
        out TimeSpan freshness,
        out string refusalCode,
        out string refusalDetail)
    {
        freshness = TimeSpan.Zero;
        refusalCode = string.Empty;
        refusalDetail = string.Empty;

        string? raw = readEnvironment(FreshnessVariable);
        if (string.IsNullOrWhiteSpace(raw)
            || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int milliseconds)
            || milliseconds is < 1 or > 600000)
        {
            refusalCode = RefusedFreshness;
            refusalDetail = $"{FreshnessVariable} must be an integer between 1 and 600000 milliseconds. It is never defaulted.";
            return false;
        }

        freshness = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    private static bool TryReadDouble(Func<string, string?> readEnvironment, string variable, out double value)
    {
        value = 0;
        string? raw = readEnvironment(variable);
        return !string.IsNullOrWhiteSpace(raw)
               && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value);
    }

    // ------------------------------------------------------------------ reporting

    private static void ReportOutcome(
        PumpReadyReadOutcome outcome,
        PumpReadyReadConfiguration trial,
        TextWriter output)
    {
        ProcessIoPhysicalObservation observation = outcome.Ai003;

        output.WriteLine("  trial_code=" + outcome.Code);
        output.WriteLine("  source_tag=" + ProcessIoIsolationValves.PumpOutletPressureTag);

        // The evidence class states how the value was acquired. The verdict states whether it was accepted.
        output.WriteLine("  evidence_kind=" + (observation.Provenance == ProcessIoObservationProvenance.PHYSICAL_READ
            ? EvidenceKindPhysical
            : EvidenceKindNotEstablished));

        // Physical verification is TRUE only when the read-only chain actually passed end to end.
        output.WriteLine("  physical_pump_verified=" + Flag(outcome.PumpReady));
        output.WriteLine("  physical_ai003_verified=" + Flag(outcome.PumpReady));
        output.WriteLine("  pump_ready=" + Flag(outcome.PumpReady));

        output.WriteLine("  raw_value=" + observation.RawValue.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  engineering_bar=" + (observation.EngineeringValue?.ToString("0.####", CultureInfo.InvariantCulture) ?? "UNAVAILABLE"));
        output.WriteLine("  engineering_unit=" + (observation.Unit ?? "UNAVAILABLE"));
        output.WriteLine("  quality=" + observation.Quality);
        output.WriteLine("  observed_at=" + observation.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        output.WriteLine("  received_at=" + observation.ReceivedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        output.WriteLine("  fresh=" + Flag(observation.Fresh));
        output.WriteLine("  provenance=" + observation.Provenance);
        output.WriteLine("  observation_diagnostic=" + observation.DiagnosticCode);
        output.WriteLine("  session_generation=" + outcome.SessionGeneration.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  configured_setpoint_bar=" + trial.Thresholds.PumpReadySetpointBar.ToString("0.####", CultureInfo.InvariantCulture));
        output.WriteLine("  strict_comparison="
                          + (observation.EngineeringValue?.ToString("0.####", CultureInfo.InvariantCulture) ?? "UNAVAILABLE")
                          + " > "
                          + trial.Thresholds.PumpReadySetpointBar.ToString("0.####", CultureInfo.InvariantCulture)
                          + " => "
                          + Flag(outcome.PumpReady));

        // AI-002+ is reported only as excluded. It is never the decision input.
        output.WriteLine("  ai002_role=" + outcome.Ai002Role);
        output.WriteLine("  ai002_read=" + Flag(outcome.Ai002Diagnostic is not null));
        if (outcome.Ai002Diagnostic is not null)
        {
            output.WriteLine("  ai002_raw_value=" + outcome.Ai002Diagnostic.RawValue.ToString(CultureInfo.InvariantCulture));
        }

        output.WriteLine("  ai002_used_for_decision=FALSE");
        output.WriteLine("  iv_command_executed=FALSE");
        output.WriteLine("  motion_executed=FALSE");
        output.WriteLine("  waterjet_command_executed=FALSE");
        output.WriteLine("  write_authorization_created=FALSE");
    }

    private static string Flag(bool value) => value ? "TRUE" : "FALSE";
}

/// <summary>One validated Trial A configuration. Every member came from an Owner-local value.</summary>
public sealed record PumpReadyReadConfiguration
{
    public required DeviceProfile Profile { get; init; }

    /// <summary>The validated selector literal, carried so the report can restate what selected the trial.</summary>
    public required string ModeLiteral { get; init; }

    public required ProcessIoEndpointHandle Endpoint { get; init; }

    /// <summary>A digest code for the consent that was supplied. The phrase itself is never carried.</summary>
    public required string ConsentCode { get; init; }

    /// <summary>The catalogue loaded from the Owner-local file by the existing loader.</summary>
    public required IReadOnlyDictionary<string, ProcessIoCatalogueRow> Catalogue { get; init; }

    /// <summary>The validated <c>AI-003+</c> row: the sole Pump-ready source.</summary>
    public required ProcessIoCatalogueRow PumpRow { get; init; }

    public required SequencingPressureThresholds Thresholds { get; init; }

    public required TimeSpan Freshness { get; init; }
}

/// <summary>One bounded read-only request over an already open session.</summary>
public sealed record PumpReadyReadRequest
{
    public required ProcessIoCatalogueRow PumpRow { get; init; }

    public required IReadOnlyDictionary<string, ProcessIoCatalogueRow> Catalogue { get; init; }

    public required SequencingPressureThresholds Thresholds { get; init; }

    public required TimeSpan Freshness { get; init; }

    /// <summary>
    /// The clock used for both observation timestamps and the judging instant. It is injectable so the
    /// same decision can be replayed, and it is the only clock this runner consults.
    /// </summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}

/// <summary>
/// The typed Trial A result. The physical observation and the diagnostic inlet observation are separate
/// members, so a reader can never mistake the diagnostic for the decision input.
/// </summary>
public sealed record PumpReadyReadOutcome
{
    public required ProcessIoPhysicalObservation Ai003 { get; init; }

    /// <summary>The Inlet Pressure observation, reported as diagnostic only. Never a decision input.</summary>
    public ProcessIoPhysicalObservation? Ai002Diagnostic { get; init; }

    public required string Ai002Role { get; init; }

    public required int SessionGeneration { get; init; }

    public required string Code { get; init; }

    public required int ExitCode { get; init; }

    /// <summary>True only when the physical chain passed and the sample strictly exceeded the setpoint.</summary>
    public bool PumpReady { get; init; }
}
