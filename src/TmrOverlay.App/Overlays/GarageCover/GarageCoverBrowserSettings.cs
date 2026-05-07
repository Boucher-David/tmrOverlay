using System.Globalization;
using TmrOverlay.App.Localhost;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.GarageCover;

internal static class GarageCoverBrowserSettings
{
    public static GarageCoverBrowserSettingsSnapshot From(ApplicationSettings settings)
    {
        var overlay = GarageCoverOverlay(settings);
        var imagePath = overlay.GetStringOption(OverlayOptionKeys.GarageCoverImagePath);
        var imageStatus = GarageCoverImageStore.InspectImage(imagePath);
        var previewUntilUtc = ReadPreviewUntilUtc(overlay);
        var previewVisible = previewUntilUtc is not null && previewUntilUtc > DateTimeOffset.UtcNow;
        return new GarageCoverBrowserSettingsSnapshot(
            HasImage: imageStatus.IsUsable,
            ImageVersion: imageStatus.IsUsable
                ? $"{imageStatus.LastWriteTimeUtc!.Value.ToUnixTimeMilliseconds()}-{imageStatus.Length!.Value}"
                : null,
            ImageStatus: imageStatus.Status,
            FallbackReason: imageStatus.IsUsable
                ? null
                : imageStatus.Status,
            PreviewVisible: previewVisible,
            PreviewUntilUtc: previewUntilUtc,
            ImageFileName: imageStatus.FileName,
            ImageExtension: imageStatus.Extension,
            ImageLength: imageStatus.Length,
            ImageLastWriteTimeUtc: imageStatus.LastWriteTimeUtc);
    }

    public static GarageCoverDiagnosticsSnapshot Diagnostics(
        ApplicationSettings settings,
        LocalhostOverlaySnapshot localhost,
        LiveTelemetrySnapshot live)
    {
        var browserSettings = From(settings);
        var detection = DetectGarageState(live, DateTimeOffset.UtcNow);
        return new GarageCoverDiagnosticsSnapshot(
            RouteEnabled: localhost.Enabled,
            RouteStatus: localhost.Status,
            Route: "/overlays/garage-cover",
            ImageStatus: browserSettings.ImageStatus,
            ImageFileName: browserSettings.ImageFileName,
            ImageExtension: browserSettings.ImageExtension,
            ImageLength: browserSettings.ImageLength,
            ImageLastWriteTimeUtc: browserSettings.ImageLastWriteTimeUtc,
            PreviewVisible: browserSettings.PreviewVisible,
            PreviewUntilUtc: browserSettings.PreviewUntilUtc,
            LastDetectionState: detection.State,
            LastGarageVisible: live.Models.RaceEvents.IsGarageVisible,
            LastTelemetryAtUtc: live.LastUpdatedAtUtc,
            FallbackReason: browserSettings.FallbackReason);
    }

    public static GarageCoverDetectionSnapshot DetectGarageState(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        if (!availability.IsAvailable)
        {
            return availability.Reason switch
            {
                OverlayAvailabilityReason.Disconnected => new GarageCoverDetectionSnapshot("iracing_disconnected", "iRacing disconnected", isFresh: false),
                OverlayAvailabilityReason.StaleTelemetry => new GarageCoverDetectionSnapshot("telemetry_stale", "telemetry stale", isFresh: false),
                _ => new GarageCoverDetectionSnapshot("waiting_for_telemetry", availability.StatusText, isFresh: false)
            };
        }

        return snapshot.Models.RaceEvents.IsGarageVisible
            ? new GarageCoverDetectionSnapshot("garage_visible", "garage visible", isFresh: true)
            : new GarageCoverDetectionSnapshot("garage_hidden", "garage hidden", isFresh: true);
    }

    public static void SetPreviewUntil(OverlaySettings settings, DateTimeOffset untilUtc)
    {
        settings.SetStringOption(OverlayOptionKeys.GarageCoverPreviewUntilUtc, untilUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    public static DateTimeOffset? ReadPreviewUntilUtc(OverlaySettings settings)
    {
        var value = settings.GetStringOption(OverlayOptionKeys.GarageCoverPreviewUntilUtc);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static OverlaySettings GarageCoverOverlay(ApplicationSettings settings)
    {
        return settings.GetOrAddOverlay(
            GarageCoverOverlayDefinition.Definition.Id,
            GarageCoverOverlayDefinition.Definition.DefaultWidth,
            GarageCoverOverlayDefinition.Definition.DefaultHeight,
            defaultEnabled: false);
    }
}

internal sealed record GarageCoverBrowserSettingsSnapshot(
    bool HasImage,
    string? ImageVersion,
    string ImageStatus,
    string? FallbackReason,
    bool PreviewVisible,
    DateTimeOffset? PreviewUntilUtc,
    string? ImageFileName,
    string? ImageExtension,
    long? ImageLength,
    DateTimeOffset? ImageLastWriteTimeUtc);

internal sealed record GarageCoverDetectionSnapshot(
    string State,
    string DisplayText,
    bool IsFresh);

internal sealed record GarageCoverDiagnosticsSnapshot(
    bool RouteEnabled,
    string RouteStatus,
    string Route,
    string ImageStatus,
    string? ImageFileName,
    string? ImageExtension,
    long? ImageLength,
    DateTimeOffset? ImageLastWriteTimeUtc,
    bool PreviewVisible,
    DateTimeOffset? PreviewUntilUtc,
    string LastDetectionState,
    bool LastGarageVisible,
    DateTimeOffset? LastTelemetryAtUtc,
    string? FallbackReason);
