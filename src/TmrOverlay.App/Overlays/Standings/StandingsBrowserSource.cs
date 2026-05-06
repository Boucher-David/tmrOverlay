using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.Standings;

internal static class StandingsBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: StandingsOverlayDefinition.Definition.Id,
        title: StandingsOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/standings",
        fadeWhenTelemetryUnavailable: StandingsOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    TmrBrowserOverlay.register({
      render(live) {
        const timing = live?.models?.timing;
        const rows = (timing?.classRows?.length ? timing.classRows : timing?.overallRows || []).slice(0, 14);
        contentEl.innerHTML = rowsTable([
          { label: 'Pos', value: (row) => `<span class="pill">P${row.classPosition ?? row.overallPosition ?? '--'}</span>` },
          { label: 'Car', value: (row) => escapeHtml(carNumber(row)) },
          { label: 'Driver', value: (row) => escapeHtml(driverName(row)) },
          { label: 'Leader', value: (row) => formatSeconds(row.gapSecondsToClassLeader) },
          { label: 'Focus', value: (row) => formatSeconds(row.deltaSecondsToFocus) },
          { label: 'Pit', value: (row) => row.onPitRoad ? 'IN' : '' }
        ], rows);
        setStatus(live, timing?.hasData ? `live | ${quality(timing)}` : 'waiting for standings');
      }
    });
    """;
}
