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
        Assert.Contains("telemetryAvailability", html);
        Assert.Contains("waiting for fresh telemetry", html);
        Assert.Contains("--tmr-surface", html);
        Assert.Contains("border-bottom: 2px solid var(--tmr-cyan)", html);
        Assert.Contains("var(--tmr-surface-raised)", html);
        Assert.Contains("themeColor", html);
        if (expectedId == "track-map")
        {
            Assert.Contains("track-map-page", html);
            Assert.Contains("fetch('/api/track-map'", html);
            Assert.Contains("renderOffline()", html);
            Assert.Contains("let cachedTrackMapSettings", html);
            Assert.Contains("row.hasSpatialProgress === false", html);
            Assert.Contains("\"refreshIntervalMilliseconds\":100", html);
            Assert.Contains(": null", html);
            Assert.Contains("stroke=\"var(--tmr-cyan)\"", html);
            Assert.Contains("fill=\"var(--tmr-title)\"", html);
        }
        if (expectedId == "standings")
        {
            Assert.Contains("hasStandingDriverIdentity", html);
            Assert.Contains("fetch('/api/standings'", html);
            Assert.Contains("otherClassRowsPerClass", html);
            Assert.Contains("width: 35", html);
            Assert.Contains("width: 50", html);
            Assert.Contains("width: 250", html);
            Assert.Contains("width: 60", html);
            Assert.Contains("width: 30", html);
            Assert.Contains("class-header", html);
            Assert.Contains("colspan=\"${Math.max(1, headers.length)}\"", html);
            Assert.Contains("class-header-band", html);
        }
        if (expectedId == "relative")
        {
            Assert.Contains("models?.relative", html);
            Assert.Contains("fetch('/api/relative'", html);
            Assert.Contains("relativeSettings", html);
            Assert.Contains("width: 38", html);
            Assert.Contains("width: 250", html);
            Assert.Contains("width: 70", html);
            Assert.Contains("row.onPitRoad ? 'IN'", html);
        }
        if (expectedId == "pit-service")
        {
            Assert.Contains("YELLOW - optional repair", html);
            Assert.Contains("hasFastRepairSelected", html);
            Assert.Contains("pitStatus.complete", html);
            Assert.Contains("pitValueChanged", html);
            Assert.Contains("metric highlight", html);
        }
        if (expectedId == "input-state")
        {
            Assert.Contains("waiting for player in car", html);
            Assert.Contains("brakeAbsActive", html);
            Assert.Contains("var(--tmr-green)", html);
            Assert.Contains("themeColor('--tmr-green'", html);
            Assert.DoesNotContain("tractionControlActive", html);
        }
        if (expectedId == "car-radar")
        {
            Assert.Contains("radar-v2", html);
            Assert.Contains("body.car-radar-page .overlay", html);
            Assert.Contains("classColorCss(car.carClassColorHex)", html);
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
            Assert.Contains("preview visible", html);
            Assert.Contains("shouldFailClosed", html);
            Assert.Contains("isGarageVisible", html);
        }
    }

    [Fact]
    public void TryRender_BrowserV2Contract_DoesNotRenderLegacyV1ColorsOrPositionPrefixes()
    {
        var legacyFragments = new[]
        {
            "#0e1215",
            "#e7edf2",
            "#edf4f8",
            "#62c7ff",
            "#68c1ff",
            "#4dd77a",
            "#ff6b63",
            "#ffd166",
            "#d9e4eb",
            "rgba(98,199,255",
            "rgba(98, 199, 255",
            "rgba(222,238,246",
            "rgb(8,14,18)",
            "rgb(5,12,16)",
            "`P${position}`",
            "`C${position}`"
        };

        foreach (var route in BrowserOverlayPageRenderer.Routes)
        {
            Assert.True(BrowserOverlayPageRenderer.TryRender(route, out var html));
            foreach (var fragment in legacyFragments)
            {
                Assert.DoesNotContain(fragment, html);
            }
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
