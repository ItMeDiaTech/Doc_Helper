using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Microsoft.Extensions.Logging;
using Velopack;

namespace Doc_Helper.Infrastructure.Deployment
{
    /// <summary>
    /// Modern Velopack deployment service for AppData installation
    /// Handles silent auto-updates and AppData deployment strategy
    /// </summary>
    public class VelopackDeploymentService : IDeploymentService
    {
        private readonly ILogger<VelopackDeploymentService> _logger;
        private readonly string _appDataPath;
        private readonly string _updateUrl;
        private UpdateManager? _updateManager;

        public VelopackDeploymentService(ILogger<VelopackDeploymentService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocHelper");
            _updateUrl = "https://your-sharepoint-site.com/updates";
        }

        /// <summary>
        /// Initializes Velopack update manager for AppData deployment
        /// </summary>
        public async Task<DeploymentInitializationResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            var result = new DeploymentInitializationResult { StartTime = DateTime.UtcNow };

            try
            {
                _logger.LogInformation("Initializing Velopack deployment for AppData installation");

                // Ensure AppData directory exists
                Directory.CreateDirectory(_appDataPath);

                // Initialize Velopack UpdateManager
                _updateManager = new UpdateManager(_updateUrl);

                result.Success = true;
                result.EndTime = DateTime.UtcNow;
                result.AppDataPath = _appDataPath;

                _logger.LogInformation("Velopack deployment initialized successfully in AppData: {AppDataPath}", _appDataPath);

                await Task.CompletedTask;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Velopack deployment initialization failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.EndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Checks for application updates silently in background
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var result = new UpdateCheckResult { CheckStartTime = DateTime.UtcNow };

            try
            {
                if (_updateManager is null)
                {
                    await InitializeAsync(cancellationToken);
                    if (_updateManager is null)
                        throw new InvalidOperationException("Update manager could not be initialized.");
                }

                _logger.LogInformation("Checking for application updates...");
                var updateInfo = await _updateManager.CheckForUpdatesAsync();

                if (updateInfo != null)
                {
                    result.UpdatesAvailable = true;
                    result.CurrentVersion = _updateManager.CurrentVersion?.ToString() ?? "N/A";
                    result.LatestVersion = updateInfo.TargetFullRelease?.Version.ToString() ?? "N/A";
                    result.UpdateCount = 1; // Velopack typically provides one update package
                }
                else
                {
                    result.UpdatesAvailable = false;
                    result.CurrentVersion = _updateManager.CurrentVersion?.ToString() ?? "1.0.0";
                    result.LatestVersion = result.CurrentVersion;
                    result.UpdateCount = 0;
                }

                if (result.UpdatesAvailable)
                {
                    _logger.LogInformation("Updates available: from {CurrentVersion} to {LatestVersion}",
                        result.CurrentVersion, result.LatestVersion);
                }
                else
                {
                    _logger.LogDebug("No updates available. Current version: {CurrentVersion}", result.CurrentVersion);
                }

                result.Success = true;
                result.CheckEndTime = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update check failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.CheckEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Performs silent background update without user interaction
        /// </summary>
        public async Task<UpdateResult> PerformSilentUpdateAsync(CancellationToken cancellationToken = default)
        {
            var result = new UpdateResult { UpdateStartTime = DateTime.UtcNow };

            try
            {
                if (_updateManager is null)
                {
                    await InitializeAsync(cancellationToken);
                    if (_updateManager is null)
                        throw new InvalidOperationException("Update manager could not be initialized.");
                }

                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    result.UpdateAction = UpdateAction.NoUpdatesAvailable;
                    result.Success = true;
                    result.UpdateEndTime = DateTime.UtcNow;
                    return result;
                }

                _logger.LogInformation("Applying update...");
                result.VersionBefore = _updateManager.CurrentVersion?.ToString() ?? "N/A";

                // Download and apply the update
                await _updateManager.DownloadUpdatesAsync(updateInfo, progress =>
                {
                    result.DownloadProgress = progress;
                    _logger.LogDebug("Download progress: {Progress}%", progress);
                });

                _updateManager.ApplyUpdatesAndRestart(updateInfo);

                result.VersionAfter = updateInfo.TargetFullRelease?.Version.ToString() ?? "N/A";
                result.UpdateAction = UpdateAction.UpdateApplied;
                result.Success = true;
                result.UpdateEndTime = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Silent update failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.UpdateAction = UpdateAction.UpdateFailed;
                result.UpdateEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Handles initial application installation
        /// </summary>
        private void OnInitialInstall(string version)
        {
            try
            {
                _logger.LogInformation("Performing initial installation for version {Version}", version);

                // Install database to AppData
                InstallDatabaseToAppData();

                _logger.LogInformation("Initial installation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial installation failed");
            }
        }

        /// <summary>
        /// Handles application updates
        /// </summary>
        private void OnAppUpdate(string version)
        {
            try
            {
                _logger.LogInformation("Performing application update to version {Version}", version);

                // Update database if needed
                UpdateDatabaseIfNeeded();

                _logger.LogInformation("Application update completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application update failed");
            }
        }

        /// <summary>
        /// Handles application uninstallation
        /// </summary>
        private void OnAppUninstall(string version)
        {
            try
            {
                _logger.LogInformation("Performing application uninstall for version {Version}", version);

                // Optionally preserve user data in AppData
                // (Don't delete AppData folder to preserve user's database)

                _logger.LogInformation("Application uninstall completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application uninstall failed");
            }
        }

        /// <summary>
        /// Handles first application run after installation
        /// </summary>
        private void OnFirstRun()
        {
            try
            {
                _logger.LogInformation("First application run detected");

                // Verify database installation
                VerifyDatabaseInstallation();

                // Initialize application data
                InitializeApplicationData();

                _logger.LogInformation("First run initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "First run initialization failed");
            }
        }

        /// <summary>
        /// Installs pre-built database to AppData
        /// </summary>
        private void InstallDatabaseToAppData()
        {
            try
            {
                var dataDir = Path.Combine(_appDataPath, "Data");
                Directory.CreateDirectory(dataDir);

                var targetDbPath = Path.Combine(dataDir, "bulkeditor.db");
                var sourceDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "bulkeditor.db");

                if (File.Exists(sourceDbPath))
                {
                    File.Copy(sourceDbPath, targetDbPath, overwrite: true);

                    var dbSize = new FileInfo(targetDbPath).Length / (1024.0 * 1024.0);
                    _logger.LogInformation("Database installed to AppData: {DbPath} ({Size:F2} MB)", targetDbPath, dbSize);
                }
                else
                {
                    _logger.LogWarning("Source database not found: {SourcePath}", sourceDbPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database installation to AppData failed");
            }
        }

        /// <summary>
        /// Updates database if new version is available
        /// </summary>
        private void UpdateDatabaseIfNeeded()
        {
            try
            {
                var dataDir = Path.Combine(_appDataPath, "Data");
                var targetDbPath = Path.Combine(dataDir, "bulkeditor.db");
                var sourceDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "bulkeditor.db");

                // Check if source database is newer
                if (File.Exists(sourceDbPath) && File.Exists(targetDbPath))
                {
                    var sourceTime = File.GetLastWriteTime(sourceDbPath);
                    var targetTime = File.GetLastWriteTime(targetDbPath);

                    if (sourceTime > targetTime)
                    {
                        // Backup existing database
                        var backupPath = Path.Combine(dataDir, $"bulkeditor_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");
                        File.Copy(targetDbPath, backupPath);

                        // Update database
                        File.Copy(sourceDbPath, targetDbPath, overwrite: true);

                        _logger.LogInformation("Database updated from {SourceTime} to {TargetTime}", sourceTime, targetTime);
                    }
                }
                else if (File.Exists(sourceDbPath) && !File.Exists(targetDbPath))
                {
                    // Install database if missing
                    Directory.CreateDirectory(dataDir);
                    File.Copy(sourceDbPath, targetDbPath);
                    _logger.LogInformation("Database installed during update");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database update check failed");
            }
        }

        /// <summary>
        /// Verifies database installation after first run
        /// </summary>
        private void VerifyDatabaseInstallation()
        {
            try
            {
                var dbPath = Path.Combine(_appDataPath, "Data", "bulkeditor.db");

                if (File.Exists(dbPath))
                {
                    var dbSize = new FileInfo(dbPath).Length / (1024.0 * 1024.0);
                    _logger.LogInformation("Database installation verified: {DbPath} ({Size:F2} MB)", dbPath, dbSize);
                }
                else
                {
                    _logger.LogError("Database not found after installation: {DbPath}", dbPath);
                    // Attempt to install database
                    InstallDatabaseToAppData();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database verification failed");
            }
        }

        /// <summary>
        /// Initializes application data on first run
        /// </summary>
        private void InitializeApplicationData()
        {
            try
            {
                // Create necessary directories
                var directories = new[]
                {
                    Path.Combine(_appDataPath, "Logs"),
                    Path.Combine(_appDataPath, "Backup"),
                    Path.Combine(_appDataPath, "Temp"),
                    Path.Combine(_appDataPath, "Cache")
                };

                foreach (var dir in directories)
                {
                    Directory.CreateDirectory(dir);
                }

                // Create initial configuration if needed
                var configPath = Path.Combine(_appDataPath, "appsettings.json");
                if (!File.Exists(configPath))
                {
                    CreateDefaultConfiguration(configPath);
                }

                _logger.LogInformation("Application data initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application data initialization failed");
            }
        }

        /// <summary>
        /// Creates default configuration file
        /// </summary>
        private void CreateDefaultConfiguration(string configPath)
        {
            try
            {
                var defaultConfig = @"{
  ""App"": {
    ""Data"": {
      ""DatabasePath"": ""Data/bulkeditor.db"",
      ""EnableAutoSync"": true,
      ""SyncIntervalMinutes"": 60,
      ""CacheExpiryHours"": 12
    },
    ""Processing"": {
      ""MaxConcurrentFiles"": 5,
      ""CreateBackups"": true,
      ""ValidateDocuments"": true
    },
    ""UI"": {
      ""Theme"": ""System"",
      ""ShowProgressDetails"": true,
      ""AutoSelectFirstFile"": true
    }
  }
}";

                File.WriteAllText(configPath, defaultConfig);
                _logger.LogInformation("Created default configuration: {ConfigPath}", configPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create default configuration");
            }
        }

        /// <summary>
        /// Gets current application version
        /// </summary>
        public async Task<string> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_updateManager is null)
                    await InitializeAsync(cancellationToken);

                return _updateManager?.CurrentVersion?.ToString() ?? "1.0.0";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get current version");
                return "Unknown";
            }
        }

        /// <summary>
        /// Schedules restart after update
        /// </summary>
        public async Task<bool> ScheduleRestartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_updateManager is null)
                    return false;

                _logger.LogInformation("Scheduling application restart after update");

                // Velopack handles restart automatically in ApplyUpdatesAndRestart
                // This method is mainly for compatibility with the interface
                _logger.LogInformation("Restart will be handled by Velopack after update");

                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule restart");
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                // Velopack UpdateManager doesn't implement IDisposable in the current version
                _updateManager = null;
                _logger.LogInformation("VelopackDeploymentService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing VelopackDeploymentService");
            }
        }
    }

    /// <summary>
    /// Deployment service interface
    /// </summary>
    public interface IDeploymentService : IDisposable
    {
        Task<DeploymentInitializationResult> InitializeAsync(CancellationToken cancellationToken = default);
        Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
        Task<UpdateResult> PerformSilentUpdateAsync(CancellationToken cancellationToken = default);
        Task<string> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
        Task<bool> ScheduleRestartAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Deployment initialization result
    /// </summary>
    public class DeploymentInitializationResult
    {
        public bool Success { get; set; }
        public string AppDataPath { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Update check result
    /// </summary>
    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool UpdatesAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public int UpdateCount { get; set; }
        public DateTime CheckStartTime { get; set; }
        public DateTime CheckEndTime { get; set; }
        public TimeSpan CheckDuration => CheckEndTime - CheckStartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Update operation result
    /// </summary>
    public class UpdateResult
    {
        public bool Success { get; set; }
        public UpdateAction UpdateAction { get; set; }
        public string VersionBefore { get; set; } = string.Empty;
        public string VersionAfter { get; set; } = string.Empty;
        public long UpdatedFiles { get; set; }
        public int DownloadProgress { get; set; }
        public DateTime UpdateStartTime { get; set; }
        public DateTime UpdateEndTime { get; set; }
        public TimeSpan UpdateDuration => UpdateEndTime - UpdateStartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public bool RequiresRestart { get; set; } = true;
    }

    /// <summary>
    /// Update action types
    /// </summary>
    public enum UpdateAction
    {
        NoUpdatesAvailable,
        UpdateApplied,
        UpdateFailed,
        UpdateCancelled
    }
}