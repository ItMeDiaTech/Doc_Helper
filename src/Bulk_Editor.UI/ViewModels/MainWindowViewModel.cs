using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Dialogs;
using Serilog;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Microsoft.Win32; // Explicitly added for OpenFileDialog and SaveFileDialog
using Doc_Helper.Shared.Configuration;

namespace Doc_Helper.UI.ViewModels
{
    /// <summary>
    /// Main window view model implementing MVVM pattern with Prism
    /// </summary>
    public class MainWindowViewModel : BindableBase
    {
        private readonly IDocumentProcessingService _documentProcessingService;
        private readonly IConfigurationService _configurationService;
        private readonly IValidationService _validationService;
        private readonly IDialogService _dialogService;
        private readonly IBackupService _backupService;
        private readonly IChangelogService _changelogService;
        private readonly IHyperlinkProcessingService _hyperlinkProcessingService;
        // TODO: Implement ILogViewerService in modern architecture
        // private readonly ILogViewerService _logViewerService; // Removed due to legacy
        // TODO: Implement IThemeService in modern architecture
        // private readonly IThemeService _themeService; // Removed due to legacy
        private readonly ILogger _logger;

        private CancellationTokenSource? _cancellationTokenSource;
        private Progress<ProcessingProgressReport>? _progressReporter;
        private bool _isLoadingOptions;

        #region Properties

        private string _title = "Doc Helper v3.0";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private ObservableCollection<string> _selectedFiles = new();
        public ObservableCollection<string> SelectedFiles
        {
            get => _selectedFiles;
            set => SetProperty(ref _selectedFiles, value);
        }

        // Alias for backward compatibility
        public ObservableCollection<string> FileList => SelectedFiles;

        private string _selectedFile = string.Empty;
        public string SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (SetProperty(ref _selectedFile, value))
                {
                    RaisePropertyChanged(nameof(HasSelectedFile));
                    UpdateChangelogVisibility();
                }
            }
        }

        private string _folderPath = string.Empty;
        public string FolderPath
        {
            get => _folderPath;
            set => SetProperty(ref _folderPath, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    RaisePropertyChanged(nameof(CanStartProcessing));
                    RaisePropertyChanged(nameof(CanCancelProcessing));
                    ((DelegateCommand)RunToolsCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)CancelCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private string _progressStatus = "Ready";
        public string ProgressStatus
        {
            get => _progressStatus;
            set => SetProperty(ref _progressStatus, value);
        }

        private string _changelogText = string.Empty;
        public string ChangelogText
        {
            get => _changelogText;
            set => SetProperty(ref _changelogText, value);
        }

        private bool _changelogVisible;
        public bool ChangelogVisible
        {
            get => _changelogVisible;
            set => SetProperty(ref _changelogVisible, value);
        }

        // Processing Options
        private bool _fixSourceHyperlinks = true;
        public bool FixSourceHyperlinks
        {
            get => _fixSourceHyperlinks;
            set
            {
                if (SetProperty(ref _fixSourceHyperlinks, value))
                {
                    UpdateSubCheckboxStates();
                    if (!_isLoadingOptions)
                        SaveProcessingOptions();
                }
            }
        }

        private bool _appendContentID;
        public bool AppendContentID
        {
            get => _appendContentID;
            set
            {
                if (SetProperty(ref _appendContentID, value))
                    SaveProcessingOptions();
            }
        }

        private bool _checkTitleChanges;
        public bool CheckTitleChanges
        {
            get => _checkTitleChanges;
            set
            {
                if (SetProperty(ref _checkTitleChanges, value))
                    SaveProcessingOptions();
            }
        }

        private bool _fixTitles;
        public bool FixTitles
        {
            get => _fixTitles;
            set
            {
                if (SetProperty(ref _fixTitles, value))
                    SaveProcessingOptions();
            }
        }

        private bool _fixInternalHyperlink = true;
        public bool FixInternalHyperlink
        {
            get => _fixInternalHyperlink;
            set
            {
                if (SetProperty(ref _fixInternalHyperlink, value))
                    SaveProcessingOptions();
            }
        }

        private bool _fixDoubleSpaces = true;
        public bool FixDoubleSpaces
        {
            get => _fixDoubleSpaces;
            set
            {
                if (SetProperty(ref _fixDoubleSpaces, value))
                    SaveProcessingOptions();
            }
        }

        private bool _replaceHyperlink;
        public bool ReplaceHyperlink
        {
            get => _replaceHyperlink;
            set
            {
                if (SetProperty(ref _replaceHyperlink, value))
                    SaveProcessingOptions();
            }
        }

        private bool _openChangelogAfterUpdates;
        public bool OpenChangelogAfterUpdates
        {
            get => _openChangelogAfterUpdates;
            set
            {
                if (SetProperty(ref _openChangelogAfterUpdates, value))
                    SaveProcessingOptions();
            }
        }

        private bool _replaceText;
        public bool ReplaceText
        {
            get => _replaceText;
            set
            {
                if (SetProperty(ref _replaceText, value))
                    SaveProcessingOptions();
            }
        }

        // Backup and Undo functionality
        private Dictionary<string, string> _backupPaths = new();
        public Dictionary<string, string> BackupPaths => _backupPaths;

        private bool _canUndo;
        public bool CanUndo
        {
            get => _canUndo;
            set => SetProperty(ref _canUndo, value);
        }

        // Missing properties for XAML bindings
        private ObservableCollection<ProcessingResultSummary> _processingResults = new();
        public ObservableCollection<ProcessingResultSummary> ProcessingResults
        {
            get => _processingResults;
            set => SetProperty(ref _processingResults, value);
        }

        private ProcessingResultSummary? _selectedResult;
        public ProcessingResultSummary? SelectedResult
        {
            get => _selectedResult;
            set => SetProperty(ref _selectedResult, value);
        }

        private ObservableCollection<LogEntry> _processingLog = new();
        public ObservableCollection<LogEntry> ProcessingLog
        {
            get => _processingLog;
            set => SetProperty(ref _processingLog, value);
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isProgressIndeterminate;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public string FilesCountText => $"{SelectedFiles.Count} files selected";
        public string ProcessingStatusText => IsProcessing ? "Processing..." : "Ready";
        public string ProgressText => IsProcessing ? $"{ProgressValue:F1}%" : "Ready";
        public double ProgressPercentage => ProgressValue;

        public bool HasSelectedFile => !string.IsNullOrEmpty(SelectedFile);
        public bool CanStartProcessing => !IsProcessing && SelectedFiles.Count > 0;
        public bool CanCancelProcessing => IsProcessing;
        public bool CanProcess => CanStartProcessing;

        #endregion

        #region Commands

        // File Menu Commands
        public ICommand NewProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand ExitCommand { get; }

        // Tools Menu Commands
        public ICommand OpenSettingsCommand { get; }
        public ICommand ViewLogsCommand { get; }

        // Help Menu Commands
        public ICommand ShowAboutCommand { get; }

        // Main Action Commands
        public ICommand SelectFolderCommand { get; }
        public ICommand SelectFilesCommand { get; }
        public ICommand RunToolsCommand { get; }
        public ICommand ProcessDocumentsCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ConfigureReplaceHyperlinkCommand { get; }
        public ICommand ClearFilesCommand { get; }
        public ICommand OpenFileLocationCommand { get; }
        public ICommand ExportChangelogCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RestoreBackupCommand { get; }

        #endregion

        public MainWindowViewModel(
            IDocumentProcessingService documentProcessingService,
            IConfigurationService configurationService,
            IValidationService validationService,
            IDialogService dialogService,
            IBackupService backupService,
            IChangelogService changelogService,
            IHyperlinkProcessingService hyperlinkProcessingService,
            Serilog.ILogger logger)
        {
            try
            {
                _documentProcessingService = documentProcessingService ?? throw new ArgumentNullException(nameof(documentProcessingService));
                _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
                _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
                _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
                _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
                _changelogService = changelogService ?? throw new ArgumentNullException(nameof(changelogService));
                _hyperlinkProcessingService = hyperlinkProcessingService ?? throw new ArgumentNullException(nameof(hyperlinkProcessingService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"MainWindowViewModel construction failed: {ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "ViewModel Construction Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                throw;
            }

            // Initialize File Menu commands
            NewProjectCommand = new DelegateCommand(ExecuteNewProject);
            OpenProjectCommand = new DelegateCommand(ExecuteOpenProject);
            ExitCommand = new DelegateCommand(ExecuteExit);

            // Initialize Tools Menu commands
            OpenSettingsCommand = new DelegateCommand(ExecuteOpenSettings);
            ViewLogsCommand = new DelegateCommand(ExecuteViewLogs);

            // Initialize Help Menu commands
            ShowAboutCommand = new DelegateCommand(ExecuteShowAbout);

            // Initialize main action commands
            SelectFolderCommand = new DelegateCommand(ExecuteSelectFolder);
            SelectFilesCommand = new DelegateCommand(ExecuteSelectFiles);
            RunToolsCommand = new DelegateCommand(async () => await ExecuteRunToolsAsync(), () => CanStartProcessing)
                .ObservesProperty(() => IsProcessing)
                .ObservesProperty(() => SelectedFiles.Count);
            ProcessDocumentsCommand = RunToolsCommand; // Alias for XAML binding
            CancelCommand = new DelegateCommand(ExecuteCancel, () => CanCancelProcessing)
                .ObservesProperty(() => IsProcessing);
            ConfigureReplaceHyperlinkCommand = new DelegateCommand(ExecuteConfigureReplaceHyperlink);
            ClearFilesCommand = new DelegateCommand(ExecuteClearFiles);
            OpenFileLocationCommand = new DelegateCommand(ExecuteOpenFileLocation, () => HasSelectedFile)
                .ObservesProperty(() => SelectedFile);
            ExportChangelogCommand = new DelegateCommand(ExecuteExportChangelog);
            UndoCommand = new DelegateCommand(async () => await ExecuteUndoAsync(), () => CanUndo)
                .ObservesProperty(() => CanUndo);
            RestoreBackupCommand = new DelegateCommand<string>(async (path) => await ExecuteRestoreBackupAsync(path));

            // Load configuration
            LoadProcessingOptions();

            // Setup progress reporting
            SetupProgressReporting();
        }

        private void SetupProgressReporting()
        {
            _progressReporter = new Progress<ProcessingProgressReport>(UpdateProgress);
        }

        private void UpdateProgress(ProcessingProgressReport report)
        {
            ProgressStatus = report.Message;
            StatusMessage = report.Message;

            if (report.TotalCount > 0)
            {
                ProgressValue = (double)report.ProcessedCount / report.TotalCount * 100;
                IsProgressIndeterminate = false;
            }
            else
            {
                IsProgressIndeterminate = true;
            }

            // Add to processing log
            ProcessingLog.Add(new LogEntry
            {
                Timestamp = report.Timestamp,
                Level = "INFO",
                Message = $"{report.CurrentFile}: {report.Message}"
            });

            // Update changelog if processing
            if (report.Stage == ProcessingStage.Completion)
            {
                AppendToChangelog($"[{report.Timestamp:HH:mm:ss}] {report.CurrentFile}: {report.Message}");
            }

            // Notify UI of dependent property changes
            RaisePropertyChanged(nameof(FilesCountText));
            RaisePropertyChanged(nameof(ProcessingStatusText));
            RaisePropertyChanged(nameof(ProgressText));
            RaisePropertyChanged(nameof(ProgressPercentage));
        }

        private void LoadProcessingOptions()
        {
            _isLoadingOptions = true;
            try
            {
                var uiOptions = _configurationService.UiOptions;
                FixSourceHyperlinks = uiOptions.FixSourceHyperlinks;
                AppendContentID = uiOptions.AppendContentID;
                CheckTitleChanges = uiOptions.CheckTitleChanges;
                FixTitles = uiOptions.FixTitles;
                FixInternalHyperlink = uiOptions.FixInternalHyperlink;
                FixDoubleSpaces = uiOptions.FixDoubleSpaces;
                ReplaceHyperlink = uiOptions.ReplaceHyperlink;
                OpenChangelogAfterUpdates = uiOptions.OpenChangelogAfterUpdates;
            }
            finally
            {
                _isLoadingOptions = false;
            }
        }

        private void SaveProcessingOptions()
        {
            if (_isLoadingOptions) return;

            var uiOptions = _configurationService.UiOptions;
            uiOptions.FixSourceHyperlinks = FixSourceHyperlinks;
            uiOptions.AppendContentID = AppendContentID;
            uiOptions.CheckTitleChanges = CheckTitleChanges;
            uiOptions.FixTitles = FixTitles;
            uiOptions.FixInternalHyperlink = FixInternalHyperlink;
            uiOptions.FixDoubleSpaces = FixDoubleSpaces;
            uiOptions.ReplaceHyperlink = ReplaceHyperlink;
            uiOptions.OpenChangelogAfterUpdates = OpenChangelogAfterUpdates;

            // Save configuration would be handled by the configuration service
            _logger.Debug("Processing options saved");
        }

        private void UpdateSubCheckboxStates()
        {
            if (!FixSourceHyperlinks)
            {
                var wasLoading = _isLoadingOptions;
                _isLoadingOptions = true;
                try
                {
                    AppendContentID = false;
                    CheckTitleChanges = false;
                    FixTitles = false;
                }
                finally
                {
                    _isLoadingOptions = wasLoading;
                }
            }
        }

        private void UpdateChangelogVisibility()
        {
            if (HasSelectedFile && !string.IsNullOrEmpty(ChangelogText))
            {
                ChangelogVisible = true;
            }
        }

        private void ExecuteSelectFolder()
        {
            // Use OpenFileDialog as a workaround for folder selection in pure WPF
            var dialog = new OpenFileDialog
            {
                Title = "Select any file in the folder containing Word documents",
                Filter = "All Files (*.*)|*.*",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            if (!string.IsNullOrEmpty(FolderPath))
            {
                dialog.InitialDirectory = FolderPath;
            }

            if (dialog.ShowDialog() == true)
            {
                var selectedPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
                {
                    FolderPath = selectedPath;
                    LoadFilesFromFolder(selectedPath);
                }
            }
        }

        private void ExecuteSelectFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Word Documents",
                Filter = "Word Documents (*.docx)|*.docx|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedFiles.Clear();
                foreach (var file in dialog.FileNames)
                {
                    SelectedFiles.Add(file);
                }

                if (SelectedFiles.Count > 0)
                {
                    FolderPath = Path.GetDirectoryName(SelectedFiles[0]) ?? string.Empty;
                }

                _logger.Information("Loaded {Count} files", SelectedFiles.Count);
                RaisePropertyChanged(nameof(FilesCountText));
                RaisePropertyChanged(nameof(CanStartProcessing));
                RaisePropertyChanged(nameof(CanProcess));
            }
        }

        private async Task ExecuteRunToolsAsync()
        {
            if (SelectedFiles.Count == 0)
            {
                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                {
                    { "title", "No Files Selected" },
                    { "message", "Please select files to process." }
                }, (Action<IDialogResult>?)null);
                return;
            }

            try
            {
                IsProcessing = true;
                ProgressValue = 0;
                ProgressStatus = "Starting processing...";
                ChangelogText = string.Empty;
                ProcessingResults.Clear();
                ProcessingLog.Clear();

                _cancellationTokenSource = new CancellationTokenSource();

                // Validate documents first
                ProgressStatus = "Validating documents...";
                var validationSummary = await _documentProcessingService.ValidateDocumentsAsync(
                    SelectedFiles, _cancellationTokenSource.Token);

                if (validationSummary.InvalidFiles > 0)
                {
                    var result = await ShowValidationWarningAsync(validationSummary);
                    if (!result)
                    {
                        IsProcessing = false;
                        return;
                    }
                }

                // Create backups if enabled
                var processingOptions = _configurationService.ProcessingOptions;
                if (processingOptions.EnableAutoBackup)
                {
                    ProgressStatus = "Creating backups...";
                    try
                    {
                        var backupResults = await _backupService.CreateBackupsAsync(SelectedFiles);
                        BackupPaths.Clear();
                        foreach (var kvp in backupResults)
                        {
                            BackupPaths[kvp.Key] = kvp.Value;
                        }
                        CanUndo = BackupPaths.Count > 0;
                        
                        _logger.Information("Created {Count} backup files", BackupPaths.Count);
                        ProgressStatus = $"Created {BackupPaths.Count} backup files";
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to create backups");
                        var tcs = new TaskCompletionSource<bool>();
                        _dialogService.ShowDialog("ConfirmDialog", new DialogParameters
                        {
                            { "title", "Backup Failed" },
                            { "message", $"Failed to create backups: {ex.Message}\n\nContinue without backup?" }
                        }, result =>
                        {
                            tcs.SetResult(result.Result == ButtonResult.OK);
                        });

                        if (!await tcs.Task)
                        {
                            IsProcessing = false;
                            return;
                        }
                    }
                }

                // Process documents with individual changelogs
                ProgressStatus = "Processing documents...";
                var batchResults = new BatchProcessingResults
                {
                    BatchStartTime = DateTime.UtcNow,
                    TotalDocuments = SelectedFiles.Count
                };

                int processedCount = 0;
                var successfulFiles = new List<string>();
                var failedFiles = new List<string>();
                var overallChangelog = new System.Text.StringBuilder();

                foreach (var filePath in SelectedFiles)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    try
                    {
                        ProgressStatus = $"Processing {Path.GetFileName(filePath)}...";
                        ProgressValue = (double)processedCount / SelectedFiles.Count * 100;

                        // Create document processing results
                        var docResults = new DocumentProcessingResults
                        {
                            DocumentPath = filePath,
                            ProcessingStartTime = DateTime.UtcNow
                        };

                        // Process the document (simplified - you would call actual processing services)
                        var processingResult = await _documentProcessingService.ProcessSingleDocumentAsync(
                            filePath, null, _cancellationTokenSource.Token);

                        docResults.ProcessingEndTime = DateTime.UtcNow;
                        docResults.BackupCreated = BackupPaths.ContainsKey(filePath);
                        docResults.BackupPath = BackupPaths.GetValueOrDefault(filePath);

                        // Create individual changelog
                        var changelog = await _changelogService.CreateDocumentChangelogAsync(filePath, docResults);
                        overallChangelog.AppendLine(changelog);
                        overallChangelog.AppendLine();

                        // Save individual changelog if enabled
                        if (_configurationService.UiOptions.ShowIndividualChangelogs)
                        {
                            var changelogPath = _changelogService.GetChangelogPath(filePath);
                            await _changelogService.SaveChangelogAsync(changelog, changelogPath);
                        }

                        batchResults.DocumentResults.Add(docResults);
                        successfulFiles.Add(filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to process document {FilePath}", filePath);
                        failedFiles.Add(filePath);
                        batchResults.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                    }

                    processedCount++;
                    ProgressValue = (double)processedCount / SelectedFiles.Count * 100;
                }

                batchResults.BatchEndTime = DateTime.UtcNow;
                batchResults.SuccessfulDocuments = successfulFiles.Count;
                batchResults.FailedDocuments = failedFiles.Count;

                // Create batch changelog
                if (successfulFiles.Count > 1)
                {
                    var batchChangelog = await _changelogService.CreateBatchChangelogAsync(batchResults);
                    ChangelogText = batchChangelog;

                    // Export to downloads if enabled
                    if (_configurationService.UiOptions.SaveChangelogToDownloads)
                    {
                        try
                        {
                            await _changelogService.ExportChangelogToDownloadsAsync(batchChangelog);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Failed to export changelog to downloads");
                        }
                    }
                }
                else if (successfulFiles.Count == 1)
                {
                    ChangelogText = overallChangelog.ToString();
                }

                // Show results
                ShowBatchProcessingResults(batchResults, successfulFiles, failedFiles);

                if (OpenChangelogAfterUpdates && !string.IsNullOrEmpty(ChangelogText))
                {
                    ChangelogVisible = true;
                }

                ProgressStatus = "Processing completed";
                ProgressValue = 100;
            }
            catch (OperationCanceledException)
            {
                ProgressStatus = "Processing cancelled";
                _logger.Information("Processing cancelled by user");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Processing failed");
                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                {
                    { "title", "Processing Error" },
                    { "message", $"An error occurred during processing: {ex.Message}" }
                }, (Action<IDialogResult>?)null);
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void ExecuteCancel()
        {
            _cancellationTokenSource?.Cancel();
            ProgressStatus = "Cancelling...";
        }

        private void ExecuteOpenSettings()
        {
            try
            {
                // Create and show settings dialog directly
                var settingsViewModel = new Doc_Helper.UI.ViewModels.Dialogs.SettingsDialogViewModel(_configurationService, _logger);
                var settingsDialog = new Doc_Helper.UI.Views.Dialogs.SettingsDialog(settingsViewModel);
                
                settingsDialog.Owner = Application.Current.MainWindow;
                var result = settingsDialog.ShowDialog();
                
                if (result == true)
                {
                    LoadProcessingOptions();
                    _logger.Information("Settings updated, reloading processing options");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to open settings dialog");
                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                {
                    { "title", "Settings Error" },
                    { "message", $"Failed to open settings dialog: {ex.Message}" }
                }, (Action<IDialogResult>?)null);
            }
        }

        private void ExecuteConfigureReplaceHyperlink()
        {
            _dialogService.ShowDialog("ReplaceHyperlinkConfigDialog", new DialogParameters(), (Action<IDialogResult>?)null);
        }

        private void ExecuteClearFiles()
        {
            SelectedFiles.Clear();
            ProcessingResults.Clear();
            ProcessingLog.Clear();
            FolderPath = string.Empty;
            SelectedFile = string.Empty;
            ChangelogText = string.Empty;
            ChangelogVisible = false;
            ProgressValue = 0;
            ProgressStatus = "Ready";
            StatusMessage = "Ready";
            IsProgressIndeterminate = false;

            RaisePropertyChanged(nameof(FilesCountText));
            RaisePropertyChanged(nameof(ProcessingStatusText));
            RaisePropertyChanged(nameof(ProgressText));
            RaisePropertyChanged(nameof(CanStartProcessing));
            RaisePropertyChanged(nameof(CanProcess));
        }

        private void ExecuteOpenFileLocation()
        {
            if (!string.IsNullOrEmpty(SelectedFile) && File.Exists(SelectedFile))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{SelectedFile}\"");
            }
        }

        private void ExecuteViewLogs()
        {
            // TODO: Implement log viewer functionality
            // For now, open the log file location
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (Directory.Exists(logPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logPath);
                }
                else
                {
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters
                    {
                        { "title", "Logs Not Found" },
                        { "message", "Log directory not found. Please check your logging configuration." }
                    }, (Action<IDialogResult>?)null);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to open log viewer");
                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                    {
                        { "title", "Error" },
                        { "message", $"Failed to open logs: {ex.Message}" }
                    }, (Action<IDialogResult>?)null);
            }
        }

        private void ExecuteExportChangelog()
        {
            if (string.IsNullOrEmpty(ChangelogText))
            {
                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                {
                    { "title", "No Changelog" },
                    { "message", "There is no changelog to export." }
                }, (Action<IDialogResult>?)null);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Changelog",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"Changelog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, ChangelogText);
                _logger.Information("Changelog exported to {Path}", dialog.FileName);
            }
        }

        private void LoadFilesFromFolder(string folderPath)
        {
            SelectedFiles.Clear();

            if (Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath, "*.docx", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    SelectedFiles.Add(file);
                }

                _logger.Information("Loaded {Count} files from {Path}", SelectedFiles.Count, folderPath);
                RaisePropertyChanged(nameof(FilesCountText));
                RaisePropertyChanged(nameof(CanStartProcessing));
                RaisePropertyChanged(nameof(CanProcess));
            }
        }

        private async Task<bool> ShowValidationWarningAsync(DocumentValidationSummary summary)
        {
            var tcs = new TaskCompletionSource<bool>();

            _dialogService.ShowDialog("ConfirmDialog", new DialogParameters
            {
                { "title", "Validation Warning" },
                { "message", $"{summary.InvalidFiles} files failed validation. Continue with valid files only?" }
            }, result =>
            {
                tcs.SetResult(result.Result == ButtonResult.OK);
            });

            return await tcs.Task;
        }

        private void ShowProcessingResults(DocumentProcessingResult result)
        {
            // Add result to processing results collection
            var summary = new ProcessingResultSummary
            {
                FileName = result.ProcessedFiles.Count > 0 ? Path.GetFileName(result.ProcessedFiles[0]) : "Multiple Files",
                Status = result.Success ? "Success" : "Failed",
                HyperlinkCount = result.TotalHyperlinksProcessed,
                UpdatedCount = result.TotalHyperlinksUpdated,
                ErrorCount = result.FailedFiles,
                ProcessingTime = result.ProcessingDuration.ToString(@"hh\:mm\:ss")
            };

            ProcessingResults.Add(summary);

            var message = result.Success
                ? $"Processing completed successfully.\n\nProcessed: {result.SuccessfulFiles}/{result.TotalFiles} files\nDuration: {result.ProcessingDuration.TotalSeconds:F2} seconds"
                : $"Processing failed.\n\nError: {result.ErrorMessage}";

            StatusMessage = result.Success ? "Processing completed" : "Processing failed";
            ProgressStatus = StatusMessage;

            _dialogService.ShowDialog("MessageDialog", new DialogParameters
            {
                { "title", result.Success ? "Processing Complete" : "Processing Failed" },
                { "message", message }
            }, (Action<IDialogResult>?)null);
        }

        private void ShowBatchProcessingResults(BatchProcessingResults batchResults, List<string> successfulFiles, List<string> failedFiles)
        {
            // Add results to processing results collection
            foreach (var docResult in batchResults.DocumentResults)
            {
                var summary = new ProcessingResultSummary
                {
                    FileName = Path.GetFileName(docResult.DocumentPath),
                    Status = successfulFiles.Contains(docResult.DocumentPath) ? "Success" : "Failed",
                    HyperlinkCount = 0, // TODO: Extract from docResult when available
                    UpdatedCount = 0,   // TODO: Extract from docResult when available
                    ErrorCount = failedFiles.Contains(docResult.DocumentPath) ? 1 : 0,
                    ProcessingTime = (docResult.ProcessingEndTime - docResult.ProcessingStartTime).ToString(@"hh\:mm\:ss")
                };
                ProcessingResults.Add(summary);
            }

            var duration = batchResults.BatchEndTime - batchResults.BatchStartTime;
            var message = failedFiles.Count == 0
                ? $"Batch processing completed successfully.\n\nProcessed: {successfulFiles.Count}/{batchResults.TotalDocuments} files\nDuration: {duration.TotalSeconds:F2} seconds"
                : $"Batch processing completed with errors.\n\nSuccessful: {successfulFiles.Count}\nFailed: {failedFiles.Count}\nTotal: {batchResults.TotalDocuments}\nDuration: {duration.TotalSeconds:F2} seconds";

            StatusMessage = failedFiles.Count == 0 ? "Processing completed" : "Processing completed with errors";
            ProgressStatus = StatusMessage;

            _dialogService.ShowDialog("MessageDialog", new DialogParameters
            {
                { "title", failedFiles.Count == 0 ? "Processing Complete" : "Processing Completed with Errors" },
                { "message", message }
            }, (Action<IDialogResult>?)null);
        }

        private void AppendToChangelog(string text)
        {
            ChangelogText += text + Environment.NewLine;
        }

        #region Menu Command Handlers

        private void ExecuteNewProject()
        {
            // Clear current project and reset to initial state
            ExecuteClearFiles();
            _logger.Information("New project created");
        }

        private void ExecuteOpenProject()
        {
            // For now, this just opens the folder selection dialog
            ExecuteSelectFolder();
        }

        private void ExecuteExit()
        {
            Application.Current.Shutdown();
        }

        private void ExecuteShowAbout()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters
            {
                { "title", "About Doc Helper" },
                { "message", $"Doc Helper v3.0\n\nA modern WPF application for bulk document processing.\n\nBuilt with:\n• .NET 9\n• WPF\n• Prism MVVM\n• Entity Framework Core\n• Serilog" }
            }, (Action<IDialogResult>?)null);
        }

        private async Task ExecuteUndoAsync()
        {
            try
            {
                if (!CanUndo || BackupPaths.Count == 0)
                {
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters
                    {
                        { "title", "No Backups" },
                        { "message", "There are no backups available to restore." }
                    }, (Action<IDialogResult>?)null);
                    return;
                }

                var tcs = new TaskCompletionSource<bool>();
                _dialogService.ShowDialog("ConfirmDialog", new DialogParameters
                {
                    { "title", "Confirm Undo" },
                    { "message", $"This will restore {BackupPaths.Count} files from their backups. Continue?" }
                }, result =>
                {
                    tcs.SetResult(result.Result == ButtonResult.OK);
                });

                if (!await tcs.Task) return;

                IsProcessing = true;
                ProgressStatus = "Restoring files from backup...";
                
                int restored = 0;
                int total = BackupPaths.Count;
                var errors = new List<string>();

                foreach (var kvp in BackupPaths.ToList())
                {
                    try
                    {
                        await _backupService.RestoreFromBackupAsync(kvp.Key, kvp.Value);
                        restored++;
                        ProgressValue = (double)restored / total * 100;
                        _logger.Information("Restored {File} from backup", kvp.Key);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{Path.GetFileName(kvp.Key)}: {ex.Message}");
                        _logger.Error(ex, "Failed to restore {File} from backup", kvp.Key);
                    }
                }

                // Clear backup paths after successful restore
                BackupPaths.Clear();
                CanUndo = false;

                var message = errors.Count == 0
                    ? $"Successfully restored {restored} files from backup."
                    : $"Restored {restored} files. {errors.Count} files failed:\n\n{string.Join("\n", errors)}";

                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                {
                    { "title", errors.Count == 0 ? "Restore Complete" : "Restore Completed with Errors" },
                    { "message", message }
                }, (Action<IDialogResult>?)null);

                ProgressStatus = "Ready";
                ProgressValue = 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to execute undo operation");
                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                {
                    { "title", "Undo Failed" },
                    { "message", $"Failed to restore files: {ex.Message}" }
                }, (Action<IDialogResult>?)null);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ExecuteRestoreBackupAsync(string backupPath)
        {
            try
            {
                if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
                {
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters
                    {
                        { "title", "Backup Not Found" },
                        { "message", "The specified backup file was not found." }
                    }, (Action<IDialogResult>?)null);
                    return;
                }

                var originalFile = BackupPaths.FirstOrDefault(x => x.Value == backupPath).Key;
                if (string.IsNullOrEmpty(originalFile))
                {
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters
                    {
                        { "title", "Original File Not Found" },
                        { "message", "Could not determine the original file for this backup." }
                    }, (Action<IDialogResult>?)null);
                    return;
                }

                var tcs = new TaskCompletionSource<bool>();
                _dialogService.ShowDialog("ConfirmDialog", new DialogParameters
                {
                    { "title", "Confirm Restore" },
                    { "message", $"Restore {Path.GetFileName(originalFile)} from backup?" }
                }, result =>
                {
                    tcs.SetResult(result.Result == ButtonResult.OK);
                });

                if (!await tcs.Task) return;

                await _backupService.RestoreFromBackupAsync(originalFile, backupPath);
                
                // Remove from backup paths
                BackupPaths.Remove(originalFile);
                if (BackupPaths.Count == 0)
                {
                    CanUndo = false;
                }

                _logger.Information("Restored {File} from backup {BackupPath}", originalFile, backupPath);
                
                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                {
                    { "title", "Restore Complete" },
                    { "message", $"Successfully restored {Path.GetFileName(originalFile)} from backup." }
                }, (Action<IDialogResult>?)null);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to restore file from backup {BackupPath}", backupPath);
                _dialogService.ShowDialog("MessageDialog", new DialogParameters
                {
                    { "title", "Restore Failed" },
                    { "message", $"Failed to restore file: {ex.Message}" }
                }, (Action<IDialogResult>?)null);
            }
        }

        #endregion
    }

    /// <summary>
    /// Model for processing result summary display
    /// </summary>
    public class ProcessingResultSummary
    {
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int HyperlinkCount { get; set; }
        public int UpdatedCount { get; set; }
        public int ErrorCount { get; set; }
        public string ProcessingTime { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model for log entry display
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}