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
        Padding = new Padding(OverlayTheme.Layout.OverlayChromePadding);

        _titleLabel = OverlayChrome.CreateTitleLabel(_fontFamily, "Fuel Calculator", width: 150);
        _statusLabel = OverlayChrome.CreateStatusLabel(_fontFamily, titleWidth: 150, clientWidth: ClientSize.Width, minimumWidth: 120);

        _table = new OverlayTableLayoutPanel
        {
            ColumnCount = 3,
            Location = OverlayChrome.TableLocation(),
            RowCount = StintRowCount + 1,
            Size = OverlayChrome.TableSize(ClientSize.Width, ClientSize.Height, minimumWidth: 250, minimumHeight: NormalMinimumTableHeight)
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

        _sourceLabel = OverlayChrome.CreateSourceLabel(_fontFamily, ClientSize.Width, ClientSize.Height, minimumWidth: 250);

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

        _statusLabel.Location = OverlayChrome.StatusLocation(titleWidth: 150);
        _statusLabel.Size = OverlayChrome.StatusSize(ClientSize.Width, titleWidth: 150, minimumWidth: 120);
        _table.Location = OverlayChrome.TableLocation();
        _table.Size = OverlayChrome.TableSize(ClientSize.Width, ClientSize.Height, minimumWidth: 250, minimumHeight: NormalMinimumTableHeight);
        _sourceLabel.Location = OverlayChrome.SourceLocation(ClientSize.Height);
        _sourceLabel.Size = OverlayChrome.SourceSize(ClientSize.Width, minimumWidth: 250);
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
            OverlayChrome.DrawWindowBorder(e.Graphics, ClientSize);
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
                uiChanged |= OverlayChrome.ApplyChromeState(this, _titleLabel, _statusLabel, _sourceLabel, ChromeStateFor(viewModel, strategy, live, _settings, showSource), titleWidth: 150);
                uiChanged |= OverlayChrome.SetTextIfChanged(_overviewValueLabel, viewModel.Overview);
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
                        rowsChanged |= OverlayChrome.SetTextIfChanged(_stintNumberLabels[index], row.Label);
                        rowsChanged |= OverlayChrome.SetTextIfChanged(_stintLengthLabels[index], row.Value);
                        rowsChanged |= OverlayChrome.SetTextIfChanged(_stintTireLabels[index], row.Advice);
                        continue;
                    }

                    rowsChanged |= OverlayChrome.SetTextIfChanged(_stintNumberLabels[index], $"Stint {index + 1}");
                    rowsChanged |= OverlayChrome.SetTextIfChanged(_stintLengthLabels[index], string.Empty);
                    rowsChanged |= OverlayChrome.SetTextIfChanged(_stintTireLabels[index], string.Empty);
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

        var visibleRows = visibleStintRows + 1;
        var changed = OverlayChrome.SetPercentRows(_table, visibleRows);

        for (var index = 0; index < StintRowCount; index++)
        {
            var visible = index < visibleStintRows;
            changed |= OverlayChrome.SetVisibleIfChanged(_stintNumberLabels[index], visible);
            changed |= OverlayChrome.SetVisibleIfChanged(_stintLengthLabels[index], visible);
            changed |= OverlayChrome.SetVisibleIfChanged(_stintTireLabels[index], visible && showAdvice);
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

    private static Label CreateCellLabel(string fontFamily, string text, bool alignRight = false, bool bold = false)
    {
        return OverlayChrome.CreateTableCellLabel(
            fontFamily,
            text,
            alignRight,
            bold,
            padding: new Padding(8, 0, 8, 0),
            textSize: 9f,
            boldTextSize: 9.5f);
    }

    private static OverlayChromeState ChromeStateFor(
        FuelCalculatorViewModel viewModel,
        FuelStrategySnapshot strategy,
        LiveTelemetrySnapshot live,
        OverlaySettings settings,
        bool showSource)
    {
        var showStatus = OverlayChromeSettings.ShowHeaderStatus(settings, live);
        var footerMode = showSource && OverlayChromeSettings.ShowFooterSource(settings, live)
            ? OverlayChromeFooterMode.Always
            : OverlayChromeFooterMode.Never;
        return new OverlayChromeState(
            "Fuel Calculator",
            showStatus ? viewModel.Status : string.Empty,
            ChromeTone(strategy),
            viewModel.Source,
            footerMode);
    }

    private static OverlayChromeTone ChromeTone(FuelStrategySnapshot strategy)
    {
        if (!strategy.HasData || strategy.FuelPerLapLiters is null)
        {
            return OverlayChromeTone.Waiting;
        }

        if (strategy.RhythmComparison is { IsRealistic: true, AdditionalStopCount: > 0 }
            || strategy.RequiredFuelSavingPercent is > 0d and <= 0.05d
            || strategy.StopOptimization is { IsRealistic: true, RequiredSavingLitersPerLap: > 0d })
        {
            return OverlayChromeTone.Warning;
        }

        return OverlayChromeTone.Success;
    }

}
