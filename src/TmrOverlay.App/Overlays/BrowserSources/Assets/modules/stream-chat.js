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

TmrBrowserOverlay.register({
  start() {
    initStreamChat();
  },
  render() {
    initStreamChat();
  }
});

async function initStreamChat() {
  if (streamChatState.initialized) {
    return;
  }

  streamChatState.initialized = true;
  try {
    const model = await fetchOverlayModel('stream-chat');
    const settings = model?.streamChat?.settings || {};
    const initialRows = model?.streamChat?.rows || [];
    streamChatState.settings = settings;
    if (!settings.isConfigured) {
      renderChatLines(initialRows.length ? initialRows : [
        { name: 'TMR', text: streamChatStatusText(settings.status), kind: 'system' }
      ]);
      renderHeaderItems(model, model?.status || 'waiting for chat source');
      renderFooterSource(model);
      return;
    }

    if (settings.provider === 'streamlabs') {
      renderStreamlabsChat(settings.streamlabsWidgetUrl);
      return;
    }

    if (settings.provider === 'twitch') {
      if (initialRows.length) {
        renderChatLines(initialRows);
        renderHeaderItems(model, model?.status || 'connecting | twitch');
        renderFooterSource(model);
      }
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
        <div class="chat-text">Connecting to Streamlabs chat...</div>
      </div>
      <iframe class="chat-frame" title="Streamlabs Chat Box" src="${escapeAttribute(url)}" allowtransparency="true"></iframe>
    </div>`;
  const frame = contentEl.querySelector('iframe');
  const statusLine = contentEl.querySelector('.chat-line');
  const statusText = contentEl.querySelector('.chat-text');
  frame.addEventListener('load', () => {
    statusLine.className = 'chat-line system';
    statusText.textContent = 'Chat connected through Streamlabs.';
    statusEl.textContent = 'chat connected | streamlabs';
  });
  frame.addEventListener('error', () => {
    statusLine.className = 'chat-line error';
    statusText.textContent = 'Streamlabs chat frame failed to load.';
    statusEl.textContent = 'chat error | streamlabs';
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
    confirmChatLine('TMR', 'Twitch chat connection error.', 'error');
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

    if (!streamChatState.connectedAnnounced) {
      confirmChatLine('TMR', 'Twitch chat disconnected before joining.', 'error');
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
      confirmChatLine('TMR', 'Twitch rejected the chat connection.', 'error');
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
  confirmChatLine('TMR', `Chat connected to #${channel}.`, 'system');
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

function confirmChatLine(name, text, kind) {
  if (streamChatState.messages.length === 1 && streamChatState.messages[0].kind === 'system') {
    streamChatState.messages[0] = { name, text, kind };
    renderChatLines(streamChatState.messages);
    return;
  }

  pushChatLine(name, text, kind);
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
