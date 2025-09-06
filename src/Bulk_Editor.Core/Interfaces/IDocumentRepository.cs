using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces;

/// <summary>
/// Repository interface for document data operations
/// </summary>
public interface IDocumentRepository : IRepository<DocumentData>
{
    // Document-specific query operations
    Task<DocumentData?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken = default);
    Task<DocumentData?> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentData>> GetByProcessingStatusAsync(string processingStatus, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentData>> GetModifiedSinceAsync(DateTime modifiedSince, CancellationToken cancellationToken = default);

    // Document with hyperlinks
    Task<DocumentData?> GetWithHyperlinksAsync(int documentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentData>> GetAllWithHyperlinksAsync(CancellationToken cancellationToken = default);

    // Processing operations
    Task<IEnumerable<DocumentData>> GetPendingProcessingAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentData>> GetFailedProcessingAsync(CancellationToken cancellationToken = default);
    Task<int> UpdateProcessingStatusAsync(int documentId, string status, string? notes = null, CancellationToken cancellationToken = default);
    Task<int> UpdateHyperlinkCountsAsync(int documentId, int hyperlinkCount, int processedCount, int failedCount, CancellationToken cancellationToken = default);

    // File operations
    Task<bool> ExistsByPathAsync(string filePath, CancellationToken cancellationToken = default);
    Task<bool> ExistsByHashAsync(string fileHash, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentData>> GetOutdatedDocumentsAsync(CancellationToken cancellationToken = default);

    // Excel sync operations
    Task<IEnumerable<DocumentData>> GetBySyncStatusAsync(DateTime? lastSyncedBefore = null, CancellationToken cancellationToken = default);
    Task<int> UpdateSyncStatusAsync(int documentId, DateTime syncTimestamp, string? excelPath = null, CancellationToken cancellationToken = default);

    // Statistics
    Task<Dictionary<string, int>> GetProcessingStatusCountsAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetDocumentTypeCountsAsync(CancellationToken cancellationToken = default);
    Task<long> GetTotalFileSizeAsync(CancellationToken cancellationToken = default);

    // Bulk operations
    Task<int> BulkUpdateProcessingStatusAsync(IEnumerable<int> documentIds, string status, CancellationToken cancellationToken = default);
    Task<int> DeleteOrphanedDocumentsAsync(CancellationToken cancellationToken = default);

    // File hash operations
    string GenerateFileHash(string filePath);
    Task<bool> ValidateFileIntegrityAsync(int documentId, CancellationToken cancellationToken = default);
}