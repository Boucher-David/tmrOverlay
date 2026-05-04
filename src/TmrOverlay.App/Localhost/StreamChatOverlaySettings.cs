using System.Text.RegularExpressions;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Localhost;

internal static partial class StreamChatOverlaySettings
{
    public const string ProviderNone = "none";
    public const string ProviderStreamlabs = "streamlabs";
    public const string ProviderTwitch = "twitch";

    public static StreamChatBrowserSettings From(ApplicationSettings settings)
    {
        var overlay = settings.GetOrAddOverlay(
            StreamChatOverlayDefinition.Definition.Id,
            StreamChatOverlayDefinition.Definition.DefaultWidth,
            StreamChatOverlayDefinition.Definition.DefaultHeight,
            defaultEnabled: false);
        var provider = NormalizeProvider(overlay.GetStringOption(OverlayOptionKeys.StreamChatProvider, ProviderNone));
        var streamlabsUrl = NormalizeStreamlabsUrl(overlay.GetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl));
        var twitchChannel = NormalizeTwitchChannel(overlay.GetStringOption(OverlayOptionKeys.StreamChatTwitchChannel));

        return provider switch
        {
            ProviderStreamlabs when streamlabsUrl is not null => new StreamChatBrowserSettings(
                Provider: ProviderStreamlabs,
                IsConfigured: true,
                StreamlabsWidgetUrl: streamlabsUrl,
                TwitchChannel: null,
                Status: "configured_streamlabs"),
            ProviderTwitch when twitchChannel is not null => new StreamChatBrowserSettings(
                Provider: ProviderTwitch,
                IsConfigured: true,
                StreamlabsWidgetUrl: null,
                TwitchChannel: twitchChannel,
                Status: "configured_twitch"),
            ProviderStreamlabs => Unconfigured(ProviderStreamlabs, "missing_or_invalid_streamlabs_url"),
            ProviderTwitch => Unconfigured(ProviderTwitch, "missing_or_invalid_twitch_channel"),
            _ => Unconfigured(ProviderNone, "not_configured")
        };
    }

    public static string NormalizeProvider(string? provider)
    {
        var normalized = provider?.Trim().ToLowerInvariant();
        return normalized is ProviderStreamlabs or ProviderTwitch ? normalized : ProviderNone;
    }

    public static string? NormalizeStreamlabsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !IsStreamlabsHost(uri.Host)
            || !IsStreamlabsChatBoxPath(uri.AbsolutePath))
        {
            return null;
        }

        return uri.ToString();
    }

    public static string? NormalizeTwitchChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return null;
        }

        var value = channel.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Host, "twitch.tv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "www.twitch.tv", StringComparison.OrdinalIgnoreCase)))
        {
            value = uri.AbsolutePath.Trim('/');
        }

        value = value.Trim().TrimStart('@').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        value = value.ToLowerInvariant();
        return TwitchChannelRegex().IsMatch(value) ? value : null;
    }

    private static StreamChatBrowserSettings Unconfigured(string provider, string status)
    {
        return new StreamChatBrowserSettings(
            Provider: provider,
            IsConfigured: false,
            StreamlabsWidgetUrl: null,
            TwitchChannel: null,
            Status: status);
    }

    private static bool IsStreamlabsHost(string host)
    {
        return string.Equals(host, "streamlabs.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".streamlabs.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStreamlabsChatBoxPath(string path)
    {
        return string.Equals(path, "/widgets/chat-box", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/widgets/chat-box/", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[a-z0-9_]{3,25}$", RegexOptions.CultureInvariant)]
    private static partial Regex TwitchChannelRegex();
}

internal sealed record StreamChatBrowserSettings(
    string Provider,
    bool IsConfigured,
    string? StreamlabsWidgetUrl,
    string? TwitchChannel,
    string Status);
