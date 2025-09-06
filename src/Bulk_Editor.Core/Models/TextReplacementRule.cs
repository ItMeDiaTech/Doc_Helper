using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Doc_Helper.Core.Models;

/// <summary>
/// Represents a rule for replacing text in documents
/// </summary>
public class TextReplacementRule
{
    /// <summary>
    /// The text to search for and replace
    /// </summary>
    [Required]
    public string OldText { get; set; } = string.Empty;

    /// <summary>
    /// The text to replace it with
    /// </summary>
    [Required]
    public string NewText { get; set; } = string.Empty;

    /// <summary>
    /// Whether the search should be case-sensitive
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Whether to match whole words only
    /// </summary>
    public bool WholeWordsOnly { get; set; } = false;

    /// <summary>
    /// Validates that this replacement rule has the minimum required fields
    /// </summary>
    public bool IsValid => 
        !string.IsNullOrWhiteSpace(OldText) && 
        NewText != null; // NewText can be empty string for deletions

    /// <summary>
    /// Creates a deep copy of this replacement rule
    /// </summary>
    public TextReplacementRule Clone()
    {
        return new TextReplacementRule
        {
            OldText = OldText,
            NewText = NewText,
            CaseSensitive = CaseSensitive,
            WholeWordsOnly = WholeWordsOnly
        };
    }

    public override string ToString()
    {
        var options = new List<string>();
        if (CaseSensitive) options.Add("case-sensitive");
        if (WholeWordsOnly) options.Add("whole words");
        
        var optionsText = options.Any() ? $" ({string.Join(", ", options)})" : "";
        return $"Replace '{OldText}' with '{NewText}'{optionsText}";
    }
}