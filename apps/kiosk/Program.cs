namespace Wjss.Kiosk;

/// <summary>
/// Entry point of the application-owned shell (Stage 0.4C-K2).
///
/// The shell owns exactly one thing: a guarded window onto the approved local preview route. It
/// starts no other process, reads no configuration, keeps no credential and holds no control
/// authority over any device. The Operator starts the local preview host separately; see
/// apps/kiosk/README.md for the Owner-local runbook.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // WinForms source-generated bootstrap: applies the csproj-level application configuration
        // (visual styles, text rendering and ApplicationHighDpiMode=PerMonitorV2 - the WFO0003
        // correction), then runs the shell window.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
