using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Doc_Helper.Shared.Configuration;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doc_Helper.Infrastructure.Services
{
    /// <summary>
    /// Modern Word document processor with async operations and dependency injection
    /// Replaces legacy static methods with proper service architecture
    /// </summary>
    public partial class WordDocumentProcessor : IWordDocumentProcessor
    {
        private readonly ILogger<WordDocumentProcessor> _logger;
        private readonly IApiService _apiService;
        private readonly ICacheService _cacheService;
        private readonly AppOptions _appOptions;

        // Regex patterns for hyperlink processing
        [GeneratedRegex(@"(TSRC-[^-]+-[0-9]{6}|CMS-[^-]+-[0-9]{6})", RegexOptions.IgnoreCase)]
        private static partial Regex IdPatternRegex();

        [GeneratedRegex(@"docid=([^&]*)", RegexOptions.IgnoreCase)]
        private static partial Regex DocIdPatternRegex();

        [GeneratedRegex(@"\s*\((\d{5,6})\)\s*$")]
        private static partial Regex ContentIdPatternRegex();

        public WordDocumentProcessor(
            ILogger<WordDocumentProcessor> logger,
            IApiService apiService,
            ICacheService cacheService,
            IOptions<AppOptions> appOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _appOptions = appOptions?.Value ?? throw new ArgumentNullException(nameof(appOptions));
        }

        /// <summary>
        /// Extracts hyperlinks from a Word document asynchronously
        /// </summary>
        public async Task<List<HyperlinkData>> ExtractHyperlinksAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = $"hyperlinks_{Path.GetFileName(filePath)}_{File.GetLastWriteTime(filePath):yyyyMMddHHmmss}";

            // Try to get from cache first
            var cachedHyperlinks = await _cacheService.GetAsync<List<HyperlinkData>>(cacheKey, cancellationToken);
            if (cachedHyperlinks != null)
            {
                _logger.LogDebug("Retrieved {Count} hyperlinks from cache for {FileName}",
                    cachedHyperlinks.Count, Path.GetFileName(filePath));
                return cachedHyperlinks;
            }

            var hyperlinks = new List<HyperlinkData>();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("Extracting hyperlinks from {FilePath}", filePath);

                using var wordDoc = WordprocessingDocument.Open(filePath, false);
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body == null)
                {
                    _logger.LogWarning("Document body is null for {FilePath}", filePath);
                    return hyperlinks;
                }

                int pageNumber = 1;
                int lineNumber = 1;

                // Find all hyperlinks in the document
                var hyperlinkElements = body.Descendants<Hyperlink>().ToList();
                _logger.LogDebug("Found {HyperlinkCount} hyperlink elements in document", hyperlinkElements.Count);

                foreach (var hyperlinkElement in hyperlinkElements)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var hyperlink = ProcessHyperlinkElement(wordDoc, hyperlinkElement, pageNumber, lineNumber, cancellationToken);
                        if (hyperlink != null)
                        {
                            hyperlinks.Add(hyperlink);
                        }
                        lineNumber++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing hyperlink element at line {LineNumber}", lineNumber);
                        lineNumber++;
                    }
                }

                var processingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Extracted {HyperlinkCount} hyperlinks from {FileName} in {Duration:F2}s",
                    hyperlinks.Count, Path.GetFileName(filePath), processingTime.TotalSeconds);

                // Cache the results
                await _cacheService.SetAsync(cacheKey, hyperlinks, _appOptions.Cache.DefaultExpiryTime, cancellationToken);

                return hyperlinks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract hyperlinks from {FilePath}", filePath);
                throw new InvalidOperationException($"Error extracting hyperlinks from document: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Processes a single hyperlink element
        /// </summary>
        private HyperlinkData? ProcessHyperlinkElement(
            WordprocessingDocument wordDoc,
            Hyperlink hyperlinkElement,
            int pageNumber,
            int lineNumber,
            CancellationToken cancellationToken)
        {
            var hyperlink = new HyperlinkData
            {
                PageNumber = pageNumber,
                LineNumber = lineNumber,
                ElementId = hyperlinkElement.Id?.Value ?? string.Empty
            };

            // Get the hyperlink relationship
            if (!string.IsNullOrEmpty(hyperlinkElement.Id))
            {
                var hyperlinkRelationship = wordDoc.MainDocumentPart?
                    .HyperlinkRelationships
                    .FirstOrDefault(r => r.Id == hyperlinkElement.Id);

                if (hyperlinkRelationship != null)
                {
                    hyperlink.Address = hyperlinkRelationship.Uri.ToString();
                }
            }

            // Get the anchor (internal link)
            if (!string.IsNullOrEmpty(hyperlinkElement.Anchor))
            {
                hyperlink.SubAddress = hyperlinkElement.Anchor.Value ?? string.Empty;
            }

            // Get the display text
            var textElements = hyperlinkElement.Descendants<Text>();
            hyperlink.TextToDisplay = string.Join("", textElements.Select(t => t.Text ?? string.Empty));

            // Extract sub-address if present in the address
            if (!string.IsNullOrEmpty(hyperlink.Address) && hyperlink.Address.Contains('#'))
            {
                var parts = hyperlink.Address.Split('#');
                hyperlink.Address = parts[0];
                if (string.IsNullOrEmpty(hyperlink.SubAddress))
                {
                    hyperlink.SubAddress = parts.Length > 1 ? parts[1] : string.Empty;
                }
            }

            // Extract content ID and other metadata
            hyperlink.ContentID = ExtractLookupID(hyperlink.Address, hyperlink.SubAddress);
            hyperlink.Title = hyperlink.TextToDisplay;

            return hyperlink;
        }

        /// <summary>
        /// Updates hyperlinks in a Word document asynchronously
        /// </summary>
        public async Task<HyperlinkUpdateResult> UpdateHyperlinksAsync(
            string filePath,
            List<HyperlinkData> updatedHyperlinks,
            CancellationToken cancellationToken = default)
        {
            var result = new HyperlinkUpdateResult();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("Updating {HyperlinkCount} hyperlinks in {FilePath}",
                    updatedHyperlinks.Count, filePath);

                // Create backup if enabled
                if (_appOptions.Processing.CreateBackups)
                {
                    var backupResult = await CreateBackupAsync(filePath, null, cancellationToken);
                    if (!backupResult.Success)
                    {
                        _logger.LogWarning("Failed to create backup: {Error}", backupResult.ErrorMessage);
                    }
                }

                using var wordDoc = WordprocessingDocument.Open(filePath, true);
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body == null)
                {
                    throw new InvalidOperationException("Document body is null");
                }

                var hyperlinkElements = body.Descendants<Hyperlink>().ToList();
                int updatedCount = 0;

                foreach (var updatedHyperlink in updatedHyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var hyperlinkElement = hyperlinkElements.FirstOrDefault(h => h.Id == updatedHyperlink.ElementId);
                        if (hyperlinkElement != null)
                        {
                            await UpdateHyperlinkElementAsync(wordDoc, hyperlinkElement, updatedHyperlink, cancellationToken);
                            updatedCount++;
                            result.UpdatedHyperlinks.Add($"Updated hyperlink at Page:{updatedHyperlink.PageNumber} Line:{updatedHyperlink.LineNumber}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update hyperlink at Page:{Page} Line:{Line}",
                            updatedHyperlink.PageNumber, updatedHyperlink.LineNumber);
                    }
                }

                // Save the document
                wordDoc.MainDocumentPart?.Document.Save();

                result.Success = true;
                result.UpdatedCount = updatedCount;

                var processingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Updated {UpdatedCount}/{TotalCount} hyperlinks in {Duration:F2}s",
                    updatedCount, updatedHyperlinks.Count, processingTime.TotalSeconds);

                // Invalidate cache for this document
                var cacheKey = $"hyperlinks_{Path.GetFileName(filePath)}_*";
                await _cacheService.InvalidatePatternAsync(cacheKey, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update hyperlinks in {FilePath}", filePath);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                return result;
            }
        }

        /// <summary>
        /// Updates a single hyperlink element
        /// </summary>
        private async Task UpdateHyperlinkElementAsync(
            WordprocessingDocument wordDoc,
            Hyperlink hyperlinkElement,
            HyperlinkData updatedHyperlink,
            CancellationToken cancellationToken)
        {
            // Update the display text
            var textElements = hyperlinkElement.Descendants<Text>().ToList();
            if (textElements.Any())
            {
                // Clear existing text
                foreach (var text in textElements.Skip(1))
                {
                    text.Remove();
                }
                // Update first text element
                textElements.First().Text = updatedHyperlink.TextToDisplay;
            }

            // Update the hyperlink URL if needed
            if (!string.IsNullOrEmpty(hyperlinkElement.Id))
            {
                var hyperlinkRelationship = wordDoc.MainDocumentPart?
                    .HyperlinkRelationships
                    .FirstOrDefault(r => r.Id == hyperlinkElement.Id);

                if (hyperlinkRelationship != null)
                {
                    // Build the new URI
                    string newUri = updatedHyperlink.Address;
                    if (!string.IsNullOrEmpty(updatedHyperlink.SubAddress))
                    {
                        newUri += "#" + updatedHyperlink.SubAddress;
                    }

                    // Remove old relationship and add new one
                    var relationshipId = hyperlinkRelationship.Id;
                    wordDoc.MainDocumentPart?.DeleteReferenceRelationship(hyperlinkRelationship);
                    wordDoc.MainDocumentPart?.AddHyperlinkRelationship(new Uri(newUri), true, relationshipId);
                }
            }

            // Update anchor if it's an internal link
            if (!string.IsNullOrEmpty(updatedHyperlink.SubAddress) && string.IsNullOrEmpty(updatedHyperlink.Address))
            {
                hyperlinkElement.Anchor = updatedHyperlink.SubAddress;
            }

            await Task.CompletedTask; // For async consistency
        }

        /// <summary>
        /// Validates document accessibility and format
        /// </summary>
        public async Task<FileValidationResult> ValidateDocumentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(filePath);
            var result = new FileValidationResult
            {
                FilePath = filePath,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                LastModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue
            };

            if (!fileInfo.Exists)
            {
                result.IsValid = false;
                result.ErrorMessage = $"File not found: {filePath}";
                return result;
            }

            if (result.FileSize == 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "File is empty";
                return result;
            }

            if (result.FileSize > _appOptions.Processing.MaxFileSizeBytes)
            {
                result.IsValid = false;
                result.ErrorMessage = $"File size ({result.FileSize:N0} bytes) exceeds maximum allowed size";
                return result;
            }

            if (!_appOptions.Processing.AllowedExtensions.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.ErrorMessage = $"File extension '{fileInfo.Extension}' is not allowed";
                return result;
            }

            try
            {
                using var wordDoc = WordprocessingDocument.Open(filePath, false);
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Document body is null or corrupted";
                    return result;
                }

                var hyperlinkCount = body.Descendants<Hyperlink>().Count();
                _logger.LogDebug("Document validation passed for {FileName}: {HyperlinkCount} hyperlinks found", fileInfo.Name, hyperlinkCount);

                result.IsValid = true;
                await Task.CompletedTask;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document validation failed for {FilePath}", filePath);
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Creates a backup of the document before processing
        /// </summary>
        public async Task<BackupResult> CreateBackupAsync(
            string filePath,
            string? backupPath = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var defaultBackupPath = Path.Combine(
                    Path.GetDirectoryName(filePath) ?? string.Empty,
                    _appOptions.Processing.BackupFolderName,
                    $"{Path.GetFileNameWithoutExtension(filePath)}_backup_{timestamp}{fileInfo.Extension}");

                var targetPath = backupPath ?? defaultBackupPath;

                // Ensure backup directory exists
                var backupDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Copy file asynchronously
                await Task.Run(() => File.Copy(filePath, targetPath, overwrite: true), cancellationToken);

                var backupInfo = new FileInfo(targetPath);

                _logger.LogInformation("Document backup created: {BackupPath} ({Size:F2} MB)",
                    targetPath, backupInfo.Length / (1024.0 * 1024.0));

                return new BackupResult
                {
                    Success = true,
                    BackupPath = targetPath,
                    BackupSize = backupInfo.Length
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document backup failed for {FilePath}", filePath);
                return new BackupResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Extracts lookup ID from hyperlink address and sub-address
        /// </summary>
        public string ExtractLookupID(string address, string subAddress)
        {
            string fullAddress = address + (!string.IsNullOrEmpty(subAddress) ? "#" + subAddress : "");

            // Pattern to match TSRC-XXXX-XXXXXX or CMS-XXXX-XXXXXX
            var match = IdPatternRegex().Match(fullAddress);

            if (match.Success)
            {
                return match.Value.ToUpper();
            }

            // Alternative pattern for docid parameter
            match = DocIdPatternRegex().Match(fullAddress);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Removes invisible external hyperlinks from the collection
        /// </summary>
        public async Task<HyperlinkCleanupResult> RemoveInvisibleLinksAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default)
        {
            var result = new HyperlinkCleanupResult();
            var removedHyperlinks = new List<HyperlinkData>();

            try
            {
                // Remove hyperlinks with empty display text but non-empty address
                for (int i = hyperlinks.Count - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var hyperlink = hyperlinks[i];
                    if (string.IsNullOrWhiteSpace(hyperlink.TextToDisplay) && !string.IsNullOrWhiteSpace(hyperlink.Address))
                    {
                        removedHyperlinks.Add(hyperlink);
                        hyperlinks.RemoveAt(i);
                    }
                }

                result.Success = true;
                result.RemovedCount = removedHyperlinks.Count;
                result.RemovedHyperlinks = removedHyperlinks.Select(h =>
                    $"Page:{h.PageNumber} Line:{h.LineNumber} - Invisible hyperlink removed").ToList();

                _logger.LogInformation("Removed {Count} invisible hyperlinks", removedHyperlinks.Count);

                await Task.CompletedTask; // For async consistency
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing invisible hyperlinks");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Processes hyperlinks by extracting lookup IDs and cleaning data
        /// </summary>
        public async Task<List<string>> ProcessHyperlinksForApiAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Remove invisible external hyperlinks first
                var cleanupResult = await RemoveInvisibleLinksAsync(hyperlinks, cancellationToken);
                if (!cleanupResult.Success)
                {
                    _logger.LogWarning("Failed to clean invisible hyperlinks: {Error}", cleanupResult.ErrorMessage);
                }

                // Extract unique lookup IDs
                var lookupIds = hyperlinks
                    .Select(h => ExtractLookupID(h.Address, h.SubAddress))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Extracted {LookupIdCount} unique lookup IDs from {HyperlinkCount} hyperlinks",
                    lookupIds.Count, hyperlinks.Count);

                return lookupIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing hyperlinks for API");
                throw;
            }
        }

        /// <summary>
        /// Applies API results to update hyperlink data
        /// </summary>
        public async Task<List<HyperlinkData>> ApplyApiResultsAsync(
            List<HyperlinkData> hyperlinks,
            Dictionary<string, object> apiResults,
            CancellationToken cancellationToken = default)
        {
            var updatedHyperlinks = new List<HyperlinkData>();

            try
            {
                foreach (var hyperlink in hyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var lookupId = ExtractLookupID(hyperlink.Address, hyperlink.SubAddress);
                    if (!string.IsNullOrEmpty(lookupId) && apiResults.ContainsKey(lookupId))
                    {
                        var apiData = apiResults[lookupId];
                        var updatedHyperlink = await ApplyApiDataToHyperlinkAsync(hyperlink, apiData, cancellationToken);
                        updatedHyperlinks.Add(updatedHyperlink);
                    }
                    else
                    {
                        updatedHyperlinks.Add(hyperlink); // No changes
                    }
                }

                _logger.LogInformation("Applied API results to {UpdatedCount} hyperlinks", updatedHyperlinks.Count);
                return updatedHyperlinks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying API results to hyperlinks");
                throw;
            }
        }

        /// <summary>
        /// Applies API data to a single hyperlink
        /// </summary>
        private async Task<HyperlinkData> ApplyApiDataToHyperlinkAsync(
            HyperlinkData originalHyperlink,
            object apiData,
            CancellationToken cancellationToken)
        {
            var updatedHyperlink = originalHyperlink.Clone();

            try
            {
                // Parse API response based on the structure from Legacy version
                if (apiData is Dictionary<string, object> apiDict)
                {
                    // Handle status markers first (Expired, Not Found, etc.)
                    if (apiDict.TryGetValue("Status", out var statusObj))
                    {
                        var status = statusObj?.ToString() ?? string.Empty;
                        updatedHyperlink.Status = status;

                        // Apply status markers to display text based on Legacy logic
                        if (status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!updatedHyperlink.TextToDisplay.Contains(" - Expired"))
                            {
                                updatedHyperlink.TextToDisplay += " - Expired";
                            }
                        }
                        else if (status.Equals("Not Found", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!updatedHyperlink.TextToDisplay.Contains(" - Not Found"))
                            {
                                updatedHyperlink.TextToDisplay += " - Not Found";
                            }
                        }
                        else if (status.Equals("Error", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!updatedHyperlink.TextToDisplay.Contains(" - Broken"))
                            {
                                updatedHyperlink.TextToDisplay += " - Broken";
                            }
                        }
                    }

                    // Update title from API
                    if (apiDict.TryGetValue("Title", out var titleObj))
                    {
                        var newTitle = titleObj?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(newTitle) && newTitle != updatedHyperlink.Title)
                        {
                            // Remove any existing status markers before updating title
                            var cleanTitle = RemoveStatusMarkers(updatedHyperlink.TextToDisplay);
                            var contentIdSuffix = ExtractContentIdSuffix(cleanTitle);
                            var statusSuffix = ExtractStatusSuffix(updatedHyperlink.TextToDisplay);

                            updatedHyperlink.Title = newTitle;
                            updatedHyperlink.TextToDisplay = newTitle + contentIdSuffix + statusSuffix;
                        }
                    }

                    // Update Content ID from API
                    if (apiDict.TryGetValue("Content_ID", out var contentIdObj))
                    {
                        var contentId = contentIdObj?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(contentId))
                        {
                            updatedHyperlink.ContentID = contentId;
                        }
                    }

                    // Update Document ID from API
                    if (apiDict.TryGetValue("Document_ID", out var docIdObj))
                    {
                        var docId = docIdObj?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(docId))
                        {
                            updatedHyperlink.DocumentID = docId;
                        }
                    }
                }

                _logger.LogDebug("Applied API data to hyperlink at Page:{Page} Line:{Line}",
                    updatedHyperlink.PageNumber, updatedHyperlink.LineNumber);

                await Task.CompletedTask; // For async consistency
                return updatedHyperlink;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply API data to hyperlink at Page:{Page} Line:{Line}",
                    originalHyperlink.PageNumber, originalHyperlink.LineNumber);
                return originalHyperlink; // Return original if update fails
            }
        }

        /// <summary>
        /// Extracts content ID suffix from display text
        /// </summary>
        private string ExtractContentIdSuffix(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
                return string.Empty;

            var match = ContentIdPatternRegex().Match(displayText);
            return match.Success ? displayText[match.Index..] : string.Empty;
        }

        /// <summary>
        /// Removes status markers from display text (Expired, Not Found, Broken)
        /// </summary>
        private string RemoveStatusMarkers(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
                return string.Empty;

            return displayText
                .Replace(" - Expired", "")
                .Replace(" - Not Found", "")
                .Replace(" - Broken", "")
                .Trim();
        }

        /// <summary>
        /// Extracts status suffix from display text (Expired, Not Found, Broken)
        /// </summary>
        private string ExtractStatusSuffix(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
                return string.Empty;

            if (displayText.Contains(" - Expired"))
                return " - Expired";
            if (displayText.Contains(" - Not Found"))
                return " - Not Found";
            if (displayText.Contains(" - Broken"))
                return " - Broken";

            return string.Empty;
        }

        /// <summary>
        /// Fixes titles by removing status markers and cleaning up display text
        /// Based on Legacy ProcessingService.FixTitles method
        /// </summary>
        public async Task<TitleFixResult> FixTitlesAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default)
        {
            var result = new TitleFixResult();
            var fixedCount = 0;

            try
            {
                _logger.LogInformation("Fixing titles for {Count} hyperlinks", hyperlinks.Count);

                foreach (var hyperlink in hyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var originalText = hyperlink.TextToDisplay;
                    if (string.IsNullOrEmpty(originalText))
                        continue;

                    // Remove status markers from display text
                    var hasExpired = originalText.Contains(" - Expired");
                    var hasNotFound = originalText.Contains(" - Not Found");
                    var hasBroken = originalText.Contains(" - Broken");

                    if (hasExpired || hasNotFound || hasBroken)
                    {
                        var cleanedText = RemoveStatusMarkers(originalText);
                        hyperlink.TextToDisplay = cleanedText;
                        hyperlink.Title = RemoveContentIdFromTitle(cleanedText);

                        result.FixedTitles.Add($"Page:{hyperlink.PageNumber} Line:{hyperlink.LineNumber} - Removed status marker from title");
                        fixedCount++;
                    }
                }

                result.Success = true;
                result.FixedCount = fixedCount;

                _logger.LogInformation("Fixed titles for {Count} hyperlinks", fixedCount);

                await Task.CompletedTask; // For async consistency
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing titles");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Removes content ID pattern from title text
        /// </summary>
        private string RemoveContentIdFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return title;

            return ContentIdPatternRegex().Replace(title, "").Trim();
        }

        /// <summary>
        /// Processes hyperlink replacement based on configured rules
        /// Based on Legacy hyperlink replacement functionality
        /// </summary>
        public async Task<HyperlinkReplacementResult> ReplaceHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            List<HyperlinkReplacementRule> replacementRules,
            CancellationToken cancellationToken = default)
        {
            var result = new HyperlinkReplacementResult();
            var replacedCount = 0;

            try
            {
                if (!replacementRules?.Any() ?? true)
                {
                    result.Success = true;
                    return result;
                }

                _logger.LogInformation("Applying {RuleCount} replacement rules to {HyperlinkCount} hyperlinks",
                    replacementRules?.Count ?? 0, hyperlinks.Count);

                foreach (var hyperlink in hyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var rule in (replacementRules ?? Enumerable.Empty<HyperlinkReplacementRule>()).Where(r => r.IsEnabled))
                    {
                        if (ApplyReplacementRule(hyperlink, rule))
                        {
                            result.ReplacedHyperlinks.Add($"Page:{hyperlink.PageNumber} Line:{hyperlink.LineNumber} - Applied rule: {rule.Name}");
                            replacedCount++;
                        }
                    }
                }

                result.Success = true;
                result.ReplacedCount = replacedCount;

                _logger.LogInformation("Applied replacement rules to {Count} hyperlinks", replacedCount);

                await Task.CompletedTask; // For async consistency
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing hyperlinks");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Applies a single replacement rule to a hyperlink
        /// </summary>
        private bool ApplyReplacementRule(HyperlinkData hyperlink, HyperlinkReplacementRule rule)
        {
            try
            {
                var isMatch = false;

                // Check if the rule matches based on the rule type
                switch (rule.MatchType)
                {
                    case "Contains":
                        isMatch = hyperlink.Address?.Contains(rule.FindText, StringComparison.OrdinalIgnoreCase) ?? false;
                        break;
                    case "StartsWith":
                        isMatch = hyperlink.Address?.StartsWith(rule.FindText, StringComparison.OrdinalIgnoreCase) ?? false;
                        break;
                    case "EndsWith":
                        isMatch = hyperlink.Address?.EndsWith(rule.FindText, StringComparison.OrdinalIgnoreCase) ?? false;
                        break;
                    case "Exact":
                        isMatch = string.Equals(hyperlink.Address, rule.FindText, StringComparison.OrdinalIgnoreCase);
                        break;
                    case "Regex":
                        if (!string.IsNullOrEmpty(hyperlink.Address))
                        {
                            var regex = new Regex(rule.FindText, RegexOptions.IgnoreCase);
                            isMatch = regex.IsMatch(hyperlink.Address);
                        }
                        break;
                }

                if (isMatch)
                {
                    // Apply the replacement
                    if (!string.IsNullOrEmpty(rule.ReplaceText))
                    {
                        hyperlink.Address = rule.MatchType == "Regex"
                            ? Regex.Replace(hyperlink.Address ?? "", rule.FindText, rule.ReplaceText, RegexOptions.IgnoreCase)
                            : hyperlink.Address?.Replace(rule.FindText, rule.ReplaceText, StringComparison.OrdinalIgnoreCase) ?? "";
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply replacement rule {RuleName} to hyperlink", rule.Name);
                return false;
            }
        }

        /// <summary>
        /// Validates hyperlink data integrity
        /// </summary>
        public async Task<HyperlinkValidationResult> ValidateHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default)
        {
            var result = new HyperlinkValidationResult { TotalHyperlinks = hyperlinks.Count };

            try
            {
                foreach (var hyperlink in hyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var validation = ValidateSingleHyperlink(hyperlink);
                    if (validation.IsValid)
                    {
                        result.ValidHyperlinks++;
                    }
                    else
                    {
                        result.InvalidHyperlinks++;
                        result.ValidationErrors.AddRange(validation.Errors);
                    }
                }

                result.Success = true;
                _logger.LogInformation("Validated {ValidCount}/{TotalCount} hyperlinks",
                    result.ValidHyperlinks, result.TotalHyperlinks);

                await Task.CompletedTask; // For async consistency
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hyperlink validation failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Validates a single hyperlink
        /// </summary>
        private SingleHyperlinkValidation ValidateSingleHyperlink(HyperlinkData hyperlink)
        {
            var validation = new SingleHyperlinkValidation { IsValid = true };

            if (string.IsNullOrEmpty(hyperlink.Address) && string.IsNullOrEmpty(hyperlink.SubAddress))
            {
                validation.IsValid = false;
                validation.Errors.Add("Hyperlink has no address or sub-address");
            }

            if (string.IsNullOrEmpty(hyperlink.TextToDisplay))
            {
                validation.Warnings.Add("Hyperlink has no display text");
            }

            var lookupId = ExtractLookupID(hyperlink.Address, hyperlink.SubAddress);
            if (!string.IsNullOrEmpty(lookupId))
            {
                if (!IsValidLookupIdFormat(lookupId))
                {
                    validation.IsValid = false;
                    validation.Errors.Add($"Invalid lookup ID format: {lookupId}");
                }
            }

            // Final check on validity
            if (validation.Errors.Any())
            {
                validation.IsValid = false;
            }

            return validation;
        }

        /// <summary>
        /// Validates lookup ID format
        /// </summary>
        private bool IsValidLookupIdFormat(string lookupId)
        {
            if (string.IsNullOrEmpty(lookupId))
                return false;

            return IdPatternRegex().IsMatch(lookupId) || DocIdPatternRegex().IsMatch($"docid={lookupId}");
        }

        /// <summary>
        /// Gets document processing statistics
        /// </summary>
        public async Task<DocumentStatistics> GetDocumentStatisticsAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var hyperlinks = await ExtractHyperlinksAsync(filePath, cancellationToken);
                var fileInfo = new FileInfo(filePath);

                var stats = new DocumentStatistics
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    TotalHyperlinks = hyperlinks.Count,
                    ExternalHyperlinks = hyperlinks.Count(h => !string.IsNullOrEmpty(h.Address)),
                    InternalHyperlinks = hyperlinks.Count(h => string.IsNullOrEmpty(h.Address) && !string.IsNullOrEmpty(h.SubAddress)),
                    ValidLookupIds = hyperlinks.Select(h => ExtractLookupID(h.Address, h.SubAddress))
                        .Where(id => !string.IsNullOrEmpty(id)).Count(),
                    StatisticsGeneratedAt = DateTime.UtcNow
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get document statistics for {FilePath}", filePath);
                throw;
            }
        }
    }
}
