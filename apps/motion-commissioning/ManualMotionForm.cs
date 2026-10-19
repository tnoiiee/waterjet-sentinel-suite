using System.Globalization;
using Wjss.Runtime.Core.Motion.Manual;

namespace Wjss.MotionCommissioning;

/// <summary>
/// The Owner-local Manual Motion Commissioning window for WSB1 Axis A (Horizontal) and Axis B
/// (Vertical).
///
/// The window owns no motion lifecycle. Every control posts one typed request to the Application
/// session, and every displayed value comes from the session snapshot. There is no raw command entry,
/// no generic output number and no command construction here.
///
/// Hold-to-run Jog: MouseDown starts a press; MouseUp, lost pointer capture and window deactivation
/// end it; while the press is held a heartbeat is posted at the refresh cadence, and the session
/// stops the Jog on its own when the heartbeat expires or the maximum hold duration is reached.
/// Closing the window requests the session close (an active motion operation is stopped first, then
/// the port is invalidated, closed and unloaded) before the window goes away.
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

    private ActivePress? _activePress;
    private long _pressSequence;
    private long _lastLogSequence;
    private bool _closeRequested;
    private bool _closeComplete;
    private DateTimeOffset _closeRequestedAt;

    internal ManualMotionForm(ManualMotionSession session, ManualMotionSessionRunner runner)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));

        BuildLayout();
        WireAxis(_axisA);
        WireAxis(_axisB);

        Deactivate += (_, _) => EndActivePress(ManualMotionJogEndReason.Deactivated);
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
                EndActivePress(ManualMotionJogEndReason.Released);
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
        MinimumSize = new Size(1100, 760);
        Size = new Size(1320, 900);
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

        var axes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        axes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        axes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        axes.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        axes.Controls.Add(_axisA.Group, 0, 0);
        axes.Controls.Add(_axisB.Group, 1, 0);

        _logList.Dock = DockStyle.Fill;
        _logList.IntegralHeight = false;
        _logList.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 72F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 28F));
        root.Controls.Add(_modeBanner, 0, 0);
        root.Controls.Add(_sessionLine, 0, 1);
        root.Controls.Add(_operationLine, 0, 2);
        root.Controls.Add(axes, 0, 3);
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
        if (_activePress is not null || _closeRequested)
        {
            return;
        }

        _pressSequence++;
        _activePress = new ActivePress(axis, _pressSequence);
        _session.RequestJogStart(axis, direction, _pressSequence);
    }

    private void EndPress(ManualMotionAxis axis, ManualMotionJogEndReason reason)
    {
        var press = _activePress;
        if (press is null || press.Axis != axis)
        {
            return;
        }

        _activePress = null;
        _session.RequestJogRelease(press.Axis, press.PressId, reason);
    }

    private void EndActivePress(ManualMotionJogEndReason reason)
    {
        var press = _activePress;
        if (press is null)
        {
            return;
        }

        _activePress = null;
        _session.RequestJogRelease(press.Axis, press.PressId, reason);
    }

    private void OnRefreshTick()
    {
        var press = _activePress;
        if (press is not null)
        {
            _session.RequestJogHeartbeat(press.Axis, press.PressId);
        }

        var snapshot = _session.Snapshot;
        Render(snapshot);

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
            _operationLine.Text = OperationText(snapshot.ActiveOperation);
        }

        if (snapshot.Axes.Count >= 2)
        {
            RenderAxis(_axisA, snapshot.Axes[0], snapshot, _closeRequested);
            RenderAxis(_axisB, snapshot.Axes[1], snapshot, _closeRequested);
        }

        AppendNewLogEntries(snapshot.Log);
    }

    private static void RenderAxis(AxisControls controls, ManualMotionAxisSnapshot axis, ManualMotionSnapshot snapshot, bool closing)
    {
        controls.Title = string.Concat(axis.RoleLabel, " Axis - ", axis.AxisLetter);
        controls.Group.Text = axis.AllowedByProfile
            ? controls.Title
            : string.Concat(controls.Title, "   (NOT ALLOWED BY PROFILE)");

        var lines = new List<string>
        {
            Line("Brake command", string.Concat(axis.BrakeCommandState.ToString(), "  (command state only; the physical brake is NOT confirmed)")),
            Line("Brake output (raw)", ObservationText(axis.BrakeOutput, "logical state, UNCONFIRMED (no wiring or polarity meaning)")),
            Line("Status word (raw)", ObservationText(axis.StatusWord, "native status bits")),
            Line("Motion bit", InterpretationText(axis.Motion, axis.InterpretationOwnerAttested)),
            Line("Home input bit", InterpretationText(axis.HomeInput, axis.InterpretationOwnerAttested)),
            Line("Drive readiness", "observation only: raw status word above; no drive or motor interpretation (UNCONFIRMED)"),
            Line("Position TP (raw)", ObservationText(axis.PositionTp, "native units, label unresolved")),
            Line("Position RP (raw)", ObservationText(axis.PositionRp, "native units, label unresolved")),
            Line("Home state", axis.HomeState.ToString()),
            Line("Coordinate (DP)", axis.CoordinateState.ToString()),
            Line("Dual zero (DE)", string.Concat(axis.DualZeroState.ToString(), "  (effect UNVERIFIED)")),
            Line("Stop", string.Concat(axis.StopState.ToString(), "  (dispatch only; StopConfirmed = ", axis.StopConfirmed ? "true" : "false", ")")),
            Line("Last outcome", Dash(axis.LastOutcomeCode)),
            Line("NOT_CONFIGURED", axis.NotConfigured.Count == 0 ? "none" : string.Join(", ", axis.NotConfigured)),
        };
        controls.State.Text = string.Join(Environment.NewLine, lines);

        var ready = snapshot.State == ManualMotionSessionState.Ready && !closing;
        var idle = snapshot.ActiveOperation is null;
        var allowed = axis.AllowedByProfile;
        controls.BrakeRelease.Enabled = ready && idle && allowed;
        controls.BrakeApply.Enabled = ready && idle && allowed;
        controls.Home.Enabled = ready && idle && allowed;
        controls.DefineZero.Enabled = ready && idle && allowed;
        controls.DefineDualZero.Enabled = ready && idle && allowed;
        controls.Move.Enabled = ready && idle && allowed;
        controls.Target.Enabled = ready && allowed;

        // Jog buttons are never disabled in the middle of a press, so MouseUp and capture loss always arrive.
        controls.JogNegative.Enabled = ready && allowed;
        controls.JogPositive.Enabled = ready && allowed;
        controls.Stop.Enabled = snapshot.State is ManualMotionSessionState.Ready
            or ManualMotionSessionState.Closing
            or ManualMotionSessionState.Faulted;
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

    private static string OperationText(ManualMotionOperationSnapshot? operation)
    {
        if (operation is null)
        {
            return "Active operation: none";
        }

        return string.Concat(
            "Active operation #",
            operation.Id.ToString(CultureInfo.InvariantCulture),
            ": ",
            operation.Kind.ToString(),
            " on Axis ",
            operation.Axis.ToString(),
            "   Phase: ",
            operation.Phase.ToString(),
            operation.StopRequested ? "   STOP REQUESTED" : string.Empty);
    }

    private static string ObservationText(ManualMotionReadingSnapshot observation, string note)
    {
        var refusal = observation.LastRefusalCode.Length == 0
            ? string.Empty
            : string.Concat("  last read refused: ", observation.LastRefusalCode);
        if (observation.Value is not decimal value)
        {
            return string.Concat("NO VALUE READ", refusal);
        }

        var at = observation.ObservedAt is DateTimeOffset observedAt
            ? observedAt.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : "-";
        return string.Concat(
            value.ToString(CultureInfo.InvariantCulture),
            "  [",
            observation.Fresh ? "current" : "STALE",
            " @ ",
            at,
            "]  ",
            note,
            refusal);
    }

    private static string InterpretationText(ManualMotionInterpretation interpretation, bool ownerAttested)
    {
        var basis = ownerAttested ? "  (Owner-attested profile bit; not vendor- or hardware-verified)" : "  (bit NOT_CONFIGURED)";
        return interpretation switch
        {
            ManualMotionInterpretation.Active => string.Concat("ACTIVE", basis),
            ManualMotionInterpretation.Inactive => string.Concat("INACTIVE", basis),
            _ => string.Concat("UNKNOWN (no current status read, or bit NOT_CONFIGURED)", basis),
        };
    }

    private static string Line(string label, string value) => string.Concat(label.PadRight(20), ": ", value);

    private static string Dash(string value) => value.Length == 0 ? "-" : value;

    private sealed record ActivePress(ManualMotionAxis Axis, long PressId);

    /// <summary>
    /// The ordinary Label used for a frequently updated Axis state panel, with background and text
    /// painting completed in the double buffer before the completed frame is presented.
    /// </summary>
    private sealed class BufferedStateLabel : Label
    {
        internal BufferedStateLabel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
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
            State = new BufferedStateLabel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
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

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(commands, 0, 0);
            layout.Controls.Add(move, 0, 1);
            layout.Controls.Add(State, 0, 2);
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

        internal Label State { get; }

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
