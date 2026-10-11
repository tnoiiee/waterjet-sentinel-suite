using System.Globalization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Wjss.Kiosk;

/// <summary>
/// Stage 0.4C-K2: the application-owned Kiosk shell (originated in Stage 0.4C-K1).
///
/// This window hosts the local Operations preview route in the fixed-name embedded web-view control
/// supplied by the Owner-approved package. It presents one chromeless window with no address bar, no
/// tab strip and no browser chrome of any kind; it loads the approved loopback origin automatically;
/// it refuses every other address, every new window and every download; it disables developer tools
/// and browser accelerator keys; it shows a visible Runtime-unavailable / reconnecting state, and it
/// never presents a screen that stopped updating as current values; and it exits only through a
/// deliberate, confirmed Operator action.
///
/// It owns no Product data: it parses no state, keeps no credential, starts and stops no other
/// process, and holds NO control authority. The local preview host is started separately by the
/// Operator (see apps/kiosk/README.md); the shell only points a window at it and reports whether it
/// answers.
///
/// The close guard is an operational usability control, never a safety protection.
/// </summary>
public sealed class MainForm : Form
{
    // Established Operations palette (the same tokens as the local UI stylesheet: one visual
    // language, no second design system).
    private static readonly Color ShellBack = Color.FromArgb(13, 17, 23);
    private static readonly Color ShellBar = Color.FromArgb(22, 27, 34);
    private static readonly Color Ink = Color.FromArgb(230, 237, 243);
    private static readonly Color Muted = Color.FromArgb(154, 165, 177);
    private static readonly Color Ok = Color.FromArgb(86, 211, 100);
    private static readonly Color Warn = Color.FromArgb(227, 179, 65);
    private static readonly Color Err = Color.FromArgb(255, 123, 114);
    private static readonly Color Info = Color.FromArgb(121, 192, 255);

    /// <summary>Probe client. One instance, short timeout, no retry storm.</summary>
    private static readonly HttpClient ProbeClient = new()
    {
        Timeout = TimeSpan.FromMilliseconds(KioskShellOptions.ProbeTimeoutMs),
    };

    /// <summary>
    /// Local cache of the embedded view runtime. A runtime scratch folder (cache, profile, crash
    /// dump) - never Product control state - and safe to delete while the window is closed. Nothing
    /// else in this shell is written to disk.
    /// </summary>
    private static readonly string ViewDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WJSS",
        "KioskPreview",
        "ViewData");

    /// <summary>Padding and gaps of the shell header. Kept small so the header stays compact.</summary>
    private const int BarPadX = 16;
    private const int BarPadY = 8;
    private const int BarGapX = 20;

    /// <summary>Vertical slack added to every measured text row, so a wrapped line never clips.</summary>
    private const int RowSlackY = 4;

    private readonly WebView2 _view = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = ShellBack };
    private readonly TableLayoutPanel _statusBar = new();
    private readonly TableLayoutPanel _overlay = new();
    private readonly Label _overlayHeadline = new();
    private readonly Label _overlayDetail = new();

    // Header text rows. Every one is AutoSize=false and Dock=Fill: a label must never contribute a
    // preferred width, because that is what pushed the Exit action off the right edge of a 1920x1080
    // fullscreen client area (Owner Manual Review, 2026-10-12).
    private readonly Label _stageLabel = new();
    private readonly Label _connectionState = new();
    private readonly Label _boundaryLabel = new();
    private readonly Label _policyState = new();

    /// <summary>Action zone: the ONLY part of the header that keeps its preferred width.</summary>
    private readonly FlowLayoutPanel _actionZone = new();

    private readonly Button _exitButton = new();
    private readonly Button _windowModeButton = new();
    private readonly System.Windows.Forms.Timer _probeTimer = new();

    private bool _confirmedExit;
    private bool _hostAvailable;
    private bool _viewReady;
    private bool _viewFaulted;
    private bool _probing;
    private string _faultDetail = string.Empty;
    private int _blockedNavigations;
    private int _blockedPopups;
    private int _cancelledDownloads;
    private int _deniedPermissions;

    public MainForm()
    {
        Text = KioskShellOptions.WindowTitle;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = ShellBack;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(1024, 600);

        BuildLayout();

        _probeTimer.Interval = KioskShellOptions.ProbeIntervalMs;
        _probeTimer.Tick += OnProbeTick;

        // The header is re-measured on a monitor DPI change as well as on resize.
        DpiChanged += (_, _) => LayoutStatusBar();

        UpdateShellState();
        _probeTimer.Start();
    }

    private void BuildLayout()
    {
        BuildStatusBar();
        BuildOverlay();

        _content.Controls.Add(_view);
        _content.Controls.Add(_overlay);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ShellBack,
            Margin = Padding.Empty,
            AutoSize = false,
        };

        // The single column is EXPLICITLY bounded. TableLayoutPanel defaults an unstyled column to
        // AutoSize, which sizes the column to its children's preferred width: the header would then
        // be wider than the form, and the content cell - and with it the embedded view, and with it
        // the web page viewport - would be stretched past the screen edge. Owner Manual Review
        // defect 1 (native header) and defect 2 (clipped embedded page) share this one root cause.
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(_statusBar, 0, 0);
        layout.Controls.Add(_content, 0, 1);
        Controls.Add(layout);
    }

    /// <summary>
    /// The shell header: two columns and four rows, with explicit priority.
    ///
    ///  - Column 1 is AutoSize and holds the action zone (the two buttons) - the ONLY header content
    ///    that keeps its preferred width, so the actions can never be pushed off the right edge.
    ///  - Column 0 is the remaining width and holds the text rows; every text label is
    ///    AutoSize=false with Dock=Fill, so no text can demand width from the form.
    ///  - Row 0 carries the action zone beside the short Stage identity; the long Stage boundary,
    ///    the connection state and the approved-origin policy each get their own full-width row and
    ///    WRAP instead of competing for a single line.
    ///
    /// Row heights are measured (see LayoutStatusBar), which is what makes the result deterministic:
    /// nothing depends on WinForms AutoSize guesswork, and the header is complete at 1024x600 as
    /// well as at 1920x1080.
    /// </summary>
    private void BuildStatusBar()
    {
        _statusBar.Dock = DockStyle.Fill;
        _statusBar.AutoSize = true;
        _statusBar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _statusBar.ColumnCount = 2;
        _statusBar.RowCount = 4;
        _statusBar.BackColor = ShellBar;
        _statusBar.Padding = new Padding(BarPadX, BarPadY, BarPadX, BarPadY);
        _statusBar.Margin = Padding.Empty;
        _statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var row = 0; row < _statusBar.RowCount; row++)
        {
            _statusBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
        }

        _stageLabel.Text = "Kiosk Preview  -  Stage " + KioskShellOptions.StageId;
        _boundaryLabel.Text = KioskShellOptions.BoundaryText;
        ConfigureTextRow(_stageLabel, Ink, 10F, FontStyle.Bold);
        ConfigureTextRow(_connectionState, Ink, 9F, FontStyle.Regular);
        ConfigureTextRow(_boundaryLabel, Warn, 9.5F, FontStyle.Bold);
        ConfigureTextRow(_policyState, Muted, 9F, FontStyle.Regular);

        ConfigureButton(_windowModeButton, "Windowed / full screen", Info);
        ConfigureButton(_exitButton, "Exit Kiosk Preview", Err);
        _windowModeButton.Click += (_, _) => ToggleWindowMode();
        _exitButton.Click += (_, _) => RequestConfirmedExit();

        _actionZone.AutoSize = true;
        _actionZone.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _actionZone.WrapContents = false;
        _actionZone.FlowDirection = FlowDirection.LeftToRight;
        _actionZone.Margin = Padding.Empty;
        _actionZone.Anchor = AnchorStyles.Right;
        _actionZone.Controls.Add(_windowModeButton);
        _actionZone.Controls.Add(_exitButton);

        _statusBar.Controls.Add(_stageLabel, 0, 0);
        _statusBar.Controls.Add(_actionZone, 1, 0);
        _statusBar.Controls.Add(_connectionState, 0, 1);
        _statusBar.SetColumnSpan(_connectionState, 2);
        _statusBar.Controls.Add(_boundaryLabel, 0, 2);
        _statusBar.SetColumnSpan(_boundaryLabel, 2);
        _statusBar.Controls.Add(_policyState, 0, 3);
        _statusBar.SetColumnSpan(_policyState, 2);

        LayoutStatusBar();
    }

    /// <summary>
    /// Measures every header row for the width it actually gets and applies the heights, so the
    /// header always fits the client area and always stays complete. Re-run on resize, on DPI change
    /// and whenever a header text changes (connection state, policy counters, window-mode label).
    ///
    /// The header must stay complete and usable from the form's existing 1024x600 minimum upwards;
    /// nothing here raises that minimum, and no scrollbar is introduced.
    /// </summary>
    private void LayoutStatusBar()
    {
        if (_statusBar.IsDisposed || _statusBar.RowStyles.Count < 4)
        {
            return;
        }

        var clientWidth = ClientSize.Width;
        if (clientWidth <= 0)
        {
            return;
        }

        var inner = Math.Max(240, clientWidth - (BarPadX * 2));

        // Priority 1: the action zone keeps its preferred width, and it is reserved BEFORE any text.
        // The width is the sum of the two auto-sized buttons plus their margins - a value this code
        // owns, so nothing here depends on how a container measures a proposed size.
        var actionsWidth = _windowModeButton.PreferredSize.Width + _windowModeButton.Margin.Horizontal
            + _exitButton.PreferredSize.Width + _exitButton.Margin.Horizontal
            + _actionZone.Padding.Horizontal;
        var actionsHeight = Math.Max(
            _windowModeButton.PreferredSize.Height + _windowModeButton.Margin.Vertical,
            _exitButton.PreferredSize.Height + _exitButton.Margin.Vertical) + _actionZone.Padding.Vertical;

        // Priority 2: the identity line may use what is left (minus a safety margin), and wraps
        // inside it if it ever needs to.
        var identityWidth = Math.Max(120, inner - actionsWidth - BarGapX - 8);

        var row0 = Math.Max(MeasureTextRow(_stageLabel.Text, _stageLabel.Font, identityWidth), actionsHeight);
        var row1 = MeasureTextRow(_connectionState.Text, _connectionState.Font, inner);
        var row2 = MeasureTextRow(_boundaryLabel.Text, _boundaryLabel.Font, inner);
        var row3 = MeasureTextRow(_policyState.Text, _policyState.Font, inner);

        SetRowHeight(0, row0);
        SetRowHeight(1, row1);
        SetRowHeight(2, row2);
        SetRowHeight(3, row3);

        _statusBar.PerformLayout();
    }

    private void SetRowHeight(int row, int height)
    {
        var style = _statusBar.RowStyles[row];
        style.SizeType = SizeType.Absolute;
        style.Height = Math.Max(1F, height);
    }

    /// <summary>
    /// Wrapped height of one text block inside the given width, plus a small vertical slack. Uses
    /// TextRenderer directly so the header does not depend on any control's own auto-sizing rules.
    /// </summary>
    private static int MeasureTextRow(string? text, Font font, int width)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var proposed = new Size(Math.Max(1, width), int.MaxValue);
        var measured = TextRenderer.MeasureText(
            text,
            font,
            proposed,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        return measured.Height + RowSlackY;
    }

    private void BuildOverlay()
    {
        _overlay.Dock = DockStyle.Fill;
        _overlay.ColumnCount = 1;
        _overlay.RowCount = 4;
        _overlay.BackColor = ShellBack;
        _overlay.Margin = Padding.Empty;
        _overlay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _overlay.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _overlay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _overlay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _overlay.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        ConfigureChrome(_overlayHeadline, Warn, 20F, FontStyle.Bold);
        _overlayHeadline.TextAlign = ContentAlignment.MiddleCenter;
        _overlayHeadline.Dock = DockStyle.Fill;
        _overlayHeadline.Padding = new Padding(80, 0, 80, 0);

        ConfigureChrome(_overlayDetail, Muted, 11F, FontStyle.Regular);
        _overlayDetail.TextAlign = ContentAlignment.MiddleCenter;
        _overlayDetail.Dock = DockStyle.Fill;
        _overlayDetail.Padding = new Padding(80, 14, 80, 0);

        _overlay.Controls.Add(_overlayHeadline, 0, 1);
        _overlay.Controls.Add(_overlayDetail, 0, 2);
    }

    private static void ConfigureChrome(Label label, Color colour, float size, FontStyle style)
    {
        label.ForeColor = colour;
        label.Font = new Font("Segoe UI", size, style, GraphicsUnit.Point);
        label.AutoSize = true;
        label.Margin = new Padding(0, 4, 24, 4);
    }

    /// <summary>
    /// A header text row. AutoSize is FALSE on purpose: the label then contributes no preferred
    /// width to the layout, it wraps inside the width it is given, and the row height is set from the
    /// measurement in LayoutStatusBar. This is what keeps the Exit action inside the client area at
    /// 1920x1080 and at the 1024x600 minimum.
    /// </summary>
    private static void ConfigureTextRow(Label label, Color colour, float size, FontStyle style)
    {
        label.AutoSize = false;
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.ForeColor = colour;
        label.Font = new Font("Segoe UI", size, style, GraphicsUnit.Point);
        label.Margin = new Padding(0, 0, 0, 2);
    }

    private static void ConfigureButton(Button button, string text, Color colour)
    {
        button.Text = text;
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = colour;
        button.ForeColor = colour;
        button.BackColor = ShellBar;
        button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
        button.Margin = new Padding(8, 2, 0, 2);
        button.Padding = new Padding(10, 2, 10, 2);
        button.TabStop = true;
    }

    /// <summary>Borderless full-screen presentation, with a normal resizable window available.</summary>
    private void ToggleWindowMode()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1600, 900);
            CenterToScreen();
            _windowModeButton.Text = "Full screen / windowed";
        }
        else
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _windowModeButton.Text = "Windowed / full screen";
        }

        // The toggle label is part of the action zone, so its width is re-measured here.
        LayoutStatusBar();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutStatusBar();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await InitialiseViewAsync();
    }

    private async Task InitialiseViewAsync()
    {
        try
        {
            Directory.CreateDirectory(ViewDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(null, ViewDataFolder, null);
            await _view.EnsureCoreWebView2Async(environment);

            var core = _view.CoreWebView2
                ?? throw new InvalidOperationException("The embedded view runtime reported no core instance.");

            // No developer tools and no browser chrome in the Operator presentation.
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.IsBuiltInErrorPageEnabled = false;

            core.NavigationStarting += OnNavigationStarting;
            core.NavigationCompleted += OnNavigationCompleted;
            core.NewWindowRequested += OnNewWindowRequested;
            core.PermissionRequested += OnPermissionRequested;
            core.DownloadStarting += OnDownloadStarting;
            core.ProcessFailed += OnProcessFailed;

            // 100% scaling: the layout is read at 1920x1080 with 100% display scaling. The window is
            // DPI-correct through PerMonitorV2 (project property) and the view is never zoomed.
            _view.ZoomFactor = 1.0;

            _viewReady = true;
            _view.Source = KioskShellOptions.ApprovedOrigin;
        }
        catch (Exception ex)
        {
            _viewReady = false;
            _viewFaulted = true;
            _faultDetail = string.Concat(
                "The local preview view could not be started: ",
                ex.Message,
                "\n\nThe Evergreen web-view runtime is an Owner-local prerequisite. See apps/kiosk/README.md.");
        }

        UpdateShellState();
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var target = ParseUri(e.Uri);
        if (KioskShellOptions.IsApprovedOrigin(target))
        {
            return;
        }

        // The only permitted destination is the approved local origin. A local file, another host,
        // another port or another scheme is cancelled and counted.
        e.Cancel = true;
        _blockedNavigations += 1;
        UpdatePolicyState();
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _viewFaulted = false;
            _faultDetail = string.Empty;
        }
        else
        {
            _viewFaulted = true;
            _faultDetail = string.Concat(
                "The local preview route did not finish loading (",
                e.WebErrorStatus.ToString(),
                ").\n\nThe shell keeps probing the local host and reloads the approved route as soon as the host answers again.");
        }

        UpdateShellState();
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // No second window, ever: a popup cannot escape the guard.
        e.Handled = true;
        _blockedPopups += 1;
        UpdatePolicyState();
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        e.State = CoreWebView2PermissionState.Deny;
        _deniedPermissions += 1;
        UpdatePolicyState();
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        e.Cancel = true;
        _cancelledDownloads += 1;
        UpdatePolicyState();
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _viewFaulted = true;
        _faultDetail = string.Concat(
            "The local preview view stopped and is being restarted (",
            e.ProcessFailedKind.ToString(),
            ").\n\nNothing is displayed until the route loads again: a screen that stopped updating is never presented as current values.");
        UpdateShellState();
    }

    private async void OnProbeTick(object? sender, EventArgs e)
    {
        await ProbeLocalHostAsync();
    }

    private async Task ProbeLocalHostAsync()
    {
        if (_probing)
        {
            return;
        }

        _probing = true;
        var healthy = false;
        try
        {
            using var response = await ProbeClient.GetAsync(KioskShellOptions.HealthUri);
            healthy = response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            healthy = false;
        }

        _probing = false;

        if (healthy == _hostAvailable)
        {
            return;
        }

        var recovered = healthy;
        _hostAvailable = healthy;

        if (recovered && _viewReady)
        {
            try
            {
                _view.CoreWebView2?.Reload();
            }
            catch (Exception)
            {
                _viewReady = false;
            }
        }

        UpdateShellState();
    }

    /// <summary>
    /// The single place that decides what the Operator sees. The content view is shown only when the
    /// host answers, the view is ready and the route did not fault; in every other case the shell
    /// shows its own state screen, so a frame that stopped updating is never left on screen as if it
    /// were a current value.
    /// </summary>
    private void UpdateShellState()
    {
        var showView = _viewReady && _hostAvailable && !_viewFaulted;

        _view.Visible = showView;
        _overlay.Visible = !showView;

        if (_hostAvailable)
        {
            _connectionState.Text = "LOCAL HOST: CONNECTED (READ-ONLY PREVIEW)";
            _connectionState.ForeColor = Ok;
        }
        else
        {
            _connectionState.Text = "LOCAL HOST: UNAVAILABLE - RECONNECTING";
            _connectionState.ForeColor = Warn;
        }

        if (!showView)
        {
            if (_viewFaulted)
            {
                _overlayHeadline.Text = "LOCAL VIEW INTERRUPTED - RECONNECTING";
                _overlayHeadline.ForeColor = Err;
                _overlayDetail.Text = _faultDetail;
            }
            else if (!_hostAvailable)
            {
                _overlayHeadline.Text = "LOCAL PREVIEW HOST UNAVAILABLE - RECONNECTING";
                _overlayHeadline.ForeColor = Warn;
                _overlayDetail.Text = string.Concat(
                    "Nothing is shown while the host is unavailable: a screen that stopped updating is never presented as current values.\n",
                    "The shell keeps probing the local health route and reconnects by itself.\n",
                    "The host is started separately by the Operator (see apps/kiosk/README.md). This window never starts or stops it.");
            }
            else
            {
                _overlayHeadline.Text = "STARTING THE LOCAL PREVIEW";
                _overlayHeadline.ForeColor = Info;
                _overlayDetail.Text = "Waiting for the approved local route and for the embedded view to report ready.";
            }
        }

        UpdatePolicyState();
    }

    private void UpdatePolicyState()
    {
        _policyState.Text = string.Format(
            CultureInfo.InvariantCulture,
            "APPROVED ORIGIN ONLY  |  blocked: {0} navigation, {1} popup, {2} download, {3} permission",
            _blockedNavigations,
            _blockedPopups,
            _cancelledDownloads,
            _deniedPermissions);

        // The counters are part of the header text, so the header is re-measured when they change.
        LayoutStatusBar();
    }

    private static Uri? ParseUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // A deliberate, confirmed exit only. An operating-system shutdown or session end is not
        // blocked: the close guard is a usability control and never a safety protection.
        if (e.CloseReason == CloseReason.UserClosing && !_confirmedExit)
        {
            e.Cancel = true;
            RequestConfirmedExit();
            return;
        }

        base.OnFormClosing(e);
    }

    /// <summary>
    /// The one intentional exit path: a visible button (or the normal window close) plus a
    /// confirmation. No credential, no hidden keystroke, nothing irreversible - exiting closes this
    /// window and nothing else: it decommissions nothing, stops no process and writes no record.
    /// </summary>
    private void RequestConfirmedExit()
    {
        var answer = MessageBox.Show(
            this,
            string.Concat(
                "Exit the Kiosk Preview window?\n\n",
                "The window closes. The local preview host is a separate process and is not stopped by this action. ",
                "No device is connected, nothing is written to a device, and no field verification is recorded."),
            "Exit Kiosk Preview",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        _confirmedExit = true;
        Close();
    }
}
