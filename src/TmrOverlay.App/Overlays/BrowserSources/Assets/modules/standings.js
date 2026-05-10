let standingsDisplayModel = null;

TmrBrowserOverlay.register({
  async beforeRefresh() {
    standingsDisplayModel = await fetchOverlayModel('standings');
  },
  render() {
    renderOverlayModel(standingsDisplayModel);
  }
});
