using System.Text.Json;
using TmrOverlay.Core.Overlays;

namespace TmrOverlay.Core.Settings;

internal static class SharedOverlayContract
{
    public const string DefaultContractRelativePath = "shared/tmr-overlay-contract.json";
    public const string DefaultSchemaRelativePath = "shared/tmr-overlay-contract.schema.json";
    public const string StreamChatProviderNone = "none";
    public const string StreamChatProviderStreamlabs = "streamlabs";
    public const string StreamChatProviderTwitch = "twitch";

    private const int FallbackContractVersion = 1;
    private const int FallbackSettingsVersion = 11;
    private const string FallbackFontFamily = "Segoe UI";
    private const string FallbackUnitSystem = "Metric";
    private const string FallbackTwitchChannel = "techmatesracing";

    private static readonly object Sync = new();
    private static SharedOverlayContractSnapshot _current = CreateFallback();
    private static SharedOverlayContractLoadStatus _loadStatus = new(false, null, null);

    public static SharedOverlayContractSnapshot Current
    {
        get
        {
            lock (Sync)
            {
                return _current;
            }
        }
    }

    public static SharedOverlayContractLoadStatus LoadStatus
    {
        get
        {
            lock (Sync)
            {
                return _loadStatus;
            }
        }
    }

    public static bool TryLoadFromDefaultLocation(out string? error)
    {
        var path = TryFindDefaultContractPath();
        if (path is null)
        {
            error = "Shared contract file was not found.";
            lock (Sync)
            {
                _loadStatus = new(false, null, error);
            }

            return false;
        }

        return TryLoadFromFile(path, out error);
    }

    public static bool TryLoadFromFile(string path, out string? error)
    {
        try
        {
            var snapshot = Parse(File.ReadAllText(path));
            lock (Sync)
            {
                _current = snapshot;
                _loadStatus = new(true, Path.GetFullPath(path), null);
            }

            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            error = exception.Message;
            lock (Sync)
            {
                _loadStatus = new(false, Path.GetFullPath(path), error);
            }

            return false;
        }
    }

    public static string? TryFindDefaultContractPath(string? startDirectory = null)
    {
        var candidateRoots = new[]
        {
            AppContext.BaseDirectory,
            startDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var root in candidateRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            var found = TryFindByWalkingParents(root!);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public static string? TryFindDefaultSchemaPath()
    {
        var contractPath = LoadStatus.Path ?? TryFindDefaultContractPath();
        if (contractPath is null)
        {
            return null;
        }

        var schemaPath = Path.Combine(Path.GetDirectoryName(contractPath)!, Path.GetFileName(DefaultSchemaRelativePath));
        return File.Exists(schemaPath) ? schemaPath : null;
    }

    public static object DiagnosticsSnapshot()
    {
        var current = Current;
        var loadStatus = LoadStatus;
        return new
        {
            ContractVersion = current.ContractVersion,
            SettingsVersion = current.SettingsVersion,
            DefaultFontFamily = current.DefaultFontFamily,
            DefaultUnitSystem = current.DefaultUnitSystem,
            StreamChatDefaultProvider = current.StreamChatDefaultProvider,
            StreamChatDefaultTwitchChannel = current.StreamChatDefaultTwitchChannel,
            DesignV2Colors = current.DesignV2Colors,
            Loaded = loadStatus.Loaded,
            Path = loadStatus.Path,
            Error = loadStatus.Error
        };
    }

    internal static void ResetForTests()
    {
        lock (Sync)
        {
            _current = CreateFallback();
            _loadStatus = new(false, null, null);
        }
    }

    internal static SharedOverlayContractSnapshot Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var fallback = CreateFallback();
        var settings = TryGetObject(root, "settings");
        var general = settings is null ? null : TryGetObject(settings.Value, "general");
        var overlayDefaults = ParseOverlayDefaults(settings);
        var designV2Colors = ParseDesignV2Colors(root);

        return new SharedOverlayContractSnapshot(
            ContractVersion: TryGetInt(root, "contractVersion") ?? fallback.ContractVersion,
            SettingsVersion: settings is null
                ? fallback.SettingsVersion
                : TryGetInt(settings.Value, "settingsVersion") ?? fallback.SettingsVersion,
            DefaultFontFamily: TryGetTrimmedString(general, "fontFamily") ?? fallback.DefaultFontFamily,
            DefaultUnitSystem: NormalizeUnitSystem(TryGetTrimmedString(general, "unitSystem") ?? fallback.DefaultUnitSystem),
            OverlayOptionDefaults: overlayDefaults.Count > 0 ? overlayDefaults : fallback.OverlayOptionDefaults,
            DesignV2Colors: designV2Colors.Count > 0 ? designV2Colors : fallback.DesignV2Colors);
    }

    private static SharedOverlayContractSnapshot CreateFallback()
    {
        var streamChatDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [OverlayOptionKeys.StreamChatProvider] = StreamChatProviderTwitch,
            [OverlayOptionKeys.StreamChatTwitchChannel] = FallbackTwitchChannel,
            [OverlayOptionKeys.StreamChatShowAuthorColor] = "true",
            [OverlayOptionKeys.StreamChatShowBadges] = "true",
            [OverlayOptionKeys.StreamChatShowBits] = "true",
            [OverlayOptionKeys.StreamChatShowFirstMessage] = "true",
            [OverlayOptionKeys.StreamChatShowReplies] = "true",
            [OverlayOptionKeys.StreamChatShowTimestamps] = "true",
            [OverlayOptionKeys.StreamChatShowEmotes] = "true",
            [OverlayOptionKeys.StreamChatShowAlerts] = "true",
            [OverlayOptionKeys.StreamChatShowMessageIds] = "false"
        };
        var overlayDefaults = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["stream-chat"] = streamChatDefaults
        };

        return new SharedOverlayContractSnapshot(
            FallbackContractVersion,
            FallbackSettingsVersion,
            FallbackFontFamily,
            FallbackUnitSystem,
            overlayDefaults,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["backgroundTop"] = "#12051F",
                ["backgroundMid"] = "#0C122A",
                ["backgroundBottom"] = "#030B18",
                ["surface"] = "#090E20F2",
                ["surfaceInset"] = "#0D152CE6",
                ["surfaceRaised"] = "#121F3CEB",
                ["titleBar"] = "#080A1CF8",
                ["border"] = "#28486CD2",
                ["borderMuted"] = "#20365496",
                ["gridLine"] = "#00E8FF3D",
                ["textPrimary"] = "#FFF7FF",
                ["textSecondary"] = "#D0E6FF",
                ["textMuted"] = "#8CAED4",
                ["textDim"] = "#527094",
                ["cyan"] = "#00E8FF",
                ["magenta"] = "#FF2AA7",
                ["amber"] = "#FFD15B",
                ["green"] = "#62FF9F",
                ["orange"] = "#FF7D49",
                ["purple"] = "#7E32FF",
                ["error"] = "#FF6274",
                ["trackInterior"] = "#090E1296",
                ["trackHalo"] = "#FFFFFF52",
                ["trackLine"] = "#DEEAF5",
                ["trackMarkerBorder"] = "#080E12E6",
                ["pitLine"] = "#62C7FFBE",
                ["startFinishBoundary"] = "#FFD15B",
                ["startFinishBoundaryShadow"] = "#05090ED2",
                ["personalBestSector"] = "#50D67C",
                ["bestLapSector"] = "#B65CFF",
                ["flagPole"] = "#D6DCE2E1",
                ["flagPoleShadow"] = "#00000078"
            });
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> ParseOverlayDefaults(JsonElement? settings)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (settings is null
            || TryGetObject(settings.Value, "overlays") is not { } overlays
            || overlays.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var overlayProperty in overlays.EnumerateObject())
        {
            if (overlayProperty.Value.ValueKind != JsonValueKind.Object
                || TryGetObject(overlayProperty.Value, "options") is not { } options
                || options.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var optionDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var optionProperty in options.EnumerateObject())
            {
                if (optionProperty.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(optionProperty.Value.GetString()))
                {
                    optionDefaults[optionProperty.Name] = optionProperty.Value.GetString()!.Trim();
                }
            }

            if (optionDefaults.Count > 0)
            {
                result[overlayProperty.Name] = optionDefaults;
            }
        }

        return result;
    }

    private static Dictionary<string, string> ParseDesignV2Colors(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (TryGetObject(root, "design") is not { } design
            || TryGetObject(design, "v2") is not { } v2
            || TryGetObject(v2, "colors") is not { } colors
            || colors.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in colors.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String
                && IsHexColor(property.Value.GetString()))
            {
                result[property.Name] = property.Value.GetString()!.Trim().ToUpperInvariant();
            }
        }

        return result;
    }

    private static string? TryFindByWalkingParents(string start)
    {
        var directory = Directory.Exists(start)
            ? new DirectoryInfo(start)
            : new FileInfo(start).Directory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, DefaultContractRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static JsonElement? TryGetObject(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var child)
            && child.ValueKind == JsonValueKind.Object
            ? child
            : null;
    }

    private static int? TryGetInt(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var child)
            && child.ValueKind == JsonValueKind.Number
            && child.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static string? TryGetTrimmedString(JsonElement? element, string name)
    {
        return element is { ValueKind: JsonValueKind.Object }
            && element.Value.TryGetProperty(name, out var child)
            && child.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(child.GetString())
            ? child.GetString()!.Trim()
            : null;
    }

    private static string NormalizeUnitSystem(string value)
    {
        return string.Equals(value, "Imperial", StringComparison.OrdinalIgnoreCase) ? "Imperial" : "Metric";
    }

    private static bool IsHexColor(string? value)
    {
        var trimmed = value?.Trim();
        if (trimmed is not { Length: 7 or 9 } || trimmed[0] != '#')
        {
            return false;
        }

        return trimmed.Skip(1).All(Uri.IsHexDigit);
    }
}

internal sealed record SharedOverlayContractSnapshot(
    int ContractVersion,
    int SettingsVersion,
    string DefaultFontFamily,
    string DefaultUnitSystem,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> OverlayOptionDefaults,
    IReadOnlyDictionary<string, string> DesignV2Colors)
{
    public string StreamChatDefaultProvider => OverlayOptionDefault(
        "stream-chat",
        OverlayOptionKeys.StreamChatProvider,
        SharedOverlayContract.StreamChatProviderTwitch);

    public string StreamChatDefaultTwitchChannel => OverlayOptionDefault(
        "stream-chat",
        OverlayOptionKeys.StreamChatTwitchChannel,
        "techmatesracing");

    public string OverlayOptionDefault(string overlayId, string optionKey, string fallback)
    {
        return OverlayOptionDefaults.TryGetValue(overlayId, out var options)
            && options.TryGetValue(optionKey, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    public string DesignV2Color(string colorKey, string fallback)
    {
        return DesignV2Colors.TryGetValue(colorKey, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }
}

internal sealed record SharedOverlayContractLoadStatus(bool Loaded, string? Path, string? Error);
