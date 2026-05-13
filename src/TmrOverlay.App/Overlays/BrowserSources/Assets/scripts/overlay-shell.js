    const page = {{PAGE_JSON}};
    const overlayEl = document.querySelector('.overlay');
    const statusEl = document.getElementById('status');
    const timeRemainingEl = document.getElementById('time-remaining');
    const contentEl = document.getElementById('content');
    const sourceEl = document.getElementById('source');
    let lastSequence = null;
    const browserOverlay = {
      module: null,
      register(module) {
        this.module = module;
      }
    };
    window.TmrBrowserOverlay = browserOverlay;

    function apiPath(path) {
      const url = new URL(path, window.location.href);
      const preview = new URLSearchParams(window.location.search).get('preview');
      if (['practice', 'qualifying', 'race'].includes(preview)) {
        url.searchParams.set('preview', preview);
      }

      return `${url.pathname}${url.search}`;
    }

    window.TmrBrowserApiPath = apiPath;

    const formatSeconds = (value) => {
      if (!Number.isFinite(value)) return '--';
      const sign = value > 0 ? '+' : value < 0 ? '-' : '';
      const absolute = Math.abs(value);
      if (absolute >= 60) {
        const minutes = Math.floor(absolute / 60);
        const seconds = absolute - minutes * 60;
        return `${sign}${minutes}:${seconds.toFixed(1).padStart(4, '0')}`;
      }
      return `${sign}${absolute.toFixed(1)}`;
    };

    const formatNumber = (value, digits = 1) => Number.isFinite(value) ? value.toFixed(digits) : '--';
    const formatPercent = (value) => Number.isFinite(value) ? `${Math.round(value * 100)}%` : '--';
    const formatSpeed = (value) => Number.isFinite(value) ? `${Math.round(value * 3.6)} km/h` : '--';
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#39;'
    })[char]);
    const escapeAttribute = escapeHtml;

    const driverName = (row) => row?.driverName || row?.teamName || `Car ${row?.carIdx ?? '--'}`;
    const carNumber = (row) => row?.carNumber ? `#${String(row.carNumber).replace(/^#/, '')}` : `#${row?.carIdx ?? '--'}`;
    const themeColor = (name, fallback) => getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
    const themeRgba = (name, alpha, fallback) => {
      const rgb = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
      return rgb ? `rgba(${rgb}, ${alpha})` : fallback;
    };
    const parsedHexColors = new Map();
    const classHeaderStyles = new Map();
    const browserModel = {
      model(live, name) {
        return live?.models?.[name] || {};
      },
      session(live) {
        return browserModel.model(live, 'session');
      },
      scoring(live) {
        return browserModel.model(live, 'scoring');
      },
      timing(live) {
        return browserModel.model(live, 'timing');
      },
      driverDirectory(live) {
        return browserModel.model(live, 'driverDirectory');
      },
      coverage(live) {
        return browserModel.model(live, 'coverage');
      },
      relative(live) {
        return browserModel.model(live, 'relative');
      },
      spatial(live) {
        return browserModel.model(live, 'spatial');
      },
      raceEvents(live) {
        return browserModel.model(live, 'raceEvents');
      },
      trackMap(live) {
        return browserModel.model(live, 'trackMap');
      },
      raceProgress(live) {
        return browserModel.model(live, 'raceProgress');
      },
      raceProjection(live) {
        return browserModel.model(live, 'raceProjection');
      },
      weather(live) {
        return browserModel.model(live, 'weather');
      },
      fuelPit(live) {
        return browserModel.model(live, 'fuelPit');
      },
      fuel(live) {
        return browserModel.fuelPit(live).fuel || live?.fuel || {};
      },
      inputs(live) {
        return browserModel.model(live, 'inputs');
      },
      currentSessionKind(live) {
        const session = browserModel.session(live);
        const selected = [
          session.sessionType,
          session.sessionName,
          session.eventType,
          live?.context?.session?.sessionType,
          live?.context?.session?.sessionName,
          live?.context?.session?.eventType
        ].map((value) => String(value || '').trim().toLowerCase())
          .find((value) => value.length > 0) || '';
        if (selected.includes('test')) return 'test';
        if (selected.includes('practice')) return 'practice';
        if (selected.includes('qual')) return 'qualifying';
        if (selected.includes('race')) return 'race';
        return null;
      },
      sessionKind(live) {
        return browserModel.currentSessionKind(live);
      },
      requiresValidLapBeforeRendering(live) {
        return ['test', 'practice', 'qualifying'].includes(browserModel.currentSessionKind(live));
      },
      isRaceSession(live) {
        const kind = browserModel.currentSessionKind(live);
        return kind == null || kind === 'race';
      },
      referenceCarIdx(live, options = {}) {
        const reference = live?.models?.reference || {};
        const scoring = browserModel.scoring(live);
        const timing = browserModel.timing(live);
        const directory = browserModel.driverDirectory(live);
        const relative = browserModel.relative(live);
        const spatial = browserModel.spatial(live);
        const latest = live?.latestSample || {};
        const candidates = [];
        candidates.push(reference.focusCarIdx);
        if (options.preferRelative) candidates.push(relative.referenceCarIdx);
        if (options.preferSpatial) candidates.push(spatial.referenceCarIdx);
        candidates.push(
          scoring.referenceCarIdx,
          timing.focusRow?.carIdx,
          timing.focusCarIdx,
          directory.focusCarIdx);
        if (!options.preferRelative) candidates.push(relative.referenceCarIdx);
        if (!options.preferSpatial) candidates.push(spatial.referenceCarIdx);
        if (options.includeLatestSample !== false) {
          candidates.push(latest.focusCarIdx);
        }
        return candidates.find((value) => Number.isFinite(value)) ?? null;
      },
      rowsByCarIdx(rows) {
        return new Map((Array.isArray(rows) ? rows : [])
          .filter((row) => row && Number.isFinite(row.carIdx))
          .map((row) => [row.carIdx, row]));
      },
      scoringByCarIdx(live) {
        return browserModel.rowsByCarIdx(browserModel.scoring(live).rows || []);
      },
      driverByCarIdx(live) {
        return browserModel.rowsByCarIdx(browserModel.driverDirectory(live).drivers || []);
      },
      timingRows(live) {
        const timing = browserModel.timing(live);
        return [
          ...(timing.overallRows || []),
          ...(timing.classRows || []),
          timing.focusRow,
          timing.playerRow
        ].filter((row) => row && Number.isFinite(row.carIdx));
      },
      timingByCarIdx(live) {
        return browserModel.rowsByCarIdx(browserModel.timingRows(live));
      },
      isFiniteNumber(value) {
        return Number.isFinite(value);
      },
      positive(value) {
        return Number.isFinite(value) && value > 0;
      },
      isUsableLapTime(seconds) {
        return Number.isFinite(seconds) && seconds > 20 && seconds < 1800;
      },
      hasValidLap(row) {
        return browserModel.isUsableLapTime(row?.bestLapTimeSeconds)
          || browserModel.isUsableLapTime(row?.lastLapTimeSeconds);
      },
      hasDriverIdentity(row, referenceCarIdx) {
        return row?.isPlayer
          || row?.isFocus
          || row?.carIdx === referenceCarIdx
          || Boolean(row?.driverName)
          || Boolean(row?.teamName)
          || Boolean(row?.carNumber);
      },
      firstText(...values) {
        return values.find((value) => typeof value === 'string' && value.trim().length > 0) || null;
      },
      positionLabel(row, fallbackRow = null) {
        const classPosition = row?.classPosition ?? fallbackRow?.classPosition;
        if (Number.isFinite(classPosition) && classPosition > 0) return `${classPosition}`;
        const overallPosition = row?.overallPosition ?? fallbackRow?.overallPosition;
        return Number.isFinite(overallPosition) && overallPosition > 0 ? `${overallPosition}` : null;
      },
      samplePositionLabel(sample) {
        const position = sample?.focusClassPosition
          ?? sample?.focusPosition;
        return Number.isFinite(position) && position > 0 ? `${position}` : null;
      },
      hasRelativePlacement(row) {
        return Number.isFinite(row?.relativeMeters)
          || Number.isFinite(row?.relativeSeconds)
          || Number.isFinite(row?.relativeLaps);
      },
      relativeSortKey(row) {
        if (Number.isFinite(row?.relativeSeconds)) return Math.abs(row.relativeSeconds);
        if (Number.isFinite(row?.relativeMeters)) return Math.abs(row.relativeMeters);
        if (Number.isFinite(row?.relativeLaps)) return Math.abs(row.relativeLaps);
        return Number.MAX_VALUE;
      },
      relativeGap(row, direction) {
        const sign = direction === 'ahead' ? '-' : '+';
        if (Number.isFinite(row?.relativeSeconds)) return `${sign}${Math.abs(row.relativeSeconds).toFixed(3)}`;
        if (Number.isFinite(row?.relativeMeters)) return `${sign}${Math.abs(row.relativeMeters).toFixed(0)}m`;
        return '--';
      },
      hasUsableGap(row) {
        return row?.gapEvidence?.isUsable === true;
      },
      normalizeProgress(value) {
        if (!Number.isFinite(value)) return 0;
        const normalized = value % 1;
        return normalized < 0 ? normalized + 1 : normalized;
      },
      isPlayerInCar(live) {
        const race = browserModel.raceEvents(live);
        const reference = browserModel.model(live, 'reference');
        const fuelPit = browserModel.fuelPit(live);
        if (reference.focusIsPlayer === false) return false;
        if (Number.isFinite(reference.focusCarIdx)
          && Number.isFinite(reference.playerCarIdx)
          && reference.focusCarIdx !== reference.playerCarIdx) {
          return false;
        }
        if (race.isInGarage === true
          || race.isGarageVisible === true
          || reference.isInGarage === true
          || localPitContext(race, reference, fuelPit)) {
          return false;
        }
        const hasRaceContext = race.hasData === true || reference.hasData === true;
        if (!hasRaceContext) return true;
        return race.isOnTrack === true || reference.isOnTrack === true;
      },
      isLiveTelemetryAvailable(live) {
        return telemetryAvailability(live).isAvailable;
      },
      selectRowsAroundReference(rows, referenceCarIdx, limit, carIdxForRow) {
        const sourceRows = Array.isArray(rows) ? rows : [];
        const getCarIdx = typeof carIdxForRow === 'function' ? carIdxForRow : (row) => row?.carIdx;
        const boundedLimit = Math.max(0, limit);
        if (boundedLimit <= 0 || sourceRows.length <= boundedLimit) {
          return sourceRows.slice(0, boundedLimit);
        }

        if (referenceCarIdx == null) {
          return sourceRows.slice(0, boundedLimit);
        }

        const referenceIndex = sourceRows.findIndex((row) => getCarIdx(row) === referenceCarIdx);
        if (referenceIndex < 0) {
          return sourceRows.slice(0, boundedLimit);
        }

        const ahead = Math.floor(boundedLimit / 2);
        const start = Math.max(0, Math.min(referenceIndex - ahead, Math.max(0, sourceRows.length - boundedLimit)));
        return sourceRows.slice(start, start + boundedLimit);
      }
    };
    window.TmrBrowserModel = browserModel;
    const telemetryAvailability = (live) => {
      if (!live?.isConnected) {
        return { isAvailable: false, isFresh: false, reason: 'disconnected', status: 'iRacing disconnected' };
      }
      if (!live?.isCollecting) {
        return { isAvailable: false, isFresh: false, reason: 'waiting-for-telemetry', status: 'waiting for telemetry' };
      }
      if (!live?.lastUpdatedAtUtc) {
        return { isAvailable: false, isFresh: false, reason: 'stale-telemetry', status: 'waiting for fresh telemetry' };
      }
      const lastUpdated = Date.parse(live.lastUpdatedAtUtc);
      const ageMilliseconds = Date.now() - lastUpdated;
      if (!Number.isFinite(lastUpdated) || Math.abs(ageMilliseconds) > 1500) {
        return { isAvailable: false, isFresh: false, reason: 'stale-telemetry', status: 'waiting for fresh telemetry' };
      }
      return { isAvailable: true, isFresh: true, reason: 'available', status: 'live' };
    };
    const quality = (model) => model?.quality ?? 'unavailable';

    function updateTelemetryFade(live) {
      if (!page.fadeWhenTelemetryUnavailable || !overlayEl) return;
      overlayEl.style.opacity = telemetryAvailability(live).isAvailable ? '1' : '0';
    }

    function setStatus(live, detail) {
      const availability = telemetryAvailability(live);
      clearFooterSource();
      if (!availability.isAvailable) {
        statusEl.textContent = availability.status;
        if (timeRemainingEl) {
          timeRemainingEl.hidden = true;
          timeRemainingEl.textContent = '';
        }
        return;
      }
      const sequence = live.sequence ?? 0;
      const changed = sequence !== lastSequence;
      lastSequence = sequence;
      statusEl.textContent = detail || `${changed ? 'live' : 'steady'} | seq ${sequence}`;
      if (timeRemainingEl) {
        timeRemainingEl.hidden = true;
        timeRemainingEl.textContent = '';
      }
    }

    function localPitContext(race, reference, fuelPit) {
      return race.onPitRoad === true
        || reference.onPitRoad === true
        || reference.playerOnPitRoad === true
        || reference.playerCarInPitStall === true
        || isPitRoadTrackSurface(reference.trackSurface)
        || isPitRoadTrackSurface(reference.playerTrackSurface)
        || fuelPit.onPitRoad === true
        || fuelPit.pitstopActive === true
        || fuelPit.playerCarInPitStall === true
        || fuelPit.teamOnPitRoad === true;
    }

    function isPitRoadTrackSurface(trackSurface) {
      return trackSurface === 1 || trackSurface === 2;
    }

    function rowsTable(headers, rows) {
      if (!rows.length) {
        return '<div class="empty">Waiting for live rows.</div>';
      }
      const fixedWidth = headers.reduce((total, header) => total + columnWidth(header), 0);
      const tableStyle = fixedWidth > 0
        ? ` style="width:${fixedWidth}px; min-width:${fixedWidth}px; table-layout:fixed;"`
        : '';
      const colGroup = fixedWidth > 0
        ? `<colgroup>${headers.map((header) => `<col style="width:${columnWidth(header)}px;">`).join('')}</colgroup>`
        : '';
      const headerHtml = headers.map((header) => `<th${cellStyle(header)}>${escapeHtml(header.label)}</th>`).join('');
      const rowHtml = rows.map((row) => {
        const classes = [
          row.isFocus || row.isReference || row.isReferenceCar ? 'focus' : '',
          (row.isPit || row.onPitRoad) && !(row.isFocus || row.isReference || row.isReferenceCar) ? 'pit' : '',
          row.isPendingGrid ? 'pending-grid' : '',
          row.isPartial ? 'partial' : '',
          row.isPlaceholder ? 'placeholder' : '',
          lapRelationshipClass(row),
          row.carClassColorHex && !isClassHeaderRow(row) ? 'class-colored' : '',
          isClassHeaderRow(row) ? 'class-header' : ''
        ].filter(Boolean).join(' ');
        if (isClassHeaderRow(row)) {
          return `<tr class="${classes}"${classHeaderStyle(row)}><td colspan="${Math.max(1, headers.length)}">${classHeaderContent(row)}</td></tr>`;
        }

        const cells = headers.map((header) => `<td${cellStyle(header)}>${header.value(row)}</td>`).join('');
        return `<tr class="${classes}"${rowStyle(row)}>${cells}</tr>`;
      }).join('');
      return `<table${tableStyle}>${colGroup}<thead><tr>${headerHtml}</tr></thead><tbody>${rowHtml}</tbody></table>`;
    }

    function isClassHeaderRow(row) {
      return row?.rowKind === 'header' || row?.isClassHeader === true;
    }

    function lapRelationshipClass(row) {
      const delta = Number(row?.relativeLapDelta ?? row?.lapDeltaToReference);
      if (!Number.isFinite(delta) || delta === 0) return '';
      if (delta >= 2) return 'lap-ahead-2';
      if (delta === 1) return 'lap-ahead-1';
      if (delta === -1) return 'lap-behind-1';
      if (delta <= -2) return 'lap-behind-2';
      return '';
    }

    function classHeaderContent(row) {
      const title = escapeHtml(row?.headerTitle || row?.driverName || row?.className || row?.label || 'Class');
      const detail = row?.headerDetail
        ? escapeHtml(row.headerDetail)
        : [
          row?.rowCount ? `${row.rowCount} cars` : '',
          row?.estimatedLapsLabel || ''
        ].filter(Boolean).map(escapeHtml).join(' | ');
      return `
        <div class="class-header-band">
          <span class="class-header-title">${title}</span>
          <span class="class-header-detail">${detail}</span>
        </div>`;
    }

    function columnWidth(header) {
      const width = Number(header?.width);
      return Number.isFinite(width) && width > 0 ? Math.round(width) : 0;
    }

    function cellStyle(header) {
      const styles = [];
      const width = columnWidth(header);
      if (width > 0) styles.push(`width:${width}px`);
      const align = ['left', 'right', 'center'].includes(header?.align) ? header.align : null;
      if (align) styles.push(`text-align:${align}`);
      if (header?.dataKey === 'driver') styles.push('padding-left:14px');
      return styles.length ? ` style="${styles.join(';')}"` : '';
    }

    function classHeaderStyle(row) {
      if (!isClassHeaderRow(row)) return '';
      return classColorStyle(row?.carClassColorHex);
    }

    function rowStyle(row) {
      if (isClassHeaderRow(row)) return classHeaderStyle(row);
      const color = parseHexColor(row?.carClassColorHex);
      return color
        ? ` style="--row-class-accent: #${color.key}; --row-class-bg: rgba(${color.r}, ${color.g}, ${color.b}, 0.13);"`
        : '';
    }

    function classColorStyle(value) {
      const color = parseHexColor(value);
      if (!color) return '';
      if (classHeaderStyles.has(color.key)) return classHeaderStyles.get(color.key);
      const style = ` style="--class-header-bg: rgba(${color.r}, ${color.g}, ${color.b}, 0.28); --class-header-fg: var(--tmr-text);"`;
      classHeaderStyles.set(color.key, style);
      return style;
    }

    function classColorCss(value, fallback = 'var(--tmr-text-secondary)') {
      const color = parseHexColor(value);
      return color ? `#${color.key}` : fallback;
    }

    function parseHexColor(value) {
      const match = /^(?:#|0x)?([0-9a-f]{6})$/i.exec(String(value || '').trim());
      if (!match) return null;
      const key = match[1].toUpperCase();
      if (parsedHexColors.has(key)) return parsedHexColors.get(key);
      const rgb = Number.parseInt(key, 16);
      const color = {
        key,
        r: (rgb >> 16) & 255,
        g: (rgb >> 8) & 255,
        b: rgb & 255
      };
      parsedHexColors.set(key, color);
      return color;
    }

    function metric(label, value) {
      return `<div class="metric"><div class="label">${escapeHtml(label)}</div><div class="value">${escapeHtml(value)}</div></div>`;
    }

    function toneClass(tone) {
      const normalized = String(tone || '').trim().toLowerCase();
      return ['live', 'modeled', 'normal', 'waiting', 'info', 'success', 'warning', 'error'].includes(normalized)
        ? normalized
        : 'normal';
    }

    function metricRow(row) {
      const tone = toneClass(row?.tone);
      const highlight = tone === 'info' ? ' highlight' : '';
      return `
        <div class="metric ${tone}${highlight}">
          <div class="label">${escapeHtml(row?.label || '')}</div>
          <div class="value">${escapeHtml(row?.value || '--')}</div>
        </div>`;
    }

    async function fetchOverlayModel(overlayId) {
      const response = await fetch(apiPath(`/api/overlay-model/${encodeURIComponent(overlayId)}`), { cache: 'no-store' });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const payload = await response.json();
      return payload.model || null;
    }

    function renderOverlayModel(model) {
      if (!model) {
        contentEl.innerHTML = '<div class="empty">Waiting for overlay model.</div>';
        renderHeaderItems(null, 'waiting for model');
        clearFooterSource();
        return;
      }

      const metrics = Array.isArray(model.metrics) ? model.metrics : [];
      const rows = Array.isArray(model.rows) ? model.rows : [];
      if (model.bodyKind === 'summary-table') {
        const summary = metrics.length
          ? `<div class="grid" style="margin-bottom: 10px;">${metrics.map(metricRow).join('')}</div>`
          : '';
        contentEl.innerHTML = summary + rowsTable(displayModelHeaders(model), rows);
      } else if (model.bodyKind === 'graph') {
        contentEl.innerHTML = '<div class="model-graph-panel"><canvas class="model-graph" aria-label="Gap trend graph"></canvas></div>';
        drawOverlayGraph(contentEl.querySelector('.model-graph'), Array.isArray(model.points) ? model.points : []);
      } else if (model.bodyKind === 'metrics') {
        contentEl.innerHTML = metrics.length
          ? `<div class="grid">${metrics.map(metricRow).join('')}</div>`
          : '<div class="empty">Waiting for live values.</div>';
      } else {
        contentEl.innerHTML = rowsTable(displayModelHeaders(model), rows);
      }

      renderHeaderItems(model, model.status || 'live');
      renderFooterSource(model);
    }

    function renderHeaderItems(model, fallbackStatus) {
      const items = Array.isArray(model?.headerItems) ? model.headerItems : [];
      const statusItem = items.find((item) => String(item?.key || '').toLowerCase() === 'status');
      const timeItem = items.find((item) => String(item?.key || '').toLowerCase() === 'timeremaining');
      statusEl.textContent = statusItem?.value || fallbackStatus || '';
      if (timeRemainingEl) {
        const value = timeItem?.value || '';
        timeRemainingEl.textContent = value;
        timeRemainingEl.hidden = !value;
      }
    }

    function renderFooterSource(model) {
      if (!sourceEl) return;
      const value = String(model?.source || '').trim();
      sourceEl.textContent = value;
      sourceEl.hidden = !value;
    }

    function clearFooterSource() {
      if (!sourceEl) return;
      sourceEl.hidden = true;
      sourceEl.textContent = '';
    }

    function drawOverlayGraph(canvas, points) {
      if (!canvas) return;
      const rect = canvas.getBoundingClientRect();
      const width = Math.max(1, rect.width);
      const height = Math.max(1, rect.height);
      const dpr = window.devicePixelRatio || 1;
      canvas.width = Math.round(width * dpr);
      canvas.height = Math.round(height * dpr);
      const ctx = canvas.getContext('2d');
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.clearRect(0, 0, width, height);

      const values = points.map(Number).filter(Number.isFinite);
      if (values.length < 2) {
        ctx.fillStyle = themeColor('--tmr-text-muted', '#8caed4');
        ctx.font = '700 13px "Segoe UI", Arial, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText('waiting for trend', width / 2, height / 2);
        return;
      }

      const axisWidth = 58;
      const xAxisHeight = 17;
      const plot = {
        left: axisWidth,
        top: 0,
        width: Math.max(40, width - axisWidth - 4),
        height: Math.max(40, height - xAxisHeight)
      };
      const max = Math.max(1, ...values.map((value) => Math.max(0, value)));
      ctx.strokeStyle = themeRgba('--tmr-text-muted-rgb', 0.28, 'rgba(140, 174, 212, 0.28)');
      ctx.lineWidth = 1;
      for (let index = 1; index < 4; index += 1) {
        const y = plot.top + index * plot.height / 4;
        ctx.beginPath();
        ctx.moveTo(plot.left, y);
        ctx.lineTo(plot.left + plot.width, y);
        ctx.stroke();
      }
      ctx.strokeStyle = themeRgba('--tmr-text-muted-rgb', 0.24, 'rgba(140, 174, 212, 0.24)');
      ctx.beginPath();
      ctx.moveTo(plot.left, plot.top + plot.height);
      ctx.lineTo(plot.left + plot.width, plot.top + plot.height);
      ctx.stroke();
      ctx.strokeStyle = themeRgba('--tmr-text-muted-rgb', 0.34, 'rgba(140, 174, 212, 0.34)');
      ctx.beginPath();
      ctx.moveTo(plot.left, plot.top);
      ctx.lineTo(plot.left + plot.width, plot.top);
      ctx.stroke();

      ctx.fillStyle = themeColor('--tmr-text-muted', '#8caed4');
      ctx.font = '10px "Segoe UI", Arial, sans-serif';
      ctx.textBaseline = 'middle';
      ctx.textAlign = 'right';
      ctx.fillText('leader', axisWidth - 8, plot.top + 7);
      ctx.fillText(formatSignedGap(max), axisWidth - 8, plot.top + plot.height - 7);
      ctx.textAlign = 'left';
      ctx.fillText('10m', plot.left, plot.top + plot.height + 12);
      ctx.textAlign = 'right';
      ctx.fillText('now', plot.left + plot.width, plot.top + plot.height + 12);

      ctx.strokeStyle = themeColor('--tmr-cyan', '#00e8ff');
      ctx.lineWidth = 2;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.beginPath();
      values.forEach((value, index) => {
        const progress = index / Math.max(1, values.length - 1);
        const normalized = Math.max(0, value) / max;
        const x = plot.left + progress * plot.width;
        const y = plot.top + normalized * plot.height;
        if (index === 0) {
          ctx.moveTo(x, y);
        } else {
          ctx.lineTo(x, y);
        }
      });
      ctx.stroke();
    }

    function formatSignedGap(value) {
      if (!Number.isFinite(value)) return '--';
      if (Math.abs(value) >= 60) {
        const minutes = Math.floor(Math.abs(value) / 60);
        const seconds = Math.abs(value) - minutes * 60;
        return `+${minutes}:${seconds.toFixed(1).padStart(4, '0')}`;
      }
      return `+${value.toFixed(1)}s`;
    }

    function displayModelHeaders(model) {
      return (Array.isArray(model?.columns) ? model.columns : []).map((column, index) => ({
        label: column.label,
        dataKey: column.dataKey,
        width: column.width,
        align: column.alignment,
        value: (row) => escapeHtml((row?.cells || [])[index] || '')
      }));
    }

    function bar(label, value, color) {
      const percent = Number.isFinite(value) ? Math.max(0, Math.min(1, value)) * 100 : 0;
      return `
        <div class="bar-row">
          <div class="muted">${escapeHtml(label)}</div>
          <div class="bar"><span style="width:${percent.toFixed(0)}%; background:${color};"></span></div>
          <div>${formatPercent(Number.isFinite(value) ? value : null)}</div>
        </div>`;
    }

    {{MODULE_SCRIPT}}

    function render(live) {
      const module = browserOverlay.module;
      updateTelemetryFade(live);
      if (!module?.render) {
        contentEl.innerHTML = '<div class="empty">Unknown overlay route.</div>';
        statusEl.textContent = 'not configured';
        clearFooterSource();
        if (timeRemainingEl) {
          timeRemainingEl.hidden = true;
          timeRemainingEl.textContent = '';
        }
        return;
      }

      if (page.requiresTelemetry && !telemetryAvailability(live).isAvailable && !page.renderWhenTelemetryUnavailable) {
        contentEl.innerHTML = '<div class="empty">Waiting for iRacing telemetry.</div>';
        setStatus(live);
        return;
      }

      module.render(live);
    }

    async function refresh() {
      const module = browserOverlay.module;
      try {
        if (module?.beforeRefresh) {
          await module.beforeRefresh();
        }

        if (!page.requiresTelemetry) {
          render(null);
          return;
        }

        const response = await fetch(apiPath('/api/snapshot'), { cache: 'no-store' });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const payload = await response.json();
        render(payload.live);
      } catch (error) {
        updateTelemetryFade(null);
        if (module?.renderOffline) {
          module.renderOffline(error);
          return;
        }

        statusEl.textContent = 'localhost offline';
        clearFooterSource();
        if (timeRemainingEl) {
          timeRemainingEl.hidden = true;
          timeRemainingEl.textContent = '';
        }
        contentEl.innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`;
      }
    }

    const module = browserOverlay.module;
    if (module?.start) {
      module.start({ refresh });
    } else {
      refresh();
      setInterval(refresh, page.refreshIntervalMilliseconds || 250);
    }
