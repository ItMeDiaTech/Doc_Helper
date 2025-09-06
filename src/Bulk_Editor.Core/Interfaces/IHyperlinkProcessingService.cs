using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces
{
    /// <summary>
    /// Hyperlink processing service interface for advanced hyperlink operations
    /// </summary>
    public interface IHyperlinkProcessingService
    {
        /// <summary>
        /// Processes hyperlinks for validation and cleanup
        /// </summary>
        /// <param name="hyperlinks">Hyperlinks to process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processing result with updated hyperlinks</returns>
        Task<HyperlinkProcessingResult> ProcessHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fixes internal hyperlinks by validating anchors
        /// </summary>
        /// <param name="hyperlinks">Hyperlinks to fix</param>
        /// <param name="documentContent">Document content for anchor validation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Internal link fix result</returns>
        Task<InternalLinkFixResult> FixInternalLinksAsync(
            List<HyperlinkData> hyperlinks,
            string? documentContent = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates hyperlink titles based on API data
        /// </summary>
        /// <param name="hyperlinks">Hyperlinks to update</param>
        /// <param name="apiData">API data for title updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Title update result</returns>
        Task<TitleUpdateResult> UpdateTitlesAsync(
            List<HyperlinkData> hyperlinks,
            Dictionary<string, object> apiData,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Appends content IDs to hyperlink display text
        /// </summary>
        /// <param name="hyperlinks">Hyperlinks to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Content ID append result</returns>
        Task<ContentIdAppendResult> AppendContentIdsAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes invisible external hyperlinks
        /// </summary>
        /// <param name="hyperlinks">Hyperlinks to clean</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cleanup result</returns>
        Task<HyperlinkCleanupResult> RemoveInvisibleLinksAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Detects title changes by comparing current titles with API results
        /// </summary>
        /// <param name="hyperlinks">Hyperlinks to check</param>
        /// <param name="apiResults">API results for comparison</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Title detection result</returns>
        Task<TitleUpdateResult> DetectTitleChangesAsync(
            List<HyperlinkData> hyperlinks,
            Dictionary<string, object> apiResults,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fixes double spaces in hyperlink display text
        /// </summary>
        /// <param name="hyperlinks">Hyperlinks to process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of hyperlinks fixed</returns>
        Task<int> FixDoubleSpacesAsync(
            List<HyperlinkData> hyperlinks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces hyperlinks based on replacement rules
        /// </summary>
        /// <param name="hyperlinks">Hyperlinks to process</param>
        /// <param name="replacementRules">Rules for hyperlink replacement</param>
        /// <param name="apiData">API/dictionary data for lookups</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Hyperlink replacement result</returns>
        Task<HyperlinkReplacementResult> ReplaceHyperlinksAsync(
            List<HyperlinkData> hyperlinks,
            List<HyperlinkReplacementRule> replacementRules,
            Dictionary<string, object> apiData,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces text in document based on replacement rules
        /// </summary>
        /// <param name="filePath">Path to the Word document</param>
        /// <param name="replacementRules">Rules for text replacement</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Text replacement result</returns>
        Task<TextReplacementResult> ReplaceTextAsync(
            string filePath,
            List<TextReplacementRule> replacementRules,
            CancellationToken cancellationToken = default);
    }
}