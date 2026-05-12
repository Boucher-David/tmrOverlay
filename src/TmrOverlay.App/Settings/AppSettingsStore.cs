using System.Text.Json;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.Settings;

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

            var shouldPersistMigratedSettings = true;
            if (!File.Exists(_settingsPath))
            {
                _settings = AppSettingsMigrator.Migrate(new ApplicationSettings());
                SaveCore(_settings);
                return _settings;
            }

            try
            {
                using var stream = File.OpenRead(_settingsPath);
                _settings = JsonSerializer.Deserialize<ApplicationSettings>(stream, JsonOptions);
            }
            catch
            {
                _settings = new ApplicationSettings();
                shouldPersistMigratedSettings = false;
            }

            _settings = AppSettingsMigrator.Migrate(_settings);
            if (shouldPersistMigratedSettings)
            {
                SaveCore(_settings);
            }

            return _settings;
        }
    }

    public void Save(ApplicationSettings settings)
    {
        lock (_sync)
        {
            _settings = AppSettingsMigrator.Migrate(settings);
            SaveCore(_settings);
        }
    }

    private void SaveCore(ApplicationSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
