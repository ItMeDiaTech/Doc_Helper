using System;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Dialogs;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Shared.Models.Configuration;
using Serilog;

namespace Doc_Helper.UI.ViewModels.Dialogs
{
    /// <summary>
    /// ViewModel for the Settings Dialog
    /// </summary>
    public class SettingsDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IConfigurationService _configurationService;
        private readonly ILogger _logger;
        private AppOptions _originalOptions;
        private AppOptions _workingOptions;

        public SettingsDialogViewModel(IConfigurationService configurationService, ILogger logger)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize commands
            SaveCommand = new DelegateCommand(ExecuteSave);
            CancelCommand = new DelegateCommand(ExecuteCancel);
            ResetToDefaultsCommand = new DelegateCommand(ExecuteResetToDefaults);

            // Load current settings
            LoadSettings();
        }

        #region Properties

        // General Settings
        private string _theme = "Auto";
        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        private bool _showProgressDetails = true;
        public bool ShowProgressDetails
        {
            get => _showProgressDetails;
            set => SetProperty(ref _showProgressDetails, value);
        }

        private bool _autoSelectFirstFile = false;
        public bool AutoSelectFirstFile
        {
            get => _autoSelectFirstFile;
            set => SetProperty(ref _autoSelectFirstFile, value);
        }

        private bool _rememberWindowPosition = true;
        public bool RememberWindowPosition
        {
            get => _rememberWindowPosition;
            set => SetProperty(ref _rememberWindowPosition, value);
        }

        private bool _confirmOnExit = true;
        public bool ConfirmOnExit
        {
            get => _confirmOnExit;
            set => SetProperty(ref _confirmOnExit, value);
        }

        private bool _showToolTips = true;
        public bool ShowToolTips
        {
            get => _showToolTips;
            set => SetProperty(ref _showToolTips, value);
        }

        private bool _minimizeToTray = false;
        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set => SetProperty(ref _minimizeToTray, value);
        }

        private bool _showStatusBar = true;
        public bool ShowStatusBar
        {
            get => _showStatusBar;
            set => SetProperty(ref _showStatusBar, value);
        }

        // Processing Settings
        private bool _fixSourceHyperlinks = true;
        public bool FixSourceHyperlinks
        {
            get => _fixSourceHyperlinks;
            set => SetProperty(ref _fixSourceHyperlinks, value);
        }

        private bool _appendContentID = true;
        public bool AppendContentID
        {
            get => _appendContentID;
            set => SetProperty(ref _appendContentID, value);
        }

        private bool _checkTitleChanges = true;
        public bool CheckTitleChanges
        {
            get => _checkTitleChanges;
            set => SetProperty(ref _checkTitleChanges, value);
        }

        private bool _fixTitles = false;
        public bool FixTitles
        {
            get => _fixTitles;
            set => SetProperty(ref _fixTitles, value);
        }

        private bool _fixInternalHyperlink = true;
        public bool FixInternalHyperlink
        {
            get => _fixInternalHyperlink;
            set => SetProperty(ref _fixInternalHyperlink, value);
        }

        private bool _fixDoubleSpaces = true;
        public bool FixDoubleSpaces
        {
            get => _fixDoubleSpaces;
            set => SetProperty(ref _fixDoubleSpaces, value);
        }

        private bool _replaceHyperlink = false;
        public bool ReplaceHyperlink
        {
            get => _replaceHyperlink;
            set => SetProperty(ref _replaceHyperlink, value);
        }

        private bool _replaceText = false;
        public bool ReplaceText
        {
            get => _replaceText;
            set => SetProperty(ref _replaceText, value);
        }

        private bool _openChangelogAfterUpdates = true;
        public bool OpenChangelogAfterUpdates
        {
            get => _openChangelogAfterUpdates;
            set => SetProperty(ref _openChangelogAfterUpdates, value);
        }

        // Backup Settings
        private bool _enableAutoBackup = true;
        public bool EnableAutoBackup
        {
            get => _enableAutoBackup;
            set => SetProperty(ref _enableAutoBackup, value);
        }

        private bool _useCentralizedBackups = true;
        public bool UseCentralizedBackups
        {
            get => _useCentralizedBackups;
            set => SetProperty(ref _useCentralizedBackups, value);
        }

        private bool _enableUndo = true;
        public bool EnableUndo
        {
            get => _enableUndo;
            set => SetProperty(ref _enableUndo, value);
        }

        private bool _autoCleanupBackups = false;
        public bool AutoCleanupBackups
        {
            get => _autoCleanupBackups;
            set => SetProperty(ref _autoCleanupBackups, value);
        }

        private int _backupRetentionDays = 30;
        public int BackupRetentionDays
        {
            get => _backupRetentionDays;
            set => SetProperty(ref _backupRetentionDays, value);
        }

        private string _backupFolderName = "Backups";
        public string BackupFolderName
        {
            get => _backupFolderName;
            set => SetProperty(ref _backupFolderName, value);
        }

        private string _backupRootPath = string.Empty;
        public string BackupRootPath
        {
            get => _backupRootPath;
            set => SetProperty(ref _backupRootPath, value);
        }

        // API Settings
        private string _powerAutomateFlowUrl = string.Empty;
        public string PowerAutomateFlowUrl
        {
            get => _powerAutomateFlowUrl;
            set => SetProperty(ref _powerAutomateFlowUrl, value);
        }

        private string _hyperlinkBaseUrl = string.Empty;
        public string HyperlinkBaseUrl
        {
            get => _hyperlinkBaseUrl;
            set => SetProperty(ref _hyperlinkBaseUrl, value);
        }

        private int _timeoutSeconds = 30;
        public int TimeoutSeconds
        {
            get => _timeoutSeconds;
            set => SetProperty(ref _timeoutSeconds, value);
        }

        // Changelog Settings
        private bool _autoOpenChangelog = true;
        public bool AutoOpenChangelog
        {
            get => _autoOpenChangelog;
            set => SetProperty(ref _autoOpenChangelog, value);
        }

        private bool _saveChangelogToDownloads = true;
        public bool SaveChangelogToDownloads
        {
            get => _saveChangelogToDownloads;
            set => SetProperty(ref _saveChangelogToDownloads, value);
        }

        private bool _showIndividualChangelogs = true;
        public bool ShowIndividualChangelogs
        {
            get => _showIndividualChangelogs;
            set => SetProperty(ref _showIndividualChangelogs, value);
        }

        private bool _enableChangelogExport = true;
        public bool EnableChangelogExport
        {
            get => _enableChangelogExport;
            set => SetProperty(ref _enableChangelogExport, value);
        }

        // Data Settings
        private string _connectionString = "Data Source=BulkEditor.db";
        public string ConnectionString
        {
            get => _connectionString;
            set => SetProperty(ref _connectionString, value);
        }

        private bool _enableDatabaseLogging = false;
        public bool EnableDatabaseLogging
        {
            get => _enableDatabaseLogging;
            set => SetProperty(ref _enableDatabaseLogging, value);
        }

        private bool _enableCompression = true;
        public bool EnableCompression
        {
            get => _enableCompression;
            set => SetProperty(ref _enableCompression, value);
        }

        private bool _autoMigrateDatabase = true;
        public bool AutoMigrateDatabase
        {
            get => _autoMigrateDatabase;
            set => SetProperty(ref _autoMigrateDatabase, value);
        }

        private int _cacheExpirationMinutes = 60;
        public int CacheExpirationMinutes
        {
            get => _cacheExpirationMinutes;
            set => SetProperty(ref _cacheExpirationMinutes, value);
        }

        private int _maxCacheSizeMB = 100;
        public int MaxCacheSizeMB
        {
            get => _maxCacheSizeMB;
            set => SetProperty(ref _maxCacheSizeMB, value);
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }

        #endregion

        #region IDialogAware Implementation

        public string Title => "Settings";

        public DialogCloseListener RequestClose { get; set; } = new();

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            // Cleanup if needed
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // Dialog opened - settings already loaded in constructor
        }

        #endregion

        #region Private Methods

        private void LoadSettings()
        {
            try
            {
                _originalOptions = _configurationService.GetAppOptions();
                _workingOptions = CloneAppOptions(_originalOptions);

                // Map AppOptions to ViewModel properties
                Theme = _workingOptions.Ui.Theme;
                ShowProgressDetails = true; // Custom UI setting
                RememberWindowPosition = _workingOptions.Ui.RememberWindowState;
                ShowToolTips = _workingOptions.Ui.EnableTooltips;

                // Processing settings
                FixSourceHyperlinks = _workingOptions.Ui.FixSourceHyperlinks;
                AppendContentID = _workingOptions.Ui.AppendContentID;
                CheckTitleChanges = _workingOptions.Ui.CheckTitleChanges;
                FixTitles = _workingOptions.Ui.FixTitles;
                FixInternalHyperlink = _workingOptions.Ui.FixInternalHyperlink;
                FixDoubleSpaces = _workingOptions.Ui.FixDoubleSpaces;
                ReplaceHyperlink = _workingOptions.Ui.ReplaceHyperlink;
                ReplaceText = _workingOptions.Ui.ReplaceText;
                OpenChangelogAfterUpdates = _workingOptions.Ui.OpenChangelogAfterUpdates;

                // Backup settings
                EnableAutoBackup = _workingOptions.Processing.EnableAutoBackup;
                UseCentralizedBackups = _workingOptions.Processing.UseCentralizedBackups;
                EnableUndo = _workingOptions.Processing.EnableUndo;
                AutoCleanupBackups = _workingOptions.Processing.AutoCleanupBackups;
                BackupRetentionDays = _workingOptions.Processing.BackupRetentionDays;
                BackupFolderName = _workingOptions.Processing.BackupFolderName;
                BackupRootPath = _workingOptions.Processing.BackupRootPath;

                // API settings
                PowerAutomateFlowUrl = _workingOptions.Api.PowerAutomateFlowUrl;
                TimeoutSeconds = _workingOptions.Api.TimeoutSeconds;

                // Changelog settings
                AutoOpenChangelog = _workingOptions.Ui.AutoOpenChangelog;
                SaveChangelogToDownloads = _workingOptions.Ui.SaveChangelogToDownloads;
                ShowIndividualChangelogs = _workingOptions.Ui.ShowIndividualChangelogs;
                EnableChangelogExport = _workingOptions.Ui.EnableChangelogExport;

                // Data settings
                ConnectionString = _workingOptions.Data.ConnectionString;
                EnableDatabaseLogging = _workingOptions.Data.EnableDatabaseLogging;
                EnableCompression = _workingOptions.Data.EnableCompression;
                AutoMigrateDatabase = _workingOptions.Data.AutoMigrateDatabase;
                CacheExpirationMinutes = _workingOptions.Data.CacheExpirationMinutes;
                MaxCacheSizeMB = _workingOptions.Data.MaxCacheSizeMB;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load settings");
            }
        }

        private void ExecuteSave()
        {
            try
            {
                // Map ViewModel properties back to AppOptions
                _workingOptions.Ui.Theme = Theme;
                _workingOptions.Ui.RememberWindowState = RememberWindowPosition;
                _workingOptions.Ui.EnableTooltips = ShowToolTips;

                // Processing settings
                _workingOptions.Ui.FixSourceHyperlinks = FixSourceHyperlinks;
                _workingOptions.Ui.AppendContentID = AppendContentID;
                _workingOptions.Ui.CheckTitleChanges = CheckTitleChanges;
                _workingOptions.Ui.FixTitles = FixTitles;
                _workingOptions.Ui.FixInternalHyperlink = FixInternalHyperlink;
                _workingOptions.Ui.FixDoubleSpaces = FixDoubleSpaces;
                _workingOptions.Ui.ReplaceHyperlink = ReplaceHyperlink;
                _workingOptions.Ui.ReplaceText = ReplaceText;
                _workingOptions.Ui.OpenChangelogAfterUpdates = OpenChangelogAfterUpdates;

                // Backup settings
                _workingOptions.Processing.EnableAutoBackup = EnableAutoBackup;
                _workingOptions.Processing.UseCentralizedBackups = UseCentralizedBackups;
                _workingOptions.Processing.EnableUndo = EnableUndo;
                _workingOptions.Processing.AutoCleanupBackups = AutoCleanupBackups;
                _workingOptions.Processing.BackupRetentionDays = BackupRetentionDays;
                _workingOptions.Processing.BackupFolderName = BackupFolderName;
                _workingOptions.Processing.BackupRootPath = BackupRootPath;

                // API settings
                _workingOptions.Api.PowerAutomateFlowUrl = PowerAutomateFlowUrl;
                _workingOptions.Api.TimeoutSeconds = TimeoutSeconds;

                // Changelog settings
                _workingOptions.Ui.AutoOpenChangelog = AutoOpenChangelog;
                _workingOptions.Ui.SaveChangelogToDownloads = SaveChangelogToDownloads;
                _workingOptions.Ui.ShowIndividualChangelogs = ShowIndividualChangelogs;
                _workingOptions.Ui.EnableChangelogExport = EnableChangelogExport;

                // Data settings
                _workingOptions.Data.ConnectionString = ConnectionString;
                _workingOptions.Data.EnableDatabaseLogging = EnableDatabaseLogging;
                _workingOptions.Data.EnableCompression = EnableCompression;
                _workingOptions.Data.AutoMigrateDatabase = AutoMigrateDatabase;
                _workingOptions.Data.CacheExpirationMinutes = CacheExpirationMinutes;
                _workingOptions.Data.MaxCacheSizeMB = MaxCacheSizeMB;

                // Save the settings
                _configurationService.UpdateAppOptions(_workingOptions);

                _logger.Information("Settings saved successfully");
                RequestClose.Invoke(new DialogResult(ButtonResult.OK));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save settings");
                // TODO: Show error message to user
            }
        }

        private void ExecuteCancel()
        {
            _logger.Debug("Settings dialog cancelled");
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        private void ExecuteResetToDefaults()
        {
            try
            {
                var defaults = new AppOptions();
                
                // Map defaults to ViewModel properties
                Theme = defaults.Ui.Theme;
                ShowProgressDetails = true;
                RememberWindowPosition = defaults.Ui.RememberWindowState;
                ShowToolTips = defaults.Ui.EnableTooltips;

                // Processing settings
                FixSourceHyperlinks = defaults.Ui.FixSourceHyperlinks;
                AppendContentID = defaults.Ui.AppendContentID;
                CheckTitleChanges = defaults.Ui.CheckTitleChanges;
                FixTitles = defaults.Ui.FixTitles;
                FixInternalHyperlink = defaults.Ui.FixInternalHyperlink;
                FixDoubleSpaces = defaults.Ui.FixDoubleSpaces;
                ReplaceHyperlink = defaults.Ui.ReplaceHyperlink;
                ReplaceText = defaults.Ui.ReplaceText;
                OpenChangelogAfterUpdates = defaults.Ui.OpenChangelogAfterUpdates;

                // Backup settings
                EnableAutoBackup = defaults.Processing.EnableAutoBackup;
                UseCentralizedBackups = defaults.Processing.UseCentralizedBackups;
                EnableUndo = defaults.Processing.EnableUndo;
                AutoCleanupBackups = defaults.Processing.AutoCleanupBackups;
                BackupRetentionDays = defaults.Processing.BackupRetentionDays;
                BackupFolderName = defaults.Processing.BackupFolderName;
                BackupRootPath = defaults.Processing.BackupRootPath;

                // API settings
                PowerAutomateFlowUrl = defaults.Api.PowerAutomateFlowUrl;
                TimeoutSeconds = defaults.Api.TimeoutSeconds;

                // Changelog settings
                AutoOpenChangelog = defaults.Ui.AutoOpenChangelog;
                SaveChangelogToDownloads = defaults.Ui.SaveChangelogToDownloads;
                ShowIndividualChangelogs = defaults.Ui.ShowIndividualChangelogs;
                EnableChangelogExport = defaults.Ui.EnableChangelogExport;

                // Data settings
                ConnectionString = defaults.Data.ConnectionString;
                EnableDatabaseLogging = defaults.Data.EnableDatabaseLogging;
                EnableCompression = defaults.Data.EnableCompression;
                AutoMigrateDatabase = defaults.Data.AutoMigrateDatabase;
                CacheExpirationMinutes = defaults.Data.CacheExpirationMinutes;
                MaxCacheSizeMB = defaults.Data.MaxCacheSizeMB;

                _logger.Information("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reset settings to defaults");
            }
        }

        private AppOptions CloneAppOptions(AppOptions original)
        {
            // Simple cloning - in a real app you might use a more sophisticated approach
            return new AppOptions
            {
                ApplicationName = original.ApplicationName,
                Version = original.Version,
                Environment = original.Environment,
                Api = new ApiOptions
                {
                    PowerAutomateFlowUrl = original.Api.PowerAutomateFlowUrl,
                    TimeoutSeconds = original.Api.TimeoutSeconds,
                    RetryCount = original.Api.RetryCount,
                    RetryDelaySeconds = original.Api.RetryDelaySeconds,
                    MaxBatchSize = original.Api.MaxBatchSize,
                    RateLimitPerMinute = original.Api.RateLimitPerMinute
                },
                Processing = new ProcessingOptions
                {
                    MaxDegreeOfParallelism = original.Processing.MaxDegreeOfParallelism,
                    BufferSize = original.Processing.BufferSize,
                    EnableOptimizations = original.Processing.EnableOptimizations,
                    DefaultFileExtensions = new System.Collections.Generic.List<string>(original.Processing.DefaultFileExtensions),
                    EnableAutoBackup = original.Processing.EnableAutoBackup,
                    BackupPath = original.Processing.BackupPath,
                    UseCentralizedBackups = original.Processing.UseCentralizedBackups,
                    BackupRootPath = original.Processing.BackupRootPath,
                    BackupFolderName = original.Processing.BackupFolderName,
                    BackupRetentionDays = original.Processing.BackupRetentionDays,
                    EnableUndo = original.Processing.EnableUndo,
                    AutoCleanupBackups = original.Processing.AutoCleanupBackups
                },
                Ui = new UiOptions
                {
                    Theme = original.Ui.Theme,
                    ShowSplashScreen = original.Ui.ShowSplashScreen,
                    EnableAnimations = original.Ui.EnableAnimations,
                    RememberWindowState = original.Ui.RememberWindowState,
                    DefaultWindowWidth = original.Ui.DefaultWindowWidth,
                    DefaultWindowHeight = original.Ui.DefaultWindowHeight,
                    EnableTooltips = original.Ui.EnableTooltips,
                    AutoOpenChangelog = original.Ui.AutoOpenChangelog,
                    SaveChangelogToDownloads = original.Ui.SaveChangelogToDownloads,
                    ShowIndividualChangelogs = original.Ui.ShowIndividualChangelogs,
                    EnableChangelogExport = original.Ui.EnableChangelogExport,
                    FixSourceHyperlinks = original.Ui.FixSourceHyperlinks,
                    AppendContentID = original.Ui.AppendContentID,
                    CheckTitleChanges = original.Ui.CheckTitleChanges,
                    FixTitles = original.Ui.FixTitles,
                    FixInternalHyperlink = original.Ui.FixInternalHyperlink,
                    FixDoubleSpaces = original.Ui.FixDoubleSpaces,
                    ReplaceHyperlink = original.Ui.ReplaceHyperlink,
                    ReplaceText = original.Ui.ReplaceText,
                    OpenChangelogAfterUpdates = original.Ui.OpenChangelogAfterUpdates
                },
                Data = new DataOptions
                {
                    ConnectionString = original.Data.ConnectionString,
                    EnableDatabaseLogging = original.Data.EnableDatabaseLogging,
                    CacheExpirationMinutes = original.Data.CacheExpirationMinutes,
                    MaxCacheSizeMB = original.Data.MaxCacheSizeMB,
                    EnableCompression = original.Data.EnableCompression,
                    AutoMigrateDatabase = original.Data.AutoMigrateDatabase
                },
                Update = new UpdateOptions
                {
                    EnableAutoUpdates = original.Update.EnableAutoUpdates,
                    CheckFrequencyHours = original.Update.CheckFrequencyHours,
                    UpdateServerUrl = original.Update.UpdateServerUrl,
                    EnablePreReleaseUpdates = original.Update.EnablePreReleaseUpdates,
                    SilentInstall = original.Update.SilentInstall
                }
            };
        }

        #endregion
    }
}