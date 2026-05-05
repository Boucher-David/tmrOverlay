using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Settings;
using TmrOverlay.App.TrackMaps;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Localhost;

internal sealed class LocalhostOverlayHostedService : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly LocalhostOverlayOptions _options;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly TrackMapStore _trackMapStore;
    private readonly AppSettingsStore _settingsStore;
    private readonly ILogger<LocalhostOverlayHostedService> _logger;
    private CancellationTokenSource? _cancellation;
    private HttpListener? _listener;
    private Task? _listenerTask;

    public LocalhostOverlayHostedService(
        LocalhostOverlayOptions options,
        ILiveTelemetrySource liveTelemetrySource,
        TrackMapStore trackMapStore,
        AppSettingsStore settingsStore,
        ILogger<LocalhostOverlayHostedService> logger)
    {
        _options = options;
        _liveTelemetrySource = liveTelemetrySource;
        _trackMapStore = trackMapStore;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        try
        {
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listener = new HttpListener();
            _listener.Prefixes.Add(_options.Prefix);
            _listener.Start();
            _listenerTask = Task.Run(() => ListenAsync(_cancellation.Token), CancellationToken.None);
            _logger.LogInformation("Localhost overlays listening on {Prefix}.", _options.Prefix);
        }
        catch (Exception exception)
        {
            _listener?.Close();
            _listener = null;
            _logger.LogWarning(
                exception,
                "Localhost overlays could not listen on {Prefix}. The app will continue without localhost browser overlays.",
                _options.Prefix);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellation?.Cancel();
        _listener?.Stop();
        _listener?.Close();

        if (_listenerTask is not null)
        {
            try
            {
                await _listenerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpListenerException or ObjectDisposedException)
            {
                // Expected while the host is shutting down the listener.
            }
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener?.IsListening == true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpListenerException or ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            AddCorsHeaders(context.Response);
            if (string.Equals(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                context.Response.Close();
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.MethodNotAllowed, new
                {
                    error = "method_not_allowed"
                }, cancellationToken).ConfigureAwait(false);
                return;
            }

            var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            switch (path)
            {
                case "":
                case "/":
                    await WriteHtmlAsync(
                        context.Response,
                        HttpStatusCode.OK,
                        LocalhostOverlayPageRenderer.RenderIndex(_options.Port),
                        cancellationToken).ConfigureAwait(false);
                    break;

                case "/health":
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        ok = true,
                        service = "tmr-localhost-overlays",
                        routes = LocalhostOverlayPageRenderer.Routes,
                        generatedAtUtc = DateTimeOffset.UtcNow
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                case "/api/snapshot":
                case "/snapshot":
                    var live = _liveTelemetrySource.Snapshot();
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        live
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                case "/api/track-map":
                    var snapshot = _liveTelemetrySource.Snapshot();
                    var trackMapSettings = ReadTrackMapSettings();
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        trackMap = _trackMapStore.TryReadBest(
                            snapshot.Context.Track,
                            includeUserMaps: trackMapSettings.IncludeUserMaps),
                        trackMapSettings = new
                        {
                            internalOpacity = trackMapSettings.InternalOpacity
                        }
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                case "/api/stream-chat":
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        streamChat = StreamChatOverlaySettings.From(_settingsStore.Load())
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    if (LocalhostOverlayPageRenderer.TryRender(path, out var html))
                    {
                        await WriteHtmlAsync(context.Response, HttpStatusCode.OK, html, cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new
                    {
                        error = "not_found"
                    }, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Localhost overlay request failed.");
            try
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new
                {
                    error = "localhost_overlay_error"
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // The client may have disconnected while the service was writing the response.
            }
        }
    }

    private TrackMapBrowserSettings ReadTrackMapSettings()
    {
        try
        {
            var settings = _settingsStore.Load();
            var trackMap = settings.Overlays.FirstOrDefault(
                overlay => string.Equals(overlay.Id, "track-map", StringComparison.OrdinalIgnoreCase));
            return new TrackMapBrowserSettings(
                IncludeUserMaps: trackMap?.GetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, defaultValue: true) ?? true,
                InternalOpacity: Math.Clamp(trackMap?.Opacity ?? 0.88d, 0.2d, 1d));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to read track map settings for localhost map lookup. Defaulting to include user maps.");
            return new TrackMapBrowserSettings(IncludeUserMaps: true, InternalOpacity: 0.88d);
        }
    }

    private static void AddCorsHeaders(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await WriteBytesAsync(response, statusCode, "application/json; charset=utf-8", json, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteHtmlAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string html,
        CancellationToken cancellationToken)
    {
        await WriteBytesAsync(response, statusCode, "text/html; charset=utf-8", html, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteBytesAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string contentType,
        string content,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        response.StatusCode = (int)statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }
}

internal sealed record TrackMapBrowserSettings(bool IncludeUserMaps, double InternalOpacity);
