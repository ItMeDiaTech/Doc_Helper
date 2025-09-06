using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doc_Helper.Data.Entities;

/// <summary>
/// Entity Framework model for document information
/// </summary>
[Table("Documents")]
public class DocumentEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime FileModifiedDate { get; set; }

    [MaxLength(100)]
    public string DocumentType { get; set; } = "Word"; // Word, Excel, etc.

    [MaxLength(50)]
    public string DocumentVersion { get; set; } = string.Empty;

    // Processing information
    [MaxLength(50)]
    public string ProcessingStatus { get; set; } = "Pending"; // Pending, Processing, Completed, Failed, Skipped

    public DateTime? LastProcessedAt { get; set; }

    public DateTime? LastValidatedAt { get; set; }

    [MaxLength(2000)]
    public string ProcessingNotes { get; set; } = string.Empty;

    public int HyperlinkCount { get; set; } = 0;

    public int ProcessedHyperlinkCount { get; set; } = 0;

    public int FailedHyperlinkCount { get; set; } = 0;

    // Navigation properties
    public virtual ICollection<HyperlinkEntity> Hyperlinks { get; set; } = new List<HyperlinkEntity>();

    public virtual ICollection<ProcessingResultEntity> ProcessingResults { get; set; } = new List<ProcessingResultEntity>();

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = "System";

    [MaxLength(100)]
    public string UpdatedBy { get; set; } = "System";

    // Soft delete support
    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    // Excel/SharePoint integration
    [MaxLength(1000)]
    public string SourceExcelPath { get; set; } = string.Empty;

    public DateTime? LastSyncedFromExcel { get; set; }

    [MaxLength(100)]
    public string ExcelRowReference { get; set; } = string.Empty;
}