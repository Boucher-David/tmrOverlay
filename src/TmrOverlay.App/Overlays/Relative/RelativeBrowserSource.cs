using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.Relative;

internal static class RelativeBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: RelativeOverlayDefinition.Definition.Id,
        title: RelativeOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/relative",
        fadeWhenTelemetryUnavailable: RelativeOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    const defaultRelativeColumns = [
      { id: 'relative.position', label: 'Pos', dataKey: 'relative-position', width: 58, alignment: 'right' },
      { id: 'relative.driver', label: 'Driver', dataKey: 'driver', width: 300, alignment: 'left' },
      { id: 'relative.gap', label: 'Gap', dataKey: 'gap', width: 92, alignment: 'right' }
    ];
    const defaultRelativeSettings = {
      carsAhead: 5,
      carsBehind: 5,
      columns: defaultRelativeColumns
    };
    let relativeSettings = defaultRelativeSettings;
    let nextRelativeSettingsFetchAt = 0;

    TmrBrowserOverlay.register({
      async beforeRefresh() {
        await refreshRelativeSettings();
      },
      render(live) {
        const relative = live?.models?.relative;
        const rows = relativeRows(live, relativeSettings);
        contentEl.innerHTML = rowsTable(relativeColumnHeaders(), rows);
        setStatus(live, relative?.hasData ? relativeStatus(live, rows) : 'waiting for relative');
      }
    });

    async function refreshRelativeSettings() {
      if (Date.now() < nextRelativeSettingsFetchAt) {
        return;
      }

      nextRelativeSettingsFetchAt = Date.now() + 2000;
      try {
        const response = await fetch('/api/relative', { cache: 'no-store' });
        if (!response.ok) return;
        const payload = await response.json();
        relativeSettings = normalizeRelativeSettings(payload.relativeSettings || relativeSettings);
      } catch {
        relativeSettings = defaultRelativeSettings;
      }
    }

    function normalizeRelativeSettings(settings) {
      const columns = Array.isArray(settings?.columns) && settings.columns.length
        ? settings.columns
        : defaultRelativeColumns;
      return {
        carsAhead: settings?.carsAhead ?? defaultRelativeSettings.carsAhead,
        carsBehind: settings?.carsBehind ?? defaultRelativeSettings.carsBehind,
        columns
      };
    }

    function relativeColumnHeaders() {
      return normalizeColumns(relativeSettings.columns, defaultRelativeColumns)
        .map((column) => ({
          label: column.label,
          width: column.width,
          align: column.alignment,
          value: (row) => relativeColumnValue(row, column.dataKey)
        }));
    }

    function relativeColumnValue(row, dataKey) {
      switch (dataKey) {
        case 'relative-position':
          return row.positionLabel || '--';
        case 'driver':
          return escapeHtml(relativeDriver(row));
        case 'gap':
          return row.gapLabel || '--';
        case 'pit':
          return row.onPitRoad ? 'IN' : '';
        default:
          return '';
      }
    }

    function normalizeColumns(columns, fallbackColumns) {
      const normalized = (Array.isArray(columns) ? columns : [])
        .map((column) => ({
          id: String(column?.id || ''),
          label: String(column?.label || ''),
          dataKey: normalizeRelativeDataKey(column?.dataKey, column?.id),
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

    function normalizeRelativeDataKey(value, columnId) {
      const normalized = String(value || '').toLowerCase();
      if (['relative-position', 'driver', 'gap', 'pit'].includes(normalized)) {
        return normalized;
      }

      const legacyId = String(columnId || '').toLowerCase();
      switch (legacyId) {
        case 'position':
        case 'relative.position':
          return 'relative-position';
        case 'driver':
        case 'relative.driver':
          return 'driver';
        case 'gap':
        case 'relative.gap':
          return 'gap';
        case 'pit':
        case 'relative.pit':
          return 'pit';
        default:
          return '';
      }
    }

    function relativeRows(live, settings) {
      const relative = live?.models?.relative || {};
      const scoringByCarIdx = new Map((live?.models?.scoring?.rows || []).map((row) => [row.carIdx, row]));
      const driverByCarIdx = new Map((live?.models?.driverDirectory?.drivers || []).map((row) => [row.carIdx, row]));
      const timingByCarIdx = timingRows(live);
      const reference = referenceRow(live, scoringByCarIdx, driverByCarIdx, timingByCarIdx);
      const rows = relative.rows || [];
      const carsAhead = clamp(Number(settings?.carsAhead), 0, 8);
      const carsBehind = clamp(Number(settings?.carsBehind), 0, 8);
      const ahead = rows
        .filter((row) => row.isAhead)
        .sort((left, right) => relativeSortKey(left) - relativeSortKey(right) || left.carIdx - right.carIdx)
        .slice(0, carsAhead)
        .sort((left, right) => relativeSortKey(right) - relativeSortKey(left) || left.carIdx - right.carIdx)
        .map((row) => decorateRelativeRow(row, scoringByCarIdx, driverByCarIdx, 'ahead'));
      const behind = rows
        .filter((row) => row.isBehind)
        .sort((left, right) => relativeSortKey(left) - relativeSortKey(right) || left.carIdx - right.carIdx)
        .slice(0, carsBehind)
        .map((row) => decorateRelativeRow(row, scoringByCarIdx, driverByCarIdx, 'behind'));
      return reference ? [...ahead, reference, ...behind] : [...ahead, ...behind];
    }

    function referenceRow(live, scoringByCarIdx, driverByCarIdx, timingByCarIdx) {
      const timing = live?.models?.timing || {};
      const referenceCarIdx = live?.models?.relative?.referenceCarIdx
        ?? timing.focusRow?.carIdx
        ?? timing.playerRow?.carIdx
        ?? timing.focusCarIdx
        ?? timing.playerCarIdx
        ?? null;
      if (!Number.isFinite(referenceCarIdx)) return null;
      const timingRow = timingByCarIdx.get(referenceCarIdx) || null;
      const scoringRow = scoringByCarIdx.get(referenceCarIdx);
      const driver = driverByCarIdx.get(referenceCarIdx);
      return {
        carIdx: referenceCarIdx,
        isFocus: true,
        rowKind: 'car',
        positionLabel: positionLabel(timingRow, scoringRow),
        carNumber: scoringRow?.carNumber || driver?.carNumber || timingRow?.carNumber,
        driverName: firstText(timingRow?.driverName, scoringRow?.driverName, scoringRow?.teamName, driver?.driverName),
        gapLabel: '0.000',
        onPitRoad: timingRow?.onPitRoad === true,
        carClassColorHex: scoringRow?.carClassColorHex || driver?.carClassColorHex || timingRow?.carClassColorHex
      };
    }

    function decorateRelativeRow(row, scoringByCarIdx, driverByCarIdx, direction) {
      const scoringRow = scoringByCarIdx.get(row.carIdx);
      const driver = driverByCarIdx.get(row.carIdx);
      return {
        ...row,
        rowKind: 'car',
        positionLabel: positionLabel(row, scoringRow),
        carNumber: scoringRow?.carNumber || driver?.carNumber,
        driverName: firstText(row.driverName, scoringRow?.driverName, scoringRow?.teamName, driver?.driverName),
        gapLabel: relativeGap(row, direction),
        carClassColorHex: scoringRow?.carClassColorHex || driver?.carClassColorHex
      };
    }

    function relativeStatus(live, rows) {
      const relative = live?.models?.relative || {};
      const shown = rows.filter((row) => !row.isFocus).length;
      const available = (relative.rows || []).length;
      const reference = rows.find((row) => row.isFocus);
      const prefix = reference?.positionLabel || 'live relative';
      return available > shown ? `${prefix} - ${shown}/${available} cars` : `${prefix} - ${shown} cars`;
    }

    function relativeDriver(row) {
      const label = row.driverName || row.teamName || `Car ${row.carIdx ?? '--'}`;
      return row.carNumber ? `#${String(row.carNumber).replace(/^#/, '')} ${label}` : label;
    }

    function relativeGap(row, direction) {
      const sign = direction === 'ahead' ? '+' : '-';
      if (Number.isFinite(row.relativeSeconds)) return `${sign}${Math.abs(row.relativeSeconds).toFixed(3)}`;
      if (Number.isFinite(row.relativeMeters)) return `${sign}${Math.abs(row.relativeMeters).toFixed(0)}m`;
      if (Number.isFinite(row.relativeLaps)) return `${sign}${Math.abs(row.relativeLaps).toFixed(3)}L`;
      return '--';
    }

    function relativeSortKey(row) {
      if (Number.isFinite(row.relativeSeconds)) return Math.abs(row.relativeSeconds);
      if (Number.isFinite(row.relativeMeters)) return Math.abs(row.relativeMeters);
      if (Number.isFinite(row.relativeLaps)) return Math.abs(row.relativeLaps);
      return Number.MAX_VALUE;
    }

    function positionLabel(row, fallbackRow) {
      const classPosition = row?.classPosition ?? fallbackRow?.classPosition;
      if (Number.isFinite(classPosition) && classPosition > 0) return `${classPosition}`;
      const overallPosition = row?.overallPosition ?? fallbackRow?.overallPosition;
      return Number.isFinite(overallPosition) && overallPosition > 0 ? `${overallPosition}` : null;
    }

    function timingRows(live) {
      const timing = live?.models?.timing || {};
      return new Map([...(timing.overallRows || []), ...(timing.classRows || []), timing.focusRow, timing.playerRow]
        .filter((row) => row && Number.isFinite(row.carIdx))
        .map((row) => [row.carIdx, row]));
    }

    function firstText(...values) {
      return values.find((value) => typeof value === 'string' && value.trim().length > 0) || null;
    }

    function clamp(value, minimum, maximum) {
      return Math.max(minimum, Math.min(maximum, Number.isFinite(value) ? value : minimum));
    }
    """;
}
