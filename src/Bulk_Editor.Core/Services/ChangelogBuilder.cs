using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Services;

/// <summary>
/// Builds changelog content in the Legacy-compatible format
/// </summary>
public class ChangelogBuilder
{
    /// <summary>
    /// Builds complete changelog content with all sections (matching Legacy format exactly)
    /// </summary>
    public string BuildChangelogContent(
        List<string>? updatedLinks = null,
        List<string>? notFoundLinks = null, 
        List<string>? expiredLinks = null,
        List<string>? errorLinks = null,
        List<string>? titleMismatchDetections = null,
        List<string>? fixedMismatchedTitles = null,
        List<string>? internalHyperlinkIssues = null,
        List<string>? replacedHyperlinks = null,
        List<string>? replacedTextItems = null,
        int doubleSpaceCount = 0)
    {
        var content = new StringBuilder();

        // Ensure lists are not null
        updatedLinks ??= new List<string>();
        notFoundLinks ??= new List<string>();
        expiredLinks ??= new List<string>();
        errorLinks ??= new List<string>();
        titleMismatchDetections ??= new List<string>();
        fixedMismatchedTitles ??= new List<string>();
        internalHyperlinkIssues ??= new List<string>();
        replacedHyperlinks ??= new List<string>();
        replacedTextItems ??= new List<string>();

        // 1. Updated Links section (Legacy format)
        AppendSection(content, "Updated Links", updatedLinks);

        // 2. Found Expired section (Legacy format)
        AppendSection(content, "Found Expired", expiredLinks);

        // 3. Not Found section (Legacy format)
        AppendSection(content, "Not Found", notFoundLinks);

        // 4. Found Error section (Legacy format)
        AppendSection(content, "Found Error", errorLinks);

        // 5. Title Mismatch section (Legacy format - detection only)
        if (titleMismatchDetections.Count > 0)
        {
            AppendSection(content, "Title Mismatch", titleMismatchDetections);
        }

        // 6. Fixed Mismatched Titles section (Legacy format - actual fixes)
        if (fixedMismatchedTitles.Count > 0)
        {
            AppendSection(content, "Fixed Mismatched Titles", fixedMismatchedTitles);
        }

        // 7. Internal Hyperlink Issues section (Legacy format)
        if (internalHyperlinkIssues.Count > 0)
        {
            AppendSection(content, "Internal Hyperlink Issues", internalHyperlinkIssues);
        }

        // 8. Replaced Hyperlinks section (Legacy format)
        if (replacedHyperlinks.Count > 0)
        {
            AppendSection(content, "Replaced Hyperlinks", replacedHyperlinks);
        }

        // 9. NEW: Replaced Text section (Modern addition)
        if (replacedTextItems.Count > 0)
        {
            AppendSection(content, "Replaced Text", replacedTextItems);
        }

        // 10. Amount of Double Spaces Removed (Legacy format - at bottom with no indent)
        if (doubleSpaceCount > 0)
        {
            content.AppendLine($"Amount of Double Spaces Removed: {doubleSpaceCount}");
        }

        return content.ToString();
    }

    /// <summary>
    /// Appends a section with Legacy formatting: "Section Name (count):" followed by 4-space indented items
    /// </summary>
    private void AppendSection(StringBuilder content, string sectionName, List<string> items)
    {
        content.AppendLine($"{sectionName} ({items.Count}):");
        
        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                content.AppendLine($"    {item}");
            }
        }
        
        content.AppendLine(); // Empty line after each section
    }

    /// <summary>
    /// Builds changelog from processing results (convenience method)
    /// </summary>
    public string BuildFromResults(
        HyperlinkProcessingResult? hyperlinkResult = null,
        HyperlinkReplacementResult? hyperlinkReplacementResult = null,
        TextReplacementResult? textReplacementResult = null,
        int doubleSpaceCount = 0)
    {
        var updatedLinks = new List<string>();
        var notFoundLinks = new List<string>();
        var expiredLinks = new List<string>();
        var errorLinks = new List<string>();
        var replacedHyperlinks = new List<string>();
        var replacedTextItems = new List<string>();

        // Extract data from hyperlink processing result
        if (hyperlinkResult != null)
        {
            // Note: This would need to be expanded based on actual HyperlinkProcessingResult structure
            // For now, using placeholder logic
        }

        // Extract data from hyperlink replacement result
        if (hyperlinkReplacementResult != null)
        {
            replacedHyperlinks.AddRange(hyperlinkReplacementResult.ReplacedHyperlinks);
        }

        // Extract data from text replacement result
        if (textReplacementResult != null)
        {
            replacedTextItems.AddRange(textReplacementResult.ReplacedTextItems);
        }

        return BuildChangelogContent(
            updatedLinks: updatedLinks,
            notFoundLinks: notFoundLinks,
            expiredLinks: expiredLinks,
            errorLinks: errorLinks,
            replacedHyperlinks: replacedHyperlinks,
            replacedTextItems: replacedTextItems,
            doubleSpaceCount: doubleSpaceCount);
    }
}