using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces;

/// <summary>
/// Service for managing changelogs per document and batch operations
/// </summary>
public interface IChangelogService
{
    /// <summary>
    /// Creates a changelog for a single document
    /// </summary>
    /// <param name="filePath">Path to the processed file</param>
    /// <param name="processingResults">Results from various processing operations</param>
    /// <returns>Changelog content</returns>
    Task<string> CreateDocumentChangelogAsync(string filePath, DocumentProcessingResults processingResults);

    /// <summary>
    /// Creates a combined changelog for batch processing
    /// </summary>
    /// <param name="batchResults">Results from batch processing</param>
    /// <returns>Combined changelog content</returns>
    Task<string> CreateBatchChangelogAsync(BatchProcessingResults batchResults);

    /// <summary>
    /// Saves changelog to file
    /// </summary>
    /// <param name="content">Changelog content</param>
    /// <param name="filePath">Path where to save the changelog</param>
    Task SaveChangelogAsync(string content, string filePath);

    /// <summary>
    /// Gets changelog for a specific document
    /// </summary>
    /// <param name="documentPath">Original document path</param>
    /// <returns>Changelog content if exists, null otherwise</returns>
    Task<string?> GetDocumentChangelogAsync(string documentPath);

    /// <summary>
    /// Exports changelog to downloads folder
    /// </summary>
    /// <param name="content">Changelog content</param>
    /// <param name="fileName">Optional custom filename</param>
    /// <returns>Path to the exported file</returns>
    Task<string> ExportChangelogToDownloadsAsync(string content, string? fileName = null);

    /// <summary>
    /// Gets all available changelogs for a document
    /// </summary>
    /// <param name="documentPath">Original document path</param>
    /// <returns>List of changelog file paths</returns>
    IEnumerable<string> GetChangelogHistory(string documentPath);

    /// <summary>
    /// Cleans up old changelog files
    /// </summary>
    Task CleanupOldChangelogsAsync();

    /// <summary>
    /// Gets the changelog file path for a document
    /// </summary>
    /// <param name="documentPath">Original document path</param>
    /// <returns>Full path where the changelog would be saved</returns>
    string GetChangelogPath(string documentPath);
}

/// <summary>
/// Contains processing results for a single document
/// </summary>
public class DocumentProcessingResults
{
    public string DocumentPath { get; set; } = string.Empty;
    public DateTime ProcessingStartTime { get; set; }
    public DateTime ProcessingEndTime { get; set; }
    
    public HyperlinkProcessingResult? HyperlinkResult { get; set; }
    public TitleUpdateResult? TitleUpdateResult { get; set; }
    public TitleUpdateResult? TitleDetectionResult { get; set; }
    public ContentIdAppendResult? ContentIdResult { get; set; }
    public HyperlinkCleanupResult? CleanupResult { get; set; }
    public InternalLinkFixResult? InternalLinkResult { get; set; }
    public HyperlinkReplacementResult? HyperlinkReplacementResult { get; set; }
    public TextReplacementResult? TextReplacementResult { get; set; }
    
    public int DoubleSpacesFixed { get; set; }
    public bool BackupCreated { get; set; }
    public string? BackupPath { get; set; }
}

/// <summary>
/// Contains processing results for multiple documents
/// </summary>
public class BatchProcessingResults
{
    public DateTime BatchStartTime { get; set; }
    public DateTime BatchEndTime { get; set; }
    public int TotalDocuments { get; set; }
    public int SuccessfulDocuments { get; set; }
    public int FailedDocuments { get; set; }
    
    public List<DocumentProcessingResults> DocumentResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}