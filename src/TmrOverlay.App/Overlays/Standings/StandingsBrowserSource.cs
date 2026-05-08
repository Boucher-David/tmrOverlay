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
    const defaultStandingsColumns = [
      { id: 'standings.class-position', label: 'CLS', dataKey: 'class-position', width: 54, alignment: 'right' },
      { id: 'standings.car-number', label: 'CAR', dataKey: 'car-number', width: 66, alignment: 'right' },
      { id: 'standings.driver', label: 'Driver', dataKey: 'driver', width: 300, alignment: 'left' },
      { id: 'standings.gap', label: 'GAP', dataKey: 'gap', width: 92, alignment: 'right' },
      { id: 'standings.interval', label: 'INT', dataKey: 'interval', width: 92, alignment: 'right' },
      { id: 'standings.pit', label: 'PIT', dataKey: 'pit', width: 46, alignment: 'right' }
    ];
    const defaultStandingsSettings = {
      maximumRows: 14,
      classSeparatorsEnabled: true,
      otherClassRowsPerClass: 2,
      columns: defaultStandingsColumns
    };
    let standingsSettings = defaultStandingsSettings;
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
        const classSeparatorsEnabled = standingsSettings.classSeparatorsEnabled !== false;
        const otherClassRows = clamp(Number(standingsSettings.otherClassRowsPerClass), 0, 6);
        const rows = scoring?.hasData
          ? scoringRows(live, scoring, timing, referenceCarIdx, maximumRows, otherClassRows, classSeparatorsEnabled)
          : (timing?.classRows?.length ? timing.classRows : timing?.overallRows || [])
            .filter((row) => hasStandingDriverIdentity(row, referenceCarIdx))
            .slice(0, maximumRows)
            .map((row) => ({ ...row, rowKind: 'car' }));
        contentEl.innerHTML = rowsTable(standingsColumnHeaders(), rows);
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
        standingsSettings = normalizeStandingsSettings(payload.standingsSettings || standingsSettings);
      } catch {
        standingsSettings = defaultStandingsSettings;
      }
    }

    function normalizeStandingsSettings(settings) {
      const columns = Array.isArray(settings?.columns) && settings.columns.length
        ? settings.columns
        : defaultStandingsColumns;
      return {
        maximumRows: settings?.maximumRows ?? defaultStandingsSettings.maximumRows,
        classSeparatorsEnabled: settings?.classSeparatorsEnabled ?? defaultStandingsSettings.classSeparatorsEnabled,
        otherClassRowsPerClass: settings?.otherClassRowsPerClass ?? defaultStandingsSettings.otherClassRowsPerClass,
        columns
      };
    }

    function standingsColumnHeaders() {
      return normalizeColumns(standingsSettings.columns, defaultStandingsColumns)
        .map((column) => ({
          label: column.label,
          width: column.width,
          align: column.alignment,
          value: (row) => standingsColumnValue(row, column.dataKey)
        }));
    }

    function standingsColumnValue(row, dataKey) {
      switch (dataKey) {
        case 'class-position':
          return row.rowKind === 'header' ? '' : `<span class="pill">${row.classPosition ? `C${row.classPosition}` : '--'}</span>`;
        case 'car-number':
          return row.rowKind === 'header' ? '' : escapeHtml(carNumber(row));
        case 'driver':
          return escapeHtml(driverName(row));
        case 'gap':
          return row.rowKind === 'header' ? escapeHtml(`${row.rowCount || 0} cars`) : leaderGap(row);
        case 'interval':
          return row.rowKind === 'header' ? escapeHtml(row.estimatedLapsLabel || '') : formatSeconds(row.deltaSecondsToFocus);
        case 'pit':
          return row.rowKind === 'header' ? '' : row.onPitRoad ? 'IN' : '';
        default:
          return '';
      }
    }

    function normalizeColumns(columns, fallbackColumns) {
      const normalized = (Array.isArray(columns) ? columns : [])
        .map((column) => ({
          id: String(column?.id || ''),
          label: String(column?.label || ''),
          dataKey: normalizeStandingsDataKey(column?.dataKey, column?.id),
          width: clamp(Number(column?.width), 34, 520),
          alignment: normalizeColumnAlignment(column?.alignment ?? column?.align, column?.id, fallbackColumns)
        }))
        .filter((column) => column.id && column.label && column.dataKey);
      return normalized.length ? normalized : fallbackColumns;
    }

    function normalizeColumnAlignment(value, columnId, fallbackColumns) {
      const normalized = String(value || '').toLowerCase();
      const id = String(columnId || '');
      if (['left', 'right', 'center'].includes(normalized)) {
        return normalized;
      }

      const fallback = (fallbackColumns || []).find((column) => column.id === id);
      if (fallback) {
        return normalizeColumnAlignment(fallback.alignment ?? fallback.align, null, []);
      }

      return id === 'driver' || id.endsWith('.driver') ? 'left' : 'right';
    }

    function normalizeStandingsDataKey(value, columnId) {
      const normalized = String(value || '').toLowerCase();
      if (['class-position', 'car-number', 'driver', 'gap', 'interval', 'pit'].includes(normalized)) {
        return normalized;
      }

      const legacyId = String(columnId || '').toLowerCase();
      switch (legacyId) {
        case 'class':
        case 'standings.class-position':
          return 'class-position';
        case 'car':
        case 'standings.car-number':
          return 'car-number';
        case 'driver':
        case 'standings.driver':
          return 'driver';
        case 'gap':
        case 'standings.gap':
          return 'gap';
        case 'interval':
        case 'standings.interval':
          return 'interval';
        case 'pit':
        case 'standings.pit':
          return 'pit';
        default:
          return '';
      }
    }

    function scoringRows(live, scoring, timing, referenceCarIdx, maximumRows, otherClassRows, classSeparatorsEnabled) {
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
      const otherGroups = groups.filter((group) => classSeparatorsEnabled && group !== primary && otherClassRows > 0);
      const includeHeaders = classSeparatorsEnabled && groups.length > 1;
      const reservedOtherRows = otherGroups.length * (includeHeaders ? 1 + otherClassRows : otherClassRows);
      const minimumPrimaryRows = Math.min(maximumRows, includeHeaders ? 2 : 1);
      const primaryLimit = clamp(maximumRows - reservedOtherRows, minimumPrimaryRows, maximumRows);
      const rows = [];
      appendScoringGroup(live, rows, primary, timingByCarIdx, referenceCarIdx, maximumRows, primaryLimit, includeHeaders);
      for (const group of otherGroups) {
        if (rows.length >= maximumRows) break;
        appendScoringGroup(
          live,
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

    function appendScoringGroup(live, rows, group, timingByCarIdx, referenceCarIdx, maximumRows, groupLimit, includeHeader) {
      if (groupLimit <= 0 || rows.length >= maximumRows) return;
      if (includeHeader) {
        rows.push({
          rowKind: 'header',
          driverName: group.className || 'Class',
          rowCount: group.rowCount || 0,
          estimatedLapsLabel: classEstimatedLaps(group, live),
          carClassColorHex: group.carClassColorHex
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

    function classEstimatedLaps(group, live) {
      const projection = (live?.models?.raceProjection?.classProjections || [])
        .find((candidate) => candidate.carClass === group?.carClass);
      if (Number.isFinite(projection?.estimatedLapsRemaining)
        && projection.estimatedLapsRemaining >= 0
        && projection.estimatedLapsRemaining < 1000) {
        const laps = projection.estimatedLapsRemaining;
        return `~${Number(laps).toFixed(laps % 1 === 0 ? 0 : 1)} laps`;
      }

      const pace = (group?.rows || [])
        .map((row) => row.lastLapTimeSeconds ?? row.bestLapTimeSeconds)
        .find(isUsableLapTime)
        ?? live?.models?.raceProgress?.racePaceSeconds;
      const remaining = live?.models?.session?.sessionTimeRemainSeconds;
      if (Number.isFinite(remaining) && remaining > 0 && isUsableLapTime(pace)) {
        return `~${Math.ceil(remaining / pace + 1)} laps`;
      }

      const laps = live?.models?.raceProgress?.raceLapsRemaining;
      if (Number.isFinite(laps) && laps >= 0 && laps < 1000) {
        return `~${Number(laps).toFixed(laps % 1 === 0 ? 0 : 1)} laps`;
      }

      return '';
    }

    function isUsableLapTime(seconds) {
      return Number.isFinite(seconds) && seconds > 20 && seconds < 1800;
    }

    function groupFirstPosition(group) {
      return Math.min(...(group?.rows || []).map((row) => row.overallPosition || 999999));
    }

    function clamp(value, minimum, maximum) {
      return Math.max(minimum, Math.min(maximum, Number.isFinite(value) ? value : minimum));
    }
    """;
}
