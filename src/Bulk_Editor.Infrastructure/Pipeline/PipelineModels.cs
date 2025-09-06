using System;
using System.Collections.Generic;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Infrastructure.Pipeline
{
    /// <summary>
    /// Configuration options for the document processing pipeline
    /// </summary>
    public class DocumentPipelineOptions
    {
        /// <summary>
        /// Maximum overall concurrency level for the pipeline
        /// </summary>
        public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Parallelism for file validation stage
        /// </summary>
        public int FileValidationParallelism { get; set; } = Environment.ProcessorCount * 2;

        /// <summary>
        /// Parallelism for hyperlink extraction stage
        /// </summary>
        public int ExtractionParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Parallelism for hyperlink processing stage
        /// </summary>
        public int HyperlinkProcessingParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Parallelism for API processing stage (limited to prevent API overload)
        /// </summary>
        public int ApiProcessingParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

        /// <summary>
        /// Parallelism for document update stage
        /// </summary>
        public int DocumentUpdateParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Parallelism for completion stage
        /// </summary>
        public int CompletionParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Bounded capacity for pipeline blocks to control memory usage
        /// </summary>
        public int BoundedCapacity { get; set; } = 100;

        /// <summary>
        /// Timeout for individual pipeline stages
        /// </summary>
        public TimeSpan StageTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Batch size for API calls
        /// </summary>
        public int ApiBatchSize { get; set; } = 50;

        /// <summary>
        /// Enable detailed progress reporting
        /// </summary>
        public bool EnableDetailedProgress { get; set; } = true;
    }

    /// <summary>
    /// Processing batch for efficient API calls
    /// </summary>
    public class HyperlinkProcessingBatch
    {
        public List<DocumentExtractionResult> Documents { get; set; } = new();
        public List<string> LookupIds { get; set; } = new();
        public int BatchSize => Documents.Count;
        public DateTime BatchTimestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Hyperlink processing statistics
    /// </summary>
    public class HyperlinkProcessingStats
    {
        public int TotalHyperlinks { get; set; }
        public int ProcessedHyperlinks { get; set; }
        public int UpdatedHyperlinks { get; set; }
        public int FailedHyperlinks { get; set; }
        public int InternalLinksFixed { get; set; }
        public int ExternalLinksUpdated { get; set; }
        public int TitlesUpdated { get; set; }
        public int ContentIdsAppended { get; set; }
        public int ApiCallsMade { get; set; }
        public int SuccessfulApiCalls { get; set; }
        public int FailedApiCalls { get; set; }
        public TimeSpan TotalApiTime { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }

        /// <summary>
        /// Success rate for hyperlink processing
        /// </summary>
        public double HyperlinkSuccessRate => TotalHyperlinks > 0
            ? (double)ProcessedHyperlinks / TotalHyperlinks * 100
            : 0;

        /// <summary>
        /// API success rate
        /// </summary>
        public double ApiSuccessRate => ApiCallsMade > 0
            ? (double)SuccessfulApiCalls / ApiCallsMade * 100
            : 0;

        /// <summary>
        /// Average processing time per hyperlink
        /// </summary>
        public TimeSpan AverageHyperlinkProcessingTime => ProcessedHyperlinks > 0
            ? TimeSpan.FromMilliseconds(TotalProcessingTime.TotalMilliseconds / ProcessedHyperlinks)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Pipeline performance metrics
    /// </summary>
    public class PipelinePerformanceMetrics
    {
        public TimeSpan FileValidationTime { get; set; }
        public TimeSpan ExtractionTime { get; set; }
        public TimeSpan HyperlinkProcessingTime { get; set; }
        public TimeSpan ApiProcessingTime { get; set; }
        public TimeSpan DocumentUpdateTime { get; set; }
        public TimeSpan CompletionTime { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long PeakMemoryUsageBytes { get; set; }
        public int ThreadPoolThreadsUsed { get; set; }
        public int MaxConcurrentOperations { get; set; }

        /// <summary>
        /// Total processing time across all stages
        /// </summary>
        public TimeSpan TotalProcessingTime =>
            FileValidationTime + ExtractionTime + HyperlinkProcessingTime +
            ApiProcessingTime + DocumentUpdateTime + CompletionTime;

        /// <summary>
        /// Pipeline efficiency (percentage of time spent in actual processing vs waiting)
        /// </summary>
        public double PipelineEfficiency { get; set; }
    }
}