using System;
using System.ComponentModel.DataAnnotations;

namespace Doc_Helper.Core.Models;

/// <summary>
/// Represents a rule for replacing hyperlinks based on title matching
/// </summary>
public class HyperlinkReplacementRule
{
    /// <summary>
    /// Rule name for identification
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The text to search for in hyperlink titles
    /// </summary>
    [Required]
    public string FindText { get; set; } = string.Empty;

    /// <summary>
    /// The replacement text or Content_ID
    /// </summary>
    [Required]
    public string ReplaceText { get; set; } = string.Empty;

    /// <summary>
    /// Match type: Contains, StartsWith, EndsWith, Exact, Regex
    /// </summary>
    public string MatchType { get; set; } = "Contains";
    
    /// <summary>
    /// Whether this rule is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Description of what this rule does
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// When this rule was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this rule was last modified
    /// </summary>
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Legacy properties for backward compatibility
    /// </summary>
    public string OldTitle 
    { 
        get => FindText; 
        set => FindText = value; 
    }
    
    public string NewFullContentId 
    { 
        get => ReplaceText; 
        set => ReplaceText = value; 
    }
    
    public string? NewTitle { get; set; }

    /// <summary>
    /// Validates that this replacement rule has the minimum required fields
    /// </summary>
    public bool IsValid => 
        !string.IsNullOrWhiteSpace(FindText) && 
        !string.IsNullOrWhiteSpace(ReplaceText);

    /// <summary>
    /// Creates a deep copy of this replacement rule
    /// </summary>
    public HyperlinkReplacementRule Clone()
    {
        return new HyperlinkReplacementRule
        {
            Name = Name,
            FindText = FindText,
            ReplaceText = ReplaceText,
            MatchType = MatchType,
            IsEnabled = IsEnabled,
            Description = Description,
            CreatedDate = CreatedDate,
            ModifiedDate = DateTime.UtcNow,
            NewTitle = NewTitle
        };
    }

    public override string ToString()
    {
        return $"Replace '{FindText}' with '{ReplaceText}' (Match: {MatchType})";
    }
}