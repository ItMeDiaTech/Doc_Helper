using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doc_Helper.Data.Entities;

/// <summary>
/// Entity Framework model for processing results and statistics
/// </summary>
[Table("ProcessingResults")]
public class ProcessingResultEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    // Document reference
    public int? DocumentId { get; set; }
    public virtual DocumentEntity? Document { get; set; }

    // Processing session information
    [Required]
    [MaxLength(100)]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(100)]
    public string ProcessingType { get; set; } = string.Empty; // "HyperlinkProcessing", "Validation", etc.

    public bool Success { get; set; }

    [MaxLength(2000)]
    public string ErrorMessage { get; set; } = string.Empty;

    [Column(TypeName = "TEXT")]
    public string ExceptionDetails { get; set; } = string.Empty;

    [Column(TypeName = "TEXT")]
    public string Warnings { get; set; } = string.Empty; // JSON array of warnings

    [Column(TypeName = "TEXT")]
    public string Metadata { get; set; } = string.Empty; // JSON metadata

    // Processing statistics
    public int TotalItems { get; set; } = 0;
    public int ProcessedItems { get; set; } = 0;
    public int FailedItems { get; set; } = 0;
    public int SkippedItems { get; set; } = 0;

    // Timing information
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan ProcessingDuration { get; set; } = TimeSpan.Zero;

    // File information
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; } = 0;

    // Performance metrics
    public double ItemsPerSecond { get; set; } = 0;
    public long BytesPerSecond { get; set; } = 0;

    // Retry information
    public int RetryAttempt { get; set; } = 0;
    public int MaxRetryAttempts { get; set; } = 0;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = "System";

    // Calculated properties for legacy compatibility
    public double SuccessRate => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;

    public TimeSpan AverageProcessingTime => ProcessedItems > 0
        ? TimeSpan.FromMilliseconds(ProcessingDuration.TotalMilliseconds / ProcessedItems)
        : TimeSpan.Zero;
}