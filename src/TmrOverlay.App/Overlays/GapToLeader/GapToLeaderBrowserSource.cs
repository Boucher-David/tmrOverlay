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
        const usableGap = (row) => row?.gapEvidence?.isUsable === true;
        const formatGap = (seconds, laps, legacy) => {
          if (seconds != null) {
            return formatSeconds(seconds);
          }
          if (laps != null) {
            return `+${Number(laps).toFixed(2)} lap`;
          }
          if (legacy?.isLeader) {
            return 'LEADER';
          }
          return formatSeconds(legacy?.seconds);
        };
        const session = live?.models?.session || {};
        const sessionName = `${session.sessionType || session.sessionName || session.eventType || ''}`.toLowerCase();
        if (sessionName && !sessionName.includes('race')) {
          contentEl.innerHTML = `<div class="grid">${metric('Mode', 'Race only')}</div>`;
          setStatus(live, 'race sessions only');
          return;
        }
        const timing = live?.models?.timing || {};
        const progress = live?.models?.raceProgress || {};
        const legacyGap = live?.leaderGap || {};
        const rows = timing.classRows || [];
        const focus = timing.focusRow || {};
        const hasModelGap = timing.hasData || progress.hasData;
        const classGapSeconds = usableGap(focus) ? focus.gapSecondsToClassLeader : null;
        const classGapLaps = usableGap(focus) ? focus.gapLapsToClassLeader : progress.referenceClassLeaderGapLaps;
        const classPosition = focus.classPosition || progress.referenceClassPosition || legacyGap.referenceClassPosition;
        const cars = hasModelGap
          ? rows
              .filter(row => row.isClassLeader || row.isFocus || usableGap(row) || row.deltaSecondsToFocus != null)
              .map(row => ({
                classPosition: row.classPosition,
                carIdx: row.carIdx,
                isClassLeader: row.isClassLeader,
                isFocus: row.isFocus,
                gapSecondsToClassLeader: row.isClassLeader ? 0 : usableGap(row) ? row.gapSecondsToClassLeader : null,
                gapLapsToClassLeader: row.isClassLeader ? 0 : usableGap(row) ? row.gapLapsToClassLeader : null,
                deltaSecondsToReference: usableGap(row) ? row.deltaSecondsToFocus : null
              }))
          : legacyGap.classCars || [];
        const summary = `
          <div class="grid" style="margin-bottom: 10px;">
            ${metric('Class pos', classPosition ? `P${classPosition}` : '--')}
            ${metric('Class leader', formatGap(classGapSeconds, classGapLaps, legacyGap.classLeaderGap))}
          </div>`;
        contentEl.innerHTML = summary + rowsTable([
          { label: 'Pos', value: (row) => row.classPosition ? `P${row.classPosition}` : '--' },
          { label: 'Car', value: (row) => `#${row.carIdx}` },
          { label: 'Leader', value: (row) => row.isClassLeader ? 'LEADER' : formatGap(row.gapSecondsToClassLeader, row.gapLapsToClassLeader) },
          { label: 'Focus', value: (row) => formatSeconds(row.deltaSecondsToReference) }
        ], cars.slice(0, 10));
        setStatus(live, (hasModelGap || legacyGap.hasData) ? 'live | race gap' : 'waiting for timing');
      }
    });
    """;
}
