using System.Globalization;
using Wjss.Runtime.Core.Motion.Manual;

namespace Wjss.MotionCommissioning;

/// <summary>
/// The Owner-local Manual Motion Commissioning window for the WSB1 axes A (Horizontal), B
/// (Vertical), C (Horizontal) and D (Vertical), shown as four independent axis panels in a 2x2 grid.
/// The Operator wording is role-first (Horizontal / Vertical); the controller axis letter and the
/// canonical WSB / Water Jet identity of each axis are diagnostic context from the snapshot only.
///
/// The window owns no motion lifecycle. Every control posts one typed request to the Application
/// session, and every displayed value comes from the session snapshot. There is no raw command entry,
/// no generic output number and no command construction here.
///
/// Hold-to-run Jog: MouseDown starts a press on its axis; MouseUp, lost pointer capture and window
/// deactivation end it; while a press is held a heartbeat is posted at the refresh cadence for every
/// active press, and the session stops each Jog on its own axis when its heartbeat expires or its
/// maximum hold duration is reached. Closing the window requests the session close (every active
/// motion operation is stopped first, then the port is invalidated, closed and unloaded) before the
/// window goes away.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build and review are the validation of record).
/// </summary>
internal sealed class ManualMotionForm : Form
{
    private const int RefreshIntervalMilliseconds = 100;
    private const int CloseWaitSeconds = 30;
    private const int MaximumLogItems = 500;

    private static readonly Color SimulatedBanner = Color.FromArgb(20, 70, 125);
    private static readonly Color HardwareBanner = Color.FromArgb(160, 82, 0);

    private readonly ManualMotionSession _session;
    private readonly ManualMotionSessionRunner _runner;
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly Label _modeBanner = new();
    private readonly Label _sessionLine = new();
    private readonly Label _operationLine = new();
    private readonly ListBox _logList = new();
    private readonly AxisControls _axisA = new(ManualMotionAxis.A);
    private readonly AxisControls _axisB = new(ManualMotionAxis.B);
    private readonly AxisControls _axisC = new(ManualMotionAxis.C);
    private readonly AxisControls _axisD = new(ManualMotionAxis.D);
    private readonly BufferedTableLayoutPanel _axesLayout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };

    // Hold-to-run press state is per axis: at most one active press per axis, heartbeats are posted
    // for every active press each tick, and a release (or capture loss) ends only its own axis.
    private readonly ActivePress?[] _activePresses = new ActivePress?[4];
    private long _pressSequence;
    private long _lastLogSequence;
    private bool _closeRequested;
    private bool _closeComplete;
    private DateTimeOffset _closeRequestedAt;

    // Interactive-resize presentation coalescing: while the Operator drags the window frame (move
    // or resize), ticks retain only the latest snapshot here and presentation renders it once when
    // the drag ends. This never touches the heartbeat above, any session request, or the close
    // path; the session runner keeps stepping throughout.
    private bool _interactiveResize;
    private ManualMotionSnapshot? _pendingSnapshot;

    internal ManualMotionForm(ManualMotionSession session, ManualMotionSessionRunner runner)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));

        // Buffer this window's own painting; the layout containers and state grids buffer theirs.
        DoubleBuffered = true;

        BuildLayout();
        WireAxis(_axisA);
        WireAxis(_axisB);
        WireAxis(_axisC);
        WireAxis(_axisD);

        // The size/move drag loop raises ResizeBegin/ResizeEnd for both moving and resizing.
        ResizeBegin += (_, _) => _interactiveResize = true;
        ResizeEnd += (_, _) => OnInteractiveResizeEnd();
        Deactivate += (_, _) => EndAllActivePresses(ManualMotionJogEndReason.Deactivated);
        _refreshTimer.Interval = RefreshIntervalMilliseconds;
        _refreshTimer.Tick += (_, _) => OnRefreshTick();
        _refreshTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!_closeComplete)
        {
            e.Cancel = true;
            if (!_closeRequested)
            {
                _closeRequested = true;
                _closeRequestedAt = DateTimeOffset.UtcNow;
                EndAllActivePresses(ManualMotionJogEndReason.Released);
                _session.RequestClose();
                _operationLine.Text = "Closing: stopping any active motion, then invalidating, closing and unloading the port...";
            }
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var simulated = _session.Mode == ManualMotionPortMode.Simulated;
        Text = simulated
            ? "WJSS Manual Motion Commissioning - SIMULATED"
            : "WJSS Manual Motion Commissioning - HARDWARE-CAPABLE (WSB1)";
        StartPosition = FormStartPosition.CenterScreen;

        // Minimum usable size for the four-axis 2x2 layout: each of the two panel rows must still
        // fit the identity line, the command rows and the 14-row state grid with every grid row at
        // least one full text line high, and each panel column must keep the field, value and
        // freshness cells readable. Below this size the previous 1100x760 floor (sized for the
        // former single panel row) collapsed grid rows and corrupted the text during resizing.
        MinimumSize = new Size(1240, 920);
        Size = new Size(1360, 980);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        _modeBanner.Dock = DockStyle.Fill;
        _modeBanner.TextAlign = ContentAlignment.MiddleLeft;
        _modeBanner.ForeColor = Color.White;
        _modeBanner.BackColor = simulated ? SimulatedBanner : HardwareBanner;
        _modeBanner.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
        _modeBanner.Padding = new Padding(10, 0, 10, 0);
        _modeBanner.Text = simulated
            ? "SIMULATED MODE - deterministic in-process simulation. No controller, no native library, no network, no output. Results are NOT hardware evidence."
            : "HARDWARE-CAPABLE MODE - WSB1 controller session (Owner-local). Use only under the separate hardware commissioning Scope Gate. Stop dispatch is never a confirmed stop.";

        _sessionLine.Dock = DockStyle.Fill;
        _sessionLine.AutoEllipsis = true;
        _sessionLine.TextAlign = ContentAlignment.MiddleLeft;
        _operationLine.Dock = DockStyle.Fill;
        _operationLine.AutoEllipsis = true;
        _operationLine.TextAlign = ContentAlignment.MiddleLeft;

        _axesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _axesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _axesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _axesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _axesLayout.Controls.Add(_axisA.Group, 0, 0);
        _axesLayout.Controls.Add(_axisB.Group, 1, 0);
        _axesLayout.Controls.Add(_axisC.Group, 0, 1);
        _axesLayout.Controls.Add(_axisD.Group, 1, 1);

        _logList.Dock = DockStyle.Fill;
        _logList.IntegralHeight = false;
        _logList.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var root = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 78F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 22F));
        root.Controls.Add(_modeBanner, 0, 0);
        root.Controls.Add(_sessionLine, 0, 1);
        root.Controls.Add(_operationLine, 0, 2);
        root.Controls.Add(_axesLayout, 0, 3);
        root.Controls.Add(_logList, 0, 4);
        Controls.Add(root);
    }

    private void WireAxis(AxisControls controls)
    {
        var axis = controls.Axis;
        controls.BrakeRelease.Click += (_, _) => _session.RequestBrakeRelease(axis);
        controls.BrakeApply.Click += (_, _) => _session.RequestBrakeApply(axis);
        controls.Stop.Click += (_, _) => _session.RequestStop(axis);
        controls.Home.Click += (_, _) =>
        {
            if (Confirm(string.Concat("Start Home on ", controls.Title, "?")))
            {
                _session.RequestHome(axis);
            }
        };
        controls.DefineZero.Click += (_, _) =>
        {
            if (Confirm(string.Concat("Define position zero on ", controls.Title, "? This clears the session Home state.")))
            {
                _session.RequestDefinePositionZero(axis);
            }
        };
        controls.DefineDualZero.Click += (_, _) =>
        {
            if (Confirm(string.Concat("Define dual zero on ", controls.Title, "? Its effect stays UNVERIFIED and it clears the session Home state.")))
            {
                _session.RequestDefineDualZero(axis);
            }
        };
        controls.Move.Click += (_, _) =>
        {
            var target = decimal.ToInt32(controls.Target.Value);
            var question = string.Concat(
                "Move ",
                controls.Title,
                " to absolute target ",
                target.ToString(CultureInfo.InvariantCulture),
                " (native units)?");
            if (Confirm(question))
            {
                _session.RequestMove(axis, target);
            }
        };

        WireHoldToRun(controls.JogNegative, axis, ManualMotionJogDirection.Negative);
        WireHoldToRun(controls.JogPositive, axis, ManualMotionJogDirection.Positive);
    }

    private void WireHoldToRun(Button button, ManualMotionAxis axis, ManualMotionJogDirection direction)
    {
        button.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                BeginPress(axis, direction);
            }
        };
        button.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                EndPress(axis, ManualMotionJogEndReason.Released);
            }
        };
        button.MouseCaptureChanged += (_, _) =>
        {
            if (!button.Capture)
            {
                var released = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.None;
                EndPress(axis, released ? ManualMotionJogEndReason.Released : ManualMotionJogEndReason.CaptureLost);
            }
        };
    }

    private void BeginPress(ManualMotionAxis axis, ManualMotionJogDirection direction)
    {
        if (_closeRequested || PressFor(axis) is not null)
        {
            // At most one active press per axis; a close releases everything instead of starting.
            return;
        }

        _pressSequence++;
        _activePresses[PressIndex(axis)] = new ActivePress(axis, _pressSequence, direction);
        _session.RequestJogStart(axis, direction, _pressSequence);
    }

    private void EndPress(ManualMotionAxis axis, ManualMotionJogEndReason reason)
    {
        // A release ends only its own axis press; it never releases or stops another axis.
        var press = PressFor(axis);
        if (press is null)
        {
            return;
        }

        _activePresses[PressIndex(axis)] = null;
        _session.RequestJogRelease(press.Axis, press.PressId, reason);
    }

    private void EndAllActivePresses(ManualMotionJogEndReason reason)
    {
        // Capture loss and window deactivation end every applicable press safely.
        for (var index = 0; index < _activePresses.Length; index++)
        {
            var press = _activePresses[index];
            if (press is null)
            {
                continue;
            }

            _activePresses[index] = null;
            _session.RequestJogRelease(press.Axis, press.PressId, reason);
        }
    }

    private ActivePress? PressFor(ManualMotionAxis axis) => _activePresses[PressIndex(axis)];

    private static int PressIndex(ManualMotionAxis axis) => axis switch
    {
        ManualMotionAxis.A => 0,
        ManualMotionAxis.B => 1,
        ManualMotionAxis.C => 2,
        ManualMotionAxis.D => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "axis is outside the closed A/B/C/D domain"),
    };

    private void OnRefreshTick()
    {
        // The hold-to-run heartbeats are posted first and unconditionally for every active press:
        // presentation coalescing during an interactive resize never delays or suppresses them, and
        // each heartbeat carries its own axis and press ID so it never keeps another axis alive.
        foreach (var press in _activePresses)
        {
            if (press is not null)
            {
                _session.RequestJogHeartbeat(press.Axis, press.PressId);
            }
        }

        var snapshot = _session.Snapshot;
        if (_interactiveResize)
        {
            // Retain only the latest snapshot (never a queue, never replayed); it is rendered once
            // when the drag ends. Log entries are sequence-based, so none are lost by waiting.
            _pendingSnapshot = snapshot;
        }
        else
        {
            Render(snapshot);
        }

        if (_closeRequested && !_closeComplete)
        {
            var waited = DateTimeOffset.UtcNow - _closeRequestedAt;
            if (snapshot.IsTerminal || waited > TimeSpan.FromTicks(CloseWaitSeconds * TimeSpan.TicksPerSecond))
            {
                _closeComplete = true;
                _refreshTimer.Stop();
                _ = _runner.WaitForTermination(TimeSpan.FromTicks(2 * TimeSpan.TicksPerSecond));
                Close();
            }
        }
    }

    private void OnInteractiveResizeEnd()
    {
        _interactiveResize = false;
        if (IsDisposed)
        {
            _pendingSnapshot = null;
            return;
        }

        // One layout at the final size, then one render of the latest retained snapshot; normal
        // per-tick rendering resumes from the next tick, so no field can remain outdated.
        var snapshot = _pendingSnapshot ?? _session.Snapshot;
        _pendingSnapshot = null;
        PerformLayout();
        Render(snapshot);
    }

    private void Render(ManualMotionSnapshot snapshot)
    {
        _sessionLine.Text = string.Concat(
            "Mode: ",
            snapshot.Mode == ManualMotionPortMode.Simulated ? "SIMULATED" : "HARDWARE-CAPABLE",
            "   Session: ",
            snapshot.State.ToString(),
            "   Generation: ",
            snapshot.Generation.ToString(CultureInfo.InvariantCulture),
            "   Profile: ",
            ProfileText(snapshot),
            "   Open: ",
            Dash(snapshot.OpenCode),
            "   Close: ",
            Dash(snapshot.CloseCode));

        if (!_closeRequested)
        {
            _operationLine.Text = OperationText(snapshot.ActiveOperations);
        }

        if (snapshot.Axes.Count >= 4)
        {
            // One layout pass for the whole four-panel update instead of one per changed cell.
            // The suspension is confined to this render pass and always resumes before returning.
            _axesLayout.SuspendLayout();
            try
            {
                RenderAxis(_axisA, snapshot.Axes[0], OperationFor(snapshot, ManualMotionAxis.A), PressFor(ManualMotionAxis.A), snapshot, _closeRequested);
                RenderAxis(_axisB, snapshot.Axes[1], OperationFor(snapshot, ManualMotionAxis.B), PressFor(ManualMotionAxis.B), snapshot, _closeRequested);
                RenderAxis(_axisC, snapshot.Axes[2], OperationFor(snapshot, ManualMotionAxis.C), PressFor(ManualMotionAxis.C), snapshot, _closeRequested);
                RenderAxis(_axisD, snapshot.Axes[3], OperationFor(snapshot, ManualMotionAxis.D), PressFor(ManualMotionAxis.D), snapshot, _closeRequested);
            }
            finally
            {
                _axesLayout.ResumeLayout(performLayout: false);
            }
        }

        AppendNewLogEntries(snapshot.Log);
    }

    private static void RenderAxis(
        AxisControls controls,
        ManualMotionAxisSnapshot axis,
        ManualMotionOperationSnapshot? operation,
        ActivePress? press,
        ManualMotionSnapshot snapshot,
        bool closing)
    {
        // The title, group caption and canonical identity line are stable for the whole session:
        // assign each only when its text actually changes, never as per-tick layout churn.
        var title = string.Concat(axis.RoleLabel, " Axis - ", axis.AxisLetter);
        if (!string.Equals(controls.Title, title, StringComparison.Ordinal))
        {
            controls.Title = title;
        }

        var groupText = axis.AllowedByProfile
            ? title
            : string.Concat(title, "   (NOT ALLOWED BY PROFILE)");
        if (!string.Equals(controls.Group.Text, groupText, StringComparison.Ordinal))
        {
            controls.Group.Text = groupText;
        }

        var identity = IdentityText(axis);
        if (!string.Equals(controls.Identity.Text, identity, StringComparison.Ordinal))
        {
            controls.Identity.Text = identity;
        }

        controls.StateGrid.Display(axis);

        // Enablement is per axis: this panel is gated only by its OWN operation state, so an active
        // operation on another axis never disables these controls. Starting a second operation on a
        // busy axis stays impossible (the session refuses it); Stop remains reachable in Ready,
        // Closing and Faulted under the existing typed rules.
        var ready = snapshot.State == ManualMotionSessionState.Ready && !closing;
        var idle = operation is null;
        var allowed = axis.AllowedByProfile;
        controls.BrakeRelease.Enabled = ready && idle && allowed;
        controls.BrakeApply.Enabled = ready && idle && allowed;
        controls.Home.Enabled = ready && idle && allowed;
        controls.DefineZero.Enabled = ready && idle && allowed;
        controls.DefineDualZero.Enabled = ready && idle && allowed;
        controls.Move.Enabled = ready && idle && allowed;
        controls.Target.Enabled = ready && allowed;

        // A held press keeps only its own button enabled, so MouseUp and capture loss always arrive
        // for it; without a held press a busy axis disables both Jog buttons.
        controls.JogNegative.Enabled = ready && allowed && (idle || press is { Direction: ManualMotionJogDirection.Negative });
        controls.JogPositive.Enabled = ready && allowed && (idle || press is { Direction: ManualMotionJogDirection.Positive });
        controls.Stop.Enabled = snapshot.State is ManualMotionSessionState.Ready
            or ManualMotionSessionState.Closing
            or ManualMotionSessionState.Faulted;
    }

    private static ManualMotionOperationSnapshot? OperationFor(ManualMotionSnapshot snapshot, ManualMotionAxis axis)
    {
        foreach (var operation in snapshot.ActiveOperations)
        {
            if (operation.Axis == axis)
            {
                return operation;
            }
        }

        return null;
    }

    private void AppendNewLogEntries(IReadOnlyList<ManualMotionLogEntry> entries)
    {
        var added = false;
        foreach (var entry in entries)
        {
            if (entry.Sequence <= _lastLogSequence)
            {
                continue;
            }

            _lastLogSequence = entry.Sequence;
            _logList.Items.Add(FormatLogEntry(entry));
            added = true;
        }

        while (_logList.Items.Count > MaximumLogItems)
        {
            _logList.Items.RemoveAt(0);
        }

        if (added && _logList.Items.Count > 0)
        {
            _logList.TopIndex = _logList.Items.Count - 1;
        }
    }

    private bool Confirm(string question) =>
        MessageBox.Show(this, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
        == DialogResult.Yes;

    private static string FormatLogEntry(ManualMotionLogEntry entry) => string.Concat(
        "#",
        entry.Sequence.ToString(CultureInfo.InvariantCulture),
        "  ",
        entry.At.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
        "  ",
        entry.Axis is ManualMotionAxis axis ? string.Concat("[", axis.ToString(), "]") : "[-]",
        "  ",
        entry.Operation.ToString(),
        "  ",
        entry.Code,
        "  ",
        entry.Detail);

    private static string ProfileText(ManualMotionSnapshot snapshot)
    {
        var source = snapshot.ProfileSource switch
        {
            ManualMotionProfileSource.OwnerLocalFile => "OWNER-LOCAL FILE",
            ManualMotionProfileSource.SimulationBuiltIn => "SIMULATION BUILT-IN (synthetic; never hardware)",
            _ => "NONE",
        };

        return string.Concat(source, " / ", snapshot.ProfileCode);
    }

    private static string OperationText(IReadOnlyList<ManualMotionOperationSnapshot> operations)
    {
        // Deterministic compact header over ALL active operations: one entry per axis in stable
        // A/B/C/D order, each with its own ID, kind and phase, and StopRequested is never hidden.
        var parts = new List<string>(4);
        foreach (var axis in new[] { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D })
        {
            ManualMotionOperationSnapshot? found = null;
            foreach (var operation in operations)
            {
                if (operation.Axis == axis)
                {
                    found = operation;
                    break;
                }
            }

            parts.Add(found is null
                ? string.Concat(axis.ToString(), ": none")
                : string.Concat(
                    axis.ToString(),
                    ": #",
                    found.Id.ToString(CultureInfo.InvariantCulture),
                    " ",
                    found.Kind.ToString(),
                    " ",
                    found.Phase.ToString(),
                    found.StopRequested ? " STOP REQUESTED" : string.Empty));
        }

        return string.Concat("Active operations: ", string.Join(" | ", parts));
    }

    private static string Dash(string value) => value.Length == 0 ? "-" : value;

    private static string IdentityText(ManualMotionAxisSnapshot axis) => string.Concat(
        Dash(axis.CabinetId),
        " / ",
        Dash(axis.WaterJetId),
        "   installed ",
        Dash(axis.InstalledPosition),
        "   targets ",
        Dash(axis.TargetCoverage),
        "   brake output ",
        axis.BrakeOutputNumber > 0
            ? axis.BrakeOutputNumber.ToString(CultureInfo.InvariantCulture)
            : "none allocated");

    private sealed record ActivePress(ManualMotionAxis Axis, long PressId, ManualMotionJogDirection Direction);

    /// <summary>
    /// A layout container whose own painting is double-buffered, so interactive resizing repaints
    /// the container surface without visible tearing. Presentation only: it changes no layout
    /// semantics, request, or session behavior.
    /// </summary>
    private sealed class BufferedTableLayoutPanel : TableLayoutPanel
    {
        internal BufferedTableLayoutPanel()
        {
            DoubleBuffered = true;
        }
    }

    /// <summary>The controls of one axis panel. Not a control type itself, so it declares no designer-visible property.</summary>
    private sealed class AxisControls
    {
        internal AxisControls(ManualMotionAxis axis)
        {
            Axis = axis;
            Title = string.Concat("Axis ", axis.ToString());
            Group = new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(8), Text = Title };
            BrakeRelease = CreateButton("Brake release");
            BrakeApply = CreateButton("Brake apply");
            JogNegative = CreateHoldButton("Jog - (hold to run)");
            JogPositive = CreateHoldButton("Jog + (hold to run)");
            Stop = CreateButton("STOP");
            Stop.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            Home = CreateButton("Home");
            DefineZero = CreateButton("Define zero (DP)");
            DefineDualZero = CreateButton("Define dual zero (DE)");
            Move = CreateButton("Move absolute (PA)");
            Target = new NumericUpDown
            {
                Minimum = int.MinValue + 1,
                Maximum = int.MaxValue,
                DecimalPlaces = 0,
                Increment = 1,
                Width = 150,
                ThousandsSeparator = false,
            };
            StateGrid = new ManualMotionAxisStateGrid();

            // Diagnostic identity line: the text is composed only from snapshot fields (canonical
            // catalog projections). This window derives no topology and no Water Jet identity itself.
            Identity = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 2),
            };

            var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            commands.Controls.Add(BrakeRelease);
            commands.Controls.Add(BrakeApply);
            commands.Controls.Add(JogNegative);
            commands.Controls.Add(JogPositive);
            commands.Controls.Add(Stop);
            commands.Controls.Add(Home);
            commands.Controls.Add(DefineZero);
            commands.Controls.Add(DefineDualZero);

            var move = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            move.Controls.Add(new Label
            {
                Text = "Absolute target (native units):",
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0),
            });
            move.Controls.Add(Target);
            move.Controls.Add(Move);

            var layout = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(Identity, 0, 0);
            layout.Controls.Add(commands, 0, 1);
            layout.Controls.Add(move, 0, 2);
            layout.Controls.Add(StateGrid, 0, 3);
            Group.Controls.Add(layout);
        }

        internal ManualMotionAxis Axis { get; }

        internal string Title { get; set; }

        internal GroupBox Group { get; }

        internal Button BrakeRelease { get; }

        internal Button BrakeApply { get; }

        internal Button JogNegative { get; }

        internal Button JogPositive { get; }

        internal Button Stop { get; }

        internal Button Home { get; }

        internal Button DefineZero { get; }

        internal Button DefineDualZero { get; }

        internal Button Move { get; }

        internal NumericUpDown Target { get; }

        internal ManualMotionAxisStateGrid StateGrid { get; }

        internal Label Identity { get; }

        private static Button CreateButton(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(4),
            Padding = new Padding(6, 3, 6, 3),
        };

        private static Button CreateHoldButton(string text)
        {
            var button = CreateButton(text);

            // Hold-to-run is mouse/touch only: no keyboard focus, so a key press can never start a Jog.
            button.TabStop = false;
            return button;
        }
    }
}
