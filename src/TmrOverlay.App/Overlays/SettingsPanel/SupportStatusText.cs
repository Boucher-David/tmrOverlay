using System.Globalization;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.AppInfo;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal static class SupportStatusText
{
    public static SupportAppStatus AppStatus(TelemetryCaptureStatusSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.LastError))
        {
            return new SupportAppStatus("Error", SupportStatusLevel.Error);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastWarning)
            || !string.IsNullOrWhiteSpace(snapshot.AppWarning)
            || snapshot.DroppedFrameCount > 0)
        {
            return new SupportAppStatus("Warning", SupportStatusLevel.Warning);
        }

        if (snapshot.IsCapturing)
        {
            return new SupportAppStatus("Live telemetry", SupportStatusLevel.Success);
        }

        if (snapshot.IsConnected)
        {
            return new SupportAppStatus("Connected", SupportStatusLevel.Info);
        }

        return new SupportAppStatus("Waiting for iRacing", SupportStatusLevel.Neutral);
    }

    public static string SessionStateText(TelemetryCaptureStatusSnapshot snapshot)
    {
        if (snapshot.RawCaptureActive)
        {
            return $"Diagnostic telemetry active ({FormatCount(snapshot.WrittenFrameCount)} frames written)";
        }

        if (snapshot.RawCaptureEnabled)
        {
            return "Diagnostic telemetry requested; starts with live data";
        }

        if (snapshot.IsCapturing)
        {
            return $"Receiving live telemetry ({FormatCount(snapshot.FrameCount)} frames)";
        }

        if (snapshot.IsConnected)
        {
            return "iRacing connected; waiting for live session data";
        }

        return "Not connected; start iRacing when ready";
    }

    public static string CurrentIssueText(TelemetryCaptureStatusSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.LastError))
        {
            return $"Error: {snapshot.LastError}";
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastWarning))
        {
            return $"Warning: {snapshot.LastWarning}";
        }

        if (!string.IsNullOrWhiteSpace(snapshot.AppWarning))
        {
            return $"App warning: {snapshot.AppWarning}";
        }

        if (!snapshot.IsConnected)
        {
            return "No active issue. Waiting is expected before iRacing is running.";
        }

        if (!snapshot.IsCapturing)
        {
            return "No active issue. Live telemetry starts after session data arrives.";
        }

        return "No active issue recorded.";
    }

    public static string LatestBundleDisplayText(string? bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            return "Latest bundle: none created yet";
        }

        return $"Latest bundle: {Path.GetFileName(bundlePath)}";
    }

    public static string AppVersionText(AppVersionInfo appVersion)
    {
        var version = NormalizeVersion(string.IsNullOrWhiteSpace(appVersion.Version)
            ? "unknown"
            : appVersion.Version.Trim());
        var informationalVersion = string.IsNullOrWhiteSpace(appVersion.InformationalVersion)
            ? version
            : appVersion.InformationalVersion.Trim();

        if (string.Equals(version, informationalVersion, StringComparison.OrdinalIgnoreCase)
            || string.Equals(version, NormalizeVersion(informationalVersion), StringComparison.OrdinalIgnoreCase)
            || informationalVersion.StartsWith(version + "+", StringComparison.OrdinalIgnoreCase))
        {
            return $"v{informationalVersion}";
        }

        return $"v{version} ({informationalVersion})";
    }

    private static string NormalizeVersion(string version)
    {
        return version.EndsWith(".0", StringComparison.Ordinal) && version.Count(character => character == '.') == 3
            ? version[..^2]
            : version;
    }

    private static string FormatCount(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}

internal readonly record struct SupportAppStatus(string Text, SupportStatusLevel Level);

internal enum SupportStatusLevel
{
    Neutral,
    Info,
    Success,
    Warning,
    Error
}
