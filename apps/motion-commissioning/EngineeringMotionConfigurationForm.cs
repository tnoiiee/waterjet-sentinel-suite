using System.Globalization;
using Wjss.Runtime.Core.Motion.Manual;

namespace Wjss.MotionCommissioning;

/// <summary>
/// The Engineering Motion Configuration surface: the five per-axis conversion settings of WSB1 axes
/// A (Horizontal), B (Vertical), C (Horizontal) and D (Vertical), plus the four DERIVED quantities
/// those settings produce.
///
/// Every value is per-axis engineering data. Nothing here is a Product constant, nothing is shared
/// between axes, and there is deliberately no divide-by-two, encoder-multiplier or other free factor:
/// the theoretical/reference relationship is the ratio of the axis's own drive pulses per revolution
/// to its own encoder pulses per revolution, displayed read-only. A control cannot express a factor
/// the model does not derive.
///
/// A field is a TEXT field, not a spinner, because "absent" is a real, distinct state of the profile
/// schema: an empty field means NOT_CONFIGURED and is captured as absent, whereas a spinner would
/// silently coerce an absent value to its minimum and turn "never measured" into "configured as 1".
///
/// The surface owns no motion lifecycle and no session state. It seeds its editors from the session's
/// immutable profile, and Apply posts one typed candidate to the Application session, which validates
/// it and captures it as the immutable Engineering Motion Configuration. While any operation is in
/// flight the session refuses a change and this surface disables Apply and says why; it never queues,
/// retries or applies a change later on its own.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build and review are the validation of record).
/// </summary>
internal sealed class EngineeringMotionConfigurationForm : Form
{
    private const int RefreshIntervalMilliseconds = 300;

    private static readonly ManualMotionAxis[] Axes =
        { ManualMotionAxis.A, ManualMotionAxis.B, ManualMotionAxis.C, ManualMotionAxis.D };

    private static readonly Color EditableBanner = Color.FromArgb(20, 70, 125);
    private static readonly Color LockedBanner = Color.FromArgb(140, 30, 30);

    private static readonly EngineeringRow[] Rows = new[]
    {
        new EngineeringRow(EngineeringSetting.EncoderPulsesPerRevolution, "Encoder pulses per revolution (counts/rev)", false, 1m, 10000000m, 0),
        new EngineeringRow(EngineeringSetting.DrivePulsesPerRevolution, "Drive pulses per revolution (counts/rev)", false, 1m, 10000000m, 0),
        new EngineeringRow(EngineeringSetting.MechanicalGearRatio, "Mechanical gear ratio (output rev / motor rev)", false, 0.0000001m, 1000000m, 7),
        new EngineeringRow(EngineeringSetting.PitchMillimetresPerRevolution, "Pitch (mm per output revolution)", false, 0.0000001m, 1000000m, 7),
        new EngineeringRow(EngineeringSetting.PositionToleranceMillimetres, "Position tolerance (mm)", false, 0.0000001m, 1000000m, 7),
        new EngineeringRow(EngineeringSetting.TpNormalizationRatio, "DERIVED: TP normalization ratio (drive / encoder)", true, 0m, 0m, 0),
        new EngineeringRow(EngineeringSetting.PaCommandUnitsPerMillimetre, "DERIVED: PA command units per mm", true, 0m, 0m, 0),
        new EngineeringRow(EngineeringSetting.TpFeedbackUnitsPerMillimetre, "DERIVED: TP feedback units per mm", true, 0m, 0m, 0),
        new EngineeringRow(EngineeringSetting.RpFeedbackUnitsPerMillimetre, "DERIVED: RP feedback units per mm", true, 0m, 0m, 0),
    };

    private readonly ManualMotionSession _session;
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly Label _banner = new();
    private readonly Label _stateLine = new();
    private readonly Label _messageLine = new();
    private readonly Button _apply = new();
    private readonly Button _close = new();
    private readonly Dictionary<ManualMotionAxis, AxisEditors> _editors = new();

    internal EngineeringMotionConfigurationForm(ManualMotionSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));

        DoubleBuffered = true;
        Text = "Engineering Motion Configuration - per-axis conversion settings";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(940, 560);
        Size = new Size(1060, 640);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
        SeedFromProfile();

        _refreshTimer.Interval = RefreshIntervalMilliseconds;
        _refreshTimer.Tick += (_, _) => RefreshEngineeringState();
        _refreshTimer.Start();
        RefreshEngineeringState();
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
        _banner.Dock = DockStyle.Fill;
        _banner.TextAlign = ContentAlignment.MiddleLeft;
        _banner.ForeColor = Color.White;
        _banner.BackColor = EditableBanner;
        _banner.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
        _banner.Padding = new Padding(10, 0, 10, 0);

        _stateLine.Dock = DockStyle.Fill;
        _stateLine.AutoEllipsis = true;
        _stateLine.TextAlign = ContentAlignment.MiddleLeft;

        _messageLine.Dock = DockStyle.Fill;
        _messageLine.AutoEllipsis = true;
        _messageLine.TextAlign = ContentAlignment.MiddleLeft;

        // One row per quantity, one column per axis: the four independent values of one quantity are
        // compared across axes far more often than a single axis is read alone, and this is the only
        // layout in which all four are simultaneously visible.
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = Axes.Length + 1,
            RowCount = Rows.Length + 1,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
        for (var index = 0; index < Axes.Length; index++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
        }

        grid.Controls.Add(HeaderCell("Setting (independent per axis)"), 0, 0);
        for (var index = 0; index < Axes.Length; index++)
        {
            grid.Controls.Add(
                HeaderCell(string.Concat("Axis ", Axes[index].ToString(), " (", ManualMotionTopology.RoleLabel(Axes[index]), ")")),
                index + 1,
                0);
        }

        for (var row = 0; row < Rows.Length; row++)
        {
            var spec = Rows[row];
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(RowLabel(spec), 0, row + 1);

            for (var index = 0; index < Axes.Length; index++)
            {
                var editors = EditorsFor(Axes[index]);
                grid.Controls.Add(spec.IsDerived ? editors.Derived[spec.Key] : editors.Input[spec.Key], index + 1, row + 1);
            }
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        _apply.Text = "Apply to session";
        _apply.AutoSize = true;
        _apply.Margin = new Padding(6);
        _apply.Click += (_, _) => Apply();
        _close.Text = "Close";
        _close.AutoSize = true;
        _close.Margin = new Padding(6);
        _close.Click += (_, _) => Close();
        buttons.Controls.Add(_apply);
        buttons.Controls.Add(_close);

        var footer = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Derived values are read-only. There is no divide-by-two, encoder-multiplier or other free factor: "
                + "TP normalization is this axis's drive pulses per revolution over its own encoder pulses per revolution. "
                + "An empty field means NOT_CONFIGURED and is captured as absent.",
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_banner, 0, 0);
        root.Controls.Add(_stateLine, 0, 1);
        root.Controls.Add(_messageLine, 0, 2);
        root.Controls.Add(grid, 0, 3);
        root.Controls.Add(footer, 0, 4);
        root.Controls.Add(buttons, 0, 5);
        Controls.Add(root);
    }

    private AxisEditors EditorsFor(ManualMotionAxis axis)
    {
        if (!_editors.TryGetValue(axis, out var editors))
        {
            editors = new AxisEditors();
            _editors.Add(axis, editors);
        }

        return editors;
    }

    private void SeedFromProfile()
    {
        // A refused profile has no axis settings to seed: the editors stay empty and Apply is refused.
        var profile = _session.Profile;
        foreach (var axis in Axes)
        {
            var axisProfile = profile?.For(axis);
            var editors = EditorsFor(axis);
            editors.SetInput(EngineeringSetting.EncoderPulsesPerRevolution, axisProfile?.EncoderPulsesPerRevolution);
            editors.SetInput(EngineeringSetting.DrivePulsesPerRevolution, axisProfile?.DrivePulsesPerRevolution);
            editors.SetInput(EngineeringSetting.MechanicalGearRatio, axisProfile?.MechanicalGearRatio);
            editors.SetInput(EngineeringSetting.PitchMillimetresPerRevolution, axisProfile?.PitchMillimetresPerRevolution);
            editors.SetInput(EngineeringSetting.PositionToleranceMillimetres, axisProfile?.PositionToleranceMillimetres);
        }
    }

    private void Apply()
    {
        var profile = _session.Profile;
        if (profile is null)
        {
            _messageLine.Text = "No profile to edit: the session's constructed profile was refused. Nothing was applied.";
            return;
        }

        // Every authored value is re-read as the type the schema demands. An unparsable or out-of-range
        // field is an explicit refusal here rather than a coerced number that silently becomes valid.
        var candidate = profile;
        foreach (var axis in Axes)
        {
            var editors = EditorsFor(axis);
            var values = new decimal?[5];
            for (var index = 0; index < EditableRows.Length; index++)
            {
                var row = EditableRows[index];
                if (!editors.TryRead(row, out values[index], out var refusal))
                {
                    _messageLine.Text = string.Concat(
                        "Axis ",
                        axis.ToString(),
                        ": ",
                        row.Caption,
                        " - ",
                        refusal,
                        " Nothing was applied.");
                    return;
                }
            }

            var current = candidate.For(axis) ?? new ManualMotionAxisProfile();
            candidate = candidate.WithAxis(
                axis,
                current.WithEngineering(ToInt(values[0]), ToInt(values[1]), values[2], values[3], values[4]));
        }

        var result = _session.ApplyEngineeringSettings(ManualMotionProfileResult.FromProfile(candidate));
        _messageLine.Text = result.Accepted
            ? string.Concat(
                "Captured at generation ",
                result.Generation.ToString(CultureInfo.InvariantCulture),
                ". Every later operation is planned against this immutable snapshot.")
            : string.Concat("Refused ", result.Code, ": ", ManualMotionCodes.DetailFor(result.Code));
        RefreshEngineeringState();
    }

    /// <summary>
    /// Re-reads the session snapshot into this window's banner, editors and derived values. It is NOT a
    /// repaint: Control.Refresh() already means that on a Form, so this method is deliberately named for
    /// what it does instead of hiding the inherited member.
    /// </summary>
    private void RefreshEngineeringState()
    {
        var snapshot = _session.Snapshot;
        var editable = _session.EngineeringSettingsEditable && _session.Profile is not null;

        _apply.Enabled = editable;
        _banner.BackColor = editable ? EditableBanner : LockedBanner;
        _banner.Text = editable
            ? "EDITABLE - nothing is in flight. An accepted change becomes the immutable Engineering Motion Configuration."
            : "LOCKED - an operation or a pending Close is in flight. The Engineering Motion Configuration cannot change.";

        foreach (var axis in Axes)
        {
            EditorsFor(axis).SetEditable(editable);
        }

        // The derived values are recomputed from the CURRENT editor contents, so an authored change is
        // previewed before it is applied; after it is applied they equal the captured snapshot.
        foreach (var axis in Axes)
        {
            EditorsFor(axis).ShowDerived(Derive(EditorsFor(axis)));
        }

        _stateLine.Text = string.Concat(
            "Captured generation ",
            snapshot.Engineering.Generation.ToString(CultureInfo.InvariantCulture),
            " at ",
            snapshot.Engineering.CapturedAt.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            "   Session: ",
            snapshot.State.ToString(),
            "   Active operations: ",
            snapshot.ActiveOperations.Count.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The four derived quantities of one axis, computed from the editor values only. This is the same
    /// derivation the session publishes, so the preview and the captured snapshot cannot disagree.
    /// </summary>
    private static ManualMotionPulseDomain? Derive(AxisEditors editors)
    {
        if (!editors.TryRead(EditableRows[0], out var encoder, out _)
            || !editors.TryRead(EditableRows[1], out var drive, out _)
            || !editors.TryRead(EditableRows[2], out var ratio, out _)
            || !editors.TryRead(EditableRows[3], out var pitch, out _))
        {
            return null;
        }

        return ManualMotionPulseDomain.TryCreate(ToInt(encoder), ToInt(drive), ratio, pitch, out var domain)
            ? domain
            : null;
    }

    private static EngineeringRow[] EditableRows { get; } =
        Rows.Where(static row => !row.IsDerived).ToArray();

    private static int? ToInt(decimal? value) => value is { } raw && raw >= int.MinValue && raw <= int.MaxValue
        ? decimal.ToInt32(raw)
        : null;

    private static Label HeaderCell(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
        Padding = new Padding(4, 6, 4, 6),
    };

    private static Label RowLabel(EngineeringRow row) => new()
    {
        Text = row.Caption,
        AutoSize = true,
        Padding = new Padding(4, 6, 4, 6),
        ForeColor = row.IsDerived ? SystemColors.GrayText : SystemColors.ControlText,
    };

    /// <summary>One quantity: its schema range, its display precision, and whether it is authored.</summary>
    private sealed record EngineeringRow(
        EngineeringSetting Key,
        string Caption,
        bool IsDerived,
        decimal Minimum,
        decimal Maximum,
        int DecimalPlaces);

    private enum EngineeringSetting
    {
        EncoderPulsesPerRevolution,
        DrivePulsesPerRevolution,
        MechanicalGearRatio,
        PitchMillimetresPerRevolution,
        PositionToleranceMillimetres,
        TpNormalizationRatio,
        PaCommandUnitsPerMillimetre,
        TpFeedbackUnitsPerMillimetre,
        RpFeedbackUnitsPerMillimetre,
    }

    /// <summary>The editors of one axis: five authored values and four read-only derivations.</summary>
    private sealed class AxisEditors
    {
        internal AxisEditors()
        {
            foreach (var row in Rows)
            {
                if (row.IsDerived)
                {
                    Derived.Add(row.Key, new Label
                    {
                        AutoSize = true,
                        Padding = new Padding(4, 6, 4, 6),
                        Text = "not configured",
                        ForeColor = SystemColors.GrayText,
                    });
                }
                else
                {
                    Input.Add(row.Key, new TextBox
                    {
                        Width = 130,
                        TextAlign = HorizontalAlignment.Right,
                        Margin = new Padding(4, 3, 4, 3),
                        PlaceholderText = "not configured",
                    });
                }
            }
        }

        internal Dictionary<EngineeringSetting, TextBox> Input { get; } = new();

        internal Dictionary<EngineeringSetting, Label> Derived { get; } = new();

        internal void SetInput(EngineeringSetting key, decimal? value) =>
            Input[key].Text = value is null
                ? string.Empty
                : value.Value.ToString("0.################", CultureInfo.InvariantCulture);

        internal void SetEditable(bool editable)
        {
            foreach (var input in Input.Values)
            {
                input.ReadOnly = !editable;
            }
        }

        /// <summary>
        /// Reads one authored value. Empty means NOT_CONFIGURED and is carried as absent; a value that is
        /// not a number, or is outside the schema range, is a refusal that names the field.
        /// </summary>
        internal bool TryRead(EngineeringRow row, out decimal? value, out string refusal)
        {
            value = null;
            refusal = string.Empty;

            var text = Input[row.Key].Text.Trim();
            if (text.Length == 0)
            {
                return true;
            }

            if (!decimal.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed))
            {
                refusal = "not a number in invariant form.";
                return false;
            }

            if (parsed < row.Minimum || parsed > row.Maximum)
            {
                refusal = string.Concat(
                    "outside ",
                    row.Minimum.ToString(CultureInfo.InvariantCulture),
                    " to ",
                    row.Maximum.ToString(CultureInfo.InvariantCulture),
                    ".");
                return false;
            }

            if (decimal.Round(parsed, row.DecimalPlaces) != parsed)
            {
                refusal = string.Concat("more than ", row.DecimalPlaces.ToString(CultureInfo.InvariantCulture), " decimal places.");
                return false;
            }

            value = parsed;
            return true;
        }

        internal void ShowDerived(ManualMotionPulseDomain? domain)
        {
            Derived[EngineeringSetting.TpNormalizationRatio].Text = Number(domain?.TpNormalizationRatio);
            Derived[EngineeringSetting.PaCommandUnitsPerMillimetre].Text = Number(domain?.PaCommandUnitsPerMillimetre);
            Derived[EngineeringSetting.TpFeedbackUnitsPerMillimetre].Text = Number(domain?.TpFeedbackUnitsPerMillimetre);
            Derived[EngineeringSetting.RpFeedbackUnitsPerMillimetre].Text = Number(domain?.RpFeedbackUnitsPerMillimetre);
        }

        private static string Number(decimal? value) => value is null
            ? "not configured"
            : value.Value.ToString("0.################", CultureInfo.InvariantCulture);
    }
}
