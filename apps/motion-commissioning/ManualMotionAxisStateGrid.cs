using System.Globalization;
using Wjss.Runtime.Core.Motion.Manual;

namespace Wjss.MotionCommissioning;

/// <summary>
/// The fixed presentation grid for one axis of the Manual Motion Commissioning window.
///
/// Each state field, raw value, observation freshness/timestamp and interpretation detail has its
/// own stable cell. A render assigns text only to a cell whose displayed value changed, so an
/// observation timestamp repaint is confined to its timestamp cell instead of invalidating a
/// complete multiline surface. The grid owns presentation only: it posts no request, constructs no
/// command and changes no observation, freshness, or motion lifecycle semantics.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build and review are the validation of record).
/// </summary>
internal sealed class ManualMotionAxisStateGrid : TableLayoutPanel
{
    private const int StateRowCount = 14;
    private const int BrakeCommandRow = 0;
    private const int BrakeOutputRow = 1;
    private const int StatusWordRow = 2;
    private const int MotionBitRow = 3;
    private const int HomeInputRow = 4;
    private const int DriveReadinessRow = 5;
    private const int PositionTpRow = 6;
    private const int PositionRpRow = 7;
    private const int HomeStateRow = 8;
    private const int CoordinateRow = 9;
    private const int DualZeroRow = 10;
    private const int StopRow = 11;
    private const int LastOutcomeRow = 12;
    private const int NotConfiguredRow = 13;

    private readonly Label[] _values = new Label[StateRowCount];
    private readonly ManualMotionTimestampCell?[] _timestamps = new ManualMotionTimestampCell?[StateRowCount];
    private readonly Label?[] _details = new Label?[StateRowCount];

    internal ManualMotionAxisStateGrid()
    {
        Dock = DockStyle.Fill;
        ColumnCount = 4;
        RowCount = StateRowCount;
        Margin = Padding.Empty;
        Padding = new Padding(2);
        BackColor = SystemColors.Control;

        SuspendLayout();
        try
        {
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

            for (var row = 0; row < StateRowCount; row++)
            {
                RowStyles.Add(new RowStyle(SizeType.Percent, RowWeight(row)));
                AddRow(row);
            }
        }
        finally
        {
            ResumeLayout(performLayout: false);
        }
    }

    internal void Display(ManualMotionAxisSnapshot axis)
    {
        ArgumentNullException.ThrowIfNull(axis);

        SetValue(BrakeCommandRow, axis.BrakeCommandState.ToString());
        DisplayObservation(
            BrakeOutputRow,
            axis.BrakeOutput,
            "logical state, UNCONFIRMED (no wiring or polarity meaning)");
        DisplayObservation(StatusWordRow, axis.StatusWord, "native status bits");
        DisplayInterpretation(MotionBitRow, axis.Motion, axis.MotionInterpretationConfigured);
        DisplayInterpretation(HomeInputRow, axis.HomeInput, axis.HomeInterpretationConfigured);
        DisplayObservation(PositionTpRow, axis.PositionTp, "native units, label unresolved");
        DisplayObservation(PositionRpRow, axis.PositionRp, "native units, label unresolved");
        SetValue(HomeStateRow, axis.HomeState.ToString());
        SetValue(CoordinateRow, axis.CoordinateState.ToString());
        SetValue(DualZeroRow, axis.DualZeroState.ToString());
        SetValue(StopRow, axis.StopState.ToString());
        SetDetail(
            StopRow,
            string.Concat("(dispatch only; StopConfirmed = ", axis.StopConfirmed ? "true" : "false", ")"));
        SetValue(LastOutcomeRow, Dash(axis.LastOutcomeCode));
        SetValue(
            NotConfiguredRow,
            axis.NotConfigured.Count == 0 ? "none" : string.Join(", ", axis.NotConfigured));
    }

    private void AddRow(int row)
    {
        Controls.Add(CreateFieldLabel(FieldText(row)), 0, row);

        var value = CreateLabel(InitialValue(row), monospace: row != DriveReadinessRow);
        Controls.Add(value, 1, row);
        SetColumnSpan(value, ValueSpan(row));
        _values[row] = value;

        if (HasTimestamp(row))
        {
            var timestamp = CreateTimestampCell();
            Controls.Add(timestamp, 2, row);
            _timestamps[row] = timestamp;
        }

        if (ValueSpan(row) < 3)
        {
            var detail = CreateLabel(InitialDetail(row), monospace: false);
            Controls.Add(detail, 3, row);
            _details[row] = detail;
        }
    }

    private void DisplayObservation(int row, ManualMotionReadingSnapshot observation, string note)
    {
        var refusal = observation.LastRefusalCode.Length == 0
            ? string.Empty
            : string.Concat("last read refused: ", observation.LastRefusalCode);
        if (observation.Value is not decimal value)
        {
            SetValue(row, "NO VALUE READ");
            SetTimestamp(row, string.Empty);
            SetDetail(row, refusal);
            return;
        }

        var at = observation.ObservedAt is DateTimeOffset observedAt
            ? observedAt.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : "-";
        SetValue(row, value.ToString(CultureInfo.InvariantCulture));
        SetTimestamp(
            row,
            string.Concat("[", observation.Fresh ? "current" : "STALE", " @ ", at, "]"));
        SetDetail(row, refusal.Length == 0 ? note : string.Concat(note, "  ", refusal));
    }

    private void DisplayInterpretation(
        int row,
        ManualMotionInterpretation interpretation,
        bool configured)
    {
        SetValue(
            row,
            interpretation switch
            {
                ManualMotionInterpretation.Active => "ACTIVE",
                ManualMotionInterpretation.Inactive => "INACTIVE",
                _ => "UNKNOWN (no current status read, or bit NOT_CONFIGURED)",
            });
        SetDetail(
            row,
            configured
                ? "(Owner-attested profile bit; not vendor- or hardware-verified)"
                : "(bit NOT_CONFIGURED)");
    }

    private void SetValue(int row, string text) => SetText(_values[row], text);

    private void SetTimestamp(int row, string text) => SetText(_timestamps[row], text);

    private void SetDetail(int row, string text) => SetText(_details[row], text);

    private static void SetText(Control? control, string text)
    {
        if (control is not null && !string.Equals(control.Text, text, StringComparison.Ordinal))
        {
            control.Text = text;
        }
    }

    private static Label CreateFieldLabel(string text)
    {
        var label = CreateLabel(text, monospace: false);
        label.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        return label;
    }

    private static Label CreateLabel(string text, bool monospace)
    {
        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = new Padding(3, 0, 3, 0),
            Padding = new Padding(2, 0, 2, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            BackColor = SystemColors.Control,
        };

        if (monospace)
        {
            label.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        }

        return label;
    }

    private static ManualMotionTimestampCell CreateTimestampCell() => new()
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(3, 0, 3, 0),
        Padding = new Padding(2, 0, 2, 0),
        Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
    };

    private static string FieldText(int row) => row switch
    {
        BrakeCommandRow => "Brake command",
        BrakeOutputRow => "Brake output (raw)",
        StatusWordRow => "Status word (raw)",
        MotionBitRow => "Motion bit",
        HomeInputRow => "Home input bit",
        DriveReadinessRow => "Drive readiness",
        PositionTpRow => "Position TP (raw)",
        PositionRpRow => "Position RP (raw)",
        HomeStateRow => "Home state",
        CoordinateRow => "Coordinate (DP)",
        DualZeroRow => "Dual zero (DE)",
        StopRow => "Stop",
        LastOutcomeRow => "Last outcome",
        NotConfiguredRow => "NOT_CONFIGURED",
        _ => string.Empty,
    };

    private static string InitialValue(int row) => row == DriveReadinessRow
        ? "observation only: raw status word above; no drive or motor interpretation (UNCONFIRMED)"
        : string.Empty;

    private static string InitialDetail(int row) => row switch
    {
        BrakeCommandRow => "(command state only; the physical brake is NOT confirmed)",
        DualZeroRow => "(effect UNVERIFIED)",
        _ => string.Empty,
    };

    private static int ValueSpan(int row) => row switch
    {
        BrakeOutputRow or StatusWordRow or PositionTpRow or PositionRpRow => 1,
        BrakeCommandRow or MotionBitRow or HomeInputRow or DualZeroRow or StopRow => 2,
        _ => 3,
    };

    private static bool HasTimestamp(int row) =>
        row is BrakeOutputRow or StatusWordRow or PositionTpRow or PositionRpRow;

    private static float RowWeight(int row) => row switch
    {
        BrakeCommandRow or BrakeOutputRow or StatusWordRow or PositionTpRow or PositionRpRow => 1.25F,
        MotionBitRow or HomeInputRow => 1.4F,
        NotConfiguredRow => 2.8F,
        _ => 1F,
    };

    private static string Dash(string value) => value.Length == 0 ? "-" : value;
}
