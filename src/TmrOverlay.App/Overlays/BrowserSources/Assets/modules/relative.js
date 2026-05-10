let relativeDisplayModel = null;

TmrBrowserOverlay.register({
  async beforeRefresh() {
    relativeDisplayModel = await fetchOverlayModel('relative');
  },
  render() {
    renderOverlayModel(relativeDisplayModel);
  }
});
