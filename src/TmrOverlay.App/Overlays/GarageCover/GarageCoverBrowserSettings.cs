using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.GarageCover;

internal static class GarageCoverBrowserSettings
{
    public static GarageCoverBrowserSettingsSnapshot From(ApplicationSettings settings)
    {
        var overlay = settings.GetOrAddOverlay(
            GarageCoverOverlayDefinition.Definition.Id,
            GarageCoverOverlayDefinition.Definition.DefaultWidth,
            GarageCoverOverlayDefinition.Definition.DefaultHeight,
            defaultEnabled: false);
        var imagePath = overlay.GetStringOption(OverlayOptionKeys.GarageCoverImagePath);
        var imageInfo = GarageCoverImageStore.GetSupportedImageInfo(imagePath);
        return new GarageCoverBrowserSettingsSnapshot(
            HasImage: imageInfo is not null,
            ImageVersion: imageInfo is null
                ? null
                : $"{imageInfo.LastWriteTimeUtc.ToUnixTimeMilliseconds()}-{imageInfo.Length}");
    }
}

internal sealed record GarageCoverBrowserSettingsSnapshot(
    bool HasImage,
    string? ImageVersion);
