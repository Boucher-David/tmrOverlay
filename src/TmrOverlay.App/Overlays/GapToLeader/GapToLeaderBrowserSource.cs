using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.GapToLeader;

internal static class GapToLeaderBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: GapToLeaderOverlayDefinition.Definition.Id,
        title: GapToLeaderOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/gap-to-leader",
        fadeWhenTelemetryUnavailable: GapToLeaderOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    TmrBrowserOverlay.register({
      render(live) {
        const gap = live?.leaderGap || {};
        const cars = gap.classCars || [];
        const summary = `
          <div class="grid" style="margin-bottom: 10px;">
            ${metric('Class pos', gap.referenceClassPosition ? `P${gap.referenceClassPosition}` : '--')}
            ${metric('Class leader', formatSeconds(gap.classLeaderGap?.seconds))}
          </div>`;
        contentEl.innerHTML = summary + rowsTable([
          { label: 'Pos', value: (row) => row.classPosition ? `P${row.classPosition}` : '--' },
          { label: 'Car', value: (row) => `#${row.carIdx}` },
          { label: 'Leader', value: (row) => row.isClassLeader ? 'LEADER' : formatSeconds(row.gapSecondsToClassLeader) },
          { label: 'Focus', value: (row) => formatSeconds(row.deltaSecondsToReference) }
        ], cars.slice(0, 10));
        setStatus(live, gap.hasData ? 'live | gap' : 'waiting for timing');
      }
    });
    """;
}
