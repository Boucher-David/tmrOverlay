using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Brand;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.GarageCover;

internal sealed class GarageCoverForm : PersistentOverlayForm
{
    private const int RefreshIntervalMilliseconds = 150;
    private const double StaleSeconds = 1.5d;

    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger<GarageCoverForm> _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly OverlaySettings _settings;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private Image? _coverImage;
    private Image? _defaultCoverImage;
    private string? _loadedImagePath;
    private DateTimeOffset? _loadedImageLastWriteUtc;
    private long? _loadedImageLength;
    private string? _lastLoggedImageErrorPath;
    private bool _managedEnabled;

    public GarageCoverForm(
        ILiveTelemetrySource liveTelemetrySource,
        ILogger<GarageCoverForm> logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            GarageCoverOverlayDefinition.Definition.DefaultWidth,
            GarageCoverOverlayDefinition.Definition.DefaultHeight)
    {
        _liveTelemetrySource = liveTelemetrySource;
        _logger = logger;
        _performanceState = performanceState;
        _settings = settings;

        BackColor = Color.Black;
        Padding = Padding.Empty;

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds
        };
        _refreshTimer.Tick += (_, _) => RefreshCoverVisibility();
        _refreshTimer.Start();
    }

    public void SetManagedEnabled(bool enabled)
    {
        _managedEnabled = enabled;
        if (!enabled && Visible)
        {
            Hide();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _coverImage?.Dispose();
            _defaultCoverImage?.Dispose();
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
            e.Graphics.Clear(Color.Black);
            EnsureCoverImage();
            if (_coverImage is not null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                e.Graphics.DrawImage(_coverImage, CoverDestinationRect(_coverImage.Size, ClientSize));
            }
            else
            {
                DrawFallback(e.Graphics);
            }

            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(AppPerformanceMetricIds.OverlayGarageCoverPaint, started, succeeded);
        }
    }

    private void RefreshCoverVisibility()
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var snapshotStarted = Stopwatch.GetTimestamp();
            var snapshotSucceeded = false;
            LiveTelemetrySnapshot snapshot;
            try
            {
                snapshot = _liveTelemetrySource.Snapshot();
                snapshotSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(AppPerformanceMetricIds.OverlayGarageCoverSnapshot, snapshotStarted, snapshotSucceeded);
            }

            var shouldCover = _managedEnabled && IsFresh(snapshot) && snapshot.Models.RaceEvents.IsGarageVisible;
            if (shouldCover)
            {
                EnsureCoverImage();
                if (!Visible)
                {
                    Show();
                }

                Invalidate();
            }
            else if (Visible)
            {
                Hide();
            }

            succeeded = true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Garage cover refresh failed.");
            if (Visible)
            {
                Hide();
            }
        }
        finally
        {
            _performanceState.RecordOperation(AppPerformanceMetricIds.OverlayGarageCoverRefresh, started, succeeded);
        }
    }

    private static bool IsFresh(LiveTelemetrySnapshot snapshot)
    {
        return snapshot.IsConnected
            && snapshot.IsCollecting
            && snapshot.LastUpdatedAtUtc is { } updatedAt
            && Math.Abs((DateTimeOffset.UtcNow - updatedAt).TotalSeconds) <= StaleSeconds;
    }

    private void EnsureCoverImage()
    {
        var imagePath = _settings.GetStringOption(OverlayOptionKeys.GarageCoverImagePath);
        var imageInfo = GarageCoverImageStore.GetSupportedImageInfo(imagePath);
        if (string.Equals(imagePath, _loadedImagePath, StringComparison.Ordinal)
            && ((imageInfo is null && _loadedImageLastWriteUtc is null && _loadedImageLength is null)
                || (imageInfo is not null
                    && _loadedImageLastWriteUtc == imageInfo.LastWriteTimeUtc
                    && _loadedImageLength == imageInfo.Length)))
        {
            return;
        }

        _coverImage?.Dispose();
        _coverImage = null;
        _loadedImagePath = imagePath;
        _loadedImageLastWriteUtc = imageInfo?.LastWriteTimeUtc;
        _loadedImageLength = imageInfo?.Length;

        if (imageInfo is null)
        {
            return;
        }

        try
        {
            using var source = Image.FromFile(imagePath);
            _coverImage = new Bitmap(source);
        }
        catch (Exception exception)
        {
            if (!string.Equals(_lastLoggedImageErrorPath, imagePath, StringComparison.Ordinal))
            {
                _logger.LogWarning(exception, "Failed to load garage cover image {ImagePath}.", imagePath);
                _lastLoggedImageErrorPath = imagePath;
            }
        }
    }

    private static Rectangle CoverDestinationRect(Size imageSize, Size clientSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || clientSize.Width <= 0 || clientSize.Height <= 0)
        {
            return new Rectangle(Point.Empty, clientSize);
        }

        var scale = Math.Max(
            clientSize.Width / (double)imageSize.Width,
            clientSize.Height / (double)imageSize.Height);
        var width = (int)Math.Ceiling(imageSize.Width * scale);
        var height = (int)Math.Ceiling(imageSize.Height * scale);
        return new Rectangle(
            (clientSize.Width - width) / 2,
            (clientSize.Height - height) / 2,
            width,
            height);
    }

    private void DrawFallback(Graphics graphics)
    {
        using var background = new SolidBrush(Color.Black);
        graphics.FillRectangle(background, ClientRectangle);

        _defaultCoverImage ??= TmrBrandAssets.LoadLogoImage();
        if (_defaultCoverImage is not null)
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(_defaultCoverImage, ContainDestinationRect(_defaultCoverImage.Size, ClientSize, 0.58d));
        }
        else
        {
            using var textBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
            using var font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, Math.Max(28f, ClientSize.Height / 12f), FontStyle.Bold);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            graphics.DrawString("TMR", font, textBrush, ClientRectangle, format);
        }

        using var border = new Pen(OverlayTheme.Colors.WindowBorder);
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private static Rectangle ContainDestinationRect(Size imageSize, Size clientSize, double maxBoundsFraction)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || clientSize.Width <= 0 || clientSize.Height <= 0)
        {
            return new Rectangle(Point.Empty, clientSize);
        }

        var targetWidth = Math.Max(1d, clientSize.Width * maxBoundsFraction);
        var targetHeight = Math.Max(1d, clientSize.Height * maxBoundsFraction);
        var scale = Math.Min(
            targetWidth / imageSize.Width,
            targetHeight / imageSize.Height);
        var width = (int)Math.Round(imageSize.Width * scale);
        var height = (int)Math.Round(imageSize.Height * scale);
        return new Rectangle(
            (clientSize.Width - width) / 2,
            (clientSize.Height - height) / 2,
            width,
            height);
    }
}
