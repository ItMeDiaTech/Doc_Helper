using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Shared.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doc_Helper.Infrastructure.Services;

/// <summary>
/// Service for managing application-wide caching with configurable expiry
/// Provides high-performance memory caching for hyperlink data and document metadata
/// </summary>
public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;
    private readonly AppOptions _appOptions;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    // Cache key prefixes for different data types
    private const string HYPERLINK_PREFIX = "hyperlink:";
    private const string DOCUMENT_PREFIX = "document:";
    private const string STATS_PREFIX = "stats:";
    private const string SEARCH_PREFIX = "search:";

    public CacheService(
        IMemoryCache memoryCache,
        ILogger<CacheService> logger,
        IOptions<AppOptions> appOptions)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        _appOptions = appOptions.Value;
    }

    /// <summary>
    /// Get cached item by key
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(key, out var cachedValue))
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);

                if (cachedValue is T directValue)
                    return directValue;

                if (cachedValue is string jsonValue)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<T>(jsonValue);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize cached value for key: {Key}", key);
                        _memoryCache.Remove(key);
                        return default;
                    }
                }
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return default;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Set cached item with default expiry (12 hours)
    /// </summary>
    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        await SetAsync(key, value, TimeSpan.FromHours(_appOptions.Cache.DefaultExpiryHours), cancellationToken);
    }

    /// <summary>
    /// Set cached item with custom expiry
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry,
                SlidingExpiration = TimeSpan.FromHours(2), // Reset expiry on access
                Priority = CacheItemPriority.Normal,
                Size = CalculateCacheSize(value)
            };

            // Use JSON serialization for complex objects to ensure deep copies
            if (typeof(T).IsClass && typeof(T) != typeof(string))
            {
                try
                {
                    var jsonValue = JsonSerializer.Serialize(value);
                    _memoryCache.Set(key, jsonValue, cacheOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to serialize value for caching, storing direct reference for key: {Key}", key);
                    _memoryCache.Set(key, value, cacheOptions);
                }
            }
            else
            {
                _memoryCache.Set(key, value, cacheOptions);
            }

            _logger.LogDebug("Cache set for key: {Key}, expiry: {Expiry}", key, expiry);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Remove item from cache
    /// </summary>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            _memoryCache.Remove(key);
            _logger.LogDebug("Cache item removed for key: {Key}", key);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Clear all cache items with specific prefix
    /// </summary>
    public async Task ClearByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Note: IMemoryCache doesn't support key enumeration directly
            // This is a limitation - we'd need to track keys separately for this functionality
            _logger.LogWarning("ClearByPrefix not fully supported with IMemoryCache. Consider using distributed cache for this feature.");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Clear all cache items
    /// </summary>
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache is MemoryCache memCache)
            {
                memCache.Compact(1.0); // Removes all entries
                _logger.LogInformation("All cache items cleared");
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Get or create cached item with factory function
    /// </summary>
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default)
    {
        return await GetOrCreateAsync(key, factory, TimeSpan.FromHours(_appOptions.Cache.DefaultExpiryHours), cancellationToken);
    }

    /// <summary>
    /// Get or create cached item with factory function and custom expiry
    /// </summary>
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var cachedValue = await GetAsync<T>(key, cancellationToken);
        if (cachedValue != null)
            return cachedValue;

        _logger.LogDebug("Creating new cache entry for key: {Key}", key);
        var newValue = await factory(cancellationToken);

        if (newValue != null)
            await SetAsync(key, newValue, expiry, cancellationToken);

        return newValue;
    }

    /// <summary>
    /// Generate cache key for hyperlink data
    /// </summary>
    public string GenerateHyperlinkKey(int documentId, string? filter = null)
    {
        return $"{HYPERLINK_PREFIX}{documentId}" + (filter != null ? $":{filter}" : "");
    }

    /// <summary>
    /// Generate cache key for document data
    /// </summary>
    public string GenerateDocumentKey(int documentId)
    {
        return $"{DOCUMENT_PREFIX}{documentId}";
    }

    /// <summary>
    /// Generate cache key for document by path
    /// </summary>
    public string GenerateDocumentPathKey(string filePath)
    {
        return $"{DOCUMENT_PREFIX}path:{Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(filePath))}";
    }

    /// <summary>
    /// Generate cache key for statistics
    /// </summary>
    public string GenerateStatsKey(string statsType, int? documentId = null)
    {
        return $"{STATS_PREFIX}{statsType}" + (documentId.HasValue ? $":{documentId}" : ":global");
    }

    /// <summary>
    /// Generate cache key for search results
    /// </summary>
    public string GenerateSearchKey(string searchText, string searchType = "text")
    {
        var searchHash = Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(searchText));
        return $"{SEARCH_PREFIX}{searchType}:{searchHash}";
    }

    /// <summary>
    /// Invalidate hyperlink-related cache entries
    /// </summary>
    public async Task InvalidateHyperlinkCacheAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = new[]
        {
            GenerateHyperlinkKey(documentId),
            GenerateHyperlinkKey(documentId, "pending"),
            GenerateHyperlinkKey(documentId, "failed"),
            GenerateDocumentKey(documentId),
            GenerateStatsKey("hyperlinks", documentId),
            GenerateStatsKey("processing", documentId)
        };

        foreach (var key in keysToRemove)
        {
            await RemoveAsync(key, cancellationToken);
        }

        _logger.LogDebug("Invalidated hyperlink cache for document: {DocumentId}", documentId);
    }

    /// <summary>
    /// Invalidate document-related cache entries
    /// </summary>
    public async Task InvalidateDocumentCacheAsync(int documentId, CancellationToken cancellationToken = default)
    {
        await InvalidateHyperlinkCacheAsync(documentId, cancellationToken);

        var additionalKeys = new[]
        {
            GenerateStatsKey("documents"),
            GenerateStatsKey("processing")
        };

        foreach (var key in additionalKeys)
        {
            await RemoveAsync(key, cancellationToken);
        }

        _logger.LogDebug("Invalidated document cache for document: {DocumentId}", documentId);
    }

    /// <summary>
    /// Invalidate cache entries matching a pattern
    /// </summary>
    public async Task InvalidatePatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Note: IMemoryCache doesn't support pattern-based removal directly
            // This is a limitation - we'd need to track keys separately for this functionality
            _logger.LogWarning("InvalidatePatternAsync not fully supported with IMemoryCache. Pattern: {Pattern}", pattern);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Calculate approximate cache size for memory management
    /// </summary>
    private static long CalculateCacheSize<T>(T value)
    {
        if (value is string str)
            return str.Length * sizeof(char);

        if (value is ICollection<object> collection)
            return collection.Count * 100; // Rough estimate

        return 100; // Default size estimate
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public Task<CacheStatistics> GetCacheStatisticsAsync(CancellationToken cancellationToken = default)
    {
        // IMemoryCache doesn't provide direct statistics
        // This would require tracking separately or using a distributed cache
        var stats = new CacheStatistics
        {
            TotalEntries = 0, // Not available with IMemoryCache
            MemoryUsage = 0,  // Not available with IMemoryCache
            HitRatio = 0.0,   // Would need separate tracking
            LastClearTime = DateTime.UtcNow
        };

        return Task.FromResult(stats);
    }

    public void Dispose()
    {
        _cacheLock?.Dispose();
    }
}
