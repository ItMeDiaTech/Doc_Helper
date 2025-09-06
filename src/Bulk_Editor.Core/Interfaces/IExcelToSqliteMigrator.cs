using System;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces
{
    /// <summary>
    /// Interface for Excel to SQLite migration operations
    /// </summary>
    public interface IExcelToSqliteMigrator : IDisposable
    {
        /// <summary>
        /// Performs complete migration from Excel file to SQLite database
        /// </summary>
        /// <param name="excelFilePath">Path to the Excel file</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Migration result with statistics</returns>
        Task<MigrationResult> MigrateFromExcelAsync(
            string excelFilePath,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Synchronizes data between Excel file and SQLite database
        /// </summary>
        /// <param name="excelFilePath">Path to the Excel file</param>
        /// <param name="lastSyncTime">Last synchronization timestamp</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Synchronization result</returns>
        Task<SyncResult> SynchronizeDataAsync(
            string excelFilePath,
            DateTime? lastSyncTime = null,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates database backup before migration
        /// </summary>
        /// <param name="backupPath">Optional backup file path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Backup operation result</returns>
        Task<BackupResult> CreateDatabaseBackupAsync(
            string? backupPath = null,
            CancellationToken cancellationToken = default);
    }
}