using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Settings;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.StreamChat;

internal sealed class StreamChatForm : PersistentOverlayForm
{
    private const int SettingsRefreshIntervalMilliseconds = 1000;
    private const int MaximumMessages = 64;
    private const int VisibleMessageBudget = 36;
    private static readonly Uri TwitchChatUri = new("wss://irc-ws.chat.twitch.tv:443");

    private readonly AppSettingsStore _settingsStore;
    private readonly ILogger<StreamChatForm> _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly string _fontFamily;
    private readonly System.Windows.Forms.Timer _settingsTimer;
    private readonly List<StreamChatMessage> _messages = [];
    private readonly Button? _closeButton;
    private CancellationTokenSource? _connectionCancellation;
    private Task? _connectionTask;
    private string _status = "waiting for chat source";
    private string? _activeSettingsKey;
    private string? _lastLoggedError;
    private bool _connectedAnnounced;
    private bool _disposed;

    public StreamChatForm(
        AppSettingsStore settingsStore,
        ILogger<StreamChatForm> logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            StreamChatOverlayDefinition.Definition.DefaultWidth,
            StreamChatOverlayDefinition.Definition.DefaultHeight)
    {
        _settingsStore = settingsStore;
        _logger = logger;
        _performanceState = performanceState;
        _fontFamily = fontFamily;

        BackColor = OverlayTheme.Colors.WindowBackground;
        MinimumSize = new Size(280, 260);

        _settingsTimer = new System.Windows.Forms.Timer
        {
            Interval = SettingsRefreshIntervalMilliseconds
        };
        _settingsTimer.Tick += (_, _) =>
        {
            _performanceState.RecordOverlayTimerTick(
                StreamChatOverlayDefinition.Definition.Id,
                SettingsRefreshIntervalMilliseconds,
                Visible,
                !Visible || Opacity <= 0.001d);
            RefreshChatSettings();
        };
        _settingsTimer.Start();

        _closeButton = CreateCloseButton();
        Controls.Add(_closeButton);
        LayoutCloseButton();

        RefreshChatSettings(force: true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            _settingsTimer.Stop();
            _settingsTimer.Dispose();
            _closeButton?.Dispose();
            StopConnection();
        }

        base.Dispose(disposing);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutCloseButton();
    }

    public override bool IsIntrinsicallyInputTransparentOverlay => true;

    protected override bool ShouldReceiveInputWhileTransparent(Point clientPoint)
    {
        return IsCloseButtonHit(clientPoint)
            || IsHeaderDragHit(clientPoint, ClientSize);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && IsCloseButtonHit(e.Location))
        {
            DisableOverlayAndClose();
            return;
        }

        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            DrawHeader(e.Graphics);
            DrawMessages(e.Graphics);
            using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);
            e.Graphics.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(AppPerformanceMetricIds.OverlayStreamChatPaint, started, succeeded);
        }
    }

    private void RefreshChatSettings(bool force = false)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var settings = StreamChatOverlaySettings.From(_settingsStore.Load());
            ApplyChatSettings(settings, force);
            succeeded = true;
        }
        catch (Exception exception)
        {
            LogWarningOnce(exception, "settings");
            StopConnection();
            ReplaceMessages(new StreamChatMessage("TMR", "Chat settings unavailable.", StreamChatMessageKind.Error));
            SetStatus("chat settings unavailable");
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayStreamChatSettingsRefresh,
                started,
                succeeded);
        }
    }

    private void ApplyChatSettings(StreamChatBrowserSettings settings, bool force)
    {
        var settingsKey = $"{settings.Provider}|{settings.StreamlabsWidgetUrl}|{settings.TwitchChannel}|{settings.Status}";
        if (!force && string.Equals(_activeSettingsKey, settingsKey, StringComparison.Ordinal))
        {
            return;
        }

        _activeSettingsKey = settingsKey;
        _connectedAnnounced = false;
        StopConnection();

        if (!settings.IsConfigured)
        {
            ReplaceMessages(new StreamChatMessage("TMR", StreamChatStatusText(settings.Status), StreamChatMessageKind.System));
            SetStatus("waiting for chat source");
            return;
        }

        if (string.Equals(settings.Provider, StreamChatOverlaySettings.ProviderTwitch, StringComparison.Ordinal)
            && settings.TwitchChannel is { Length: > 0 } channel)
        {
            ReplaceMessages(new StreamChatMessage("TMR", $"Connecting to #{channel}...", StreamChatMessageKind.System));
            SetStatus("connecting | twitch");
            StartTwitchConnection(channel);
            return;
        }

        if (string.Equals(settings.Provider, StreamChatOverlaySettings.ProviderStreamlabs, StringComparison.Ordinal))
        {
            ReplaceMessages(new StreamChatMessage("TMR", "Streamlabs is browser-source only in this build.", StreamChatMessageKind.Error));
            SetStatus("streamlabs unavailable");
            return;
        }

        ReplaceMessages(new StreamChatMessage("TMR", "Stream chat provider unavailable.", StreamChatMessageKind.Error));
        SetStatus("chat provider unavailable");
    }

    private void StartTwitchConnection(string channel)
    {
        StopConnection();
        var cancellation = new CancellationTokenSource();
        _connectionCancellation = cancellation;
        _connectionTask = Task.Run(() => RunTwitchConnectionLoopAsync(channel, cancellation.Token), CancellationToken.None);
    }

    private void StopConnection()
    {
        var cancellation = _connectionCancellation;
        var task = _connectionTask;
        _connectionCancellation = null;
        _connectionTask = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        _ = (task ?? Task.CompletedTask).ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task RunTwitchConnectionLoopAsync(string channel, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var shouldReconnect = true;
            try
            {
                shouldReconnect = await ConnectAndReadTwitchAsync(channel, cancellationToken).ConfigureAwait(false);
                attempt = shouldReconnect ? attempt + 1 : 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                attempt++;
                LogWarningOnce(exception, "connection");
                RunOnUiThread(() =>
                {
                    ConfirmConnectionMessage(new StreamChatMessage("TMR", "Twitch chat connection error.", StreamChatMessageKind.Error));
                    SetStatus("chat reconnecting | twitch");
                });
            }

            if (!shouldReconnect || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var delay = TimeSpan.FromSeconds(Math.Min(15, 3 + attempt * 2));
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task<bool> ConnectAndReadTwitchAsync(string channel, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        using var socket = new ClientWebSocket();
        try
        {
            RunOnUiThread(() => SetStatus("connecting | twitch"));
            await socket.ConnectAsync(TwitchChatUri, cancellationToken).ConfigureAwait(false);
            await SendRawAsync(socket, "CAP REQ :twitch.tv/tags twitch.tv/commands", cancellationToken).ConfigureAwait(false);
            await SendRawAsync(socket, "PASS SCHMOOPIIE", cancellationToken).ConfigureAwait(false);
            await SendRawAsync(socket, $"NICK justinfan{Random.Shared.Next(10000, 99999)}", cancellationToken).ConfigureAwait(false);
            await SendRawAsync(socket, $"JOIN #{channel}", cancellationToken).ConfigureAwait(false);
            RunOnUiThread(() => SetStatus("joining | twitch"));
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(AppPerformanceMetricIds.OverlayStreamChatConnect, started, succeeded);
        }

        return await ReceiveTwitchMessagesAsync(socket, channel, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ReceiveTwitchMessagesAsync(
        ClientWebSocket socket,
        string channel,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                HandleSocketClosed();
                return true;
            }

            builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (!result.EndOfMessage)
            {
                continue;
            }

            var shouldContinue = await ProcessTwitchPayloadAsync(
                socket,
                channel,
                builder.ToString(),
                cancellationToken).ConfigureAwait(false);
            builder.Clear();
            if (!shouldContinue)
            {
                return false;
            }
        }

        HandleSocketClosed();
        return true;
    }

    private async Task<bool> ProcessTwitchPayloadAsync(
        ClientWebSocket socket,
        string channel,
        string payload,
        CancellationToken cancellationToken)
    {
        foreach (var rawLine in payload.Split("\r\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (StreamChatIrcParser.TryGetPingResponse(rawLine, out var pong))
            {
                await SendRawAsync(socket, pong, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (StreamChatIrcParser.IsAuthFailure(rawLine))
            {
                RunOnUiThread(() =>
                {
                    ConfirmConnectionMessage(new StreamChatMessage("TMR", "Twitch rejected the chat connection.", StreamChatMessageKind.Error));
                    SetStatus("twitch auth rejected");
                });
                return false;
            }

            if (StreamChatIrcParser.IsReconnect(rawLine))
            {
                RunOnUiThread(() => SetStatus("chat reconnecting | twitch"));
                return true;
            }

            if (StreamChatIrcParser.IsJoined(rawLine, channel))
            {
                AnnounceConnected(channel);
                continue;
            }

            var message = StreamChatIrcParser.TryParsePrivMsg(rawLine);
            if (message is not null)
            {
                RunOnUiThread(() => AddMessage(message));
            }
        }

        return true;
    }

    private static async Task SendRawAsync(ClientWebSocket socket, string line, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    private void AnnounceConnected(string channel)
    {
        RunOnUiThread(() =>
        {
            if (_connectedAnnounced)
            {
                return;
            }

            _connectedAnnounced = true;
            ConfirmConnectionMessage(new StreamChatMessage("TMR", $"Chat connected to #{channel}.", StreamChatMessageKind.System));
            SetStatus("chat connected | twitch");
        });
    }

    private void HandleSocketClosed()
    {
        RunOnUiThread(() =>
        {
            if (_connectedAnnounced)
            {
                SetStatus("chat reconnecting | twitch");
                return;
            }

            ConfirmConnectionMessage(new StreamChatMessage("TMR", "Twitch chat disconnected before joining.", StreamChatMessageKind.Error));
            SetStatus("chat reconnecting | twitch");
        });
    }

    private void ReplaceMessages(params StreamChatMessage[] messages)
    {
        _messages.Clear();
        _messages.AddRange(messages);
        Invalidate();
    }

    private void AddMessage(StreamChatMessage message)
    {
        _messages.Add(message);
        if (_messages.Count > MaximumMessages)
        {
            _messages.RemoveRange(0, _messages.Count - MaximumMessages);
        }

        Invalidate();
    }

    private void ConfirmConnectionMessage(StreamChatMessage message)
    {
        if (_messages.Count == 1 && _messages[0].Kind == StreamChatMessageKind.System)
        {
            _messages[0] = message;
            Invalidate();
            return;
        }

        AddMessage(message);
    }

    private void SetStatus(string status)
    {
        if (string.Equals(_status, status, StringComparison.Ordinal))
        {
            return;
        }

        _status = status;
        Invalidate();
    }

    private void RunOnUiThread(Action action)
    {
        if (_disposed || IsDisposed)
        {
            return;
        }

        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
                return;
            }

            action();
        }
        catch (InvalidOperationException)
        {
            // The overlay can be closing while the chat socket is still unwinding.
        }
    }

    private void DrawHeader(Graphics graphics)
    {
        var header = new Rectangle(0, 0, ClientSize.Width, 42);
        using var headerBrush = new SolidBrush(OverlayTheme.Colors.TitleBarBackground);
        using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);
        graphics.FillRectangle(headerBrush, header);
        graphics.DrawLine(borderPen, 0, header.Bottom, ClientSize.Width, header.Bottom);

        using var titleFont = OverlayTheme.Font(_fontFamily, 11f, FontStyle.Bold);
        using var statusFont = OverlayTheme.Font(_fontFamily, 8.7f);
        using var titleBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        using var statusBrush = new SolidBrush(OverlayTheme.Colors.TextSubtle);
        graphics.DrawString("Stream Chat", titleFont, titleBrush, 14, 11);

        var statusRect = new RectangleF(132, 13, Math.Max(90, ClientSize.Width - 180), 18);
        using var statusFormat = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        graphics.DrawString(_status, statusFont, statusBrush, statusRect, statusFormat);
    }

    private Button CreateCloseButton()
    {
        var button = new Button
        {
            Text = "X",
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            Width = 22,
            Height = 22,
            BackColor = OverlayTheme.Colors.TitleBarBackground,
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = OverlayTheme.Colors.WindowBorder;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(74, 40, 48, 62);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(96, 60, 34, 44);
        button.Click += (_, _) => DisableOverlayAndClose();
        return button;
    }

    private void LayoutCloseButton()
    {
        if (_closeButton is null)
        {
            return;
        }

        _closeButton.Location = new Point(Math.Max(4, ClientSize.Width - _closeButton.Width - 10), 10);
    }

    private bool IsCloseButtonHit(Point clientPoint)
    {
        return _closeButton is not null && _closeButton.Bounds.Contains(clientPoint);
    }

    internal static bool IsHeaderDragHit(Point clientPoint, Size clientSize)
    {
        return clientPoint.X >= 0
            && clientPoint.X < clientSize.Width
            && clientPoint.Y >= 0
            && clientPoint.Y < Math.Min(42, clientSize.Height);
    }

    private void DrawMessages(Graphics graphics)
    {
        var content = new Rectangle(12, 52, Math.Max(120, ClientSize.Width - 24), Math.Max(120, ClientSize.Height - 64));
        using var nameFont = OverlayTheme.Font(_fontFamily, 8.6f, FontStyle.Bold);
        using var messageFont = OverlayTheme.Font(_fontFamily, 9.4f);
        using var format = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter
        };

        var y = content.Bottom;
        var visibleMessages = _messages
            .Skip(Math.Max(0, _messages.Count - VisibleMessageBudget))
            .Reverse()
            .ToArray();
        foreach (var message in visibleMessages)
        {
            var textWidth = Math.Max(80, content.Width - 18);
            var textHeight = Math.Max(
                18,
                (int)Math.Ceiling(graphics.MeasureString(message.Text, messageFont, textWidth, format).Height));
            var height = Math.Min(96, 30 + textHeight);
            y -= height + 7;
            if (y < content.Top)
            {
                break;
            }

            DrawMessage(graphics, message, new Rectangle(content.Left, y, content.Width, height), nameFont, messageFont, format);
        }
    }

    private static void DrawMessage(
        Graphics graphics,
        StreamChatMessage message,
        Rectangle bounds,
        Font nameFont,
        Font messageFont,
        StringFormat format)
    {
        using var background = new SolidBrush(Color.FromArgb(36, 255, 255, 255));
        using var nameBrush = new SolidBrush(NameColor(message.Kind));
        using var textBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        graphics.FillRectangle(background, bounds);
        graphics.DrawString(message.Name, nameFont, nameBrush, new RectangleF(bounds.Left + 9, bounds.Top + 7, bounds.Width - 18, 16), format);
        graphics.DrawString(
            message.Text,
            messageFont,
            textBrush,
            new RectangleF(bounds.Left + 9, bounds.Top + 24, bounds.Width - 18, bounds.Height - 30),
            format);
    }

    private static Color NameColor(StreamChatMessageKind kind)
    {
        return kind switch
        {
            StreamChatMessageKind.System => OverlayTheme.Colors.WarningText,
            StreamChatMessageKind.Error => OverlayTheme.Colors.ErrorText,
            _ => OverlayTheme.Colors.InfoText
        };
    }

    private static string StreamChatStatusText(string status)
    {
        return status switch
        {
            "missing_or_invalid_streamlabs_url" => "Streamlabs Chat Box URL is missing.",
            "missing_or_invalid_twitch_channel" => "Twitch channel is missing.",
            _ => "Choose a chat source in settings."
        };
    }

    private void LogWarningOnce(Exception exception, string phase)
    {
        var key = $"{phase}:{exception.GetType().FullName}:{exception.Message}";
        if (string.Equals(_lastLoggedError, key, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedError = key;
        _logger.LogWarning(exception, "Stream chat overlay {Phase} failed.", phase);
    }
}
