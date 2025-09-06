using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Data;
using Doc_Helper.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Doc_Helper.Infrastructure.Migration
{
    /// <summary>
    /// Migrates hyperlink data from Excel files to SQLite database
    /// Handles initial data migration and ongoing synchronization
    /// </summary>
    public class ExcelToSqliteMigrator : IExcelToSqliteMigrator
    {
        private readonly ILogger<ExcelToSqliteMigrator> _logger;
        private readonly DocHelperDbContext _context;
        private readonly ICacheService _cacheService;
        private readonly MigrationOptions _options;

        public ExcelToSqliteMigrator(
            ILogger<ExcelToSqliteMigrator> logger,
            DocHelperDbContext context,
            ICacheService cacheService,
            MigrationOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _options = options ?? new MigrationOptions();

            // Configure EPPlus for Excel processing
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Performs complete migration from Excel file to SQLite database
        /// </summary>
        public async Task<MigrationResult> MigrateFromExcelAsync(
            string excelFilePath,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var result = new MigrationResult
            {
                SourceFile = excelFilePath,
                StartTime = startTime
            };

            try
            {
                _logger.LogInformation("Starting Excel to SQLite migration from {ExcelFile}", excelFilePath);

                // Validate Excel file
                progress?.Report(new MigrationProgress { Stage = "Validation", Message = "Validating Excel file" });
                await ValidateExcelFileAsync(excelFilePath, cancellationToken);

                // Read Excel data
                progress?.Report(new MigrationProgress { Stage = "Reading", Message = "Reading Excel data" });
                var hyperlinkData = await ReadExcelDataAsync(excelFilePath, progress, cancellationToken);

                result.TotalRecords = hyperlinkData.Count;
                _logger.LogInformation("Read {RecordCount} records from Excel file", hyperlinkData.Count);

                // Migrate to SQLite
                progress?.Report(new MigrationProgress { Stage = "Migration", Message = "Migrating to SQLite" });
                var migratedCount = await MigrateToSqliteAsync(hyperlinkData, progress, cancellationToken);

                result.MigratedRecords = migratedCount;
                result.Success = true;
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;

                _logger.LogInformation(
                    "Migration completed successfully: {MigratedRecords}/{TotalRecords} records in {Duration:F2}s",
                    result.MigratedRecords, result.TotalRecords, result.Duration.TotalSeconds);

                // Cache migration metadata
                await _cacheService.SetAsync($"migration_result_{Path.GetFileName(excelFilePath)}",
                    result, TimeSpan.FromDays(1), cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration failed for {ExcelFile}", excelFilePath);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
                return result;
            }
        }

        /// <summary>
        /// Synchronizes data between Excel file and SQLite database
        /// </summary>
        public async Task<SyncResult> SynchronizeDataAsync(
            string excelFilePath,
            DateTime? lastSyncTime = null,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new SyncResult
            {
                SourceFile = excelFilePath,
                LastSyncTime = lastSyncTime,
                SyncStartTime = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Starting data synchronization from {ExcelFile}", excelFilePath);

                // Check if Excel file has been modified since last sync
                var fileInfo = new FileInfo(excelFilePath);
                if (lastSyncTime.HasValue && fileInfo.LastWriteTime <= lastSyncTime.Value)
                {
                    _logger.LogInformation("Excel file has not been modified since last sync, skipping");
                    result.Success = true;
                    result.SyncAction = SyncAction.NoChanges;
                    return result;
                }

                progress?.Report(new MigrationProgress { Stage = "Comparison", Message = "Comparing Excel data with database" });

                // Read current Excel data
                var excelData = await ReadExcelDataAsync(excelFilePath, progress, cancellationToken);

                // Get existing database data
                var existingData = await GetExistingDatabaseDataAsync(cancellationToken);

                // Determine changes
                var changes = await AnalyzeChangesAsync(excelData, existingData, cancellationToken);

                progress?.Report(new MigrationProgress
                {
                    Stage = "Synchronization",
                    Message = $"Applying {changes.TotalChanges} changes"
                });

                // Apply changes to database
                await ApplyChangesAsync(changes, progress, cancellationToken);

                result.AddedRecords = changes.AddedRecords.Count;
                result.UpdatedRecords = changes.UpdatedRecords.Count;
                result.DeletedRecords = changes.DeletedRecords.Count;
                result.Success = true;
                result.SyncAction = changes.TotalChanges > 0 ? SyncAction.Updated : SyncAction.NoChanges;
                result.SyncEndTime = DateTime.UtcNow;

                _logger.LogInformation(
                    "Synchronization completed: +{Added} ~{Updated} -{Deleted} records",
                    result.AddedRecords, result.UpdatedRecords, result.DeletedRecords);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Synchronization failed for {ExcelFile}", excelFilePath);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.SyncEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Validates Excel file format and accessibility
        /// </summary>
        private async Task ValidateExcelFileAsync(string excelFilePath, CancellationToken cancellationToken)
        {
            if (!File.Exists(excelFilePath))
                throw new FileNotFoundException($"Excel file not found: {excelFilePath}");

            var fileInfo = new FileInfo(excelFilePath);
            if (fileInfo.Length == 0)
                throw new InvalidOperationException($"Excel file is empty: {excelFilePath}");

            if (!excelFilePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"File must be an Excel (.xlsx) file: {excelFilePath}");

            // Test file accessibility
            try
            {
                using var package = new ExcelPackage(fileInfo);
                if (package.Workbook.Worksheets.Count == 0)
                    throw new InvalidOperationException("Excel file contains no worksheets");
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"Cannot open Excel file: {ex.Message}", ex);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Reads hyperlink data from Excel file
        /// </summary>
        private async Task<List<HyperlinkData>> ReadExcelDataAsync(
            string excelFilePath,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var hyperlinks = new List<HyperlinkData>();
            var fileInfo = new FileInfo(excelFilePath);

            using var package = new ExcelPackage(fileInfo);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
                throw new InvalidOperationException("No worksheets found in Excel file");

            var rowCount = worksheet.Dimension?.Rows ?? 0;
            if (rowCount <= 1) // Only header row or empty
            {
                _logger.LogWarning("Excel file appears to be empty or contains only headers");
                return hyperlinks;
            }

            _logger.LogInformation("Processing {RowCount} rows from Excel worksheet", rowCount);

            // Read data starting from row 2 (assuming row 1 is headers)
            for (int row = 2; row <= rowCount; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var hyperlinkData = await ReadExcelRowAsync(worksheet, row, cancellationToken);
                    if (hyperlinkData != null)
                    {
                        hyperlinks.Add(hyperlinkData);
                    }

                    // Report progress every 100 rows
                    if (row % 100 == 0)
                    {
                        progress?.Report(new MigrationProgress
                        {
                            Stage = "Reading",
                            Message = $"Processed {row - 1} of {rowCount - 1} records",
                            ProcessedCount = row - 1,
                            TotalCount = rowCount - 1
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading Excel row {RowNumber}, skipping", row);
                    continue;
                }
            }

            _logger.LogInformation("Successfully read {HyperlinkCount} hyperlinks from Excel", hyperlinks.Count);
            return hyperlinks;
        }

        /// <summary>
        /// Reads a single row from Excel worksheet
        /// </summary>
        private async Task<HyperlinkData> ReadExcelRowAsync(
            ExcelWorksheet worksheet,
            int row,
            CancellationToken cancellationToken)
        {
            // Expected columns: Document_ID, Content_ID, Title, Status, Address, SubAddress, etc.
            var documentId = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
            var contentId = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
            var title = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
            var status = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
            var address = worksheet.Cells[row, 5].Value?.ToString()?.Trim();
            var subAddress = worksheet.Cells[row, 6].Value?.ToString()?.Trim();

            // Skip empty rows
            if (string.IsNullOrEmpty(documentId) && string.IsNullOrEmpty(contentId) && string.IsNullOrEmpty(title))
                return null;

            return new HyperlinkData
            {
                Address = address ?? string.Empty,
                SubAddress = subAddress ?? string.Empty,
                TextToDisplay = title ?? string.Empty,
                Title = title ?? string.Empty,
                ContentID = contentId ?? string.Empty,
                Status = status ?? string.Empty,
                PageNumber = 1, // Default since Excel doesn't have page info
                LineNumber = row - 1 // Use row number as line reference
            };
        }

        /// <summary>
        /// Migrates hyperlink data to SQLite database
        /// </summary>
        private async Task<int> MigrateToSqliteAsync(
            List<HyperlinkData> hyperlinkData,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            int migratedCount = 0;
            var batchSize = _options.BatchSize;
            var totalBatches = (int)Math.Ceiling((double)hyperlinkData.Count / batchSize);

            _logger.LogInformation("Migrating {RecordCount} records in {BatchCount} batches of {BatchSize}",
                hyperlinkData.Count, totalBatches, batchSize);

            for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = hyperlinkData
                    .Skip(batchIndex * batchSize)
                    .Take(batchSize)
                    .ToList();

                var batchResult = await ProcessBatchAsync(batch, cancellationToken);
                migratedCount += batchResult;

                progress?.Report(new MigrationProgress
                {
                    Stage = "Migration",
                    Message = $"Migrated batch {batchIndex + 1} of {totalBatches}",
                    ProcessedCount = (batchIndex + 1) * batchSize,
                    TotalCount = hyperlinkData.Count
                });

                _logger.LogDebug("Completed batch {BatchIndex}: {BatchCount} records migrated",
                    batchIndex + 1, batchResult);
            }

            return migratedCount;
        }

        /// <summary>
        /// Processes a batch of hyperlink data for database insertion
        /// </summary>
        private async Task<int> ProcessBatchAsync(List<HyperlinkData> batch, CancellationToken cancellationToken)
        {
            var entities = new List<HyperlinkEntity>();

            foreach (var hyperlink in batch)
            {
                try
                {
                    var entity = new HyperlinkEntity
                    {
                        Address = hyperlink.Address,
                        SubAddress = hyperlink.SubAddress,
                        TextToDisplay = hyperlink.TextToDisplay,
                        Title = hyperlink.Title,
                        ContentID = hyperlink.ContentID,
                        Status = hyperlink.Status,
                        PageNumber = hyperlink.PageNumber,
                        LineNumber = hyperlink.LineNumber,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    entities.Add(entity);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error converting hyperlink data to entity, skipping record");
                }
            }

            if (entities.Count > 0)
            {
                await _context.Hyperlinks.AddRangeAsync(entities, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return entities.Count;
        }

        /// <summary>
        /// Gets existing database data for comparison
        /// </summary>
        private async Task<List<HyperlinkEntity>> GetExistingDatabaseDataAsync(CancellationToken cancellationToken)
        {
            return await _context.Hyperlinks
                .Where(h => !h.IsDeleted)
                .OrderBy(h => h.ContentID)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Analyzes differences between Excel data and database data
        /// </summary>
        private async Task<DataChanges> AnalyzeChangesAsync(
            List<HyperlinkData> excelData,
            List<HyperlinkEntity> databaseData,
            CancellationToken cancellationToken)
        {
            var changes = new DataChanges();

            // Create lookup for database data by content ID
            var databaseLookup = databaseData
            .Where(d => !string.IsNullOrEmpty(d.ContentID))
            .ToDictionary(d => d.ContentID, d => d);

            // Create lookup for Excel data by content ID
            var excelLookup = excelData
                .Where(e => !string.IsNullOrEmpty(e.ContentID))
                .ToDictionary(e => e.ContentID, e => e);

            // Find new records (in Excel but not in database)
            foreach (var excelRecord in excelData)
            {
                if (string.IsNullOrEmpty(excelRecord.ContentID)) continue;

                if (!databaseLookup.ContainsKey(excelRecord.ContentID))
                {
                    changes.AddedRecords.Add(excelRecord);
                }
                else
                {
                    // Check for updates
                    var existingEntity = databaseLookup[excelRecord.ContentID];
                    if (HasChanges(excelRecord, existingEntity))
                    {
                        changes.UpdatedRecords.Add(new UpdateRecord
                        {
                            ExcelData = excelRecord,
                            DatabaseEntity = ConvertEntityToHyperlinkData(existingEntity)
                        });
                    }
                }
            }

            // Find deleted records (in database but not in Excel)
            foreach (var dbRecord in databaseData)
            {
                if (string.IsNullOrEmpty(dbRecord.ContentID)) continue;

                if (!excelLookup.ContainsKey(dbRecord.ContentID))
                {
                    changes.DeletedRecords.Add(ConvertEntityToHyperlinkData(dbRecord));
                }
            }

            _logger.LogInformation("Change analysis: +{Added} ~{Updated} -{Deleted}",
                changes.AddedRecords.Count, changes.UpdatedRecords.Count, changes.DeletedRecords.Count);

            return changes;
        }

        /// <summary>
        /// Applies data changes to the database
        /// </summary>
        private async Task ApplyChangesAsync(
            DataChanges changes,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            int totalOperations = changes.TotalChanges;
            int processedOperations = 0;

            // Add new records
            if (changes.AddedRecords.Count > 0)
            {
                var newEntities = changes.AddedRecords.Select(h => new HyperlinkEntity
                {
                    Address = h.Address,
                    SubAddress = h.SubAddress,
                    TextToDisplay = h.TextToDisplay,
                    Title = h.Title,
                    ContentID = h.ContentID,
                    Status = h.Status,
                    PageNumber = h.PageNumber,
                    LineNumber = h.LineNumber,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

                await _context.Hyperlinks.AddRangeAsync(newEntities, cancellationToken);
                processedOperations += changes.AddedRecords.Count;

                progress?.Report(new MigrationProgress
                {
                    Stage = "Synchronization",
                    Message = $"Added {changes.AddedRecords.Count} new records",
                    ProcessedCount = processedOperations,
                    TotalCount = totalOperations
                });
            }

            // Update existing records
            foreach (var updateRecord in changes.UpdatedRecords)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Find the entity in the database and update it
                var entityToUpdate = await _context.Hyperlinks
                    .FirstOrDefaultAsync(h => h.ContentID == updateRecord.ExcelData.ContentID, cancellationToken);

                if (entityToUpdate != null)
                {
                    entityToUpdate.Address = updateRecord.ExcelData.Address;
                    entityToUpdate.SubAddress = updateRecord.ExcelData.SubAddress;
                    entityToUpdate.TextToDisplay = updateRecord.ExcelData.TextToDisplay;
                    entityToUpdate.Title = updateRecord.ExcelData.Title;
                    entityToUpdate.Status = updateRecord.ExcelData.Status;
                    entityToUpdate.UpdatedAt = DateTime.UtcNow;

                    _context.Hyperlinks.Update(entityToUpdate);
                }
                processedOperations++;

                if (processedOperations % 50 == 0)
                {
                    progress?.Report(new MigrationProgress
                    {
                        Stage = "Synchronization",
                        Message = $"Updated {processedOperations} records",
                        ProcessedCount = processedOperations,
                        TotalCount = totalOperations
                    });
                }
            }

            // Mark deleted records as inactive (soft delete)
            foreach (var deletedRecord in changes.DeletedRecords)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Find the entity in the database and mark as deleted
                var entityToDelete = await _context.Hyperlinks
                    .FirstOrDefaultAsync(h => h.ContentID == deletedRecord.ContentID, cancellationToken);

                if (entityToDelete != null)
                {
                    entityToDelete.IsDeleted = true;
                    entityToDelete.UpdatedAt = DateTime.UtcNow;
                    _context.Hyperlinks.Update(entityToDelete);
                }
                processedOperations++;
            }

            await _context.SaveChangesAsync(cancellationToken);

            progress?.Report(new MigrationProgress
            {
                Stage = "Synchronization",
                Message = "Changes saved to database",
                ProcessedCount = totalOperations,
                TotalCount = totalOperations
            });
        }

        /// <summary>
        /// Checks if Excel data differs from database entity
        /// </summary>
        private bool HasChanges(HyperlinkData excelData, HyperlinkEntity databaseEntity)
        {
            return excelData.Address != databaseEntity.Address ||
                   excelData.SubAddress != databaseEntity.SubAddress ||
                   excelData.TextToDisplay != databaseEntity.TextToDisplay ||
                   excelData.Title != databaseEntity.Title ||
                   excelData.Status != databaseEntity.Status;
        }

        /// <summary>
        /// Converts HyperlinkEntity to HyperlinkData for compatibility
        /// </summary>
        private HyperlinkData ConvertEntityToHyperlinkData(HyperlinkEntity entity)
        {
            return new HyperlinkData
            {
                Address = entity.Address,
                SubAddress = entity.SubAddress,
                TextToDisplay = entity.TextToDisplay,
                Title = entity.Title,
                ContentID = entity.ContentID,
                Status = entity.Status,
                PageNumber = entity.PageNumber,
                LineNumber = entity.LineNumber
            };
        }

        /// <summary>
        /// Creates database backup before migration
        /// </summary>
        public async Task<BackupResult> CreateDatabaseBackupAsync(
            string? backupPath = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var defaultBackupPath = $"Backup/database_backup_{timestamp}.db";
                var targetPath = backupPath ?? defaultBackupPath;

                // Ensure backup directory exists
                var backupDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Get database file path
                var connectionString = _context.Database.GetConnectionString();
                var dbPath = ExtractDatabasePathFromConnectionString(connectionString);

                if (File.Exists(dbPath))
                {
                    await Task.Run(() => File.Copy(dbPath, targetPath, overwrite: true), cancellationToken);

                    var backupInfo = new FileInfo(targetPath);

                    _logger.LogInformation("Database backup created: {BackupPath} ({Size:F2} MB)",
                        targetPath, backupInfo.Length / (1024.0 * 1024.0));

                    return new BackupResult
                    {
                        Success = true,
                        BackupPath = targetPath,
                        BackupSize = backupInfo.Length
                    };
                }
                else
                {
                    throw new FileNotFoundException($"Database file not found: {dbPath}");
                }
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
        /// Extracts database file path from connection string
        /// </summary>
        private string ExtractDatabasePathFromConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Database connection string is null or empty");

            // Handle SQLite connection string format
            var dataSourceIndex = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
            if (dataSourceIndex >= 0)
            {
                var start = dataSourceIndex + "Data Source=".Length;
                var end = connectionString.IndexOf(';', start);
                if (end < 0) end = connectionString.Length;

                return connectionString.Substring(start, end - start).Trim();
            }

            throw new InvalidOperationException("Could not extract database path from connection string");
        }

        public void Dispose()
        {
            // Context is managed by DI container, don't dispose here
        }
    }
}