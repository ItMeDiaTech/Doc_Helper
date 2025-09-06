using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Infrastructure.Deployment;
using Doc_Helper.Shared.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doc_Helper.Infrastructure.Services
{
    /// <summary>
    /// Background service for silent application updates
    /// Monitors for new versions and applies updates transparently
    /// Integrates with Velopack for seamless AppData deployment
    /// </summary>
    public class BackgroundUpdateService : BackgroundService
    {
        private readonly ILogger<BackgroundUpdateService> _logger;
        private readonly IDeploymentService _deploymentService;
        private readonly ICacheService _cacheService;
        private readonly AppOptions _appOptions;
        private readonly Timer _updateCheckTimer;

        private DateTime _lastUpdateCheck = DateTime.MinValue;
        private DateTime _lastSuccessfulUpdate = DateTime.MinValue;
        private string _currentVersion = "Unknown";
        private bool _updateAvailable;

        public BackgroundUpdateService(
            ILogger<BackgroundUpdateService> logger,
            IDeploymentService deploymentService,
            ICacheService cacheService,
            IOptions<AppOptions> appOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _appOptions = appOptions?.Value ?? throw new ArgumentNullException(nameof(appOptions));

            // Initialize update check timer (check every 4 hours)
            _updateCheckTimer = new Timer(OnUpdateCheckTimerElapsed, null, TimeSpan.Zero, TimeSpan.FromHours(4));
        }

        /// <summary>
        /// Main background service execution loop
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background update service started");

            try
            {
                // Get current version on startup
                _currentVersion = await _deploymentService.GetCurrentVersionAsync(stoppingToken);
                _logger.LogInformation("Current application version: {Version}", _currentVersion);

                // Perform initial update check after startup delay
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); // Wait for app to fully load
                await PerformUpdateCheckAsync(stoppingToken);

                // Continue monitoring for updates
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Main loop - check every hour for immediate updates if needed
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

                        // Only check if it's been more than 4 hours since last check
                        if (DateTime.UtcNow - _lastUpdateCheck > TimeSpan.FromHours(4))
                        {
                            await PerformUpdateCheckAsync(stoppingToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break; // Service is stopping
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background update loop");
                        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Wait before retrying
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background update service failed");
            }
            finally
            {
                _logger.LogInformation("Background update service stopped");
            }
        }

        /// <summary>
        /// Performs update check and applies updates if available
        /// </summary>
        private async Task PerformUpdateCheckAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Checking for application updates");
                _lastUpdateCheck = DateTime.UtcNow;

                var updateCheckResult = await _deploymentService.CheckForUpdatesAsync(cancellationToken);

                if (!updateCheckResult.Success)
                {
                    _logger.LogWarning("Update check failed: {Error}", updateCheckResult.ErrorMessage);
                    return;
                }

                _updateAvailable = updateCheckResult.UpdatesAvailable;

                if (updateCheckResult.UpdatesAvailable)
                {
                    _logger.LogInformation("Updates available: {UpdateCount} releases, {CurrentVersion} → {LatestVersion}",
                        updateCheckResult.UpdateCount, updateCheckResult.CurrentVersion, updateCheckResult.LatestVersion);

                    // Cache update availability for UI
                    await _cacheService.SetAsync("update_available", updateCheckResult, TimeSpan.FromHours(1), cancellationToken);

                    // Perform silent update during off-hours or when appropriate
                    if (ShouldPerformSilentUpdate())
                    {
                        await PerformSilentUpdateAsync(cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("Update available but deferring to optimal time");
                        // Could notify user or schedule for later
                    }
                }
                else
                {
                    _logger.LogDebug("No updates available. Current version {Version} is latest", updateCheckResult.CurrentVersion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update check operation failed");
            }
        }

        /// <summary>
        /// Performs silent update without user interaction
        /// </summary>
        private async Task PerformSilentUpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting silent application update");

                var updateResult = await _deploymentService.PerformSilentUpdateAsync(cancellationToken);

                if (updateResult.Success)
                {
                    _lastSuccessfulUpdate = DateTime.UtcNow;
                    _currentVersion = updateResult.VersionAfter;

                    // Cache successful update info
                    await _cacheService.SetAsync("last_successful_update", _lastSuccessfulUpdate, TimeSpan.FromDays(30), cancellationToken);
                    await _cacheService.SetAsync("current_version", _currentVersion, TimeSpan.FromDays(30), cancellationToken);

                    _logger.LogInformation("Silent update completed successfully: {VersionBefore} → {VersionAfter}",
                        updateResult.VersionBefore, updateResult.VersionAfter);

                    // Schedule restart if required (typically for major updates)
                    if (updateResult.RequiresRestart)
                    {
                        _logger.LogInformation("Update requires restart - scheduling for next application start");
                        await _cacheService.SetAsync("restart_required", true, TimeSpan.FromDays(1), cancellationToken);
                    }
                }
                else
                {
                    _logger.LogWarning("Silent update failed: {Error}", updateResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Silent update operation failed");
            }
        }

        /// <summary>
        /// Determines if silent update should be performed now
        /// </summary>
        private bool ShouldPerformSilentUpdate()
        {
            try
            {
                var now = DateTime.Now;

                // Perform updates during off-hours (late night/early morning)
                var isOffHours = now.Hour >= 23 || now.Hour <= 5;

                // Don't update if recently updated (within 24 hours)
                var recentlyUpdated = DateTime.UtcNow - _lastSuccessfulUpdate < TimeSpan.FromHours(24);

                // Don't update on weekends during business hours
                var isWeekendBusinessHours = (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday) &&
                                           now.Hour >= 9 && now.Hour <= 17;

                var shouldUpdate = isOffHours && !recentlyUpdated && !isWeekendBusinessHours;

                _logger.LogDebug("Silent update decision: ShouldUpdate={ShouldUpdate}, OffHours={OffHours}, RecentlyUpdated={RecentlyUpdated}",
                    shouldUpdate, isOffHours, recentlyUpdated);

                return shouldUpdate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error determining update timing");
                return false; // Be conservative
            }
        }

        /// <summary>
        /// Timer callback for periodic update checks
        /// </summary>
        private async void OnUpdateCheckTimerElapsed(object? state)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                await PerformUpdateCheckAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Timer-based update check failed");
            }
        }

        /// <summary>
        /// Forces an immediate update check
        /// </summary>
        public async Task<UpdateCheckResult> ForceUpdateCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Force update check requested");
                return await _deploymentService.CheckForUpdatesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Force update check failed");
                return new UpdateCheckResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Forces an immediate update if available
        /// </summary>
        public async Task<UpdateResult> ForceUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Force update requested");
                return await _deploymentService.PerformSilentUpdateAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Force update failed");
                return new UpdateResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    UpdateAction = UpdateAction.UpdateFailed
                };
            }
        }

        /// <summary>
        /// Gets current update status
        /// </summary>
        public async Task<UpdateStatus> GetUpdateStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var status = new UpdateStatus
                {
                    CurrentVersion = _currentVersion,
                    LastUpdateCheck = _lastUpdateCheck,
                    LastSuccessfulUpdate = _lastSuccessfulUpdate,
                    UpdateAvailable = _updateAvailable,
                    StatusCheckTime = DateTime.UtcNow
                };

                // Get cached update info
                var cachedUpdateInfo = await _cacheService.GetAsync<UpdateCheckResult>("update_available", cancellationToken);
                if (cachedUpdateInfo != null)
                {
                    status.LatestVersion = cachedUpdateInfo.LatestVersion;
                    status.UpdateCount = cachedUpdateInfo.UpdateCount;
                }

                // Check if restart is required
                var restartRequired = await _cacheService.GetAsync<bool?>("restart_required", cancellationToken) ?? false;
                status.RestartRequired = restartRequired;

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get update status");
                return new UpdateStatus
                {
                    CurrentVersion = _currentVersion,
                    StatusCheckTime = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Schedules application restart for pending updates
        /// </summary>
        public async Task<bool> ScheduleRestartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Scheduling application restart for pending updates");

                var success = await _deploymentService.ScheduleRestartAsync(cancellationToken);

                if (success)
                {
                    await _cacheService.SetAsync("restart_scheduled", DateTime.UtcNow, TimeSpan.FromDays(1), cancellationToken);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule restart");
                return false;
            }
        }

        public override void Dispose()
        {
            try
            {
                _updateCheckTimer?.Dispose();
                _logger.LogInformation("BackgroundUpdateService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing BackgroundUpdateService");
            }

            base.Dispose();
        }
    }

    /// <summary>
    /// Current update status information
    /// </summary>
    public class UpdateStatus
    {
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public bool UpdateAvailable { get; set; }
        public int UpdateCount { get; set; }
        public DateTime LastUpdateCheck { get; set; }
        public DateTime LastSuccessfulUpdate { get; set; }
        public bool RestartRequired { get; set; }
        public DateTime StatusCheckTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Time since last update check
        /// </summary>
        public TimeSpan TimeSinceLastCheck => StatusCheckTime - LastUpdateCheck;

        /// <summary>
        /// Time since last successful update
        /// </summary>
        public TimeSpan TimeSinceLastUpdate => StatusCheckTime - LastSuccessfulUpdate;

        /// <summary>
        /// Indicates if update check is overdue
        /// </summary>
        public bool IsUpdateCheckOverdue => TimeSinceLastCheck > TimeSpan.FromHours(8);

        /// <summary>
        /// User-friendly status message
        /// </summary>
        public string StatusMessage
        {
            get
            {
                if (!string.IsNullOrEmpty(ErrorMessage))
                    return $"Update check failed: {ErrorMessage}";

                if (RestartRequired)
                    return "Update installed - restart recommended";

                if (UpdateAvailable)
                    return $"Update available: v{LatestVersion}";

                if (LastUpdateCheck == DateTime.MinValue)
                    return "Checking for updates...";

                return "Application is up to date";
            }
        }
    }

    /// <summary>
    /// Update notification configuration
    /// </summary>
    public class UpdateNotificationConfig
    {
        /// <summary>
        /// Enable automatic update checks
        /// </summary>
        public bool EnableAutoCheck { get; set; } = true;

        /// <summary>
        /// Enable silent updates
        /// </summary>
        public bool EnableSilentUpdates { get; set; } = true;

        /// <summary>
        /// Notify user of available updates
        /// </summary>
        public bool NotifyUserOfUpdates { get; set; } = false; // Silent by default

        /// <summary>
        /// Hours between update checks
        /// </summary>
        public int CheckIntervalHours { get; set; } = 4;

        /// <summary>
        /// Preferred update time (24-hour format)
        /// </summary>
        public int PreferredUpdateHour { get; set; } = 2; // 2 AM

        /// <summary>
        /// Maximum update age before forcing update
        /// </summary>
        public int MaxUpdateAgeDays { get; set; } = 30;

        /// <summary>
        /// Enable update rollback capability
        /// </summary>
        public bool EnableRollback { get; set; } = true;
    }

    /// <summary>
    /// Update notification for user interaction
    /// </summary>
    public class UpdateNotification
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string NewVersion { get; set; } = string.Empty;
        public DateTime NotificationTime { get; set; } = DateTime.UtcNow;
        public bool IsOptional { get; set; } = true;
        public bool IsSilent { get; set; } = true;
        public UpdatePriority Priority { get; set; } = UpdatePriority.Normal;
        public List<string> UpdateFeatures { get; set; } = new();
    }

    /// <summary>
    /// Update priority levels
    /// </summary>
    public enum UpdatePriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}