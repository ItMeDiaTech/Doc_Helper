using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Doc_Helper.Core.Models;
using Doc_Helper.UI.ViewModels.Base;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace Doc_Helper.UI.ViewModels
{
    /// <summary>
    /// ViewModel for real-time progress reporting and status display
    /// Provides detailed progress tracking for TPL Dataflow pipeline operations
    /// </summary>
    public class ProgressReportingViewModel : BaseViewModel
    {
        private readonly IEventAggregator _eventAggregator;

        #region Private Fields
        private bool _isVisible;
        private string _currentOperation = "Ready";
        private string _currentFile = string.Empty;
        private ProcessingStage _currentStage = ProcessingStage.Initialization;
        private double _overallProgress;
        private double _stageProgress;
        private int _processedFiles;
        private int _totalFiles;
        private int _processedHyperlinks;
        private int _totalHyperlinks;
        private TimeSpan _elapsedTime;
        private TimeSpan _estimatedTimeRemaining;
        private string _throughputInfo = string.Empty;
        private ObservableCollection<StageProgressInfo> _stageProgressCollection = new();
        private ObservableCollection<PerformanceMetric> _performanceMetrics = new();
        private bool _showDetailedProgress = true;
        private bool _showPerformanceMetrics;
        private DateTime _operationStartTime;
        #endregion

        #region Public Properties
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public string CurrentOperation
        {
            get => _currentOperation;
            set => SetProperty(ref _currentOperation, value);
        }

        public string CurrentFile
        {
            get => _currentFile;
            set => SetProperty(ref _currentFile, value);
        }

        public ProcessingStage CurrentStage
        {
            get => _currentStage;
            set => SetProperty(ref _currentStage, value);
        }

        public double OverallProgress
        {
            get => _overallProgress;
            set => SetProperty(ref _overallProgress, value);
        }

        public double StageProgress
        {
            get => _stageProgress;
            set => SetProperty(ref _stageProgress, value);
        }

        public int ProcessedFiles
        {
            get => _processedFiles;
            set => SetProperty(ref _processedFiles, value);
        }

        public int TotalFiles
        {
            get => _totalFiles;
            set => SetProperty(ref _totalFiles, value);
        }

        public int ProcessedHyperlinks
        {
            get => _processedHyperlinks;
            set => SetProperty(ref _processedHyperlinks, value);
        }

        public int TotalHyperlinks
        {
            get => _totalHyperlinks;
            set => SetProperty(ref _totalHyperlinks, value);
        }

        public TimeSpan ElapsedTime
        {
            get => _elapsedTime;
            set
            {
                SetProperty(ref _elapsedTime, value);
                RaisePropertyChanged(nameof(ElapsedTimeFormatted));
            }
        }

        public TimeSpan EstimatedTimeRemaining
        {
            get => _estimatedTimeRemaining;
            set
            {
                SetProperty(ref _estimatedTimeRemaining, value);
                RaisePropertyChanged(nameof(EstimatedTimeRemainingFormatted));
            }
        }

        public string ThroughputInfo
        {
            get => _throughputInfo;
            set => SetProperty(ref _throughputInfo, value);
        }

        public ObservableCollection<StageProgressInfo> StageProgressCollection
        {
            get => _stageProgressCollection;
            set => SetProperty(ref _stageProgressCollection, value);
        }

        public ObservableCollection<PerformanceMetric> PerformanceMetrics
        {
            get => _performanceMetrics;
            set => SetProperty(ref _performanceMetrics, value);
        }

        public bool ShowDetailedProgress
        {
            get => _showDetailedProgress;
            set => SetProperty(ref _showDetailedProgress, value);
        }

        public bool ShowPerformanceMetrics
        {
            get => _showPerformanceMetrics;
            set => SetProperty(ref _showPerformanceMetrics, value);
        }

        // Formatted properties for UI binding
        public string ElapsedTimeFormatted => ElapsedTime.ToString(@"hh\:mm\:ss");
        public string EstimatedTimeRemainingFormatted => EstimatedTimeRemaining.ToString(@"hh\:mm\:ss");
        public string FilesProgressText => $"{ProcessedFiles:N0} / {TotalFiles:N0} files";
        public string HyperlinksProgressText => $"{ProcessedHyperlinks:N0} / {TotalHyperlinks:N0} hyperlinks";
        public string OverallProgressText => $"{OverallProgress:F1}%";
        public string CurrentStageText => CurrentStage.ToString().Replace("_", " ");
        #endregion

        #region Commands
        public ICommand ToggleDetailedProgressCommand { get; }
        public ICommand TogglePerformanceMetricsCommand { get; }
        public ICommand ClearMetricsCommand { get; }
        public ICommand ExportProgressDataCommand { get; }
        #endregion

        public ProgressReportingViewModel(
            ILogger<ProgressReportingViewModel> logger,
            IEventAggregator eventAggregator) : base(logger)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

            // Initialize commands
            ToggleDetailedProgressCommand = new DelegateCommand(() => ShowDetailedProgress = !ShowDetailedProgress);
            TogglePerformanceMetricsCommand = new DelegateCommand(() => ShowPerformanceMetrics = !ShowPerformanceMetrics);
            ClearMetricsCommand = new DelegateCommand(() => ClearMetrics());
            ExportProgressDataCommand = new DelegateCommand(async () => await ExportProgressDataAsync());

            // Initialize stage progress tracking
            InitializeStageProgress();

            _logger.LogInformation("ProgressReportingViewModel initialized");
        }

        #region Progress Tracking
        /// <summary>
        /// Updates progress from processing service reports
        /// </summary>
        public void UpdateProgress(ProcessingProgressReport progress)
        {
            try
            {
                if (_operationStartTime == default)
                    _operationStartTime = DateTime.UtcNow;

                CurrentOperation = progress.Message;
                CurrentFile = Path.GetFileName(progress.CurrentFile);
                CurrentStage = progress.Stage;
                OverallProgress = progress.ProgressPercentage;
                ProcessedFiles = progress.ProcessedCount;
                TotalFiles = progress.TotalCount;
                ElapsedTime = progress.ElapsedTime;

                // Update stage progress
                UpdateStageProgress(progress.Stage, progress.ProgressPercentage);

                // Calculate throughput and ETA
                UpdateThroughputAndETA();

                // Add performance metrics
                if (ShowPerformanceMetrics)
                {
                    AddPerformanceMetric(progress);
                }

                _logger.LogDebug("Progress updated: {Stage} {Progress:F1}% - {Message}",
                    progress.Stage, progress.ProgressPercentage, progress.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating progress");
            }
        }

        /// <summary>
        /// Initializes stage progress tracking
        /// </summary>
        private void InitializeStageProgress()
        {
            var stages = Enum.GetValues<ProcessingStage>();
            foreach (var stage in stages)
            {
                StageProgressCollection.Add(new StageProgressInfo
                {
                    Stage = stage,
                    Name = stage.ToString().Replace("_", " "),
                    Progress = 0,
                    IsActive = false,
                    IsCompleted = false
                });
            }
        }

        /// <summary>
        /// Updates progress for a specific stage
        /// </summary>
        private void UpdateStageProgress(ProcessingStage stage, double progress)
        {
            var stageInfo = StageProgressCollection.FirstOrDefault(s => s.Stage == stage);
            if (stageInfo != null)
            {
                // Mark previous stages as completed
                foreach (var previousStage in StageProgressCollection.Where(s => s.Stage < stage))
                {
                    previousStage.IsCompleted = true;
                    previousStage.IsActive = false;
                    previousStage.Progress = 100;
                }

                // Update current stage
                stageInfo.IsActive = true;
                stageInfo.IsCompleted = false;
                stageInfo.Progress = progress;
                stageInfo.LastUpdateTime = DateTime.UtcNow;

                // Mark future stages as not started
                foreach (var futureStage in StageProgressCollection.Where(s => s.Stage > stage))
                {
                    futureStage.IsActive = false;
                    futureStage.IsCompleted = false;
                    futureStage.Progress = 0;
                }
            }
        }

        /// <summary>
        /// Updates throughput and estimated time remaining
        /// </summary>
        private void UpdateThroughputAndETA()
        {
            try
            {
                if (ProcessedFiles > 0 && ElapsedTime.TotalSeconds > 0)
                {
                    var filesPerSecond = ProcessedFiles / ElapsedTime.TotalSeconds;
                    var remainingFiles = TotalFiles - ProcessedFiles;

                    if (filesPerSecond > 0 && remainingFiles > 0)
                    {
                        var etaSeconds = remainingFiles / filesPerSecond;
                        EstimatedTimeRemaining = TimeSpan.FromSeconds(etaSeconds);
                    }

                    ThroughputInfo = $"{filesPerSecond:F1} files/sec";
                }
                else
                {
                    ThroughputInfo = "Calculating...";
                    EstimatedTimeRemaining = TimeSpan.Zero;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error calculating throughput");
            }
        }

        /// <summary>
        /// Adds performance metric for tracking
        /// </summary>
        private void AddPerformanceMetric(ProcessingProgressReport progress)
        {
            try
            {
                var metric = new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    Stage = progress.Stage.ToString(),
                    Progress = progress.ProgressPercentage,
                    ElapsedTime = progress.ElapsedTime,
                    MemoryUsageMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0)
                };

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    PerformanceMetrics.Add(metric);

                    // Limit metrics collection size
                    while (PerformanceMetrics.Count > 200)
                    {
                        PerformanceMetrics.RemoveAt(0);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error adding performance metric");
            }
        }
        #endregion

        #region Operations
        /// <summary>
        /// Starts progress tracking for new operation
        /// </summary>
        public void StartOperation(string operationName, int totalFiles)
        {
            try
            {
                _operationStartTime = DateTime.UtcNow;
                CurrentOperation = operationName;
                TotalFiles = totalFiles;
                ProcessedFiles = 0;
                OverallProgress = 0;
                ElapsedTime = TimeSpan.Zero;
                EstimatedTimeRemaining = TimeSpan.Zero;
                ThroughputInfo = "Starting...";
                IsVisible = true;

                // Reset stage progress
                foreach (var stage in StageProgressCollection)
                {
                    stage.Progress = 0;
                    stage.IsActive = false;
                    stage.IsCompleted = false;
                }

                // Clear previous metrics if requested
                if (ShowPerformanceMetrics)
                {
                    PerformanceMetrics.Clear();
                }

                _logger.LogInformation("Started progress tracking for operation: {Operation} with {FileCount} files",
                    operationName, totalFiles);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error starting operation progress tracking");
            }
        }

        /// <summary>
        /// Completes progress tracking
        /// </summary>
        public void CompleteOperation(bool success, string message = null)
        {
            try
            {
                OverallProgress = 100;
                CurrentOperation = success ? "Completed successfully" : "Completed with errors";

                if (!string.IsNullOrEmpty(message))
                {
                    CurrentOperation = message;
                }

                // Mark all stages as completed
                foreach (var stage in StageProgressCollection)
                {
                    stage.IsCompleted = true;
                    stage.IsActive = false;
                    stage.Progress = 100;
                }

                _logger.LogInformation("Operation progress tracking completed. Success: {Success}", success);

                // Auto-hide after delay if successful
                if (success)
                {
                    Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(_ =>
                    {
                        Application.Current?.Dispatcher.Invoke(() => IsVisible = false);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error completing operation progress tracking");
            }
        }

        /// <summary>
        /// Clears performance metrics
        /// </summary>
        private void ClearMetrics()
        {
            PerformanceMetrics.Clear();
            _logger.LogDebug("Performance metrics cleared");
        }

        /// <summary>
        /// Exports progress data for analysis
        /// </summary>
        private async Task ExportProgressDataAsync()
        {
            try
            {
                // This could export performance data to CSV or JSON
                _logger.LogInformation("Progress data export requested (not yet implemented)");

                // Placeholder for future implementation
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting progress data");
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Gets current progress summary
        /// </summary>
        public ProgressSummary GetProgressSummary()
        {
            return new ProgressSummary
            {
                OverallProgress = OverallProgress,
                CurrentStage = CurrentStage,
                ProcessedFiles = ProcessedFiles,
                TotalFiles = TotalFiles,
                ElapsedTime = ElapsedTime,
                EstimatedTimeRemaining = EstimatedTimeRemaining,
                IsComplete = OverallProgress >= 100,
                ThroughputInfo = ThroughputInfo
            };
        }

        /// <summary>
        /// Updates progress from external source
        /// </summary>
        public void UpdateFromExternalProgress(ProcessingProgressReport progress)
        {
            UpdateProgress(progress);
        }
        #endregion

        public override void Dispose()
        {
            try
            {
                _logger.LogInformation("ProgressReportingViewModel disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during ProgressReportingViewModel disposal");
            }

            base.Dispose();
        }
    }

    /// <summary>
    /// Information about progress for a specific processing stage
    /// </summary>
    public class StageProgressInfo : BindableBase
    {
        private ProcessingStage _stage;
        private string _name = string.Empty;
        private double _progress;
        private bool _isActive;
        private bool _isCompleted;
        private DateTime _lastUpdateTime;


        public ProcessingStage Stage
        {
            get => _stage;
            set => SetProperty(ref _stage, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set => SetProperty(ref _lastUpdateTime, value);
        }

        public string ProgressText => $"{Progress:F1}%";
        public string StatusText => IsCompleted ? "✓ Completed" : IsActive ? "⚡ Active" : "⏳ Pending";
    }

    /// <summary>
    /// Performance metric for tracking
    /// </summary>
    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public string Stage { get; set; } = string.Empty;
        public double Progress { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public double MemoryUsageMB { get; set; }
        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");
        public string ProgressText => $"{Progress:F1}%";
        public string MemoryText => $"{MemoryUsageMB:F1} MB";
    }

    /// <summary>
    /// Progress summary for external consumers
    /// </summary>
    public class ProgressSummary
    {
        public double OverallProgress { get; set; }
        public ProcessingStage CurrentStage { get; set; }
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public bool IsComplete { get; set; }
        public string ThroughputInfo { get; set; } = string.Empty;
    }
}