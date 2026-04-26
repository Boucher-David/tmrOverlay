using System.Text.Json;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.Settings;

internal sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _settingsPath;
    private ApplicationSettings? _settings;

    public AppSettingsStore(AppStorageOptions storageOptions)
    {
        _settingsPath = Path.Combine(storageOptions.SettingsRoot, "settings.json");
    }

    public ApplicationSettings Load()
    {
        lock (_sync)
        {
            if (_settings is not null)
            {
                return _settings;
            }

            if (!File.Exists(_settingsPath))
            {
                _settings = new ApplicationSettings();
                SaveCore(_settings);
                return _settings;
            }

            try
            {
                using var stream = File.OpenRead(_settingsPath);
                _settings = JsonSerializer.Deserialize<ApplicationSettings>(stream, JsonOptions) ?? new ApplicationSettings();
            }
            catch
            {
                _settings = new ApplicationSettings();
            }

            return _settings;
        }
    }

    public void Save(ApplicationSettings settings)
    {
        lock (_sync)
        {
            _settings = settings;
            SaveCore(settings);
        }
    }

    private void SaveCore(ApplicationSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
