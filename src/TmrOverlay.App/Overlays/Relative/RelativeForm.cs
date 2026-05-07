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
        Padding = new Padding(12);

        _titleLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Font = OverlayTheme.Font(_fontFamily, 11f, FontStyle.Bold),
            Location = new Point(14, 10),
            Size = new Size(150, 24),
            Text = "Relative"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextSubtle,
            Font = OverlayTheme.Font(_fontFamily, 9f),
            Location = new Point(164, 11),
            Size = new Size(ClientSize.Width - 178, 22),
            Text = "waiting",
            TextAlign = ContentAlignment.MiddleRight
        };

        _table = new OverlayTableLayoutPanel
        {
            ColumnCount = 4,
            Location = new Point(14, 42),
            RowCount = MaximumRows,
            Size = new Size(ClientSize.Width - 28, ClientSize.Height - 76)
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17f));
        for (var row = 0; row < MaximumRows; row++)
        {
            _table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / MaximumRows));
            _positionLabels[row] = CreatePositionCellLabel(_fontFamily, "--");
            _driverLabels[row] = CreateCellLabel(_fontFamily, string.Empty);
            _gapLabels[row] = CreateCellLabel(_fontFamily, "--", alignRight: true, monospace: true);
            _detailLabels[row] = CreateCellLabel(_fontFamily, string.Empty, alignRight: true);
            _table.Controls.Add(_positionLabels[row], 0, row);
            _table.Controls.Add(_driverLabels[row], 1, row);
            _table.Controls.Add(_gapLabels[row], 2, row);
            _table.Controls.Add(_detailLabels[row], 3, row);
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
                uiChanged |= SetTextIfChanged(_statusLabel, viewModel.Status);
                uiChanged |= SetTextIfChanged(_sourceLabel, viewModel.Source);
                uiChanged |= ApplyStatusColor(viewModel);
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
            SetTextIfChanged(_statusLabel, "relative error");
            SetTextIfChanged(_sourceLabel, _overlayError);
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
        changed |= SetVisibleIfChanged(_positionLabels[index], visible);
        changed |= SetVisibleIfChanged(_driverLabels[index], visible);
        changed |= SetVisibleIfChanged(_gapLabels[index], visible);
        changed |= SetVisibleIfChanged(_detailLabels[index], visible);
        changed |= SetTextIfChanged(_positionLabels[index], row.Position);
        changed |= SetTextIfChanged(_driverLabels[index], row.Driver);
        changed |= SetTextIfChanged(_gapLabels[index], row.Gap);
        changed |= SetTextIfChanged(_detailLabels[index], row.Detail);

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
        changed |= SetVisibleIfChanged(_positionLabels[index], visible);
        changed |= SetVisibleIfChanged(_driverLabels[index], visible);
        changed |= SetVisibleIfChanged(_gapLabels[index], visible);
        changed |= SetVisibleIfChanged(_detailLabels[index], visible);
        changed |= SetTextIfChanged(_positionLabels[index], string.Empty);
        changed |= SetTextIfChanged(_driverLabels[index], placeholder);
        changed |= SetTextIfChanged(_gapLabels[index], string.Empty);
        changed |= SetTextIfChanged(_detailLabels[index], string.Empty);
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

        var changed = false;
        for (var row = 0; row < _table.RowStyles.Count; row++)
        {
            var sizeType = SizeType.Absolute;
            var height = row < visibleRows ? CompactRowHeight : 0f;
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

        _lastVisibleRows = visibleRows;
        return changed;
    }

    private bool UpdateTableHeight(int visibleRows)
    {
        var maximumHeight = Math.Max(CompactRowHeight, ClientSize.Height - 76);
        var desiredHeight = Math.Min(maximumHeight, Math.Max(CompactRowHeight, visibleRows * CompactRowHeight + 2));
        if (_table.Height == desiredHeight)
        {
            return false;
        }

        _table.Height = desiredHeight;
        return true;
    }

    private bool ApplyStatusColor(RelativeOverlayViewModel viewModel)
    {
        if (_overlayError is not null)
        {
            var errorChanged = SetBackColorIfChanged(this, OverlayTheme.Colors.ErrorBackground);
            errorChanged |= SetForeColorIfChanged(_statusLabel, OverlayTheme.Colors.ErrorText);
            return errorChanged;
        }

        if (viewModel.Rows.Count == 0)
        {
            var waitingChanged = SetBackColorIfChanged(this, OverlayTheme.Colors.WindowBackground);
            waitingChanged |= SetForeColorIfChanged(_statusLabel, OverlayTheme.Colors.TextSubtle);
            return waitingChanged;
        }

        var changed = SetBackColorIfChanged(this, OverlayTheme.Colors.InfoBackground);
        changed |= SetForeColorIfChanged(_statusLabel, OverlayTheme.Colors.InfoText);
        return changed;
    }

    private bool ApplyCellColors(int index, Color backColor, Color textColor, Color gapColor, Color detailColor)
    {
        var changed = false;
        changed |= SetBackColorIfChanged(_positionLabels[index], backColor);
        changed |= SetBackColorIfChanged(_driverLabels[index], backColor);
        changed |= SetBackColorIfChanged(_gapLabels[index], backColor);
        changed |= SetBackColorIfChanged(_detailLabels[index], backColor);
        changed |= SetForeColorIfChanged(_positionLabels[index], textColor);
        changed |= SetForeColorIfChanged(_driverLabels[index], textColor);
        changed |= SetForeColorIfChanged(_gapLabels[index], gapColor);
        changed |= SetForeColorIfChanged(_detailLabels[index], detailColor);
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

    private static Label CreateCellLabel(
        string fontFamily,
        string text,
        bool alignRight = false,
        bool bold = false,
        bool monospace = false)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = OverlayTheme.Colors.PanelBackground,
            Dock = DockStyle.Fill,
            Font = monospace
                ? new Font(FontFamily.GenericMonospace, bold ? 9f : 8.8f, bold ? FontStyle.Bold : FontStyle.Regular)
                : OverlayTheme.Font(fontFamily, bold ? 9.2f : 8.8f, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = bold ? OverlayTheme.Colors.TextPrimary : OverlayTheme.Colors.TextSecondary,
            Margin = Padding.Empty,
            Padding = new Padding(7, 0, 7, 0),
            Text = text,
            TextAlign = alignRight ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
        };
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
