let sessionWeatherDisplayModel = null;

TmrBrowserOverlay.register({
  async beforeRefresh() {
    sessionWeatherDisplayModel = await fetchOverlayModel('session-weather');
  },
  render() {
    renderOverlayModel(sessionWeatherDisplayModel);
  }
});
