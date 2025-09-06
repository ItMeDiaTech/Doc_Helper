using System;
using System.ComponentModel.DataAnnotations;

namespace Doc_Helper.Shared.Configuration;

/// <summary>
/// Root application configuration options
/// </summary>
public class AppOptions
{
    public const string SectionName = "App";

    [Required]
    public ApiOptions Api { get; set; } = new();

    [Required]
    public ProcessingOptions Processing { get; set; } = new();

    [Required]
    public UiOptions UI { get; set; } = new();

    [Required]
    public DataOptions Data { get; set; } = new();

    [Required]
    public CacheOptions Cache { get; set; } = new();
}

/// <summary>
/// API-related configuration options
/// </summary>
public class ApiOptions
{
    [Required]
    [Url]
    public string PowerAutomateFlowUrl { get; set; } = string.Empty;

    [Required]
    [Url]
    public string HyperlinkBaseUrl { get; set; } = string.Empty;

    public string HyperlinkViewPath { get; set; } = "!/view?docid=";

    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(1, 10)]
    public int MaxConcurrentRequests { get; set; } = 5;

    public string UserAgent { get; set; } = "Bulk-Editor-Modern/3.0";

    public bool ValidateSsl { get; set; } = true;
}

/// <summary>
/// Document processing configuration options
/// </summary>
public class ProcessingOptions
{
    [Range(1, 1000)]
    public int MaxFileBatchSize { get; set; } = 100;

    [Range(1, 20)]
    public int MaxConcurrentFiles { get; set; } = 5;

    public bool CreateBackups { get; set; } = true;

    public string BackupFolderName { get; set; } = "Backup";

    public bool ValidateDocuments { get; set; } = true;

    public string[] AllowedExtensions { get; set; } = { ".docx" };

    [Range(1, 120)]
    public int ProcessingTimeoutMinutes { get; set; } = 30;

    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB

    public bool SkipCorruptedFiles { get; set; } = true;

    public bool PreserveFileAttributes { get; set; } = true;

    public string TempFolderPath { get; set; } = "Temp";
}

/// <summary>
/// User interface configuration options
/// </summary>
public class UiOptions
{
    public string Theme { get; set; } = "System"; // Light, Dark, System

    public bool ShowProgressDetails { get; set; } = true;

    public bool AutoSelectFirstFile { get; set; } = true;

    public bool RememberWindowPosition { get; set; } = true;

    public bool ConfirmOnExit { get; set; } = false;

    [Range(100, 5000)]
    public int ChangelogRefreshIntervalMs { get; set; } = 1000;

    public bool ShowToolTips { get; set; } = true;

    public bool MinimizeToTray { get; set; } = false;

    public bool ShowStatusBar { get; set; } = true;

    [Range(30, 600)]
    public int AutoSaveIntervalSeconds { get; set; } = 300;

    // Processing options that user can configure
    public bool FixSourceHyperlinks { get; set; } = true;
    public bool AppendContentID { get; set; } = false;
    public bool CheckTitleChanges { get; set; } = false;
    public bool FixTitles { get; set; } = false;
    public bool FixInternalHyperlink { get; set; } = true;
    public bool FixDoubleSpaces { get; set; } = true;
    public bool ReplaceHyperlink { get; set; } = false;
    public bool OpenChangelogAfterUpdates { get; set; } = false;
}

/// <summary>
/// Data storage and synchronization configuration options
/// </summary>
public class DataOptions
{
    [Required]
    public string DatabasePath { get; set; } = "Data/bulkeditor.db";

    public string ExcelSourcePath { get; set; } = string.Empty;

    public bool EnableAutoSync { get; set; } = true;

    [Range(1, 1440)]
    public int SyncIntervalMinutes { get; set; } = 60;

    [Range(1, 168)]
    public int CacheExpiryHours { get; set; } = 12;

    public bool EnableOfflineMode { get; set; } = true;

    public string BaseStoragePath { get; set; } = "Data";

    public bool UseCentralizedStorage { get; set; } = true;

    public bool OrganizeByDate { get; set; } = true;

    [Range(1, 365)]
    public int AutoCleanupDays { get; set; } = 30;

    public bool SeparateIndividualAndCombined { get; set; } = true;

    public bool CentralizeBackups { get; set; } = true;
}

/// <summary>
/// Caching configuration options
/// </summary>
public class CacheOptions
{
    [Range(1, 72)]
    public int DefaultExpiryHours { get; set; } = 12;

    [Range(100, 10000)]
    public int MaxCacheSize { get; set; } = 1000;

    public bool EnableDistributedCache { get; set; } = false;

    public string DistributedCacheConnectionString { get; set; } = string.Empty;

    public bool EnableMemoryCompression { get; set; } = true;

    [Range(1, 60)]
    public int CleanupIntervalMinutes { get; set; } = 15;

    public bool EnableCacheStatistics { get; set; } = true;

    public TimeSpan DefaultExpiryTime => TimeSpan.FromHours(DefaultExpiryHours);
}