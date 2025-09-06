using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.UI.ViewModels.Base;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace Doc_Helper.UI.ViewModels
{
    /// <summary>
    /// ViewModel for comprehensive error handling and status display
    /// Provides real-time system health monitoring and user feedback
    /// </summary>
    public class StatusDisplayViewModel : BaseViewModel
    {
        private readonly IHyperlinkLookupService _lookupService;
        private readonly IValidationService _validationService;
        private readonly IEventAggregator _eventAggregator;

        #region Private Fields
        private SystemHealthStatus _systemHealth = new();
        private DatabaseHealthResult _databaseHealth = new();
        private ApiValidationResult _apiHealth = new();
        private ObservableCollection<StatusMessage> _statusMessages = new();
        private ObservableCollection<PerformanceIndicator> _performanceIndicators = new();
        private bool _isSystemHealthy = true;
        private string _overallStatusMessage = "System Ready";
        private string _lastSyncTime = "Never";
        private string _databaseRecordCount = "Loading...";
        private double _cacheHitRate;
        private bool _showDetailedStatus;
        private DateTime _lastHealthCheck = DateTime.MinValue;
        #endregion

        #region Public Properties
        public SystemHealthStatus SystemHealth
        {
            get => _systemHealth;
            set => SetProperty(ref _systemHealth, value);
        }

        public DatabaseHealthResult DatabaseHealth
        {
            get => _databaseHealth;
            set => SetProperty(ref _databaseHealth, value);
        }

        public ApiValidationResult ApiHealth
        {
            get => _apiHealth;
            set => SetProperty(ref _apiHealth, value);
        }

        public ObservableCollection<StatusMessage> StatusMessages
        {
            get => _statusMessages;
            set => SetProperty(ref _statusMessages, value);
        }

        public ObservableCollection<PerformanceIndicator> PerformanceIndicators
        {
            get => _performanceIndicators;
            set => SetProperty(ref _performanceIndicators, value);
        }

        public bool IsSystemHealthy
        {
            get => _isSystemHealthy;
            set => SetProperty(ref _isSystemHealthy, value);
        }

        public string OverallStatusMessage
        {
            get => _overallStatusMessage;
            set => SetProperty(ref _overallStatusMessage, value);
        }

        public string LastSyncTime
        {
            get => _lastSyncTime;
            set => SetProperty(ref _lastSyncTime, value);
        }

        public string DatabaseRecordCount
        {
            get => _databaseRecordCount;
            set => SetProperty(ref _databaseRecordCount, value);
        }

        public double CacheHitRate
        {
            get => _cacheHitRate;
            set => SetProperty(ref _cacheHitRate, value);
        }

        public bool ShowDetailedStatus
        {
            get => _showDetailedStatus;
            set => SetProperty(ref _showDetailedStatus, value);
        }

        public string HealthStatusIcon => IsSystemHealthy ? "✅" : "⚠️";
        public string SystemStatusText => IsSystemHealthy ? "All Systems Operational" : "Issues Detected";
        #endregion

        #region Commands
        public ICommand RefreshStatusCommand { get; }
        public ICommand ToggleDetailedStatusCommand { get; }
        public ICommand ClearStatusMessagesCommand { get; }
        public ICommand RunHealthCheckCommand { get; }
        public ICommand OptimizePerformanceCommand { get; }
        #endregion

        public StatusDisplayViewModel(
            ILogger<StatusDisplayViewModel> logger,
            IHyperlinkLookupService lookupService,
            IValidationService validationService,
            IEventAggregator eventAggregator) : base(logger)
        {
            _lookupService = lookupService ?? throw new ArgumentNullException(nameof(lookupService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

            // Initialize commands
            RefreshStatusCommand = new DelegateCommand(async () => await RefreshStatusAsync());
            ToggleDetailedStatusCommand = new DelegateCommand(() => ShowDetailedStatus = !ShowDetailedStatus);
            ClearStatusMessagesCommand = new DelegateCommand(() => ClearStatusMessages());
            RunHealthCheckCommand = new DelegateCommand(async () => await RunHealthCheckAsync());
            OptimizePerformanceCommand = new DelegateCommand(async () => await OptimizePerformanceAsync());

            // Initialize status tracking
            InitializeStatusTracking();

            _logger.LogInformation("StatusDisplayViewModel initialized");
        }

        #region Status Tracking
        /// <summary>
        /// Initializes status tracking and periodic health checks
        /// </summary>
        private void InitializeStatusTracking()
        {
            try
            {
                // Initialize performance indicators
                InitializePerformanceIndicators();

                // Start periodic health monitoring
                Task.Run(async () => await StartPeriodicHealthMonitoringAsync());

                AddStatusMessage("Status monitoring initialized", StatusLevel.Info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing status tracking");
                AddStatusMessage($"Status tracking initialization failed: {ex.Message}", StatusLevel.Error);
            }
        }

        /// <summary>
        /// Initializes performance indicators
        /// </summary>
        private void InitializePerformanceIndicators()
        {
            PerformanceIndicators.Add(new PerformanceIndicator
            {
                Name = "Cache Hit Rate",
                Description = "Percentage of lookups served from cache",
                ValueFormat = "{0:F1}%",
                TargetValue = 80,
                WarningThreshold = 60,
                ErrorThreshold = 40
            });

            PerformanceIndicators.Add(new PerformanceIndicator
            {
                Name = "Local DB Hit Rate",
                Description = "Percentage of lookups served from local database",
                ValueFormat = "{0:F1}%",
                TargetValue = 95,
                WarningThreshold = 85,
                ErrorThreshold = 70
            });

            PerformanceIndicators.Add(new PerformanceIndicator
            {
                Name = "Database Size",
                Description = "Size of local SQLite database",
                ValueFormat = "{0:F2} MB",
                TargetValue = 50,
                WarningThreshold = 100,
                ErrorThreshold = 200
            });

            PerformanceIndicators.Add(new PerformanceIndicator
            {
                Name = "Sync Status",
                Description = "Time since last data synchronization",
                ValueFormat = "{0} hours",
                TargetValue = 0,
                WarningThreshold = 24,
                ErrorThreshold = 72
            });
        }

        /// <summary>
        /// Starts periodic health monitoring
        /// </summary>
        private async Task StartPeriodicHealthMonitoringAsync()
        {
            while (!IsDisposed)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5)); // Check every 5 minutes

                    if (DateTime.UtcNow - _lastHealthCheck > TimeSpan.FromMinutes(5))
                    {
                        await UpdateHealthStatusAsync();
                        _lastHealthCheck = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in periodic health monitoring");
                    await Task.Delay(TimeSpan.FromMinutes(1)); // Wait before retrying
                }
            }
        }

        /// <summary>
        /// Updates health status information
        /// </summary>
        private async Task UpdateHealthStatusAsync()
        {
            try
            {
                // Check database health
                DatabaseHealth = await _lookupService.CheckDatabaseHealthAsync();

                // Update performance indicators
                UpdatePerformanceIndicators();

                // Update overall health status
                UpdateOverallHealthStatus();

                // Update sync information
                await UpdateSyncInformationAsync();

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating health status");
                AddStatusMessage($"Health status update failed: {ex.Message}", StatusLevel.Warning);
            }
        }

        /// <summary>
        /// Updates performance indicators with current values
        /// </summary>
        private void UpdatePerformanceIndicators()
        {
            try
            {
                var lookupStats = _lookupService.GetPerformanceStats();

                // Update cache hit rate
                var cacheIndicator = PerformanceIndicators.FirstOrDefault(p => p.Name == "Cache Hit Rate");
                if (cacheIndicator != null)
                {
                    cacheIndicator.CurrentValue = lookupStats.CacheHitRate;
                    cacheIndicator.UpdateStatus();
                }

                // Update local DB hit rate
                var localDbIndicator = PerformanceIndicators.FirstOrDefault(p => p.Name == "Local DB Hit Rate");
                if (localDbIndicator != null)
                {
                    localDbIndicator.CurrentValue = lookupStats.LocalHitRate;
                    localDbIndicator.UpdateStatus();
                }

                // Update database size
                var dbSizeIndicator = PerformanceIndicators.FirstOrDefault(p => p.Name == "Database Size");
                if (dbSizeIndicator != null && DatabaseHealth != null)
                {
                    dbSizeIndicator.CurrentValue = DatabaseHealth.DatabaseSizeMB;
                    dbSizeIndicator.UpdateStatus();
                }

                // Update sync status
                var syncIndicator = PerformanceIndicators.FirstOrDefault(p => p.Name == "Sync Status");
                if (syncIndicator != null && DatabaseHealth != null)
                {
                    syncIndicator.CurrentValue = DatabaseHealth.DataStalenessHours;
                    syncIndicator.UpdateStatus();
                }

                // Cache overall hit rate for binding
                CacheHitRate = lookupStats.CacheHitRate;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error updating performance indicators");
            }
        }

        /// <summary>
        /// Updates overall system health status
        /// </summary>
        private void UpdateOverallHealthStatus()
        {
            try
            {
                var hasErrors = PerformanceIndicators.Any(p => p.Status == IndicatorStatus.Error);
                var hasWarnings = PerformanceIndicators.Any(p => p.Status == IndicatorStatus.Warning);

                IsSystemHealthy = !hasErrors && DatabaseHealth?.IsHealthy == true;

                if (IsSystemHealthy)
                {
                    OverallStatusMessage = "All systems operational";
                }
                else if (hasErrors)
                {
                    OverallStatusMessage = "System issues detected - check status details";
                }
                else if (hasWarnings)
                {
                    OverallStatusMessage = "System operational with warnings";
                }

                // Update database record count display
                if (DatabaseHealth != null)
                {
                    DatabaseRecordCount = $"{DatabaseHealth.TotalRecords:N0} records";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error updating overall health status");
            }
        }

        /// <summary>
        /// Updates synchronization information display
        /// </summary>
        private async Task UpdateSyncInformationAsync()
        {
            try
            {
                // This would integrate with BackgroundSyncService
                // For now, use placeholder logic
                if (DatabaseHealth?.LastSyncTime != null)
                {
                    var syncAge = DateTime.UtcNow - DatabaseHealth.LastSyncTime;
                    if (syncAge.TotalDays >= 1)
                        LastSyncTime = $"{syncAge.TotalDays:F0} days ago";
                    else if (syncAge.TotalHours >= 1)
                        LastSyncTime = $"{syncAge.TotalHours:F0} hours ago";
                    else
                        LastSyncTime = $"{syncAge.TotalMinutes:F0} minutes ago";
                }
                else
                {
                    LastSyncTime = "Never";
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error updating sync information");
            }
        }
        #endregion

        #region Status Operations
        /// <summary>
        /// Refreshes all status information
        /// </summary>
        private async Task RefreshStatusAsync()
        {
            try
            {
                AddStatusMessage("Refreshing system status...", StatusLevel.Info);
                await UpdateHealthStatusAsync();
                AddStatusMessage("Status refresh completed", StatusLevel.Info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing status");
                AddStatusMessage($"Status refresh failed: {ex.Message}", StatusLevel.Error);
            }
        }

        /// <summary>
        /// Runs comprehensive health check
        /// </summary>
        private async Task RunHealthCheckAsync()
        {
            try
            {
                AddStatusMessage("Running comprehensive health check...", StatusLevel.Info);

                var healthResult = await _validationService.PerformHealthCheckAsync();

                if (healthResult.IsHealthy)
                {
                    AddStatusMessage($"Health check passed - Score: {healthResult.HealthScore}/100", StatusLevel.Success);
                }
                else
                {
                    AddStatusMessage($"Health check failed - Score: {healthResult.HealthScore}/100", StatusLevel.Warning);
                }

                await UpdateHealthStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                AddStatusMessage($"Health check error: {ex.Message}", StatusLevel.Error);
            }
        }

        /// <summary>
        /// Optimizes system performance
        /// </summary>
        private async Task OptimizePerformanceAsync()
        {
            try
            {
                AddStatusMessage("Optimizing system performance...", StatusLevel.Info);

                var optimizationResult = await _lookupService.OptimizePerformanceAsync();

                if (optimizationResult.Success)
                {
                    AddStatusMessage("Performance optimization completed successfully", StatusLevel.Success);
                }
                else
                {
                    AddStatusMessage($"Performance optimization failed: {optimizationResult.ErrorMessage}", StatusLevel.Warning);
                }

                await UpdateHealthStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Performance optimization failed");
                AddStatusMessage($"Performance optimization error: {ex.Message}", StatusLevel.Error);
            }
        }

        /// <summary>
        /// Clears status messages
        /// </summary>
        private void ClearStatusMessages()
        {
            StatusMessages.Clear();
            AddStatusMessage("Status messages cleared", StatusLevel.Info);
        }

        /// <summary>
        /// Adds status message to the collection
        /// </summary>
        private void AddStatusMessage(string message, StatusLevel level)
        {
            try
            {
                var statusMessage = new StatusMessage
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message
                };

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusMessages.Add(statusMessage);

                    // Limit collection size
                    while (StatusMessages.Count > 500)
                    {
                        StatusMessages.RemoveAt(0);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error adding status message");
            }
        }
        #endregion

        #region User Feedback
        /// <summary>
        /// Shows user notification for important events
        /// </summary>
        public void ShowUserNotification(string title, string message, NotificationLevel level)
        {
            try
            {
                // This would integrate with a notification system
                AddStatusMessage($"[{level}] {title}: {message}", level switch
                {
                    NotificationLevel.Info => StatusLevel.Info,
                    NotificationLevel.Warning => StatusLevel.Warning,
                    NotificationLevel.Error => StatusLevel.Error,
                    NotificationLevel.Success => StatusLevel.Success,
                    _ => StatusLevel.Info
                });

                _logger.LogInformation("User notification: [{Level}] {Title} - {Message}", level, title, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error showing user notification");
            }
        }

        /// <summary>
        /// Reports processing error to user with actionable information
        /// </summary>
        public void ReportProcessingError(string fileName, ProcessingStage stage, string errorMessage, Exception exception = null)
        {
            try
            {
                var userFriendlyMessage = GetUserFriendlyErrorMessage(stage, errorMessage);
                var suggestion = GetErrorResolutionSuggestion(stage, errorMessage);

                AddStatusMessage($"Error processing {fileName}: {userFriendlyMessage}", StatusLevel.Error);

                if (!string.IsNullOrEmpty(suggestion))
                {
                    AddStatusMessage($"Suggestion: {suggestion}", StatusLevel.Info);
                }

                _logger.LogError(exception, "Processing error reported to user: {FileName} {Stage} {Error}",
                    fileName, stage, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reporting processing error to user");
            }
        }

        /// <summary>
        /// Gets user-friendly error message
        /// </summary>
        private string GetUserFriendlyErrorMessage(ProcessingStage stage, string errorMessage)
        {
            return stage switch
            {
                ProcessingStage.FileValidation => "Document cannot be opened or is corrupted",
                ProcessingStage.HyperlinkExtraction => "Unable to read hyperlinks from document",
                ProcessingStage.ApiProcessing => "Could not retrieve updated hyperlink information",
                ProcessingStage.DocumentUpdate => "Unable to save changes to document",
                _ => "An unexpected error occurred during processing"
            };
        }

        /// <summary>
        /// Gets actionable error resolution suggestion
        /// </summary>
        private string GetErrorResolutionSuggestion(ProcessingStage stage, string errorMessage)
        {
            return stage switch
            {
                ProcessingStage.FileValidation => "Ensure the document is not open in another application and is not corrupted",
                ProcessingStage.HyperlinkExtraction => "Try opening the document in Word to verify it's not corrupted",
                ProcessingStage.ApiProcessing => "Check internet connection - processing will continue with local data",
                ProcessingStage.DocumentUpdate => "Ensure the document is not read-only and you have write permissions",
                _ => "Check the processing log for more details"
            };
        }
        #endregion

        public override void Dispose()
        {
            try
            {
                _logger.LogInformation("StatusDisplayViewModel disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during StatusDisplayViewModel disposal");
            }

            base.Dispose();
        }
    }

    /// <summary>
    /// Status message for UI display
    /// </summary>
    public class StatusMessage
    {
        public DateTime Timestamp { get; set; }
        public StatusLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");
        public string LevelIcon => Level switch
        {
            StatusLevel.Info => "ℹ️",
            StatusLevel.Success => "✅",
            StatusLevel.Warning => "⚠️",
            StatusLevel.Error => "❌",
            _ => "📝"
        };
    }

    /// <summary>
    /// Performance indicator for system monitoring
    /// </summary>
    public class PerformanceIndicator : BindableBase
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private double _currentValue;
        private double _targetValue;
        private double _warningThreshold;
        private double _errorThreshold;
        private string _valueFormat = "{0:F2}";
        private IndicatorStatus _status = IndicatorStatus.Good;


        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public double CurrentValue
        {
            get => _currentValue;
            set
            {
                SetProperty(ref _currentValue, value);
                RaisePropertyChanged(nameof(FormattedValue));
            }
        }

        public double TargetValue
        {
            get => _targetValue;
            set => SetProperty(ref _targetValue, value);
        }

        public double WarningThreshold
        {
            get => _warningThreshold;
            set => SetProperty(ref _warningThreshold, value);
        }

        public double ErrorThreshold
        {
            get => _errorThreshold;
            set => SetProperty(ref _errorThreshold, value);
        }

        public string ValueFormat
        {
            get => _valueFormat;
            set => SetProperty(ref _valueFormat, value);
        }

        public IndicatorStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string FormattedValue => string.Format(ValueFormat, CurrentValue);
        public string StatusIcon => Status switch
        {
            IndicatorStatus.Good => "🟢",
            IndicatorStatus.Warning => "🟡",
            IndicatorStatus.Error => "🔴",
            _ => "⚪"
        };

        public void UpdateStatus()
        {
            if (CurrentValue <= ErrorThreshold)
                Status = IndicatorStatus.Error;
            else if (CurrentValue <= WarningThreshold)
                Status = IndicatorStatus.Warning;
            else
                Status = IndicatorStatus.Good;
        }
    }

    /// <summary>
    /// Status levels for UI display
    /// </summary>
    public enum StatusLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Notification levels for user feedback
    /// </summary>
    public enum NotificationLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Performance indicator status
    /// </summary>
    public enum IndicatorStatus
    {
        Good,
        Warning,
        Error
    }

    /// <summary>
    /// System health status information
    /// </summary>
    public class SystemHealthStatus
    {
        public bool IsHealthy { get; set; } = true;
        public int HealthScore { get; set; } = 100;
        public DateTime LastCheckTime { get; set; } = DateTime.UtcNow;
        public List<string> Issues { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }
}