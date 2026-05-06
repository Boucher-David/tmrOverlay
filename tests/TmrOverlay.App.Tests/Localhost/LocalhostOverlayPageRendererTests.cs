using TmrOverlay.App.Overlays.BrowserSources;
using Xunit;

namespace TmrOverlay.App.Tests.Localhost;

public sealed class BrowserOverlayPageRendererTests
{
    [Theory]
    [InlineData("/overlays/standings", "standings")]
    [InlineData("/overlays/relative", "relative")]
    [InlineData("/overlays/fuel-calculator", "fuel-calculator")]
    [InlineData("/overlays/calculator", "fuel-calculator")]
    [InlineData("/overlays/session-weather", "session-weather")]
    [InlineData("/overlays/pit-service", "pit-service")]
    [InlineData("/overlays/input-state", "input-state")]
    [InlineData("/overlays/inputs", "input-state")]
    [InlineData("/overlays/car-radar", "car-radar")]
    [InlineData("/overlays/gap-to-leader", "gap-to-leader")]
    [InlineData("/overlays/track-map", "track-map")]
    [InlineData("/overlays/stream-chat", "stream-chat")]
    [InlineData("/overlays/garage-cover", "garage-cover")]
    public void TryRender_RendersKnownOverlayRoutes(string route, string expectedId)
    {
        var rendered = BrowserOverlayPageRenderer.TryRender(route, out var html);

        Assert.True(rendered);
        Assert.Contains("\"id\":\"" + expectedId + "\"", html);
        Assert.Contains("fetch('/api/snapshot'", html);
        if (expectedId == "track-map")
        {
            Assert.Contains("track-map-page", html);
            Assert.Contains("fetch('/api/track-map'", html);
            Assert.Contains("renderOffline()", html);
            Assert.Contains("let cachedTrackMapSettings", html);
        }
        if (expectedId == "standings")
        {
            Assert.Contains("hasStandingDriverIdentity", html);
        }
        if (expectedId == "stream-chat")
        {
            Assert.Contains("fetch('/api/stream-chat'", html);
            Assert.Contains("connectTwitchChat", html);
            Assert.Contains("chat connected", html);
        }
        if (expectedId == "garage-cover")
        {
            Assert.Contains("garage-cover-page", html);
            Assert.Contains("fetch('/api/garage-cover'", html);
            Assert.Contains("/api/garage-cover/image", html);
        }
    }

    [Fact]
    public void TryRender_RejectsUnknownRoute()
    {
        var rendered = BrowserOverlayPageRenderer.TryRender("/overlays/unknown", out var html);

        Assert.False(rendered);
        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void TryRender_RejectsFlagsRouteWhileBrowserSourceIsDisabled()
    {
        var rendered = BrowserOverlayPageRenderer.TryRender("/overlays/flags", out var html);

        Assert.False(rendered);
        Assert.Equal(string.Empty, html);
    }

    [Theory]
    [InlineData("standings", "/overlays/standings")]
    [InlineData("relative", "/overlays/relative")]
    [InlineData("fuel-calculator", "/overlays/fuel-calculator")]
    [InlineData("session-weather", "/overlays/session-weather")]
    [InlineData("pit-service", "/overlays/pit-service")]
    [InlineData("input-state", "/overlays/input-state")]
    [InlineData("car-radar", "/overlays/car-radar")]
    [InlineData("gap-to-leader", "/overlays/gap-to-leader")]
    [InlineData("track-map", "/overlays/track-map")]
    [InlineData("stream-chat", "/overlays/stream-chat")]
    [InlineData("garage-cover", "/overlays/garage-cover")]
    public void TryGetRouteForOverlayId_ReturnsCanonicalRoute(string overlayId, string expectedRoute)
    {
        var found = BrowserOverlayPageRenderer.TryGetRouteForOverlayId(overlayId, out var route);

        Assert.True(found);
        Assert.Equal(expectedRoute, route);
    }

    [Fact]
    public void TryGetRouteForOverlayId_ReturnsFalseForFlagsWhileBrowserSourceIsDisabled()
    {
        var found = BrowserOverlayPageRenderer.TryGetRouteForOverlayId("flags", out var route);

        Assert.False(found);
        Assert.Equal(string.Empty, route);
    }

    [Fact]
    public void RenderIndex_ListsCanonicalRoutes()
    {
        var html = BrowserOverlayPageRenderer.RenderIndex(8765);

        Assert.Contains("TMR Localhost Overlays", html);
        Assert.Contains("/overlays/standings", html);
        Assert.Contains("/overlays/fuel-calculator", html);
        Assert.DoesNotContain("/overlays/flags", html);
        Assert.Contains("/overlays/session-weather", html);
        Assert.Contains("/overlays/pit-service", html);
        Assert.Contains("/overlays/input-state", html);
        Assert.Contains("/overlays/car-radar", html);
        Assert.Contains("/overlays/track-map", html);
        Assert.Contains("/overlays/stream-chat", html);
        Assert.Contains("/overlays/garage-cover", html);
        Assert.DoesNotContain("/overlays/calculator", html);
        Assert.DoesNotContain("/overlays/inputs", html);
    }
}
