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
    private readonly OverlayTableLayoutPanel _table;
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
    private int _lastVisibleStintRows = -1;
    private bool? _lastAdviceColumnVisible;
    private bool? _lastAdviceRowVisibility;
    private long? _lastRefreshSequence;
    private bool? _lastRefreshShowAdvice;
    private bool? _lastRefreshShowSource;

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

        _table = new OverlayTableLayoutPanel
        {
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
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            base.OnPaint(e);
            using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayFuelPaint,
                started,
                succeeded);
        }
    }

    private void RefreshOverlay()
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            LiveTelemetrySnapshot live;
            var snapshotStarted = Stopwatch.GetTimestamp();
            var snapshotSucceeded = false;
            try
            {
                live = _liveTelemetrySource.Snapshot();
                snapshotSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayFuelSnapshot,
                    snapshotStarted,
                    snapshotSucceeded);
            }

            var showAdvice = ShowAdvice;
            var showSource = ShowSource;
            var now = DateTimeOffset.UtcNow;
            var previousSequence = _lastRefreshSequence;
            if (previousSequence == live.Sequence
                && _lastRefreshShowAdvice == showAdvice
                && _lastRefreshShowSource == showSource)
            {
                _performanceState.RecordOverlayRefreshDecision(
                    FuelCalculatorOverlayDefinition.Definition.Id,
                    now,
                    previousSequence,
                    live.Sequence,
                    live.LastUpdatedAtUtc,
                    applied: false);
                succeeded = true;
                return;
            }

            SessionHistoryLookupResult history;
            var historyStarted = Stopwatch.GetTimestamp();
            var historySucceeded = false;
            try
            {
                history = LookupHistory(live.Combo);
                historySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayFuelHistoryLookup,
                    historyStarted,
                    historySucceeded);
            }

            FuelStrategySnapshot strategy;
            var strategyStarted = Stopwatch.GetTimestamp();
            var strategySucceeded = false;
            try
            {
                strategy = FuelStrategyCalculator.From(live, history);
                strategySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayFuelStrategy,
                    strategyStarted,
                    strategySucceeded);
            }

            FuelCalculatorViewModel viewModel;
            var viewModelStarted = Stopwatch.GetTimestamp();
            var viewModelSucceeded = false;
            try
            {
                viewModel = FuelCalculatorViewModel.From(strategy, history, showAdvice, _unitSystem, StintRowCount);
                viewModelSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayFuelViewModel,
                    viewModelStarted,
                    viewModelSucceeded);
            }

            var uiChanged = false;
            var applyStarted = Stopwatch.GetTimestamp();
            var applySucceeded = false;
            try
            {
                uiChanged |= ApplyStatusColor(strategy);
                uiChanged |= SetTextIfChanged(_statusLabel, viewModel.Status);
                uiChanged |= SetTextIfChanged(_overviewValueLabel, viewModel.Overview);
                uiChanged |= SetTextIfChanged(_sourceLabel, viewModel.Source);
                uiChanged |= SetVisibleIfChanged(_sourceLabel, showSource);
                uiChanged |= ApplyAdviceColumnVisibility(showAdvice);
                applySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayFuelApplyUi,
                    applyStarted,
                    applySucceeded);
            }

            var rows = viewModel.Rows;
            var rowsStarted = Stopwatch.GetTimestamp();
            var rowsSucceeded = false;
            try
            {
                var rowsChanged = false;
                for (var index = 0; index < StintRowCount; index++)
                {
                    if (index < rows.Count)
                    {
                        var row = rows[index];
                        rowsChanged |= SetTextIfChanged(_stintNumberLabels[index], row.Label);
                        rowsChanged |= SetTextIfChanged(_stintLengthLabels[index], row.Value);
                        rowsChanged |= SetTextIfChanged(_stintTireLabels[index], row.Advice);
                        continue;
                    }

                    rowsChanged |= SetTextIfChanged(_stintNumberLabels[index], $"Stint {index + 1}");
                    rowsChanged |= SetTextIfChanged(_stintLengthLabels[index], string.Empty);
                    rowsChanged |= SetTextIfChanged(_stintTireLabels[index], string.Empty);
                }

                var layoutChanged = UpdateVisibleRows(rows.Count, showAdvice);
                if (layoutChanged)
                {
                    _table.Invalidate();
                }

                uiChanged |= rowsChanged || layoutChanged;
                rowsSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayFuelRows,
                    rowsStarted,
                    rowsSucceeded);
            }

            _lastRefreshSequence = live.Sequence;
            _lastRefreshShowAdvice = showAdvice;
            _lastRefreshShowSource = showSource;
            _performanceState.RecordOverlayRefreshDecision(
                FuelCalculatorOverlayDefinition.Definition.Id,
                now,
                previousSequence,
                live.Sequence,
                live.LastUpdatedAtUtc,
                applied: uiChanged);
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayFuelRefresh,
                started,
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

    private bool UpdateVisibleRows(int _, bool showAdvice)
    {
        var visibleStintRows = StintRowCount;
        if (_lastVisibleStintRows == visibleStintRows && _lastAdviceRowVisibility == showAdvice)
        {
            return false;
        }

        var changed = false;
        var visibleRows = visibleStintRows + 1;
        for (var row = 0; row < _table.RowStyles.Count; row++)
        {
            var sizeType = row < visibleRows ? SizeType.Percent : SizeType.Absolute;
            var height = row < visibleRows ? 100f / visibleRows : 0f;
            if (_table.RowStyles[row].SizeType != sizeType)
            {
                _table.RowStyles[row].SizeType = sizeType;
                changed = true;
            }

            if (Math.Abs(_table.RowStyles[row].Height - height) > 0.001f)
            {
                _table.RowStyles[row].Height = height;
                changed = true;
            }
        }

        for (var index = 0; index < StintRowCount; index++)
        {
            var visible = index < visibleStintRows;
            changed |= SetVisibleIfChanged(_stintNumberLabels[index], visible);
            changed |= SetVisibleIfChanged(_stintLengthLabels[index], visible);
            changed |= SetVisibleIfChanged(_stintTireLabels[index], visible && showAdvice);
        }

        _lastVisibleStintRows = visibleStintRows;
        _lastAdviceRowVisibility = showAdvice;
        return changed;
    }

    private bool ApplyAdviceColumnVisibility(bool showAdvice)
    {
        if (_lastAdviceColumnVisible == showAdvice)
        {
            return false;
        }

        _table.ColumnStyles[0].Width = showAdvice ? 24f : 28f;
        _table.ColumnStyles[1].Width = showAdvice ? 48f : 72f;
        _table.ColumnStyles[2].Width = showAdvice ? 28f : 0f;
        _lastAdviceColumnVisible = showAdvice;
        return true;
    }

    private bool ApplyStatusColor(FuelStrategySnapshot strategy)
    {
        if (!strategy.HasData || strategy.FuelPerLapLiters is null)
        {
            var changed = SetBackColorIfChanged(this, OverlayTheme.Colors.WindowBackground);
            changed |= SetForeColorIfChanged(_statusLabel, OverlayTheme.Colors.TextSubtle);
            return changed;
        }

        if (strategy.RhythmComparison is { IsRealistic: true, AdditionalStopCount: > 0 }
            || strategy.RequiredFuelSavingPercent is > 0d and <= 0.05d
            || strategy.StopOptimization is { IsRealistic: true, RequiredSavingLitersPerLap: > 0d })
        {
            var changed = SetBackColorIfChanged(this, OverlayTheme.Colors.WarningStrongBackground);
            changed |= SetForeColorIfChanged(_statusLabel, OverlayTheme.Colors.WarningText);
            return changed;
        }

        var successChanged = SetBackColorIfChanged(this, OverlayTheme.Colors.SuccessBackground);
        successChanged |= SetForeColorIfChanged(_statusLabel, OverlayTheme.Colors.SuccessText);
        return successChanged;
    }

    private static bool SetTextIfChanged(Label label, string? value)
    {
        var text = value ?? string.Empty;
        if (string.Equals(label.Text, text, StringComparison.Ordinal))
        {
            return false;
        }

        label.Text = text;
        return true;
    }

    private static bool SetVisibleIfChanged(Control control, bool visible)
    {
        if (control.Visible == visible)
        {
            return false;
        }

        control.Visible = visible;
        return true;
    }

    private static bool SetBackColorIfChanged(Control control, Color color)
    {
        if (control.BackColor == color)
        {
            return false;
        }

        control.BackColor = color;
        return true;
    }

    private static bool SetForeColorIfChanged(Control control, Color color)
    {
        if (control.ForeColor == color)
        {
            return false;
        }

        control.ForeColor = color;
        return true;
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
