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
/// name) are exercised against the BUILT host by <c>tools/launch/Start-WjssOperatorKiosk.ps1
/// -Probe</c>, not in process: reaching a live listener from a test needs a test-only package
/// (Microsoft.AspNetCore.Mvc.Testing), and this repository pins its dependency set centrally with a
/// locked restore, so no such package may be added here. What these facts do instead is refuse every
/// shape that could make those answers wrong before the host is ever started - the content item must
/// resolve to files that exist, the build must fail if the copy did not happen, and an output that
/// exists must carry the presentation byte for byte.
///
/// That last pair is not decoration. The first revision of this slice carried a Content glob that
/// walked one directory too far up, MSBuild matched no file against an empty glob, the build stayed
/// green, and the live result was 500 on the document and 404 on every module. A source-shape pin
/// that repeats the author's path cannot see that; a resolved path and a copied directory can.
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

    /// <summary>The allow-listed assets plus the document: everything the presentation ships.</summary>
    private static readonly string[] ShippedPresentationFileNames =
        ["app.mjs", "dispatchPanel.mjs", "feed.mjs", "global.css", "index.html", "opsPanel.mjs",
            "sensorDetail.mjs", "thermal.mjs", "wallMap.mjs"];

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

    /// <summary>
    /// The host's build output directory, or null when the host has not been built in this checkout.
    /// Release is preferred because that is the configuration every Owner gate uses.
    /// </summary>
    private static string? HostBuildOutput()
    {
        var root = Path.Combine(RepoRoot(), "apps", "runtime", "bin");
        foreach (var configuration in new[] { "Release", "Debug" })
        {
            var candidate = Path.Combine(root, configuration, "net10.0");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The value of the single Content item that carries the presentation.</summary>
    private static string ContentItemInclude()
    {
        var project = Read("apps/runtime/Wjss.Runtime.csproj");
        var match = System.Text.RegularExpressions.Regex.Match(
            project,
            "<Content Include=\"(?<value>[^\"]+)\" Link=\"Ui\\\\%\\(RecursiveDir\\)%\\(Filename\\)%\\(Extension\\)\" CopyToOutputDirectory=\"PreserveNewest\" CopyToPublishDirectory=\"PreserveNewest\" />");
        Assert.True(match.Success, "the presentation must be carried by exactly one Content item with explicit Link and publish metadata");
        return match.Groups["value"].Value;
    }

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

        // The metadata is asserted as properties of the one item the helper above locates, so the item
        // cannot be renamed, duplicated or moved into a different mechanism unnoticed. Link with the
        // RecursiveDir and Filename item functions is what keeps the copied tree flat for a build and
        // for a publish alike, which a LinkBase on a project outside the cone does not guarantee.
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(project, "<Content Include=\"[^\"]*\" Link=\"Ui"));
        Assert.Contains(ContentItemInclude(), project, StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory=\"PreserveNewest\"", project, StringComparison.Ordinal);

        // No package is introduced by this slice, so the restore lock file is untouched and a locked
        // restore stays honest: the UI ships as content, and content carries no dependency metadata.
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.NET.Sdk.StaticWebAssets", project, StringComparison.Ordinal);
        Assert.DoesNotContain("node_modules", project, StringComparison.Ordinal);
        Assert.DoesNotContain("npm", project, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Content_Glob_Resolves_To_Files_That_Exist_After_One_Directory_Up()
    {
        var include = ContentItemInclude();

        // This is the exact fact the first revision got wrong: a project file in apps/runtime is two
        // directories below the repository root, so one level up is apps, and an item that walks up
        // twice points at a directory that does not exist. An unmatched glob is not an MSBuild error,
        // so nothing but this resolution catches it.
        Assert.StartsWith("..", include, StringComparison.Ordinal);
        Assert.False(
            include.Replace('\\', '/').StartsWith("../../", StringComparison.Ordinal),
            "the presentation lives at apps/ui/public: from apps/runtime the include may walk up only one directory");

        var wildcard = include.IndexOf("*", StringComparison.Ordinal);
        Assert.True(wildcard > 0, "the item is a glob over a directory, not a single file");
        // The item value is written with MSBuild separators; normalising them keeps the resolution
        // honest on a build host where the backslash is not the directory separator.
        var directory = include[..wildcard].TrimEnd('\\', '/').Replace('\\', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(RepoRoot(), "apps", "runtime", directory));

        // Compared case-insensitively because the build host's file system is: the fact is about the
        // directory the copy reads, not about the spelling of its name.
        Assert.True(
            string.Equals(
                Path.GetFullPath(Path.Combine(RepoRoot(), "apps", "ui", "public")),
                resolved,
                StringComparison.OrdinalIgnoreCase),
            "the content item must resolve to apps/ui/public, and it resolves to " + resolved);
        Assert.True(Directory.Exists(resolved), "the glob must resolve to a real directory: " + resolved);

        Assert.True(File.Exists(Path.Combine(resolved, "index.html")), "the document must exist where the item points");
        foreach (var asset in ExpectedUiAssets)
        {
            Assert.True(File.Exists(Path.Combine(resolved, asset)), "the allow-listed asset is not at the resolved path: " + asset);
        }
    }

    [Fact]
    public void An_Empty_Copy_Fails_The_Build_Instead_Of_Serving_500_And_404()
    {
        var project = Read("apps/runtime/Wjss.Runtime.csproj");
        var start = project.IndexOf("<Target Name=\"VerifyOperatorUiOutput\" AfterTargets=\"CopyFilesToOutputDirectory\">", StringComparison.Ordinal);
        Assert.True(start >= 0, "the fail-fast target must run after the copy step of every build");
        var end = project.IndexOf("</Target>", start, StringComparison.Ordinal);
        Assert.True(end > start, "the fail-fast target is not closed");
        var target = project[start..end];

        // Two names are enough to prove both halves of the failure: a glob that matched nothing and a
        // copy that ran but lost the entry module. They are the document and the only module element.
        Assert.Contains("<Error Condition=", target, StringComparison.Ordinal);
        Assert.Contains("!Exists('$(WjssUiOutputRoot)Ui\\index.html')", target, StringComparison.Ordinal);
        Assert.Contains("!Exists('$(WjssUiOutputRoot)Ui\\app.mjs')", target, StringComparison.Ordinal);

        // The root is the directory the copy actually writes to, with the OutputPath convention as the
        // fallback and an explicit trailing separator, so a good build can never be failed by the guard.
        Assert.Contains("<WjssUiOutputRoot Condition=\"'$(OutDir)' != ''\">$(OutDir)</WjssUiOutputRoot>", target, StringComparison.Ordinal);
        Assert.Contains("<WjssUiOutputRoot Condition=\"'$(WjssUiOutputRoot)' == ''\">$(OutputPath)</WjssUiOutputRoot>", target, StringComparison.Ordinal);
        Assert.Contains("$([MSBuild]::EnsureTrailingSlash('$(WjssUiOutputRoot)'))", target, StringComparison.Ordinal);

        // A guard must not be able to repair the build it guards: it declares no output path of its own.
        Assert.DoesNotContain("<OutDir>", target, StringComparison.Ordinal);
        Assert.DoesNotContain("<OutputPath>", target, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Built_Host_Output_Carries_The_Whole_Presentation()
    {
        var output = HostBuildOutput();
        var ui = output is null ? null : Path.Combine(output, "Ui");

        // An output that exists must carry the presentation: no Ui directory is precisely the defect
        // this slice shipped with. No output at all is a checkout that has not been built, and then
        // this fact observes nothing rather than pretending to.
        Assert.True(ui is null || Directory.Exists(ui),
            "the host build output at " + output + " carries no Ui directory, so GET / answers 500 and every /ui/ asset 404");
        if (ui is null || !Directory.Exists(ui))
        {
            return;
        }

        var copied = Directory
            .EnumerateFiles(ui, "*.*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(ui, file).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ShippedPresentationFileNames, copied);

        // The copy is a copy: the served bytes are the authored bytes, with no transform in between.
        foreach (var name in ShippedPresentationFileNames)
        {
            var file = Path.Combine(ui, name.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(new FileInfo(file).Length > 0, "the copied presentation file is empty: " + name);
            Assert.True(
                string.Equals(Read("apps/ui/public/" + name), File.ReadAllText(file), StringComparison.Ordinal),
                "the copied presentation file differs from the authored file: " + name);
        }
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
