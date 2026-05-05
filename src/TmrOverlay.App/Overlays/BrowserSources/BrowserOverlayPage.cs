namespace TmrOverlay.App.Overlays.BrowserSources;

internal sealed class BrowserOverlayPage
{
    public BrowserOverlayPage(
        string id,
        string title,
        string canonicalRoute,
        string script,
        bool requiresTelemetry = true,
        bool renderWhenTelemetryUnavailable = false,
        string bodyClass = "",
        int refreshIntervalMilliseconds = 250,
        IReadOnlyList<string>? aliases = null)
    {
        Id = id;
        Title = title;
        CanonicalRoute = NormalizeRoute(canonicalRoute);
        Script = script;
        RequiresTelemetry = requiresTelemetry;
        RenderWhenTelemetryUnavailable = renderWhenTelemetryUnavailable;
        BodyClass = bodyClass;
        RefreshIntervalMilliseconds = refreshIntervalMilliseconds;
        Aliases = aliases?.Select(NormalizeRoute).ToArray() ?? [];
        Routes = [CanonicalRoute, .. Aliases];
    }

    public string Id { get; }

    public string Title { get; }

    public string CanonicalRoute { get; }

    public string Script { get; }

    public bool RequiresTelemetry { get; }

    public bool RenderWhenTelemetryUnavailable { get; }

    public string BodyClass { get; }

    public int RefreshIntervalMilliseconds { get; }

    public IReadOnlyList<string> Aliases { get; }

    public IReadOnlyList<string> Routes { get; }

    public bool MatchesRoute(string route)
    {
        var normalized = NormalizeRoute(route);
        return Routes.Any(candidate => string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeRoute(string route)
    {
        var normalized = string.IsNullOrWhiteSpace(route)
            ? "/"
            : route.Trim().TrimEnd('/').ToLowerInvariant();
        return normalized.Length == 0 ? "/" : normalized;
    }
}
