using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.InputState;

internal sealed class InputStateOverlayForm : PersistentOverlayForm
{
    private const int RefreshIntervalMilliseconds = 50;
    private const int MaximumTracePoints = 180;
    private const int CompactLayoutWidthThreshold = 430;
    private const int CompactLayoutHeightThreshold = 250;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly string _fontFamily;
    private readonly string _unitSystem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly List<InputTracePoint> _trace = [];
    private LiveInputTelemetryModel _latestInputs = LiveInputTelemetryModel.Empty;
    private string _status = "waiting";
    private long? _lastRefreshSequence;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;

    public InputStateOverlayForm(
        ILiveTelemetrySource liveTelemetrySource,
        ILogger logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        string unitSystem,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            InputStateOverlayDefinition.Definition.DefaultWidth,
            InputStateOverlayDefinition.Definition.DefaultHeight)
    {
        _liveTelemetrySource = liveTelemetrySource;
        _logger = logger;
        _performanceState = performanceState;
        _fontFamily = fontFamily;
        _unitSystem = unitSystem;
        BackColor = OverlayTheme.Colors.WindowBackground;

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds
        };
        _refreshTimer.Tick += (_, _) => RefreshOverlay();
        _refreshTimer.Start();

        RefreshOverlay();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
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
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            DrawHeader(e.Graphics);
            if (UseCompactLayout())
            {
                DrawCompactState(e.Graphics);
            }
            else
            {
                DrawPedalTraces(e.Graphics);
                DrawWheel(e.Graphics);
                DrawCarState(e.Graphics);
            }

            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "render");
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayInputStatePaint,
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
                    AppPerformanceMetricIds.OverlayInputStateSnapshot,
                    snapshotStarted,
                    snapshotSucceeded);
            }

            var previousSequence = _lastRefreshSequence;
            var now = DateTimeOffset.UtcNow;
            if (!snapshot.IsConnected || !snapshot.IsCollecting)
            {
                _status = "waiting for iRacing";
            }
            else if (snapshot.LastUpdatedAtUtc is not { } updatedAt || Math.Abs((now - updatedAt).TotalSeconds) > 1.5d)
            {
                _status = "waiting for fresh telemetry";
            }
            else if (!snapshot.Models.Inputs.HasData)
            {
                _status = "waiting for inputs";
            }
            else
            {
                _latestInputs = snapshot.Models.Inputs;
                _status = FormatStatus(_latestInputs);
                AddTracePoint(_latestInputs);
            }

            _lastRefreshSequence = snapshot.Sequence;
            _performanceState.RecordOverlayRefreshDecision(
                InputStateOverlayDefinition.Definition.Id,
                now,
                previousSequence,
                snapshot.Sequence,
                snapshot.LastUpdatedAtUtc,
                applied: true);
            Invalidate();
            succeeded = true;
        }
        catch (Exception exception)
        {
            _status = "input error";
            ReportOverlayError(exception, "refresh");
            Invalidate();
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayInputStateRefresh,
                started,
                succeeded);
        }
    }

    private void AddTracePoint(LiveInputTelemetryModel inputs)
    {
        _trace.Add(new InputTracePoint(
            Throttle: Clamp01(inputs.Throttle),
            Brake: Clamp01(inputs.Brake),
            Clutch: Clamp01(inputs.Clutch)));
        if (_trace.Count > MaximumTracePoints)
        {
            _trace.RemoveRange(0, _trace.Count - MaximumTracePoints);
        }
    }

    private void DrawHeader(Graphics graphics)
    {
        using var titleFont = OverlayTheme.Font(_fontFamily, 11f, FontStyle.Bold);
        using var statusFont = OverlayTheme.Font(_fontFamily, 9f);
        using var titleBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        using var statusBrush = new SolidBrush(OverlayTheme.Colors.TextSubtle);
        graphics.DrawString("Inputs", titleFont, titleBrush, 14, 10);
        DrawRightAligned(graphics, _status, statusFont, statusBrush, new RectangleF(130, 11, Width - 144, 20));
    }

    private void DrawPedalTraces(Graphics graphics)
    {
        var graph = new Rectangle(14, 44, Math.Max(180, Width - 190), Math.Max(118, Height - 72));
        using var background = new SolidBrush(OverlayTheme.Colors.PanelBackground);
        graphics.FillRectangle(background, graph);
        using var border = new Pen(OverlayTheme.Colors.WindowBorder);
        graphics.DrawRectangle(border, graph);

        using var gridPen = new Pen(Color.FromArgb(46, 255, 255, 255));
        for (var step = 1; step < 4; step++)
        {
            var y = graph.Top + graph.Height * step / 4;
            graphics.DrawLine(gridPen, graph.Left, y, graph.Right, y);
        }

        DrawTrace(graphics, graph, point => point.Throttle, Color.FromArgb(48, 214, 109));
        DrawTrace(graphics, graph, point => point.Brake, Color.FromArgb(236, 112, 99));
        DrawTrace(graphics, graph, point => point.Clutch, Color.FromArgb(104, 193, 255));

        using var font = OverlayTheme.Font(_fontFamily, 8.6f, FontStyle.Bold);
        DrawLegend(graphics, graph, font);
    }

    private void DrawCompactState(Graphics graphics)
    {
        var content = new Rectangle(
            14,
            42,
            Math.Max(160, ClientSize.Width - 28),
            Math.Max(120, ClientSize.Height - 54));
        using var background = new SolidBrush(OverlayTheme.Colors.PanelBackground);
        using var border = new Pen(OverlayTheme.Colors.WindowBorder);
        graphics.FillRectangle(background, content);
        graphics.DrawRectangle(border, content);

        var pedalHeight = Math.Min(76, Math.Max(58, content.Height / 2 - 8));
        var pedalRect = new Rectangle(content.Left + 10, content.Top + 8, content.Width - 20, pedalHeight);
        DrawCompactPedalBars(graphics, pedalRect);

        var readoutTop = pedalRect.Bottom + 8;
        var readoutRect = new Rectangle(
            content.Left + 10,
            readoutTop,
            content.Width - 20,
            Math.Max(44, content.Bottom - readoutTop - 8));
        DrawCompactReadouts(graphics, readoutRect);
    }

    private void DrawCompactPedalBars(Graphics graphics, Rectangle bounds)
    {
        using var labelFont = OverlayTheme.Font(_fontFamily, 8.2f, FontStyle.Bold);
        using var valueFont = new Font(FontFamily.GenericMonospace, 8.2f);
        var rowHeight = Math.Max(16, bounds.Height / 3);
        DrawCompactBar(graphics, bounds.Left, bounds.Top, bounds.Width, rowHeight, "T", Clamp01(_latestInputs.Throttle), Color.FromArgb(48, 214, 109), labelFont, valueFont);
        DrawCompactBar(graphics, bounds.Left, bounds.Top + rowHeight, bounds.Width, rowHeight, "B", Clamp01(_latestInputs.Brake), Color.FromArgb(236, 112, 99), labelFont, valueFont);
        DrawCompactBar(graphics, bounds.Left, bounds.Top + rowHeight * 2, bounds.Width, rowHeight, "C", Clamp01(_latestInputs.Clutch), Color.FromArgb(104, 193, 255), labelFont, valueFont);
    }

    private static void DrawCompactBar(
        Graphics graphics,
        int x,
        int y,
        int width,
        int height,
        string label,
        double? value,
        Color color,
        Font labelFont,
        Font valueFont)
    {
        var labelWidth = 22;
        var valueWidth = 44;
        var barRect = new Rectangle(
            x + labelWidth,
            y + Math.Max(4, height / 2 - 4),
            Math.Max(20, width - labelWidth - valueWidth - 8),
            8);
        using var labelBrush = new SolidBrush(OverlayTheme.Colors.TextSubtle);
        using var trackBrush = new SolidBrush(Color.FromArgb(42, 255, 255, 255));
        using var valueBrush = new SolidBrush(color);
        using var valueTextBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        graphics.DrawString(label, labelFont, labelBrush, x, y + Math.Max(0, (height - 15) / 2));
        graphics.FillRectangle(trackBrush, barRect);

        var normalized = Math.Clamp(value ?? 0d, 0d, 1d);
        if (normalized > 0d)
        {
            graphics.FillRectangle(valueBrush, barRect.Left, barRect.Top, (int)Math.Round(barRect.Width * normalized), barRect.Height);
        }

        DrawText(
            graphics,
            FormatPercent(value),
            valueFont,
            valueTextBrush,
            new RectangleF(barRect.Right + 8, y, valueWidth, height),
            ContentAlignment.MiddleRight);
    }

    private void DrawCompactReadouts(Graphics graphics, Rectangle bounds)
    {
        using var labelFont = OverlayTheme.Font(_fontFamily, 7.8f);
        using var valueFont = new Font(FontFamily.GenericMonospace, 8.1f);
        using var labelBrush = new SolidBrush(OverlayTheme.Colors.TextSubtle);
        using var valueBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        var rows = new[]
        {
            ("Speed", SimpleTelemetryOverlayViewModel.FormatSpeed(_latestInputs.SpeedMetersPerSecond, _unitSystem)),
            ("Gear", FormatGear(_latestInputs.Gear)),
            ("RPM", FormatRpm(_latestInputs.Rpm)),
            ("Steer", FormatSteering(_latestInputs.SteeringWheelAngle)),
            ("Water", SimpleTelemetryOverlayViewModel.FormatTemperature(_latestInputs.WaterTempC, _unitSystem)),
            ("Oil", SimpleTelemetryOverlayViewModel.FormatPressure(_latestInputs.OilPressureBar, _unitSystem))
        };
        var columnCount = bounds.Width >= 300 ? 3 : 2;
        var rowCount = (int)Math.Ceiling(rows.Length / (double)columnCount);
        var cellWidth = Math.Max(70, bounds.Width / columnCount);
        var cellHeight = Math.Max(18, bounds.Height / Math.Max(1, rowCount));
        for (var index = 0; index < rows.Length; index++)
        {
            var column = index % columnCount;
            var row = index / columnCount;
            var cell = new RectangleF(
                bounds.Left + column * cellWidth,
                bounds.Top + row * cellHeight,
                Math.Min(cellWidth, bounds.Right - (bounds.Left + column * cellWidth)),
                cellHeight);
            DrawText(graphics, rows[index].Item1, labelFont, labelBrush, new RectangleF(cell.Left, cell.Top, 42, cell.Height), ContentAlignment.MiddleLeft);
            DrawText(graphics, rows[index].Item2, valueFont, valueBrush, new RectangleF(cell.Left + 42, cell.Top, Math.Max(20, cell.Width - 44), cell.Height), ContentAlignment.MiddleRight);
        }
    }

    private void DrawTrace(Graphics graphics, Rectangle graph, Func<InputTracePoint, double?> select, Color color)
    {
        var points = _trace
            .Select((point, index) => (Value: select(point), Index: index))
            .Where(point => point.Value is { } value && value >= 0d)
            .Select(point =>
            {
                var x = graph.Left + (float)point.Index / Math.Max(1, MaximumTracePoints - 1) * graph.Width;
                var y = graph.Bottom - (float)point.Value!.Value * graph.Height;
                return new PointF(x, y);
            })
            .ToArray();
        if (points.Length < 2)
        {
            return;
        }

        using var pen = new Pen(color, 2f);
        graphics.DrawLines(pen, points);
    }

    private void DrawLegend(Graphics graphics, Rectangle graph, Font font)
    {
        var x = graph.Left + 8;
        DrawLegendItem(graphics, "Throttle", Color.FromArgb(48, 214, 109), font, ref x, graph.Top + 8);
        DrawLegendItem(graphics, "Brake", Color.FromArgb(236, 112, 99), font, ref x, graph.Top + 8);
        DrawLegendItem(graphics, "Clutch", Color.FromArgb(104, 193, 255), font, ref x, graph.Top + 8);
    }

    private void DrawLegendItem(Graphics graphics, string label, Color color, Font font, ref int x, int y)
    {
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, x, y + 5, 14, 3);
        x += 18;
        graphics.DrawString(label, font, brush, x, y);
        x += (int)graphics.MeasureString(label, font).Width + 14;
    }

    private void DrawWheel(Graphics graphics)
    {
        var wheelRect = new Rectangle(Math.Max(Width - 150, 280), 56, 108, 108);
        var center = new PointF(wheelRect.Left + wheelRect.Width / 2f, wheelRect.Top + wheelRect.Height / 2f);
        using var rimPen = new Pen(OverlayTheme.Colors.TextSecondary, 4f);
        graphics.DrawEllipse(rimPen, wheelRect);
        var angle = (float)(_latestInputs.SteeringWheelAngle ?? 0d);
        var spokeLength = wheelRect.Width * 0.4f;
        using var spokePen = new Pen(OverlayTheme.Colors.InfoText, 3f);
        for (var spoke = 0; spoke < 3; spoke++)
        {
            var theta = angle + (float)(spoke * Math.PI * 2d / 3d) - (float)Math.PI / 2f;
            graphics.DrawLine(
                spokePen,
                center,
                new PointF(
                    center.X + MathF.Cos(theta) * spokeLength,
                    center.Y + MathF.Sin(theta) * spokeLength));
        }
    }

    private void DrawCarState(Graphics graphics)
    {
        using var labelFont = OverlayTheme.Font(_fontFamily, 8.4f);
        using var valueFont = new Font(FontFamily.GenericMonospace, 8.4f);
        using var labelBrush = new SolidBrush(OverlayTheme.Colors.TextSubtle);
        using var valueBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        var x = Math.Max(Width - 158, 272);
        var y = 178;
        DrawPair(graphics, "Speed", SimpleTelemetryOverlayViewModel.FormatSpeed(_latestInputs.SpeedMetersPerSecond, _unitSystem), labelFont, valueFont, labelBrush, valueBrush, x, ref y);
        DrawPair(graphics, "Gear", FormatGear(_latestInputs.Gear), labelFont, valueFont, labelBrush, valueBrush, x, ref y);
        DrawPair(graphics, "RPM", FormatRpm(_latestInputs.Rpm), labelFont, valueFont, labelBrush, valueBrush, x, ref y);
        DrawPair(graphics, "Cooling", SimpleTelemetryOverlayViewModel.FormatTemperature(_latestInputs.WaterTempC, _unitSystem), labelFont, valueFont, labelBrush, valueBrush, x, ref y);
        DrawPair(graphics, "Oil", SimpleTelemetryOverlayViewModel.FormatPressure(_latestInputs.OilPressureBar, _unitSystem), labelFont, valueFont, labelBrush, valueBrush, x, ref y);
    }

    private static void DrawPair(Graphics graphics, string label, string value, Font labelFont, Font valueFont, Brush labelBrush, Brush valueBrush, int x, ref int y)
    {
        graphics.DrawString(label, labelFont, labelBrush, x, y);
        graphics.DrawString(value, valueFont, valueBrush, x + 70, y);
        y += 19;
    }

    private bool UseCompactLayout()
    {
        return ClientSize.Width < CompactLayoutWidthThreshold
            || ClientSize.Height < CompactLayoutHeightThreshold;
    }

    private static void DrawRightAligned(Graphics graphics, string text, Font font, Brush brush, RectangleF rect)
    {
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Near
        };
        graphics.DrawString(text, font, brush, rect, format);
    }

    private static string FormatStatus(LiveInputTelemetryModel inputs)
    {
        var status = string.Join(
            " | ",
            new[] { FormatGear(inputs.Gear), FormatRpm(inputs.Rpm) }.Where(value => value != "--"));
        return string.IsNullOrWhiteSpace(status) ? "--" : status;
    }

    private static string FormatGear(int? gear)
    {
        return gear switch
        {
            -1 => "R",
            0 => "N",
            > 0 => gear.Value.ToString(CultureInfo.InvariantCulture),
            _ => "--"
        };
    }

    private static string FormatRpm(double? rpm)
    {
        return rpm is { } value && SimpleTelemetryOverlayViewModel.IsFinite(value)
            ? $"{value.ToString("0", CultureInfo.InvariantCulture)} rpm"
            : "--";
    }

    private static string FormatSteering(double? radians)
    {
        return radians is { } value && SimpleTelemetryOverlayViewModel.IsFinite(value)
            ? $"{(value * 180d / Math.PI).ToString("+0;-0;0", CultureInfo.InvariantCulture)} deg"
            : "--";
    }

    private static string FormatPercent(double? value)
    {
        return SimpleTelemetryOverlayViewModel.FormatPercent(value);
    }

    private static double? Clamp01(double? value)
    {
        return value is { } number && SimpleTelemetryOverlayViewModel.IsFinite(number)
            ? Math.Clamp(number, 0d, 1d)
            : null;
    }

    private static void DrawText(
        Graphics graphics,
        string text,
        Font font,
        Brush brush,
        RectangleF bounds,
        ContentAlignment alignment)
    {
        using var format = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        format.Alignment = alignment is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight
            ? StringAlignment.Far
            : alignment is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter
                ? StringAlignment.Center
                : StringAlignment.Near;
        format.LineAlignment = alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight
            ? StringAlignment.Far
            : alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight
                ? StringAlignment.Center
                : StringAlignment.Near;
        graphics.DrawString(text, font, brush, bounds, format);
    }

    private void ReportOverlayError(Exception exception, string stage)
    {
        var message = $"{stage}: {exception.GetType().Name} {exception.Message}";
        var now = DateTimeOffset.UtcNow;
        if (string.Equals(_lastLoggedError, message, StringComparison.Ordinal)
            && _lastLoggedErrorAtUtc is { } lastLogged
            && now - lastLogged < TimeSpan.FromSeconds(30))
        {
            return;
        }

        _lastLoggedError = message;
        _lastLoggedErrorAtUtc = now;
        _logger.LogWarning(exception, "Input overlay {Stage} failed.", stage);
    }

    private sealed record InputTracePoint(double? Throttle, double? Brake, double? Clutch);
}
