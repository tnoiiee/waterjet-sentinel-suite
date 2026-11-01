using System.Globalization;
using Wjss.Contracts;
using Wjss.Runtime.Core.CleaningPath.Composition;
using Wjss.Time;

namespace Wjss.Runtime;

/// <summary>
/// The one Owner-local invocation that exercises the TEST_HARDWARE_DRY composition.
///
/// It is a bounded, single-use console run, not a service: it binds no port, registers no route,
/// exposes no command API and adds no Kiosk control. It is refused unless the dry mode, the dry
/// profile, the Owner-local endpoint, the one-use consent and every Owner-local file are present and
/// valid, and it never substitutes a simulated value for a physical one.
///
/// Exit codes: 0 terminal result reached; 2 composition, prepare or start refusal; 3 bounded
/// timeout before a terminal result; 4 invalid invocation or options/configuration refusal.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA. The Owner-local build and the separately
/// authorized representative dry Hardware trial are the validation of record.
/// </summary>
internal static class CleaningPathDryTrial
{
    /// <summary>The exact argument that selects the dry trial. Nothing else selects it.</summary>
    internal const string Token = "--dry-cleaning-trial";

    /// <summary>Milliseconds between two driver steps. Bounded, deterministic and never zero.</summary>
    private const int StepIntervalMilliseconds = 25;

    /// <summary>The most driver steps one trial may take before it is declared timed out.</summary>
    private const int MaximumSteps = 240000;

    /// <summary>The bounded prepare window for the port open and the two axis homes.</summary>
    private const int MaximumPrepareSteps = 24000;

    /// <summary>
    /// Runs one trial. Returns the process exit code. Every refusal is reported with its canonical
    /// code and nothing else: no endpoint address, no consent phrase, no profile value and no path
    /// ever reaches the console.
    /// </summary>
    internal static int Run(
        DeviceProfile profile,
        int port,
        Func<string, string?> readEnvironment,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!RuntimeHostOptions.TryLoadDryCommand(
                profile, port, readEnvironment, out RuntimeHostOptions options, out string code, out string detail))
        {
            error.WriteLine($"WJSS dry trial refused [{code}]: {detail} Nothing was bound and nothing was contacted.");
            return 4;
        }

        var clock = new SystemClock();

        output.WriteLine("WJSS TEST_HARDWARE_DRY composition");
        output.WriteLine("  profile=" + nameof(DeviceProfile.TEST_HARDWARE_DRY));
        output.WriteLine("  mode=" + ProcessIoEndpointConfiguration.DryModeVariable);
        output.WriteLine("  surface=CONSOLE_ONLY_NO_PORT_NO_ROUTE_NO_KIOSK_CONTROL");

        if (!CleaningPathDryComposition.TryCreate(
                options, readEnvironment, clock, out CleaningPathDryComposition? composition, out code, out detail)
            || composition is null)
        {
            error.WriteLine($"WJSS dry composition refused [{code}]: {detail} Nothing was contacted.");
            return 2;
        }

        CleaningPathDryComposition dry = composition;
        using (dry)
        {
            ReportProvenance(dry.Provenance, output);

            int exitCode = 2;
            try
            {
                if (!dry.Prepare(MaximumPrepareSteps, out code, out detail))
                {
                    error.WriteLine($"WJSS dry prepare refused [{code}]: {detail}");
                    return exitCode;
                }

                output.WriteLine("  prepare=DRY_PREPARE_READY");

                CleaningPathCompositionStartOutcome start = dry.Start();
                if (!start.Started)
                {
                    error.WriteLine(
                        $"WJSS dry composition start refused [{start.Code}]: "
                        + "Pump-ready was not accepted and no P1 point was requested.");
                    return exitCode;
                }

                output.WriteLine("  start=" + start.Code);
                output.WriteLine("  pump_readiness=" + start.PumpReadiness?.Code);

                int steps = 0;
                while (dry.Result is null && steps < MaximumSteps)
                {
                    Thread.Sleep(StepIntervalMilliseconds);
                    dry.Step();
                    steps += 1;
                }

                CleaningPathCompositionResult? result = dry.Result;
                if (result is null)
                {
                    error.WriteLine(
                        "WJSS dry composition did not reach a terminal result within the bounded step budget "
                        + "[" + steps.ToString(CultureInfo.InvariantCulture) + " steps]. Nothing is retried.");
                    exitCode = 3;
                }
                else
                {
                    output.WriteLine("  terminal_status=" + result.Status);
                    output.WriteLine("  terminal_code=" + result.Code);
                    output.WriteLine("  primary_failure=" + (result.Handoff.PrimaryFailureCode ?? "NONE"));
                    output.WriteLine("  steps=" + steps.ToString(CultureInfo.InvariantCulture));
                    exitCode = 0;
                }
            }
            finally
            {
                CleaningPathDryCleanupReport cleanup = dry.Shutdown();
                output.WriteLine("  cleanup.motion.complete=" + cleanup.Motion.Complete.ToString());
                output.WriteLine("  cleanup.motion.session=" + cleanup.Motion.SessionState);
                output.WriteLine("  cleanup.motion.close=" + cleanup.Motion.CloseCode);
                output.WriteLine("  cleanup.valves.complete=" + cleanup.Valves.Complete.ToString());
                output.WriteLine("  cleanup.valves.attempted=" + cleanup.Valves.AttemptedValveIds.Count.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("  cleanup.valves.closed=" + cleanup.Valves.ClosedValveIds.Count.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("  cleanup.valves.refused=" + cleanup.Valves.RefusedValves.Count.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("  cleanup.valves.transport_failures=" + cleanup.Valves.TransportFailures.Count.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("  cleanup.valves.unknown=" + cleanup.Valves.RemainingUnknownState.Count.ToString(CultureInfo.InvariantCulture));
                output.WriteLine("  cleanup.complete=" + cleanup.Complete.ToString());
                output.WriteLine("  session_state=" + cleanup.SessionState);
                output.WriteLine("  session_generation=" + cleanup.SessionGeneration.ToString(CultureInfo.InvariantCulture));
            }

            return exitCode;
        }
    }

    /// <summary>
    /// Prints the composition provenance. Every line is a canonical identity or a redacted digest;
    /// the endpoint address, the consent phrase and every Owner-local value stay out of the output.
    /// </summary>
    private static void ReportProvenance(CleaningPathDryProvenance provenance, TextWriter output)
    {
        output.WriteLine("  endpoint=" + provenance.EndpointFingerprint);
        output.WriteLine("  process_io_device=" + provenance.ProcessIoDeviceKey);
        output.WriteLine("  catalogue_rows=" + provenance.CatalogueRowCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("  pump_ready_source=" + provenance.PumpReadySourceTag);
        output.WriteLine("  sensor=" + provenance.SensorId);
        output.WriteLine("  water_jet=" + provenance.WaterJetId);
        output.WriteLine("  isolation_valve=" + provenance.IsolationValveId);
        output.WriteLine("  axes=" + provenance.HorizontalAxisId + "/" + provenance.VerticalAxisId);
        output.WriteLine("  motion_port=" + provenance.MotionPortMode);
        output.WriteLine("  motion_profile=" + provenance.MotionProfileCode);
        output.WriteLine("  write_authority=" + (provenance.SessionAuthorizedWrites ? "PROCESS_IO_SESSION" : "NONE"));
        output.WriteLine("  coordinators=" + (provenance.CoordinatorsComposed ? "COMPOSITION+EXECUTION" : "NONE"));
        foreach (string statement in CleaningPathDryProvenance.StandingStatements)
        {
            output.WriteLine("  " + statement);
        }
    }
}
