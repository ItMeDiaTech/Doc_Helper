
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Shared.Configuration;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Doc_Helper.Infrastructure.Services
{
    /// <summary>
    /// Modern validation service with dependency injection and resilience patterns
    /// Handles hyperlink validation, API connectivity testing, and data integrity checks
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly ILogger<ValidationService> _logger;
        private readonly IApiService _apiService;
        private readonly ICacheService _cacheService;
        private readonly IWordDocumentProcessor _documentProcessor;
        private readonly AppOptions _appOptions;
        private readonly HttpClient _httpClient;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public ValidationService(
            ILogger<ValidationService> logger,
            IApiService apiService,
            ICacheService cacheService,
            IWordDocumentProcessor documentProcessor,
            IOptions<AppOptions> appOptions,
            HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _documentProcessor = documentProcessor ?? throw new ArgumentNullException(nameof(documentProcessor));
            _appOptions = appOptions?.Value ?? throw new ArgumentNullException(nameof(appOptions));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Configure Polly retry policy
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry attempt {RetryCount} for {Operation} in {Delay}s",
                            retryCount, context.OperationKey, timespan.TotalSeconds);
                    });
        }

        /// <summary>
        /// Validates hyperlinks for data integrity and format compliance
        /// </summary>
        public async Task<HyperlinkValidationResult> ValidateHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            ValidationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var validationOptions = options ?? new ValidationOptions();
            var result = new HyperlinkValidationResult
            {
                TotalHyperlinks = hyperlinks.Count,
                ValidationStartTime = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Starting validation of {HyperlinkCount} hyperlinks", hyperlinks.Count);

                var validationTasks = new List<Task<SingleHyperlinkValidation>>();

                // Process hyperlinks in batches for better performance
                var batchSize = validationOptions.BatchSize;
                var batches = hyperlinks
                    .Select((h, i) => new { Hyperlink = h, Index = i })
                    .GroupBy(x => x.Index / batchSize)
                    .Select(g => g.Select(x => x.Hyperlink).ToList());

                foreach (var batch in batches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batchTasks = batch.Select(h => ValidateSingleHyperlinkAsync(h, validationOptions, cancellationToken));
                    validationTasks.AddRange(batchTasks);
                }

                // Wait for all validations to complete
                var validationResults = await Task.WhenAll(validationTasks);

                // Aggregate results
                foreach (var validation in validationResults)
                {
                    if (validation.IsValid)
                    {
                        result.ValidHyperlinks++;
                    }
                    else
                    {
                        result.InvalidHyperlinks++;
                        result.ValidationErrors.AddRange(validation.Errors);
                    }

                    result.ValidationWarnings.AddRange(validation.Warnings);
                }

                result.Success = true;
                result.ValidationEndTime = DateTime.UtcNow;

                _logger.LogInformation("Hyperlink validation completed: {Valid}/{Total} valid ({Rate:F1}%)",
                    result.ValidHyperlinks, result.TotalHyperlinks, result.ValidationRate);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hyperlink validation failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.ValidationEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Validates a single hyperlink with comprehensive checks
        /// </summary>
        private async Task<SingleHyperlinkValidation> ValidateSingleHyperlinkAsync(
            HyperlinkData hyperlink,
            ValidationOptions options,
            CancellationToken cancellationToken)
        {
            var validation = new SingleHyperlinkValidation { HyperlinkId = $"Page:{hyperlink.PageNumber} Line:{hyperlink.LineNumber}" };

            try
            {
                // Basic structure validation
                ValidateHyperlinkStructure(hyperlink, validation);

                // Content validation
                ValidateHyperlinkContent(hyperlink, validation);

                // URL format validation
                if (options.ValidateUrlFormat && !string.IsNullOrEmpty(hyperlink.Address))
                {
                    await ValidateUrlFormatAsync(hyperlink.Address, validation, cancellationToken);
                }

                // Lookup ID validation
                if (options.ValidateLookupIds)
                {
                    ValidateLookupId(hyperlink, validation);
                }

                validation.IsValid = validation.Errors.Count == 0;
                return validation;
            }
            catch (Exception ex)
            {
                validation.IsValid = false;
                validation.Errors.Add($"Validation exception: {ex.Message}");
                return validation;
            }
        }

        /// <summary>
        /// Validates basic hyperlink structure
        /// </summary>
        private void ValidateHyperlinkStructure(HyperlinkData hyperlink, SingleHyperlinkValidation validation)
        {
            // Check for required fields
            if (string.IsNullOrEmpty(hyperlink.Address) && string.IsNullOrEmpty(hyperlink.SubAddress))
            {
                validation.Errors.Add("Hyperlink has no address or sub-address");
            }

            if (string.IsNullOrEmpty(hyperlink.TextToDisplay))
            {
                validation.Warnings.Add("Hyperlink has no display text");
            }

            // Check for suspicious patterns
            if (!string.IsNullOrEmpty(hyperlink.TextToDisplay))
            {
                if (hyperlink.TextToDisplay.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    validation.Warnings.Add("Display text contains 'ERROR'");
                }

                if (hyperlink.TextToDisplay.Length > 200)
                {
                    validation.Warnings.Add("Display text is unusually long");
                }
            }

            // Validate page and line numbers
            if (hyperlink.PageNumber <= 0)
            {
                validation.Warnings.Add("Invalid page number");
            }

            if (hyperlink.LineNumber <= 0)
            {
                validation.Warnings.Add("Invalid line number");
            }
        }

        /// <summary>
        /// Validates hyperlink content and format
        /// </summary>
        private void ValidateHyperlinkContent(HyperlinkData hyperlink, SingleHyperlinkValidation validation)
        {
            // Validate external links
            if (!string.IsNullOrEmpty(hyperlink.Address))
            {
                if (!Uri.TryCreate(hyperlink.Address, UriKind.Absolute, out var uri))
                {
                    validation.Errors.Add($"Invalid URL format: {hyperlink.Address}");
                }
                else
                {
                    // Check for common URL issues
                    if (uri.Scheme != "http" && uri.Scheme != "https")
                    {
                        validation.Warnings.Add($"Unusual URL scheme: {uri.Scheme}");
                    }

                    if (string.IsNullOrEmpty(uri.Host))
                    {
                        validation.Errors.Add("URL has no host");
                    }
                }
            }

            // Validate internal links
            if (!string.IsNullOrEmpty(hyperlink.SubAddress))
            {
                if (hyperlink.SubAddress.Contains("  ")) // Double spaces
                {
                    validation.Warnings.Add("Sub-address contains double spaces");
                }

                if (hyperlink.SubAddress.Contains("&") && !hyperlink.SubAddress.Contains("&amp;"))
                {
                    validation.Warnings.Add("Sub-address may need HTML encoding");
                }
            }
        }

        /// <summary>
        /// Validates URL format and accessibility
        /// </summary>
        private async Task ValidateUrlFormatAsync(
            string url,
            SingleHyperlinkValidation validation,
            CancellationToken cancellationToken)
        {
            try
            {
                // Check if URL is reachable (optional, can be expensive)
                var cacheKey = $"url_validation_{url.GetHashCode()}";
                var cachedResult = await _cacheService.GetAsync<bool?>(cacheKey, cancellationToken);

                if (cachedResult.HasValue)
                {
                    if (!cachedResult.Value)
                    {
                        validation.Warnings.Add("URL may not be accessible (cached result)");
                    }
                    return;
                }

                // Perform HEAD request with retry policy
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                });

                var isAccessible = response.IsSuccessStatusCode;

                // Cache the result for 1 hour to avoid repeated checks
                await _cacheService.SetAsync(cacheKey, isAccessible, TimeSpan.FromHours(1), cancellationToken);

                if (!isAccessible)
                {
                    validation.Warnings.Add($"URL returned status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                validation.Warnings.Add($"Could not validate URL accessibility: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates lookup ID format and content
        /// </summary>
        private void ValidateLookupId(HyperlinkData hyperlink, SingleHyperlinkValidation validation)
        {
            var lookupId = _documentProcessor.ExtractLookupID(hyperlink.Address, hyperlink.SubAddress);

            if (string.IsNullOrEmpty(lookupId))
            {
                validation.Warnings.Add("No lookup ID found in hyperlink");
                return;
            }

            // Validate lookup ID format
            if (!IsValidLookupIdFormat(lookupId))
            {
                validation.Errors.Add($"Invalid lookup ID format: {lookupId}");
            }

            // Check content ID consistency
            if (!string.IsNullOrEmpty(hyperlink.ContentID) && hyperlink.ContentID != lookupId)
            {
                validation.Warnings.Add($"Content ID mismatch: hyperlink={lookupId}, stored={hyperlink.ContentID}");
            }
        }

        /// <summary>
        /// Validates lookup ID format using regex patterns
        /// </summary>
        private bool IsValidLookupIdFormat(string lookupId)
        {
            if (string.IsNullOrEmpty(lookupId))
                return false;

            // TSRC-XXXX-XXXXXX or CMS-XXXX-XXXXXX pattern
            var tsrcCmsPattern = @"^(TSRC|CMS)-[A-Z0-9]+-[0-9]{6}$";
            if (Regex.IsMatch(lookupId, tsrcCmsPattern, RegexOptions.IgnoreCase))
                return true;

            // Alternative numeric ID pattern
            var numericPattern = @"^[0-9]{5,10}$";
            if (Regex.IsMatch(lookupId, numericPattern))
                return true;

            return false;
        }

        /// <summary>
        /// Validates API connectivity and response format
        /// </summary>
        public async Task<ApiValidationResult> ValidateApiConnectivityAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new ApiValidationResult { ValidationStartTime = DateTime.UtcNow };

            try
            {
                _logger.LogInformation("Validating API connectivity to {ApiUrl}", _appOptions.Api.PowerAutomateFlowUrl);

                var healthStatus = await _apiService.GetApiHealthAsync(cancellationToken);
                result.IsHealthy = healthStatus.IsHealthy;
                result.ResponseTime = healthStatus.ResponseTime;
                // result.ApiVersion = healthStatus.Metrics.GetValueOrDefault("Version", "Unknown")?.ToString() ?? "Unknown";

                if (!result.IsHealthy)
                {
                    result.ValidationErrors.Add($"API is not healthy: {healthStatus.Status}");
                }

                var authResult = await _apiService.ValidateApiConnectionAsync(cancellationToken);
                result.IsAuthenticated = authResult.IsValid;

                if (!authResult.IsValid)
                {
                    result.ValidationErrors.Add($"API authentication failed: {authResult.ErrorMessage}");
                }

                if (result.IsHealthy && result.IsAuthenticated)
                {
                    await ValidateApiResponseFormatAsync(result, cancellationToken);
                }

                result.Success = result.ValidationErrors.Count == 0;
                result.ValidationEndTime = DateTime.UtcNow;

                _logger.LogInformation("API validation completed. Success: {Success}, Healthy: {Healthy}, Authenticated: {Authenticated}",
                    result.Success, result.IsHealthy, result.IsAuthenticated);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API validation failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.ValidationEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Validates API response format with sample data
        /// </summary>
        private async Task ValidateApiResponseFormatAsync(ApiValidationResult result, CancellationToken cancellationToken)
        {
            try
            {
                // Test with sample lookup IDs
                var sampleLookupIds = new[] { "TSRC-TEST-123456", "CMS-SAMPLE-789012" };

                var apiResponse = await _apiService.GetHyperlinkDataAsync(sampleLookupIds, cancellationToken);

                if (apiResponse.Success)
                {
                    result.SampleResponseReceived = true;

                    // Validate response structure
                    if (apiResponse.Data != null && apiResponse.Data.Count > 0)
                    {
                        result.ResponseFormatValid = true;
                    }
                    else
                    {
                        result.ValidationWarnings.Add("API response has no data (expected for test IDs)");
                        result.ResponseFormatValid = true; // Still valid format
                    }
                }
                else
                {
                    result.ValidationWarnings.Add($"Sample API call failed: {apiResponse.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                result.ValidationWarnings.Add($"Could not validate API response format: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates document files before processing
        /// </summary>
        public async Task<DocumentValidationSummary> ValidateDocumentsAsync(
            IEnumerable<string> filePaths,
            ValidationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var validationOptions = options ?? new ValidationOptions();
            var filePathList = filePaths.ToList();
            var summary = new DocumentValidationSummary
            {
                TotalFiles = filePathList.Count,
                ValidationStartTime = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Validating {FileCount} documents", filePathList.Count);

                var validationTasks = filePathList.Select(filePath => _documentProcessor.ValidateDocumentAsync(filePath, cancellationToken));

                var validationResults = await Task.WhenAll(validationTasks);

                // Aggregate results
                summary.ValidFiles = validationResults.Count(r => r.IsValid);
                summary.InvalidFiles = validationResults.Count(r => !r.IsValid);
                summary.FileValidations = validationResults.ToList();

                // Collect all error messages
                summary.ValidationErrors = validationResults
                    .Where(r => !r.IsValid)
                    .Select(r => $"{Path.GetFileName(r.FilePath)}: {r.ErrorMessage}")
                    .ToList();

                summary.Success = summary.InvalidFiles == 0;
                summary.ValidationEndTime = DateTime.UtcNow;

                _logger.LogInformation("Document validation completed: {Valid}/{Total} valid files",
                    summary.ValidFiles, summary.TotalFiles);

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document validation failed");
                summary.Success = false;
                summary.ErrorMessage = ex.Message;
                summary.Exception = ex;
                summary.ValidationEndTime = DateTime.UtcNow;
                return summary;
            }
        }

        /// <summary>
        /// Validates system configuration and dependencies
        /// </summary>
        public async Task<SystemValidationResult> ValidateSystemConfigurationAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new SystemValidationResult { ValidationStartTime = DateTime.UtcNow };

            try
            {
                _logger.LogInformation("Validating system configuration");

                // Validate application configuration
                ValidateAppConfiguration(result);

                // Validate database connectivity
                await ValidateDatabaseConnectivityAsync(result, cancellationToken);

                // Validate API configuration
                await ValidateApiConfigurationAsync(result, cancellationToken);

                // Validate file system permissions
                ValidateFileSystemPermissions(result);

                // Validate dependencies
                ValidateDependencies(result);

                result.Success = result.ValidationErrors.Count == 0;
                result.ValidationEndTime = DateTime.UtcNow;

                _logger.LogInformation("System validation completed. Success: {Success}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                    result.Success, result.ValidationErrors.Count, result.ValidationWarnings.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System validation failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.ValidationEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Validates application configuration settings
        /// </summary>
        private void ValidateAppConfiguration(SystemValidationResult result)
        {
            try
            {
                // Validate API settings
                if (string.IsNullOrEmpty(_appOptions.Api.PowerAutomateFlowUrl))
                {
                    result.ValidationErrors.Add("PowerAutomate Flow URL is not configured");
                }
                else if (!Uri.TryCreate(_appOptions.Api.PowerAutomateFlowUrl, UriKind.Absolute, out _))
                {
                    result.ValidationErrors.Add("PowerAutomate Flow URL is not a valid URL");
                }

                if (string.IsNullOrEmpty(_appOptions.Api.HyperlinkBaseUrl))
                {
                    result.ValidationErrors.Add("Hyperlink Base URL is not configured");
                }

                if (_appOptions.Api.TimeoutSeconds < 5 || _appOptions.Api.TimeoutSeconds > 300)
                {
                    result.ValidationWarnings.Add($"API timeout ({_appOptions.Api.TimeoutSeconds}s) is outside recommended range (5-300s)");
                }

                // Validate processing settings
                if (_appOptions.Processing.MaxConcurrentFiles < 1 || _appOptions.Processing.MaxConcurrentFiles > 20)
                {
                    result.ValidationWarnings.Add($"Max concurrent files ({_appOptions.Processing.MaxConcurrentFiles}) is outside recommended range (1-20)");
                }

                // Validate data settings
                if (string.IsNullOrEmpty(_appOptions.Data.DatabasePath))
                {
                    result.ValidationErrors.Add("Database path is not configured");
                }

                result.ConfigurationValid = result.ValidationErrors.Count == 0;
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Configuration validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates database connectivity
        /// </summary>
        private async Task ValidateDatabaseConnectivityAsync(SystemValidationResult result, CancellationToken cancellationToken)
        {
            try
            {
                // This would use a database service to test connectivity
                result.DatabaseConnectivity = true; // Placeholder
                result.ValidationWarnings.Add("Database connectivity validation not fully implemented");
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Database connectivity validation failed: {ex.Message}");
                result.DatabaseConnectivity = false;
            }
        }

        /// <summary>
        /// Validates API configuration and connectivity
        /// </summary>
        private async Task ValidateApiConfigurationAsync(SystemValidationResult result, CancellationToken cancellationToken)
        {
            try
            {
                var apiValidation = await ValidateApiConnectivityAsync(cancellationToken);
                result.ApiConnectivity = apiValidation.Success;

                if (!apiValidation.Success)
                {
                    result.ValidationErrors.AddRange(apiValidation.ValidationErrors);
                }

                result.ValidationWarnings.AddRange(apiValidation.ValidationWarnings);
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"API configuration validation failed: {ex.Message}");
                result.ApiConnectivity = false;
            }
        }

        /// <summary>
        /// Validates file system permissions
        /// </summary>
        private void ValidateFileSystemPermissions(SystemValidationResult result)
        {
            try
            {
                // Test write permissions in common directories
                var testDirectories = new[]
                {
                    _appOptions.Data.BaseStoragePath,
                    _appOptions.Processing.TempFolderPath,
                    Path.GetDirectoryName(_appOptions.Data.DatabasePath) ?? "Data"
                };

                foreach (var directory in testDirectories)
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                        var testFile = Path.Combine(directory, $"test_{Guid.NewGuid()}.tmp");
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                    catch (Exception ex)
                    {
                        result.ValidationErrors.Add($"No write permission for directory '{directory}': {ex.Message}");
                    }
                }

                result.FileSystemPermissions = result.ValidationErrors.Count == 0;
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"File system permission validation failed: {ex.Message}");
                result.FileSystemPermissions = false;
            }
        }

        /// <summary>
        /// Validates required dependencies
        /// </summary>
        private void ValidateDependencies(SystemValidationResult result)
        {
            try
            {
                // Check for required assemblies
                var requiredTypes = new[]
                {
                    typeof(WordprocessingDocument), // DocumentFormat.OpenXml
                    typeof(HttpClient), // System.Net.Http
                    typeof(Microsoft.EntityFrameworkCore.DbContext) // EntityFrameworkCore
                };

                foreach (var type in requiredTypes)
                {
                    if (type.Assembly == null)
                    {
                        result.ValidationErrors.Add($"Required dependency not found: {type.Name}");
                    }
                }

                result.DependenciesValid = result.ValidationErrors.Count == 0;
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add($"Dependency validation failed: {ex.Message}");
                result.DependenciesValid = false;
            }
        }

        /// <summary>
        /// Performs comprehensive system health check
        /// </summary>
        public async Task<SystemHealthResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var result = new SystemHealthResult { CheckStartTime = DateTime.UtcNow };

            try
            {
                _logger.LogInformation("Performing comprehensive system health check");

                // System validation
                var systemValidation = await ValidateSystemConfigurationAsync(cancellationToken);
                result.SystemValidation = systemValidation;

                // API validation
                var apiValidation = await ValidateApiConnectivityAsync(cancellationToken);
                result.ApiValidation = apiValidation;

                // Performance check
                await PerformPerformanceCheckAsync(result, cancellationToken);

                // Overall health assessment
                result.IsHealthy = systemValidation.Success &&
                                  apiValidation.Success &&
                                  result.PerformanceMetrics.ResponseTimeMs < 5000;

                result.HealthScore = CalculateHealthScore(result);
                result.CheckEndTime = DateTime.UtcNow;

                _logger.LogInformation("Health check completed. Health Score: {HealthScore}/100, Healthy: {IsHealthy}",
                    result.HealthScore, result.IsHealthy);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                result.IsHealthy = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.CheckEndTime = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Performs basic performance checks
        /// </summary>
        private async Task PerformPerformanceCheckAsync(SystemHealthResult result, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Test memory allocation
                var testData = new byte[1024 * 1024]; // 1MB
                Array.Fill(testData, (byte)0xFF);

                // Test cache performance
                await _cacheService.SetAsync("health_check_test", testData, TimeSpan.FromMinutes(1), cancellationToken);
                var retrievedData = await _cacheService.GetAsync<byte[]>("health_check_test", cancellationToken);
                await _cacheService.RemoveAsync("health_check_test", cancellationToken);

                var responseTime = DateTime.UtcNow - startTime;

                result.PerformanceMetrics = new PerformanceMetrics
                {
                    ResponseTimeMs = (int)responseTime.TotalMilliseconds,
                    MemoryUsageMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                    CacheOperationSuccessful = retrievedData != null
                };
            }
            catch (Exception ex)
            {
                result.PerformanceMetrics = new PerformanceMetrics
                {
                    ResponseTimeMs = 9999,
                    MemoryUsageMB = 0,
                    CacheOperationSuccessful = false
                };
                (result.SystemValidation.ValidationWarnings ??= new()).Add($"Performance check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates overall health score (0-100)
        /// </summary>
        private int CalculateHealthScore(SystemHealthResult result)
        {
            var score = 100;

            // Deduct points for errors
            score -= result.SystemValidation.ValidationErrors.Count * 20;
            score -= result.ApiValidation.ValidationErrors.Count * 15;

            // Deduct points for warnings
            score -= result.SystemValidation.ValidationWarnings.Count * 5;
            score -= result.ApiValidation.ValidationWarnings.Count * 3;

            // Deduct points for poor performance
            if (result.PerformanceMetrics.ResponseTimeMs > 2000)
                score -= 10;
            if (result.PerformanceMetrics.ResponseTimeMs > 5000)
                score -= 20;

            if (!result.PerformanceMetrics.CacheOperationSuccessful)
                score -= 15;

            return Math.Max(0, Math.Min(100, score));
        }
    }
}