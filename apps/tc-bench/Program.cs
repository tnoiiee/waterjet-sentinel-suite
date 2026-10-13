using System.Text.Json;
using Wjss.Adapters.TcAcquisition;
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
        // Fail-closed gate order: build identity and physical capability, artifact/signature location,
        // exact payload bytes, strict schema and canonical form, trusted key identifier, detached
        // signature, validity window, implementation/base/source/build-identity binding, manifest
        // digest, exact candidate tuple and scope, then atomic single-use consumption. Only a plan
        // returned here can construct a client, and no authorization artifact can supply the
        // deployment identity.
        var decision = PhysicalTrialAuthorization.Authorize(catalog, BuildIdentity.LoadTrusted(), options.AuthorizationPath, options.SignaturePath);
        if (decision.Plan is null)
        {
            // Typed refusal only: no path, no message and no partially acquired fact.
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                mode = "PHYSICAL_TEST",
                refusalCode = decision.RefusalCode,
                acquisitionPerformed = false,
                deploymentState = PhysicalTrialRefusals.DeploymentNotAuthorized,
            }, StrictJson.Options));
            return 2;
        }
        var evidence = await new TrialRunner().RunPhysicalTrialAsync(decision.Plan, () => TcReadOnlyClientFactory.Create(), CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new { mode = "PHYSICAL_TEST", configuration = selected.Trial, evidence }, StrictJson.Options));
        return 0;
    }
    var evidencePlan = new TrialRunner().DryRun(catalog, options.TrialId);
    Console.WriteLine(JsonSerializer.Serialize(new { mode = "SIMULATION", configuration = selected.Trial, evidencePlan }, StrictJson.Options));
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException or IOException or InvalidDataException)
{
    // No raw exception message or path is reflected into stdout/stderr evidence.
    Console.Error.WriteLine("TC bench refused: configuration or authorization rejected. No acquisition performed.");
    return 2;
}
