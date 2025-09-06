using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doc_Helper.Data.Entities;

/// <summary>
/// Entity Framework model for hyperlink data stored in SQLite database
/// </summary>
[Table("Hyperlinks")]
public class HyperlinkEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(500)]
    public string SubAddress { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string TextToDisplay { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public int LineNumber { get; set; }

    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ContentID { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ElementId { get; set; } = string.Empty;

    // Navigation properties for document association
    public int? DocumentId { get; set; }
    public virtual DocumentEntity? Document { get; set; }

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

    // Processing metadata
    [MaxLength(50)]
    public string ProcessingStatus { get; set; } = "Pending"; // Pending, Processing, Completed, Failed

    public DateTime? LastProcessedAt { get; set; }

    [MaxLength(1000)]
    public string ProcessingNotes { get; set; } = string.Empty;

    // Hash for deduplication
    [MaxLength(64)]
    public string ContentHash { get; set; } = string.Empty;
}