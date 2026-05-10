using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.AppInfo;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal static class SupportStatusText
{
    public static SupportAppStatus AppStatus(TelemetryCaptureStatusSnapshot snapshot)
    {
        var status = AppDiagnosticsStatusModel.From(snapshot);
        return new SupportAppStatus(status.SupportStatusText, ToSupportStatusLevel(status.SupportSeverity));
    }

    public static string SessionStateText(TelemetryCaptureStatusSnapshot snapshot)
    {
        return AppDiagnosticsStatusModel.From(snapshot).SessionStateText;
    }

    public static string SessionStateCompactText(TelemetryCaptureStatusSnapshot snapshot)
    {
        if (snapshot.RawCaptureActive)
        {
            return $"Diagnostics active ({FormatCount(snapshot.WrittenFrameCount)} frames)";
        }

        if (snapshot.RawCaptureEnabled)
        {
            return "Diagnostics armed";
        }

        if (snapshot.IsCapturing)
        {
            return $"Live telemetry ({FormatCount(snapshot.FrameCount)} frames)";
        }

        if (snapshot.IsConnected)
        {
            return "Connected, waiting";
        }

        return "Not connected";
    }

    public static string CurrentIssueText(TelemetryCaptureStatusSnapshot snapshot)
    {
        return AppDiagnosticsStatusModel.From(snapshot).CurrentIssueText;
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

    private static string FormatCount(long value)
    {
        return value switch
        {
            >= 1_000_000 => $"{value / 1_000_000d:0.#}M",
            >= 10_000 => $"{value / 1_000d:0.#}k",
            _ => value.ToString()
        };
    }

    private static SupportStatusLevel ToSupportStatusLevel(AppDiagnosticsSeverity severity)
    {
        return severity switch
        {
            AppDiagnosticsSeverity.Info => SupportStatusLevel.Info,
            AppDiagnosticsSeverity.Success => SupportStatusLevel.Success,
            AppDiagnosticsSeverity.Warning => SupportStatusLevel.Warning,
            AppDiagnosticsSeverity.Error => SupportStatusLevel.Error,
            _ => SupportStatusLevel.Neutral
        };
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
