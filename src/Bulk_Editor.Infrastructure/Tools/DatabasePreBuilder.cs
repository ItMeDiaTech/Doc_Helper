using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Data;
using Doc_Helper.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace Doc_Helper.Infrastructure.Tools
{
    /// <summary>
    /// Pre-builds SQLite database from Excel file for installer inclusion
    /// Creates optimized, production-ready database with 100k+ hyperlink records
    /// Eliminates need for initial API calls and provides instant application startup
    /// </summary>
    public class DatabasePreBuilder
    {
        private readonly ILogger<DatabasePreBuilder> _logger;
        private readonly string _outputDatabasePath;

        public DatabasePreBuilder(ILogger<DatabasePreBuilder> logger, string outputDatabasePath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _outputDatabasePath = outputDatabasePath ?? throw new ArgumentNullException(nameof(outputDatabasePath));

            // Configure EPPlus for Excel processing
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Builds production SQLite database from Excel source
        /// This creates the database that will be included in the installer
        /// </summary>
        public async Task<DatabaseBuildResult> BuildProductionDatabaseAsync(
            string sourceExcelPath,
            IProgress<DatabaseBuildProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new DatabaseBuildResult
            {
                SourceExcelPath = sourceExcelPath,
                OutputDatabasePath = _outputDatabasePath,
                BuildStartTime = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Building production SQLite database from {ExcelPath}", sourceExcelPath);

                // Step 1: Validate source Excel file
                progress?.Report(new DatabaseBuildProgress
                {
                    Stage = "Validation",
                    Message = "Validating source Excel file",
                    PercentComplete = 5
                });

                await ValidateSourceExcelAsync(sourceExcelPath, cancellationToken);

                // Step 2: Create optimized database structure
                progress?.Report(new DatabaseBuildProgress
                {
                    Stage = "Schema",
                    Message = "Creating optimized database schema",
                    PercentComplete = 10
                });

                await CreateOptimizedDatabaseAsync(cancellationToken);

                // Step 3: Import Excel data with optimizations
                progress?.Report(new DatabaseBuildProgress
                {
                    Stage = "Import",
                    Message = "Importing Excel data",
                    PercentComplete = 20
                });

                var importResult = await ImportExcelDataOptimizedAsync(sourceExcelPath, progress, cancellationToken);
                result.TotalRecordsProcessed = importResult.TotalRecords;
                result.RecordsImported = importResult.ImportedRecords;

                // Step 4: Create indexes for performance
                progress?.Report(new DatabaseBuildProgress
                {
                    Stage = "Indexing",
                    Message = "Creating performance indexes",
                    PercentComplete = 85
                });

                await CreatePerformanceIndexesAsync(cancellationToken);

                // Step 5: Optimize database for production
                progress?.Report(new DatabaseBuildProgress
                {
                    Stage = "Optimization",
                    Message = "Optimizing database for production",
                    PercentComplete = 95
                });

                await OptimizeForProductionAsync(cancellationToken);

                // Step 6: Validate final database
                progress?.Report(new DatabaseBuildProgress
                {
                    Stage = "Final Validation",
                    Message = "Validating production database",
                    PercentComplete = 100
                });

                await ValidateProductionDatabaseAsync(result, cancellationToken);

                result.Success = true;
                result.BuildEndTime = DateTime.UtcNow;

                _logger.LogInformation(
                    "Production database built successfully: {Records} records in {Duration:F2}s, Size: {Size:F2} MB",
                    result.RecordsImported, result.BuildDuration.TotalSeconds, result.DatabaseSizeMB);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database build failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.BuildEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Creates optimized database with production-ready configuration
        /// </summary>
        private async Task CreateOptimizedDatabaseAsync(CancellationToken cancellationToken)
        {
            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(_outputDatabasePath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Delete existing database if it exists
            if (File.Exists(_outputDatabasePath))
            {
                File.Delete(_outputDatabasePath);
                _logger.LogInformation("Deleted existing database file");
            }

            // Create optimized connection string for production
            var connectionString = $"Data Source={_outputDatabasePath};Cache=Shared;Journal Mode=WAL;Synchronous=NORMAL;Foreign Keys=True;";

            var options = new DbContextOptionsBuilder<DocHelperDbContext>()
                .UseSqlite(connectionString)
                .EnableSensitiveDataLogging(false) // Disable for production
                .EnableDetailedErrors(false) // Disable for production
                .Options;

            using var context = new DocHelperDbContext(options);

            // Create database with optimized settings
            await context.Database.EnsureCreatedAsync(cancellationToken);

            // Set production SQLite pragmas for optimal performance
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL", cancellationToken);
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL", cancellationToken);
            await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size=10000", cancellationToken); // 40MB cache
            await context.Database.ExecuteSqlRawAsync("PRAGMA temp_store=MEMORY", cancellationToken);
            await context.Database.ExecuteSqlRawAsync("PRAGMA mmap_size=268435456", cancellationToken); // 256MB mmap

            _logger.LogInformation("Created optimized database with production settings");
        }

        /// <summary>
        /// Imports Excel data with high-performance batch processing
        /// </summary>
        private async Task<ImportResult> ImportExcelDataOptimizedAsync(
            string excelPath,
            IProgress<DatabaseBuildProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new ImportResult();
            var batchSize = 5000; // Large batches for performance
            var connectionString = $"Data Source={_outputDatabasePath};Cache=Shared;Journal Mode=WAL;";

            var options = new DbContextOptionsBuilder<DocHelperDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using var context = new DocHelperDbContext(options);
            using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                using var package = new ExcelPackage(new FileInfo(excelPath));
                var worksheet = package.Workbook.Worksheets[0]; // First worksheet
                var rowCount = worksheet.Dimension?.Rows ?? 0;

                _logger.LogInformation("Importing {RowCount} rows from Excel in batches of {BatchSize}",
                    rowCount, batchSize);

                // Disable auto-save during bulk import for performance
                context.ChangeTracker.AutoDetectChangesEnabled = false;

                for (int startRow = 2; startRow <= rowCount; startRow += batchSize) // Skip header row
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var endRow = Math.Min(startRow + batchSize - 1, rowCount);
                    var currentBatch = endRow - startRow + 1;

                    var batch = new List<HyperlinkEntity>();

                    // Process batch of rows
                    for (int row = startRow; row <= endRow; row++)
                    {
                        try
                        {
                            var entity = CreateHyperlinkEntityFromExcelRow(worksheet, row);
                            if (entity != null)
                            {
                                batch.Add(entity);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Error processing Excel row {Row}", row);
                            result.ErrorRows++;
                        }
                    }

                    // Bulk insert batch
                    if (batch.Count > 0)
                    {
                        await context.Hyperlinks.AddRangeAsync(batch, cancellationToken);
                        await context.SaveChangesAsync(cancellationToken);

                        result.ImportedRecords += batch.Count;
                        result.ProcessedBatches++;
                    }

                    result.TotalRecords = endRow - 1; // Exclude header

                    // Report progress
                    var percentComplete = 20 + (int)((double)(endRow - 1) / (rowCount - 1) * 65); // 20-85% for import
                    progress?.Report(new DatabaseBuildProgress
                    {
                        Stage = "Import",
                        Message = $"Imported {result.ImportedRecords:N0} records ({percentComplete}%)",
                        PercentComplete = percentComplete,
                        CurrentBatch = result.ProcessedBatches,
                        RecordsProcessed = result.ImportedRecords
                    });

                    _logger.LogDebug("Imported batch {BatchNum}: rows {StartRow}-{EndRow} ({Count} records)",
                        result.ProcessedBatches, startRow, endRow, batch.Count);
                }

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Import completed: {Imported}/{Total} records imported, {Errors} errors",
                    result.ImportedRecords, result.TotalRecords, result.ErrorRows);

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Excel import failed");
                throw;
            }
        }

        /// <summary>
        /// Creates HyperlinkEntity from Excel row data
        /// </summary>
        private Data.Entities.HyperlinkEntity? CreateHyperlinkEntityFromExcelRow(ExcelWorksheet worksheet, int row)
        {
            try
            {
                // Expected columns: Document_ID, Content_ID, Title, Status, URL, etc.
                var documentId = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                var contentId = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                var title = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                var status = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                var url = worksheet.Cells[row, 5].Value?.ToString()?.Trim();

                // Skip empty rows
                if (string.IsNullOrEmpty(contentId) && string.IsNullOrEmpty(title))
                    return null;

                var entity = new HyperlinkEntity
                {
                    ContentID = contentId ?? string.Empty,
                    Title = title ?? string.Empty,
                    Status = status ?? string.Empty,
                    Address = url ?? string.Empty,
                    TextToDisplay = title ?? string.Empty,
                    SubAddress = string.Empty,
                    PageNumber = 1,
                    LineNumber = row - 1, // Row number as reference
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                return entity;
            }
            catch
            {
                return null; // Skip problematic rows
            }
        }

        /// <summary>
        /// Creates performance indexes for fast lookups
        /// </summary>
        private async Task CreatePerformanceIndexesAsync(CancellationToken cancellationToken)
        {
            var connectionString = $"Data Source={_outputDatabasePath};";
            var options = new DbContextOptionsBuilder<DocHelperDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using var context = new DocHelperDbContext(options);

            var indexes = new[]
            {
                "CREATE INDEX IF NOT EXISTS IX_Hyperlinks_ContentId ON Hyperlinks(ContentId)",
                "CREATE INDEX IF NOT EXISTS IX_Hyperlinks_Status ON Hyperlinks(Status)",
                "CREATE INDEX IF NOT EXISTS IX_Hyperlinks_IsActive ON Hyperlinks(IsActive)",
                "CREATE INDEX IF NOT EXISTS IX_Hyperlinks_Title ON Hyperlinks(Title)",
                "CREATE INDEX IF NOT EXISTS IX_Hyperlinks_ContentId_Status ON Hyperlinks(ContentId, Status)",
                "CREATE INDEX IF NOT EXISTS IX_Documents_FileName ON Documents(FileName)",
                "CREATE INDEX IF NOT EXISTS IX_Documents_IsActive ON Documents(IsActive)"
            };

            foreach (var indexSql in indexes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await context.Database.ExecuteSqlRawAsync(indexSql, cancellationToken);
            }

            _logger.LogInformation("Created {IndexCount} performance indexes", indexes.Length);
        }

        /// <summary>
        /// Optimizes database for production deployment
        /// </summary>
        private async Task OptimizeForProductionAsync(CancellationToken cancellationToken)
        {
            var connectionString = $"Data Source={_outputDatabasePath};";
            var options = new DbContextOptionsBuilder<DocHelperDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using var context = new DocHelperDbContext(options);

            // Analyze database for query optimization
            await context.Database.ExecuteSqlRawAsync("ANALYZE", cancellationToken);

            // Vacuum to reclaim space and optimize
            await context.Database.ExecuteSqlRawAsync("VACUUM", cancellationToken);

            // Set optimal production pragmas
            await context.Database.ExecuteSqlRawAsync("PRAGMA optimize", cancellationToken);

            _logger.LogInformation("Database optimized for production deployment");
        }

        /// <summary>
        /// Validates source Excel file before processing
        /// </summary>
        private async Task ValidateSourceExcelAsync(string excelPath, CancellationToken cancellationToken)
        {
            if (!File.Exists(excelPath))
                throw new FileNotFoundException($"Source Excel file not found: {excelPath}");

            var fileInfo = new FileInfo(excelPath);
            if (fileInfo.Length == 0)
                throw new InvalidOperationException("Source Excel file is empty");

            // Test Excel file accessibility
            using var package = new ExcelPackage(fileInfo);
            if (package.Workbook.Worksheets.Count == 0)
                throw new InvalidOperationException("Excel file contains no worksheets");

            var worksheet = package.Workbook.Worksheets[0];
            var rowCount = worksheet.Dimension?.Rows ?? 0;

            if (rowCount < 2) // Header + at least one data row
                throw new InvalidOperationException("Excel file appears to contain no data");

            _logger.LogInformation("Source Excel validation passed: {RowCount} rows found", rowCount);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates the final production database
        /// </summary>
        private async Task ValidateProductionDatabaseAsync(DatabaseBuildResult result, CancellationToken cancellationToken)
        {
            var connectionString = $"Data Source={_outputDatabasePath};";
            var options = new DbContextOptionsBuilder<DocHelperDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using var context = new DocHelperDbContext(options);

            // Test database connectivity
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
                throw new InvalidOperationException("Cannot connect to created database");

            // Validate record count
            var recordCount = await context.Hyperlinks.CountAsync(cancellationToken);
            if (recordCount == 0)
                throw new InvalidOperationException("Database contains no records");

            // Test query performance
            var startTime = DateTime.UtcNow;
            var sampleRecord = await context.Hyperlinks.FirstOrDefaultAsync(cancellationToken);
            var queryTime = DateTime.UtcNow - startTime;

            if (queryTime.TotalMilliseconds > 1000)
            {
                _logger.LogWarning("Slow query performance detected: {QueryTime}ms", queryTime.TotalMilliseconds);
            }

            // Get database file size
            var dbFileInfo = new FileInfo(_outputDatabasePath);
            result.DatabaseSizeBytes = dbFileInfo.Length;

            _logger.LogInformation("Production database validation passed: {Records} records, {Size:F2} MB, Query time: {QueryTime}ms",
                recordCount, result.DatabaseSizeMB, queryTime.TotalMilliseconds);
        }

        /// <summary>
        /// Generates installer-ready database package
        /// </summary>
        public async Task<PackageResult> CreateInstallerPackageAsync(
            string packageOutputPath,
            CancellationToken cancellationToken = default)
        {
            var result = new PackageResult { PackageStartTime = DateTime.UtcNow };

            try
            {
                _logger.LogInformation("Creating installer package for {DatabasePath}", _outputDatabasePath);

                // Ensure package directory exists
                var packageDir = Path.GetDirectoryName(packageOutputPath);
                if (!string.IsNullOrEmpty(packageDir))
                {
                    Directory.CreateDirectory(packageDir);
                }

                // Copy database to package location with installer-friendly name
                var packageDbPath = Path.Combine(packageDir, "data", "bulkeditor.db");
                var packageDbDir = Path.GetDirectoryName(packageDbPath);
                Directory.CreateDirectory(packageDbDir);

                File.Copy(_outputDatabasePath, packageDbPath, overwrite: true);

                // Create version info file
                var versionInfoPath = Path.Combine(packageDir, "data", "version.json");
                var versionInfo = new
                {
                    DatabaseVersion = "1.0.0",
                    BuildDate = DateTime.UtcNow,
                    RecordCount = result.RecordCount,
                    DatabaseSizeBytes = new FileInfo(packageDbPath).Length,
                    OptimizedForProduction = true
                };

                await File.WriteAllTextAsync(versionInfoPath,
                    System.Text.Json.JsonSerializer.Serialize(versionInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);

                // Create installation script for AppData deployment
                var installScriptPath = Path.Combine(packageDir, "install-data.ps1");
                await CreateAppDataInstallScriptAsync(installScriptPath, cancellationToken);

                result.Success = true;
                result.PackagePath = packageOutputPath;
                result.DatabasePath = packageDbPath;
                result.PackageEndTime = DateTime.UtcNow;

                _logger.LogInformation("Installer package created successfully: {PackagePath}", packageOutputPath);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Package creation failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.PackageEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Creates PowerShell script for AppData installation
        /// </summary>
        private async Task CreateAppDataInstallScriptAsync(string scriptPath, CancellationToken cancellationToken)
        {
            var script = @"
# BulkEditor Database Installation Script
# Installs database to AppData for seamless user experience

$AppDataPath = [Environment]::GetFolderPath('ApplicationData')
$BulkEditorDataPath = Join-Path $AppDataPath 'BulkEditor\Data'

Write-Host 'Installing BulkEditor database to AppData...'

# Create directory structure
if (!(Test-Path $BulkEditorDataPath)) {
    New-Item -ItemType Directory -Path $BulkEditorDataPath -Force | Out-Null
    Write-Host 'Created data directory: $BulkEditorDataPath'
}

# Copy database file
$SourceDb = Join-Path $PSScriptRoot 'data\bulkeditor.db'
$TargetDb = Join-Path $BulkEditorDataPath 'bulkeditor.db'

if (Test-Path $SourceDb) {
    Copy-Item $SourceDb $TargetDb -Force
    Write-Host 'Database installed successfully'

    # Verify installation
    if (Test-Path $TargetDb) {
        $DbSize = (Get-Item $TargetDb).Length / 1MB
        Write-Host ""Database size: $($DbSize.ToString('F2')) MB""
        Write-Host 'Installation completed successfully!'
    } else {
        Write-Error 'Database installation failed - file not found after copy'
        exit 1
    }
} else {
    Write-Error 'Source database file not found: $SourceDb'
    exit 1
}

Write-Host 'BulkEditor is ready to use!'
";

            await File.WriteAllTextAsync(scriptPath, script, cancellationToken);
            _logger.LogInformation("Created AppData installation script: {ScriptPath}", scriptPath);
        }
    }

    /// <summary>
    /// Database build result
    /// </summary>
    public class DatabaseBuildResult
    {
        public string SourceExcelPath { get; set; } = string.Empty;
        public string OutputDatabasePath { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int TotalRecordsProcessed { get; set; }
        public int RecordsImported { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public DateTime BuildStartTime { get; set; }
        public DateTime BuildEndTime { get; set; }
        public TimeSpan BuildDuration => BuildEndTime - BuildStartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        public double DatabaseSizeMB => DatabaseSizeBytes / (1024.0 * 1024.0);
        public double ImportSuccessRate => TotalRecordsProcessed > 0 ? (double)RecordsImported / TotalRecordsProcessed * 100 : 0;
    }

    /// <summary>
    /// Database build progress
    /// </summary>
    public class DatabaseBuildProgress
    {
        public string Stage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int PercentComplete { get; set; }
        public int CurrentBatch { get; set; }
        public int RecordsProcessed { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Import operation result
    /// </summary>
    public class ImportResult
    {
        public int TotalRecords { get; set; }
        public int ImportedRecords { get; set; }
        public int ErrorRows { get; set; }
        public int ProcessedBatches { get; set; }
    }

    /// <summary>
    /// Package creation result
    /// </summary>
    public class PackageResult
    {
        public bool Success { get; set; }
        public string PackagePath { get; set; } = string.Empty;
        public string DatabasePath { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public DateTime PackageStartTime { get; set; }
        public DateTime PackageEndTime { get; set; }
        public TimeSpan PackageDuration => PackageEndTime - PackageStartTime;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }
}