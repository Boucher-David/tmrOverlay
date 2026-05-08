using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;
using TmrOverlay.App.Overlays.BrowserSources;
using TmrOverlay.App.Overlays.GarageCover;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.App.Overlays.TrackMap;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Settings;
using TmrOverlay.App.TrackMaps;
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
    private readonly LocalhostOverlayState _state;
    private readonly AppEventRecorder _events;
    private readonly AppPerformanceState _performanceState;
    private readonly ILogger<LocalhostOverlayHostedService> _logger;
    private readonly LocalhostSnapshotResponseCache _snapshotResponseCache = new(JsonOptions);
    private CancellationTokenSource? _cancellation;
    private HttpListener? _listener;
    private Task? _listenerTask;

    public LocalhostOverlayHostedService(
        LocalhostOverlayOptions options,
        ILiveTelemetrySource liveTelemetrySource,
        TrackMapStore trackMapStore,
        AppSettingsStore settingsStore,
        LocalhostOverlayState state,
        AppEventRecorder events,
        AppPerformanceState performanceState,
        ILogger<LocalhostOverlayHostedService> logger)
    {
        _options = options;
        _liveTelemetrySource = liveTelemetrySource;
        _trackMapStore = trackMapStore;
        _settingsStore = settingsStore;
        _state = state;
        _events = events;
        _performanceState = performanceState;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _state.RecordDisabled();
            _events.Record("localhost_overlay_disabled", new Dictionary<string, string?>
            {
                ["prefix"] = _options.Prefix,
                ["port"] = _options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
            return Task.CompletedTask;
        }

        _state.RecordStartAttempted();
        try
        {
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listener = new HttpListener();
            _listener.Prefixes.Add(_options.Prefix);
            _listener.Start();
            _listenerTask = Task.Run(() => ListenAsync(_cancellation.Token), CancellationToken.None);
            _state.RecordStarted();
            _events.Record("localhost_overlay_started", new Dictionary<string, string?>
            {
                ["prefix"] = _options.Prefix,
                ["port"] = _options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
            _logger.LogInformation("Localhost overlays listening on {Prefix}.", _options.Prefix);
        }
        catch (Exception exception)
        {
            _listener?.Close();
            _listener = null;
            _state.RecordStartFailed(exception);
            _events.Record("localhost_overlay_start_failed", new Dictionary<string, string?>
            {
                ["prefix"] = _options.Prefix,
                ["port"] = _options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["error"] = exception.Message
            });
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

        _state.RecordStopped();
        if (_options.Enabled)
        {
            _events.Record("localhost_overlay_stopped", new Dictionary<string, string?>
            {
                ["prefix"] = _options.Prefix,
                ["port"] = _options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
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
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var route = "unknown";
        var statusCode = (int)HttpStatusCode.InternalServerError;
        Exception? requestException = null;
        try
        {
            AddCorsHeaders(context.Response);
            if (string.Equals(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                route = "options";
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                statusCode = context.Response.StatusCode;
                context.Response.Close();
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                route = "method_not_allowed";
                await WriteJsonAsync(context.Response, HttpStatusCode.MethodNotAllowed, new
                {
                    error = "method_not_allowed"
                }, cancellationToken).ConfigureAwait(false);
                statusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            switch (path)
            {
                case "":
                case "/":
                    route = "index";
                    await WriteHtmlAsync(
                        context.Response,
                        HttpStatusCode.OK,
                        BrowserOverlayPageRenderer.RenderIndex(_options.Port),
                        cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/health":
                    route = "health";
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        ok = true,
                        service = "tmr-localhost-overlays",
                        routes = BrowserOverlayPageRenderer.Routes,
                        generatedAtUtc = DateTimeOffset.UtcNow
                    }, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/api/snapshot":
                case "/snapshot":
                    route = "snapshot";
                    var live = _liveTelemetrySource.Snapshot();
                    await WriteBytesAsync(
                        context.Response,
                        HttpStatusCode.OK,
                        "application/json; charset=utf-8",
                        _snapshotResponseCache.GetOrCreate(live, DateTimeOffset.UtcNow),
                        cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/api/track-map":
                    route = "track_map";
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
                            internalOpacity = trackMapSettings.InternalOpacity,
                            showSectorBoundaries = trackMapSettings.ShowSectorBoundaries
                        }
                    }, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/api/stream-chat":
                    route = "stream_chat";
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        streamChat = StreamChatOverlaySettings.From(_settingsStore.Load())
                    }, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/api/standings":
                    route = "standings_settings";
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        standingsSettings = ReadStandingsSettings()
                    }, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/api/relative":
                    route = "relative_settings";
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        relativeSettings = ReadRelativeSettings()
                    }, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/api/input-state":
                    route = "input_state_settings";
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        inputStateSettings = ReadInputStateSettings()
                    }, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/api/garage-cover":
                    route = "garage_cover_settings";
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        garageCover = ReadGarageCoverSettings()
                    }, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                case "/api/garage-cover/image":
                    route = "garage_cover_image";
                    var imagePath = ReadGarageCoverImagePath();
                    if (imagePath is null)
                    {
                        await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new
                        {
                            error = "garage_cover_image_not_found"
                        }, cancellationToken).ConfigureAwait(false);
                        statusCode = (int)HttpStatusCode.NotFound;
                        break;
                    }

                    await WriteFileAsync(
                        context.Response,
                        HttpStatusCode.OK,
                        GarageCoverImageContentType(imagePath),
                        imagePath,
                        cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.OK;
                    break;

                default:
                    if (BrowserOverlayPageRenderer.TryRender(path, out var html))
                    {
                        route = "overlay_page";
                        await WriteHtmlAsync(context.Response, HttpStatusCode.OK, html, cancellationToken).ConfigureAwait(false);
                        statusCode = (int)HttpStatusCode.OK;
                        break;
                    }

                    route = "not_found";
                    await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new
                    {
                        error = "not_found"
                    }, cancellationToken).ConfigureAwait(false);
                    statusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }
        catch (Exception exception)
        {
            requestException = exception;
            _logger.LogWarning(exception, "Localhost overlay request failed.");
            _events.Record("localhost_overlay_request_failed", new Dictionary<string, string?>
            {
                ["route"] = route,
                ["method"] = context.Request.HttpMethod,
                ["path"] = context.Request.Url?.AbsolutePath ?? string.Empty,
                ["error"] = exception.Message
            });
            try
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new
                {
                    error = "localhost_overlay_error"
                }, CancellationToken.None).ConfigureAwait(false);
                statusCode = (int)HttpStatusCode.InternalServerError;
            }
            catch
            {
                // The client may have disconnected while the service was writing the response.
            }
        }
        finally
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
            _state.RecordRequest(
                route,
                context.Request.HttpMethod,
                context.Request.Url?.AbsolutePath ?? string.Empty,
                statusCode,
                elapsed,
                requestException);
            _performanceState.RecordOperation(AppPerformanceMetricIds.LocalhostRequest, elapsed, requestException is null && statusCode < 500);
            _performanceState.RecordLocalhostRequest(route, statusCode, elapsed, requestException is null && statusCode < 500);
        }
    }

    private TrackMapBrowserSettings ReadTrackMapSettings()
    {
        try
        {
            return TrackMapBrowserSettings.From(_settingsStore.Load());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to read track map settings for localhost map lookup. Defaulting to include user maps.");
            return TrackMapBrowserSettings.Default;
        }
    }

    private StandingsBrowserSettings ReadStandingsSettings()
    {
        try
        {
            return StandingsBrowserSettings.From(_settingsStore.Load());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to read standings settings for localhost browser source.");
            return StandingsBrowserSettings.Default;
        }
    }

    private RelativeBrowserSettings ReadRelativeSettings()
    {
        try
        {
            return RelativeBrowserSettings.From(_settingsStore.Load());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to read relative settings for localhost browser source.");
            return RelativeBrowserSettings.Default;
        }
    }

    private InputStateBrowserSettings ReadInputStateSettings()
    {
        try
        {
            return InputStateBrowserSettings.From(_settingsStore.Load());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to read input-state settings for localhost browser source.");
            return InputStateBrowserSettings.Default;
        }
    }

    private GarageCoverBrowserSettingsSnapshot ReadGarageCoverSettings()
    {
        try
        {
            return GarageCoverBrowserSettings.From(_settingsStore.Load());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to read garage cover settings for localhost browser source.");
            return new GarageCoverBrowserSettingsSnapshot(
                HasImage: false,
                ImageVersion: null,
                ImageStatus: "settings_unavailable",
                FallbackReason: "settings_unavailable",
                PreviewVisible: false,
                PreviewUntilUtc: null,
                ImageFileName: null,
                ImageExtension: null,
                ImageLength: null,
                ImageLastWriteTimeUtc: null);
        }
    }

    private string? ReadGarageCoverImagePath()
    {
        try
        {
            var settings = _settingsStore.Load();
            var overlay = settings.GetOrAddOverlay(
                GarageCoverOverlayDefinition.Definition.Id,
                GarageCoverOverlayDefinition.Definition.DefaultWidth,
                GarageCoverOverlayDefinition.Definition.DefaultHeight,
                defaultEnabled: false);
            var imagePath = overlay.GetStringOption(TmrOverlay.Core.Overlays.OverlayOptionKeys.GarageCoverImagePath);
            return GarageCoverImageStore.GetSupportedImageInfo(imagePath) is null ? null : imagePath;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to read garage cover image for localhost browser source.");
            return null;
        }
    }

    private static string GarageCoverImageContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "image/png"
        };
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

    private static async Task WriteFileAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string contentType,
        string path,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        await WriteBytesAsync(response, statusCode, contentType, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteBytesAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string contentType,
        string content,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        await WriteBytesAsync(response, statusCode, contentType, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteBytesAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string contentType,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }
}

internal sealed class LocalhostSnapshotResponseCache
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _sync = new();
    private long? _sequence;
    private byte[]? _bytes;

    public LocalhostSnapshotResponseCache(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
    }

    public byte[] GetOrCreate(LiveTelemetrySnapshot live, DateTimeOffset generatedAtUtc)
    {
        lock (_sync)
        {
            if (_sequence == live.Sequence && _bytes is not null)
            {
                return _bytes;
            }

            _sequence = live.Sequence;
            _bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                generatedAtUtc,
                live
            }, _jsonOptions));
            return _bytes;
        }
    }
}
