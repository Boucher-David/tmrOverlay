let garageCoverSettings = { hasImage: false, imageVersion: null, fallbackReason: 'not_configured', previewVisible: false };
let garageCoverDisplayModel = null;
let lastGarageCoverRenderKey = '';

TmrBrowserOverlay.register({
  async beforeRefresh() {
    garageCoverDisplayModel = await fetchOverlayModel('garage-cover');
  },
  render() {
    renderGarageCover(garageCoverDisplayModel);
  },
  renderOffline() {
    renderGarageCover(null);
  }
});

function renderGarageCover(model) {
  const garageCover = model?.garageCover;
  const settings = garageCover?.browserSettings || garageCoverSettings;
  const detection = garageCover?.detection || { displayText: 'localhost offline', isFresh: false };
  const shouldCover = garageCover?.shouldCover ?? true;
  overlayEl.style.opacity = shouldCover ? '1' : '0';

  const renderKey = `${settings.hasImage}:${settings.imageVersion ?? ''}:${settings.fallbackReason ?? ''}`;
  if (renderKey !== lastGarageCoverRenderKey) {
    lastGarageCoverRenderKey = renderKey;
    contentEl.innerHTML = garageCoverContent(settings);
  }

  renderHeaderItems(model, model?.status || detection.displayText || 'garage cover');
  renderFooterSource(model);
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
