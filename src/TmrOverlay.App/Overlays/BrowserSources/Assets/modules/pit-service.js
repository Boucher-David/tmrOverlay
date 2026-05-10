let pitServiceDisplayModel = null;

TmrBrowserOverlay.register({
  async beforeRefresh() {
    pitServiceDisplayModel = await fetchOverlayModel('pit-service');
  },
  render() {
    renderOverlayModel(pitServiceDisplayModel);
  }
});
