using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces
{
    /// <summary>
    /// Modern document processing service interface using TPL Dataflow pipeline
    /// </summary>
    public interface IDocumentProcessingService
    {
        /// <summary>
        /// Processes multiple documents through the TPL Dataflow pipeline
        /// </summary>
        /// <param name="filePaths">Collection of file paths to process</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Pipeline execution result with statistics</returns>
        Task<DocumentProcessingResult> ProcessDocumentsAsync(
            IEnumerable<string> filePaths,
            IProgress<ProcessingProgressReport>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes a single document with detailed progress reporting
        /// </summary>
        /// <param name="filePath">Path to the document to process</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Single document processing result</returns>
        Task<SingleDocumentProcessingResult> ProcessSingleDocumentAsync(
            string filePath,
            IProgress<ProcessingProgressReport>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates documents before processing
        /// </summary>
        /// <param name="filePaths">Collection of file paths to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation results</returns>
        Task<DocumentValidationSummary> ValidateDocumentsAsync(
            IEnumerable<string> filePaths,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets processing statistics for completed operations
        /// </summary>
        /// <returns>Processing statistics</returns>
        ProcessingStatistics GetProcessingStatistics();

        /// <summary>
        /// Configures pipeline options for processing
        /// </summary>
        /// <param name="options">Pipeline configuration options</param>
        void ConfigurePipeline(PipelineConfiguration options);

        /// <summary>
        /// Cancels all ongoing processing operations
        /// </summary>
        Task CancelAllProcessingAsync();

        /// <summary>
        /// Event fired when processing stage changes
        /// </summary>
        event EventHandler<ProcessingStageChangedEventArgs> ProcessingStageChanged;

        /// <summary>
        /// Event fired when an error occurs during processing
        /// </summary>
        event EventHandler<ProcessingErrorEventArgs> ProcessingError;
    }

    /// <summary>
    /// Word document processor interface for hyperlink operations
    /// </summary>
    public interface IWordDocumentProcessor
    {
        /// <summary>
        /// Extracts hyperlinks from a Word document
        /// </summary>
        /// <param name="filePath">Path to the Word document</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of extracted hyperlinks</returns>
        Task<List<HyperlinkData>> ExtractHyperlinksAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates hyperlinks in a Word document
        /// </summary>
        /// <param name="filePath">Path to the Word document</param>
        /// <param name="hyperlinks">Updated hyperlinks to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Update operation result</returns>
        Task<HyperlinkUpdateResult> UpdateHyperlinksAsync(
            string filePath,
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates document accessibility and format
        /// </summary>
        /// <param name="filePath">Path to the document</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        Task<FileValidationResult> ValidateDocumentAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a backup of the document before processing
        /// </summary>
        /// <param name="filePath">Path to the document</param>
        /// <param name="backupPath">Path for the backup file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Backup operation result</returns>
        Task<BackupResult> CreateBackupAsync(
            string filePath,
            string? backupPath = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts lookup ID from hyperlink address and sub-address
        /// </summary>
        /// <param name="address">Hyperlink address</param>
        /// <param name="subAddress">Hyperlink sub-address</param>
        /// <returns>Extracted lookup ID or empty string if not found</returns>
        string ExtractLookupID(string address, string subAddress);

        /// <summary>
        /// Removes invisible external hyperlinks from the collection
        /// </summary>
        Task<HyperlinkCleanupResult> RemoveInvisibleLinksAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes hyperlinks by extracting lookup IDs and cleaning data
        /// </summary>
        Task<List<string>> ProcessHyperlinksForApiAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies API results to update hyperlink data
        /// </summary>
        Task<List<HyperlinkData>> ApplyApiResultsAsync(
            List<HyperlinkData> hyperlinks,
            Dictionary<string, object> apiResults,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fixes titles by removing status markers and cleaning up display text
        /// </summary>
        Task<TitleFixResult> FixTitlesAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes hyperlink replacement based on configured rules
        /// </summary>
        Task<HyperlinkReplacementResult> ReplaceHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            List<HyperlinkReplacementRule> replacementRules,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates hyperlink data integrity
        /// </summary>
        Task<HyperlinkValidationResult> ValidateHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets document processing statistics
        /// </summary>
        Task<DocumentStatistics> GetDocumentStatisticsAsync(
            string filePath,
            CancellationToken cancellationToken = default);
    }


    /// <summary>
    /// Progress reporting service interface
    /// </summary>
    public interface IProgressReportingService
    {
        /// <summary>
        /// Reports processing progress with detailed information
        /// </summary>
        /// <param name="progress">Progress information to report</param>
        void ReportProgress(ProcessingProgressReport progress);

        /// <summary>
        /// Reports stage change in processing
        /// </summary>
        /// <param name="stage">New processing stage</param>
        /// <param name="message">Stage change message</param>
        void ReportStageChange(ProcessingStage stage, string message);

        /// <summary>
        /// Reports error during processing
        /// </summary>
        /// <param name="error">Error information</param>
        void ReportError(ProcessingErrorInfo error);

        /// <summary>
        /// Reports completion of processing
        /// </summary>
        /// <param name="result">Final processing result</param>
        void ReportCompletion(ProcessingCompletionInfo result);

        /// <summary>
        /// Subscribes to progress updates
        /// </summary>
        /// <param name="callback">Progress callback</param>
        /// <returns>Subscription token for unsubscribing</returns>
        IDisposable SubscribeToProgress(Action<ProcessingProgressReport> callback);
    }
}