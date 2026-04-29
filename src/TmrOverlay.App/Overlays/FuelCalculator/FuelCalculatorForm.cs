using System.Diagnostics;
using System.Drawing;
using TmrOverlay.App.History;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Fuel;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.FuelCalculator;

internal sealed class FuelCalculatorForm : PersistentOverlayForm
{
    private const int StintRowCount = 6;
    private const int NormalMinimumTableHeight = 150;
    private const int RefreshIntervalMilliseconds = 1000;
    private static readonly TimeSpan HistoryLookupCacheDuration = TimeSpan.FromSeconds(30);
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly AppPerformanceState _performanceState;
    private readonly OverlaySettings _settings;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly TableLayoutPanel _table;
    private readonly Label _overviewValueLabel;
    private readonly Label _sourceLabel;
    private readonly string _fontFamily;
    private readonly string _unitSystem;
    private readonly Label[] _stintNumberLabels = new Label[StintRowCount];
    private readonly Label[] _stintLengthLabels = new Label[StintRowCount];
    private readonly Label[] _stintTireLabels = new Label[StintRowCount];
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private HistoricalComboIdentity? _cachedHistoryCombo;
    private SessionHistoryLookupResult? _cachedHistory;
    private DateTimeOffset _cachedHistoryAtUtc;

    private bool ShowAdvice => _settings.GetBooleanOption(OverlayOptionKeys.FuelAdvice, defaultValue: true);

    private bool ShowSource => _settings.GetBooleanOption(OverlayOptionKeys.FuelSource, defaultValue: true);

    public FuelCalculatorForm(
        ILiveTelemetrySource liveTelemetrySource,
        SessionHistoryQueryService historyQueryService,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        string unitSystem,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            FuelCalculatorOverlayDefinition.Definition.DefaultWidth,
            FuelCalculatorOverlayDefinition.Definition.DefaultHeight)
    {
        _liveTelemetrySource = liveTelemetrySource;
        _historyQueryService = historyQueryService;
        _performanceState = performanceState;
        _settings = settings;
        _fontFamily = fontFamily;
        _unitSystem = string.Equals(unitSystem, "Imperial", StringComparison.OrdinalIgnoreCase)
            ? "Imperial"
            : "Metric";

        BackColor = OverlayTheme.Colors.WindowBackground;
        Padding = new Padding(12);

        _titleLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Font = OverlayTheme.Font(_fontFamily, 11f, FontStyle.Bold),
            Location = new Point(14, 10),
            Size = new Size(150, 24),
            Text = "Fuel Calculator"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.SuccessText,
            Font = OverlayTheme.Font(_fontFamily, 9f),
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(164, 11),
            Size = new Size(ClientSize.Width - 178, 22),
            Text = "waiting"
        };

        _table = new TableLayoutPanel
        {
            BackColor = OverlayTheme.Colors.PanelBackground,
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

        var overviewLabel = CreateCellLabel(_fontFamily, "Overview", bold: true);
        _overviewValueLabel = CreateCellLabel(_fontFamily, "waiting for live fuel", alignRight: true, bold: true);
        var tiresHeaderLabel = CreateCellLabel(_fontFamily, "Advice", alignRight: true, bold: true);
        _table.Controls.Add(overviewLabel, 0, 0);
        _table.Controls.Add(_overviewValueLabel, 1, 0);
        _table.Controls.Add(tiresHeaderLabel, 2, 0);

        for (var index = 0; index < StintRowCount; index++)
        {
            var stintLabel = CreateCellLabel(_fontFamily, $"Stint {index + 1}");
            var lengthLabel = CreateCellLabel(_fontFamily, "--", alignRight: true);
            var tireLabel = CreateCellLabel(_fontFamily, "--", alignRight: true);
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
            ForeColor = OverlayTheme.Colors.TextMuted,
            Font = OverlayTheme.Font(_fontFamily, 8.5f),
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
            Interval = RefreshIntervalMilliseconds
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
            Math.Max(NormalMinimumTableHeight, ClientSize.Height - 76));
        _sourceLabel.Location = new Point(14, ClientSize.Height - 28);
        _sourceLabel.Size = new Size(Math.Max(250, ClientSize.Width - 28), 18);
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
        using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private void RefreshOverlay()
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var live = _liveTelemetrySource.Snapshot();
            var history = LookupHistory(live.Combo);
            var strategy = FuelStrategyCalculator.From(live, history);
            var viewModel = FuelCalculatorViewModel.From(strategy, history, ShowAdvice, _unitSystem, StintRowCount);

            ApplyStatusColor(strategy);
            _statusLabel.Text = viewModel.Status;
            _overviewValueLabel.Text = viewModel.Overview;
            _sourceLabel.Text = viewModel.Source;
            _sourceLabel.Visible = ShowSource;
            ApplyAdviceColumnVisibility();

            var rows = viewModel.Rows;
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
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayFuelRefresh,
                Stopwatch.GetElapsedTime(started),
                succeeded);
        }
    }

    private SessionHistoryLookupResult LookupHistory(HistoricalComboIdentity combo)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cachedHistory is not null
            && SameCombo(_cachedHistoryCombo, combo)
            && now - _cachedHistoryAtUtc <= HistoryLookupCacheDuration)
        {
            return _cachedHistory;
        }

        _cachedHistory = _historyQueryService.Lookup(combo);
        _cachedHistoryCombo = combo;
        _cachedHistoryAtUtc = now;
        return _cachedHistory;
    }

    private static bool SameCombo(HistoricalComboIdentity? left, HistoricalComboIdentity right)
    {
        return left is not null
            && string.Equals(left.CarKey, right.CarKey, StringComparison.Ordinal)
            && string.Equals(left.TrackKey, right.TrackKey, StringComparison.Ordinal)
            && string.Equals(left.SessionKey, right.SessionKey, StringComparison.Ordinal);
    }

    private void UpdateVisibleRows(int stintCount)
    {
        var visibleStintRows = Math.Clamp(stintCount, 0, StintRowCount);
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
            _stintTireLabels[index].Visible = visible && ShowAdvice;
        }
    }

    private void ApplyAdviceColumnVisibility()
    {
        _table.ColumnStyles[0].Width = ShowAdvice ? 24f : 28f;
        _table.ColumnStyles[1].Width = ShowAdvice ? 48f : 72f;
        _table.ColumnStyles[2].Width = ShowAdvice ? 28f : 0f;
    }

    private void ApplyStatusColor(FuelStrategySnapshot strategy)
    {
        if (!strategy.HasData || strategy.FuelPerLapLiters is null)
        {
            BackColor = OverlayTheme.Colors.WindowBackground;
            _statusLabel.ForeColor = OverlayTheme.Colors.TextSubtle;
            return;
        }

        if (strategy.RhythmComparison is { IsRealistic: true, AdditionalStopCount: > 0 }
            || strategy.RequiredFuelSavingPercent is > 0d and <= 0.05d
            || strategy.StopOptimization is { IsRealistic: true, RequiredSavingLitersPerLap: > 0d })
        {
            BackColor = OverlayTheme.Colors.WarningStrongBackground;
            _statusLabel.ForeColor = OverlayTheme.Colors.WarningText;
            return;
        }

        BackColor = OverlayTheme.Colors.SuccessBackground;
        _statusLabel.ForeColor = OverlayTheme.Colors.SuccessText;
    }

    private static Label CreateCellLabel(string fontFamily, string text, bool alignRight = false, bool bold = false)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = OverlayTheme.Font(fontFamily, bold ? 9.5f : 9f, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = bold ? OverlayTheme.Colors.TextPrimary : OverlayTheme.Colors.TextSecondary,
            Padding = new Padding(8, 0, 8, 0),
            Text = text,
            TextAlign = alignRight ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
        };
    }

}
