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
    let standingsSettings = { maximumRows: 14, otherClassRowsPerClass: 2 };
    let nextStandingsSettingsFetchAt = 0;

    TmrBrowserOverlay.register({
      async beforeRefresh() {
        await refreshStandingsSettings();
      },
      render(live) {
        const timing = live?.models?.timing;
        const scoring = live?.models?.scoring;
        const coverage = live?.models?.coverage || {};
        const referenceCarIdx = scoring?.referenceCarIdx
          ?? timing?.focusRow?.carIdx
          ?? timing?.playerRow?.carIdx
          ?? timing?.focusCarIdx
          ?? timing?.playerCarIdx
          ?? null;
        const maximumRows = clamp(Number(standingsSettings.maximumRows), 1, 20);
        const otherClassRows = clamp(Number(standingsSettings.otherClassRowsPerClass), 0, 6);
        const rows = scoring?.hasData
          ? scoringRows(scoring, timing, referenceCarIdx, maximumRows, otherClassRows)
          : (timing?.classRows?.length ? timing.classRows : timing?.overallRows || [])
            .filter((row) => hasStandingDriverIdentity(row, referenceCarIdx))
            .slice(0, maximumRows)
            .map((row) => ({ ...row, rowKind: 'car' }));
        contentEl.innerHTML = rowsTable([
          { label: 'Pos', value: (row) => row.rowKind === 'header' ? '' : `<span class="pill">P${row.classPosition ?? row.overallPosition ?? '--'}</span>` },
          { label: 'Car', value: (row) => row.rowKind === 'header' ? '' : escapeHtml(carNumber(row)) },
          { label: 'Driver', value: (row) => escapeHtml(driverName(row)) },
          { label: 'Leader', value: (row) => row.rowKind === 'header' ? escapeHtml(`${row.rowCount || 0} cars`) : leaderGap(row) },
          { label: 'Focus', value: (row) => row.rowKind === 'header' ? '' : formatSeconds(row.deltaSecondsToFocus) },
          { label: 'Pit', value: (row) => row.rowKind === 'header' ? '' : row.onPitRoad ? 'IN' : '' }
        ], rows);
        setStatus(live, scoring?.hasData
          ? `scoring | ${coverage.liveScoringRowCount || 0}/${coverage.resultRowCount || 0} live`
          : timing?.hasData ? `live | ${quality(timing)}` : 'waiting for standings');
      }
    });

    async function refreshStandingsSettings() {
      if (Date.now() < nextStandingsSettingsFetchAt) {
        return;
      }

      nextStandingsSettingsFetchAt = Date.now() + 2000;
      try {
        const response = await fetch('/api/standings', { cache: 'no-store' });
        if (!response.ok) return;
        const payload = await response.json();
        standingsSettings = payload.standingsSettings || standingsSettings;
      } catch {
        standingsSettings = { maximumRows: 14, otherClassRowsPerClass: 2 };
      }
    }

    function scoringRows(scoring, timing, referenceCarIdx, maximumRows, otherClassRows) {
      const timingByCarIdx = new Map([
        ...(timing?.overallRows || []),
        ...(timing?.classRows || [])
      ].map((row) => [row.carIdx, row]));
      const groups = (scoring.classGroups || []).length
        ? [...scoring.classGroups]
        : [{ className: 'Standings', isReferenceClass: true, rowCount: scoring.rows?.length || 0, rows: scoring.rows || [] }];
      groups.sort((left, right) =>
        Number(right.isReferenceClass) - Number(left.isReferenceClass)
        || groupFirstPosition(left) - groupFirstPosition(right)
        || (left.carClass ?? 999999) - (right.carClass ?? 999999));
      const primary = groups.find((group) => group.isReferenceClass) || groups[0];
      const otherGroups = groups.filter((group) => group !== primary && otherClassRows > 0);
      const includeHeaders = groups.length > 1;
      const reservedOtherRows = otherGroups.length * (includeHeaders ? 1 + otherClassRows : otherClassRows);
      const minimumPrimaryRows = Math.min(maximumRows, includeHeaders ? 2 : 1);
      const primaryLimit = clamp(maximumRows - reservedOtherRows, minimumPrimaryRows, maximumRows);
      const rows = [];
      appendScoringGroup(rows, primary, timingByCarIdx, referenceCarIdx, maximumRows, primaryLimit, includeHeaders);
      for (const group of otherGroups) {
        if (rows.length >= maximumRows) break;
        appendScoringGroup(
          rows,
          group,
          timingByCarIdx,
          referenceCarIdx,
          maximumRows,
          Math.min(maximumRows - rows.length, includeHeaders ? 1 + otherClassRows : otherClassRows),
          includeHeaders);
      }
      return rows;
    }

    function appendScoringGroup(rows, group, timingByCarIdx, referenceCarIdx, maximumRows, groupLimit, includeHeader) {
      if (groupLimit <= 0 || rows.length >= maximumRows) return;
      if (includeHeader) {
        rows.push({
          rowKind: 'header',
          driverName: group.className || 'Class',
          rowCount: group.rowCount || 0
        });
        groupLimit -= 1;
      }
      for (const scoringRow of (group.rows || []).slice(0, Math.max(0, groupLimit))) {
        if (rows.length >= maximumRows) break;
        const timingRow = timingByCarIdx.get(scoringRow.carIdx) || {};
        rows.push({
          ...scoringRow,
          ...timingRow,
          rowKind: 'car',
          classPosition: scoringRow.classPosition,
          overallPosition: scoringRow.overallPosition,
          driverName: scoringRow.driverName || timingRow.driverName,
          teamName: scoringRow.teamName || timingRow.teamName,
          carNumber: scoringRow.carNumber || timingRow.carNumber,
          isFocus: scoringRow.isFocus || scoringRow.isPlayer || scoringRow.carIdx === referenceCarIdx,
          isPlayer: scoringRow.isPlayer,
          isClassLeader: scoringRow.classPosition === 1
        });
      }
    }

    function hasStandingDriverIdentity(row, referenceCarIdx) {
      return row?.isPlayer
        || row?.isFocus
        || row?.carIdx === referenceCarIdx
        || Boolean(row?.driverName)
        || Boolean(row?.teamName)
        || Boolean(row?.carNumber);
    }

    function leaderGap(row) {
      if (row?.isClassLeader || row?.classPosition === 1) return 'Leader';
      return formatSeconds(row?.gapSecondsToClassLeader);
    }

    function groupFirstPosition(group) {
      return Math.min(...(group?.rows || []).map((row) => row.overallPosition || 999999));
    }

    function clamp(value, minimum, maximum) {
      return Math.max(minimum, Math.min(maximum, Number.isFinite(value) ? value : minimum));
    }
    """;
}
