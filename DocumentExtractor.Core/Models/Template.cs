using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DocumentExtractor.Core.Models
{
    /// <summary>
    /// Represents a template that users upload to teach the AI how to fill with extracted data.
    /// Templates can be Excel files, Word documents, PDF forms, etc.
    /// </summary>
    public class Template
    {
        /// <summary>
        /// Unique identifier for this template.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// User-friendly name for this template.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this template is used for.
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Category of this template (Monthly Reports, Quarterly Reports, Client Reports, etc.).
        /// </summary>
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// File path to the template file on disk.
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Original filename when uploaded.
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File extension (xlsx, docx, pdf, etc.).
        /// </summary>
        [MaxLength(10)]
        public string FileExtension { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// When this template was uploaded/created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this template was last used.
        /// </summary>
        public DateTime? LastUsedDate { get; set; }

        /// <summary>
        /// How many times this template has been used.
        /// </summary>
        public int UsageCount { get; set; } = 0;

        /// <summary>
        /// Whether this template is currently active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Path to template preview image (for thumbnails).
        /// </summary>
        [MaxLength(500)]
        public string? PreviewImagePath { get; set; }

        /// <summary>
        /// JSON metadata about the template (field mappings, settings, etc.).
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Field mappings that define where extracted data should be placed.
        /// </summary>
        public virtual List<TemplateFieldMapping> FieldMappings { get; set; } = new();

        /// <summary>
        /// Update usage statistics when template is used.
        /// </summary>
        public void UpdateUsage()
        {
            UsageCount++;
            LastUsedDate = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a mapping between an extracted field and a location in the template.
    /// For example: "TotalAmount" field goes to cell "D15" in Excel template.
    /// </summary>
    public class TemplateFieldMapping
    {
        /// <summary>
        /// Unique identifier for this field mapping.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The template this mapping belongs to.
        /// </summary>
        [Required]
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property to the template.
        /// </summary>
        public virtual Template? Template { get; set; }

        /// <summary>
        /// Name of the field from extracted document (e.g., "TotalAmount", "AccountNumber").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Location in the template where this field should be placed.
        /// For Excel: "D15", "B8", etc.
        /// For PDF: field name like "totalAmount"
        /// For Word: bookmark name or table cell reference
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string TargetLocation { get; set; } = string.Empty;

        /// <summary>
        /// Type of target location (Cell, Field, Bookmark, etc.).
        /// </summary>
        [MaxLength(50)]
        public string LocationType { get; set; } = string.Empty;

        /// <summary>
        /// Optional formatting instructions for this field.
        /// </summary>
        [MaxLength(200)]
        public string? FormatInstructions { get; set; }

        /// <summary>
        /// Whether this field mapping is required.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Display order for field mappings.
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// When this mapping was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User-friendly description of this mapping.
        /// </summary>
        [MaxLength(200)]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Template categories for organization.
    /// </summary>
    public enum TemplateCategory
    {
        MonthlyReports,
        QuarterlyReports,
        ClientReports,
        ComplianceReports,
        ExpenseReports,
        FinancialReports,
        Custom
    }

    /// <summary>
    /// Types of template target locations.
    /// </summary>
    public enum TemplateLocationType
    {
        ExcelCell,      // D15, B8, etc.
        PDFField,       // Form field name
        WordBookmark,   // Bookmark name
        WordTableCell,  // Table cell reference
        Custom          // User-defined location
    }
}