using System;
using System.Collections.Generic;

namespace DocumentExtractor.Core.Models
{
    /// <summary>
    /// Represents a document that has been processed for data extraction.
    /// This is the main entity that holds all information about a processed document.
    /// 
    /// Key Concept for VB.NET Developers:
    /// - C# uses 'using' instead of 'Imports' for namespace references
    /// - Properties use { get; set; } syntax instead of Property...End Property
    /// - List<T> is equivalent to VB.NET's List(Of T)
    /// </summary>
    public class ExtractedDocument
    {
        /// <summary>
        /// Unique identifier for this document processing session.
        /// Using string instead of Guid for easier database storage and API compatibility.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Full file system path to the original document.
        /// Example: "/Users/john/Documents/utility_bill_jan2024.pdf"
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Just the filename without the path.
        /// Example: "utility_bill_jan2024.pdf"
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The type of document we detected (utility bill, invoice, etc.).
        /// This drives which extraction patterns and fields we look for.
        /// </summary>
        public DocumentType DocumentType { get; set; } = DocumentType.Unknown;

        /// <summary>
        /// The company or organization that issued this document.
        /// Example: "ConEd", "Verizon", "AT&T"
        /// This is crucial for applying learned patterns specific to each supplier.
        /// </summary>
        public string Supplier { get; set; } = string.Empty;

        /// <summary>
        /// When this document was processed by our system.
        /// Using DateTime instead of DateOnly for compatibility across .NET versions.
        /// </summary>
        public DateTime ProcessedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// All the individual data fields extracted from this document.
        /// Example: Account number, due date, total amount, etc.
        /// This is the core value of our application.
        /// </summary>
        public List<ExtractedField> Fields { get; set; } = new List<ExtractedField>();

        /// <summary>
        /// Overall confidence score for this document processing (0.0 to 1.0).
        /// Calculated as the average confidence of all extracted fields.
        /// Helps users identify which documents need manual review.
        /// </summary>
        public double OverallConfidence { get; set; } = 0.0;

        /// <summary>
        /// Current processing status of this document.
        /// Allows for batch processing and progress tracking.
        /// </summary>
        public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

        /// <summary>
        /// Error message if processing failed.
        /// Null or empty if processing was successful.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Raw text extracted from the document via OCR.
        /// Stored for debugging and reprocessing purposes.
        /// Note: Nullable reference types (string?) require .NET 6+ and nullable context.
        /// </summary>
        public string? RawText { get; set; }

        /// <summary>
        /// File size in bytes.
        /// Useful for progress tracking and storage management.
        /// </summary>
        public long FileSizeBytes { get; set; } = 0;

        /// <summary>
        /// How long it took to process this document (in milliseconds).
        /// Useful for performance monitoring and user feedback.
        /// </summary>
        public int ProcessingTimeMs { get; set; } = 0;

        /// <summary>
        /// Constructor for creating a new document processing session.
        /// C# Note: Unlike VB.NET's New(), C# constructors don't use a keyword.
        /// </summary>
        public ExtractedDocument()
        {
            // Initialize with default values - already done via property initializers above
            // Property initializers are a C# feature that sets default values inline
        }

        /// <summary>
        /// Constructor with file path - demonstrates method overloading in C#.
        /// This is equivalent to VB.NET's overloaded New() methods.
        /// </summary>
        /// <param name="filePath">Path to the document file</param>
        public ExtractedDocument(string filePath) : this()
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            
            // Get file size if file exists
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                FileSizeBytes = fileInfo.Length;
            }
        }

        /// <summary>
        /// Calculate the overall confidence score based on individual field confidences.
        /// This method demonstrates C# LINQ usage - similar to VB.NET's LINQ but with different syntax.
        /// </summary>
        public void CalculateOverallConfidence()
        {
            if (Fields == null || Fields.Count == 0)
            {
                OverallConfidence = 0.0;
                return;
            }

            // LINQ Average method - equivalent to VB.NET's Fields.Average(Function(f) f.Confidence)
            OverallConfidence = Fields.Average(field => field.Confidence);
        }

        /// <summary>
        /// Get all fields of a specific type.
        /// Demonstrates LINQ Where clause and method chaining.
        /// </summary>
        /// <param name="fieldType">The type of fields to retrieve</param>
        /// <returns>List of fields matching the specified type</returns>
        public List<ExtractedField> GetFieldsByType(FieldType fieldType)
        {
            // C# LINQ syntax: collection.Where(predicate).ToList()
            // VB.NET equivalent: collection.Where(Function(item) condition).ToList()
            return Fields.Where(field => field.Type == fieldType).ToList();
        }

        /// <summary>
        /// Check if this document has any fields below the specified confidence threshold.
        /// Returns true if manual review is recommended.
        /// </summary>
        /// <param name="minimumConfidence">Minimum acceptable confidence (default: 0.8)</param>
        /// <returns>True if any fields need review</returns>
        public bool NeedsManualReview(double minimumConfidence = 0.8)
        {
            return Fields.Any(field => field.Confidence < minimumConfidence);
        }
    }
}