using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Doc_Helper.Shared.Configuration;

namespace Doc_Helper.UI.Services
{
    /// <summary>
    /// Service for managing application settings
    /// </summary>
    public class SettingsService
    {
        private readonly ILogger<SettingsService> _logger;
        private readonly string _settingsPath;
        private AppOptions _currentSettings;

        public SettingsService(ILogger<SettingsService> logger, AppOptions appOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentSettings = appOptions ?? throw new ArgumentNullException(nameof(appOptions));

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DocHelper",
                "settings.json");
        }

        /// <summary>
        /// Get current settings
        /// </summary>
        public AppOptions GetSettings()
        {
            return _currentSettings;
        }

        /// <summary>
        /// Update settings
        /// </summary>
        public async Task UpdateSettingsAsync(AppOptions settings)
        {
            try
            {
                _currentSettings = settings ?? throw new ArgumentNullException(nameof(settings));

                var directory = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_settingsPath, json);
                _logger.LogInformation("Settings saved to {Path}", _settingsPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings to {Path}", _settingsPath);
                throw;
            }
        }

        /// <summary>
        /// Load settings from file
        /// </summary>
        public async Task<AppOptions> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    _logger.LogInformation("Settings file not found, using defaults");
                    return _currentSettings;
                }

                var json = await File.ReadAllTextAsync(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppOptions>(json);

                if (settings != null)
                {
                    _currentSettings = settings;
                    _logger.LogInformation("Settings loaded from {Path}", _settingsPath);
                }

                return _currentSettings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings from {Path}, using defaults", _settingsPath);
                return _currentSettings;
            }
        }

        /// <summary>
        /// Reset settings to defaults
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            try
            {
                _currentSettings = new AppOptions();
                await UpdateSettingsAsync(_currentSettings);
                _logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset settings to defaults");
                throw;
            }
        }
    }
}