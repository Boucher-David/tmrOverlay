using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Bridge;

internal sealed class OverlayBridgeHostedService : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly OverlayBridgeOptions _options;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger<OverlayBridgeHostedService> _logger;
    private CancellationTokenSource? _cancellation;
    private HttpListener? _listener;
    private Task? _listenerTask;

    public OverlayBridgeHostedService(
        OverlayBridgeOptions options,
        ILiveTelemetrySource liveTelemetrySource,
        ILogger<OverlayBridgeHostedService> logger)
    {
        _options = options;
        _liveTelemetrySource = liveTelemetrySource;
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
            _logger.LogInformation("Overlay bridge listening on {Prefix}.", _options.Prefix);
        }
        catch (Exception exception)
        {
            _listener?.Close();
            _listener = null;
            _logger.LogWarning(
                exception,
                "Overlay bridge could not listen on {Prefix}. The app will continue without the bridge.",
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
                case "/health":
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        ok = true,
                        bridge = "tmr-overlay",
                        generatedAtUtc = DateTimeOffset.UtcNow
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                case "/snapshot":
                    var live = _liveTelemetrySource.Snapshot();
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
                    {
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        live
                    }, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new
                    {
                        error = "not_found"
                    }, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Overlay bridge request failed.");
            try
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new
                {
                    error = "bridge_error"
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // The client may have disconnected while the bridge was writing the response.
            }
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
        var bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }
}
