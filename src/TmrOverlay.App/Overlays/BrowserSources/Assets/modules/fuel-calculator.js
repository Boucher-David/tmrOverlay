let fuelDisplayModel = null;

TmrBrowserOverlay.register({
  async beforeRefresh() {
    fuelDisplayModel = await fetchOverlayModel('fuel-calculator');
  },
  render() {
    renderOverlayModel(fuelDisplayModel);
  }
});
