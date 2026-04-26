using TmrOverlay.App.History;
using Xunit;

namespace TmrOverlay.App.Tests.History;

public sealed class SessionHistoryPathTests
{
    [Theory]
    [InlineData("Mercedes-AMG GT3 2020", "mercedes-amg-gt3-2020")]
    [InlineData("Track 252 - Gesamtstrecke 24h", "track-252-gesamtstrecke-24h")]
    [InlineData("  Race / Endurance  ", "race-endurance")]
    [InlineData("", "unknown")]
    public void Slug_ReturnsStableAsciiPathSegment(string value, string expected)
    {
        Assert.Equal(expected, SessionHistoryPath.Slug(value));
    }
}
