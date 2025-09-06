using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Doc_Helper.Core.Interfaces;

/// <summary>
/// Service for managing file backups and restoration
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a backup of the specified file
    /// </summary>
    /// <param name="filePath">Path to the file to backup</param>
    /// <returns>Path to the created backup file</returns>
    Task<string> CreateBackupAsync(string filePath);

    /// <summary>
    /// Creates backups for multiple files
    /// </summary>
    /// <param name="filePaths">Paths to the files to backup</param>
    /// <returns>Dictionary mapping original file paths to backup file paths</returns>
    Task<Dictionary<string, string>> CreateBackupsAsync(IEnumerable<string> filePaths);

    /// <summary>
    /// Restores a file from its backup
    /// </summary>
    /// <param name="originalFilePath">Path to the original file</param>
    /// <param name="backupFilePath">Path to the backup file</param>
    Task RestoreFromBackupAsync(string originalFilePath, string backupFilePath);

    /// <summary>
    /// Gets the backup path for a given file
    /// </summary>
    /// <param name="filePath">Original file path</param>
    /// <returns>Expected backup file path</returns>
    string GetBackupPath(string filePath);

    /// <summary>
    /// Checks if a backup exists for the specified file
    /// </summary>
    /// <param name="filePath">Original file path</param>
    /// <returns>True if backup exists</returns>
    bool BackupExists(string filePath);

    /// <summary>
    /// Deletes backup file
    /// </summary>
    /// <param name="backupFilePath">Path to the backup file</param>
    Task DeleteBackupAsync(string backupFilePath);

    /// <summary>
    /// Cleans up old backup files based on retention policy
    /// </summary>
    Task CleanupOldBackupsAsync();

    /// <summary>
    /// Gets all backup files for a given original file
    /// </summary>
    /// <param name="originalFilePath">Original file path</param>
    /// <returns>List of backup file paths</returns>
    IEnumerable<string> GetBackupHistory(string originalFilePath);
}