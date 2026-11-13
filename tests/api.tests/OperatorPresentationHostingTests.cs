using System.Reflection;
using Wjss.Contracts;
using Xunit;

namespace Wjss.Runtime.Api.Tests;

/// <summary>
/// Stage 0.4C-K4 hosting facts: the Operator presentation is a read-only FILE surface of this host.
/// The host owns the document, the frozen module whitelist and the liveness answer the Kiosk shell
/// probes, so no second process and no static-file middleware stands between the screen and the
/// Runtime. These facts pin the source shape that carries those guarantees - route identities, the
/// order in which the asset handler refuses before it composes anything, the parity between the
/// whitelist and the files that actually ship, and the parity between what the presentation asks for
/// and what this host registers.
///
/// The HTTP answers themselves (200 on the document and its modules, 404 on an unknown or nested
/// name) are exercised Owner-local against the built host, as documented by this project: composing
/// the runtime in process is what these tests do, and no test-only package may be added to reach a
/// listener.
/// </summary>
public sealed class OperatorPresentationHostingTests
{
    /// <summary>
    /// The module and stylesheet names the host is willing to serve. The list is sorted, because that
    /// is the order the whitelist parse below returns, and the parity that matters is set membership.
    /// </summary>
    private static readonly string[] ExpectedUiAssets =
    [
        "app.mjs", "dispatchPanel.mjs", "feed.mjs", "global.css", "opsPanel.mjs",
        "sensorDetail.mjs", "thermal.mjs", "wallMap.mjs",
    ];

    /// <summary>Every property of the shell liveness body, and nothing else.</summary>
    private static readonly string[] ExpectedHealthFields =
    [
        "Status", "Host", "DeviceProfile", "AcquisitionMode", "Ready", "CurrentRevision", "StageMarker",
    ];

    /// <summary>The routes the presentation may request, by contract identity.</summary>
    private static readonly string[] RequestableRoutes =
    [
        ApiRoutes.Snapshot, ApiRoutes.Deltas, ApiRoutes.Runtime, ApiRoutes.DispatchWorkspace,
        ApiRoutes.KioskHealth,
    ];

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaterJetSentinelSuite.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string ProgramSource => Read("apps/runtime/Program.cs");

    [Fact]
    public void Presentation_And_Shell_Probe_Route_Identities_Are_Pinned()
    {
        // The document is served at the origin root because the shell navigates to the approved
        // origin itself; the asset route is a single named parameter; the probe route is the one the
        // shell's HealthUri resolves against that origin.
        Assert.Equal("/", ApiRoutes.Ui);
        Assert.Equal("/ui/{file}", ApiRoutes.UiAsset);
        Assert.Equal("/api/health", ApiRoutes.KioskHealth);

        Assert.DoesNotContain("*", ApiRoutes.UiAsset, StringComparison.Ordinal);
        Assert.DoesNotContain("{*file}", ApiRoutes.UiAsset, StringComparison.Ordinal);
        Assert.DoesNotContain("index.html", ApiRoutes.UiAsset, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Asset_Handler_Binds_The_Route_Token_By_Name_And_Refuses_Before_Composing_A_Path()
    {
        var source = ProgramSource;

        // A minimal-API handler parameter binds from a route value only when its name matches the
        // token; a different name would silently bind a query string instead and the asset route
        // would never resolve. This is the single most load-bearing line of the hosting slice.
        Assert.Contains("IResult UiAssetEndpoint(string file)", source, StringComparison.Ordinal);

        var refusal = source.IndexOf("!uiAssets.Contains(file)", StringComparison.Ordinal);
        var composition = source.IndexOf("Path.Combine(uiDirectory, file)", StringComparison.Ordinal);
        Assert.True(refusal >= 0, "the handler must test the frozen whitelist first");
        Assert.True(composition >= 0, "the handler must compose the path from the whitelisted name");
        Assert.True(refusal < composition,
            "an unmatched name is answered with 404 before any path is composed, so no request ever contributes a path segment");

        // The content type comes from the extension of an allow-listed name, never from the request.
        Assert.Contains("uiContentTypes.TryGetValue(Path.GetExtension(file), out var contentType)", source, StringComparison.Ordinal);

        // A wildcard or middleware-backed file surface would serve more than the frozen set.
        foreach (var forbidden in new[] { "UseStaticFiles", "UseDefaultFiles", "MapFallback", "StaticFileOptions", "PhysicalFileProvider" })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Asset_Whitelist_Is_Exactly_The_Shipped_Presentation_Files()
    {
        var source = ProgramSource;
        var declared = ParseWhitelist(source);

        Assert.Equal(ExpectedUiAssets, declared);

        var published = Directory
            .EnumerateFiles(Path.Combine(RepoRoot(), "apps", "ui", "public"), "*", SearchOption.TopDirectoryOnly)
            .Select(file => Path.GetFileName(file))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // The document is served by its own route, so it is absent from the whitelist on purpose.
        Assert.Contains("index.html", published, StringComparer.Ordinal);
        var modules = published.Where(name => !string.Equals(name, "index.html", StringComparison.Ordinal)).ToArray();
        Assert.Equal(declared, modules);
    }

    [Fact]
    public void The_Document_Boots_From_One_Module_Element_That_The_Host_Serves()
    {
        var document = Read("apps/ui/public/index.html");

        var scriptTags = System.Text.RegularExpressions.Regex.Matches(document, "<script\\b[^>]*>");
        Assert.Equal(1, scriptTags.Count);
        var script = scriptTags[0].Value;
        Assert.Contains("type=\"module\"", script, StringComparison.Ordinal);
        Assert.Contains("src=\"/ui/app.mjs\"", script, StringComparison.Ordinal);

        // An inline script would make a restrictive content policy impossible, and a stylesheet that
        // is not served by the host would make the presentation depend on another process. The single
        // script element above carries a src, so a script without one is by definition an inline one.
        Assert.DoesNotContain("<style", document, StringComparison.Ordinal);
        Assert.Equal(0, System.Text.RegularExpressions.Regex.Matches(document, "<script\\b(?![^>]*\\bsrc=)").Count);
        Assert.Contains("href=\"/ui/global.css\"", document, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Presentation_Requests_Only_Routes_This_Host_Registers()
    {
        var feed = Read("apps/ui/public/feed.mjs");
        var matches = System.Text.RegularExpressions.Regex.Matches(
            feed,
            "^export const [A-Z_]+ = '(?<path>/[a-z0-9/._-]+)';$",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        var requested = new string[matches.Count];
        for (var index = 0; index < matches.Count; index++)
        {
            requested[index] = matches[index].Groups["path"].Value;
        }

        Assert.NotEmpty(requested);
        foreach (var path in requested)
        {
            Assert.Contains(path, RequestableRoutes, StringComparer.Ordinal);
        }

        // The request surface is the read-only set: the snapshot, the bounded feed, the status, the
        // workspace document and the liveness answer. Nothing else is reachable from the browser.
        Assert.Equal(
            RequestableRoutes.OrderBy(route => route, StringComparer.Ordinal).ToArray(),
            requested.OrderBy(route => route, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void The_Host_Project_Copies_The_Presentation_Without_Adding_A_Dependency()
    {
        var project = Read("apps/runtime/Wjss.Runtime.csproj");

        Assert.Contains(
            "<Content Include=\"..\\..\\ui\\public\\**\\*.*\" LinkBase=\"Ui\" CopyToOutputDirectory=\"PreserveNewest\" />",
            project,
            StringComparison.Ordinal);

        // No package is introduced by this slice, so the restore lock file is untouched and a locked
        // restore stays honest: the UI ships as content, and content carries no dependency metadata.
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.NET.Sdk.StaticWebAssets", project, StringComparison.Ordinal);
        Assert.DoesNotContain("node_modules", project, StringComparison.Ordinal);
        Assert.DoesNotContain("npm", project, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Shell_Liveness_Body_Carries_Observation_Fields_And_No_Control_Handle()
    {
        var properties = typeof(KioskHealthPayload)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedHealthFields.OrderBy(name => name, StringComparer.Ordinal), properties);

        Assert.Equal(typeof(string), typeof(KioskHealthPayload).GetProperty("Status")!.PropertyType);
        Assert.Equal(typeof(string), typeof(KioskHealthPayload).GetProperty("AcquisitionMode")!.PropertyType);
        Assert.Equal(typeof(bool), typeof(KioskHealthPayload).GetProperty("Ready")!.PropertyType);
        Assert.Equal(typeof(int?), typeof(KioskHealthPayload).GetProperty("CurrentRevision")!.PropertyType);

        // A public field would be a handle rather than a report, and a public method other than an
        // accessor or the record's own equality members would be a surface to call.
        Assert.Empty(typeof(KioskHealthPayload).GetFields(BindingFlags.Public | BindingFlags.Instance));
        foreach (var method in typeof(KioskHealthPayload)
                 .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var allowed = method.Name.StartsWith("get_", StringComparison.Ordinal)
                || method.Name.StartsWith("set_", StringComparison.Ordinal)
                || method.Name.StartsWith("Equals", StringComparison.Ordinal);
            Assert.True(allowed, method.Name + " is not an accessor or the record equality member");
        }

        foreach (var property in typeof(KioskHealthPayload).GetProperties())
        {
            Assert.NotNull(property.GetMethod);
        }
    }

    [Fact]
    public void The_Liveness_Route_Answers_Independently_Of_Readiness()
    {
        var source = ProgramSource;

        // The 200 of the probe route is not conditioned on readiness: the shell must be able to
        // distinguish "the host answers" from "the host has an authoritative Snapshot", and only
        // /health/ready carries the readiness verdict.
        var probe = source.IndexOf("app.MapGet(ApiRoutes.KioskHealth", StringComparison.Ordinal);
        Assert.True(probe >= 0, "the shell probe route must be registered");
        var body = source[probe..(source.IndexOf("));", probe, StringComparison.Ordinal) + 2]);
        Assert.DoesNotContain("statusCode:", body, StringComparison.Ordinal);
        Assert.Contains("Status = \"ALIVE\"", body, StringComparison.Ordinal);
        Assert.Contains("TcEndpointConfiguration.DescribeMode(options.AcquisitionMode)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the names out of the <c>uiAssets</c> initializer in the host source. The parse is strict
    /// on purpose: a whitelist that is no longer a literal set of quoted names is a hosting change
    /// this file has to hear about.
    /// </summary>
    private static string[] ParseWhitelist(string source)
    {
        const string marker = "var uiAssets = new HashSet<string>(StringComparer.Ordinal)";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "the asset whitelist must stay a frozen literal set");
        var open = source.IndexOf('{', start);
        var close = source.IndexOf("};", open, StringComparison.Ordinal);
        Assert.True(open >= 0 && close > open, "the asset whitelist literal is malformed");

        return System.Text.RegularExpressions.Regex
            .Matches(source[open..close], "\"(?<name>[^\"]+)\"")
            .Select(match => match.Groups["name"].Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
