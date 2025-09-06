using System;
using System.Collections.Generic;

namespace Doc_Helper.Core.Models
{
    /// <summary>
    /// Result of Excel to SQLite migration operation
    /// </summary>
    public class MigrationResult
    {
        public string SourceFile { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int TotalRecords { get; set; }
        public int MigratedRecords { get; set; }
        public int SkippedRecords { get; set; }
        public int ErrorRecords { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Migration success rate as percentage
        /// </summary>
        public double SuccessRate => TotalRecords > 0 ? (double)MigratedRecords / TotalRecords * 100 : 0;

        /// <summary>
        /// Migration throughput (records per second)
        /// </summary>
        public double RecordsPerSecond => Duration.TotalSeconds > 0 ? MigratedRecords / Duration.TotalSeconds : 0;
    }

    /// <summary>
    /// Progress information for migration operations
    /// </summary>
    public class MigrationProgress
    {
        public string Stage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int ProcessedCount { get; set; }
        public int TotalCount { get; set; }
        public double ProgressPercentage => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100 : 0;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan ElapsedTime { get; set; }
        public string CurrentItem { get; set; } = string.Empty;
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Result of data synchronization operation
    /// </summary>
    public class SyncResult
    {
        public string SourceFile { get; set; } = string.Empty;
        public bool Success { get; set; }
        public SyncAction SyncAction { get; set; }
        public int AddedRecords { get; set; }
        public int UpdatedRecords { get; set; }
        public int DeletedRecords { get; set; }
        public DateTime? LastSyncTime { get; set; }
        public DateTime SyncStartTime { get; set; }
        public DateTime SyncEndTime { get; set; }
        public TimeSpan SyncDuration => SyncEndTime - SyncStartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public List<string> ConflictResolutions { get; set; } = new();

        /// <summary>
        /// Total number of changes applied
        /// </summary>
        public int TotalChanges => AddedRecords + UpdatedRecords + DeletedRecords;

        /// <summary>
        /// Indicates if any data was modified
        /// </summary>
        public bool HasChanges => TotalChanges > 0;
    }

    /// <summary>
    /// Types of synchronization actions
    /// </summary>
    public enum SyncAction
    {
        NoChanges,
        Updated,
        FullMigration,
        ConflictResolution,
        Error
    }

    /// <summary>
    /// Represents changes detected between Excel and database data
    /// </summary>
    public class DataChanges
    {
        public List<HyperlinkData> AddedRecords { get; set; } = new();
        public List<UpdateRecord> UpdatedRecords { get; set; } = new();
        public List<HyperlinkData> DeletedRecords { get; set; } = new();
        public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan AnalysisDuration { get; set; }

        /// <summary>
        /// Total number of changes detected
        /// </summary>
        public int TotalChanges => AddedRecords.Count + UpdatedRecords.Count + DeletedRecords.Count;

        /// <summary>
        /// Indicates if any changes were detected
        /// </summary>
        public bool HasChanges => TotalChanges > 0;
    }

    /// <summary>
    /// Represents a record update operation
    /// </summary>
    public class UpdateRecord
    {
        public HyperlinkData ExcelData { get; set; } = new();
        public HyperlinkData DatabaseEntity { get; set; } = new();
        public List<string> ChangedFields { get; set; } = new();
        public DateTime UpdateTimestamp { get; set; } = DateTime.UtcNow;
        public UpdateConflictResolution ConflictResolution { get; set; } = UpdateConflictResolution.UseExcelData;
    }

    /// <summary>
    /// Update conflict resolution strategies
    /// </summary>
    public enum UpdateConflictResolution
    {
        UseExcelData,
        UseDatabaseData,
        Merge,
        SkipRecord,
        PromptUser
    }

    /// <summary>
    /// Configuration options for migration operations
    /// </summary>
    public class MigrationOptions
    {
        /// <summary>
        /// Batch size for database operations
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Maximum number of concurrent migration operations
        /// </summary>
        public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Timeout for migration operations
        /// </summary>
        public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Whether to create backups before migration
        /// </summary>
        public bool CreateBackup { get; set; } = true;

        /// <summary>
        /// Backup retention period
        /// </summary>
        public TimeSpan BackupRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// Skip validation for faster migration (not recommended for production)
        /// </summary>
        public bool SkipValidation { get; set; } = false;

        /// <summary>
        /// Use transactions for atomic operations
        /// </summary>
        public bool UseTransactions { get; set; } = true;

        /// <summary>
        /// Conflict resolution strategy for updates
        /// </summary>
        public UpdateConflictResolution DefaultConflictResolution { get; set; } = UpdateConflictResolution.UseExcelData;

        /// <summary>
        /// Maximum number of retry attempts for failed operations
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Enable detailed logging for troubleshooting
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;
    }

    /// <summary>
    /// Migration validation result
    /// </summary>
    public class MigrationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public int ExpectedRecordCount { get; set; }
        public string ExcelVersion { get; set; } = string.Empty;
        public DateTime FileLastModified { get; set; }
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Indicates if there are blocking validation errors
        /// </summary>
        public bool HasBlockingErrors => ValidationErrors.Count > 0;

        /// <summary>
        /// Indicates if there are warnings but migration can proceed
        /// </summary>
        public bool HasWarnings => ValidationWarnings.Count > 0;
    }

    /// <summary>
    /// Migration statistics and performance metrics
    /// </summary>
    public class MigrationStatistics
    {
        public int TotalMigrations { get; set; }
        public int SuccessfulMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public int TotalRecordsProcessed { get; set; }
        public int TotalRecordsMigrated { get; set; }
        public TimeSpan TotalMigrationTime { get; set; }
        public DateTime LastMigrationTime { get; set; }
        public long TotalDataVolumeBytes { get; set; }
        public double AverageRecordsPerSecond { get; set; }
        public Dictionary<string, int> ErrorCounts { get; set; } = new();

        /// <summary>
        /// Migration success rate as percentage
        /// </summary>
        public double SuccessRate => TotalMigrations > 0 ? (double)SuccessfulMigrations / TotalMigrations * 100 : 0;

        /// <summary>
        /// Average migration time per operation
        /// </summary>
        public TimeSpan AverageMigrationTime => SuccessfulMigrations > 0
            ? TimeSpan.FromMilliseconds(TotalMigrationTime.TotalMilliseconds / SuccessfulMigrations)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Data synchronization configuration
    /// </summary>
    public class SyncConfiguration
    {
        /// <summary>
        /// Excel file path to monitor
        /// </summary>
        public string ExcelFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Synchronization interval
        /// </summary>
        public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Enable automatic synchronization
        /// </summary>
        public bool EnableAutoSync { get; set; } = true;

        /// <summary>
        /// Check for file changes before sync
        /// </summary>
        public bool CheckFileModificationTime { get; set; } = true;

        /// <summary>
        /// Maximum age of data before forced sync
        /// </summary>
        public TimeSpan MaxDataAge { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Enable conflict detection and resolution
        /// </summary>
        public bool EnableConflictResolution { get; set; } = true;

        /// <summary>
        /// Default conflict resolution strategy
        /// </summary>
        public UpdateConflictResolution ConflictResolution { get; set; } = UpdateConflictResolution.UseExcelData;

        /// <summary>
        /// Enable change notifications
        /// </summary>
        public bool EnableChangeNotifications { get; set; } = true;

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;
    }

    /// <summary>
    /// Data change notification
    /// </summary>
    public class DataChangeNotification
    {
        public string ChangeType { get; set; } = string.Empty; // Added, Updated, Deleted
        public string RecordId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> ChangeDetails { get; set; } = new();
    }
}