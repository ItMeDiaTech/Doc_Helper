using System;
using System.Collections.Generic;

namespace Doc_Helper.Core.Models;

/// <summary>
/// Result of hyperlink replacement operations
/// </summary>
public class HyperlinkReplacementResult
{
    /// <summary>
    /// Whether the replacement operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Exception that occurred during processing (if any)
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Number of hyperlinks that were replaced
    /// </summary>
    public int ReplacedCount { get; set; }

    /// <summary>
    /// Summary message describing the replacements made
    /// </summary>
    public string ReplacementSummary { get; set; } = string.Empty;

    /// <summary>
    /// Detailed list of replacements made (for changelog)
    /// </summary>
    public List<string> ReplacedHyperlinks { get; set; } = new();

    /// <summary>
    /// Processing start time
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static HyperlinkReplacementResult CreateSuccess(int replacedCount, string summary = "")
    {
        return new HyperlinkReplacementResult
        {
            Success = true,
            ReplacedCount = replacedCount,
            ReplacementSummary = summary
        };
    }

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static HyperlinkReplacementResult CreateFailure(string errorMessage, Exception? exception = null)
    {
        return new HyperlinkReplacementResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}