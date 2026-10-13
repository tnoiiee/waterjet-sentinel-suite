using System.Text.Json;
using Wjss.TcBench;

// No endpoint switches, listeners, environment activation, capture, or acceptance command.
try
{
    var options = BenchArguments.Parse(args);
    var catalog = TrialCatalog.LoadTrusted();
    var selected = catalog.Select(options.TrialId);
    if (options.ManifestPath is not null) catalog.ValidateManifest(options.TrialId, File.ReadAllText(options.ManifestPath));
    if (options.Mode == BenchMode.PHYSICAL_TEST)
    {
        var authorization = options.AuthorizationPath is null ? null : File.ReadAllText(options.AuthorizationPath);
        // No authorization is shipped; T01 is not executable. Do not derive approval from software scope.
        catalog.RequirePhysicalAuthorization(options.TrialId, authorization, "NOT_PROVIDED");
        throw new InvalidOperationException("PHYSICAL_DEPLOYMENT_NOT_AUTHORIZED");
    }
    var evidence = new TrialRunner().DryRun(catalog, options.TrialId);
    Console.WriteLine(JsonSerializer.Serialize(new { mode = "SIMULATION", configuration = selected.Trial, evidencePlan = evidence }, StrictJson.Options));
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException or IOException)
{
    // No raw exception message or path is reflected into stdout/stderr evidence.
    Console.Error.WriteLine("TC bench refused: configuration or authorization rejected. No acquisition performed.");
    return 2;
}
