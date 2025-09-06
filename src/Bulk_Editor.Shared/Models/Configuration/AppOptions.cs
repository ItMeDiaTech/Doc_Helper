using System.Collections.Generic;

namespace Doc_Helper.Shared.Models.Configuration
{
    /// <summary>
    /// Application-wide configuration options
    /// </summary>
    public class AppOptions
    {
        /// <summary>
        /// Application name
        /// </summary>
        public string ApplicationName { get; set; } = "Doc Helper";

        /// <summary>
        /// Current application version
        /// </summary>
        public string Version { get; set; } = "2.0.0";

        /// <summary>
        /// Application environment (Development, Staging, Production)
        /// </summary>
        public string Environment { get; set; } = "Production";

        /// <summary>
        /// API configuration
        /// </summary>
        public ApiOptions Api { get; set; } = new();

        /// <summary>
        /// Processing configuration
        /// </summary>
        public ProcessingOptions Processing { get; set; } = new();

        /// <summary>
        /// UI configuration
        /// </summary>
        public UiOptions Ui { get; set; } = new();

        /// <summary>
        /// Data configuration
        /// </summary>
        public DataOptions Data { get; set; } = new();

        /// <summary>
        /// Update configuration
        /// </summary>
        public UpdateOptions Update { get; set; } = new();
    }

    /// <summary>
    /// API-related configuration options
    /// </summary>
    public class ApiOptions
    {
        /// <summary>
        /// PowerAutomate flow URL
        /// </summary>
        public string PowerAutomateFlowUrl { get; set; } = "";

        /// <summary>
        /// API timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Number of retry attempts
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Base delay between retries in seconds
        /// </summary>
        public int RetryDelaySeconds { get; set; } = 2;

        /// <summary>
        /// Maximum batch size for API requests
        /// </summary>
        public int MaxBatchSize { get; set; } = 100;

        /// <summary>
        /// Rate limit - requests per minute
        /// </summary>
        public int RateLimitPerMinute { get; set; } = 60;
    }

    /// <summary>
    /// Processing-related configuration options
    /// </summary>
    public class ProcessingOptions
    {
        /// <summary>
        /// Maximum degree of parallelism for processing
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 4;

        /// <summary>
        /// Processing buffer size
        /// </summary>
        public int BufferSize { get; set; } = 1000;

        /// <summary>
        /// Enable processing optimizations
        /// </summary>
        public bool EnableOptimizations { get; set; } = true;

        /// <summary>
        /// Default file extensions to process
        /// </summary>
        public List<string> DefaultFileExtensions { get; set; } = new() { ".docx", ".doc" };

        /// <summary>
        /// Enable automatic backup before processing
        /// </summary>
        public bool EnableAutoBackup { get; set; } = true;

        /// <summary>
        /// Backup directory path (relative or absolute)
        /// </summary>
        public string BackupPath { get; set; } = "Backups";

        /// <summary>
        /// Use centralized backup location instead of local folder
        /// </summary>
        public bool UseCentralizedBackups { get; set; } = true;

        /// <summary>
        /// Root path for centralized backups (empty = user documents)
        /// </summary>
        public string BackupRootPath { get; set; } = string.Empty;

        /// <summary>
        /// Backup folder name when using local backups
        /// </summary>
        public string BackupFolderName { get; set; } = "Backups";

        /// <summary>
        /// Number of days to retain backup files
        /// </summary>
        public int BackupRetentionDays { get; set; } = 30;

        /// <summary>
        /// Enable undo/restore functionality
        /// </summary>
        public bool EnableUndo { get; set; } = true;

        /// <summary>
        /// Automatically delete backups after successful processing
        /// </summary>
        public bool AutoCleanupBackups { get; set; } = false;
    }

    /// <summary>
    /// UI-related configuration options
    /// </summary>
    public class UiOptions
    {
        /// <summary>
        /// Default theme (Light, Dark, Auto)
        /// </summary>
        public string Theme { get; set; } = "Auto";

        /// <summary>
        /// Show splash screen on startup
        /// </summary>
        public bool ShowSplashScreen { get; set; } = true;

        /// <summary>
        /// Enable animations
        /// </summary>
        public bool EnableAnimations { get; set; } = true;

        /// <summary>
        /// Window state persistence
        /// </summary>
        public bool RememberWindowState { get; set; } = true;

        /// <summary>
        /// Default window width
        /// </summary>
        public double DefaultWindowWidth { get; set; } = 1200;

        /// <summary>
        /// Default window height
        /// </summary>
        public double DefaultWindowHeight { get; set; } = 800;

        /// <summary>
        /// Enable tooltips
        /// </summary>
        public bool EnableTooltips { get; set; } = true;

        /// <summary>
        /// Automatically open changelog after processing
        /// </summary>
        public bool AutoOpenChangelog { get; set; } = true;

        /// <summary>
        /// Save changelog to downloads folder
        /// </summary>
        public bool SaveChangelogToDownloads { get; set; } = true;

        /// <summary>
        /// Show individual document changelogs in UI
        /// </summary>
        public bool ShowIndividualChangelogs { get; set; } = true;

        /// <summary>
        /// Enable changelog export functionality
        /// </summary>
        public bool EnableChangelogExport { get; set; } = true;

        /// <summary>
        /// Show processing options in UI (checkboxes)
        /// </summary>
        public bool FixSourceHyperlinks { get; set; } = true;
        public bool AppendContentID { get; set; } = true;
        public bool CheckTitleChanges { get; set; } = true;
        public bool FixTitles { get; set; } = false;
        public bool FixInternalHyperlink { get; set; } = true;
        public bool FixDoubleSpaces { get; set; } = true;
        public bool ReplaceHyperlink { get; set; } = false;
        public bool ReplaceText { get; set; } = false;
        public bool OpenChangelogAfterUpdates { get; set; } = true;
    }

    /// <summary>
    /// Data-related configuration options
    /// </summary>
    public class DataOptions
    {
        /// <summary>
        /// Database connection string
        /// </summary>
        public string ConnectionString { get; set; } = "Data Source=BulkEditor.db";

        /// <summary>
        /// Enable database logging
        /// </summary>
        public bool EnableDatabaseLogging { get; set; } = false;

        /// <summary>
        /// Cache expiration in minutes
        /// </summary>
        public int CacheExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum cache size in MB
        /// </summary>
        public int MaxCacheSizeMB { get; set; } = 100;

        /// <summary>
        /// Enable data compression
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Auto-migrate database on startup
        /// </summary>
        public bool AutoMigrateDatabase { get; set; } = true;
    }

    /// <summary>
    /// Update-related configuration options
    /// </summary>
    public class UpdateOptions
    {
        /// <summary>
        /// Enable automatic updates
        /// </summary>
        public bool EnableAutoUpdates { get; set; } = true;

        /// <summary>
        /// Update check frequency in hours
        /// </summary>
        public int CheckFrequencyHours { get; set; } = 24;

        /// <summary>
        /// Update server URL
        /// </summary>
        public string UpdateServerUrl { get; set; } = "";

        /// <summary>
        /// Enable pre-release updates
        /// </summary>
        public bool EnablePreReleaseUpdates { get; set; } = false;

        /// <summary>
        /// Silent update installation
        /// </summary>
        public bool SilentInstall { get; set; } = false;
    }
}