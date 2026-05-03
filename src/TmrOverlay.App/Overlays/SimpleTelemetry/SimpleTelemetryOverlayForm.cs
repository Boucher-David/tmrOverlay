using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.SimpleTelemetry;

internal sealed class SimpleTelemetryOverlayForm : PersistentOverlayForm
{
    private const int MaximumRows = 8;
    private const int MinimumTableHeight = 128;
    private const int RefreshIntervalMilliseconds = 500;

    private readonly OverlayDefinition _definition;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger<SimpleTelemetryOverlayForm> _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly OverlaySettings _settings;
    private readonly string _fontFamily;
    private readonly string _unitSystem;
    private readonly SimpleTelemetryOverlayMetrics _metrics;
    private readonly Func<LiveTelemetrySnapshot, DateTimeOffset, string, SimpleTelemetryOverlayViewModel> _buildViewModel;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly TableLayoutPanel _table;
    private readonly Label _sourceLabel;
    private readonly Label[] _labelCells = new Label[MaximumRows];
    private readonly Label[] _valueCells = new Label[MaximumRows];
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private long? _lastRefreshSequence;
    private int _lastVisibleRows = -1;
    private string? _overlayError;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;

    public SimpleTelemetryOverlayForm(
        OverlayDefinition definition,
        ILiveTelemetrySource liveTelemetrySource,
        ILogger<SimpleTelemetryOverlayForm> logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        string unitSystem,
        SimpleTelemetryOverlayMetrics metrics,
        Func<LiveTelemetrySnapshot, DateTimeOffset, string, SimpleTelemetryOverlayViewModel> buildViewModel,
        Action saveSettings)
        : base(settings, saveSettings, definition.DefaultWidth, definition.DefaultHeight)
    {
        _definition = definition;
        _liveTelemetrySource = liveTelemetrySource;
        _logger = logger;
        _performanceState = performanceState;
        _settings = settings;
        _fontFamily = fontFamily;
        _unitSystem = unitSystem;
        _metrics = metrics;
        _buildViewModel = buildViewModel;

        BackColor = OverlayTheme.Colors.WindowBackground;
        Padding = new Padding(12);

        _titleLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Font = OverlayTheme.Font(_fontFamily, 11f, FontStyle.Bold),
            Location = new Point(14, 10),
            Size = new Size(190, 24),
            Text = _definition.DisplayName
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextSubtle,
            Font = OverlayTheme.Font(_fontFamily, 9f),
            Location = new Point(204, 11),
            Size = new Size(ClientSize.Width - 218, 22),
            Text = "waiting",
            TextAlign = ContentAlignment.MiddleRight
        };

        _table = new TableLayoutPanel
        {
            BackColor = OverlayTheme.Colors.PanelBackground,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            ColumnCount = 2,
            Location = new Point(14, 42),
            RowCount = MaximumRows,
            Size = new Size(ClientSize.Width - 28, ClientSize.Height - 76)
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));

        for (var row = 0; row < MaximumRows; row++)
        {
            _table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / MaximumRows));
            _labelCells[row] = CreateCellLabel(_fontFamily, string.Empty, bold: true);
            _valueCells[row] = CreateCellLabel(_fontFamily, string.Empty, alignRight: true, monospace: true);
            _table.Controls.Add(_labelCells[row], 0, row);
            _table.Controls.Add(_valueCells[row], 1, row);
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
        ApplySourceFooterLayout();

        RegisterDragSurfaces(_titleLabel, _statusLabel, _table, _sourceLabel);
        RegisterDragSurfaces(_labelCells.Concat(_valueCells).ToArray());

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

        _statusLabel.Location = new Point(204, 11);
        _statusLabel.Size = new Size(Math.Max(100, ClientSize.Width - 218), 22);
        ApplySourceFooterLayout();
    }

    private bool ApplySourceFooterLayout()
    {
        if (_table is null || _sourceLabel is null)
        {
            return false;
        }

        var showSourceFooter = ShowSourceFooter;
        var changed = SetVisibleIfChanged(_sourceLabel, showSourceFooter);
        _table.Location = new Point(14, 42);
        changed |= SetSizeIfChanged(
            _table,
            new Size(
                Math.Max(240, ClientSize.Width - 28),
                Math.Max(MinimumTableHeight, ClientSize.Height - (showSourceFooter ? 76 : 56))));
        _sourceLabel.Location = new Point(14, ClientSize.Height - 28);
        changed |= SetSizeIfChanged(_sourceLabel, new Size(Math.Max(240, ClientSize.Width - 28), 18));
        return changed;
    }

    private bool ShowSourceFooter =>
        _overlayError is not null;

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
            _performanceState.RecordOperation(_metrics.Paint, started, succeeded);
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
                _performanceState.RecordOperation(_metrics.Snapshot, snapshotStarted, snapshotSucceeded);
            }

            var now = DateTimeOffset.UtcNow;
            var previousSequence = _lastRefreshSequence;
            SimpleTelemetryOverlayViewModel viewModel;
            var viewModelStarted = Stopwatch.GetTimestamp();
            var viewModelSucceeded = false;
            try
            {
                viewModel = _buildViewModel(snapshot, now, _unitSystem);
                viewModelSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(_metrics.ViewModel, viewModelStarted, viewModelSucceeded);
            }

            var applyStarted = Stopwatch.GetTimestamp();
            var applySucceeded = false;
            var uiChanged = false;
            try
            {
                _overlayError = null;
                uiChanged |= SetTextIfChanged(_titleLabel, viewModel.Title);
                uiChanged |= SetTextIfChanged(_statusLabel, viewModel.Status);
                uiChanged |= SetTextIfChanged(_sourceLabel, viewModel.Source);
                uiChanged |= ApplySourceFooterLayout();
                uiChanged |= ApplyTone(viewModel.Tone);
                uiChanged |= ApplyRows(viewModel);
                applySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(_metrics.ApplyUi, applyStarted, applySucceeded);
            }

            _lastRefreshSequence = snapshot.Sequence;
            _performanceState.RecordOverlayRefreshDecision(
                _definition.Id,
                now,
                previousSequence,
                snapshot.Sequence,
                snapshot.LastUpdatedAtUtc,
                applied: uiChanged);
            if (uiChanged)
            {
                Invalidate();
            }

            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "refresh");
            SetTextIfChanged(_statusLabel, $"{_definition.DisplayName.ToLowerInvariant()} error");
            SetTextIfChanged(_sourceLabel, _overlayError);
            ApplySourceFooterLayout();
            ApplyTone(SimpleTelemetryTone.Error);
            Invalidate();
        }
        finally
        {
            _performanceState.RecordOperation(_metrics.Refresh, started, succeeded);
        }
    }

    private bool ApplyRows(SimpleTelemetryOverlayViewModel viewModel)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var rows = viewModel.Rows.Take(MaximumRows).ToArray();
            var visibleRows = Math.Clamp(rows.Length == 0 ? 1 : rows.Length, 1, MaximumRows);
            var changed = false;

            _table.SuspendLayout();
            try
            {
                changed |= UpdateVisibleRows(visibleRows);
                for (var index = 0; index < MaximumRows; index++)
                {
                    if (index < rows.Length)
                    {
                        changed |= ApplyRow(index, rows[index], visible: index < visibleRows);
                        continue;
                    }

                    var placeholder = rows.Length == 0 && index == 0
                        ? viewModel.Status
                        : string.Empty;
                    changed |= ApplyBlankRow(index, placeholder, visible: index < visibleRows);
                }
            }
            finally
            {
                _table.ResumeLayout(changed);
            }

            succeeded = true;
            return changed;
        }
        finally
        {
            _performanceState.RecordOperation(_metrics.Rows, started, succeeded);
        }
    }

    private bool ApplyRow(int index, SimpleTelemetryRowViewModel row, bool visible)
    {
        var changed = false;
        changed |= SetVisibleIfChanged(_labelCells[index], visible);
        changed |= SetVisibleIfChanged(_valueCells[index], visible);
        changed |= SetTextIfChanged(_labelCells[index], row.Label);
        changed |= SetTextIfChanged(_valueCells[index], row.Value);

        var (backColor, textColor, valueColor) = RowColors(row.Tone);
        changed |= SetBackColorIfChanged(_labelCells[index], backColor);
        changed |= SetBackColorIfChanged(_valueCells[index], backColor);
        changed |= SetForeColorIfChanged(_labelCells[index], textColor);
        changed |= SetForeColorIfChanged(_valueCells[index], valueColor);
        return changed;
    }

    private bool ApplyBlankRow(int index, string placeholder, bool visible)
    {
        var changed = false;
        changed |= SetVisibleIfChanged(_labelCells[index], visible);
        changed |= SetVisibleIfChanged(_valueCells[index], visible);
        changed |= SetTextIfChanged(_labelCells[index], string.Empty);
        changed |= SetTextIfChanged(_valueCells[index], placeholder);
        changed |= SetBackColorIfChanged(_labelCells[index], OverlayTheme.Colors.PanelBackground);
        changed |= SetBackColorIfChanged(_valueCells[index], OverlayTheme.Colors.PanelBackground);
        changed |= SetForeColorIfChanged(_labelCells[index], OverlayTheme.Colors.TextMuted);
        changed |= SetForeColorIfChanged(_valueCells[index], OverlayTheme.Colors.TextMuted);
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

        _lastVisibleRows = visibleRows;
        return changed;
    }

    private bool ApplyTone(SimpleTelemetryTone tone)
    {
        var backColor = tone switch
        {
            SimpleTelemetryTone.Error => OverlayTheme.Colors.ErrorBackground,
            SimpleTelemetryTone.Warning => OverlayTheme.Colors.WarningStrongBackground,
            SimpleTelemetryTone.Success => OverlayTheme.Colors.SuccessBackground,
            SimpleTelemetryTone.Info => OverlayTheme.Colors.InfoBackground,
            _ => OverlayTheme.Colors.WindowBackground
        };
        var statusColor = tone switch
        {
            SimpleTelemetryTone.Error => OverlayTheme.Colors.ErrorText,
            SimpleTelemetryTone.Warning => OverlayTheme.Colors.WarningText,
            SimpleTelemetryTone.Success => OverlayTheme.Colors.SuccessText,
            SimpleTelemetryTone.Info => OverlayTheme.Colors.InfoText,
            _ => OverlayTheme.Colors.TextSubtle
        };

        var changed = SetBackColorIfChanged(this, backColor);
        changed |= SetForeColorIfChanged(_statusLabel, statusColor);
        return changed;
    }

    private static (Color BackColor, Color TextColor, Color ValueColor) RowColors(SimpleTelemetryTone tone)
    {
        return tone switch
        {
            SimpleTelemetryTone.Error => (
                OverlayTheme.Colors.ErrorBackground,
                OverlayTheme.Colors.TextSecondary,
                OverlayTheme.Colors.ErrorText),
            SimpleTelemetryTone.Warning => (
                OverlayTheme.Colors.WarningBackground,
                OverlayTheme.Colors.TextSecondary,
                OverlayTheme.Colors.WarningText),
            SimpleTelemetryTone.Success => (
                OverlayTheme.Colors.SuccessBackground,
                OverlayTheme.Colors.TextSecondary,
                OverlayTheme.Colors.SuccessText),
            SimpleTelemetryTone.Info => (
                OverlayTheme.Colors.InfoBackground,
                OverlayTheme.Colors.TextSecondary,
                OverlayTheme.Colors.InfoText),
            SimpleTelemetryTone.Waiting => (
                OverlayTheme.Colors.PanelBackground,
                OverlayTheme.Colors.TextMuted,
                OverlayTheme.Colors.TextMuted),
            _ => (
                OverlayTheme.Colors.PanelBackground,
                OverlayTheme.Colors.TextSecondary,
                OverlayTheme.Colors.TextPrimary)
        };
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
        _logger.LogWarning(exception, "{Overlay} overlay {Stage} failed.", _definition.DisplayName, stage);
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

    private static bool SetSizeIfChanged(Control control, Size size)
    {
        if (control.Size == size)
        {
            return false;
        }

        control.Size = size;
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
}
