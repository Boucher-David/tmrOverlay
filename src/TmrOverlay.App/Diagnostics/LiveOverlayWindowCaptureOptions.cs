using Microsoft.Extensions.Configuration;

namespace TmrOverlay.App.Diagnostics;

internal sealed class LiveOverlayWindowCaptureOptions
{
    public bool CaptureScreenshots { get; init; }

    public static LiveOverlayWindowCaptureOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("LiveOverlayWindowDiagnostics");
        return new LiveOverlayWindowCaptureOptions
        {
            CaptureScreenshots = ParseBoolean(section["CaptureScreenshots"], defaultValue: false)
        };
    }

    private static bool ParseBoolean(string? configuredValue, bool defaultValue)
    {
        return bool.TryParse(configuredValue, out var parsedValue) ? parsedValue : defaultValue;
    }
}
