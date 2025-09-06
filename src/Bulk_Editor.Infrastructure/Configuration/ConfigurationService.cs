using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Shared.Models.Configuration;

namespace Doc_Helper.Infrastructure.Configuration;

/// <summary>
/// Service for managing application configuration with hot reload support
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IOptionsMonitor<AppOptions> _appOptions;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(IOptionsMonitor<AppOptions> appOptions, ILogger<ConfigurationService> logger)
    {
        _appOptions = appOptions;
        _logger = logger;

        // Subscribe to configuration changes
        _appOptions.OnChange(OnConfigurationChanged);
    }

    public T GetOptions<T>() where T : class, new()
    {
        return typeof(T).Name switch
        {
            nameof(ApiOptions) => (T)(object)_appOptions.CurrentValue.Api,
            nameof(ProcessingOptions) => (T)(object)_appOptions.CurrentValue.Processing,
            nameof(UiOptions) => (T)(object)_appOptions.CurrentValue.Ui,
            nameof(DataOptions) => (T)(object)_appOptions.CurrentValue.Data,
            _ => throw new ArgumentException($"Unknown options type: {typeof(T).Name}")
        };
    }

    public AppOptions AppOptions => _appOptions.CurrentValue;
    public ApiOptions ApiOptions => _appOptions.CurrentValue.Api;
    public ProcessingOptions ProcessingOptions => _appOptions.CurrentValue.Processing;
    public UiOptions UiOptions => _appOptions.CurrentValue.Ui;
    public DataOptions DataOptions => _appOptions.CurrentValue.Data;

    private void OnConfigurationChanged(AppOptions newOptions)
    {
        _logger.LogInformation("Configuration changed, new values loaded");
        ConfigurationChanged?.Invoke(newOptions);
    }

    public event Action<AppOptions>? ConfigurationChanged;

    public AppOptions GetAppOptions()
    {
        // Return a copy of current options
        return CloneAppOptions(_appOptions.CurrentValue);
    }

    public void UpdateAppOptions(AppOptions options)
    {
        try
        {
            // In a real implementation, you would save these to a configuration file
            // For now, we'll log the update and raise the configuration changed event
            _logger.LogInformation("Configuration updated");
            ConfigurationChanged?.Invoke(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update configuration");
            throw;
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
}