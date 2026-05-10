let gapDisplayModel = null;

TmrBrowserOverlay.register({
  async beforeRefresh() {
    gapDisplayModel = await fetchOverlayModel('gap-to-leader');
  },
  render() {
    renderOverlayModel(gapDisplayModel);
  }
});
