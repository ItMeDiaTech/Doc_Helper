using System;
using System.Collections.Generic;

namespace Doc_Helper.Core.Models
{
    /// <summary>
    /// Processing stages enumeration
    /// </summary>
    public enum ProcessingStage
    {
        Initialization,
        FileValidation,
        HyperlinkExtraction,
        HyperlinkProcessing,
        ApiProcessing,
        DocumentUpdate,
        Completion,
        Error
    }

    /// <summary>
    /// Progress report for processing operations
    /// </summary>
    public class ProcessingProgressReport
    {
        public ProcessingStage Stage { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int ProcessedCount { get; set; }
        public int TotalCount { get; set; }
        public double ProgressPercentage => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100 : 0;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan ElapsedTime { get; set; }
        public bool IsCompleted { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
        public Guid ProcessingId { get; set; }
    }

    /// <summary>
    /// Document processing result
    /// </summary>
    public class DocumentProcessingResult
    {
        public int TotalFiles { get; set; }
        public int SuccessfulFiles { get; set; }
        public int FailedFiles { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public List<string> ProcessedFiles { get; set; } = new();
        public List<string> FailedFilesList { get; set; } = new();
        public int TotalHyperlinksProcessed { get; set; }
        public int TotalHyperlinksUpdated { get; set; }
        public double SuccessRate => TotalFiles > 0 ? (double)SuccessfulFiles / TotalFiles * 100 : 0;
    }

    /// <summary>
    /// Single document processing result
    /// </summary>
    public class SingleDocumentProcessingResult
    {
        public string FilePath { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
        public int HyperlinksProcessed { get; set; }
        public int HyperlinksUpdated { get; set; }
        public List<string> ChangesSummary { get; set; } = new();
    }


    /// <summary>
    /// Processing statistics
    /// </summary>
    public class ProcessingStatistics
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int FailedFiles { get; set; }
        public int TotalHyperlinks { get; set; }
        public int ProcessedHyperlinks { get; set; }
        public int UpdatedHyperlinks { get; set; }
        public int ApiCallsMade { get; set; }
        public int SuccessfulApiCalls { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public TimeSpan TotalApiTime { get; set; }
        public DateTime LastProcessingTime { get; set; }

        public ProcessingStatistics Clone() => new()
        {
            TotalFiles = TotalFiles,
            ProcessedFiles = ProcessedFiles,
            FailedFiles = FailedFiles,
            TotalHyperlinks = TotalHyperlinks,
            ProcessedHyperlinks = ProcessedHyperlinks,
            UpdatedHyperlinks = UpdatedHyperlinks,
            ApiCallsMade = ApiCallsMade,
            SuccessfulApiCalls = SuccessfulApiCalls,
            TotalProcessingTime = TotalProcessingTime,
            TotalApiTime = TotalApiTime,
            LastProcessingTime = LastProcessingTime
        };
    }

    /// <summary>
    /// Pipeline configuration
    /// </summary>
    public class PipelineConfiguration
    {
        public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
        public int BoundedCapacity { get; set; } = 100;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableDetailedLogging { get; set; } = true;
        public int ApiBatchSize { get; set; } = 50;
    }

    /// <summary>
    /// Processing stage changed event arguments
    /// </summary>
    public class ProcessingStageChangedEventArgs : EventArgs
    {
        public ProcessingStage PreviousStage { get; set; }
        public ProcessingStage CurrentStage { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Processing error event arguments
    /// </summary>
    public class ProcessingErrorEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public ProcessingStage Stage { get; set; }
        public Exception? Exception { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Hyperlink update result
    /// </summary>
    public class HyperlinkUpdateResult
    {
        public bool Success { get; set; }
        public int UpdatedCount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public List<string> UpdatedHyperlinks { get; set; } = new();
    }

    /// <summary>
    /// Backup operation result
    /// </summary>
    public class BackupResult
    {
        public bool Success { get; set; }
        public string BackupPath { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public long BackupSize { get; set; }
    }

    /// <summary>
    /// API response model
    /// </summary>
    public class ApiResponse
    {
        public bool Success { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public int ResultCount { get; set; }
    }

    /// <summary>
    /// Batch API response model
    /// </summary>
    public class BatchApiResponse
    {
        public bool Success { get; set; }
        public List<ApiResponse> Responses { get; set; } = new();
        public int TotalBatches { get; set; }
        public int SuccessfulBatches { get; set; }
        public TimeSpan TotalResponseTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// API progress report
    /// </summary>
    public class ApiProgressReport
    {
        public int CompletedBatches { get; set; }
        public int TotalBatches { get; set; }
        public int ProcessedItems { get; set; }
        public int TotalItems { get; set; }
        public double ProgressPercentage => TotalBatches > 0 ? (double)CompletedBatches / TotalBatches * 100 : 0;
        public TimeSpan ElapsedTime { get; set; }
        public string CurrentBatch { get; set; } = string.Empty;
    }

    /// <summary>
    /// API validation result
    /// </summary>
    public class ApiValidationResult
    {
        public bool Success { get; set; }
        public bool IsValid { get; set; }
        public bool IsHealthy { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool SampleResponseReceived { get; set; }
        public bool ResponseFormatValid { get; set; }
        public DateTime ValidationStartTime { get; set; }
        public DateTime ValidationEndTime { get; set; }
        public TimeSpan ValidationDuration => ValidationEndTime - ValidationStartTime;
        public TimeSpan ResponseTime { get; set; }
        public string ApiVersion { get; set; } = string.Empty;
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// API health status
    /// </summary>
    public class ApiHealthStatus
    {
        public bool IsHealthy { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, object> Metrics { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Hyperlink processing result
    /// </summary>
    public class HyperlinkProcessingResult
    {
        public bool Success { get; set; }
        public List<HyperlinkData> ProcessedHyperlinks { get; set; } = new();
        public List<string> LookupIds { get; set; } = new();
        public int TotalProcessed { get; set; }
        public int UpdatedCount { get; set; }
        public int FailedCount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public DocumentExtractionResult ExtractionResult { get; set; } = new();
    }

    /// <summary>
    /// Document extraction result
    /// </summary>
    public class DocumentExtractionResult
    {
        public DocumentInput Input { get; set; } = new();
        public List<HyperlinkData> Hyperlinks { get; set; } = new();
        public DateTime ExtractionTimestamp { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public TimeSpan ExtractionDuration { get; set; }
    }

    /// <summary>
    /// Document input data
    /// </summary>
    public class DocumentInput
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public Guid ProcessingId { get; set; }
        public DateTime InputTimestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Internal link fix result
    /// </summary>
    public class InternalLinkFixResult
    {
        public bool Success { get; set; }
        public int FixedLinks { get; set; }
        public int BrokenLinks { get; set; }
        public List<string> FixedLinkDetails { get; set; } = new();
        public List<string> BrokenLinkDetails { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Title update result
    /// </summary>
    public class TitleUpdateResult
    {
        public bool Success { get; set; }
        public int UpdatedTitles { get; set; }
        public List<string> TitleChanges { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Title change information
    /// </summary>
    public class TitleChange
    {
        public string OldTitle { get; set; } = string.Empty;
        public string NewTitle { get; set; } = string.Empty;
        public string LookupId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Content ID append result
    /// </summary>
    public class ContentIdAppendResult
    {
        public bool Success { get; set; }
        public int AppendedCount { get; set; }
        public List<string> AppendedDetails { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Hyperlink cleanup result
    /// </summary>
    public class HyperlinkCleanupResult
    {
        public bool Success { get; set; }
        public int RemovedCount { get; set; }
        public List<string> RemovedHyperlinks { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Processing error information
    /// </summary>
    public class ProcessingErrorInfo
    {
        public string FileName { get; set; } = string.Empty;
        public ProcessingStage Stage { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string StackTrace { get; set; } = string.Empty;
    }

    /// <summary>
    /// Processing completion information
    /// </summary>
    public class ProcessingCompletionInfo
    {
        public int TotalFiles { get; set; }
        public int SuccessfulFiles { get; set; }
        public int FailedFiles { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public DateTime CompletionTime { get; set; } = DateTime.UtcNow;
        public double SuccessRate => TotalFiles > 0 ? (double)SuccessfulFiles / TotalFiles * 100 : 0;
    }

    /// <summary>
    /// Result of API processing for lookup IDs
    /// </summary>
    public class ApiProcessingResult
    {
        public HyperlinkProcessingResult HyperlinkResult { get; set; } = new();
        public Dictionary<string, object> ApiResults { get; set; } = new();
        public DateTime ApiCallTimestamp { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public TimeSpan ApiCallDuration { get; set; }
        public int SuccessfulLookups { get; set; }
        public int FailedLookups { get; set; }
    }

    /// <summary>
    /// Result of document update operations
    /// </summary>
    public class DocumentUpdateResult
    {
        public ApiProcessingResult ApiResult { get; set; } = new();
        public List<HyperlinkData> UpdatedHyperlinks { get; set; } = new();
        public DateTime UpdateTimestamp { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public TimeSpan UpdateDuration { get; set; }
        public int HyperlinksUpdated { get; set; }
        public int TitlesUpdated { get; set; }
        public int ContentIdsAppended { get; set; }
    }

    /// <summary>
    /// Result of fixing titles operation
    /// </summary>
    public class TitleFixResult
    {
        public bool Success { get; set; }
        public int FixedCount { get; set; }
        public List<string> FixedTitles { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Document processing statistics
    /// </summary>
    public class DocumentStatistics
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public int TotalHyperlinks { get; set; }
        public int ExternalHyperlinks { get; set; }
        public int InternalHyperlinks { get; set; }
        public int ValidLookupIds { get; set; }
        public DateTime StatisticsGeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// File size in megabytes
        /// </summary>
        public double FileSizeMB => FileSize / (1024.0 * 1024.0);

        /// <summary>
        /// Percentage of hyperlinks that have valid lookup IDs
        /// </summary>
        public double ValidLookupIdPercentage => TotalHyperlinks > 0 ? (double)ValidLookupIds / TotalHyperlinks * 100 : 0;
    }
}