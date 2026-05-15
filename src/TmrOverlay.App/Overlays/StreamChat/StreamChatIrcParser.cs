namespace TmrOverlay.App.Overlays.StreamChat;

internal static class StreamChatIrcParser
{
    private const string TwitchSource = "twitch";

    public static bool TryGetPingResponse(string line, out string response)
    {
        response = string.Empty;
        if (!line.StartsWith("PING", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = line.Length > 5 ? line[5..].Trim() : ":tmi.twitch.tv";
        response = $"PONG {payload}";
        return true;
    }

    public static bool IsReconnect(string line)
    {
        return line.Contains(" RECONNECT ", StringComparison.Ordinal);
    }

    public static bool IsAuthFailure(string line)
    {
        return line.Contains(" NOTICE * :Login authentication failed", StringComparison.Ordinal)
            || line.Contains(" NOTICE * :Improperly formatted auth", StringComparison.Ordinal);
    }

    public static bool IsJoined(string line, string channel)
    {
        return line.Contains(" 001 ", StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(channel)
                && line.Contains($" ROOMSTATE #{channel}", StringComparison.OrdinalIgnoreCase));
    }

    public static StreamChatMessage? TryParsePrivMsg(string line)
    {
        var messageIndex = line.IndexOf(" PRIVMSG ", StringComparison.Ordinal);
        var textIndex = messageIndex >= 0
            ? line.IndexOf(" :", messageIndex, StringComparison.Ordinal)
            : -1;
        if (messageIndex < 0 || textIndex < 0)
        {
            return null;
        }

        var prefixAndTags = line[..messageIndex];
        var tags = ParseTags(prefixAndTags);
        var text = DecodeTagValue(line[(textIndex + 2)..]);
        var fallbackName = ParseFallbackName(prefixAndTags);
        return new StreamChatMessage(
            Name: tags.TryGetValue("display-name", out var displayName) && !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : fallbackName,
            Text: text,
            Kind: StreamChatMessageKind.Message)
        {
            Source = TwitchSource,
            ColorHex = ValidColorHex(TagValue(tags, "color")),
            Badges = ParseBadges(TagValue(tags, "badges")),
            Emotes = ParseEmotes(TagValue(tags, "emotes"), text),
            Bits = ParsePositiveInt(TagValue(tags, "bits")),
            IsFirstMessage = string.Equals(TagValue(tags, "first-msg"), "1", StringComparison.Ordinal),
            ReplyTo = FirstNonEmpty(
                TagValue(tags, "reply-parent-display-name"),
                TagValue(tags, "reply-parent-user-login")),
            TwitchMessageId = FirstNonEmpty(TagValue(tags, "id")),
            TwitchRoomId = FirstNonEmpty(TagValue(tags, "room-id")),
            TimestampUtc = ParseTwitchTimestamp(TagValue(tags, "tmi-sent-ts"))
        };
    }

    public static StreamChatMessage? TryParseUserNotice(string line)
    {
        var noticeIndex = line.IndexOf(" USERNOTICE ", StringComparison.Ordinal);
        if (noticeIndex < 0)
        {
            return null;
        }

        var prefixAndTags = line[..noticeIndex];
        var tags = ParseTags(prefixAndTags);
        var textIndex = line.IndexOf(" :", noticeIndex, StringComparison.Ordinal);
        var userMessage = textIndex >= 0
            ? DecodeTagValue(line[(textIndex + 2)..])
            : string.Empty;
        var systemMessage = FirstNonEmpty(TagValue(tags, "system-msg"));
        var fallbackName = ParseFallbackName(prefixAndTags);
        var text = FirstNonEmpty(systemMessage, userMessage, "Twitch chat event.") ?? "Twitch chat event.";
        if (!string.IsNullOrWhiteSpace(systemMessage) && !string.IsNullOrWhiteSpace(userMessage))
        {
            text = $"{systemMessage} {userMessage}";
        }

        return new StreamChatMessage(
            Name: tags.TryGetValue("display-name", out var displayName) && !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : fallbackName,
            Text: text,
            Kind: StreamChatMessageKind.Notice)
        {
            Source = TwitchSource,
            ColorHex = ValidColorHex(TagValue(tags, "color")),
            Badges = ParseBadges(TagValue(tags, "badges")),
            Emotes = ParseEmotes(TagValue(tags, "emotes"), userMessage),
            Bits = ParsePositiveInt(TagValue(tags, "bits")),
            IsFirstMessage = string.Equals(TagValue(tags, "first-msg"), "1", StringComparison.Ordinal),
            ReplyTo = FirstNonEmpty(
                TagValue(tags, "reply-parent-display-name"),
                TagValue(tags, "reply-parent-user-login")),
            TwitchMessageId = FirstNonEmpty(TagValue(tags, "id")),
            TwitchRoomId = FirstNonEmpty(TagValue(tags, "room-id")),
            TimestampUtc = ParseTwitchTimestamp(TagValue(tags, "tmi-sent-ts")),
            NoticeKind = FirstNonEmpty(TagValue(tags, "msg-id")),
            NoticeText = systemMessage
        };
    }

    private static Dictionary<string, string> ParseTags(string prefixAndTags)
    {
        if (!prefixAndTags.StartsWith('@'))
        {
            return [];
        }

        var tagEnd = prefixAndTags.IndexOf(' ');
        if (tagEnd <= 1)
        {
            return [];
        }

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in prefixAndTags[1..tagEnd].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var splitAt = pair.IndexOf('=');
            if (splitAt < 0)
            {
                tags[pair] = string.Empty;
                continue;
            }

            tags[pair[..splitAt]] = DecodeTagValue(pair[(splitAt + 1)..]);
        }

        return tags;
    }

    private static string ParseFallbackName(string prefixAndTags)
    {
        var marker = prefixAndTags.IndexOf(':');
        if (marker < 0 || marker == prefixAndTags.Length - 1)
        {
            return "chat";
        }

        var end = prefixAndTags.IndexOfAny(['!', ' '], marker + 1);
        return end > marker
            ? prefixAndTags[(marker + 1)..end]
            : prefixAndTags[(marker + 1)..];
    }

    private static string DecodeTagValue(string value)
    {
        return value
            .Replace(@"\\", @"\", StringComparison.Ordinal)
            .Replace(@"\s", " ", StringComparison.Ordinal)
            .Replace(@"\:", ";", StringComparison.Ordinal);
    }

    private static string? TagValue(IReadOnlyDictionary<string, string> tags, string key)
    {
        return tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? ValidColorHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 7
            && trimmed[0] == '#'
            && trimmed[1..].All(Uri.IsHexDigit)
                ? trimmed.ToUpperInvariant()
                : null;
    }

    private static int? ParsePositiveInt(string? value)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
    }

    private static DateTimeOffset? ParseTwitchTimestamp(string? value)
    {
        return long.TryParse(value, out var unixMilliseconds) && unixMilliseconds > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
            : null;
    }

    private static IReadOnlyList<StreamChatBadge> ParseBadges(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseBadge)
            .Where(badge => badge is not null)
            .Select(badge => badge!)
            .ToArray();
    }

    private static StreamChatBadge? ParseBadge(string value)
    {
        var splitAt = value.IndexOf('/');
        var id = splitAt >= 0 ? value[..splitAt] : value;
        var version = splitAt >= 0 && splitAt < value.Length - 1 ? value[(splitAt + 1)..] : string.Empty;
        id = id.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new StreamChatBadge(id, version.Trim());
    }

    private static IReadOnlyList<StreamChatEmote> ParseEmotes(string? value, string text)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(text))
        {
            return [];
        }

        var emotes = new List<StreamChatEmote>();
        foreach (var emoteGroup in value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var splitAt = emoteGroup.IndexOf(':');
            if (splitAt <= 0 || splitAt >= emoteGroup.Length - 1)
            {
                continue;
            }

            var id = emoteGroup[..splitAt].Trim();
            foreach (var range in emoteGroup[(splitAt + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var rangeSplit = range.IndexOf('-');
                if (rangeSplit <= 0
                    || !int.TryParse(range[..rangeSplit], out var start)
                    || !int.TryParse(range[(rangeSplit + 1)..], out var end)
                    || start < 0
                    || end < start
                    || end >= text.Length)
                {
                    continue;
                }

                emotes.Add(new StreamChatEmote(id, text[start..(end + 1)], start, end));
            }
        }

        return emotes;
    }
}

internal enum StreamChatMessageKind
{
    Message,
    Notice,
    System,
    Error
}

internal sealed record StreamChatBadge(
    string Id,
    string Version);

internal sealed record StreamChatEmote(
    string Id,
    string Token,
    int Start,
    int End);

internal sealed record StreamChatMessage(
    string Name,
    string Text,
    StreamChatMessageKind Kind)
{
    public string Source { get; init; } = string.Empty;

    public string? ColorHex { get; init; }

    public IReadOnlyList<StreamChatBadge> Badges { get; init; } = [];

    public IReadOnlyList<StreamChatEmote> Emotes { get; init; } = [];

    public int? Bits { get; init; }

    public bool IsFirstMessage { get; init; }

    public string? ReplyTo { get; init; }

    public string? TwitchMessageId { get; init; }

    public string? TwitchRoomId { get; init; }

    public DateTimeOffset? TimestampUtc { get; init; }

    public string? NoticeKind { get; init; }

    public string? NoticeText { get; init; }

    public bool IsTwitch => string.Equals(Source, "twitch", StringComparison.OrdinalIgnoreCase);
}
