using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.UI.ViewModels.Base;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;

namespace Doc_Helper.UI.ViewModels
{
    /// <summary>
    /// Main ViewModel for document processing operations
    /// Orchestrates the TPL Dataflow pipeline through the UI
    /// </summary>
    public class DocumentProcessingViewModel : BaseViewModel
    {
        private readonly IDocumentProcessingService _processingService;
        private readonly IHyperlinkLookupService _lookupService;
        private readonly IValidationService _validationService;
        private readonly IEventAggregator _eventAggregator;

        #region Private Fields
        private CancellationTokenSource _processingCancellation;
        private bool _isProcessing;
        private double _progressPercentage;
        private string _currentFile = string.Empty;
        private ProcessingStage _currentStage = ProcessingStage.Initialization;
        private ObservableCollection<string> _selectedFiles = new();
        private ObservableCollection<ProcessingLogEntry> _processingLog = new();
        private ProcessingStatistics _statistics = new();
        private string _processingDuration = "00:00:00";
        private bool _canStartProcessing = true;
        private bool _canCancelProcessing;
        #endregion

        #region Public Properties
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }


        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
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

        public ObservableCollection<string> SelectedFiles
        {
            get => _selectedFiles;
            set => SetProperty(ref _selectedFiles, value);
        }

        public ObservableCollection<ProcessingLogEntry> ProcessingLog
        {
            get => _processingLog;
            set => SetProperty(ref _processingLog, value);
        }

        public ProcessingStatistics Statistics
        {
            get => _statistics;
            set => SetProperty(ref _statistics, value);
        }

        public string ProcessingDuration
        {
            get => _processingDuration;
            set => SetProperty(ref _processingDuration, value);
        }

        public bool CanStartProcessing
        {
            get => _canStartProcessing;
            set => SetProperty(ref _canStartProcessing, value);
        }

        public bool CanCancelProcessing
        {
            get => _canCancelProcessing;
            set => SetProperty(ref _canCancelProcessing, value);
        }
        #endregion

        #region Commands
        public ICommand SelectFilesCommand { get; }
        public ICommand StartProcessingCommand { get; }
        public ICommand CancelProcessingCommand { get; }
        public ICommand ClearFilesCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand RemoveFileCommand { get; }
        public ICommand ValidateFilesCommand { get; }
        public ICommand ViewStatisticsCommand { get; }
        #endregion

        public DocumentProcessingViewModel(
            ILogger<DocumentProcessingViewModel> logger,
            IDocumentProcessingService processingService,
            IHyperlinkLookupService lookupService,
            IValidationService validationService,
            IEventAggregator eventAggregator) : base(logger)
        {
            _processingService = processingService ?? throw new ArgumentNullException(nameof(processingService));
            _lookupService = lookupService ?? throw new ArgumentNullException(nameof(lookupService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

            // Initialize commands
            SelectFilesCommand = new DelegateCommand(async () => await SelectFilesAsync());
            StartProcessingCommand = new DelegateCommand(async () => await StartProcessingAsync(), () => CanStartProcessing);
            CancelProcessingCommand = new DelegateCommand(async () => await CancelProcessingAsync(), () => CanCancelProcessing);
            ClearFilesCommand = new DelegateCommand(() => ClearFiles(), () => !IsProcessing);
            ClearLogCommand = new DelegateCommand(() => ClearLog(), () => !IsProcessing);
            RemoveFileCommand = new DelegateCommand<string>(filePath => RemoveFile(filePath), _ => !IsProcessing);
            ValidateFilesCommand = new DelegateCommand(async () => await ValidateFilesAsync(), () => SelectedFiles.Any() && !IsProcessing);
            ViewStatisticsCommand = new DelegateCommand(() => ViewStatistics());

            // Subscribe to processing events
            _processingService.ProcessingStageChanged += OnProcessingStageChanged;
            _processingService.ProcessingError += OnProcessingError;

            // Initialize with sample message
            AddLogEntry("Application initialized and ready for document processing", LogLevel.Information);

            _logger.LogInformation("DocumentProcessingViewModel initialized");
        }

        #region File Management
        /// <summary>
        /// Opens file dialog for document selection
        /// </summary>
        private async Task SelectFilesAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Word Documents to Process",
                    Filter = "Word Documents (*.docx)|*.docx|All Files (*.*)|*.*",
                    Multiselect = true,
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    var newFiles = dialog.FileNames.Where(f => !SelectedFiles.Contains(f)).ToList();

                    foreach (var file in newFiles)
                    {
                        SelectedFiles.Add(file);
                    }

                    AddLogEntry($"Selected {newFiles.Count} document(s) for processing", LogLevel.Information);
                    _logger.LogInformation("User selected {FileCount} documents", newFiles.Count);

                    // Auto-validate selected files
                    if (newFiles.Count > 0)
                    {
                        await ValidateFilesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting files");
                AddLogEntry($"Error selecting files: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Removes a file from the selected files list
        /// </summary>
        private void RemoveFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && SelectedFiles.Contains(filePath))
            {
                SelectedFiles.Remove(filePath);
                AddLogEntry($"Removed file: {Path.GetFileName(filePath)}", LogLevel.Information);
            }
        }

        /// <summary>
        /// Clears all selected files
        /// </summary>
        private void ClearFiles()
        {
            var fileCount = SelectedFiles.Count;
            SelectedFiles.Clear();
            AddLogEntry($"Cleared {fileCount} selected file(s)", LogLevel.Information);
        }
        #endregion

        #region Processing Operations
        /// <summary>
        /// Starts document processing using TPL Dataflow pipeline
        /// </summary>
        private async Task StartProcessingAsync()
        {
            if (!SelectedFiles.Any())
            {
                AddLogEntry("No files selected for processing", LogLevel.Warning);
                return;
            }

            try
            {
                IsProcessing = true;
                CanStartProcessing = false;
                CanCancelProcessing = true;
                _processingCancellation = new CancellationTokenSource();

                var startTime = DateTime.UtcNow;
                StatusMessage = "Starting document processing...";
                ProgressPercentage = 0;

                AddLogEntry($"Starting processing of {SelectedFiles.Count} document(s)", LogLevel.Information);
                _logger.LogInformation("Starting document processing for {FileCount} files", SelectedFiles.Count);

                // Create progress reporter
                var progress = new Progress<ProcessingProgressReport>(OnProgressReported);

                // Start processing using TPL Dataflow pipeline
                var result = await _processingService.ProcessDocumentsAsync(
                    SelectedFiles.ToList(),
                    progress,
                    _processingCancellation.Token);

                // Update statistics
                Statistics = _processingService.GetProcessingStatistics();

                var duration = DateTime.UtcNow - startTime;
                ProcessingDuration = duration.ToString(@"hh\:mm\:ss");

                if (result.Success)
                {
                    StatusMessage = $"Processing completed successfully - {result.SuccessfulFiles}/{result.TotalFiles} files processed";
                    AddLogEntry($"Processing completed: {result.SuccessfulFiles}/{result.TotalFiles} files processed in {ProcessingDuration}", LogLevel.Information);
                    ProgressPercentage = 100;
                }
                else
                {
                    StatusMessage = $"Processing failed: {result.ErrorMessage}";
                    AddLogEntry($"Processing failed: {result.ErrorMessage}", LogLevel.Error);
                }

                _logger.LogInformation("Document processing completed. Success: {Success}, Duration: {Duration}",
                    result.Success, duration);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Processing was cancelled";
                AddLogEntry("Processing was cancelled by user", LogLevel.Warning);
                _logger.LogInformation("Document processing was cancelled");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Processing error: {ex.Message}";
                AddLogEntry($"Processing error: {ex.Message}", LogLevel.Error);
                _logger.LogError(ex, "Document processing failed");
            }
            finally
            {
                IsProcessing = false;
                CanStartProcessing = true;
                CanCancelProcessing = false;
                CurrentStage = ProcessingStage.Completion;
                _processingCancellation?.Dispose();
                _processingCancellation = null;

                // Refresh commands
                ((DelegateCommand)StartProcessingCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)CancelProcessingCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)ClearFilesCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)ValidateFilesCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Cancels ongoing processing operation
        /// </summary>
        private async Task CancelProcessingAsync()
        {
            try
            {
                if (_processingCancellation != null && !_processingCancellation.Token.IsCancellationRequested)
                {
                    _processingCancellation.Cancel();
                    StatusMessage = "Cancelling processing...";
                    AddLogEntry("Processing cancellation requested", LogLevel.Warning);

                    // Give the pipeline time to clean up
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling processing");
                AddLogEntry($"Error cancelling processing: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Validates selected files before processing
        /// </summary>
        private async Task ValidateFilesAsync()
        {
            if (!SelectedFiles.Any()) return;

            try
            {
                StatusMessage = "Validating selected files...";
                AddLogEntry($"Validating {SelectedFiles.Count} selected file(s)", LogLevel.Information);

                var validationResult = await _validationService.ValidateDocumentsAsync(SelectedFiles);

                if (validationResult.Success)
                {
                    StatusMessage = $"Validation completed: {validationResult.ValidFiles}/{validationResult.TotalFiles} files are valid";
                    AddLogEntry($"File validation passed: {validationResult.ValidFiles}/{validationResult.TotalFiles} files valid", LogLevel.Information);
                }
                else
                {
                    StatusMessage = $"Validation issues found: {validationResult.InvalidFiles} invalid files";
                    AddLogEntry($"File validation issues: {validationResult.InvalidFiles} invalid files", LogLevel.Warning);

                    // Log specific validation errors
                    foreach (var error in validationResult.ValidationErrors.Take(5)) // Limit to first 5 errors
                    {
                        AddLogEntry($"Validation error: {error}", LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File validation failed");
                AddLogEntry($"File validation error: {ex.Message}", LogLevel.Error);
            }
        }
        #endregion

        #region Progress Reporting
        /// <summary>
        /// Handles progress reports from the processing service
        /// </summary>
        private void OnProgressReported(ProcessingProgressReport progress)
        {
            try
            {
                CurrentStage = progress.Stage;
                CurrentFile = progress.CurrentFile;
                ProgressPercentage = progress.ProgressPercentage;
                StatusMessage = $"{progress.Stage}: {progress.Message}";

                // Add detailed progress to log
                if (!string.IsNullOrEmpty(progress.Message))
                {
                    AddLogEntry($"[{progress.Stage}] {progress.Message}", LogLevel.Debug);
                }

                // Update processing duration
                if (progress.ElapsedTime.TotalSeconds > 0)
                {
                    ProcessingDuration = progress.ElapsedTime.ToString(@"hh\:mm\:ss");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error handling progress report");
            }
        }

        /// <summary>
        /// Handles processing stage changes
        /// </summary>
        private void OnProcessingStageChanged(object sender, ProcessingStageChangedEventArgs e)
        {
            try
            {
                CurrentStage = e.CurrentStage;
                AddLogEntry($"Stage changed: {e.PreviousStage} → {e.CurrentStage} ({e.FileName})", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error handling stage change");
            }
        }

        /// <summary>
        /// Handles processing errors
        /// </summary>
        private void OnProcessingError(object sender, ProcessingErrorEventArgs e)
        {
            try
            {
                var fileName = Path.GetFileName(e.FilePath);
                AddLogEntry($"Error in {e.Stage} for {fileName}: {e.ErrorMessage}", LogLevel.Error);
                _logger.LogError(e.Exception, "Processing error for {FileName} in stage {Stage}", fileName, e.Stage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error handling processing error event");
            }
        }
        #endregion

        #region Logging
        /// <summary>
        /// Clears the processing log
        /// </summary>
        private void ClearLog()
        {
            ProcessingLog.Clear();
            AddLogEntry("Processing log cleared", LogLevel.Information);
        }

        /// <summary>
        /// Adds entry to processing log with timestamp
        /// </summary>
        private void AddLogEntry(string message, LogLevel level)
        {
            try
            {
                var entry = new ProcessingLogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message
                };

                // Add to UI collection on UI thread
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ProcessingLog.Add(entry);

                    // Limit log size to prevent memory issues
                    while (ProcessingLog.Count > 1000)
                    {
                        ProcessingLog.RemoveAt(0);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error adding log entry");
            }
        }
        #endregion

        #region Statistics and Health
        /// <summary>
        /// Views detailed processing statistics
        /// </summary>
        private void ViewStatistics()
        {
            try
            {
                // This could open a detailed statistics dialog
                var stats = _processingService.GetProcessingStatistics();
                var lookupStats = _lookupService.GetPerformanceStats();

                var message = $@"Processing Statistics:
Total Files Processed: {stats.ProcessedFiles:N0}
Total Hyperlinks: {stats.TotalHyperlinks:N0}
Cache Hit Rate: {lookupStats.CacheHitRate:F1}%
Local DB Hit Rate: {lookupStats.LocalHitRate:F1}%
API Call Rate: {lookupStats.ApiCallRate:F1}%
Total Processing Time: {stats.TotalProcessingTime:hh\:mm\:ss}";

                // For now, add to log - later could be a proper dialog
                AddLogEntry("=== PERFORMANCE STATISTICS ===", LogLevel.Information);
                foreach (var line in message.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        AddLogEntry(line.Trim(), LogLevel.Information);
                }
                AddLogEntry("=== END STATISTICS ===", LogLevel.Information);

                _logger.LogInformation("User viewed processing statistics");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing statistics");
                AddLogEntry($"Error viewing statistics: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Performs periodic health checks
        /// </summary>
        public async Task PerformHealthCheckAsync()
        {
            try
            {
                var healthResult = await _lookupService.CheckDatabaseHealthAsync();

                if (!healthResult.IsHealthy)
                {
                    AddLogEntry($"Health check warning: {string.Join(", ", healthResult.HealthIssues)}", LogLevel.Warning);

                    if (healthResult.RecommendSync)
                    {
                        AddLogEntry("Recommend data refresh - running background sync", LogLevel.Information);
                        // Trigger background sync if needed
                        await _lookupService.OptimizePerformanceAsync();
                    }
                }
                else
                {
                    _logger.LogDebug("Health check passed: Database healthy with {Records:N0} records", healthResult.TotalRecords);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed");
            }
        }
        #endregion

        #region Cleanup
        public override void Dispose()
        {
            try
            {
                _processingCancellation?.Cancel();
                _processingCancellation?.Dispose();

                // Unsubscribe from events
                if (_processingService != null)
                {
                    _processingService.ProcessingStageChanged -= OnProcessingStageChanged;
                    _processingService.ProcessingError -= OnProcessingError;
                }

                _logger.LogInformation("DocumentProcessingViewModel disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during ViewModel disposal");
            }

            base.Dispose();
        }
        #endregion
    }

    /// <summary>
    /// Processing log entry for UI display
    /// </summary>
    public class ProcessingLogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");
        public string LevelString => Level.ToString().ToUpper();
    }
}