using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Doc_Helper.Infrastructure.Migration
{
    /// <summary>
    /// Database migration and versioning service
    /// Handles schema migrations, version tracking, and database upgrades
    /// </summary>
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly ILogger<DatabaseMigrationService> _logger;
        private readonly DocHelperDbContext _context;
        private readonly List<IDatabaseMigration> _migrations;
        private readonly DatabaseMigrationOptions _options;

        public DatabaseMigrationService(
            ILogger<DatabaseMigrationService> logger,
            DocHelperDbContext context,
            IEnumerable<IDatabaseMigration>? migrations = null,
            DatabaseMigrationOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _migrations = (migrations ?? Enumerable.Empty<IDatabaseMigration>()).OrderBy(m => m.Version).ToList();
            _options = options ?? new DatabaseMigrationOptions();
        }

        /// <summary>
        /// Initializes database and applies any pending migrations
        /// </summary>
        public async Task<DatabaseInitializationResult> InitializeDatabaseAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new DatabaseInitializationResult { StartTime = DateTime.UtcNow };
            var databaseDirectory = Path.GetDirectoryName(_options.DatabasePath);
            if (!string.IsNullOrEmpty(databaseDirectory) && !Directory.Exists(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }
            if (!string.IsNullOrEmpty(databaseDirectory) && !Directory.Exists(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            try
            {
                _logger.LogInformation("Initializing database and checking for migrations");

                // Ensure database exists
                var databaseExists = await _context.Database.CanConnectAsync(cancellationToken);
                if (!databaseExists)
                {
                    _logger.LogInformation("Database does not exist, creating new database");
                    await _context.Database.EnsureCreatedAsync(cancellationToken);
                    result.DatabaseCreated = true;
                }

                // Apply Entity Framework migrations
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("Applying {MigrationCount} Entity Framework migrations", pendingMigrations.Count());
                    await _context.Database.MigrateAsync(cancellationToken);
                    result.EfMigrationsApplied = pendingMigrations.Count();
                }

                // Apply custom migrations
                var currentVersion = await GetCurrentDatabaseVersionAsync(cancellationToken);
                var pendingCustomMigrations = GetPendingMigrations(currentVersion);

                if (pendingCustomMigrations.Count > 0)
                {
                    _logger.LogInformation("Applying {MigrationCount} custom migrations from version {CurrentVersion}",
                        pendingCustomMigrations.Count, currentVersion);

                    foreach (var migration in pendingCustomMigrations)
                    {
                        await ApplyMigrationAsync(migration, cancellationToken);
                        result.CustomMigrationsApplied++;
                    }
                }

                result.Success = true;
                result.EndTime = DateTime.UtcNow;
                result.FinalVersion = await GetCurrentDatabaseVersionAsync(cancellationToken);

                _logger.LogInformation("Database initialization completed successfully. Current version: {Version}",
                    result.FinalVersion);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.EndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Gets the current database version
        /// </summary>
        public async Task<DatabaseVersion> GetCurrentDatabaseVersionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if version tracking table exists
                var hasVersionTable = await _context.Database
                    .SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='DatabaseVersions'")
                    .FirstOrDefaultAsync(cancellationToken) > 0;

                if (!hasVersionTable)
                {
                    await CreateVersionTableAsync(cancellationToken);
                    return new DatabaseVersion { Major = 1, Minor = 0, Patch = 0 };
                }

                // Get latest version record
                var versionRecord = await _context.Database
                    .SqlQueryRaw<string>("SELECT Version FROM DatabaseVersions ORDER BY AppliedAt DESC LIMIT 1")
                    .FirstOrDefaultAsync(cancellationToken);

                if (!string.IsNullOrEmpty(versionRecord) && DatabaseVersion.TryParse(versionRecord, out var version))
                {
                    return version;
                }

                return new DatabaseVersion { Major = 1, Minor = 0, Patch = 0 };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting database version, defaulting to 1.0.0");
                return new DatabaseVersion { Major = 1, Minor = 0, Patch = 0 };
            }
        }

        /// <summary>
        /// Creates the database version tracking table
        /// </summary>
        private async Task CreateVersionTableAsync(CancellationToken cancellationToken)
        {
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS DatabaseVersions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Version TEXT NOT NULL,
                    Description TEXT,
                    AppliedAt DATETIME NOT NULL,
                    AppliedBy TEXT,
                    Duration INTEGER,
                    Success INTEGER NOT NULL DEFAULT 1
                )";

            await _context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);

            // Insert initial version record
            var insertVersionSql = @"
                INSERT INTO DatabaseVersions (Version, Description, AppliedAt, AppliedBy, Duration, Success)
                VALUES ('1.0.0', 'Initial database creation', datetime('now'), 'System', 0, 1)";

            await _context.Database.ExecuteSqlRawAsync(insertVersionSql, cancellationToken);

            _logger.LogInformation("Created database version tracking table with initial version 1.0.0");
        }

        /// <summary>
        /// Gets list of pending migrations to apply
        /// </summary>
        private List<IDatabaseMigration> GetPendingMigrations(DatabaseVersion currentVersion)
        {
            return _migrations
                .Where(m => m.Version > currentVersion)
                .OrderBy(m => m.Version)
                .ToList();
        }

        /// <summary>
        /// Applies a single migration
        /// </summary>
        private async Task ApplyMigrationAsync(IDatabaseMigration migration, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Applying migration {Version}: {Description}",
                migration.Version, migration.Description);

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Execute migration
                await migration.ApplyAsync(_context, cancellationToken);

                // Record migration in version table
                var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                var recordMigrationSql = @"
                    INSERT INTO DatabaseVersions (Version, Description, AppliedAt, AppliedBy, Duration, Success)
                    VALUES ({0}, {1}, datetime('now'), {2}, {3}, 1)";

                await _context.Database.ExecuteSqlRawAsync(recordMigrationSql,
                    migration.Version.ToString(),
                    migration.Description,
                    Environment.UserName,
                    duration);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Migration {Version} applied successfully in {Duration}ms",
                    migration.Version, duration);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);

                // Record failed migration
                var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                try
                {
                    var recordFailureSql = @"
                        INSERT INTO DatabaseVersions (Version, Description, AppliedAt, AppliedBy, Duration, Success)
                        VALUES ({0}, {1}, datetime('now'), {2}, {3}, 0)";

                    await _context.Database.ExecuteSqlRawAsync(recordFailureSql,
                        migration.Version.ToString(),
                        $"FAILED: {migration.Description} - {ex.Message}",
                        Environment.UserName,
                        duration);
                }
                catch
                {
                    // Ignore errors recording failure
                }

                _logger.LogError(ex, "Migration {Version} failed", migration.Version);
                throw;
            }
        }

        /// <summary>
        /// Validates database integrity and schema
        /// </summary>
        public async Task<DatabaseValidationResult> ValidateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var result = new DatabaseValidationResult { ValidationStartTime = DateTime.UtcNow };

            try
            {
                _logger.LogInformation("Starting database validation");

                // Check database connectivity
                result.CanConnect = await _context.Database.CanConnectAsync(cancellationToken);
                if (!result.CanConnect)
                {
                    result.ValidationErrors.Add("Cannot connect to database");
                    result.Success = false;
                    return result;
                }

                // Validate schema
                await ValidateSchemaAsync(result, cancellationToken);

                // Validate data integrity
                await ValidateDataIntegrityAsync(result, cancellationToken);

                // Check performance
                await ValidatePerformanceAsync(result, cancellationToken);

                result.Success = result.ValidationErrors.Count == 0;
                result.ValidationEndTime = DateTime.UtcNow;

                _logger.LogInformation("Database validation completed. Success: {Success}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                    result.Success, result.ValidationErrors.Count, result.ValidationWarnings.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database validation failed");
                result.Success = false;
                result.ValidationErrors.Add($"Validation exception: {ex.Message}");
                result.Exception = ex;
                result.ValidationEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Validates database schema
        /// </summary>
        private async Task ValidateSchemaAsync(DatabaseValidationResult result, CancellationToken cancellationToken)
        {
            try
            {
                // Check if required tables exist
                var requiredTables = new[] { "Hyperlinks", "Documents", "DatabaseVersions" };

                foreach (var tableName in requiredTables)
                {
                    var tableExists = await _context.Database
                        .SqlQueryRaw<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name={0}", tableName)
                        .FirstOrDefaultAsync(cancellationToken) > 0;

                    if (!tableExists)
                    {
                        result.ValidationErrors.Add($"Required table '{tableName}' does not exist");
                    }
                }

                // Validate table schemas
                await ValidateTableSchemas(result, cancellationToken);
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Schema validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates individual table schemas
        /// </summary>
        private async Task ValidateTableSchemas(DatabaseValidationResult result, CancellationToken cancellationToken)
        {
            // Validate Hyperlinks table structure
            var hyperlinkColumns = await GetTableColumnsAsync("Hyperlinks", cancellationToken);
            var requiredHyperlinkColumns = new[] { "Id", "Address", "SubAddress", "TextToDisplay", "ContentId", "CreatedAt", "UpdatedAt", "IsActive" };

            foreach (var column in requiredHyperlinkColumns)
            {
                if (!hyperlinkColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                {
                    result.ValidationErrors.Add($"Hyperlinks table missing required column: {column}");
                }
            }

            // Validate Documents table structure
            var documentColumns = await GetTableColumnsAsync("Documents", cancellationToken);
            var requiredDocumentColumns = new[] { "Id", "FilePath", "FileName", "ContentHash", "CreatedAt", "UpdatedAt", "IsActive" };

            foreach (var column in requiredDocumentColumns)
            {
                if (!documentColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                {
                    result.ValidationErrors.Add($"Documents table missing required column: {column}");
                }
            }
        }

        /// <summary>
        /// Gets column names for a table
        /// </summary>
        private async Task<List<string>> GetTableColumnsAsync(string tableName, CancellationToken cancellationToken)
        {
            try
            {
                var columnInfo = await _context.Database
                    .SqlQueryRaw<string>("PRAGMA table_info({0})", tableName)
                    .ToListAsync(cancellationToken);

                // Parse column names from PRAGMA output
                // PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
                return columnInfo;
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Validates data integrity
        /// </summary>
        private async Task ValidateDataIntegrityAsync(DatabaseValidationResult result, CancellationToken cancellationToken)
        {
            try
            {
                // Check for orphaned records
                var orphanedHyperlinks = await _context.Database
                    .SqlQueryRaw<int>(@"
                        SELECT COUNT(*) FROM Hyperlinks h
                        LEFT JOIN Documents d ON h.DocumentId = d.Id
                        WHERE h.DocumentId IS NOT NULL AND d.Id IS NULL")
                    .FirstOrDefaultAsync(cancellationToken);

                if (orphanedHyperlinks > 0)
                {
                    result.ValidationWarnings.Add($"{orphanedHyperlinks} orphaned hyperlink records found");
                }

                // Check for duplicate content IDs
                var duplicateContentIds = await _context.Database
                    .SqlQueryRaw<int>(@"
                        SELECT COUNT(*) FROM (
                            SELECT ContentId FROM Hyperlinks
                            WHERE ContentId IS NOT NULL AND ContentId != ''
                            GROUP BY ContentId HAVING COUNT(*) > 1
                        )")
                    .FirstOrDefaultAsync(cancellationToken);

                if (duplicateContentIds > 0)
                {
                    result.ValidationWarnings.Add($"{duplicateContentIds} duplicate content IDs found");
                }

                // Check for records with invalid data
                var invalidRecords = await _context.Hyperlinks
                    .CountAsync(h => string.IsNullOrEmpty(h.ContentID) &&
                                    string.IsNullOrEmpty(h.Address) &&
                                    string.IsNullOrEmpty(h.TextToDisplay), cancellationToken);

                if (invalidRecords > 0)
                {
                    result.ValidationWarnings.Add($"{invalidRecords} records with no useful data found");
                }

                result.TotalRecords = await _context.Hyperlinks.CountAsync(cancellationToken);
                result.ActiveRecords = await _context.Hyperlinks.CountAsync(h => !h.IsDeleted, cancellationToken);
                result.InactiveRecords = result.TotalRecords - result.ActiveRecords;
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Data integrity validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates database performance
        /// </summary>
        private async Task ValidatePerformanceAsync(DatabaseValidationResult result, CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.UtcNow;

                // Test basic query performance
                await _context.Hyperlinks.Take(100).ToListAsync(cancellationToken);

                var queryTime = DateTime.UtcNow - startTime;
                result.QueryPerformanceMs = (int)queryTime.TotalMilliseconds;

                if (queryTime.TotalMilliseconds > 1000)
                {
                    result.ValidationWarnings.Add($"Slow query performance detected: {queryTime.TotalMilliseconds:F0}ms for 100 records");
                }

                // Check database size
                var dbSizeQuery = "SELECT page_count * page_size as size FROM pragma_page_count(), pragma_page_size()";
                var dbSize = await _context.Database
                    .SqlQueryRaw<long>(dbSizeQuery)
                    .FirstOrDefaultAsync(cancellationToken);

                result.DatabaseSizeBytes = dbSize;

                if (dbSize > 100 * 1024 * 1024) // 100MB
                {
                    result.ValidationWarnings.Add($"Large database size: {dbSize / (1024.0 * 1024.0):F2} MB");
                }
            }
            catch (Exception ex)
            {
                result.ValidationWarnings.Add($"Performance validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a complete database backup
        /// </summary>
        public async Task<BackupResult> CreateFullBackupAsync(
            string? backupPath = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var defaultBackupPath = $"Backup/full_backup_{timestamp}.db";
                var targetPath = backupPath ?? defaultBackupPath;

                _logger.LogInformation("Creating full database backup to {BackupPath}", targetPath);

                // Create backup directory
                var backupDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Perform vacuum to optimize before backup
                await _context.Database.ExecuteSqlRawAsync("VACUUM", cancellationToken);

                // Use SQLite backup API for consistent backup
                var connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Database connection string is not available");
                }
                var sourceDbPath = ExtractDatabasePath(connectionString);

                await Task.Run(() => File.Copy(sourceDbPath, targetPath, overwrite: true), cancellationToken);

                var backupInfo = new FileInfo(targetPath);

                _logger.LogInformation("Database backup completed: {BackupPath} ({Size:F2} MB)",
                    targetPath, backupInfo.Length / (1024.0 * 1024.0));

                return new BackupResult
                {
                    Success = true,
                    BackupPath = targetPath,
                    BackupSize = backupInfo.Length
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup failed");
                return new BackupResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Optimizes database performance
        /// </summary>
        public async Task<DatabaseOptimizationResult> OptimizeDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var result = new DatabaseOptimizationResult { StartTime = DateTime.UtcNow };

            try
            {
                _logger.LogInformation("Starting database optimization");

                // Get initial database size
                result.SizeBeforeBytes = await GetDatabaseSizeAsync(cancellationToken);

                // Analyze database for optimization opportunities
                await _context.Database.ExecuteSqlRawAsync("ANALYZE", cancellationToken);

                // Rebuild indexes
                await _context.Database.ExecuteSqlRawAsync("REINDEX", cancellationToken);

                // Vacuum to reclaim space
                await _context.Database.ExecuteSqlRawAsync("VACUUM", cancellationToken);

                // Update statistics
                await _context.Database.ExecuteSqlRawAsync("ANALYZE", cancellationToken);

                result.SizeAfterBytes = await GetDatabaseSizeAsync(cancellationToken);
                result.SpaceSavedBytes = result.SizeBeforeBytes - result.SizeAfterBytes;
                result.Success = true;
                result.EndTime = DateTime.UtcNow;

                _logger.LogInformation("Database optimization completed. Space saved: {SpaceSaved:F2} MB",
                    result.SpaceSavedBytes / (1024.0 * 1024.0));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database optimization failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.EndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Gets current database size in bytes
        /// </summary>
        private async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var sizeQuery = "SELECT page_count * page_size as size FROM pragma_page_count(), pragma_page_size()";
                return await _context.Database
                    .SqlQueryRaw<long>(sizeQuery)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Extracts database file path from connection string
        /// </summary>
        private string ExtractDatabasePath(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string is null or empty");

            var dataSourceIndex = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
            if (dataSourceIndex >= 0)
            {
                var start = dataSourceIndex + "Data Source=".Length;
                var end = connectionString.IndexOf(';', start);
                if (end < 0) end = connectionString.Length;

                return connectionString.Substring(start, end - start).Trim();
            }

            throw new ArgumentException("Could not extract database path from connection string");
        }
    }

    /// <summary>
    /// Base interface for database migrations
    /// </summary>
    public interface IDatabaseMigration
    {
        /// <summary>
        /// Migration version
        /// </summary>
        DatabaseVersion Version { get; }

        /// <summary>
        /// Migration description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Applies the migration
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ApplyAsync(DocHelperDbContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the migration (optional)
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RollbackAsync(DocHelperDbContext context, CancellationToken cancellationToken = default);
    }
}