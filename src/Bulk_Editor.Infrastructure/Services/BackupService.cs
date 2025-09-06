using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Doc_Helper.Shared.Models.Configuration;

namespace Doc_Helper.Infrastructure.Services;

/// <summary>
/// Service for managing file backups and restoration
/// </summary>
public class BackupService : IBackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly AppOptions _appOptions;

    public BackupService(ILogger<BackupService> logger, IOptions<AppOptions> appOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appOptions = appOptions?.Value ?? throw new ArgumentNullException(nameof(appOptions));
    }

    /// <summary>
    /// Creates a backup of the specified file
    /// </summary>
    public async Task<string> CreateBackupAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            throw new ArgumentException($"File does not exist: {filePath}", nameof(filePath));
        }

        try
        {
            string backupPath = GenerateBackupPath(filePath);
            
            // Ensure backup directory exists
            string? backupDir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            // Copy file to backup location
            await Task.Run(() => File.Copy(filePath, backupPath, overwrite: true));

            // Preserve file attributes and timestamps
            File.SetAttributes(backupPath, File.GetAttributes(filePath));
            File.SetCreationTime(backupPath, File.GetCreationTime(filePath));
            File.SetLastWriteTime(backupPath, File.GetLastWriteTime(filePath));

            _logger.LogInformation("Created backup for {OriginalFile} at {BackupPath}", filePath, backupPath);
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup for file {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Creates backups for multiple files
    /// </summary>
    public async Task<Dictionary<string, string>> CreateBackupsAsync(IEnumerable<string> filePaths)
    {
        var backupMap = new Dictionary<string, string>();
        var tasks = new List<Task>();

        foreach (var filePath in filePaths)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var backupPath = await CreateBackupAsync(filePath);
                    lock (backupMap)
                    {
                        backupMap[filePath] = backupPath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backup file {FilePath}", filePath);
                    // Continue with other files even if one fails
                }
            }));
        }

        await Task.WhenAll(tasks);
        return backupMap;
    }

    /// <summary>
    /// Restores a file from its backup
    /// </summary>
    public async Task RestoreFromBackupAsync(string originalFilePath, string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"Backup file does not exist: {backupFilePath}");
        }

        try
        {
            // Ensure target directory exists
            string? targetDir = Path.GetDirectoryName(originalFilePath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            await Task.Run(() => File.Copy(backupFilePath, originalFilePath, overwrite: true));
            
            _logger.LogInformation("Restored {OriginalFile} from backup {BackupPath}", originalFilePath, backupFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore {OriginalFile} from backup {BackupPath}", originalFilePath, backupFilePath);
            throw;
        }
    }

    /// <summary>
    /// Gets the backup path for a given file
    /// </summary>
    public string GetBackupPath(string filePath)
    {
        return GenerateBackupPath(filePath);
    }

    /// <summary>
    /// Checks if a backup exists for the specified file
    /// </summary>
    public bool BackupExists(string filePath)
    {
        string backupPath = GetBackupPath(filePath);
        return File.Exists(backupPath);
    }

    /// <summary>
    /// Deletes backup file
    /// </summary>
    public async Task DeleteBackupAsync(string backupFilePath)
    {
        if (File.Exists(backupFilePath))
        {
            await Task.Run(() => File.Delete(backupFilePath));
            _logger.LogInformation("Deleted backup file {BackupPath}", backupFilePath);
        }
    }

    /// <summary>
    /// Cleans up old backup files based on retention policy
    /// </summary>
    public async Task CleanupOldBackupsAsync()
    {
        try
        {
            string backupRoot = GetBackupRootDirectory();
            if (!Directory.Exists(backupRoot)) return;

            var cutoffDate = DateTime.UtcNow.AddDays(-_appOptions.Processing.BackupRetentionDays);
            
            await Task.Run(() =>
            {
                var oldBackups = Directory.GetFiles(backupRoot, "*", SearchOption.AllDirectories)
                    .Where(file => File.GetCreationTime(file) < cutoffDate)
                    .ToList();

                foreach (var oldBackup in oldBackups)
                {
                    try
                    {
                        File.Delete(oldBackup);
                        _logger.LogDebug("Deleted old backup file {BackupPath}", oldBackup);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old backup file {BackupPath}", oldBackup);
                    }
                }

                _logger.LogInformation("Cleaned up {Count} old backup files", oldBackups.Count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup cleanup");
        }
    }

    /// <summary>
    /// Gets all backup files for a given original file
    /// </summary>
    public IEnumerable<string> GetBackupHistory(string originalFilePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(originalFilePath);
        string extension = Path.GetExtension(originalFilePath);
        string backupDir = GetBackupDirectory(originalFilePath);

        if (!Directory.Exists(backupDir))
        {
            return Enumerable.Empty<string>();
        }

        string searchPattern = $"{fileName}_backup_*{extension}";
        return Directory.GetFiles(backupDir, searchPattern)
            .OrderByDescending(File.GetCreationTime);
    }

    #region Private Methods

    /// <summary>
    /// Generates a backup path with timestamp
    /// </summary>
    private string GenerateBackupPath(string originalFilePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(originalFilePath);
        string extension = Path.GetExtension(originalFilePath);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        string backupFileName = $"{fileName}_backup_{timestamp}{extension}";
        string backupDir = GetBackupDirectory(originalFilePath);
        
        return Path.Combine(backupDir, backupFileName);
    }

    /// <summary>
    /// Gets the backup directory for a file
    /// </summary>
    private string GetBackupDirectory(string originalFilePath)
    {
        if (_appOptions.Processing.UseCentralizedBackups)
        {
            // Use centralized backup location
            return Path.Combine(GetBackupRootDirectory(), "Backups");
        }
        else
        {
            // Use local backup folder next to original file
            string? originalDir = Path.GetDirectoryName(originalFilePath);
            return Path.Combine(originalDir ?? string.Empty, _appOptions.Processing.BackupFolderName);
        }
    }

    /// <summary>
    /// Gets the root backup directory
    /// </summary>
    private string GetBackupRootDirectory()
    {
        string backupRoot = _appOptions.Processing.BackupRootPath;
        
        if (string.IsNullOrEmpty(backupRoot))
        {
            // Default to user's documents folder
            backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DocHelper"
            );
        }

        return backupRoot;
    }

    #endregion
}