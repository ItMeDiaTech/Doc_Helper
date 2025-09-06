using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Shared.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Doc_Helper.Infrastructure.Services
{
    /// <summary>
    /// Service for API communication with PowerAutomate and hyperlink data retrieval
    /// </summary>
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiService> _logger;
        private readonly ApiOptions _apiOptions;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public ApiService(
            HttpClient httpClient,
            ILogger<ApiService> logger,
            IOptions<ApiOptions> apiOptions)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiOptions = apiOptions?.Value ?? throw new ArgumentNullException(nameof(apiOptions));

            // Configure HTTP client
            _httpClient.Timeout = TimeSpan.FromSeconds(_apiOptions.TimeoutSeconds);

            // Configure retry policy with exponential backoff
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("API retry attempt {RetryCount} after {Delay}ms",
                            retryCount, timespan.TotalMilliseconds);
                    });
        }

        /// <summary>
        /// Retrieves hyperlink data from the API using lookup IDs
        /// </summary>
        public async Task<ApiResponse> GetHyperlinkDataAsync(
            IEnumerable<string> lookupIds,
            CancellationToken cancellationToken = default)
        {
            var lookupIdList = lookupIds.ToList();
            _logger.LogInformation("Fetching hyperlink data for {Count} lookup IDs", lookupIdList.Count);

            try
            {
                var requestBody = new
                {
                    lookupIds = lookupIdList,
                    timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteAsync(async (ct) =>
                    await _httpClient.PostAsync(_apiOptions.PowerAutomateFlowUrl, content, ct),
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var apiData = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent)
                        ?? new Dictionary<string, object>();

                    _logger.LogInformation("Successfully retrieved data for {Count} items", apiData.Count);

                    return new ApiResponse
                    {
                        Success = true,
                        Data = apiData,
                        ResponseTime = TimeSpan.Zero
                    };
                }

                _logger.LogWarning("API request failed with status {StatusCode}", response.StatusCode);
                return new ApiResponse
                {
                    Success = false,
                    ErrorMessage = $"API returned status code: {response.StatusCode}",
                    ResponseTime = TimeSpan.Zero
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed");
                return new ApiResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    ResponseTime = TimeSpan.Zero
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout");
                return new ApiResponse
                {
                    Success = false,
                    ErrorMessage = "Request timed out",
                    Exception = ex,
                    ResponseTime = TimeSpan.Zero
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during API call");
                return new ApiResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    ResponseTime = TimeSpan.Zero
                };
            }
        }

        /// <summary>
        /// Retrieves hyperlink data in batches for better performance
        /// </summary>
        public async Task<BatchApiResponse> GetHyperlinkDataBatchAsync(
            IEnumerable<string> lookupIds,
            int batchSize = 50,
            IProgress<ApiProgressReport>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var lookupIdList = lookupIds.ToList();
            var totalBatches = (int)Math.Ceiling(lookupIdList.Count / (double)batchSize);
            var results = new List<ApiResponse>();
            var allData = new Dictionary<string, object>();

            _logger.LogInformation("Processing {Total} IDs in {Batches} batches of {Size}",
                lookupIdList.Count, totalBatches, batchSize);

            for (int i = 0; i < totalBatches; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = lookupIdList.Skip(i * batchSize).Take(batchSize);
                var batchResponse = await GetHyperlinkDataAsync(batch, cancellationToken);

                results.Add(batchResponse);

                if (batchResponse.Success)
                {
                    foreach (var kvp in batchResponse.Data)
                    {
                        allData[kvp.Key] = kvp.Value;
                    }
                }

                // Report progress
                progress?.Report(new ApiProgressReport
                {
                    CompletedBatches = i + 1,
                    TotalBatches = totalBatches,
                    ProcessedItems = Math.Min((i + 1) * batchSize, lookupIdList.Count),
                    TotalItems = lookupIdList.Count
                });
            }

            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count(r => !r.Success);

            return new BatchApiResponse
            {
                Success = failureCount == 0,
                TotalBatches = totalBatches,
                SuccessfulBatches = successCount,
                Responses = results
            };
        }

        /// <summary>
        /// Validates API connectivity and authentication
        /// </summary>
        public async Task<ApiValidationResult> ValidateApiConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Validating API connection");

                var testRequest = new { test = true, timestamp = DateTime.UtcNow };
                var json = JsonSerializer.Serialize(testRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiOptions.PowerAutomateFlowUrl, content, cancellationToken);

                return new ApiValidationResult
                {
                    IsValid = response.IsSuccessStatusCode,
                    Success = response.IsSuccessStatusCode,
                    ErrorMessage = response.IsSuccessStatusCode
                        ? string.Empty
                        : $"API validation failed with status {response.StatusCode}",
                    ResponseTime = TimeSpan.Zero
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API validation failed");
                return new ApiValidationResult
                {
                    IsValid = false,
                    Success = false,
                    ErrorMessage = $"API validation failed: {ex.Message}",
                    Exception = ex,
                    ResponseTime = TimeSpan.Zero
                };
            }
        }

        /// <summary>
        /// Gets API health status and performance metrics
        /// </summary>
        public async Task<ApiHealthStatus> GetApiHealthAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var validation = await ValidateApiConnectionAsync(cancellationToken);
                var responseTime = DateTime.UtcNow - startTime;

                return new ApiHealthStatus
                {
                    IsHealthy = validation.IsValid,
                    ResponseTime = responseTime,
                    Status = validation.IsValid ? "Healthy" : "Unhealthy"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return new ApiHealthStatus
                {
                    IsHealthy = false,
                    ResponseTime = TimeSpan.Zero,
                    Status = $"Health check failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Sends lookup IDs to PowerAutomate flow for processing
        /// </summary>
        public async Task<string> SendToPowerAutomateFlowAsync(
            List<string> lookupIds,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Sending {Count} lookup IDs to PowerAutomate", lookupIds.Count);

                var requestBody = new
                {
                    lookupIds = lookupIds,
                    requestTime = DateTime.UtcNow,
                    source = "BulkEditorModern"
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteAsync(async (ct) =>
                    await _httpClient.PostAsync(_apiOptions.PowerAutomateFlowUrl, content, ct),
                    cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("PowerAutomate flow executed successfully");
                }
                else
                {
                    _logger.LogWarning("PowerAutomate flow returned status {StatusCode}", response.StatusCode);
                }

                return responseContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send data to PowerAutomate");
                throw;
            }
        }
    }
}