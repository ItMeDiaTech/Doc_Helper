using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Doc_Helper.Shared.Models.Configuration;

namespace Doc_Helper.Infrastructure.Services;

/// <summary>
/// Service for managing changelogs per document and batch operations
/// </summary>
public class ChangelogService : IChangelogService
{
    private readonly ILogger<ChangelogService> _logger;
    private readonly AppOptions _appOptions;
    private readonly ChangelogBuilder _changelogBuilder;

    public ChangelogService(
        ILogger<ChangelogService> logger, 
        IOptions<AppOptions> appOptions,
        ChangelogBuilder changelogBuilder)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appOptions = appOptions?.Value ?? throw new ArgumentNullException(nameof(appOptions));
        _changelogBuilder = changelogBuilder ?? throw new ArgumentNullException(nameof(changelogBuilder));
    }

    /// <summary>
    /// Creates a changelog for a single document
    /// </summary>
    public async Task<string> CreateDocumentChangelogAsync(string filePath, DocumentProcessingResults processingResults)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var duration = processingResults.ProcessingEndTime - processingResults.ProcessingStartTime;

            var header = $"Bulk Editor: Changelog - {DateTime.Now}\n" +
                        $"Version: {_appOptions.Version}\n" +
                        $"Document: {fileName}\n" +
                        $"Processing Duration: {duration.TotalSeconds:F2} seconds\n";
            
            if (processingResults.BackupCreated && !string.IsNullOrEmpty(processingResults.BackupPath))
            {
                header += $"Backup Created: {Path.GetFileName(processingResults.BackupPath)}\n";
            }
            
            header += "\n";

            // Extract data from results
            var updatedLinks = ExtractUpdatedLinks(processingResults);
            var notFoundLinks = ExtractNotFoundLinks(processingResults);
            var expiredLinks = ExtractExpiredLinks(processingResults);
            var errorLinks = ExtractErrorLinks(processingResults);
            var titleMismatches = ExtractTitleMismatches(processingResults);
            var fixedTitles = ExtractFixedTitles(processingResults);
            var internalIssues = ExtractInternalIssues(processingResults);
            var replacedHyperlinks = ExtractReplacedHyperlinks(processingResults);
            var replacedText = ExtractReplacedText(processingResults);

            var changelogContent = _changelogBuilder.BuildChangelogContent(
                updatedLinks: updatedLinks,
                notFoundLinks: notFoundLinks,
                expiredLinks: expiredLinks,
                errorLinks: errorLinks,
                titleMismatchDetections: titleMismatches,
                fixedMismatchedTitles: fixedTitles,
                internalHyperlinkIssues: internalIssues,
                replacedHyperlinks: replacedHyperlinks,
                replacedTextItems: replacedText,
                doubleSpaceCount: processingResults.DoubleSpacesFixed);

            var fullChangelog = header + changelogContent;

            await Task.CompletedTask;
            return fullChangelog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document changelog for {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Creates a combined changelog for batch processing
    /// </summary>
    public async Task<string> CreateBatchChangelogAsync(BatchProcessingResults batchResults)
    {
        try
        {
            var duration = batchResults.BatchEndTime - batchResults.BatchStartTime;

            var header = $"Bulk Editor: Batch Changelog - {DateTime.Now}\n" +
                        $"Version: {_appOptions.Version}\n" +
                        $"Batch Processing Summary:\n" +
                        $"  Total Documents: {batchResults.TotalDocuments}\n" +
                        $"  Successful: {batchResults.SuccessfulDocuments}\n" +
                        $"  Failed: {batchResults.FailedDocuments}\n" +
                        $"  Duration: {duration.TotalSeconds:F2} seconds\n\n";

            var combinedContent = header;

            // Process each document
            foreach (var docResult in batchResults.DocumentResults)
            {
                var docName = Path.GetFileNameWithoutExtension(docResult.DocumentPath);
                combinedContent += $"=== {docName} ===\n";
                
                var docChangelog = await CreateDocumentChangelogAsync(docResult.DocumentPath, docResult);
                // Remove header from individual changelog
                var contentStart = docChangelog.IndexOf("Updated Links");
                if (contentStart > 0)
                {
                    combinedContent += docChangelog[contentStart..];
                }
                else
                {
                    combinedContent += docChangelog;
                }
                
                combinedContent += "\n";
            }

            // Add batch errors if any
            if (batchResults.Errors.Any())
            {
                combinedContent += "=== Batch Processing Errors ===\n";
                foreach (var error in batchResults.Errors)
                {
                    combinedContent += $"    {error}\n";
                }
                combinedContent += "\n";
            }

            return combinedContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating batch changelog");
            throw;
        }
    }

    /// <summary>
    /// Saves changelog to file
    /// </summary>
    public async Task SaveChangelogAsync(string content, string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, content);
            _logger.LogInformation("Changelog saved to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changelog to {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Gets changelog for a specific document
    /// </summary>
    public async Task<string?> GetDocumentChangelogAsync(string documentPath)
    {
        try
        {
            var changelogPath = GetChangelogPath(documentPath);
            if (File.Exists(changelogPath))
            {
                return await File.ReadAllTextAsync(changelogPath);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading changelog for {DocumentPath}", documentPath);
            return null;
        }
    }

    /// <summary>
    /// Exports changelog to downloads folder
    /// </summary>
    public async Task<string> ExportChangelogToDownloadsAsync(string content, string? fileName = null)
    {
        try
        {
            var downloadsPath = GetDownloadsPath();
            fileName ??= $"BulkEditor_Changelog_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            
            var fullPath = Path.Combine(downloadsPath, fileName);
            
            // Ensure unique filename
            var counter = 1;
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            
            while (File.Exists(fullPath))
            {
                fullPath = Path.Combine(downloadsPath, $"{baseName}_{counter}{extension}");
                counter++;
            }

            await File.WriteAllTextAsync(fullPath, content);
            _logger.LogInformation("Changelog exported to {FilePath}", fullPath);
            
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting changelog to downloads");
            throw;
        }
    }

    /// <summary>
    /// Gets all available changelogs for a document
    /// </summary>
    public IEnumerable<string> GetChangelogHistory(string documentPath)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(documentPath);
            var changelogDir = GetChangelogDirectory(documentPath);
            
            if (!Directory.Exists(changelogDir))
            {
                return Enumerable.Empty<string>();
            }

            var pattern = $"{fileName}_changelog_*.txt";
            return Directory.GetFiles(changelogDir, pattern)
                .OrderByDescending(File.GetCreationTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting changelog history for {DocumentPath}", documentPath);
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Cleans up old changelog files
    /// </summary>
    public async Task CleanupOldChangelogsAsync()
    {
        try
        {
            var retentionDate = DateTime.UtcNow.AddDays(-_appOptions.Processing.BackupRetentionDays);
            var changelogRoot = GetChangelogRootDirectory();
            
            if (!Directory.Exists(changelogRoot)) return;

            await Task.Run(() =>
            {
                var oldChangelogs = Directory.GetFiles(changelogRoot, "*.txt", SearchOption.AllDirectories)
                    .Where(file => File.GetCreationTime(file) < retentionDate)
                    .ToList();

                foreach (var oldChangelog in oldChangelogs)
                {
                    try
                    {
                        File.Delete(oldChangelog);
                        _logger.LogDebug("Deleted old changelog {FilePath}", oldChangelog);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old changelog {FilePath}", oldChangelog);
                    }
                }

                _logger.LogInformation("Cleaned up {Count} old changelog files", oldChangelogs.Count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during changelog cleanup");
        }
    }

    /// <summary>
    /// Gets the changelog file path for a document
    /// </summary>
    public string GetChangelogPath(string documentPath)
    {
        return GenerateChangelogPath(documentPath);
    }

    #region Private Helper Methods

    private string GenerateChangelogPath(string documentPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(documentPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var changelogFileName = $"{fileName}_changelog_{timestamp}.txt";
        var changelogDir = GetChangelogDirectory(documentPath);
        
        return Path.Combine(changelogDir, changelogFileName);
    }

    private string GetChangelogDirectory(string documentPath)
    {
        if (_appOptions.Ui.ShowIndividualChangelogs)
        {
            return Path.Combine(GetChangelogRootDirectory(), "Documents");
        }
        else
        {
            var docDir = Path.GetDirectoryName(documentPath) ?? string.Empty;
            return Path.Combine(docDir, "Changelogs");
        }
    }

    private string GetChangelogRootDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DocHelper",
            "Changelogs"
        );
    }

    private string GetDownloadsPath()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads))
        {
            downloads = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
        return downloads;
    }

    // Extract methods for different types of results
    private List<string> ExtractUpdatedLinks(DocumentProcessingResults results)
    {
        var links = new List<string>();
        
        if (results.TitleUpdateResult?.Success == true)
        {
            links.AddRange(results.TitleUpdateResult.TitleChanges);
        }
        
        return links;
    }

    private List<string> ExtractNotFoundLinks(DocumentProcessingResults results)
    {
        return new List<string>(); // TODO: Extract from processing results
    }

    private List<string> ExtractExpiredLinks(DocumentProcessingResults results)
    {
        return new List<string>(); // TODO: Extract from processing results
    }

    private List<string> ExtractErrorLinks(DocumentProcessingResults results)
    {
        return new List<string>(); // TODO: Extract from processing results
    }

    private List<string> ExtractTitleMismatches(DocumentProcessingResults results)
    {
        var mismatches = new List<string>();
        
        if (results.TitleDetectionResult?.Success == true)
        {
            mismatches.AddRange(results.TitleDetectionResult.TitleChanges);
        }
        
        return mismatches;
    }

    private List<string> ExtractFixedTitles(DocumentProcessingResults results)
    {
        return new List<string>(); // TODO: Extract from processing results
    }

    private List<string> ExtractInternalIssues(DocumentProcessingResults results)
    {
        var issues = new List<string>();
        
        if (results.InternalLinkResult?.Success == true)
        {
            issues.AddRange(results.InternalLinkResult.FixedLinkDetails);
        }
        
        return issues;
    }

    private List<string> ExtractReplacedHyperlinks(DocumentProcessingResults results)
    {
        var replaced = new List<string>();
        
        if (results.HyperlinkReplacementResult?.Success == true)
        {
            replaced.AddRange(results.HyperlinkReplacementResult.ReplacedHyperlinks);
        }
        
        return replaced;
    }

    private List<string> ExtractReplacedText(DocumentProcessingResults results)
    {
        var replaced = new List<string>();
        
        if (results.TextReplacementResult?.Success == true)
        {
            replaced.AddRange(results.TextReplacementResult.ReplacedTextItems);
        }
        
        return replaced;
    }

    #endregion
}