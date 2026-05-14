using System.Globalization;

namespace TmrOverlay.App.Overlays.StreamChat;

internal static class StreamChatMessageDisplay
{
    public static string? AuthorColorHex(StreamChatMessage message, StreamChatContentOptions options)
    {
        return message.IsTwitch && options.ShowAuthorColor ? message.ColorHex : null;
    }

    public static IReadOnlyList<string> MetadataParts(StreamChatMessage message, StreamChatContentOptions options)
    {
        if (!message.IsTwitch)
        {
            return [];
        }

        var parts = new List<string>();
        if (message.Kind == StreamChatMessageKind.Notice && options.ShowAlerts && !string.IsNullOrWhiteSpace(message.NoticeKind))
        {
            parts.Add($"alert {CompactToken(message.NoticeKind)}");
        }

        if (options.ShowBits && message.Bits is > 0)
        {
            parts.Add($"{message.Bits.Value.ToString(CultureInfo.InvariantCulture)} bits");
        }

        if (options.ShowFirstMessage && message.IsFirstMessage)
        {
            parts.Add("first");
        }

        if (options.ShowReplies && !string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            parts.Add($"reply @{CompactToken(message.ReplyTo)}");
        }

        if (options.ShowTimestamps && message.TimestampUtc is { } timestamp)
        {
            parts.Add(timestamp.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        if (options.ShowMessageIds && !string.IsNullOrWhiteSpace(message.TwitchMessageId))
        {
            parts.Add($"id {ShortId(message.TwitchMessageId)}");
        }

        return parts;
    }

    public static IReadOnlyList<StreamChatDisplayBadge> BadgeParts(StreamChatMessage message, StreamChatContentOptions options)
    {
        if (!message.IsTwitch || !options.ShowBadges)
        {
            return [];
        }

        return message.Badges
            .Select(badge => new StreamChatDisplayBadge(badge.Id, badge.Version, FormatBadge(badge), message.TwitchRoomId))
            .Where(badge => !string.IsNullOrWhiteSpace(badge.Label))
            .ToArray();
    }

    public static IReadOnlyList<StreamChatDisplaySegment> MessageSegments(StreamChatMessage message, StreamChatContentOptions options)
    {
        if (!message.IsTwitch
            || !options.ShowEmotes
            || message.Emotes.Count == 0
            || (message.Kind == StreamChatMessageKind.Notice && !string.IsNullOrWhiteSpace(message.NoticeText)))
        {
            return [StreamChatDisplaySegment.TextSegment(message.Text)];
        }

        var segments = new List<StreamChatDisplaySegment>();
        var offset = 0;
        foreach (var emote in message.Emotes.OrderBy(emote => emote.Start).ThenBy(emote => emote.End))
        {
            if (emote.Start < offset
                || emote.Start < 0
                || emote.End < emote.Start
                || emote.End >= message.Text.Length)
            {
                continue;
            }

            if (emote.Start > offset)
            {
                segments.Add(StreamChatDisplaySegment.TextSegment(message.Text[offset..emote.Start]));
            }

            segments.Add(StreamChatDisplaySegment.EmoteSegment(
                emote.Token,
                $"https://static-cdn.jtvnw.net/emoticons/v2/{Uri.EscapeDataString(emote.Id)}/default/dark/1.0"));
            offset = emote.End + 1;
        }

        if (offset < message.Text.Length)
        {
            segments.Add(StreamChatDisplaySegment.TextSegment(message.Text[offset..]));
        }

        return segments.Count == 0 ? [StreamChatDisplaySegment.TextSegment(message.Text)] : segments;
    }

    private static string FormatBadge(StreamChatBadge badge)
    {
        var id = CompactToken(badge.Id);
        return id switch
        {
            "broadcaster" => "broadcaster",
            "moderator" => "mod",
            "subscriber" => string.IsNullOrWhiteSpace(badge.Version) || badge.Version == "0" ? "sub" : $"sub {badge.Version}",
            "vip" => "vip",
            _ => id
        };
    }

    private static string CompactToken(string value)
    {
        var compact = value.Trim().Replace("-", " ", StringComparison.Ordinal);
        return compact.Length <= 18 ? compact : compact[..18];
    }

    private static string ShortId(string value)
    {
        var compact = value.Trim();
        return compact.Length <= 8 ? compact : compact[..8];
    }
}

internal sealed record StreamChatDisplayBadge(
    string Id,
    string Version,
    string Label,
    string? RoomId);

internal sealed record StreamChatDisplaySegment(
    string Kind,
    string Text,
    string? ImageUrl)
{
    public static StreamChatDisplaySegment TextSegment(string text)
    {
        return new StreamChatDisplaySegment("text", text, null);
    }

    public static StreamChatDisplaySegment EmoteSegment(string text, string imageUrl)
    {
        return new StreamChatDisplaySegment("emote", text, imageUrl);
    }
}
