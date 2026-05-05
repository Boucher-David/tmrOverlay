using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.Relative;

internal static class RelativeBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: RelativeOverlayDefinition.Definition.Id,
        title: RelativeOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/relative",
        script: Script);

    private const string Script = """
    TmrBrowserOverlay.register({
      render(live) {
        const relative = live?.models?.relative;
        const rows = [...(relative?.rows || [])]
          .sort((left, right) => Math.abs(left.relativeSeconds ?? 9999) - Math.abs(right.relativeSeconds ?? 9999))
          .slice(0, 12);
        contentEl.innerHTML = rowsTable([
          { label: 'Dir', value: (row) => row.isAhead ? 'Ahead' : row.isBehind ? 'Behind' : 'Near' },
          { label: 'Pos', value: (row) => row.classPosition ? `P${row.classPosition}` : '--' },
          { label: 'Driver', value: (row) => escapeHtml(driverName(row)) },
          { label: 'Gap', value: (row) => formatSeconds(row.relativeSeconds) },
          { label: 'Pit', value: (row) => row.onPitRoad ? 'IN' : '' }
        ], rows);
        setStatus(live, relative?.hasData ? `live | ${quality(relative)}` : 'waiting for relative');
      }
    });
    """;
}
