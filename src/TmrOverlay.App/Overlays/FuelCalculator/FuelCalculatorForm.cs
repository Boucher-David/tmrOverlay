using System.Drawing;
using TmrOverlay.App.History;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Telemetry.Live;

namespace TmrOverlay.App.Overlays.FuelCalculator;

internal sealed class FuelCalculatorForm : PersistentOverlayForm
{
    private const int StintRowCount = 6;
    private const int CompactHeight = 154;
    private const int CompactMinimumTableHeight = 70;
    private const int NormalMinimumTableHeight = 150;
    private readonly LiveTelemetryStore _liveTelemetryStore;
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly TableLayoutPanel _table;
    private readonly Label _overviewValueLabel;
    private readonly Label _sourceLabel;
    private readonly Label[] _stintNumberLabels = new Label[StintRowCount];
    private readonly Label[] _stintLengthLabels = new Label[StintRowCount];
    private readonly Label[] _stintTireLabels = new Label[StintRowCount];
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly int _normalClientHeight;
    private bool _compactLayout;

    public FuelCalculatorForm(
        LiveTelemetryStore liveTelemetryStore,
        SessionHistoryQueryService historyQueryService,
        OverlaySettings settings,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            FuelCalculatorOverlayDefinition.Definition.DefaultWidth,
            FuelCalculatorOverlayDefinition.Definition.DefaultHeight)
    {
        _liveTelemetryStore = liveTelemetryStore;
        _historyQueryService = historyQueryService;
        _normalClientHeight = Math.Max(ClientSize.Height, FuelCalculatorOverlayDefinition.Definition.DefaultHeight);

        BackColor = Color.FromArgb(14, 18, 21);
        Padding = new Padding(12);

        _titleLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(14, 10),
            Size = new Size(150, 24),
            Text = "Fuel Calculator"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(145, 224, 170),
            Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(164, 11),
            Size = new Size(ClientSize.Width - 178, 22),
            Text = "waiting"
        };

        _table = new TableLayoutPanel
        {
            BackColor = Color.FromArgb(24, 30, 34),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            ColumnCount = 3,
            Location = new Point(14, 42),
            RowCount = StintRowCount + 1,
            Size = new Size(ClientSize.Width - 28, ClientSize.Height - 76)
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
        for (var row = 0; row < _table.RowCount; row++)
        {
            _table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _table.RowCount));
        }

        var overviewLabel = CreateCellLabel("Overview", bold: true);
        _overviewValueLabel = CreateCellLabel("waiting for live fuel", alignRight: true, bold: true);
        var tiresHeaderLabel = CreateCellLabel("Advice", alignRight: true, bold: true);
        _table.Controls.Add(overviewLabel, 0, 0);
        _table.Controls.Add(_overviewValueLabel, 1, 0);
        _table.Controls.Add(tiresHeaderLabel, 2, 0);

        for (var index = 0; index < StintRowCount; index++)
        {
            var stintLabel = CreateCellLabel($"Stint {index + 1}");
            var lengthLabel = CreateCellLabel("--", alignRight: true);
            var tireLabel = CreateCellLabel("--", alignRight: true);
            _stintNumberLabels[index] = stintLabel;
            _stintLengthLabels[index] = lengthLabel;
            _stintTireLabels[index] = tireLabel;
            _table.Controls.Add(stintLabel, 0, index + 1);
            _table.Controls.Add(lengthLabel, 1, index + 1);
            _table.Controls.Add(tireLabel, 2, index + 1);
        }

        _sourceLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(128, 145, 153),
            Font = new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(14, ClientSize.Height - 28),
            Size = new Size(ClientSize.Width - 28, 18),
            Text = "source: waiting",
            TextAlign = ContentAlignment.MiddleLeft
        };

        Controls.Add(_titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_table);
        Controls.Add(_sourceLabel);

        RegisterDragSurfaces(
            _titleLabel,
            _statusLabel,
            _table,
            _overviewValueLabel,
            _sourceLabel);
        RegisterDragSurfaces(_stintNumberLabels.Concat(_stintLengthLabels).Concat(_stintTireLabels).ToArray());

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _refreshTimer.Tick += (_, _) => RefreshOverlay();
        _refreshTimer.Start();

        RefreshOverlay();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_statusLabel is null || _table is null || _sourceLabel is null)
        {
            return;
        }

        _statusLabel.Location = new Point(164, 11);
        _statusLabel.Size = new Size(Math.Max(120, ClientSize.Width - 178), 22);
        _table.Location = new Point(14, 42);
        _table.Size = new Size(
            Math.Max(250, ClientSize.Width - 28),
            Math.Max(_compactLayout ? CompactMinimumTableHeight : NormalMinimumTableHeight, ClientSize.Height - 76));
        _sourceLabel.Location = new Point(14, ClientSize.Height - 28);
        _sourceLabel.Size = new Size(Math.Max(250, ClientSize.Width - 28), 18);
    }

    protected override Size GetPersistedOverlaySize()
    {
        return _compactLayout
            ? new Size(Width, _normalClientHeight)
            : base.GetPersistedOverlaySize();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _table.Dispose();
            _titleLabel.Dispose();
            _statusLabel.Dispose();
            _sourceLabel.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var borderPen = new Pen(Color.FromArgb(72, 255, 255, 255));
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private void RefreshOverlay()
    {
        var live = _liveTelemetryStore.Snapshot();
        var history = _historyQueryService.Lookup(live.Combo);
        var strategy = FuelStrategyCalculator.From(live, history);

        ApplyStatusColor(strategy);
        _statusLabel.Text = strategy.Status;
        _overviewValueLabel.Text = BuildOverview(strategy);
        _sourceLabel.Text = BuildSourceText(strategy, history);

        var rows = BuildDisplayRows(strategy);
        ApplyPreferredLayout(rows.Count);
        for (var index = 0; index < StintRowCount; index++)
        {
            if (index < rows.Count)
            {
                var row = rows[index];
                _stintNumberLabels[index].Text = row.Label;
                _stintLengthLabels[index].Text = row.Value;
                _stintTireLabels[index].Text = row.Advice;
                continue;
            }

            _stintNumberLabels[index].Text = $"Stint {index + 1}";
            _stintLengthLabels[index].Text = string.Empty;
            _stintTireLabels[index].Text = string.Empty;
        }

        UpdateVisibleRows(rows.Count);
        Invalidate();
    }

    private static string BuildOverview(FuelStrategySnapshot strategy)
    {
        if (strategy.PlannedRaceLaps is { } plannedLaps
            && strategy.PlannedStintCount is { } stintCount
            && strategy.FinalStintTargetLaps is { } finalStintLaps)
        {
            if (stintCount <= 1)
            {
                return $"{plannedLaps} laps | no stop";
            }

            return $"{plannedLaps} laps | {stintCount} stints | final {finalStintLaps}";
        }

        var fuel = FuelStrategyCalculator.FormatNumber(strategy.CurrentFuelLiters, " L");
        var remaining = FuelStrategyCalculator.FormatNumber(strategy.RaceLapsRemaining, " laps");
        var needed = strategy.AdditionalFuelNeededLiters is > 0.1d
            ? $"+{strategy.AdditionalFuelNeededLiters.Value:0.0} L"
            : "covered";
        return $"{fuel} | {remaining} | {needed}";
    }

    private static string BuildStintText(FuelStintEstimate stint)
    {
        if (string.Equals(stint.Source, "finish", StringComparison.OrdinalIgnoreCase))
        {
            return "no fuel stop needed";
        }

        if (stint.TargetLaps is { } targetLaps)
        {
            var target = FormatPlain(stint.TargetFuelPerLapLiters);
            var suffix = stint.Source == "final" ? " final" : string.Empty;
            return $"{targetLaps} laps{suffix} | target {target} L/lap";
        }

        return $"{stint.LengthLaps:0.0} laps";
    }

    private static IReadOnlyList<FuelDisplayRow> BuildDisplayRows(FuelStrategySnapshot strategy)
    {
        var rows = new List<FuelDisplayRow>(StintRowCount);
        if (strategy.RhythmComparison is { AdditionalStopCount: > 0 } comparison)
        {
            rows.Add(new FuelDisplayRow(
                "Strategy",
                BuildRhythmText(comparison),
                BuildRhythmAdvice(comparison)));
        }

        foreach (var stint in strategy.Stints.Take(StintRowCount - rows.Count))
        {
            rows.Add(new FuelDisplayRow(
                $"Stint {stint.Number}",
                BuildStintText(stint),
                stint.TireAdvice?.Text ?? "--"));
        }

        return rows;
    }

    private static string BuildRhythmText(FuelRhythmComparison comparison)
    {
        return $"{comparison.LongTargetLaps}-lap rhythm avoids +{comparison.AdditionalStopCount} {Pluralize("stop", comparison.AdditionalStopCount)}";
    }

    private static string BuildRhythmAdvice(FuelRhythmComparison comparison)
    {
        var time = comparison.EstimatedTimeLossSeconds is { } seconds && seconds > 0d
            ? $"~{seconds:0}s"
            : "--";
        return comparison.RequiredSavingLitersPerLap > 0.01d
            ? $"{time} | save {comparison.RequiredSavingLitersPerLap:0.0}"
            : time;
    }

    private static string BuildSourceText(FuelStrategySnapshot strategy, SessionHistoryLookupResult history)
    {
        var fuelPerLap = FuelStrategyCalculator.FormatNumber(strategy.FuelPerLapLiters, " L/lap");
        var fullTank = FuelStrategyCalculator.FormatNumber(strategy.FullTankStintLaps, " laps/tank");
        var historySource = history.UserAggregate is not null
            ? "user"
            : history.BaselineAggregate is not null
                ? "baseline"
                : "none";
        var historicalRange = strategy.FuelPerLapMinimumLiters is not null || strategy.FuelPerLapMaximumLiters is not null
            ? $" | min/avg/max {FormatPlain(strategy.FuelPerLapMinimumLiters)}/{FormatPlain(strategy.FuelPerLapLiters)}/{FormatPlain(strategy.FuelPerLapMaximumLiters)}"
            : string.Empty;
        var gaps = strategy.OverallLeaderGapLaps is not null || strategy.ClassLeaderGapLaps is not null
            ? $" | gap O{FormatPlain(strategy.OverallLeaderGapLaps)} C{FormatPlain(strategy.ClassLeaderGapLaps)}"
            : string.Empty;
        var tireModel = strategy.TireChangeServiceSeconds is not null || strategy.FuelFillRateLitersPerSecond is not null
            ? $" | tires {strategy.TireModelSource}"
            : string.Empty;
        return $"burn {fuelPerLap} ({strategy.FuelPerLapSource}) | {fullTank} | history {historySource}{historicalRange}{tireModel}{gaps}";
    }

    private void ApplyPreferredLayout(int rowCount)
    {
        var shouldCompact = rowCount <= 1;
        var targetHeight = shouldCompact ? CompactHeight : _normalClientHeight;
        _compactLayout = shouldCompact;
        if (ClientSize.Height != targetHeight)
        {
            ClientSize = new Size(ClientSize.Width, targetHeight);
        }
    }

    private void UpdateVisibleRows(int stintCount)
    {
        var visibleStintRows = Math.Clamp(stintCount, 1, StintRowCount);
        var visibleRows = visibleStintRows + 1;
        for (var row = 0; row < _table.RowStyles.Count; row++)
        {
            _table.RowStyles[row].SizeType = row < visibleRows ? SizeType.Percent : SizeType.Absolute;
            _table.RowStyles[row].Height = row < visibleRows ? 100f / visibleRows : 0f;
        }

        for (var index = 0; index < StintRowCount; index++)
        {
            var visible = index < visibleStintRows;
            _stintNumberLabels[index].Visible = visible;
            _stintLengthLabels[index].Visible = visible;
            _stintTireLabels[index].Visible = visible;
        }
    }

    private static string FormatPlain(double? value)
    {
        return value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)
            ? "--"
            : FormattableString.Invariant($"{value.Value:0.0}");
    }

    private static string Pluralize(string singular, int count)
    {
        return count == 1 ? singular : $"{singular}s";
    }

    private void ApplyStatusColor(FuelStrategySnapshot strategy)
    {
        if (!strategy.HasData || strategy.FuelPerLapLiters is null)
        {
            BackColor = Color.FromArgb(14, 18, 21);
            _statusLabel.ForeColor = Color.FromArgb(160, 160, 160);
            return;
        }

        if (strategy.RhythmComparison is { IsRealistic: true, AdditionalStopCount: > 0 }
            || strategy.RequiredFuelSavingPercent is > 0d and <= 0.05d
            || strategy.StopOptimization is { IsRealistic: true, RequiredSavingLitersPerLap: > 0d })
        {
            BackColor = Color.FromArgb(54, 30, 14);
            _statusLabel.ForeColor = Color.FromArgb(246, 184, 88);
            return;
        }

        BackColor = Color.FromArgb(14, 38, 28);
        _statusLabel.ForeColor = Color.FromArgb(112, 224, 146);
    }

    private static Label CreateCellLabel(string text, bool alignRight = false, bool bold = false)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", bold ? 9.5f : 9f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = bold ? Color.White : Color.FromArgb(218, 226, 230),
            Padding = new Padding(8, 0, 8, 0),
            Text = text,
            TextAlign = alignRight ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
        };
    }

    private sealed record FuelDisplayRow(string Label, string Value, string Advice);
}
