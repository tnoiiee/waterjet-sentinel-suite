namespace Wjss.Kiosk;

/// <summary>
/// The application-owned top-level window, compile-only skeleton (Stage 0.3A-1).
///
/// Real behaviour arrives in 0.3A-5/0.3A-6 (NOT AUTHORIZED in this
/// checkpoint): WebView2 attachment to the loopback origin, navigation and
/// new-window policy, DevTools/context-menu policy, user-data folder,
/// Runtime-unavailable state, process-failure recovery, reconnect, and the
/// close guard (operational usability control, never a safety protection).
/// The shell holds NO Production control authority and never commands a device.
/// </summary>
public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "WaterJet Sentinel Suite - Kiosk Shell Skeleton (Stage 0.3A-1)";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        BackColor = Color.FromArgb(13, 17, 23);
        DoubleBuffered = true;
        MinimumSize = new Size(1024, 600);

        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(226, 232, 240),
            Font = new Font("Segoe UI", 13.5F, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleCenter,
            Text =
                "WJSS application-owned shell skeleton.\r\n" +
                "WebView2, loopback origin attach, close guard and Runtime\r\n" +
                "reconnect are wired in Stage 0.3A-5/0.3A-6 (not authorized yet).\r\n" +
                "SIMULATOR profile only. No Production control authority.",
        });
    }
}
