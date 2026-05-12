using Microsoft.Extensions.Hosting;

namespace TmrOverlay.App.Diagnostics;

internal sealed class ForegroundWindowTracker : IHostedService, IDisposable
{
    private const int PollIntervalMilliseconds = 250;
    private const int MaximumHistoryEntries = 120;

    private readonly object _sync = new();
    private readonly List<ForegroundWindowChangeSnapshot> _history = [];
    private System.Threading.Timer? _timer;
    private string? _lastForegroundHwnd;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        RecordForegroundWindow();
        _timer = new System.Threading.Timer(
            _ => RecordForegroundWindow(),
            null,
            PollIntervalMilliseconds,
            PollIntervalMilliseconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public IReadOnlyList<ForegroundWindowChangeSnapshot> SnapshotHistory()
    {
        lock (_sync)
        {
            return _history.ToArray();
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private void RecordForegroundWindow()
    {
        TopLevelWindowSnapshot? window;
        try
        {
            window = WindowsTopLevelWindowDiagnostics.CaptureForegroundWindow();
        }
        catch
        {
            return;
        }

        if (window is null)
        {
            return;
        }

        lock (_sync)
        {
            if (string.Equals(_lastForegroundHwnd, window.Hwnd, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastForegroundHwnd = window.Hwnd;
            _history.Add(new ForegroundWindowChangeSnapshot(
                AtUtc: DateTimeOffset.UtcNow,
                Hwnd: window.Hwnd,
                ProcessId: window.ProcessId,
                ProcessName: window.ProcessName,
                Title: window.Title,
                ClassName: window.ClassName,
                TopMost: window.TopMost,
                ZOrderIndex: window.ZOrderIndex));
            if (_history.Count > MaximumHistoryEntries)
            {
                _history.RemoveRange(0, _history.Count - MaximumHistoryEntries);
            }
        }
    }
}
