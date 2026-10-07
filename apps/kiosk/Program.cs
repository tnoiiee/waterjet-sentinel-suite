namespace Wjss.Kiosk;

/// <summary>
/// Entry point of the application-owned shell. Compile-only in Stage 0.3A-1:
/// no WebView2, no navigation, no Runtime process control. The shell never
/// owns control authority; it displays and guards a window only.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // WinForms source-generated bootstrap: applies the csproj-level
        // application configuration (visual styles, text rendering, and
        // ApplicationHighDpiMode=PerMonitorV2 - see the WFO0003 correction),
        // then runs the skeleton window.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
