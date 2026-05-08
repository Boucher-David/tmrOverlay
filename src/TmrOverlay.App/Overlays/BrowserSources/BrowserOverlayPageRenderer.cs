using System.Text.Json;

namespace TmrOverlay.App.Overlays.BrowserSources;

internal static class BrowserOverlayPageRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<string> Routes => BrowserOverlayCatalog.Routes;

    public static bool TryGetRouteForOverlayId(string overlayId, out string route)
    {
        return BrowserOverlayCatalog.TryGetRouteForOverlayId(overlayId, out route);
    }

    public static bool TryRender(string path, out string html)
    {
        if (!BrowserOverlayCatalog.TryGetPageByRoute(path, out var page))
        {
            html = string.Empty;
            return false;
        }

        html = Render(page);
        return true;
    }

    public static string RenderIndex(int port)
    {
        var links = string.Join(
            Environment.NewLine,
            Routes.Select(route => $"""<a href="{route}">{TitleFromRoute(route)}</a>"""));
        return IndexTemplate
            .Replace("{{PORT}}", port.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{LINKS}}", links, StringComparison.Ordinal);
    }

    private static string Render(BrowserOverlayPage page)
    {
        var pageJson = JsonSerializer.Serialize(new BrowserOverlayClientPage(
            page.Id,
            page.Title,
            page.RequiresTelemetry,
            page.RenderWhenTelemetryUnavailable,
            page.FadeWhenTelemetryUnavailable,
            page.RefreshIntervalMilliseconds), JsonOptions);
        return OverlayTemplate
            .Replace("{{TITLE}}", page.Title, StringComparison.Ordinal)
            .Replace("{{BODY_CLASS}}", page.BodyClass, StringComparison.Ordinal)
            .Replace("{{PAGE_JSON}}", pageJson, StringComparison.Ordinal)
            .Replace("{{MODULE_SCRIPT}}", page.Script, StringComparison.Ordinal);
    }

    private static string TitleFromRoute(string route)
    {
        return BrowserOverlayCatalog.TryGetPageByRoute(route, out var page) ? page.Title : route;
    }

    private sealed record BrowserOverlayClientPage(
        string Id,
        string Title,
        bool RequiresTelemetry,
        bool RenderWhenTelemetryUnavailable,
        bool FadeWhenTelemetryUnavailable,
        int RefreshIntervalMilliseconds);

    private const string IndexTemplate = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>TMR Localhost Overlays</title>
  <style>
    :root {
      color-scheme: dark;
      font-family: "Segoe UI", Arial, sans-serif;
      background: #0e1215;
      color: #e7edf2;
    }

    body {
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      background: #0e1215;
    }

    main {
      width: min(720px, calc(100vw - 48px));
      border: 1px solid rgba(255, 255, 255, 0.16);
      border-radius: 8px;
      background: rgba(20, 27, 32, 0.96);
      padding: 24px;
      box-shadow: 0 18px 48px rgba(0, 0, 0, 0.34);
    }

    h1 {
      margin: 0 0 6px;
      font-size: 22px;
      font-weight: 700;
    }

    p {
      margin: 0 0 20px;
      color: #9eacb6;
      font-size: 13px;
      line-height: 1.4;
    }

    nav {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 10px;
    }

    a {
      display: block;
      color: #e7edf2;
      text-decoration: none;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 6px;
      padding: 11px 12px;
      background: rgba(255, 255, 255, 0.05);
    }

    a:hover {
      border-color: #62c7ff;
      background: rgba(98, 199, 255, 0.10);
    }
  </style>
</head>
<body>
  <main>
    <h1>TMR Localhost Overlays</h1>
    <p>Use these local browser-source routes in OBS. This service listens only on localhost port {{PORT}}.</p>
    <nav>
      {{LINKS}}
    </nav>
  </main>
</body>
</html>
""";

    private const string OverlayTemplate = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>TMR {{TITLE}}</title>
  <style>
    :root {
      color-scheme: dark;
      font-family: "Segoe UI", Arial, sans-serif;
      background: transparent;
      color: #edf4f8;
    }

    * {
      box-sizing: border-box;
    }

    html,
    body {
      margin: 0;
      width: 100%;
      min-height: 100%;
      overflow: hidden;
      background: transparent;
    }

    body {
      padding: 8px;
    }

    body.track-map-page,
    body.garage-cover-page {
      padding: 0;
    }

    .overlay {
      width: fit-content;
      min-width: 360px;
      max-width: calc(100vw - 16px);
      border: 1px solid rgba(255, 255, 255, 0.22);
      border-radius: 8px;
      background: rgba(10, 14, 17, 0.84);
      box-shadow: 0 10px 36px rgba(0, 0, 0, 0.34);
      overflow: hidden;
      opacity: 1;
      transition: opacity 220ms ease;
    }

    body.track-map-page .overlay,
    body.garage-cover-page .overlay {
      width: 100vw;
      height: 100vh;
      min-width: 0;
      max-width: none;
      border: 0;
      border-radius: 0;
      box-shadow: none;
    }

    body.track-map-page .overlay {
      background: transparent;
    }

    body.garage-cover-page .overlay {
      background: #000000;
    }

    .header {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 18px;
      padding: 10px 12px 8px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.10);
      background: rgba(255, 255, 255, 0.04);
    }

    body.track-map-page .header,
    body.garage-cover-page .header {
      display: none;
    }

    .title {
      font-size: 15px;
      font-weight: 700;
      letter-spacing: 0;
      white-space: nowrap;
    }

    .status {
      color: #98a8b3;
      font-size: 11px;
      text-align: right;
      white-space: nowrap;
    }

    .content {
      padding: 10px 12px 12px;
    }

    body.track-map-page .content,
    body.garage-cover-page .content {
      width: 100%;
      height: 100%;
      display: grid;
      place-items: center;
      padding: 0;
    }

    table {
      border-collapse: collapse;
      width: 100%;
      min-width: 500px;
    }

    th,
    td {
      padding: 5px 7px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      text-align: left;
      white-space: nowrap;
      font-size: 12px;
    }

    th {
      color: #8fa1ad;
      font-size: 10px;
      font-weight: 700;
      text-transform: uppercase;
    }

    tr.focus td {
      color: #ffffff;
      background: rgba(98, 199, 255, 0.14);
    }

    tr.pit td {
      color: #8fa1ad;
      background: rgba(255, 255, 255, 0.045);
    }

    tr.class-header td {
      color: var(--class-header-fg, #e7edf2);
      background: var(--class-header-bg, rgba(255, 255, 255, 0.07));
      font-size: 10px;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0;
    }

    .muted {
      color: #8fa1ad;
    }

    .pill {
      display: inline-flex;
      min-width: 28px;
      justify-content: center;
      padding: 2px 6px;
      border-radius: 4px;
      background: rgba(255, 255, 255, 0.10);
      color: #dbe6ec;
      font-size: 10px;
      font-weight: 700;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(120px, 1fr));
      gap: 10px;
      min-width: 420px;
    }

    .metric {
      padding: 9px 10px;
      border: 1px solid rgba(255, 255, 255, 0.10);
      border-radius: 6px;
      background: rgba(255, 255, 255, 0.045);
    }

    .metric .label {
      color: #91a3af;
      font-size: 10px;
      font-weight: 700;
      text-transform: uppercase;
    }

    .metric .value {
      margin-top: 3px;
      font-size: 18px;
      font-weight: 700;
      color: #f4f9fb;
    }

    .bars {
      display: grid;
      gap: 8px;
      min-width: 420px;
    }

    .bar-row {
      display: grid;
      grid-template-columns: 72px 1fr 56px;
      align-items: center;
      gap: 10px;
      font-size: 12px;
    }

    .bar {
      height: 9px;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.10);
      overflow: hidden;
    }

    .bar span {
      display: block;
      height: 100%;
      width: 0;
      border-radius: inherit;
      background: #62c7ff;
    }

    .track {
      display: grid;
      place-items: center;
      width: 360px;
      height: 300px;
    }

    body.track-map-page .track {
      width: 100%;
      height: 100%;
      padding: 18px;
    }

    svg {
      width: 320px;
      height: 240px;
      overflow: visible;
    }

    body.track-map-page svg {
      width: 100%;
      height: 100%;
    }

    .empty {
      min-width: 340px;
      color: #98a8b3;
      font-size: 13px;
      line-height: 1.4;
      padding: 10px 2px;
    }

    .chat {
      display: grid;
      gap: 8px;
      min-width: 360px;
      max-width: 420px;
      max-height: min(620px, calc(100vh - 74px));
      overflow: hidden;
    }

    .chat-line {
      display: grid;
      gap: 2px;
      padding: 8px 9px;
      border-radius: 6px;
      background: rgba(255, 255, 255, 0.055);
    }

    .chat-name {
      color: #62c7ff;
      font-size: 11px;
      font-weight: 700;
    }

    .chat-text {
      color: #edf4f8;
      font-size: 13px;
      line-height: 1.25;
      overflow-wrap: anywhere;
    }

    .chat-line.system .chat-name {
      color: #ffc44d;
    }

    .chat-line.error .chat-name {
      color: #ff7168;
    }

    .chat-frame {
      display: block;
      width: min(420px, calc(100vw - 40px));
      height: min(620px, calc(100vh - 74px));
      min-height: 320px;
      border: 0;
      background: transparent;
    }

    .garage-cover {
      width: 100vw;
      height: 100vh;
      display: grid;
      place-items: center;
      background: #000000;
      overflow: hidden;
    }

    .garage-cover img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }

    .garage-cover-fallback div {
      color: #edf4f8;
      font-size: 96px;
      font-weight: 800;
      letter-spacing: 0;
    }
  </style>
</head>
<body class="{{BODY_CLASS}}">
  <section class="overlay">
    <div class="header">
      <div class="title">{{TITLE}}</div>
      <div id="status" class="status">waiting</div>
    </div>
    <div id="content" class="content">
      <div class="empty">Waiting for live telemetry.</div>
    </div>
  </section>

  <script>
    const page = {{PAGE_JSON}};
    const overlayEl = document.querySelector('.overlay');
    const statusEl = document.getElementById('status');
    const contentEl = document.getElementById('content');
    let lastSequence = null;
    const browserOverlay = {
      module: null,
      register(module) {
        this.module = module;
      }
    };
    window.TmrBrowserOverlay = browserOverlay;

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
    const parsedHexColors = new Map();
    const classHeaderStyles = new Map();
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
      if (!availability.isAvailable) {
        statusEl.textContent = availability.status;
        return;
      }
      const sequence = live.sequence ?? 0;
      const changed = sequence !== lastSequence;
      lastSequence = sequence;
      statusEl.textContent = detail || `${changed ? 'live' : 'steady'} | seq ${sequence}`;
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
        const cells = headers.map((header) => `<td${cellStyle(header)}>${header.value(row)}</td>`).join('');
        const classes = [
          row.isFocus || row.isReferenceCar ? 'focus' : '',
          row.onPitRoad && !(row.isFocus || row.isReferenceCar) ? 'pit' : '',
          row.rowKind === 'header' || row.isClassHeader ? 'class-header' : ''
        ].filter(Boolean).join(' ');
        return `<tr class="${classes}"${classHeaderStyle(row)}>${cells}</tr>`;
      }).join('');
      return `<table${tableStyle}>${colGroup}<thead><tr>${headerHtml}</tr></thead><tbody>${rowHtml}</tbody></table>`;
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
      return styles.length ? ` style="${styles.join(';')}"` : '';
    }

    function classHeaderStyle(row) {
      if (row?.rowKind !== 'header' && !row?.isClassHeader) return '';
      return classColorStyle(row?.carClassColorHex);
    }

    function classColorStyle(value) {
      const color = parseHexColor(value);
      if (!color) return '';
      if (classHeaderStyles.has(color.key)) return classHeaderStyles.get(color.key);
      const style = ` style="--class-header-bg: rgba(${color.r}, ${color.g}, ${color.b}, 0.28); --class-header-fg: #ffffff;"`;
      classHeaderStyles.set(color.key, style);
      return style;
    }

    function classColorCss(value, fallback = '#ecf4f8') {
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

        const response = await fetch('/api/snapshot', { cache: 'no-store' });
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
  </script>
</body>
</html>
""";
}
