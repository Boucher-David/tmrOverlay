using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.Standings;

internal sealed class StandingsForm : PersistentOverlayForm
{
    private const int MaximumRows = 8;
    private const int MinimumTableHeight = 210;
    private const int RefreshIntervalMilliseconds = 500;
    private static readonly string[] Columns = ["CLS", "CAR", "DRIVER", "GAP", "INT", "PIT"];
    private static readonly float[] ColumnWidths = [10f, 10f, 40f, 16f, 16f, 8f];

    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger<StandingsForm> _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly string _fontFamily;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly TableLayoutPanel _table;
    private readonly Label _sourceLabel;
    private readonly Label[] _headerLabels = new Label[Columns.Length];
    private readonly Label[,] _rowLabels = new Label[MaximumRows, 6];
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly OverlaySettings _settings;
    private long? _lastRefreshSequence;
    private string? _overlayError;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;

    public StandingsForm(
        ILiveTelemetrySource liveTelemetrySource,
        ILogger<StandingsForm> logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            StandingsOverlayDefinition.Definition.DefaultWidth,
            StandingsOverlayDefinition.Definition.DefaultHeight)
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
            Size = new Size(160, 24),
            Text = "Standings"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextSubtle,
            Font = OverlayTheme.Font(_fontFamily, 9f),
            Location = new Point(174, 11),
            Size = new Size(ClientSize.Width - 188, 22),
            Text = "waiting",
            TextAlign = ContentAlignment.MiddleRight
        };

        _table = new TableLayoutPanel
        {
            BackColor = OverlayTheme.Colors.PanelBackground,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            ColumnCount = Columns.Length,
            Location = new Point(14, 42),
            RowCount = MaximumRows + 1,
            Size = new Size(ClientSize.Width - 28, ClientSize.Height - 76)
        };

        foreach (var width in ColumnWidths)
        {
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        }

        for (var row = 0; row <= MaximumRows; row++)
        {
            _table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / (MaximumRows + 1)));
        }

        for (var column = 0; column < Columns.Length; column++)
        {
            _headerLabels[column] = CreateCellLabel(
                Columns[column],
                bold: true,
                alignRight: column != 2,
                muted: true);
            _table.Controls.Add(_headerLabels[column], column, 0);
        }

        for (var row = 0; row < MaximumRows; row++)
        {
            for (var column = 0; column < Columns.Length; column++)
            {
                _rowLabels[row, column] = CreateCellLabel(
                    string.Empty,
                    alignRight: column != 2,
                    monospace: column is 0 or 1 or 3 or 4 or 5);
                _table.Controls.Add(_rowLabels[row, column], column, row + 1);
            }
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
        RegisterDragSurfaces(_headerLabels.Concat(RowLabels()).ToArray());

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

        _statusLabel.Location = new Point(174, 11);
        _statusLabel.Size = new Size(Math.Max(120, ClientSize.Width - 188), 22);
        _table.Location = new Point(14, 42);
        _table.Size = new Size(
            Math.Max(420, ClientSize.Width - 28),
            Math.Max(MinimumTableHeight, ClientSize.Height - 76));
        _sourceLabel.Location = new Point(14, ClientSize.Height - 28);
        _sourceLabel.Size = new Size(Math.Max(420, ClientSize.Width - 28), 18);
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
            _performanceState.RecordOperation(AppPerformanceMetricIds.OverlayStandingsPaint, started, succeeded);
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
                    AppPerformanceMetricIds.OverlayStandingsSnapshot,
                    snapshotStarted,
                    snapshotSucceeded);
            }

            var now = DateTimeOffset.UtcNow;
            var previousSequence = _lastRefreshSequence;
            if (previousSequence == snapshot.Sequence && _overlayError is null)
            {
                _performanceState.RecordOverlayRefreshDecision(
                    StandingsOverlayDefinition.Definition.Id,
                    now,
                    previousSequence,
                    snapshot.Sequence,
                    snapshot.LastUpdatedAtUtc,
                    applied: false);
                succeeded = true;
                return;
            }

            var viewModelStarted = Stopwatch.GetTimestamp();
            var viewModelSucceeded = false;
            StandingsOverlayViewModel viewModel;
            try
            {
                viewModel = StandingsOverlayViewModel.From(
                    snapshot,
                    DateTimeOffset.UtcNow,
                    MaximumRows,
                    OtherClassRowsPerClass());
                viewModelSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayStandingsViewModel,
                    viewModelStarted,
                    viewModelSucceeded);
            }

            var applyStarted = Stopwatch.GetTimestamp();
            var applySucceeded = false;
            var uiChanged = false;
            try
            {
                _overlayError = null;
                uiChanged = ApplyViewModel(viewModel);
                _lastRefreshSequence = snapshot.Sequence;
                applySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayStandingsApplyUi,
                    applyStarted,
                    applySucceeded);
            }

            _performanceState.RecordOverlayRefreshDecision(
                StandingsOverlayDefinition.Definition.Id,
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
        }
        finally
        {
            _performanceState.RecordOperation(AppPerformanceMetricIds.OverlayStandingsRefresh, started, succeeded);
        }
    }

    private bool ApplyViewModel(StandingsOverlayViewModel viewModel)
    {
        var changed = false;
        SuspendLayout();
        _table.SuspendLayout();
        try
        {
            changed |= SetLabelText(_statusLabel, viewModel.Status);
            changed |= SetLabelText(_sourceLabel, viewModel.Source);

            var populatedRows = Math.Min(MaximumRows, viewModel.Rows.Count);
            for (var row = 0; row < populatedRows; row++)
            {
                changed |= ApplyRow(row, viewModel.Rows[row]);
            }

            for (var row = populatedRows; row < MaximumRows; row++)
            {
                changed |= ClearRow(row);
            }

            var backgroundColor = _overlayError is null
                ? OverlayTheme.Colors.WindowBackground
                : OverlayTheme.Colors.ErrorBackground;
            if (BackColor != backgroundColor)
            {
                BackColor = backgroundColor;
                changed = true;
            }
        }
        finally
        {
            _table.ResumeLayout(performLayout: false);
            ResumeLayout(performLayout: false);
        }

        return changed;
    }

    private Label[] RowLabels()
    {
        var labels = new Label[MaximumRows * Columns.Length];
        var labelIndex = 0;
        for (var row = 0; row < MaximumRows; row++)
        {
            for (var column = 0; column < Columns.Length; column++)
            {
                labels[labelIndex++] = _rowLabels[row, column];
            }
        }

        return labels;
    }

    private bool ApplyRow(int index, StandingsOverlayRowViewModel row)
    {
        var changed = false;
        var values = new[]
        {
            row.ClassPosition,
            row.CarNumber,
            row.Driver,
            row.Gap,
            row.Interval,
            row.Pit
        };

        for (var column = 0; column < Columns.Length; column++)
        {
            changed |= SetLabelText(_rowLabels[index, column], values[column]);
            changed |= SetLabelColor(_rowLabels[index, column], TextColor(row, column));
        }

        return changed;
    }

    private int OtherClassRowsPerClass()
    {
        return _settings.GetIntegerOption(
            OverlayOptionKeys.StandingsOtherClassRows,
            defaultValue: 2,
            minimum: 0,
            maximum: 6);
    }

    private static Color TextColor(StandingsOverlayRowViewModel row, int column)
    {
        if (row.IsClassHeader)
        {
            return column == 2
                ? OverlayTheme.Colors.TextPrimary
                : OverlayTheme.Colors.TextMuted;
        }

        if (row.IsPartial)
        {
            return OverlayTheme.Colors.TextMuted;
        }

        if (column == 5 && !string.IsNullOrEmpty(row.Pit))
        {
            return OverlayTheme.Colors.WarningIndicator;
        }

        if (row.IsReference)
        {
            return Color.FromArgb(255, 218, 89);
        }

        if (row.IsLeader && column == 3)
        {
            return OverlayTheme.Colors.SuccessText;
        }

        return column == 2
            ? OverlayTheme.Colors.TextSecondary
            : OverlayTheme.Colors.TextPrimary;
    }

    private bool ClearRow(int row)
    {
        var changed = false;
        for (var column = 0; column < Columns.Length; column++)
        {
            changed |= SetLabelText(_rowLabels[row, column], string.Empty);
            changed |= SetLabelColor(_rowLabels[row, column], OverlayTheme.Colors.TextPrimary);
        }

        return changed;
    }

    private static bool SetLabelText(Label label, string text)
    {
        if (string.Equals(label.Text, text, StringComparison.Ordinal))
        {
            return false;
        }

        label.Text = text;
        return true;
    }

    private static bool SetLabelColor(Label label, Color color)
    {
        if (label.ForeColor == color)
        {
            return false;
        }

        label.ForeColor = color;
        return true;
    }

    private Label CreateCellLabel(
        string text,
        bool bold = false,
        bool alignRight = false,
        bool monospace = false,
        bool muted = false)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            ForeColor = muted ? OverlayTheme.Colors.TextMuted : OverlayTheme.Colors.TextPrimary,
            Font = monospace
                ? OverlayTheme.MonospaceFont(8.5f, bold ? FontStyle.Bold : FontStyle.Regular)
                : OverlayTheme.Font(_fontFamily, 8.8f, bold ? FontStyle.Bold : FontStyle.Regular),
            Margin = Padding.Empty,
            Padding = new Padding(5, 2, 5, 2),
            Text = text,
            TextAlign = alignRight ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
        };
    }

    private void ReportOverlayError(Exception exception, string stage)
    {
        var now = DateTimeOffset.UtcNow;
        var message = exception.GetType().Name;
        if (!string.Equals(_lastLoggedError, message, StringComparison.Ordinal)
            || _lastLoggedErrorAtUtc is null
            || now - _lastLoggedErrorAtUtc.Value > TimeSpan.FromSeconds(30))
        {
            _logger.LogWarning(exception, "Standings overlay {Stage} failed.", stage);
            _lastLoggedError = message;
            _lastLoggedErrorAtUtc = now;
        }

        _overlayError = message;
        _statusLabel.Text = "overlay error";
        _sourceLabel.Text = $"source: error ({message})";
        for (var row = 0; row < MaximumRows; row++)
        {
            ClearRow(row);
        }

        _rowLabels[0, 0].Text = "ERR";
        _rowLabels[0, 2].Text = message;
        _rowLabels[0, 2].ForeColor = OverlayTheme.Colors.ErrorIndicator;
        BackColor = OverlayTheme.Colors.ErrorBackground;
        Invalidate();
    }
}
