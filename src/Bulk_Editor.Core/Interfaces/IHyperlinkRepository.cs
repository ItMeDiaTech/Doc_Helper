using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces;

/// <summary>
/// Repository interface for hyperlink data operations
/// </summary>
public interface IHyperlinkRepository : IRepository<HyperlinkData>
{
    // Hyperlink-specific query operations
    Task<IEnumerable<HyperlinkData>> GetByDocumentIdAsync(int documentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<HyperlinkData>> GetByAddressAsync(string address, CancellationToken cancellationToken = default);
    Task<IEnumerable<HyperlinkData>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<IEnumerable<HyperlinkData>> GetByProcessingStatusAsync(string processingStatus, CancellationToken cancellationToken = default);
    Task<HyperlinkData?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);

    // Search operations
    Task<IEnumerable<HyperlinkData>> SearchByTextAsync(string searchText, CancellationToken cancellationToken = default);
    Task<IEnumerable<HyperlinkData>> GetDuplicatesAsync(CancellationToken cancellationToken = default);

    // Processing operations
    Task<IEnumerable<HyperlinkData>> GetPendingProcessingAsync(int? documentId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<HyperlinkData>> GetFailedProcessingAsync(int? documentId = null, CancellationToken cancellationToken = default);
    Task<int> UpdateProcessingStatusAsync(IEnumerable<int> hyperlinkIds, string status, string? notes = null, CancellationToken cancellationToken = default);

    // Statistics
    Task<Dictionary<string, int>> GetStatusCountsAsync(int? documentId = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetProcessingStatusCountsAsync(int? documentId = null, CancellationToken cancellationToken = default);

    // Bulk operations
    Task<int> BulkInsertWithHashAsync(IEnumerable<HyperlinkData> hyperlinks, CancellationToken cancellationToken = default);
    Task<int> DeduplicateAsync(CancellationToken cancellationToken = default);

    // Content hash operations
    string GenerateContentHash(HyperlinkData hyperlink);
    Task<bool> ExistsWithHashAsync(string contentHash, CancellationToken cancellationToken = default);
}