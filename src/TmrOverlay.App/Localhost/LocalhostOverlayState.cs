namespace TmrOverlay.App.Localhost;

internal sealed class LocalhostOverlayState
{
    private const string StatusDisabled = "disabled";
    private const string StatusFailed = "failed";
    private const string StatusListening = "listening";
    private const string StatusNotStarted = "not_started";
    private const string StatusStarting = "starting";
    private const string StatusStopped = "stopped";

    private readonly LocalhostOverlayOptions _options;
    private readonly object _sync = new();
    private readonly Dictionary<string, long> _routeCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _statusCodeCounts = new(StringComparer.OrdinalIgnoreCase);
    private string _status;
    private DateTimeOffset? _startAttemptedAtUtc;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _stoppedAtUtc;
    private string? _lastError;
    private DateTimeOffset? _lastErrorAtUtc;
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _requestErrorCount;
    private DateTimeOffset? _lastRequestAtUtc;
    private string? _lastRequestMethod;
    private string? _lastRequestPath;
    private string? _lastRequestRoute;
    private int? _lastRequestStatusCode;
    private double? _lastRequestDurationMs;
    private string? _lastRequestError;

    public LocalhostOverlayState(LocalhostOverlayOptions options)
    {
        _options = options;
        _status = options.Enabled ? StatusNotStarted : StatusDisabled;
    }

    public void RecordDisabled()
    {
        lock (_sync)
        {
            _status = StatusDisabled;
            _stoppedAtUtc = null;
            _lastError = null;
            _lastErrorAtUtc = null;
        }
    }

    public void RecordStartAttempted()
    {
        lock (_sync)
        {
            _status = StatusStarting;
            _startAttemptedAtUtc = DateTimeOffset.UtcNow;
            _stoppedAtUtc = null;
            _lastError = null;
            _lastErrorAtUtc = null;
        }
    }

    public void RecordStarted()
    {
        lock (_sync)
        {
            _status = StatusListening;
            _startedAtUtc = DateTimeOffset.UtcNow;
            _stoppedAtUtc = null;
            _lastError = null;
            _lastErrorAtUtc = null;
        }
    }

    public void RecordStartFailed(Exception exception)
    {
        lock (_sync)
        {
            _status = StatusFailed;
            _lastError = exception.Message;
            _lastErrorAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RecordStopped()
    {
        lock (_sync)
        {
            _stoppedAtUtc = DateTimeOffset.UtcNow;
            if (string.Equals(_status, StatusListening, StringComparison.Ordinal)
                || string.Equals(_status, StatusStarting, StringComparison.Ordinal))
            {
                _status = StatusStopped;
            }
        }
    }

    public void RecordRequest(
        string route,
        string method,
        string path,
        int statusCode,
        TimeSpan duration,
        Exception? exception = null)
    {
        var routeKey = string.IsNullOrWhiteSpace(route) ? "unknown" : route.Trim();
        var statusKey = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var failed = statusCode >= 400 || exception is not null;
        lock (_sync)
        {
            _totalRequests++;
            if (failed)
            {
                _failedRequests++;
            }
            else
            {
                _successfulRequests++;
            }

            if (exception is not null)
            {
                _requestErrorCount++;
            }

            _routeCounts[routeKey] = _routeCounts.GetValueOrDefault(routeKey) + 1;
            _statusCodeCounts[statusKey] = _statusCodeCounts.GetValueOrDefault(statusKey) + 1;
            _lastRequestAtUtc = DateTimeOffset.UtcNow;
            _lastRequestMethod = method;
            _lastRequestPath = string.IsNullOrWhiteSpace(path) ? "/" : path;
            _lastRequestRoute = routeKey;
            _lastRequestStatusCode = statusCode;
            _lastRequestDurationMs = Math.Round(duration.TotalMilliseconds, 3);
            _lastRequestError = exception?.Message;
        }
    }

    public LocalhostOverlaySnapshot Snapshot()
    {
        lock (_sync)
        {
            return new LocalhostOverlaySnapshot(
                Enabled: _options.Enabled,
                Port: _options.Port,
                Prefix: _options.Prefix,
                Status: _status,
                StartAttemptedAtUtc: _startAttemptedAtUtc,
                StartedAtUtc: _startedAtUtc,
                StoppedAtUtc: _stoppedAtUtc,
                LastError: _lastError,
                LastErrorAtUtc: _lastErrorAtUtc,
                TotalRequests: _totalRequests,
                SuccessfulRequests: _successfulRequests,
                FailedRequests: _failedRequests,
                RequestErrorCount: _requestErrorCount,
                LastRequestAtUtc: _lastRequestAtUtc,
                LastRequestMethod: _lastRequestMethod,
                LastRequestPath: _lastRequestPath,
                LastRequestRoute: _lastRequestRoute,
                LastRequestStatusCode: _lastRequestStatusCode,
                LastRequestDurationMs: _lastRequestDurationMs,
                LastRequestError: _lastRequestError,
                RouteCounts: CopyCounts(_routeCounts),
                StatusCodeCounts: CopyCounts(_statusCodeCounts));
        }
    }

    private static IReadOnlyDictionary<string, long> CopyCounts(Dictionary<string, long> counts)
    {
        return counts
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed record LocalhostOverlaySnapshot(
    bool Enabled,
    int Port,
    string Prefix,
    string Status,
    DateTimeOffset? StartAttemptedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    string? LastError,
    DateTimeOffset? LastErrorAtUtc,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    long RequestErrorCount,
    DateTimeOffset? LastRequestAtUtc,
    string? LastRequestMethod,
    string? LastRequestPath,
    string? LastRequestRoute,
    int? LastRequestStatusCode,
    double? LastRequestDurationMs,
    string? LastRequestError,
    IReadOnlyDictionary<string, long> RouteCounts,
    IReadOnlyDictionary<string, long> StatusCodeCounts);
