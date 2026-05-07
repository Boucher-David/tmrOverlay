using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.Relative;

internal sealed class RelativeForm : PersistentOverlayForm
{
    private const int MaximumRows = 17;
    private const int NormalMinimumTableHeight = 180;
    private const int CompactRowHeight = 26;
    private const int RefreshIntervalMilliseconds = 250;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger<RelativeForm> _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly OverlaySettings _settings;
    private readonly string _fontFamily;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly OverlayTableLayoutPanel _table;
    private readonly Label _sourceLabel;
    private readonly ClassPositionLabel[] _positionLabels = new ClassPositionLabel[MaximumRows];
    private readonly Label[] _driverLabels = new Label[MaximumRows];
    private readonly Label[] _gapLabels = new Label[MaximumRows];
    private readonly Label[] _detailLabels = new Label[MaximumRows];
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private long? _lastRefreshSequence;
    private int _lastVisibleRows = -1;
    private string? _overlayError;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;

    private int CarsAhead => _settings.GetIntegerOption(
        OverlayOptionKeys.RelativeCarsAhead,
        defaultValue: 5,
        minimum: 0,
        maximum: 8);

    private int CarsBehind => _settings.GetIntegerOption(
        OverlayOptionKeys.RelativeCarsBehind,
        defaultValue: 5,
        minimum: 0,
        maximum: 8);

    public RelativeForm(
        ILiveTelemetrySource liveTelemetrySource,
        ILogger<RelativeForm> logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            RelativeOverlayDefinition.Definition.DefaultWidth,
            RelativeOverlayDefinition.Definition.DefaultHeight)
    {
        _liveTelemetrySource = liveTelemetrySource;
        _logger = logger;
        _performanceState = performanceState;
        _settings = settings;
        _fontFamily = fontFamily;

        BackColor = OverlayTheme.Colors.WindowBackground;
        Padding = new Padding(OverlayTheme.Layout.OverlayChromePadding);

        _titleLabel = OverlayChrome.CreateTitleLabel(_fontFamily, "Relative", width: 150);
        _statusLabel = OverlayChrome.CreateStatusLabel(_fontFamily, titleWidth: 150, clientWidth: ClientSize.Width, minimumWidth: 120);

        _table = new OverlayTableLayoutPanel
        {
            ColumnCount = 4,
            Location = OverlayChrome.TableLocation(),
            RowCount = MaximumRows,
            Size = OverlayChrome.TableSize(ClientSize.Width, ClientSize.Height, minimumWidth: 250, minimumHeight: NormalMinimumTableHeight)
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17f));
        for (var row = 0; row < MaximumRows; row++)
        {
            _table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / MaximumRows));
            _positionLabels[row] = CreatePositionCellLabel(_fontFamily, "--");
            _driverLabels[row] = OverlayChrome.CreateTableCellLabel(_fontFamily, string.Empty);
            _gapLabels[row] = OverlayChrome.CreateTableCellLabel(_fontFamily, "--", alignRight: true, monospace: true);
            _detailLabels[row] = OverlayChrome.CreateTableCellLabel(_fontFamily, string.Empty, alignRight: true);
            _table.Controls.Add(_positionLabels[row], 0, row);
            _table.Controls.Add(_driverLabels[row], 1, row);
            _table.Controls.Add(_gapLabels[row], 2, row);
            _table.Controls.Add(_detailLabels[row], 3, row);
        }

        _sourceLabel = OverlayChrome.CreateSourceLabel(_fontFamily, ClientSize.Width, ClientSize.Height, minimumWidth: 250);

        Controls.Add(_titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_table);
        Controls.Add(_sourceLabel);

        RegisterDragSurfaces(_titleLabel, _statusLabel, _table, _sourceLabel);
        RegisterDragSurfaces(
            _positionLabels
                .Cast<Label>()
                .Concat(_driverLabels)
                .Concat(_gapLabels)
                .Concat(_detailLabels)
                .ToArray());

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
        catch (Exception exception)
        {
            ReportOverlayError(exception, "render");
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayRelativePaint,
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
            LiveTelemetrySnapshot snapshot;
            var snapshotStarted = Stopwatch.GetTimestamp();
            var snapshotSucceeded = false;
            try
            {
                snapshot = _liveTelemetrySource.Snapshot();
                snapshotSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayRelativeSnapshot,
                    snapshotStarted,
                    snapshotSucceeded);
            }

            var carsAhead = CarsAhead;
            var carsBehind = CarsBehind;
            var now = DateTimeOffset.UtcNow;
            var previousSequence = _lastRefreshSequence;

            RelativeOverlayViewModel viewModel;
            var viewModelStarted = Stopwatch.GetTimestamp();
            var viewModelSucceeded = false;
            try
            {
                viewModel = RelativeOverlayViewModel.From(snapshot, now, carsAhead, carsBehind);
                viewModelSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayRelativeViewModel,
                    viewModelStarted,
                    viewModelSucceeded);
            }

            var applyStarted = Stopwatch.GetTimestamp();
            var applySucceeded = false;
            var uiChanged = false;
            try
            {
                _overlayError = null;
                uiChanged |= OverlayChrome.ApplyChromeState(this, _titleLabel, _statusLabel, _sourceLabel, ChromeStateFor(viewModel, snapshot, _settings), titleWidth: 150);
                uiChanged |= ApplyRows(viewModel);
                applySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayRelativeApplyUi,
                    applyStarted,
                    applySucceeded);
            }

            _lastRefreshSequence = snapshot.Sequence;
            _performanceState.RecordOverlayRefreshDecision(
                RelativeOverlayDefinition.Definition.Id,
                now,
                previousSequence,
                snapshot.Sequence,
                snapshot.LastUpdatedAtUtc,
                applied: uiChanged);
            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "refresh");
            OverlayChrome.ApplyChromeState(
                this,
                _titleLabel,
                _statusLabel,
                _sourceLabel,
                OverlayChromeState.Error("Relative", "relative error", _overlayError),
                titleWidth: 150);
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayRelativeRefresh,
                started,
                succeeded);
        }
    }

    private bool ApplyRows(RelativeOverlayViewModel viewModel)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var rows = BuildStableRows(viewModel);
            var visibleRows = rows.Count;

            var changed = false;
            var layoutChanged = UpdateVisibleRows(visibleRows);
            layoutChanged |= UpdateTableHeight(visibleRows);
            if (layoutChanged)
            {
                _table.SuspendLayout();
            }

            try
            {
                for (var index = 0; index < MaximumRows; index++)
                {
                    if (index < rows.Count && rows[index] is { } row)
                    {
                        changed |= ApplyRow(index, row, visible: true);
                        continue;
                    }

                    var placeholder = viewModel.Rows.Count == 0 && index == 0
                        ? viewModel.Status
                        : string.Empty;
                    changed |= ApplyBlankRow(index, placeholder, visible: index < visibleRows);
                }
            }
            finally
            {
                if (layoutChanged)
                {
                    _table.ResumeLayout(changed || layoutChanged);
                    _table.Invalidate();
                }
            }

            succeeded = true;
            return changed || layoutChanged;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayRelativeRows,
                started,
                succeeded);
        }
    }

    private IReadOnlyList<RelativeOverlayRowViewModel?> BuildStableRows(RelativeOverlayViewModel viewModel)
    {
        if (viewModel.Rows.Count == 0)
        {
            return [null];
        }

        var aheadCapacity = Math.Clamp(CarsAhead, 0, 8);
        var behindCapacity = Math.Clamp(CarsBehind, 0, 8);
        var reference = viewModel.Rows.FirstOrDefault(row => row.IsReference);
        var hasReference = reference is not null;
        var visibleRows = Math.Clamp(
            aheadCapacity + behindCapacity + (hasReference ? 1 : 0),
            1,
            MaximumRows);
        var stableRows = new RelativeOverlayRowViewModel?[visibleRows];
        var ahead = viewModel.Rows.Where(row => row.IsAhead).ToArray();
        var aheadStart = Math.Max(0, aheadCapacity - ahead.Length);
        for (var index = 0; index < ahead.Length && aheadStart + index < stableRows.Length; index++)
        {
            stableRows[aheadStart + index] = ahead[index];
        }

        var behindStart = hasReference ? aheadCapacity + 1 : aheadCapacity;
        if (hasReference && aheadCapacity < stableRows.Length)
        {
            stableRows[aheadCapacity] = reference;
        }

        var behind = viewModel.Rows.Where(row => row.IsBehind).ToArray();
        for (var index = 0; index < behind.Length && behindStart + index < stableRows.Length; index++)
        {
            stableRows[behindStart + index] = behind[index];
        }

        if (!hasReference && ahead.Length == 0 && behind.Length == 0)
        {
            return [null];
        }

        return stableRows;
    }

    private bool ApplyRow(int index, RelativeOverlayRowViewModel row, bool visible)
    {
        var changed = false;
        changed |= OverlayChrome.SetVisibleIfChanged(_positionLabels[index], visible);
        changed |= OverlayChrome.SetVisibleIfChanged(_driverLabels[index], visible);
        changed |= OverlayChrome.SetVisibleIfChanged(_gapLabels[index], visible);
        changed |= OverlayChrome.SetVisibleIfChanged(_detailLabels[index], visible);
        changed |= OverlayChrome.SetTextIfChanged(_positionLabels[index], row.Position);
        changed |= OverlayChrome.SetTextIfChanged(_driverLabels[index], row.Driver);
        changed |= OverlayChrome.SetTextIfChanged(_gapLabels[index], row.Gap);
        changed |= OverlayChrome.SetTextIfChanged(_detailLabels[index], row.Detail);

        var backColor = row.IsReference
            ? OverlayTheme.Colors.InfoBackground
            : row.IsPit
                ? PitRowBackground
                : OverlayTheme.Colors.PanelBackground;
        var textColor = row.IsPartial
            ? OverlayTheme.Colors.TextMuted
            : row.IsReference
                ? OverlayTheme.Colors.TextPrimary
                : row.IsPit
                    ? OverlayTheme.Colors.TextMuted
                    : OverlayTheme.Colors.TextSecondary;
        var gapColor = row.IsReference
            ? OverlayTheme.Colors.TextPrimary
            : row.IsPartial
                ? OverlayTheme.Colors.TextMuted
                : row.IsPit
                    ? OverlayTheme.Colors.TextSubtle
                    : row.IsAhead
                        ? OverlayTheme.Colors.InfoText
                        : OverlayTheme.Colors.SuccessText;
        var classColor = TryParseColor(row.ClassColorHex);
        changed |= _positionLabels[index].SetClassColor(row.IsPit
            ? MutedClassColor(classColor)
            : classColor);
        var detailColor = row.IsPit
            ? OverlayTheme.Colors.TextSubtle
            : classColor ?? (row.IsSameClass ? OverlayTheme.Colors.TextSubtle : OverlayTheme.Colors.InfoText);

        changed |= ApplyCellColors(index, backColor, textColor, gapColor, detailColor);
        return changed;
    }

    private bool ApplyBlankRow(int index, string placeholder, bool visible)
    {
        var changed = false;
        changed |= OverlayChrome.SetVisibleIfChanged(_positionLabels[index], visible);
        changed |= OverlayChrome.SetVisibleIfChanged(_driverLabels[index], visible);
        changed |= OverlayChrome.SetVisibleIfChanged(_gapLabels[index], visible);
        changed |= OverlayChrome.SetVisibleIfChanged(_detailLabels[index], visible);
        changed |= OverlayChrome.SetTextIfChanged(_positionLabels[index], string.Empty);
        changed |= OverlayChrome.SetTextIfChanged(_driverLabels[index], placeholder);
        changed |= OverlayChrome.SetTextIfChanged(_gapLabels[index], string.Empty);
        changed |= OverlayChrome.SetTextIfChanged(_detailLabels[index], string.Empty);
        changed |= _positionLabels[index].SetClassColor(null);
        changed |= ApplyCellColors(
            index,
            OverlayTheme.Colors.PanelBackground,
            OverlayTheme.Colors.TextMuted,
            OverlayTheme.Colors.TextMuted,
            OverlayTheme.Colors.TextMuted);
        return changed;
    }

    private bool UpdateVisibleRows(int visibleRows)
    {
        if (_lastVisibleRows == visibleRows)
        {
            return false;
        }

        var changed = OverlayChrome.SetAbsoluteRows(_table, visibleRows, CompactRowHeight);

        _lastVisibleRows = visibleRows;
        return changed;
    }

    private bool UpdateTableHeight(int visibleRows)
    {
        var maximumHeight = Math.Max(CompactRowHeight, ClientSize.Height - OverlayTheme.Layout.OverlayTableWithFooterReservedHeight);
        var desiredHeight = Math.Min(maximumHeight, Math.Max(CompactRowHeight, visibleRows * CompactRowHeight + 2));
        if (_table.Height == desiredHeight)
        {
            return false;
        }

        _table.Height = desiredHeight;
        return true;
    }

    private bool ApplyCellColors(int index, Color backColor, Color textColor, Color gapColor, Color detailColor)
    {
        var changed = false;
        changed |= OverlayChrome.SetBackColorIfChanged(_positionLabels[index], backColor);
        changed |= OverlayChrome.SetBackColorIfChanged(_driverLabels[index], backColor);
        changed |= OverlayChrome.SetBackColorIfChanged(_gapLabels[index], backColor);
        changed |= OverlayChrome.SetBackColorIfChanged(_detailLabels[index], backColor);
        changed |= OverlayChrome.SetForeColorIfChanged(_positionLabels[index], textColor);
        changed |= OverlayChrome.SetForeColorIfChanged(_driverLabels[index], textColor);
        changed |= OverlayChrome.SetForeColorIfChanged(_gapLabels[index], gapColor);
        changed |= OverlayChrome.SetForeColorIfChanged(_detailLabels[index], detailColor);
        return changed;
    }

    private void ReportOverlayError(Exception exception, string stage)
    {
        var message = $"{stage}: {exception.GetType().Name} {exception.Message}";
        _overlayError = message;
        var now = DateTimeOffset.UtcNow;
        if (string.Equals(_lastLoggedError, message, StringComparison.Ordinal)
            && _lastLoggedErrorAtUtc is { } lastLogged
            && now - lastLogged < TimeSpan.FromSeconds(30))
        {
            return;
        }

        _lastLoggedError = message;
        _lastLoggedErrorAtUtc = now;
        _logger.LogWarning(exception, "Relative overlay {Stage} failed.", stage);
    }

    private static OverlayChromeState ChromeStateFor(
        RelativeOverlayViewModel viewModel,
        LiveTelemetrySnapshot snapshot,
        OverlaySettings settings)
    {
        var tone = viewModel.Rows.Count == 0
            ? OverlayChromeTone.Waiting
            : OverlayChromeTone.Info;
        var showStatus = OverlayChromeSettings.ShowHeaderStatus(settings, snapshot);
        var footerMode = OverlayChromeSettings.ShowFooterSource(settings, snapshot)
            ? OverlayChromeFooterMode.Always
            : OverlayChromeFooterMode.Never;
        return new OverlayChromeState(
            "Relative",
            showStatus ? viewModel.Status : string.Empty,
            tone,
            viewModel.Source,
            footerMode);
    }

    private static ClassPositionLabel CreatePositionCellLabel(string fontFamily, string text)
    {
        return new ClassPositionLabel
        {
            AutoSize = false,
            BackColor = OverlayTheme.Colors.PanelBackground,
            Dock = DockStyle.Fill,
            Font = OverlayTheme.Font(fontFamily, 9.2f, FontStyle.Bold),
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Margin = Padding.Empty,
            Padding = new Padding(28, 0, 4, 0),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var value = hex.Trim().TrimStart('#');
        return value.Length == 6
            && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            ? Color.FromArgb((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff)
            : null;
    }

    private static Color? MutedClassColor(Color? color)
    {
        if (color is not { } value)
        {
            return null;
        }

        var background = OverlayTheme.Colors.PanelBackground;
        return Blend(value, background, panelWeight: 1, accentWeight: 2);
    }

    private static Color PitRowBackground
    {
        get
        {
            var panel = OverlayTheme.Colors.PanelBackground;
            return Color.FromArgb(
                panel.A,
                Math.Min(255, panel.R + 10),
                Math.Min(255, panel.G + 10),
                Math.Min(255, panel.B + 10));
        }
    }

    private static Color Blend(Color panel, Color accent, int panelWeight, int accentWeight)
    {
        var total = Math.Max(1, panelWeight + accentWeight);
        return Color.FromArgb(
            (panel.R * panelWeight + accent.R * accentWeight) / total,
            (panel.G * panelWeight + accent.G * accentWeight) / total,
            (panel.B * panelWeight + accent.B * accentWeight) / total);
    }

    private sealed class ClassPositionLabel : Label
    {
        private Color? _classColor;

        public bool SetClassColor(Color? color)
        {
            if (ColorEquals(_classColor, color))
            {
                return false;
            }

            _classColor = color;
            Invalidate();
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_classColor is not { } color || Width <= 18 || Height <= 6)
            {
                return;
            }

            using var brush = new SolidBrush(color);
            var barHeight = Math.Max(10, Height - 10);
            var y = Math.Max(4, (Height - barHeight) / 2);
            e.Graphics.FillRectangle(brush, 8, y, 3, barHeight);
        }

        private static bool ColorEquals(Color? left, Color? right)
        {
            if (left.HasValue != right.HasValue)
            {
                return false;
            }

            return !left.HasValue || left.Value.ToArgb() == right.GetValueOrDefault().ToArgb();
        }
    }
}
