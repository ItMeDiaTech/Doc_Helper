using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Doc_Helper.Core.Models;

/// <summary>
/// Domain model representing a document in the processing system
/// Clean architecture - no dependencies on data layer
/// </summary>
public class DocumentData
{
    public int Id { get; set; }

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string FileHash { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime FileLastModified { get; set; }

    [Required]
    [StringLength(50)]
    public string ProcessingStatus { get; set; } = "Pending";

    public string? ProcessingNotes { get; set; }

    public DateTime? ProcessingStartTime { get; set; }

    public DateTime? ProcessingEndTime { get; set; }

    public int HyperlinkCount { get; set; }

    public int ProcessedHyperlinkCount { get; set; }

    public int FailedHyperlinkCount { get; set; }

    // Excel sync properties
    public string? ExcelPath { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public bool RequiresSync { get; set; }

    // Audit properties
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    /// <summary>
    /// Create a deep copy of the document data
    /// </summary>
    public DocumentData Clone()
    {
        return new DocumentData
        {
            Id = Id,
            FilePath = FilePath,
            FileName = FileName,
            FileHash = FileHash,
            FileSize = FileSize,
            FileLastModified = FileLastModified,
            ProcessingStatus = ProcessingStatus,
            ProcessingNotes = ProcessingNotes,
            ProcessingStartTime = ProcessingStartTime,
            ProcessingEndTime = ProcessingEndTime,
            HyperlinkCount = HyperlinkCount,
            ProcessedHyperlinkCount = ProcessedHyperlinkCount,
            FailedHyperlinkCount = FailedHyperlinkCount,
            ExcelPath = ExcelPath,
            LastSyncedAt = LastSyncedAt,
            RequiresSync = RequiresSync,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            IsDeleted = IsDeleted
        };
    }

    /// <summary>
    /// Generate file hash from file path
    /// </summary>
    public string GenerateFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Check if document requires processing
    /// </summary>
    public bool RequiresProcessing => ProcessingStatus == "Pending" || ProcessingStatus == "Failed";

    /// <summary>
    /// Calculate processing duration
    /// </summary>
    public TimeSpan? ProcessingDuration =>
        ProcessingStartTime.HasValue && ProcessingEndTime.HasValue
            ? ProcessingEndTime.Value - ProcessingStartTime.Value
            : null;

    /// <summary>
    /// Calculate processing success rate
    /// </summary>
    public double ProcessingSuccessRate =>
        HyperlinkCount > 0
            ? (double)ProcessedHyperlinkCount / HyperlinkCount
            : 0.0;

    public override bool Equals(object? obj)
    {
        if (obj is not DocumentData other) return false;
        return Id == other.Id && FileHash == other.FileHash;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, FileHash);
    }
}