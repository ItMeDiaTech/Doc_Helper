using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Shared.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doc_Helper.Infrastructure.Services
{
    /// <summary>
    /// Background service for silent Excel-to-SQLite synchronization
    /// Monitors SharePoint/OneDrive for Excel file changes and syncs automatically
    /// Implements "local DB first, API fallback" strategy
    /// </summary>
    public class BackgroundSyncService : BackgroundService
    {
        private readonly ILogger<BackgroundSyncService> _logger;
        private readonly IExcelToSqliteMigrator _migrator;
        private readonly ICacheService _cacheService;
        private readonly AppOptions _appOptions;
        private readonly HttpClient _httpClient;
        private readonly Timer _syncTimer;

        private DateTime _lastSyncCheck = DateTime.MinValue;
        private DateTime _lastSuccessfulSync = DateTime.MinValue;
        private bool _isFirstStartupOfDay = true;

        public BackgroundSyncService(
            ILogger<BackgroundSyncService> logger,
            IExcelToSqliteMigrator migrator,
            ICacheService cacheService,
            IOptions<AppOptions> appOptions,
            HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _appOptions = appOptions?.Value ?? throw new ArgumentNullException(nameof(appOptions));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Initialize sync timer for daily checks
            _syncTimer = new Timer(OnSyncTimerElapsed, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Main background service execution loop
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background sync service started");

            try
            {
                // Perform initial sync check on startup
                await PerformStartupSyncCheckAsync(stoppingToken);

                // Continue running until service is stopped
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); // Check every 15 minutes
                        await CheckForSyncNeedAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when service is stopping
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background sync loop");
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait before retrying
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync service failed");
            }
            finally
            {
                _logger.LogInformation("Background sync service stopped");
            }
        }

        /// <summary>
        /// Performs sync check on application startup
        /// </summary>
        private async Task PerformStartupSyncCheckAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Performing startup sync check");

                // Check if this is the first startup of the day
                var lastSyncDate = await _cacheService.GetAsync<DateTime?>("last_sync_date", cancellationToken) ?? DateTime.MinValue;
                _isFirstStartupOfDay = lastSyncDate.Date < DateTime.UtcNow.Date;

                if (_isFirstStartupOfDay)
                {
                    _logger.LogInformation("First startup of the day, checking for data updates");
                    await PerformSilentSyncAsync(cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Already synced today, skipping startup sync");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Startup sync check failed, continuing with cached data");
            }
        }

        /// <summary>
        /// Checks if synchronization is needed based on schedule and file changes
        /// </summary>
        private async Task CheckForSyncNeedAsync(CancellationToken cancellationToken)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Check daily sync requirement
                if (_lastSuccessfulSync.Date < now.Date)
                {
                    _logger.LogInformation("Daily sync required");
                    await PerformSilentSyncAsync(cancellationToken);
                    return;
                }

                // Check interval-based sync
                var timeSinceLastCheck = now - _lastSyncCheck;
                if (timeSinceLastCheck >= TimeSpan.FromHours(_appOptions.Data.SyncIntervalMinutes / 60.0))
                {
                    _logger.LogDebug("Checking for SharePoint/OneDrive file changes");

                    var hasChanges = await CheckSharePointFileChangesAsync(cancellationToken);
                    if (hasChanges)
                    {
                        _logger.LogInformation("SharePoint/OneDrive file changes detected, syncing");
                        await PerformSilentSyncAsync(cancellationToken);
                    }

                    _lastSyncCheck = now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking sync need");
            }
        }

        /// <summary>
        /// Performs silent synchronization without user interaction
        /// </summary>
        private async Task PerformSilentSyncAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting silent background sync");

                // Download latest Excel file from SharePoint/OneDrive
                var downloadResult = await DownloadLatestExcelFileAsync(cancellationToken);
                if (!downloadResult.Success)
                {
                    _logger.LogWarning("Failed to download Excel file: {Error}", downloadResult.ErrorMessage);
                    return;
                }

                // Perform silent sync to SQLite
                var syncResult = await _migrator.SynchronizeDataAsync(
                    downloadResult.LocalFilePath,
                    _lastSuccessfulSync,
                    new Progress<MigrationProgress>(progress =>
                        _logger.LogDebug("Sync progress: {Stage} - {Message}", progress.Stage, progress.Message)),
                    cancellationToken);

                if (syncResult.Success)
                {
                    _lastSuccessfulSync = DateTime.UtcNow;
                    await _cacheService.SetAsync("last_sync_date", _lastSuccessfulSync, TimeSpan.FromDays(7), cancellationToken);

                    _logger.LogInformation("Silent sync completed successfully: {Changes} changes applied",
                        syncResult.TotalChanges);

                    // Invalidate relevant caches after successful sync
                    await InvalidateDataCachesAsync(cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Silent sync failed: {Error}", syncResult.ErrorMessage);
                }

                // Clean up downloaded file if temporary
                if (downloadResult.IsTemporary && File.Exists(downloadResult.LocalFilePath))
                {
                    try
                    {
                        File.Delete(downloadResult.LocalFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not delete temporary file: {FilePath}", downloadResult.LocalFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Silent sync operation failed");
            }
        }

        /// <summary>
        /// Checks SharePoint/OneDrive for file modifications
        /// </summary>
        private async Task<bool> CheckSharePointFileChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(_appOptions.Data.ExcelSourcePath))
                {
                    _logger.LogDebug("SharePoint/OneDrive path not configured, skipping change check");
                    return false;
                }

                // Get cached last modified time
                var cachedModifiedTime = await _cacheService.GetAsync<DateTime?>("excel_last_modified", cancellationToken);

                // Check current modified time from SharePoint/OneDrive
                var currentModifiedTime = await GetSharePointFileModifiedTimeAsync(_appOptions.Data.ExcelSourcePath, cancellationToken);

                if (currentModifiedTime.HasValue)
                {
                    var hasChanges = !cachedModifiedTime.HasValue || currentModifiedTime.Value > cachedModifiedTime.Value;

                    if (hasChanges)
                    {
                        // Cache the new modified time
                        await _cacheService.SetAsync("excel_last_modified", currentModifiedTime.Value, TimeSpan.FromDays(7), cancellationToken);

                        _logger.LogInformation("SharePoint file modified: {ModifiedTime} (cached: {CachedTime})",
                            currentModifiedTime.Value, cachedModifiedTime);
                    }

                    return hasChanges;
                }

                return false; // Could not determine, assume no changes
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check SharePoint file changes");
                return false;
            }
        }

        /// <summary>
        /// Downloads the latest Excel file from SharePoint/OneDrive
        /// </summary>
        private async Task<FileDownloadResult> DownloadLatestExcelFileAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(_appOptions.Data.ExcelSourcePath))
                {
                    return new FileDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "SharePoint/OneDrive path not configured"
                    };
                }

                // Create temp directory for download
                var tempDir = Path.Combine(Path.GetTempPath(), "DocHelper", "Downloads");
                Directory.CreateDirectory(tempDir);

                var tempFileName = $"dictionary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                var tempFilePath = Path.Combine(tempDir, tempFileName);

                _logger.LogInformation("Downloading Excel file from {SourcePath} to {TempPath}",
                    _appOptions.Data.ExcelSourcePath, tempFilePath);

                // Download file from SharePoint/OneDrive
                using var response = await _httpClient.GetAsync(_appOptions.Data.ExcelSourcePath, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var fileStream = File.Create(tempFilePath);
                await response.Content.CopyToAsync(fileStream, cancellationToken);

                var fileInfo = new FileInfo(tempFilePath);

                _logger.LogInformation("Downloaded Excel file: {FilePath} ({Size:F2} MB)",
                    tempFilePath, fileInfo.Length / (1024.0 * 1024.0));

                return new FileDownloadResult
                {
                    Success = true,
                    LocalFilePath = tempFilePath,
                    IsTemporary = true,
                    FileSizeBytes = fileInfo.Length,
                    DownloadTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download Excel file from SharePoint/OneDrive");
                return new FileDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Gets file modified time from SharePoint/OneDrive
        /// </summary>
        private async Task<DateTime?> GetSharePointFileModifiedTimeAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                // For SharePoint/OneDrive, we can use HEAD request to get Last-Modified header
                using var request = new HttpRequestMessage(HttpMethod.Head, filePath);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.IsSuccessStatusCode && response.Content.Headers.LastModified.HasValue)
                {
                    return response.Content.Headers.LastModified.Value.DateTime;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not get file modified time from SharePoint/OneDrive");
                return null;
            }
        }

        /// <summary>
        /// Invalidates data caches after successful sync
        /// </summary>
        private async Task InvalidateDataCachesAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Invalidate hyperlink-related caches
                await _cacheService.InvalidatePatternAsync("hyperlinks_*", cancellationToken);
                await _cacheService.InvalidatePatternAsync("lookup_*", cancellationToken);
                await _cacheService.InvalidatePatternAsync("document_*", cancellationToken);

                _logger.LogDebug("Data caches invalidated after successful sync");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not invalidate data caches");
            }
        }

        /// <summary>
        /// Timer callback for periodic sync checks
        /// </summary>
        private async void OnSyncTimerElapsed(object state)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await CheckForSyncNeedAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Timer-based sync check failed");
            }
        }

        /// <summary>
        /// Forces an immediate sync operation
        /// </summary>
        public async Task<SyncResult> ForceSyncAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Force sync requested");
                await PerformSilentSyncAsync(cancellationToken);

                return new SyncResult
                {
                    Success = true,
                    SyncAction = SyncAction.Updated,
                    SyncStartTime = DateTime.UtcNow,
                    SyncEndTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Force sync failed");
                return new SyncResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    SyncStartTime = DateTime.UtcNow,
                    SyncEndTime = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Gets synchronization status and statistics
        /// </summary>
        public async Task<SyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var status = new SyncStatus
                {
                    LastSyncTime = _lastSuccessfulSync,
                    LastCheckTime = _lastSyncCheck,
                    IsFirstStartupOfDay = _isFirstStartupOfDay,
                    NextScheduledSync = GetNextScheduledSyncTime(),
                    SyncEnabled = _appOptions.Data.EnableAutoSync,
                    StatusCheckTime = DateTime.UtcNow
                };

                // Get cached sync statistics
                var syncStats = await _cacheService.GetAsync<SyncStatistics>("sync_statistics", cancellationToken);
                if (syncStats != null)
                {
                    status.TotalSyncsToday = syncStats.SyncsToday;
                    status.TotalChangesToday = syncStats.ChangesToday;
                    status.LastSyncDuration = syncStats.LastSyncDuration;
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get sync status");
                return new SyncStatus
                {
                    StatusCheckTime = DateTime.UtcNow,
                    SyncEnabled = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Calculates next scheduled sync time
        /// </summary>
        private DateTime GetNextScheduledSyncTime()
        {
            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            var syncHour = 6; // 6 AM UTC for daily sync
            return tomorrow.AddHours(syncHour);
        }

        /// <summary>
        /// Updates sync statistics in cache
        /// </summary>
        private async Task UpdateSyncStatisticsAsync(SyncResult syncResult, CancellationToken cancellationToken)
        {
            try
            {
                var stats = await _cacheService.GetAsync<SyncStatistics>("sync_statistics", cancellationToken)
                           ?? new SyncStatistics();

                // Reset daily counters if it's a new day
                if (stats.LastSyncDate.Date < DateTime.UtcNow.Date)
                {
                    stats.SyncsToday = 0;
                    stats.ChangesToday = 0;
                }

                stats.SyncsToday++;
                stats.ChangesToday += syncResult.TotalChanges;
                stats.LastSyncDate = DateTime.UtcNow;
                stats.LastSyncDuration = syncResult.SyncDuration;
                stats.TotalSyncs++;

                await _cacheService.SetAsync("sync_statistics", stats, TimeSpan.FromDays(7), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not update sync statistics");
            }
        }

        public override void Dispose()
        {
            _syncTimer?.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// File download result
    /// </summary>
    public class FileDownloadResult
    {
        public bool Success { get; set; }
        public string LocalFilePath { get; set; } = string.Empty;
        public bool IsTemporary { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime DownloadTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Synchronization status information
    /// </summary>
    public class SyncStatus
    {
        public DateTime LastSyncTime { get; set; }
        public DateTime LastCheckTime { get; set; }
        public bool IsFirstStartupOfDay { get; set; }
        public DateTime NextScheduledSync { get; set; }
        public bool SyncEnabled { get; set; }
        public int TotalSyncsToday { get; set; }
        public int TotalChangesToday { get; set; }
        public TimeSpan LastSyncDuration { get; set; }
        public DateTime StatusCheckTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsHealthy => SyncEnabled && string.IsNullOrEmpty(ErrorMessage);
    }

    /// <summary>
    /// Synchronization statistics
    /// </summary>
    public class SyncStatistics
    {
        public int SyncsToday { get; set; }
        public int ChangesToday { get; set; }
        public DateTime LastSyncDate { get; set; }
        public TimeSpan LastSyncDuration { get; set; }
        public int TotalSyncs { get; set; }
        public long TotalDataSynced { get; set; }
    }
}