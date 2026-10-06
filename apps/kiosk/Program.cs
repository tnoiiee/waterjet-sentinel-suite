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
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new MainForm());
    }
}
