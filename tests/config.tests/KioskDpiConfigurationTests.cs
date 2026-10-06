namespace Wjss.Config.Examples.Tests;

/// <summary>
/// Static source assertions for the WFO0003 correction (Owner-local build of
/// 2026-10-07): WinForms DPI must be configured through the project property,
/// never through app.manifest, and the entry point must be the source-
/// generated ApplicationConfiguration bootstrap. These tests read the files
/// directly - they compile and run on any OS, no Windows APIs involved.
/// </summary>
public sealed class KioskDpiConfigurationTests
{
    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(ExampleConfigTests.RepoRoot()!, relative.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void Manifest_Contains_No_Dpi_Elements()
    {
        var manifest = Read("apps/kiosk/app.manifest");
        Assert.DoesNotContain("dpiAware", manifest, StringComparison.Ordinal); // covers dpiAwareness too
        Assert.DoesNotContain("windowsSettings", manifest, StringComparison.Ordinal);
        // The Windows compatibility declaration must survive the DPI removal:
        Assert.Contains("supportedOS", manifest, StringComparison.Ordinal);
        Assert.Contains("compatibility", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Kiosk_Csproj_Declares_PerMonitorV2_Exactly_Once()
    {
        var csproj = Read("apps/kiosk/Wjss.Kiosk.csproj");
        Assert.Equal(1, CountOccurrences(csproj, "<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>"));
    }

    [Fact]
    public void Program_Uses_ApplicationConfiguration_Before_Run()
    {
        var program = Read("apps/kiosk/Program.cs");
        var init = program.IndexOf("ApplicationConfiguration.Initialize()", StringComparison.Ordinal);
        var run = program.IndexOf("Application.Run(new MainForm())", StringComparison.Ordinal);
        Assert.True(init >= 0, "ApplicationConfiguration.Initialize() call missing");
        Assert.True(run >= 0, "Application.Run(new MainForm()) call missing");
        Assert.True(init < run, "Initialize() must precede Run()");
        // The project property is the single DPI source; no separate call.
        Assert.DoesNotContain("SetHighDpiMode", program, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
