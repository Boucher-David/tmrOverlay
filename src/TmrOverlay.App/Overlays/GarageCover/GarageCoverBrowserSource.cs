using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.GarageCover;

internal static class GarageCoverBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: GarageCoverOverlayDefinition.Definition.Id,
        title: GarageCoverOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/garage-cover",
        bodyClass: "garage-cover-page",
        renderWhenTelemetryUnavailable: true,
        fadeWhenTelemetryUnavailable: true,
        refreshIntervalMilliseconds: 250,
        script: Script);

    private const string Script = """
    let garageCoverSettings = { hasImage: false, imageVersion: null };
    let nextGarageCoverSettingsFetchAt = 0;
    let lastGarageCoverRenderKey = '';

    TmrBrowserOverlay.register({
      async beforeRefresh() {
        await refreshGarageCoverSettings();
      },
      render(live) {
        renderGarageCover(live);
      },
      renderOffline() {
        renderGarageCover(null);
      }
    });

    async function refreshGarageCoverSettings() {
      if (Date.now() < nextGarageCoverSettingsFetchAt) {
        return;
      }

      nextGarageCoverSettingsFetchAt = Date.now() + 10000;
      try {
        const response = await fetch('/api/garage-cover', { cache: 'no-store' });
        if (!response.ok) return;
        const payload = await response.json();
        garageCoverSettings = payload.garageCover || garageCoverSettings;
      } catch {
        garageCoverSettings = { hasImage: false, imageVersion: null };
      }
    }

    function renderGarageCover(live) {
      const shouldCover = isLiveTelemetryAvailable(live)
        && live?.models?.raceEvents?.isGarageVisible === true;
      overlayEl.style.opacity = shouldCover ? '1' : '0';

      const renderKey = `${garageCoverSettings.hasImage}:${garageCoverSettings.imageVersion ?? ''}`;
      if (renderKey !== lastGarageCoverRenderKey) {
        lastGarageCoverRenderKey = renderKey;
        contentEl.innerHTML = garageCoverContent(garageCoverSettings);
      }

      setStatus(live, shouldCover ? 'garage visible' : 'garage hidden');
    }

    function garageCoverContent(settings) {
      if (settings?.hasImage) {
        const version = encodeURIComponent(settings.imageVersion || 'latest');
        return `
          <div class="garage-cover">
            <img alt="" src="/api/garage-cover/image?v=${version}">
          </div>`;
      }

      return '<div class="garage-cover garage-cover-fallback"><div>TMR</div></div>';
    }
    """;
}
