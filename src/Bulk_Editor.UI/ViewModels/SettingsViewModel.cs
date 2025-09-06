using System;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using Doc_Helper.Shared.Configuration;
using Doc_Helper.UI.Services;

namespace Doc_Helper.UI.ViewModels
{
    /// <summary>
    /// ViewModel for application settings
    /// </summary>
    public class SettingsViewModel : BindableBase
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly SettingsService _settingsService;
        private AppOptions _settings;

        public SettingsViewModel(ILogger<SettingsViewModel> logger, SettingsService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _settings = _settingsService.GetSettings();

            SaveCommand = new DelegateCommand(ExecuteSave);
            CancelCommand = new DelegateCommand(ExecuteCancel);
            ResetToDefaultsCommand = new DelegateCommand(ExecuteResetToDefaults);
        }

        #region Properties

        public AppOptions Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        // UI Options
        public string Theme
        {
            get => _settings.UI.Theme;
            set
            {
                _settings.UI.Theme = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowProgressDetails
        {
            get => _settings.UI.ShowProgressDetails;
            set
            {
                _settings.UI.ShowProgressDetails = value;
                RaisePropertyChanged();
            }
        }

        public bool AutoSelectFirstFile
        {
            get => _settings.UI.AutoSelectFirstFile;
            set
            {
                _settings.UI.AutoSelectFirstFile = value;
                RaisePropertyChanged();
            }
        }

        public bool RememberWindowPosition
        {
            get => _settings.UI.RememberWindowPosition;
            set
            {
                _settings.UI.RememberWindowPosition = value;
                RaisePropertyChanged();
            }
        }

        public bool ConfirmOnExit
        {
            get => _settings.UI.ConfirmOnExit;
            set
            {
                _settings.UI.ConfirmOnExit = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowToolTips
        {
            get => _settings.UI.ShowToolTips;
            set
            {
                _settings.UI.ShowToolTips = value;
                RaisePropertyChanged();
            }
        }

        public bool MinimizeToTray
        {
            get => _settings.UI.MinimizeToTray;
            set
            {
                _settings.UI.MinimizeToTray = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowStatusBar
        {
            get => _settings.UI.ShowStatusBar;
            set
            {
                _settings.UI.ShowStatusBar = value;
                RaisePropertyChanged();
            }
        }

        // Processing Options
        public bool FixSourceHyperlinks
        {
            get => _settings.UI.FixSourceHyperlinks;
            set
            {
                _settings.UI.FixSourceHyperlinks = value;
                RaisePropertyChanged();
            }
        }

        public bool AppendContentID
        {
            get => _settings.UI.AppendContentID;
            set
            {
                _settings.UI.AppendContentID = value;
                RaisePropertyChanged();
            }
        }

        public bool CheckTitleChanges
        {
            get => _settings.UI.CheckTitleChanges;
            set
            {
                _settings.UI.CheckTitleChanges = value;
                RaisePropertyChanged();
            }
        }

        public bool FixTitles
        {
            get => _settings.UI.FixTitles;
            set
            {
                _settings.UI.FixTitles = value;
                RaisePropertyChanged();
            }
        }

        public bool FixInternalHyperlink
        {
            get => _settings.UI.FixInternalHyperlink;
            set
            {
                _settings.UI.FixInternalHyperlink = value;
                RaisePropertyChanged();
            }
        }

        public bool FixDoubleSpaces
        {
            get => _settings.UI.FixDoubleSpaces;
            set
            {
                _settings.UI.FixDoubleSpaces = value;
                RaisePropertyChanged();
            }
        }

        public bool ReplaceHyperlink
        {
            get => _settings.UI.ReplaceHyperlink;
            set
            {
                _settings.UI.ReplaceHyperlink = value;
                RaisePropertyChanged();
            }
        }

        public bool OpenChangelogAfterUpdates
        {
            get => _settings.UI.OpenChangelogAfterUpdates;
            set
            {
                _settings.UI.OpenChangelogAfterUpdates = value;
                RaisePropertyChanged();
            }
        }

        // API Options
        public string PowerAutomateFlowUrl
        {
            get => _settings.Api.PowerAutomateFlowUrl;
            set
            {
                _settings.Api.PowerAutomateFlowUrl = value;
                RaisePropertyChanged();
            }
        }

        public string HyperlinkBaseUrl
        {
            get => _settings.Api.HyperlinkBaseUrl;
            set
            {
                _settings.Api.HyperlinkBaseUrl = value;
                RaisePropertyChanged();
            }
        }

        public int TimeoutSeconds
        {
            get => _settings.Api.TimeoutSeconds;
            set
            {
                _settings.Api.TimeoutSeconds = value;
                RaisePropertyChanged();
            }
        }

        // Data Options
        public string DatabasePath
        {
            get => _settings.Data.DatabasePath;
            set
            {
                _settings.Data.DatabasePath = value;
                RaisePropertyChanged();
            }
        }

        public string ExcelSourcePath
        {
            get => _settings.Data.ExcelSourcePath;
            set
            {
                _settings.Data.ExcelSourcePath = value;
                RaisePropertyChanged();
            }
        }

        public bool EnableAutoSync
        {
            get => _settings.Data.EnableAutoSync;
            set
            {
                _settings.Data.EnableAutoSync = value;
                RaisePropertyChanged();
            }
        }

        public int SyncIntervalMinutes
        {
            get => _settings.Data.SyncIntervalMinutes;
            set
            {
                _settings.Data.SyncIntervalMinutes = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }

        #endregion

        #region Command Handlers

        private async void ExecuteSave()
        {
            try
            {
                await _settingsService.UpdateSettingsAsync(_settings);
                _logger.LogInformation("Settings saved successfully");

                // Close dialog or navigate back
                // This would be handled by the dialog service
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
            }
        }

        private void ExecuteCancel()
        {
            try
            {
                // Reload settings from service to discard changes
                _settings = _settingsService.GetSettings();
                RaisePropertyChanged(nameof(Settings));

                _logger.LogInformation("Settings changes cancelled");

                // Close dialog or navigate back
                // This would be handled by the dialog service
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel settings changes");
            }
        }

        private async void ExecuteResetToDefaults()
        {
            try
            {
                await _settingsService.ResetToDefaultsAsync();
                _settings = _settingsService.GetSettings();
                RaisePropertyChanged(nameof(Settings));

                _logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset settings to defaults");
            }
        }

        #endregion
    }
}