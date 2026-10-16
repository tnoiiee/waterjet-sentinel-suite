using System.Text.Json;
using Wjss.Adapters.MotionLibrary;

// WJSS motion library native-load/version probe harness (Owner-invoked, console only).
//
// Exit codes (mirroring the Runtime host gate semantics):
//   0 — probe accepted: identity, architecture, load, export, version, module-path and
//       deterministic-unload proofs all passed, and the reported version equals the
//       Owner-configured expected version;
//   2 — refused: configuration missing/rejected or any probe proof failed (canonical
//       refusal code printed); nothing was defaulted, corrected or retried;
//   4 — invalid invocation: this harness takes NO arguments.
//
// The output is deliberately redacted: it reports the outcome code, the redacted detail,
// the native-handle-release state and — only on acceptance — the library version. It never
// reports the native directory, any path, any hash, the expected version, or an exception.
// No controller endpoint, discovery, connection, read or motion command exists here.

if (args.Length != 0)
{
    Console.Error.WriteLine(
        "WJSS motion library probe refused [INVALID_INVOCATION]: this harness takes no " +
        "arguments. Configuration comes only from the four Owner-local environment variables " +
        "named in the runbook.");
    return 4;
}

if (!MotionLibraryConfigurationResolver.TryResolve(
        Environment.GetEnvironmentVariable,
        out var configuration,
        out var refusalCode,
        out var refusalDetail))
{
    Console.Error.WriteLine($"WJSS motion library probe refused [{refusalCode}]: {refusalDetail}");
    return 2;
}

var outcome = new MotionLibraryProbeRunner(configuration).Probe();

var json = JsonSerializer.Serialize(
    new
    {
        accepted = outcome.Accepted,
        code = outcome.Code,
        detail = outcome.Detail,
        libraryVersion = outcome.LibraryVersion,
        nativeHandlesReleased = outcome.NativeHandlesReleased,
        configuration = MotionLibraryConfigurationResolver.DescribeConfiguration(configuration),
    },
    new JsonSerializerOptions { WriteIndented = true });

if (outcome.Accepted)
{
    Console.WriteLine(json);
    return 0;
}

Console.Error.WriteLine(json);
return 2;
