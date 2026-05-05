using System.Text.Json;

namespace TmrOverlay.App.Localhost;

internal static class LocalhostOverlayPageRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] CanonicalRoutes =
    [
        "/overlays/standings",
        "/overlays/relative",
        "/overlays/fuel-calculator",
        "/overlays/session-weather",
        "/overlays/pit-service",
        "/overlays/input-state",
        "/overlays/car-radar",
        "/overlays/gap-to-leader",
        "/overlays/track-map",
        "/overlays/stream-chat"
    ];

    private static readonly IReadOnlyDictionary<string, OverlayPage> Pages =
        new Dictionary<string, OverlayPage>(StringComparer.OrdinalIgnoreCase)
        {
            ["/overlays/standings"] = new("standings", "Standings", "standings"),
            ["/overlays/relative"] = new("relative", "Relative", "relative"),
            ["/overlays/calculator"] = new("fuel-calculator", "Fuel Calculator", "fuel"),
            ["/overlays/fuel-calculator"] = new("fuel-calculator", "Fuel Calculator", "fuel"),
            ["/overlays/session-weather"] = new("session-weather", "Session / Weather", "weather"),
            ["/overlays/pit-service"] = new("pit-service", "Pit Service", "pitService"),
            ["/overlays/inputs"] = new("input-state", "Inputs", "inputs"),
            ["/overlays/input-state"] = new("input-state", "Inputs", "inputs"),
            ["/overlays/car-radar"] = new("car-radar", "Car Radar", "radar"),
            ["/overlays/gap-to-leader"] = new("gap-to-leader", "Gap To Leader", "gap"),
            ["/overlays/track-map"] = new("track-map", "Track Map", "trackMap"),
            ["/overlays/stream-chat"] = new("stream-chat", "Stream Chat", "streamChat")
        };

    public static IReadOnlyList<string> Routes { get; } = CanonicalRoutes;

    public static bool TryGetRouteForOverlayId(string overlayId, out string route)
    {
        var page = Pages
            .Where(item => CanonicalRoutes.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
            .Select(item => new { Route = item.Key, Page = item.Value })
            .FirstOrDefault(item => string.Equals(item.Page.Id, overlayId, StringComparison.OrdinalIgnoreCase));
        route = page?.Route ?? string.Empty;
        return page is not null;
    }

    public static bool TryRender(string path, out string html)
    {
        var normalized = NormalizePath(path);
        if (!Pages.TryGetValue(normalized, out var page))
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

    private static string Render(OverlayPage page)
    {
        var pageJson = JsonSerializer.Serialize(page, JsonOptions);
        return OverlayTemplate
            .Replace("{{TITLE}}", page.Title, StringComparison.Ordinal)
            .Replace("{{PAGE_JSON}}", pageJson, StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        var normalized = string.IsNullOrWhiteSpace(path)
            ? "/"
            : path.TrimEnd('/').ToLowerInvariant();
        return normalized.Length == 0 ? "/" : normalized;
    }

    private static string TitleFromRoute(string route)
    {
        return Pages.TryGetValue(route, out var page) ? page.Title : route;
    }

    private sealed record OverlayPage(string Id, string Title, string Renderer);

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

    body.track-map-page {
      padding: 0;
    }

    .overlay {
      width: fit-content;
      min-width: 360px;
      max-width: min(760px, calc(100vw - 16px));
      border: 1px solid rgba(255, 255, 255, 0.22);
      border-radius: 8px;
      background: rgba(10, 14, 17, 0.84);
      box-shadow: 0 10px 36px rgba(0, 0, 0, 0.34);
      overflow: hidden;
    }

    body.track-map-page .overlay {
      width: 100vw;
      height: 100vh;
      min-width: 0;
      max-width: none;
      border: 0;
      border-radius: 0;
      background: transparent;
      box-shadow: none;
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

    body.track-map-page .header {
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

    body.track-map-page .content {
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
  </style>
</head>
<body>
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
    const statusEl = document.getElementById('status');
    const contentEl = document.getElementById('content');
    let lastSequence = null;
    let cachedTrackMap = null;
    let cachedTrackMapSettings = { internalOpacity: 0.88 };
    let nextTrackMapFetchAt = 0;
    const streamChatState = {
      initialized: false,
      settings: null,
      socket: null,
      messages: [],
      reconnectTimer: null,
      reconnectAllowed: true,
      twitchChannel: null,
      connectedAnnounced: false
    };

    if (page.renderer === 'trackMap') {
      document.body.classList.add('track-map-page');
    }

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
    const carNumber = (row) => row?.carNumber ? `#${row.carNumber}` : `#${row?.carIdx ?? '--'}`;
    const isWaiting = (live) => !live?.isConnected || !live?.isCollecting;
    const quality = (model) => model?.quality ?? 'unavailable';

    function setStatus(live, detail) {
      if (!live?.isConnected) {
        statusEl.textContent = 'iRacing disconnected';
        return;
      }
      if (!live?.isCollecting) {
        statusEl.textContent = 'waiting for telemetry';
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
      const headerHtml = headers.map((header) => `<th>${escapeHtml(header.label)}</th>`).join('');
      const rowHtml = rows.map((row) => {
        const cells = headers.map((header) => `<td>${header.value(row)}</td>`).join('');
        return `<tr class="${row.isFocus || row.isReferenceCar ? 'focus' : ''}">${cells}</tr>`;
      }).join('');
      return `<table><thead><tr>${headerHtml}</tr></thead><tbody>${rowHtml}</tbody></table>`;
    }

    function renderStandings(live) {
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

    function renderRelative(live) {
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

    function renderFuel(live) {
      const fuel = live?.models?.fuelPit?.fuel || live?.fuel || {};
      const pit = live?.models?.fuelPit || {};
      contentEl.innerHTML = `
        <div class="grid">
          ${metric('Fuel', `${formatNumber(fuel.fuelLevelLiters)} L`)}
          ${metric('Tank', formatPercent(fuel.fuelLevelPercent))}
          ${metric('Burn', `${formatNumber(fuel.fuelPerLapLiters, 2)} L/lap`)}
          ${metric('Laps left', formatNumber(fuel.estimatedLapsRemaining))}
          ${metric('Time left', `${formatNumber(fuel.estimatedMinutesRemaining)} min`)}
          ${metric('Pit road', pit.onPitRoad ? 'IN' : 'OUT')}
        </div>`;
      setStatus(live, fuel.hasValidFuel ? `live | ${fuel.confidence || 'fuel'}` : 'waiting for fuel');
    }

    function renderFlags(live) {
      const session = live?.models?.session || {};
      const flags = session.sessionFlags;
      const raw = Number.isFinite(flags) ? `0x${(flags >>> 0).toString(16).toUpperCase().padStart(8, '0')}` : '--';
      const primary = flagLabel(flags);
      contentEl.innerHTML = `
        <div class="grid">
          ${metric('Flag', primary)}
          ${metric('Raw', raw)}
          ${metric('Session', session.sessionType || session.sessionName || '--')}
          ${metric('State', Number.isFinite(session.sessionState) ? session.sessionState : '--')}
        </div>`;
      setStatus(live, Number.isFinite(flags) ? 'live | flags' : 'waiting for flags');
    }

    function renderWeather(live) {
      const session = live?.models?.session || {};
      const weather = live?.models?.weather || {};
      contentEl.innerHTML = `
        <div class="grid">
          ${metric('Track', session.trackDisplayName || '--')}
          ${metric('Session', session.sessionType || session.sessionName || '--')}
          ${metric('Air', Number.isFinite(weather.airTempC) ? `${weather.airTempC.toFixed(1)} C` : '--')}
          ${metric('Track temp', Number.isFinite(weather.trackTempCrewC) ? `${weather.trackTempCrewC.toFixed(1)} C` : '--')}
          ${metric('Surface', weather.trackWetnessLabel || '--')}
          ${metric('Skies', weather.skiesLabel || weather.weatherType || '--')}
        </div>`;
      setStatus(live, session.hasData || weather.hasData ? 'live | session' : 'waiting for session');
    }

    function renderPitService(live) {
      const pit = live?.models?.fuelPit || {};
      contentEl.innerHTML = `
        <div class="grid">
          ${metric('Pit road', pit.onPitRoad ? 'IN' : 'OUT')}
          ${metric('Pit stall', pit.playerCarInPitStall ? 'IN' : 'OUT')}
          ${metric('Service', pit.pitstopActive ? 'ACTIVE' : 'idle')}
          ${metric('Fuel req', Number.isFinite(pit.pitServiceFuelLiters) ? `${pit.pitServiceFuelLiters.toFixed(1)} L` : '--')}
          ${metric('Repair', Number.isFinite(pit.pitRepairLeftSeconds) ? `${pit.pitRepairLeftSeconds.toFixed(0)} s` : '--')}
          ${metric('Tires used', Number.isFinite(pit.tireSetsUsed) ? pit.tireSetsUsed : '--')}
        </div>`;
      setStatus(live, pit.hasData ? 'live | pit' : 'waiting for pit');
    }

    function renderInputs(live) {
      const inputs = live?.models?.inputs || {};
      contentEl.innerHTML = `
        <div class="bars">
          ${bar('Throttle', inputs.throttle, '#4dd77a')}
          ${bar('Brake', inputs.brake, '#ff6b63')}
          ${bar('Clutch', inputs.clutch, '#62c7ff')}
          <div class="grid">
            ${metric('Gear', inputs.gear === -1 ? 'R' : inputs.gear === 0 ? 'N' : inputs.gear ?? '--')}
            ${metric('RPM', Number.isFinite(inputs.rpm) ? Math.round(inputs.rpm).toLocaleString() : '--')}
            ${metric('Speed', formatSpeed(inputs.speedMetersPerSecond))}
            ${metric('Steering', Number.isFinite(inputs.steeringWheelAngle) ? `${inputs.steeringWheelAngle.toFixed(1)} deg` : '--')}
          </div>
        </div>`;
      setStatus(live, inputs.hasData ? `live | ${quality(inputs)}` : 'waiting for inputs');
    }

    function renderRadar(live) {
      const spatial = live?.models?.spatial || {};
      const cars = spatial.cars || [];
      contentEl.innerHTML = rowsTable([
        { label: 'Car', value: (row) => `#${row.carIdx}` },
        { label: 'Dir', value: (row) => row.relativeLaps > 0 ? 'Ahead' : 'Behind' },
        { label: 'Meters', value: (row) => Number.isFinite(row.relativeMeters) ? row.relativeMeters.toFixed(1) : '--' },
        { label: 'Gap', value: (row) => formatSeconds(row.relativeSeconds) },
        { label: 'Pit', value: (row) => row.onPitRoad ? 'IN' : '' }
      ], cars.slice(0, 12));
      setStatus(live, spatial.hasData ? `live | ${spatial.sideStatus || 'radar'}` : 'waiting for radar');
    }

    function renderGap(live) {
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

    async function initStreamChat() {
      if (streamChatState.initialized) {
        return;
      }

      streamChatState.initialized = true;
      try {
        const response = await fetch('/api/stream-chat', { cache: 'no-store' });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const payload = await response.json();
        const settings = payload.streamChat || {};
        streamChatState.settings = settings;
        if (!settings.isConfigured) {
          renderChatLines([
            { name: 'TMR', text: streamChatStatusText(settings.status), kind: 'system' }
          ]);
          statusEl.textContent = 'waiting for chat source';
          return;
        }

        if (settings.provider === 'streamlabs') {
          renderStreamlabsChat(settings.streamlabsWidgetUrl);
          return;
        }

        if (settings.provider === 'twitch') {
          connectTwitchChat(settings.twitchChannel);
          return;
        }

        renderChatLines([{ name: 'TMR', text: 'Stream chat provider is not supported.', kind: 'error' }]);
        statusEl.textContent = 'chat provider unavailable';
      } catch (error) {
        renderChatLines([{ name: 'TMR', text: `Chat settings unavailable: ${error.message}`, kind: 'error' }]);
        statusEl.textContent = 'chat settings unavailable';
      }
    }

    function renderStreamlabsChat(url) {
      if (!url) {
        renderChatLines([{ name: 'TMR', text: 'Streamlabs Chat Box URL is missing.', kind: 'error' }]);
        statusEl.textContent = 'streamlabs not configured';
        return;
      }

      contentEl.innerHTML = `
        <div class="chat">
          <div class="chat-line system">
            <div class="chat-name">TMR</div>
            <div class="chat-text">Chat connected through Streamlabs.</div>
          </div>
          <iframe class="chat-frame" title="Streamlabs Chat Box" src="${escapeAttribute(url)}" allowtransparency="true"></iframe>
        </div>`;
      const frame = contentEl.querySelector('iframe');
      frame.addEventListener('load', () => {
        statusEl.textContent = 'chat connected | streamlabs';
      });
      statusEl.textContent = 'connecting | streamlabs';
    }

    function connectTwitchChat(channel) {
      const normalized = normalizeTwitchChannel(channel);
      if (!normalized) {
        renderChatLines([{ name: 'TMR', text: 'Twitch channel is missing or invalid.', kind: 'error' }]);
        statusEl.textContent = 'twitch not configured';
        return;
      }

      clearTimeout(streamChatState.reconnectTimer);
      if (streamChatState.socket) {
        streamChatState.socket.close();
      }

      renderChatLines([{ name: 'TMR', text: `Connecting to #${normalized}...`, kind: 'system' }]);
      statusEl.textContent = 'connecting | twitch';
      const socket = new WebSocket('wss://irc-ws.chat.twitch.tv:443');
      streamChatState.socket = socket;
      streamChatState.reconnectAllowed = true;
      streamChatState.twitchChannel = normalized;
      streamChatState.connectedAnnounced = false;
      socket.addEventListener('open', () => {
        const nick = `justinfan${Math.floor(10000 + Math.random() * 89999)}`;
        socket.send('CAP REQ :twitch.tv/tags twitch.tv/commands');
        socket.send('PASS SCHMOOPIIE');
        socket.send(`NICK ${nick}`);
        socket.send(`JOIN #${normalized}`);
        statusEl.textContent = 'joining | twitch';
      });
      socket.addEventListener('message', (event) => handleTwitchIrcMessage(socket, String(event.data || '')));
      socket.addEventListener('error', () => {
        pushChatLine('TMR', 'Twitch chat connection error.', 'error');
        statusEl.textContent = 'chat error | twitch';
      });
      socket.addEventListener('close', () => {
        if (streamChatState.socket !== socket) {
          return;
        }
        if (!streamChatState.reconnectAllowed) {
          streamChatState.socket = null;
          return;
        }

        statusEl.textContent = 'chat reconnecting | twitch';
        streamChatState.reconnectTimer = setTimeout(() => connectTwitchChat(normalized), 3500);
      });
    }

    function handleTwitchIrcMessage(socket, payload) {
      for (const rawLine of payload.split('\r\n')) {
        const line = rawLine.trim();
        if (!line) continue;
        if (line.startsWith('PING')) {
          socket.send(`PONG ${line.slice(5)}`);
          continue;
        }
        if (isTwitchAuthFailure(line)) {
          streamChatState.reconnectAllowed = false;
          pushChatLine('TMR', 'Twitch rejected the chat connection. Use Streamlabs for this build or add Twitch auth in a future pass.', 'error');
          statusEl.textContent = 'twitch auth rejected';
          socket.close();
          continue;
        }
        if (line.includes(' RECONNECT ')) {
          socket.close();
          continue;
        }
        if (isTwitchJoined(line)) {
          announceTwitchConnected();
          continue;
        }
        if (!line.includes(' PRIVMSG ')) {
          continue;
        }

        const parsed = parseTwitchPrivMsg(line);
        if (parsed) {
          pushChatLine(parsed.name, parsed.text, 'message');
        }
      }
    }

    function isTwitchAuthFailure(line) {
      return line.includes(' NOTICE * :Login authentication failed')
        || line.includes(' NOTICE * :Improperly formatted auth');
    }

    function isTwitchJoined(line) {
      const channel = streamChatState.twitchChannel;
      return line.includes(' 001 ')
        || (channel && line.includes(` ROOMSTATE #${channel}`));
    }

    function announceTwitchConnected() {
      if (streamChatState.connectedAnnounced) {
        return;
      }

      streamChatState.connectedAnnounced = true;
      const channel = streamChatState.twitchChannel || 'channel';
      pushChatLine('TMR', `Chat connected to #${channel}.`, 'system');
      statusEl.textContent = 'chat connected | twitch';
    }

    function parseTwitchPrivMsg(line) {
      const messageIndex = line.indexOf(' PRIVMSG ');
      const textIndex = line.indexOf(' :', messageIndex);
      if (messageIndex < 0 || textIndex < 0) {
        return null;
      }

      const prefixAndTags = line.slice(0, messageIndex);
      const tags = parseTwitchTags(prefixAndTags);
      const text = decodeTwitchTag(line.slice(textIndex + 2));
      const fallbackName = prefixAndTags.match(/:([^! ]+)/)?.[1] || 'chat';
      return {
        name: tags['display-name'] || fallbackName,
        text
      };
    }

    function parseTwitchTags(prefixAndTags) {
      if (!prefixAndTags.startsWith('@')) {
        return {};
      }

      const tagText = prefixAndTags.slice(1, prefixAndTags.indexOf(' '));
      return Object.fromEntries(tagText.split(';').map((pair) => {
        const splitAt = pair.indexOf('=');
        if (splitAt < 0) return [pair, ''];
        return [pair.slice(0, splitAt), decodeTwitchTag(pair.slice(splitAt + 1))];
      }));
    }

    function decodeTwitchTag(value) {
      return String(value || '')
        .replace(/\\s/g, ' ')
        .replace(/\\:/g, ';')
        .replace(/\\\\/g, '\\');
    }

    function pushChatLine(name, text, kind) {
      streamChatState.messages.push({ name, text, kind });
      if (streamChatState.messages.length > 48) {
        streamChatState.messages.splice(0, streamChatState.messages.length - 48);
      }
      renderChatLines(streamChatState.messages);
    }

    function renderChatLines(lines) {
      contentEl.innerHTML = `
        <div class="chat">
          ${lines.map((line) => `
            <div class="chat-line ${escapeAttribute(line.kind || 'message')}">
              <div class="chat-name">${escapeHtml(line.name)}</div>
              <div class="chat-text">${escapeHtml(line.text)}</div>
            </div>`).join('')}
        </div>`;
    }

    function streamChatStatusText(status) {
      switch (status) {
        case 'missing_or_invalid_streamlabs_url':
          return 'Choose Streamlabs and paste a valid Streamlabs Chat Box widget URL.';
        case 'missing_or_invalid_twitch_channel':
          return 'Choose Twitch and enter a valid public channel name.';
        default:
          return 'Choose Streamlabs or Twitch in the Stream Chat settings tab.';
      }
    }

    function normalizeTwitchChannel(channel) {
      return String(channel || '').trim().replace(/^@/, '').toLowerCase();
    }

    function renderTrackMap(live, trackMap, trackMapSettings) {
      const spatial = live?.models?.spatial || {};
      const race = live?.models?.raceEvents || {};
      const focusPct = Number.isFinite(spatial.referenceLapDistPct)
        ? spatial.referenceLapDistPct
        : Number.isFinite(race.lapDistPct)
          ? race.lapDistPct
          : 0;
      const markers = trackMapMarkers(live, focusPct);
      const svg = trackMapSvg(trackMap, markers, trackMapSettings);
      contentEl.innerHTML = `
        <div class="track">
          ${svg}
        </div>`;
      setStatus(live, spatial.hasData || race.hasData ? 'live | track map' : 'waiting for position');
    }

    function trackMapSvg(trackMap, markers, settings) {
      const interior = trackMapInteriorFill(settings);
      const racingLine = trackMap?.racingLine;
      if (racingLine?.points?.length >= 3) {
        const transform = trackMapTransform(trackMap);
        if (transform) {
          const racingPath = pathForGeometry(racingLine, transform);
          const interiorPath = racingLine.closed
            ? `<path d="${racingPath}" fill="${interior}" stroke="none"></path>`
            : '';
          const pitPath = trackMap?.pitLane?.points?.length >= 2
            ? `<path d="${pathForGeometry(trackMap.pitLane, transform)}" fill="none" stroke="rgba(98,199,255,0.74)" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"></path>`
            : '';
          const dots = markers.map((marker) => markerSvg(marker, pointOnGeometry(racingLine, transform, marker.lapDistPct))).join('');
          return `
            <svg viewBox="0 0 320 320" role="img" aria-label="Track map">
              ${interiorPath}
              <path d="${racingPath}" fill="none" stroke="rgba(255,255,255,0.32)" stroke-width="11" stroke-linecap="round" stroke-linejoin="round"></path>
              <path d="${racingPath}" fill="none" stroke="rgba(222,238,246,1)" stroke-width="4.4" stroke-linecap="round" stroke-linejoin="round"></path>
              ${pitPath}
              ${dots}
            </svg>`;
        }
      }

      const dots = markers.map((marker) => markerSvg(marker, pointOnCircle(marker.lapDistPct))).join('');
      return `
        <svg viewBox="0 0 320 320" role="img" aria-label="Track map">
          <circle cx="160" cy="160" r="138" fill="${interior}" stroke="none"></circle>
          <circle cx="160" cy="160" r="138" fill="none" stroke="rgba(255,255,255,0.32)" stroke-width="11"></circle>
          <circle cx="160" cy="160" r="138" fill="none" stroke="rgba(222,238,246,1)" stroke-width="4.4"></circle>
          ${dots}
        </svg>`;
    }

    function trackMapInteriorFill(settings) {
      const opacity = Math.max(0.2, Math.min(1, Number(settings?.internalOpacity ?? 0.88)));
      return `rgba(9,14,18,${(0.59 * opacity).toFixed(3)})`;
    }

    function trackMapTransform(trackMap) {
      const points = [
        ...(trackMap?.racingLine?.points || []),
        ...(trackMap?.pitLane?.points || [])
      ].filter((point) => Number.isFinite(point.x) && Number.isFinite(point.y));
      if (!points.length) return null;
      const minX = Math.min(...points.map((point) => point.x));
      const maxX = Math.max(...points.map((point) => point.x));
      const minY = Math.min(...points.map((point) => point.y));
      const maxY = Math.max(...points.map((point) => point.y));
      const width = Math.max(1, maxX - minX);
      const height = Math.max(1, maxY - minY);
      const scale = Math.min(284 / width, 284 / height);
      if (!Number.isFinite(scale) || scale <= 0) return null;
      return {
        minX,
        maxY,
        scale,
        left: 18 + (284 - width * scale) / 2,
        top: 18 + (284 - height * scale) / 2
      };
    }

    function mapPoint(point, transform) {
      return {
        x: transform.left + (point.x - transform.minX) * transform.scale,
        y: transform.top + (transform.maxY - point.y) * transform.scale
      };
    }

    function pathForGeometry(geometry, transform) {
      const points = geometry.points || [];
      if (!points.length) return '';
      const mapped = points.map((point) => mapPoint(point, transform));
      const commands = mapped.map((point, index) => `${index === 0 ? 'M' : 'L'}${point.x.toFixed(1)} ${point.y.toFixed(1)}`);
      return `${commands.join(' ')}${geometry.closed ? ' Z' : ''}`;
    }

    function pointOnGeometry(geometry, transform, progress) {
      const points = geometry?.points || [];
      if (!points.length) return null;
      if (points.length === 1) return mapPoint(points[0], transform);
      const pct = normalizeProgress(progress);
      for (let index = 1; index < points.length; index += 1) {
        const previous = points[index - 1];
        const current = points[index];
        if (pct >= previous.lapDistPct && pct <= current.lapDistPct) {
          return interpolateTrackPoint(previous, current, pct, transform);
        }
      }
      if (geometry.closed) {
        const previous = points[points.length - 1];
        const current = { ...points[0], lapDistPct: points[0].lapDistPct + 1 };
        const adjusted = pct < previous.lapDistPct ? pct + 1 : pct;
        return interpolateTrackPoint(previous, current, adjusted, transform);
      }
      return mapPoint(points[0], transform);
    }

    function interpolateTrackPoint(previous, current, target, transform) {
      const span = current.lapDistPct - previous.lapDistPct;
      const ratio = span <= 0 ? 0 : Math.max(0, Math.min(1, (target - previous.lapDistPct) / span));
      return mapPoint({
        x: previous.x + (current.x - previous.x) * ratio,
        y: previous.y + (current.y - previous.y) * ratio
      }, transform);
    }

    function pointOnCircle(progress) {
      const angle = normalizeProgress(progress) * Math.PI * 2 - Math.PI / 2;
      return {
        x: 160 + Math.cos(angle) * 138,
        y: 160 + Math.sin(angle) * 138
      };
    }

    function markerSvg(marker, point) {
      if (!point) return '';
      const radius = marker.isFocus && marker.positionLabel
        ? focusMarkerRadius(marker.positionLabel)
        : marker.isFocus ? 5.7 : 3.6;
      const circle = `<circle cx="${point.x.toFixed(1)}" cy="${point.y.toFixed(1)}" r="${radius}" fill="${marker.color}" stroke="rgb(8,14,18)" stroke-width="${marker.isFocus ? 2 : 1.4}"></circle>`;
      if (!marker.isFocus || !marker.positionLabel) {
        return circle;
      }

      return `
        <g>
          ${circle}
          <text x="${point.x.toFixed(1)}" y="${(point.y + 2.9).toFixed(1)}" text-anchor="middle" font-size="7.6" font-weight="800" fill="rgb(5,12,16)">${escapeHtml(marker.positionLabel)}</text>
        </g>`;
    }

    function focusMarkerRadius(label) {
      return Math.max(5.7, 5.1 + String(label || '').length * 2.9);
    }

    function trackMapMarkers(live, fallbackFocusPct) {
      const markers = new Map();
      const rows = [
        ...(live?.models?.timing?.overallRows || []),
        ...(live?.models?.timing?.classRows || [])
      ];
      for (const row of rows) {
        if (!Number.isFinite(row.lapDistPct) || row.lapDistPct < 0) continue;
        const isFocus = Boolean(row.isFocus || row.isPlayer);
        const marker = {
          carIdx: row.carIdx,
          lapDistPct: normalizeProgress(row.lapDistPct),
          isFocus,
          color: isFocus ? '#62c7ff' : markerColor(row.carClassColorHex),
          positionLabel: isFocus ? positionLabel(row) : null
        };
        const existing = markers.get(row.carIdx);
        if (!existing || marker.isFocus || !existing.isFocus) {
          markers.set(row.carIdx, marker);
        }
      }

      const focusCarIdx = live?.models?.timing?.focusCarIdx
        ?? live?.models?.timing?.playerCarIdx
        ?? live?.models?.spatial?.referenceCarIdx
        ?? live?.latestSample?.focusCarIdx
        ?? live?.latestSample?.playerCarIdx
        ?? -1;
      if (Number.isFinite(fallbackFocusPct) && fallbackFocusPct >= 0) {
        markers.set(focusCarIdx, {
          carIdx: focusCarIdx,
          lapDistPct: normalizeProgress(fallbackFocusPct),
          isFocus: true,
          color: '#62c7ff',
          positionLabel: focusPositionLabel(live)
        });
      }

      return [...markers.values()].sort((left, right) => Number(left.isFocus) - Number(right.isFocus) || left.carIdx - right.carIdx);
    }

    function positionLabel(row) {
      const position = row?.classPosition ?? row?.overallPosition;
      return Number.isFinite(position) && position > 0 ? `P${position}` : null;
    }

    function focusPositionLabel(live) {
      const timing = live?.models?.timing || {};
      return positionLabel(timing.focusRow) || positionLabel(timing.playerRow) || samplePositionLabel(live?.latestSample);
    }

    function samplePositionLabel(sample) {
      const position = sample?.focusClassPosition
        ?? sample?.teamClassPosition
        ?? sample?.focusPosition
        ?? sample?.teamPosition;
      return Number.isFinite(position) && position > 0 ? `P${position}` : null;
    }

    function markerColor(value) {
      const text = String(value || '').trim();
      return /^#[0-9a-f]{6}$/i.test(text) ? text : '#ecf4f8';
    }

    function normalizeProgress(value) {
      if (!Number.isFinite(value)) return 0;
      const normalized = value % 1;
      return normalized < 0 ? normalized + 1 : normalized;
    }

    function metric(label, value) {
      return `<div class="metric"><div class="label">${escapeHtml(label)}</div><div class="value">${escapeHtml(value)}</div></div>`;
    }

    function flagLabel(flags) {
      if (!Number.isFinite(flags)) return '--';
      if (flags & 0x00010000 || flags & 0x00100000) return 'Critical';
      if (flags & 0x00020000) return 'Disqualify';
      if (flags & 0x00200000 || flags & 0x00400000) return 'Repair';
      if (flags & 0x00000010) return 'Blue';
      if (flags & 0x00080000) return 'Checkered';
      if (flags & 0x00008000 || flags & 0x00004000 || flags & 0x00002000 || flags & 0x00000200 || flags & 0x00000100 || flags & 0x00000040 || flags & 0x00000008) return 'Yellow';
      if (flags & 0x00000020) return 'White';
      if (flags & 0x00000001) return 'Green';
      if (flags & 0x00000002) return 'Checkered';
      return flags === 0 ? 'None' : 'Other';
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

    async function refreshTrackMapAsset() {
      if (page.renderer !== 'trackMap' || Date.now() < nextTrackMapFetchAt) {
        return;
      }

      nextTrackMapFetchAt = Date.now() + 10000;
      try {
        const response = await fetch('/api/track-map', { cache: 'no-store' });
        if (!response.ok) return;
        const payload = await response.json();
        cachedTrackMap = payload.trackMap || null;
        cachedTrackMapSettings = payload.trackMapSettings || cachedTrackMapSettings;
      } catch {
        cachedTrackMap = null;
      }
    }

    function render(live, trackMap) {
      if (isWaiting(live) && page.renderer !== 'trackMap' && page.renderer !== 'streamChat') {
        contentEl.innerHTML = '<div class="empty">Waiting for iRacing telemetry.</div>';
        setStatus(live);
        return;
      }

      switch (page.renderer) {
        case 'standings':
          renderStandings(live);
          break;
        case 'relative':
          renderRelative(live);
          break;
        case 'fuel':
          renderFuel(live);
          break;
        case 'flags':
          renderFlags(live);
          break;
        case 'weather':
          renderWeather(live);
          break;
        case 'pitService':
          renderPitService(live);
          break;
        case 'inputs':
          renderInputs(live);
          break;
        case 'radar':
          renderRadar(live);
          break;
        case 'gap':
          renderGap(live);
          break;
        case 'trackMap':
          renderTrackMap(live, trackMap, cachedTrackMapSettings);
          break;
        case 'streamChat':
          initStreamChat();
          break;
        default:
          contentEl.innerHTML = '<div class="empty">Unknown overlay route.</div>';
          statusEl.textContent = 'not configured';
      }
    }

    async function refresh() {
      try {
        await refreshTrackMapAsset();
        const response = await fetch('/api/snapshot', { cache: 'no-store' });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const payload = await response.json();
        render(payload.live, cachedTrackMap);
      } catch (error) {
        if (page.renderer === 'trackMap') {
          renderTrackMap(null, cachedTrackMap, cachedTrackMapSettings);
          return;
        }
        statusEl.textContent = 'localhost offline';
        contentEl.innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`;
      }
    }

    if (page.renderer === 'streamChat') {
      initStreamChat();
    } else {
      refresh();
      setInterval(refresh, 250);
    }
  </script>
</body>
</html>
""";
}
