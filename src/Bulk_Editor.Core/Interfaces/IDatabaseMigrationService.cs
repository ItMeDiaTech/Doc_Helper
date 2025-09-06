using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces
{
    /// <summary>
    /// Interface for database migration and versioning operations
    /// </summary>
    public interface IDatabaseMigrationService
    {
        /// <summary>
        /// Initializes database and applies any pending migrations
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Database initialization result</returns>
        Task<DatabaseInitializationResult> InitializeDatabaseAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current database version
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current database version</returns>
        Task<DatabaseVersion> GetCurrentDatabaseVersionAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates database integrity and schema
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Database validation result</returns>
        Task<DatabaseValidationResult> ValidateDatabaseAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a complete database backup
        /// </summary>
        /// <param name="backupPath">Optional backup file path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Backup operation result</returns>
        Task<BackupResult> CreateFullBackupAsync(
            string? backupPath = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Optimizes database performance
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Optimization result</returns>
        Task<DatabaseOptimizationResult> OptimizeDatabaseAsync(
            CancellationToken cancellationToken = default);
    }
}