using System.Text.RegularExpressions;
using TmrOverlay.App.Overlays.Content;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.StreamChat;

internal static partial class StreamChatOverlaySettings
{
    public const string ProviderNone = "none";
    public const string ProviderStreamlabs = "streamlabs";
    public const string ProviderTwitch = "twitch";
    public static string DefaultProvider => SharedOverlayContract.Current.StreamChatDefaultProvider;
    public static string DefaultTwitchChannel => SharedOverlayContract.Current.StreamChatDefaultTwitchChannel;

    public static StreamChatBrowserSettings From(ApplicationSettings settings)
    {
        var overlay = settings.GetOrAddOverlay(
            StreamChatOverlayDefinition.Definition.Id,
            StreamChatOverlayDefinition.Definition.DefaultWidth,
            StreamChatOverlayDefinition.Definition.DefaultHeight,
            defaultEnabled: false);
        return FromOverlay(overlay);
    }

    public static StreamChatBrowserSettings FromOverlay(OverlaySettings overlay)
    {
        var provider = NormalizeProvider(overlay.GetStringOption(OverlayOptionKeys.StreamChatProvider, DefaultProvider));
        var streamlabsUrl = NormalizeStreamlabsUrl(overlay.GetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl));
        var twitchChannel = NormalizeTwitchChannel(overlay.GetStringOption(OverlayOptionKeys.StreamChatTwitchChannel, DefaultTwitchChannel));
        var contentOptions = StreamChatContentOptions.From(overlay);

        return provider switch
        {
            ProviderStreamlabs when streamlabsUrl is not null => new StreamChatBrowserSettings(
                Provider: ProviderStreamlabs,
                IsConfigured: true,
                StreamlabsWidgetUrl: streamlabsUrl,
                TwitchChannel: null,
                Status: "configured_streamlabs")
            {
                ContentOptions = contentOptions
            },
            ProviderTwitch when twitchChannel is not null => new StreamChatBrowserSettings(
                Provider: ProviderTwitch,
                IsConfigured: true,
                StreamlabsWidgetUrl: null,
                TwitchChannel: twitchChannel,
                Status: "configured_twitch")
            {
                ContentOptions = contentOptions
            },
            ProviderStreamlabs => Unconfigured(ProviderStreamlabs, "missing_or_invalid_streamlabs_url", contentOptions),
            ProviderTwitch => Unconfigured(ProviderTwitch, "missing_or_invalid_twitch_channel", contentOptions),
            _ => Unconfigured(ProviderNone, "not_configured", contentOptions)
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

    private static StreamChatBrowserSettings Unconfigured(string provider, string status, StreamChatContentOptions contentOptions)
    {
        return new StreamChatBrowserSettings(
            Provider: provider,
            IsConfigured: false,
            StreamlabsWidgetUrl: null,
            TwitchChannel: null,
            Status: status)
        {
            ContentOptions = contentOptions
        };
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
    string Status)
{
    public StreamChatContentOptions ContentOptions { get; init; } = StreamChatContentOptions.Default;
}

internal sealed record StreamChatContentOptions(
    bool ShowAuthorColor,
    bool ShowBadges,
    bool ShowBits,
    bool ShowFirstMessage,
    bool ShowReplies,
    bool ShowTimestamps,
    bool ShowEmotes,
    bool ShowAlerts,
    bool ShowMessageIds)
{
    public static StreamChatContentOptions Default { get; } = new(
        ShowAuthorColor: true,
        ShowBadges: true,
        ShowBits: true,
        ShowFirstMessage: true,
        ShowReplies: true,
        ShowTimestamps: true,
        ShowEmotes: true,
        ShowAlerts: true,
        ShowMessageIds: false);

    public static StreamChatContentOptions From(OverlaySettings? settings)
    {
        if (settings is null)
        {
            return Default;
        }

        return new StreamChatContentOptions(
            ShowAuthorColor: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatAuthorColorBlockId),
            ShowBadges: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatBadgesBlockId),
            ShowBits: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatBitsBlockId),
            ShowFirstMessage: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatFirstMessageBlockId),
            ShowReplies: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatRepliesBlockId),
            ShowTimestamps: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatTimestampsBlockId),
            ShowEmotes: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatEmotesBlockId),
            ShowAlerts: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatAlertsBlockId),
            ShowMessageIds: BlockEnabled(settings, OverlayContentColumnSettings.StreamChatMessageIdsBlockId));
    }

    public bool ShouldShow(StreamChatMessage message)
    {
        return message.Kind != StreamChatMessageKind.Notice || !message.IsTwitch || ShowAlerts;
    }

    private static bool BlockEnabled(OverlaySettings settings, string blockId)
    {
        var block = OverlayContentColumnSettings.StreamChat.Blocks?.FirstOrDefault(
            block => string.Equals(block.Id, blockId, StringComparison.Ordinal));
        return block is null || OverlayContentColumnSettings.BlockEnabled(settings, block);
    }
}
