using DocumentExtractor.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentExtractor.Core.Interfaces
{
    /// <summary>
    /// Main interface for document processing operations.
    /// This is the primary service that orchestrates the entire document processing pipeline.
    /// 
    /// VB.NET to C# Interface Notes:
    /// - C# interfaces use 'interface' keyword instead of VB.NET's 'Interface...End Interface'
    /// - All members are public by default (no need for Public keyword)
    /// - async methods return Task or Task<T> for asynchronous operations
    /// - CancellationToken enables cancelling long-running operations
    /// </summary>
    public interface IDocumentProcessor
    {
        /// <summary>
        /// Process a single document and extract all relevant fields.
        /// This is the main method that most users will call.
        /// 
        /// Processing Steps:
        /// 1. Validate file exists and is readable
        /// 2. Extract text using OCR (Tesseract)
        /// 3. Detect document type and supplier
        /// 4. Try local learned patterns first
        /// 5. Fall back to cloud AI (Azure Form Recognizer) if needed
        /// 6. Store results and update pattern statistics
        /// </summary>
        /// <param name="filePath">Full path to the document file</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>ExtractedDocument with all processing results</returns>
        Task<ExtractedDocument> ProcessDocumentAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Process multiple documents in batch with progress reporting.
        /// This method is optimized for handling large numbers of documents efficiently.
        /// 
        /// Features:
        /// - Parallel processing where possible
        /// - Progress reporting via IProgress<T>
        /// - Individual error handling (one failed document doesn't stop the batch)
        /// - Automatic retry for transient failures
        /// </summary>
        /// <param name="filePaths">List of file paths to process</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Token to cancel the entire batch</param>
        /// <returns>List of ExtractedDocument results</returns>
        Task<List<ExtractedDocument>> ProcessBatchAsync(
            List<string> filePaths, 
            IProgress<BatchProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate that a file can be processed before attempting extraction.
        /// This quick check helps avoid processing errors and provides user feedback.
        /// 
        /// Validation checks:
        /// - File exists and is readable
        /// - File size is within limits
        /// - File format is supported (PDF, images, etc.)
        /// - File is not corrupted
        /// </summary>
        /// <param name="filePath">Path to file to validate</param>
        /// <returns>True if file can be processed</returns>
        Task<bool> ValidateDocumentAsync(string filePath);

        /// <summary>
        /// Get detailed validation results for a document.
        /// Provides specific error messages and warnings for troubleshooting.
        /// </summary>
        /// <param name="filePath">Path to file to validate</param>
        /// <returns>Validation result with details</returns>
        Task<DocumentValidationResult> GetValidationDetailsAsync(string filePath);

        /// <summary>
        /// Reprocess a document using updated patterns or settings.
        /// Useful when patterns have been improved or user has corrected previous results.
        /// </summary>
        /// <param name="document">Previously processed document</param>
        /// <param name="forceCloudProcessing">Force use of cloud AI even if local patterns exist</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated extraction results</returns>
        Task<ExtractedDocument> ReprocessDocumentAsync(
            ExtractedDocument document, 
            bool forceCloudProcessing = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get processing statistics and performance metrics.
        /// Useful for monitoring system performance and identifying bottlenecks.
        /// </summary>
        /// <returns>Processing statistics</returns>
        Task<ProcessingStatistics> GetProcessingStatisticsAsync();

        /// <summary>
        /// Event fired when document processing starts.
        /// Allows UI to show processing indicators and progress.
        /// </summary>
        event EventHandler<DocumentProcessingEventArgs>? ProcessingStarted;

        /// <summary>
        /// Event fired when document processing completes (successfully or with errors).
        /// </summary>
        event EventHandler<DocumentProcessingEventArgs>? ProcessingCompleted;

        /// <summary>
        /// Event fired during processing to report progress.
        /// Useful for long-running operations like OCR.
        /// </summary>
        event EventHandler<ProcessingProgressEventArgs>? ProgressReported;
    }

    /// <summary>
    /// Progress information for batch processing operations.
    /// This class provides detailed progress information for UI updates.
    /// </summary>
    public class BatchProcessingProgress
    {
        /// <summary>
        /// Total number of documents to process.
        /// </summary>
        public int TotalDocuments { get; set; }

        /// <summary>
        /// Number of documents completed (successfully or with errors).
        /// </summary>
        public int CompletedDocuments { get; set; }

        /// <summary>
        /// Number of documents processed successfully.
        /// </summary>
        public int SuccessfulDocuments { get; set; }

        /// <summary>
        /// Number of documents that failed processing.
        /// </summary>
        public int FailedDocuments { get; set; }

        /// <summary>
        /// Currently processing document name.
        /// </summary>
        public string? CurrentDocument { get; set; }

        /// <summary>
        /// Overall progress percentage (0.0 to 1.0).
        /// </summary>
        public double OverallProgress => TotalDocuments > 0 ? (double)CompletedDocuments / TotalDocuments : 0.0;

        /// <summary>
        /// Estimated time remaining based on current processing speed.
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Current processing phase (OCR, Pattern Matching, Cloud Processing, etc.).
        /// </summary>
        public string? CurrentPhase { get; set; }
    }

    /// <summary>
    /// Detailed validation result for a document.
    /// Provides specific information about what validation checks passed or failed.
    /// </summary>
    public class DocumentValidationResult
    {
        /// <summary>
        /// Whether the document passed all validation checks.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of validation errors that prevent processing.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// List of validation warnings (processing can continue but with limitations).
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Detected file format.
        /// </summary>
        public string? FileFormat { get; set; }

        /// <summary>
        /// Whether the file appears to be a scanned document (vs. text-based PDF).
        /// </summary>
        public bool IsScannedDocument { get; set; }

        /// <summary>
        /// Estimated processing time based on file size and type.
        /// </summary>
        public TimeSpan EstimatedProcessingTime { get; set; }

        /// <summary>
        /// Recommended processing approach for this document.
        /// </summary>
        public string? RecommendedApproach { get; set; }
    }

    /// <summary>
    /// Processing statistics and performance metrics.
    /// Useful for monitoring system performance and user analytics.
    /// </summary>
    public class ProcessingStatistics
    {
        /// <summary>
        /// Total number of documents processed.
        /// </summary>
        public int TotalDocumentsProcessed { get; set; }

        /// <summary>
        /// Number of documents processed successfully.
        /// </summary>
        public int SuccessfulProcessing { get; set; }

        /// <summary>
        /// Number of documents that failed processing.
        /// </summary>
        public int FailedProcessing { get; set; }

        /// <summary>
        /// Average processing time per document in milliseconds.
        /// </summary>
        public double AverageProcessingTimeMs { get; set; }

        /// <summary>
        /// Average confidence score across all processed documents.
        /// </summary>
        public double AverageConfidenceScore { get; set; }

        /// <summary>
        /// Number of documents processed using local patterns vs. cloud AI.
        /// </summary>
        public int LocalPatternUsage { get; set; }

        /// <summary>
        /// Number of documents processed using cloud AI.
        /// </summary>
        public int CloudAIUsage { get; set; }

        /// <summary>
        /// Most common document types processed.
        /// </summary>
        public Dictionary<DocumentType, int> DocumentTypeStats { get; set; } = new Dictionary<DocumentType, int>();

        /// <summary>
        /// Most common suppliers/companies processed.
        /// </summary>
        public Dictionary<string, int> SupplierStats { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// When these statistics were last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event arguments for document processing events.
    /// </summary>
    public class DocumentProcessingEventArgs : EventArgs
    {
        /// <summary>
        /// The document being processed.
        /// </summary>
        public ExtractedDocument Document { get; set; } = new ExtractedDocument();

        /// <summary>
        /// Processing start time.
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Processing end time (if completed).
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Whether processing was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if processing failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Event arguments for processing progress updates.
    /// </summary>
    public class ProcessingProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Current progress percentage (0.0 to 1.0).
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Current processing phase description.
        /// </summary>
        public string Phase { get; set; } = string.Empty;

        /// <summary>
        /// Additional status message.
        /// </summary>
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Document being processed.
        /// </summary>
        public string? DocumentName { get; set; }
    }
}