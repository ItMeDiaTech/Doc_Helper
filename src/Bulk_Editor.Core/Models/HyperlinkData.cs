using System;
using System.ComponentModel.DataAnnotations;

namespace Doc_Helper.Core.Models;

/// <summary>
/// Modern domain model for hyperlink data
/// </summary>
public class HyperlinkData
{
    public int Id { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    public string SubAddress { get; set; } = string.Empty;

    [Required]
    public string TextToDisplay { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public int LineNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ContentID { get; set; } = string.Empty;

    public string DocumentID { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ElementId { get; set; } = string.Empty;

    // Processing metadata
    public string ProcessingStatus { get; set; } = "Pending";
    public DateTime? LastProcessedAt { get; set; }
    public string ProcessingNotes { get; set; } = string.Empty;

    // Document association
    public int? DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;

    // Audit information
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public string UpdatedBy { get; set; } = "System";

    /// <summary>
    /// Creates a deep copy of the hyperlink data
    /// </summary>
    public HyperlinkData Clone()
    {
        return new HyperlinkData
        {
            Id = Id,
            Address = Address,
            SubAddress = SubAddress,
            TextToDisplay = TextToDisplay,
            PageNumber = PageNumber,
            LineNumber = LineNumber,
            Title = Title,
            ContentID = ContentID,
            DocumentID = DocumentID,
            Status = Status,
            ElementId = ElementId,
            ProcessingStatus = ProcessingStatus,
            LastProcessedAt = LastProcessedAt,
            ProcessingNotes = ProcessingNotes,
            DocumentId = DocumentId,
            DocumentName = DocumentName,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            CreatedBy = CreatedBy,
            UpdatedBy = UpdatedBy
        };
    }

    /// <summary>
    /// Determines whether this instance equals another HyperlinkData instance
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not HyperlinkData other)
            return false;

        return Address == other.Address &&
               SubAddress == other.SubAddress &&
               TextToDisplay == other.TextToDisplay &&
               PageNumber == other.PageNumber &&
               LineNumber == other.LineNumber &&
               Title == other.Title &&
               ContentID == other.ContentID &&
               Status == other.Status &&
               ElementId == other.ElementId;
    }

    /// <summary>
    /// Returns the hash code for this instance
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            HashCode.Combine(Address, SubAddress, TextToDisplay, PageNumber, LineNumber),
            HashCode.Combine(Title, ContentID, Status, ElementId)
        );
    }

    /// <summary>
    /// Generates a content hash for deduplication purposes
    /// </summary>
    public string GenerateContentHash()
    {
        var content = $"{Address}|{SubAddress}|{TextToDisplay}|{Title}|{ContentID}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }
}