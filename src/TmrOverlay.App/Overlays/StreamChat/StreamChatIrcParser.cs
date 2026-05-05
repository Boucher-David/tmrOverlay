namespace TmrOverlay.App.Overlays.StreamChat;

internal static class StreamChatIrcParser
{
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
            Kind: StreamChatMessageKind.Message);
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
}

internal enum StreamChatMessageKind
{
    Message,
    System,
    Error
}

internal sealed record StreamChatMessage(
    string Name,
    string Text,
    StreamChatMessageKind Kind);
