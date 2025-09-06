using System;
using System.Threading;
using System.Threading.Tasks;

namespace Doc_Helper.Core.Interfaces
{
    /// <summary>
    /// Interface for cache service operations
    /// </summary>
    public interface ICacheService : IDisposable
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task ClearByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
        Task ClearAllAsync(CancellationToken cancellationToken = default);
        Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);
        Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expiry, CancellationToken cancellationToken = default);

        // Key generation helpers
        string GenerateHyperlinkKey(int documentId, string? filter = null);
        string GenerateDocumentKey(int documentId);
        string GenerateDocumentPathKey(string filePath);
        string GenerateStatsKey(string statsType, int? documentId = null);
        string GenerateSearchKey(string searchText, string searchType = "text");

        // Cache invalidation
        Task InvalidateHyperlinkCacheAsync(int documentId, CancellationToken cancellationToken = default);
        Task InvalidateDocumentCacheAsync(int documentId, CancellationToken cancellationToken = default);
        Task InvalidatePatternAsync(string pattern, CancellationToken cancellationToken = default);

        // Statistics
        Task<CacheStatistics> GetCacheStatisticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Cache statistics for monitoring and diagnostics
    /// </summary>
    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public long MemoryUsage { get; set; }
        public double HitRatio { get; set; }
        public DateTime LastClearTime { get; set; }
    }
}