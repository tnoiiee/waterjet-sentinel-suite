using System.Reflection;
using Wjss.Contracts;
using Wjss.Domain;
using Wjss.Runtime.Core.CleaningPath;
using Wjss.Runtime.Core.CleaningPath.Execution;
using Wjss.Runtime.Core.CleaningPath.H1;
using Wjss.Runtime.Core.Motion.Manual;
using Wjss.Time;

namespace Wjss.MotionCommissioning;

/// <summary>
/// The H1 dry Cleaning Path commissioning operator surface (WSB1 only). It shows the selected
/// identity and its provenance, the fixed P1–P6 and Centre contracts, the live per-axis typed
/// observations and the run evidence, and it exposes exactly three fail-closed controls: Start
/// (enabled only when every typed precondition is satisfied), Abort and Close. The surface carries
/// a permanent DRY / NO WATER banner and exposes no raw command input of any kind.
///
/// The two resolved axes are driven through the existing typed Manual Motion session only; this
/// form never writes a device output directly and never bypasses the typed lifecycles.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal sealed class H1DryCommissioningForm : Form
{
    private readonly ManualMotionSession _session;
    private readonly IClock _clock;
    private readonly CleaningPathSnapshot _pathStore;
    private readonly IReadOnlyList<SensorConfigurationRecord> _sensorConfigurations;
    private readonly System.Windows.Forms.Timer _tick;

    private H1DryCommissioningCoordinator? _coordinator;

    private readonly ComboBox _sensorSelector = new();
    private readonly ComboBox _overrideSelector = new();
    private readonly Label _canonicalWjValue = NewValueLabel();
    private readonly Label _provenanceValue = NewValueLabel();
    private readonly Label _effectiveWjValue = NewValueLabel();
    private readonly Label _pairedIvValue = NewValueLabel();
    private readonly Label _routeValue = NewValueLabel();
    private readonly Label _pathContractValue = NewValueLabel();
    private readonly Label _phaseValue = NewValueLabel();
    private readonly Label _pointValue = NewValueLabel();
    private readonly Label _ledgerValue = NewValueLabel();
    private readonly Label _failureValue = NewValueLabel();
    private readonly Label _cleanupValue = NewValueLabel();
    private readonly Label _safeReturnValue = NewValueLabel();
    private readonly Label _horizontalLiveValue = NewValueLabel();
    private readonly Label _verticalLiveValue = NewValueLabel();
    private readonly CheckBox _identityAttested = NewGuardBox("Controller identity proven this session");
    private readonly CheckBox _endpointAttested = NewGuardBox("Owner-local endpoint configured");
    private readonly CheckBox _profileAttested = NewGuardBox("Owner-local motion profile configured");
    private readonly CheckBox _dryAttested = NewGuardBox("Dry mode declared (no water, no physical output command)");
    private readonly CheckBox _pumpAttested = NewGuardBox("No Pump command path connected");
    private readonly CheckBox _valveAttested = NewGuardBox("No physical Isolation Valve command path connected");
    private readonly CheckBox _queueAttested = NewGuardBox("No Queue / AutoSequence / next-job path connected");
    private readonly CheckBox _abortAttested = NewGuardBox("Physical abort accessible to the Operator");
    private readonly CheckBox _observerAttested = NewGuardBox("Owner present as Operator/Observer");
    private readonly Button _startButton = new();
    private readonly Button _abortButton = new();
    private readonly Button _closeButton = new();

    internal H1DryCommissioningForm(
        ManualMotionSession session,
        IClock clock,
        CleaningPathSnapshot pathStore,
        IReadOnlyList<SensorConfigurationRecord> sensorConfigurations)
    {
        _session = session;
        _clock = clock;
        _pathStore = pathStore;
        _sensorConfigurations = sensorConfigurations;

        Text = "WJSS H1 Dry Cleaning Path Commissioning (WSB1)";
        Width = 1280;
        Height = 860;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoScroll = true,
        };

        layout.Controls.Add(BuildBanner());
        layout.Controls.Add(BuildSelectionPanel());
        layout.Controls.Add(BuildIdentityPanel());
        layout.Controls.Add(BuildRunAndLivePanel());
        layout.Controls.Add(BuildGuardAndControlPanel());
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        _sensorSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var sensorConfiguration in _sensorConfigurations)
        {
            _ = _sensorSelector.Items.Add(sensorConfiguration.SensorId);
        }

        _sensorSelector.SelectedIndexChanged += (_, _) => RefreshIdentity();
        _overrideSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _ = _overrideSelector.Items.Add("(canonical assignment)");
        for (var index = 1; index <= 8; index++)
        {
            _ = _overrideSelector.Items.Add("WJ" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        _overrideSelector.SelectedIndex = 0;
        _overrideSelector.SelectedIndexChanged += (_, _) => RefreshIdentity();
        _startButton.Text = "Start";
        _startButton.Enabled = false;
        _startButton.Click += (_, _) => StartRun();
        _abortButton.Text = "Abort";
        _abortButton.Enabled = false;
        _abortButton.Click += (_, _) => _coordinator?.Abort();
        _closeButton.Text = "Close";
        _closeButton.Enabled = false;
        _closeButton.Click += (_, _) => _coordinator?.RequestClose();

        _tick = new System.Windows.Forms.Timer { Interval = 25 };
        _tick.Tick += (_, _) => OnTick();
        _tick.Start();
    }

    private static Label BuildBanner() => new Label
    {
        Dock = DockStyle.Fill,
        Text = "DRY / NO WATER — H1 WSB1 COMMISSIONING — NO PUMP COMMAND, NO PHYSICAL VALVE COMMAND, NO QUEUE",
        Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 16f, System.Drawing.FontStyle.Bold),
        ForeColor = System.Drawing.Color.White,
        BackColor = System.Drawing.Color.Firebrick,
        TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
    };

    private FlowLayoutPanel BuildSelectionPanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        panel.Controls.Add(new Label { Text = "Selected Sensor:", AutoSize = true, Padding = new Padding(8, 12, 0, 0) });
        _sensorSelector.Width = 160;
        panel.Controls.Add(_sensorSelector);
        panel.Controls.Add(new Label { Text = "Effective-WJ override:", AutoSize = true, Padding = new Padding(16, 12, 0, 0) });
        _overrideSelector.Width = 180;
        panel.Controls.Add(_overrideSelector);
        panel.Controls.Add(new Label
        {
            Text = "Build: " + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown"),
            AutoSize = true,
            Padding = new Padding(16, 12, 0, 0),
        });
        return panel;
    }

    private TableLayoutPanel BuildIdentityPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
        panel.Controls.Add(NewCaptionLabel("Canonical / default WJ:"), 0, 0);
        panel.Controls.Add(_canonicalWjValue, 1, 0);
        panel.Controls.Add(NewCaptionLabel("Assignment provenance:"), 2, 0);
        panel.Controls.Add(_provenanceValue, 3, 0);
        panel.Controls.Add(NewCaptionLabel("Effective WJ:"), 0, 1);
        panel.Controls.Add(_effectiveWjValue, 1, 1);
        panel.Controls.Add(NewCaptionLabel("Paired IV (informational only):"), 2, 1);
        panel.Controls.Add(_pairedIvValue, 3, 1);
        panel.Controls.Add(NewCaptionLabel("WSB / controller / axes / roles:"), 0, 2);
        panel.Controls.Add(_routeValue, 1, 2);
        panel.Controls.Add(NewCaptionLabel("P1-P6 path contract (mm):"), 0, 3);
        panel.Controls.Add(_pathContractValue, 1, 3);
        return panel;
    }

    private TableLayoutPanel BuildRunAndLivePanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7 };
        panel.Controls.Add(NewCaptionLabel("Current phase:"), 0, 0);
        panel.Controls.Add(_phaseValue, 1, 0);
        panel.Controls.Add(NewCaptionLabel("Current Point:"), 2, 0);
        panel.Controls.Add(_pointValue, 3, 0);
        panel.Controls.Add(NewCaptionLabel("Verified ledger:"), 0, 1);
        panel.Controls.Add(_ledgerValue, 1, 1);
        panel.Controls.Add(NewCaptionLabel("Primary failure:"), 2, 1);
        panel.Controls.Add(_failureValue, 3, 1);
        panel.Controls.Add(NewCaptionLabel("Cleanup evidence:"), 0, 2);
        panel.Controls.Add(_cleanupValue, 1, 2);
        panel.Controls.Add(NewCaptionLabel("Dry Safe Return:"), 2, 2);
        panel.Controls.Add(_safeReturnValue, 3, 2);
        panel.Controls.Add(NewCaptionLabel("Horizontal axis (live):"), 0, 3);
        panel.Controls.Add(_horizontalLiveValue, 1, 3);
        panel.Controls.Add(NewCaptionLabel("Vertical axis (live):"), 0, 4);
        panel.Controls.Add(_verticalLiveValue, 1, 4);
        return panel;
    }

    private FlowLayoutPanel BuildGuardAndControlPanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        foreach (var box in GuardBoxes())
        {
            box.Width = 360;
            box.CheckedChanged += (_, _) => RefreshControls();
            panel.Controls.Add(box);
        }

        _startButton.Width = 160;
        _abortButton.Width = 160;
        _closeButton.Width = 160;
        panel.Controls.Add(_startButton);
        panel.Controls.Add(_abortButton);
        panel.Controls.Add(_closeButton);
        return panel;
    }

    private void OnTick()
    {
        _session.Step();
        _coordinator?.Step();
        RefreshIdentity();
        RefreshRun();
        RefreshLive();
        RefreshControls();
    }

    private void RefreshIdentity()
    {
        var resolved = TryResolve();
        _canonicalWjValue.Text = resolved?.CanonicalAssignedWaterJetId ?? "NOT RESOLVED";
        _provenanceValue.Text = resolved?.EffectiveWjProvenance.ToString() ?? "NOT RESOLVED";
        _effectiveWjValue.Text = resolved?.EffectiveWaterJetId ?? "NOT RESOLVED";
        _pairedIvValue.Text = resolved?.DerivedIsolationValveId ?? "NOT RESOLVED";
        _routeValue.Text = resolved is null
            ? "NOT RESOLVED"
            : string.Join(
                " / ",
                resolved.CabinetId,
                resolved.ControllerId,
                "H=" + resolved.HorizontalAxisId,
                "V=" + resolved.VerticalAxisId,
                "brakes " + resolved.HorizontalBrakeOutputId + "+" + resolved.VerticalBrakeOutputId);

        var path = _sensorSelector.SelectedItem is string sensorId ? _pathStore.PathFor(sensorId) : null;
        if (path is null)
        {
            _pathContractValue.Text = "NO PATH SELECTED";
            return;
        }

        var pointTexts = new List<string>();
        foreach (var point in path.Points)
        {
            pointTexts.Add(
                point.PointId + "(" + point.HorizontalMillimetres.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/" + point.VerticalMillimetres.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
        }

        _pathContractValue.Text = string.Join(" -> ", pointTexts) + " ; Centre 125/125";
    }

    private ResolvedCleaningPath? TryResolve()
    {
        if (_sensorSelector.SelectedItem is not string sensorId)
        {
            return null;
        }

        SensorConfigurationRecord? configuration = null;
        foreach (var candidate in _sensorConfigurations)
        {
            if (string.Equals(candidate.SensorId, sensorId, StringComparison.Ordinal))
            {
                configuration = candidate;
                break;
            }
        }

        if (configuration is null)
        {
            return null;
        }

        var request = new CleaningPathResolutionRequest
        {
            SensorId = sensorId,
            RequestedOverrideWaterJetId = _overrideSelector.SelectedIndex <= 0
                ? null
                : _overrideSelector.SelectedItem as string,
        };

        var outcome = CleaningPathResolver.Resolve(_pathStore, configuration, request);
        return outcome.Accepted ? outcome.Resolution : null;
    }

    private void RefreshRun()
    {
        var snapshot = _coordinator?.Current;
        _phaseValue.Text = snapshot?.Phase.ToString() ?? "NotStarted";
        _pointValue.Text = snapshot?.CurrentPointId ?? "-";
        _ledgerValue.Text = snapshot is null || snapshot.VerifiedLedger.Count == 0
            ? "-"
            : string.Join(",", snapshot.VerifiedLedger);
        _failureValue.Text = snapshot?.PrimaryFailureCode ?? "-";
        _safeReturnValue.Text = (snapshot?.DrySafeReturn ?? H1DrySafeReturnState.NotPublished).ToString();
        var result = _coordinator?.Result;
        _cleanupValue.Text = result is null
            ? "-"
            : result.HorizontalCleanup.CleanupCode + " / " + result.VerticalCleanup.CleanupCode;
    }

    private void RefreshLive()
    {
        var resolved = TryResolve();
        _horizontalLiveValue.Text = resolved is null
            ? "NO AXIS RESOLVED"
            : LiveText(resolved.HorizontalAxisId);
        _verticalLiveValue.Text = resolved is null
            ? "NO AXIS RESOLVED"
            : LiveText(resolved.VerticalAxisId);
    }

    private ManualMotionAxisSnapshot? AxisSnapshotFor(string axisLetter)
    {
        foreach (var snapshot in _session.Snapshot.Axes)
        {
            if (string.Equals(snapshot.AxisLetter, axisLetter, StringComparison.Ordinal))
            {
                return snapshot;
            }
        }

        return null;
    }

    private string LiveText(string axisLetter)
    {
        var snapshot = AxisSnapshotFor(axisLetter);
        if (snapshot is null)
        {
            return "NO AXIS RESOLVED";
        }

        return string.Join(
            " | ",
            snapshot.RoleLabel,
            snapshot.CabinetId,
            "installed " + snapshot.InstalledPosition,
            "coverage " + snapshot.TargetCoverage,
            "life " + snapshot.HomeState + "/" + snapshot.CoordinateState + "/" + snapshot.StopState,
            "status " + Freshness(snapshot.StatusWord),
            "TP " + Reading(snapshot.PositionTp),
            "RP " + Reading(snapshot.PositionRp),
            "brake " + snapshot.BrakeCommandState + " out " + Freshness(snapshot.BrakeOutput),
            "last " + snapshot.LastOutcomeCode);
    }

    private static string Reading(ManualMotionReadingSnapshot reading) =>
        (reading.Value?.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) ?? "-")
        + " " + Freshness(reading);

    private static string Freshness(ManualMotionReadingSnapshot reading) =>
        reading.Fresh ? "FRESH" : (reading.ObservedAt is null ? "NEVER" : "STALE");

    private void RefreshControls()
    {
        var guardsSatisfied = GuardBoxes().TrueForAll(static box => box.Checked);
        var resolved = TryResolve();
        var active = _coordinator?.Active == true;
        _startButton.Enabled = guardsSatisfied && resolved is not null && !active;
        _abortButton.Enabled = active;
        _closeButton.Enabled = active;
    }

    private void StartRun()
    {
        var resolved = TryResolve();
        if (resolved is null)
        {
            return;
        }

        var configuration = _sensorConfigurations.FirstOrDefault(
            candidate => string.Equals(candidate.SensorId, resolved.SensorId, StringComparison.Ordinal));
        if (configuration is null)
        {
            return;
        }

        var boundary = new ManualMotionCleaningPathBoundary(
            _session,
            resolved.HorizontalAxisId,
            resolved.VerticalAxisId);
        _coordinator = new H1DryCommissioningCoordinator(_session, boundary, _clock);

        var outcome = _coordinator.Start(new H1DryCommissioningRequest
        {
            PathStore = _pathStore,
            Resolution = CleaningPathResolver.Resolve(
                _pathStore,
                configuration,
                new CleaningPathResolutionRequest
                {
                    SensorId = resolved.SensorId,
                    RequestedOverrideWaterJetId = resolved.RequestedOverrideWaterJetId,
                }),
            SelectedSensorId = resolved.SensorId,
            Guards = new H1DryCommissioningGuards
            {
                ControllerIdentityVerified = _identityAttested.Checked,
                ControllerEndpointConfigured = _endpointAttested.Checked,
                OwnerLocalProfileConfigured = _profileAttested.Checked,
                DryModeDeclared = _dryAttested.Checked,
                PumpCommandPathUnavailable = _pumpAttested.Checked,
                PhysicalValveCommandPathUnavailable = _valveAttested.Checked,
                QueueAutoSequenceUnavailable = _queueAttested.Checked,
                PhysicalAbortAccessible = _abortAttested.Checked,
                OperatorObserverPresent = _observerAttested.Checked,
            },
        });

        if (!outcome.Started)
        {
            _ = MessageBox.Show(
                "Start refused. Code: " + outcome.Code,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private List<CheckBox> GuardBoxes() =>
    [
        _identityAttested,
        _endpointAttested,
        _profileAttested,
        _dryAttested,
        _pumpAttested,
        _valveAttested,
        _queueAttested,
        _abortAttested,
        _observerAttested,
    ];

    private static Label NewValueLabel() => new()
    {
        AutoSize = true,
        MaximumSize = new System.Drawing.Size(760, 0),
        Text = "-",
    };

    private static Label NewCaptionLabel(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 9f, System.Drawing.FontStyle.Bold),
        Padding = new Padding(8, 8, 0, 0),
    };

    private static CheckBox NewGuardBox(string text) => new()
    {
        Text = text,
        AutoSize = true,
    };
}
