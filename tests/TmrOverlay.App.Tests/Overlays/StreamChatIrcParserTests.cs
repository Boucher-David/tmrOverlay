using TmrOverlay.App.Overlays.StreamChat;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class StreamChatIrcParserTests
{
    [Fact]
    public void TryParsePrivMsg_UsesDisplayNameAndDecodesTwitchEscapes()
    {
        const string line = "@display-name=Race\\sEngineer;color=#62C7FF :raceengineer!raceengineer@raceengineer.tmi.twitch.tv PRIVMSG #tmracing :Box\\sthis\\sup\\:\\sgo";

        var message = StreamChatIrcParser.TryParsePrivMsg(line);

        Assert.NotNull(message);
        Assert.Equal("Race Engineer", message.Name);
        Assert.Equal("Box this up; go", message.Text);
        Assert.Equal(StreamChatMessageKind.Message, message.Kind);
    }

    [Fact]
    public void TryParsePrivMsg_FallsBackToPrefixName()
    {
        const string line = ":justinfan12345!justinfan12345@justinfan12345.tmi.twitch.tv PRIVMSG #tmracing :hello";

        var message = StreamChatIrcParser.TryParsePrivMsg(line);

        Assert.NotNull(message);
        Assert.Equal("justinfan12345", message.Name);
        Assert.Equal("hello", message.Text);
    }

    [Fact]
    public void TryGetPingResponse_ReturnsPongPayload()
    {
        var handled = StreamChatIrcParser.TryGetPingResponse("PING :tmi.twitch.tv", out var response);

        Assert.True(handled);
        Assert.Equal("PONG :tmi.twitch.tv", response);
    }
}
