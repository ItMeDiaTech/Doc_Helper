using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Interfaces;
using Doc_Helper.Core.Models;
using Microsoft.Extensions.Logging;

namespace Doc_Helper.Infrastructure.Services
{
    /// <summary>
    /// Service for processing hyperlinks with validation and cleanup operations
    /// </summary>
    public partial class HyperlinkProcessingService : IHyperlinkProcessingService
    {
        private readonly ILogger<HyperlinkProcessingService> _logger;
        private readonly IApiService _apiService;
        private readonly ICacheService _cacheService;

        // Regex patterns
        [GeneratedRegex(@"\s*\((\d{5,6})\)\s*$")]
        private static partial Regex ContentIdPatternRegex();

        [GeneratedRegex(@"[ ]{2,}")]
        private static partial Regex MultipleSpacesPatternRegex();

        [GeneratedRegex(@"(TSRC-[^-]+-[0-9]{6}|CMS-[^-]+-[0-9]{6})", RegexOptions.IgnoreCase)]
        private static partial Regex IdPatternRegex();

        [GeneratedRegex(@"[?&]docid=([^&\s]+)", RegexOptions.IgnoreCase)]
        private static partial Regex DocIdParameterRegex();

        // Status markers (matching Legacy behavior)
        private const string ExpiredMarker = " - Expired";
        private const string NotFoundMarker = " - Not Found";

        public HyperlinkProcessingService(
            ILogger<HyperlinkProcessingService> logger,
            IApiService apiService,
            ICacheService cacheService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        /// <summary>
        /// Processes hyperlinks for validation and cleanup
        /// </summary>
        public async Task<HyperlinkProcessingResult> ProcessHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default)
        {
            var result = new HyperlinkProcessingResult();

            try
            {
                _logger.LogInformation("Processing {Count} hyperlinks", hyperlinks.Count);

                // Remove invisible links
                var cleanupResult = await RemoveInvisibleLinksAsync(hyperlinks, cancellationToken);

                // Extract lookup IDs
                var lookupIds = ExtractLookupIds(hyperlinks);
                result.LookupIds = lookupIds;

                // Get API data if lookup IDs exist
                if (lookupIds.Any())
                {
                    var apiResponse = await _apiService.GetHyperlinkDataAsync(lookupIds, cancellationToken);
                    if (apiResponse.Success)
                    {
                        // Update titles from API data
                        var titleResult = await UpdateTitlesAsync(hyperlinks, apiResponse.Data, cancellationToken);
                        result.UpdatedCount += titleResult.UpdatedTitles;
                    }
                }

                // Fix double spaces
                var fixedCount = await FixDoubleSpacesAsync(hyperlinks, cancellationToken);

                result.ProcessedHyperlinks = hyperlinks;
                result.Success = true;

                _logger.LogInformation("Hyperlink processing completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing hyperlinks");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
            }

            return result;
        }

        /// <summary>
        /// Fixes internal hyperlinks by validating anchors
        /// </summary>
        public async Task<InternalLinkFixResult> FixInternalLinksAsync(
            List<HyperlinkData> hyperlinks,
            string? documentContent = null,
            CancellationToken cancellationToken = default)
        {
            var result = new InternalLinkFixResult();

            try
            {
                var internalLinks = hyperlinks.Where(h =>
                    !string.IsNullOrEmpty(h.SubAddress) &&
                    string.IsNullOrEmpty(h.Address)).ToList();

                _logger.LogInformation("Fixing {Count} internal hyperlinks", internalLinks.Count);

                foreach (var link in internalLinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Validate anchor exists in document
                    if (!string.IsNullOrEmpty(documentContent))
                    {
                        if (!documentContent.Contains(link.SubAddress))
                        {
                            result.BrokenLinkDetails.Add($"Anchor not found: {link.SubAddress}");
                            link.Status = "Broken";
                        }
                        else
                        {
                            link.Status = "Valid";
                            result.FixedLinks++;
                        }
                    }
                }

                result.Success = true;
                // Total links tracked in FixedLinks and BrokenLinks

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing internal links");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Updates hyperlink titles based on API data
        /// </summary>
        public async Task<TitleUpdateResult> UpdateTitlesAsync(
            List<HyperlinkData> hyperlinks,
            Dictionary<string, object> apiData,
            CancellationToken cancellationToken = default)
        {
            var result = new TitleUpdateResult();

            try
            {
                _logger.LogInformation("Updating titles for {Count} hyperlinks", hyperlinks.Count);

                foreach (var hyperlink in hyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var lookupId = ExtractLookupId(hyperlink);
                    if (!string.IsNullOrEmpty(lookupId))
                    {
                        if (apiData.ContainsKey(lookupId))
                        {
                            // Found in API/dictionary - process normally
                            if (apiData[lookupId] is Dictionary<string, object> itemData &&
                                itemData.TryGetValue("Title", out var titleObj))
                            {
                                var newTitle = titleObj?.ToString() ?? string.Empty;

                                if (!string.IsNullOrEmpty(newTitle) && newTitle != hyperlink.Title)
                                {
                                    result.TitleChanges.Add($"{lookupId}: '{hyperlink.Title}' -> '{newTitle}'");

                                    hyperlink.Title = newTitle;

                                    // Update display text if needed
                                    var contentIdSuffix = ExtractContentIdSuffix(hyperlink.TextToDisplay);
                                    hyperlink.TextToDisplay = newTitle + contentIdSuffix;

                                    // Apply status marker based on API data
                                    ApplyStatusMarkerIfNeeded(hyperlink, itemData);

                                    result.UpdatedTitles++;
                                }
                            }
                        }
                        else
                        {
                            // Not found in API/dictionary - mark as "Not Found"
                            MarkAsNotFound(hyperlink);
                            result.TitleChanges.Add($"{lookupId}: Not found in dictionary");
                        }
                    }
                }

                result.Success = true;
                _logger.LogInformation("Updated {Count} titles", result.UpdatedTitles);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating titles");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Appends content IDs to hyperlink display text (matching Legacy behavior)
        /// </summary>
        public async Task<ContentIdAppendResult> AppendContentIdsAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default)
        {
            var result = new ContentIdAppendResult();

            try
            {
                _logger.LogInformation("Appending content IDs to {Count} hyperlinks", hyperlinks.Count);

                foreach (var hyperlink in hyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Extract lookup ID from URL (matching Legacy behavior)
                    string lookupId = ExtractLookupId(hyperlink);
                    if (string.IsNullOrEmpty(lookupId)) continue;

                    // Get last 6 and last 5 digits from Content_ID (matching Legacy)
                    var (last6, last5) = ExtractContentIdSuffixes(lookupId);

                    // Check if we need to append the content ID (matching Legacy check)
                    if (!string.IsNullOrEmpty(hyperlink.TextToDisplay) && !hyperlink.TextToDisplay.Contains($"({last6})"))
                    {
                        UpdateHyperlinkDisplayText(hyperlink, last6, last5);
                        result.AppendedCount++;
                    }
                }

                result.Success = true;
                _logger.LogInformation("Appended content IDs to {Count} hyperlinks", result.AppendedCount);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending content IDs");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Extracts last 6 and last 5 digits from Content_ID (matching Legacy behavior)
        /// </summary>
        private (string last6, string last5) ExtractContentIdSuffixes(string lookupId)
        {
            string last6 = lookupId.Length >= 6 ? lookupId[^6..] : lookupId;
            string last5 = last6.Length >= 5 ? last6[^5..] : last6;
            return (last6, last5);
        }

        /// <summary>
        /// Updates hyperlink display text with content ID (matching Legacy logic exactly)
        /// </summary>
        private void UpdateHyperlinkDisplayText(HyperlinkData hyperlink, string last6, string last5)
        {
            if (string.IsNullOrEmpty(hyperlink.TextToDisplay))
            {
                hyperlink.TextToDisplay = $"({last6})";
                return;
            }

            if (hyperlink.TextToDisplay.EndsWith($"({last5})"))
            {
                // Replace last5 with last6: "Title (12345)" → "Title (012345)"
                hyperlink.TextToDisplay = hyperlink.TextToDisplay[..^(last5.Length + 2)] + $"{last6})";
            }
            else
            {
                // Append new content ID: "Title" → "Title (012345)"
                hyperlink.TextToDisplay = $"{hyperlink.TextToDisplay.Trim()} ({last6})";
            }
        }

        /// <summary>
        /// Removes invisible external hyperlinks
        /// </summary>
        public async Task<HyperlinkCleanupResult> RemoveInvisibleLinksAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default)
        {
            var result = new HyperlinkCleanupResult();

            try
            {
                _logger.LogInformation("Removing invisible hyperlinks from {Count} items", hyperlinks.Count);

                for (int i = hyperlinks.Count - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var hyperlink = hyperlinks[i];
                    if (string.IsNullOrWhiteSpace(hyperlink.TextToDisplay) &&
                        !string.IsNullOrWhiteSpace(hyperlink.Address))
                    {
                        result.RemovedHyperlinks.Add(
                            $"Removed invisible link at Page:{hyperlink.PageNumber} Line:{hyperlink.LineNumber}");
                        hyperlinks.RemoveAt(i);
                        result.RemovedCount++;
                    }
                }

                result.Success = true;
                _logger.LogInformation("Removed {Count} invisible hyperlinks", result.RemovedCount);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing invisible links");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Detects title changes by comparing current titles with API results
        /// </summary>
        /// <summary>
        /// Detects title changes by comparing current titles with API results (matching Legacy behavior)
        /// This only reports mismatches without making changes - for "Find Mismatched Titles" option
        /// </summary>
        public async Task<TitleUpdateResult> DetectTitleChangesAsync(
            List<HyperlinkData> hyperlinks,
            Dictionary<string, object> apiResults,
            CancellationToken cancellationToken = default)
        {
            var result = new TitleUpdateResult();

            try
            {
                _logger.LogInformation("Detecting title changes for {Count} hyperlinks", hyperlinks.Count);

                int possibleChangesCount = 0;

                foreach (var hyperlink in hyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var lookupId = ExtractLookupId(hyperlink);
                    if (string.IsNullOrEmpty(lookupId) || !apiResults.ContainsKey(lookupId)) continue;

                    if (apiResults[lookupId] is Dictionary<string, object> itemData)
                    {
                        if (HasStatusMarkers(hyperlink.TextToDisplay)) continue;

                        if (itemData.TryGetValue("Title", out var titleObj) &&
                            itemData.TryGetValue("Content_ID", out var contentIdObj))
                        {
                            string currentTitle = hyperlink.TextToDisplay?.Trim() ?? string.Empty;
                            string apiTitle = titleObj?.ToString()?.Trim() ?? string.Empty;
                            string contentId = contentIdObj?.ToString() ?? lookupId;

                            if (AreTitlesDifferent(currentTitle, apiTitle))
                            {
                                possibleChangesCount++;
                                string changeEntry = CreateTitleMismatchEntry(hyperlink, currentTitle, apiTitle, contentId);
                                result.TitleChanges.Add(changeEntry);
                            }
                        }
                    }
                }

                if (possibleChangesCount > 0)
                {
                    result.UpdatedTitles = possibleChangesCount;
                }

                result.Success = true;
                _logger.LogInformation("Detected {Count} potential title changes", possibleChangesCount);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting title changes");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Creates a title mismatch entry (matching Legacy format exactly)
        /// </summary>
        private string CreateTitleMismatchEntry(HyperlinkData hyperlink, string currentTitle, string correctTitle, string contentId)
        {
            return $"Page:{hyperlink.PageNumber} | Line:{hyperlink.LineNumber} | Title Mismatch, Please Review\n" +
                   $"        Current Title:    {currentTitle}\n" +
                   $"        Correct Title:    {correctTitle}\n" +
                   $"        Content ID:       {contentId}";
        }

        /// <summary>
        /// Checks if two titles are different (ignoring Content IDs and whitespace) - matching Legacy logic
        /// </summary>
        private bool AreTitlesDifferent(string currentTitle, string apiTitle)
        {
            string currentTitleForComparison = RemoveContentIdFromTitle(currentTitle).Trim();
            string apiTitleForComparison = RemoveContentIdFromTitle(apiTitle).Trim();

            return !string.IsNullOrEmpty(apiTitleForComparison) &&
                   !currentTitleForComparison.Equals(apiTitleForComparison, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes content ID pattern from title (matching Legacy behavior)
        /// </summary>
        private string RemoveContentIdFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            return ContentIdPatternRegex().Replace(title, "");
        }

        /// <summary>
        /// Checks if hyperlink display text has status markers (matching Legacy behavior)
        /// </summary>
        private bool HasStatusMarkers(string? textToDisplay) =>
            !string.IsNullOrEmpty(textToDisplay) &&
            (textToDisplay.Contains(ExpiredMarker) || textToDisplay.Contains(NotFoundMarker));

        /// <summary>
        /// Fixes double spaces in hyperlink display text
        /// </summary>
        public async Task<int> FixDoubleSpacesAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default)
        {
            int fixedCount = 0;

            try
            {
                _logger.LogInformation("Fixing double spaces in {Count} hyperlinks", hyperlinks.Count);

                foreach (var hyperlink in hyperlinks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.IsNullOrEmpty(hyperlink.TextToDisplay))
                    {
                        var originalText = hyperlink.TextToDisplay;
                        hyperlink.TextToDisplay = MultipleSpacesPatternRegex().Replace(hyperlink.TextToDisplay, " ");

                        if (originalText != hyperlink.TextToDisplay)
                        {
                            fixedCount++;
                        }
                    }
                }

                _logger.LogInformation("Fixed double spaces in {Count} hyperlinks", fixedCount);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing double spaces");
            }

            return fixedCount;
        }

        /// <summary>
        /// Applies status markers to hyperlink display text based on status (matching Legacy behavior)
        /// </summary>
        private void ApplyStatusMarkerIfNeeded(HyperlinkData hyperlink, Dictionary<string, object> itemData)
        {
            if (itemData.TryGetValue("Status", out var statusObj))
            {
                var status = statusObj?.ToString() ?? string.Empty;
                
                // Remove existing status markers first
                RemoveExistingStatusMarkers(hyperlink);
                
                // Apply new status marker if needed
                if (status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
                {
                    hyperlink.TextToDisplay += ExpiredMarker;
                }
                // Note: "Not Found" is handled separately when lookup fails completely
            }
        }

        /// <summary>
        /// Removes existing status markers from hyperlink display text
        /// </summary>
        private void RemoveExistingStatusMarkers(HyperlinkData hyperlink)
        {
            if (hyperlink.TextToDisplay.EndsWith(ExpiredMarker))
            {
                hyperlink.TextToDisplay = hyperlink.TextToDisplay[..^ExpiredMarker.Length];
            }
            else if (hyperlink.TextToDisplay.EndsWith(NotFoundMarker))
            {
                hyperlink.TextToDisplay = hyperlink.TextToDisplay[..^NotFoundMarker.Length];
            }
        }

        /// <summary>
        /// Marks hyperlink as not found (for hyperlinks not in dictionary/API)
        /// </summary>
        private void MarkAsNotFound(HyperlinkData hyperlink)
        {
            RemoveExistingStatusMarkers(hyperlink);
            hyperlink.TextToDisplay += NotFoundMarker;
        }

        /// <summary>
        /// Replaces hyperlinks based on replacement rules (matching Legacy behavior)
        /// </summary>
        public async Task<HyperlinkReplacementResult> ReplaceHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            List<HyperlinkReplacementRule> replacementRules,
            Dictionary<string, object> apiData,
            CancellationToken cancellationToken = default)
        {
            var result = new HyperlinkReplacementResult();

            try
            {
                _logger.LogInformation("Processing {Count} replacement rules for {HyperlinkCount} hyperlinks", 
                    replacementRules.Count, hyperlinks.Count);

                int replacedCount = 0;

                foreach (var rule in replacementRules.Where(IsValidReplacementRule))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    replacedCount += ProcessReplacementRule(hyperlinks, rule, apiData, result.ReplacedHyperlinks);
                }

                if (replacedCount > 0)
                {
                    result.ReplacementSummary = $"Replaced {replacedCount} hyperlinks";
                }

                result.ReplacedCount = replacedCount;
                result.Success = true;

                _logger.LogInformation("Hyperlink replacement completed: {Count} replaced", replacedCount);
                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing hyperlinks");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Validates a replacement rule (matching Legacy behavior)
        /// </summary>
        private bool IsValidReplacementRule(HyperlinkReplacementRule rule) =>
            !string.IsNullOrWhiteSpace(rule.OldTitle) &&
            !string.IsNullOrWhiteSpace(rule.NewFullContentId);

        /// <summary>
        /// Processes a single replacement rule against all hyperlinks (matching Legacy behavior)
        /// </summary>
        private int ProcessReplacementRule(List<HyperlinkData> hyperlinks, HyperlinkReplacementRule rule,
            Dictionary<string, object> apiData, List<string> replacedHyperlinks)
        {
            int count = 0;
            string oldTitle = rule.OldTitle.Trim();
            var (newContentIdLast6, _) = ExtractContentIdSuffixes(rule.NewFullContentId);

            // Lookup new title and document_id from API data using the full Content_ID
            string newTitle = rule.OldTitle; // Fallback to old title
            string documentId = string.Empty;

            if (apiData.ContainsKey(rule.NewFullContentId))
            {
                if (apiData[rule.NewFullContentId] is Dictionary<string, object> itemData)
                {
                    if (itemData.TryGetValue("Title", out var titleObj))
                        newTitle = titleObj?.ToString() ?? rule.OldTitle;
                    
                    if (itemData.TryGetValue("Document_ID", out var docIdObj))
                        documentId = docIdObj?.ToString() ?? string.Empty;
                }
            }

            foreach (var hyperlink in hyperlinks)
            {
                string sanitizedText = SanitizeHyperlinkText(hyperlink.TextToDisplay);

                if (sanitizedText.Equals(oldTitle, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyHyperlinkReplacement(hyperlink, newTitle, newContentIdLast6, documentId, replacedHyperlinks);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Sanitizes hyperlink text by removing content ID suffixes (matching Legacy behavior)
        /// </summary>
        private string SanitizeHyperlinkText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string sanitized = text.Trim();
            var match = ContentIdPatternRegex().Match(sanitized);

            return match.Success ? sanitized[..match.Index].Trim() : sanitized;
        }

        /// <summary>
        /// Applies a hyperlink replacement (matching Legacy behavior)
        /// </summary>
        private void ApplyHyperlinkReplacement(HyperlinkData hyperlink, string newTitle, string newContentIdLast6,
            string documentId, List<string> replacedHyperlinks)
        {
            string oldHyperlinkText = hyperlink.TextToDisplay ?? string.Empty;
            string newHyperlinkText = $"{newTitle} ({newContentIdLast6})";

            // Update hyperlink properties
            hyperlink.TextToDisplay = newHyperlinkText;
            hyperlink.Title = newTitle;

            // Construct new URL: https://thesource.cvshealth.com/nuxeo/thesource/#!/view?docid=Document_ID
            hyperlink.Address = "https://thesource.cvshealth.com/nuxeo/thesource/";
            hyperlink.SubAddress = $"#!/view?docid={documentId}";

            // Add to changelog
            replacedHyperlinks.Add($"Page:{hyperlink.PageNumber} | Line:{hyperlink.LineNumber} | Replaced Hyperlink based on User Replacement\n" +
                $"        Old Hyperlink: {oldHyperlinkText}\n" +
                $"        New Hyperlink: {newHyperlinkText}");
        }

        /// <summary>
        /// Replaces text in document based on replacement rules with location tracking
        /// </summary>
        public async Task<TextReplacementResult> ReplaceTextAsync(
            string filePath,
            List<TextReplacementRule> replacementRules,
            CancellationToken cancellationToken = default)
        {
            var result = new TextReplacementResult();

            try
            {
                _logger.LogInformation("Processing {Count} text replacement rules for document {FilePath}", 
                    replacementRules.Count, filePath);

                int totalReplacements = 0;

                using (var wordDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(filePath, true))
                {
                    var body = wordDoc.MainDocumentPart?.Document?.Body;
                    if (body == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Unable to access document body";
                        return result;
                    }

                    // Process each text replacement rule
                    foreach (var rule in replacementRules.Where(IsValidTextReplacementRule))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        totalReplacements += ProcessTextReplacementRule(body, rule, result.ReplacedTextItems, filePath);
                    }
                }

                result.ReplacedCount = totalReplacements;
                if (totalReplacements > 0)
                {
                    result.ReplacementSummary = $"Replaced text in {totalReplacements} locations";
                }

                result.Success = true;
                _logger.LogInformation("Text replacement completed: {Count} replacements made", totalReplacements);
                
                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing text in document");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
            }

            return result;
        }

        /// <summary>
        /// Validates a text replacement rule
        /// </summary>
        private bool IsValidTextReplacementRule(TextReplacementRule rule) =>
            !string.IsNullOrWhiteSpace(rule.OldText) && rule.NewText != null;

        /// <summary>
        /// Processes a single text replacement rule throughout the document
        /// </summary>
        private int ProcessTextReplacementRule(DocumentFormat.OpenXml.Wordprocessing.Body body, 
            TextReplacementRule rule, List<string> replacedTextItems, string documentName)
        {
            int replacementCount = 0;
            int pageNumber = 1; // Approximation since page breaks are complex in OpenXML
            int lineNumber = 1;

            var textElements = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().ToList();

            foreach (var textElement in textElements)
            {
                if (string.IsNullOrEmpty(textElement.Text)) continue;

                string originalText = textElement.Text;
                string newText = PerformTextReplacement(originalText, rule);

                if (newText != originalText)
                {
                    textElement.Text = newText;
                    replacementCount++;

                    // Record the replacement with location information (matching Legacy format)
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(documentName);
                    replacedTextItems.Add($"Page:{pageNumber} | Line:{lineNumber} | Replaced Text based on User Rule\n" +
                        $"        Old Text: {rule.OldText}\n" +
                        $"        New Text: {rule.NewText}");
                }

                // Increment line number (rough approximation)
                lineNumber++;
                
                // Simple page estimation (every 50 lines, though this is very approximate)
                if (lineNumber % 50 == 0)
                {
                    pageNumber++;
                }
            }

            return replacementCount;
        }

        /// <summary>
        /// Performs text replacement based on the rule settings
        /// </summary>
        private string PerformTextReplacement(string originalText, TextReplacementRule rule)
        {
            StringComparison comparison = rule.CaseSensitive ? 
                StringComparison.Ordinal : 
                StringComparison.OrdinalIgnoreCase;

            if (rule.WholeWordsOnly)
            {
                // Use regex for whole word matching
                var pattern = rule.CaseSensitive ? 
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(rule.OldText)}\b" :
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(rule.OldText)}\b";

                var regexOptions = rule.CaseSensitive ? 
                    System.Text.RegularExpressions.RegexOptions.None :
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase;

                return System.Text.RegularExpressions.Regex.Replace(originalText, pattern, rule.NewText, regexOptions);
            }
            else
            {
                // Simple string replacement
                if (rule.CaseSensitive)
                {
                    return originalText.Replace(rule.OldText, rule.NewText);
                }
                else
                {
                    // Case-insensitive replacement
                    return System.Text.RegularExpressions.Regex.Replace(
                        originalText, 
                        System.Text.RegularExpressions.Regex.Escape(rule.OldText), 
                        rule.NewText, 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
            }
        }

        private string ExtractLookupId(HyperlinkData hyperlink)
        {
            var fullAddress = hyperlink.Address +
                (!string.IsNullOrEmpty(hyperlink.SubAddress) ? "#" + hyperlink.SubAddress : "");

            // First try to extract Content ID from standard TSRC/CMS pattern in URL
            var match = IdPatternRegex().Match(fullAddress);
            if (match.Success)
                return match.Value.ToUpper();

            // Try to extract Document ID from docid= parameter
            var docIdMatch = DocIdParameterRegex().Match(fullAddress);
            if (docIdMatch.Success)
            {
                // Return the raw Document ID (everything after docid=)
                // This will be used for dictionary lookup to find the associated Content ID
                return docIdMatch.Groups[1].Value;
            }

            // Fall back to existing ContentID property if no patterns match
            return hyperlink.ContentID;
        }

        private List<string> ExtractLookupIds(List<HyperlinkData> hyperlinks)
        {
            return hyperlinks
                .Select(ExtractLookupId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
        }

        private string ExtractContentIdSuffix(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
                return string.Empty;

            var match = ContentIdPatternRegex().Match(displayText);
            return match.Success ? displayText[match.Index..] : string.Empty;
        }
    }
}