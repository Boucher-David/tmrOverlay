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
        fadeWhenTelemetryUnavailable: false,
        refreshIntervalMilliseconds: 250,
        script: Script);

    private const string Script = """
    let garageCoverSettings = { hasImage: false, imageVersion: null, fallbackReason: 'not_configured', previewVisible: false };
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

      nextGarageCoverSettingsFetchAt = Date.now() + 1000;
      try {
        const response = await fetch('/api/garage-cover', { cache: 'no-store' });
        if (!response.ok) return;
        const payload = await response.json();
        garageCoverSettings = payload.garageCover || garageCoverSettings;
      } catch {
        garageCoverSettings = { hasImage: false, imageVersion: null, fallbackReason: 'settings_unavailable', previewVisible: false };
      }
    }

    function renderGarageCover(live) {
      const detection = garageCoverDetection(live);
      const shouldCover = Boolean(garageCoverSettings.previewVisible)
        || detection.shouldFailClosed
        || detection.isGarageVisible;
      overlayEl.style.opacity = shouldCover ? '1' : '0';

      const renderKey = `${garageCoverSettings.hasImage}:${garageCoverSettings.imageVersion ?? ''}:${garageCoverSettings.fallbackReason ?? ''}`;
      if (renderKey !== lastGarageCoverRenderKey) {
        lastGarageCoverRenderKey = renderKey;
        contentEl.innerHTML = garageCoverContent(garageCoverSettings);
      }

      setStatus(live, garageCoverSettings.previewVisible ? 'preview visible' : detection.status);
    }

    function garageCoverContent(settings) {
      if (settings?.hasImage) {
        const version = encodeURIComponent(settings.imageVersion || 'latest');
        return `
          <div class="garage-cover">
            <img alt="" src="/api/garage-cover/image?v=${version}" onerror="const p=this.parentElement;p.classList.add('garage-cover-fallback');this.remove();p.innerHTML='<div>TMR</div>';">
          </div>`;
      }

      return '<div class="garage-cover garage-cover-fallback"><div>TMR</div></div>';
    }

    function garageCoverDetection(live) {
      if (!live?.isConnected) {
        return { status: 'iRacing disconnected', isGarageVisible: false, shouldFailClosed: true };
      }
      if (!live?.isCollecting) {
        return { status: 'waiting for telemetry', isGarageVisible: false, shouldFailClosed: true };
      }
      if (!isLiveTelemetryAvailable(live)) {
        return { status: 'telemetry stale', isGarageVisible: false, shouldFailClosed: true };
      }

      const isGarageVisible = live?.models?.raceEvents?.isGarageVisible === true;
      return {
        status: isGarageVisible ? 'garage visible' : 'garage hidden',
        isGarageVisible,
        shouldFailClosed: false
      };
    }
    """;
}
