using System;
using System.Collections.Generic;

namespace Doc_Helper.Core.Models
{
    /// <summary>
    /// Options for validation operations
    /// </summary>
    public class ValidationOptions
    {
        public int BatchSize { get; set; } = 100;
        public bool ValidateUrlFormat { get; set; } = true;
        public bool ValidateLookupIds { get; set; } = true;
        public bool ValidateConnectivity { get; set; } = false; // Expensive operation
        public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public bool StopOnFirstError { get; set; } = false;
        public bool IncludeDetailedErrors { get; set; } = true;
    }

    /// <summary>
    /// Result of hyperlink validation operation
    /// </summary>
    public class HyperlinkValidationResult
    {
        public bool Success { get; set; }
        public int TotalHyperlinks { get; set; }
        public int ValidHyperlinks { get; set; }
        public int InvalidHyperlinks { get; set; }
        public DateTime ValidationStartTime { get; set; }
        public DateTime ValidationEndTime { get; set; }
        public TimeSpan ValidationDuration => ValidationEndTime - ValidationStartTime;
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        /// <summary>
        /// Validation success rate as percentage
        /// </summary>
        public double ValidationRate => TotalHyperlinks > 0 ? (double)ValidHyperlinks / TotalHyperlinks * 100 : 0;

        /// <summary>
        /// Indicates if there are any validation issues
        /// </summary>
        public bool HasIssues => ValidationErrors.Count > 0 || ValidationWarnings.Count > 0;
    }

    /// <summary>
    /// Single hyperlink validation result
    /// </summary>
    public class SingleHyperlinkValidation
    {
        public string HyperlinkId { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Document validation summary
    /// </summary>
    public class DocumentValidationSummary
    {
        public bool Success { get; set; }
        public int TotalFiles { get; set; }
        public int ValidFiles { get; set; }
        public int InvalidFiles { get; set; }
        public DateTime ValidationStartTime { get; set; }
        public DateTime ValidationEndTime { get; set; }
        public TimeSpan ValidationDuration => ValidationEndTime - ValidationStartTime;
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public List<FileValidationResult> FileValidations { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        /// <summary>
        /// Validation success rate as percentage
        /// </summary>
        public double ValidationRate => TotalFiles > 0 ? (double)ValidFiles / TotalFiles * 100 : 0;
    }

    public class DocumentValidationResult
    {
        public int TotalFiles { get; set; }
        public int ValidFiles { get; set; }
        public int InvalidFiles { get; set; }
        public List<FileValidationResult> Results { get; set; } = new();
        public bool IsValid => InvalidFiles == 0;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class FileValidationResult
    {
        public string FilePath { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public List<string> ValidationWarnings { get; set; } = new();
    }

    /// <summary>
    /// System validation result
    /// </summary>
    public class SystemValidationResult
    {
        public bool Success { get; set; }
        public DateTime ValidationStartTime { get; set; }
        public DateTime ValidationEndTime { get; set; }
        public TimeSpan ValidationDuration => ValidationEndTime - ValidationStartTime;
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        // System component status
        public bool ConfigurationValid { get; set; }
        public bool DatabaseConnectivity { get; set; }
        public bool ApiConnectivity { get; set; }
        public bool FileSystemPermissions { get; set; }
        public bool DependenciesValid { get; set; }

        /// <summary>
        /// Indicates if all system components are valid
        /// </summary>
        public bool AllComponentsValid => ConfigurationValid && DatabaseConnectivity &&
                                         ApiConnectivity && FileSystemPermissions && DependenciesValid;
    }

    /// <summary>
    /// System health check result
    /// </summary>
    public class SystemHealthResult
    {
        public bool IsHealthy { get; set; }
        public int HealthScore { get; set; } // 0-100
        public DateTime CheckStartTime { get; set; }
        public DateTime CheckEndTime { get; set; }
        public TimeSpan CheckDuration => CheckEndTime - CheckStartTime;
        public List<string> ValidationWarnings { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        // Component validations
        public SystemValidationResult SystemValidation { get; set; } = new();
        public ApiValidationResult ApiValidation { get; set; } = new();
        public PerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Health status description
        /// </summary>
        public string HealthStatus => HealthScore switch
        {
            >= 90 => "Excellent",
            >= 80 => "Good",
            >= 70 => "Fair",
            >= 60 => "Poor",
            _ => "Critical"
        };
    }


    /// <summary>
    /// Performance metrics for health checks
    /// </summary>
    public class PerformanceMetrics
    {
        public int ResponseTimeMs { get; set; }
        public double MemoryUsageMB { get; set; }
        public bool CacheOperationSuccessful { get; set; }
        public DateTime CollectionTime { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    /// <summary>
    /// System health status
    /// </summary>
    public class SystemHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public int HealthScore { get; set; }
        public List<string> Issues { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    /// <summary>
    /// Hyperlink lookup result
    /// </summary>
    public class HyperlinkLookupResult
    {
        public bool Success { get; set; }
        public int TotalRequested { get; set; }
        public int FoundInDatabase { get; set; }
        public int FoundInApi { get; set; }
        public int NotFound { get; set; }
        public List<HyperlinkData> Results { get; set; } = new();
        public DateTime LookupStartTime { get; set; }
        public DateTime LookupEndTime { get; set; }
        public TimeSpan LookupDuration => LookupEndTime - LookupStartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public LookupPerformanceStats PerformanceStats { get; set; } = new();
    }

    /// <summary>
    /// Batch lookup result
    /// </summary>
    public class BatchLookupResult
    {
        public bool Success { get; set; }
        public int TotalBatches { get; set; }
        public int CompletedBatches { get; set; }
        public int FailedBatches { get; set; }
        public List<HyperlinkLookupResult> BatchResults { get; set; } = new();
        public DateTime BatchStartTime { get; set; }
        public DateTime BatchEndTime { get; set; }
        public TimeSpan BatchDuration => BatchEndTime - BatchStartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public LookupPerformanceStats AggregatedStats { get; set; } = new();

        /// <summary>
        /// Overall success rate of batches
        /// </summary>
        public double BatchSuccessRate => TotalBatches > 0 ? (double)CompletedBatches / TotalBatches * 100 : 0;
    }

    /// <summary>
    /// Lookup performance statistics
    /// </summary>
    public class LookupPerformanceStats
    {
        public DateTime CollectionTime { get; set; } = DateTime.UtcNow;
        public int TotalLookups { get; set; }
        public int DatabaseHits { get; set; }
        public int ApiCalls { get; set; }
        public int CacheHits { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public TimeSpan FastestResponseTime { get; set; }
        public TimeSpan SlowestResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public double CacheHitRatio => TotalLookups > 0 ? (double)CacheHits / TotalLookups * 100 : 0;
        public double DatabaseHitRatio => TotalLookups > 0 ? (double)DatabaseHits / TotalLookups * 100 : 0;
        public double ApiCallRatio => TotalLookups > 0 ? (double)ApiCalls / TotalLookups * 100 : 0;
        public double ErrorRate => TotalLookups > 0 ? (double)ErrorCount / TotalLookups * 100 : 0;
        
        // Additional properties for ViewModel compatibility
        public double CacheHitRate => CacheHitRatio;
        public double LocalHitRate => DatabaseHitRatio; 
        public double ApiCallRate => ApiCallRatio;
    }

    /// <summary>
    /// Database health check result
    /// </summary>
    public class DatabaseHealthResult
    {
        public bool IsHealthy { get; set; }
        public bool CanConnect { get; set; }
        public DateTime CheckTime { get; set; } = DateTime.UtcNow;
        public TimeSpan ConnectionTime { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public int RecordCount { get; set; }
        public int TableCount { get; set; }
        public int IndexCount { get; set; }
        public List<string> HealthIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public DatabasePerformanceMetrics PerformanceMetrics { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        /// <summary>
        /// Database size in megabytes
        /// </summary>
        public double DatabaseSizeMB => DatabaseSizeBytes / (1024.0 * 1024.0);
        
        // Additional properties for ViewModel compatibility
        public bool RecommendSync { get; set; }
        public int TotalRecords => RecordCount;
        public double DataStalenessHours { get; set; }
        public DateTime LastSyncTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Optimization operation result
    /// </summary>
    public class OptimizationResult
    {
        public bool Success { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<string> OptimizationsApplied { get; set; } = new();
        public long SpaceSavedBytes { get; set; }
        public int PerformanceImprovement { get; set; } // Percentage
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();

        /// <summary>
        /// Space saved in megabytes
        /// </summary>
        public double SpaceSavedMB => SpaceSavedBytes / (1024.0 * 1024.0);
    }
}