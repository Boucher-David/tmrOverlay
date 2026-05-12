using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace TmrOverlay.App.Diagnostics;

internal static class WindowsTopLevelWindowDiagnostics
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int GwOwner = 4;
    private const int GwHwndNext = 2;
    private const ulong WsExTopMost = 0x00000008;
    private const int MaxWindows = 4096;

    public static TopLevelWindowDiagnosticsSnapshot Capture(
        IReadOnlyList<ForegroundWindowChangeSnapshot>? foregroundHistory = null)
    {
        var capturedAtUtc = DateTimeOffset.UtcNow;
        if (!OperatingSystem.IsWindows())
        {
            return TopLevelWindowDiagnosticsSnapshot.Unavailable(
                capturedAtUtc,
                "not_windows",
                foregroundHistory: foregroundHistory);
        }

        try
        {
            var windows = new List<TopLevelWindowSnapshot>();
            var visited = new HashSet<IntPtr>();
            var hwnd = GetTopWindow(IntPtr.Zero);
            var zOrderIndex = 0;
            while (hwnd != IntPtr.Zero && windows.Count < MaxWindows && visited.Add(hwnd))
            {
                windows.Add(CaptureWindow(hwnd, zOrderIndex));
                zOrderIndex++;
                hwnd = GetWindow(hwnd, GwHwndNext);
            }

            var foregroundWindow = CaptureForegroundWindow();
            return new TopLevelWindowDiagnosticsSnapshot(
                CapturedAtUtc: capturedAtUtc,
                Available: true,
                UnavailableReason: null,
                Error: null,
                ProcessId: Environment.ProcessId,
                ForegroundWindow: foregroundWindow,
                ForegroundHistory: foregroundHistory ?? [],
                WindowCount: windows.Count,
                Windows: windows);
        }
        catch (Exception exception)
        {
            return TopLevelWindowDiagnosticsSnapshot.Unavailable(
                capturedAtUtc,
                "capture_failed",
                $"{exception.GetType().Name}: {exception.Message}",
                foregroundHistory);
        }
    }

    public static TopLevelWindowSnapshot? CaptureForegroundWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var hwnd = GetForegroundWindow();
        return hwnd == IntPtr.Zero
            ? null
            : CaptureWindow(hwnd, FindZOrderIndex(hwnd));
    }

    private static TopLevelWindowSnapshot CaptureWindow(IntPtr hwnd, int? zOrderIndex)
    {
        var exStyle = unchecked((ulong)GetWindowLongPtrCompat(hwnd, GwlExStyle).ToInt64());
        var style = unchecked((ulong)GetWindowLongPtrCompat(hwnd, GwlStyle).ToInt64());
        _ = GetWindowThreadProcessId(hwnd, out var processId);
        var owner = GetWindow(hwnd, GwOwner);
        return new TopLevelWindowSnapshot(
            ZOrderIndex: zOrderIndex,
            Hwnd: FormatHandle(hwnd),
            OwnerHwnd: owner == IntPtr.Zero ? null : FormatHandle(owner),
            ProcessId: processId == 0 ? null : (int?)processId,
            ProcessName: processId == 0 ? null : TryGetProcessName((int)processId),
            Title: WindowText(hwnd),
            ClassName: ClassName(hwnd),
            Visible: IsWindowVisible(hwnd),
            Minimized: IsIconic(hwnd),
            TopMost: (exStyle & WsExTopMost) != 0,
            Style: FormatHex(style),
            ExStyle: FormatHex(exStyle),
            Bounds: GetWindowRect(hwnd, out var rect)
                ? new TopLevelWindowBounds(
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top)
                : null);
    }

    private static int? FindZOrderIndex(IntPtr target)
    {
        var hwnd = GetTopWindow(IntPtr.Zero);
        for (var index = 0; hwnd != IntPtr.Zero && index < MaxWindows; index++)
        {
            if (hwnd == target)
            {
                return index;
            }

            hwnd = GetWindow(hwnd, GwHwndNext);
        }

        return null;
    }

    private static string? TryGetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static string WindowText(IntPtr hwnd)
    {
        var length = Math.Clamp(GetWindowTextLength(hwnd), 0, 8192);
        if (length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        return GetWindowText(hwnd, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }

    private static string ClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        return GetClassName(hwnd, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }

    private static string FormatHandle(IntPtr handle)
    {
        return $"0x{unchecked((ulong)handle.ToInt64()).ToString("X16", CultureInfo.InvariantCulture)}";
    }

    private static string FormatHex(ulong value)
    {
        return $"0x{value.ToString("X16", CultureInfo.InvariantCulture)}";
    }

    private static IntPtr GetWindowLongPtrCompat(IntPtr hwnd, int index)
    {
        return Environment.Is64BitProcess
            ? GetWindowLongPtr64(hwnd, index)
            : new IntPtr(GetWindowLong32(hwnd, index));
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetTopWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}

internal sealed record TopLevelWindowDiagnosticsSnapshot(
    DateTimeOffset CapturedAtUtc,
    bool Available,
    string? UnavailableReason,
    string? Error,
    int ProcessId,
    TopLevelWindowSnapshot? ForegroundWindow,
    IReadOnlyList<ForegroundWindowChangeSnapshot> ForegroundHistory,
    int WindowCount,
    IReadOnlyList<TopLevelWindowSnapshot> Windows)
{
    public static TopLevelWindowDiagnosticsSnapshot Unavailable(
        DateTimeOffset capturedAtUtc,
        string reason,
        string? error = null,
        IReadOnlyList<ForegroundWindowChangeSnapshot>? foregroundHistory = null)
    {
        return new TopLevelWindowDiagnosticsSnapshot(
            CapturedAtUtc: capturedAtUtc,
            Available: false,
            UnavailableReason: reason,
            Error: error,
            ProcessId: Environment.ProcessId,
            ForegroundWindow: null,
            ForegroundHistory: foregroundHistory ?? [],
            WindowCount: 0,
            Windows: []);
    }
}

internal sealed record TopLevelWindowSnapshot(
    int? ZOrderIndex,
    string Hwnd,
    string? OwnerHwnd,
    int? ProcessId,
    string? ProcessName,
    string Title,
    string ClassName,
    bool Visible,
    bool Minimized,
    bool TopMost,
    string Style,
    string ExStyle,
    TopLevelWindowBounds? Bounds);

internal sealed record TopLevelWindowBounds(
    int X,
    int Y,
    int Width,
    int Height);

internal sealed record ForegroundWindowChangeSnapshot(
    DateTimeOffset AtUtc,
    string Hwnd,
    int? ProcessId,
    string? ProcessName,
    string Title,
    string ClassName,
    bool TopMost,
    int? ZOrderIndex);
