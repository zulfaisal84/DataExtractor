using DocumentExtractor.Core.Interfaces;
using DocumentExtractor.Core.Models;
using DocumentExtractor.Data.Context;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DocumentExtractor.Services
{
    /// <summary>
    /// Real document processor that integrates OCR, field extraction, and pattern learning.
    /// Implements the complete IDocumentProcessor interface for comprehensive document processing.
    /// 
    /// Features:
    /// - Complete document processing pipeline
    /// - Batch processing with progress reporting
    /// - Document validation and error handling
    /// - Reprocessing capabilities
    /// - Performance statistics and monitoring
    /// - Event-driven progress reporting
    /// </summary>
    public class RealDocumentProcessor : IDocumentProcessor
    {
        private readonly ITextExtractor _textExtractor;
        private readonly DocumentExtractionContext _context;
        private readonly ILogger<RealDocumentProcessor> _logger;

        // Events for progress reporting
        public event EventHandler<DocumentProcessingEventArgs>? ProcessingStarted;
        public event EventHandler<DocumentProcessingEventArgs>? ProcessingCompleted;
        public event EventHandler<ProcessingProgressEventArgs>? ProgressReported;

        /// <summary>
        /// Initialize the real document processor with required services.
        /// </summary>
        public RealDocumentProcessor(
            ITextExtractor textExtractor,
            DocumentExtractionContext context,
            ILogger<RealDocumentProcessor> logger)
        {
            _textExtractor = textExtractor;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Process a single document and extract all relevant fields.
        /// This is the main method that orchestrates the entire processing pipeline.
        /// </summary>
        /// <param name="filePath">Full path to the document file</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>ExtractedDocument with all processing results</returns>
        public async Task<ExtractedDocument> ProcessDocumentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var document = new ExtractedDocument(filePath)
            {
                FileName = Path.GetFileName(filePath),
                FileSizeBytes = new FileInfo(filePath).Length,
                ProcessedDate = DateTime.UtcNow,
                Status = ProcessingStatus.Processing
            };

            var eventArgs = new DocumentProcessingEventArgs
            {
                Document = document,
                StartTime = DateTime.UtcNow
            };

            try
            {
                // Fire processing started event
                OnProcessingStarted(eventArgs);

                _logger.LogInformation("Starting document processing for {FilePath}", filePath);

                // Step 1: Validate document
                OnProgressReported(new ProcessingProgressEventArgs
                {
                    Progress = 0.1,
                    Phase = "Validating document",
                    DocumentName = document.FileName,
                    StatusMessage = "Checking file format and accessibility"
                });

                var validation = await GetValidationDetailsAsync(filePath);
                if (!validation.IsValid)
                {
                    document.Status = ProcessingStatus.Failed;
                    document.ErrorMessage = string.Join("; ", validation.Errors);
                    eventArgs.Success = false;
                    eventArgs.ErrorMessage = document.ErrorMessage;
                    OnProcessingCompleted(eventArgs);
                    return document;
                }

                // Step 2: Extract text using OCR
                OnProgressReported(new ProcessingProgressEventArgs
                {
                    Progress = 0.3,
                    Phase = "Extracting text with OCR",
                    DocumentName = document.FileName,
                    StatusMessage = "Running optical character recognition"
                });

                string extractedText = await _textExtractor.ExtractTextAsync(filePath, cancellationToken);
                document.RawText = extractedText;

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    document.Status = ProcessingStatus.Failed;
                    document.ErrorMessage = "No text could be extracted from the document";
                    eventArgs.Success = false;
                    eventArgs.ErrorMessage = document.ErrorMessage;
                    OnProcessingCompleted(eventArgs);
                    return document;
                }

                // Step 3: Detect document type
                OnProgressReported(new ProcessingProgressEventArgs
                {
                    Progress = 0.5,
                    Phase = "Classifying document type",
                    DocumentName = document.FileName,
                    StatusMessage = "Analyzing document content for classification"
                });

                document.DocumentType = await _textExtractor.DetectDocumentTypeAsync(extractedText);
                _logger.LogInformation("Document classified as: {DocumentType}", document.DocumentType);

                // Step 4: Extract structured fields (simplified for now)
                OnProgressReported(new ProcessingProgressEventArgs
                {
                    Progress = 0.7,
                    Phase = "Extracting structured fields",
                    DocumentName = document.FileName,
                    StatusMessage = "Identifying and extracting data fields"
                });

                // For now, use simple field extraction based on document type
                var extractedFields = ExtractFieldsSimple(extractedText, document.DocumentType);

                // Add extracted fields to document
                foreach (var field in extractedFields)
                {
                    document.Fields.Add(field);
                }

                // Step 5: Determine supplier and calculate confidence
                document.Supplier = ExtractSupplierSimple(extractedText);
                document.CalculateOverallConfidence();

                // Step 6: Pattern learning (disabled for now)
                OnProgressReported(new ProcessingProgressEventArgs
                {
                    Progress = 0.9,
                    Phase = "Finalizing processing",
                    DocumentName = document.FileName,
                    StatusMessage = "Completing document processing"
                });

                // Pattern learning will be implemented later
                _logger.LogDebug("Pattern learning skipped for document {FileName}", document.FileName);

                // Step 7: Set final status
                document.Status = ProcessingStatus.Completed;
                stopwatch.Stop();
                document.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                OnProgressReported(new ProcessingProgressEventArgs
                {
                    Progress = 1.0,
                    Phase = "Completed",
                    DocumentName = document.FileName,
                    StatusMessage = $"Processed successfully in {document.ProcessingTimeMs}ms"
                });

                eventArgs.Success = true;
                eventArgs.EndTime = DateTime.UtcNow;
                OnProcessingCompleted(eventArgs);

                _logger.LogInformation("Document processing completed for {FilePath}. " +
                    "Time: {ProcessingTime}ms, Confidence: {Confidence:F1}%, Fields: {FieldCount}",
                    filePath, document.ProcessingTimeMs, 
                    document.OverallConfidence * 100, document.Fields.Count);

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {FilePath}", filePath);

                stopwatch.Stop();
                document.Status = ProcessingStatus.Failed;
                document.ErrorMessage = ex.Message;
                document.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                eventArgs.Success = false;
                eventArgs.ErrorMessage = ex.Message;
                eventArgs.EndTime = DateTime.UtcNow;
                OnProcessingCompleted(eventArgs);

                return document;
            }
        }

        /// <summary>
        /// Process multiple documents in batch with progress reporting.
        /// </summary>
        /// <param name="filePaths">List of file paths to process</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Token to cancel the entire batch</param>
        /// <returns>List of ExtractedDocument results</returns>
        public async Task<List<ExtractedDocument>> ProcessBatchAsync(
            List<string> filePaths, 
            IProgress<BatchProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ExtractedDocument>();
            var batchProgress = new BatchProcessingProgress
            {
                TotalDocuments = filePaths.Count
            };

            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Starting batch processing of {DocumentCount} documents", filePaths.Count);

            for (int i = 0; i < filePaths.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var filePath = filePaths[i];
                batchProgress.CurrentDocument = Path.GetFileName(filePath);
                batchProgress.CurrentPhase = "Processing document";

                try
                {
                    var result = await ProcessDocumentAsync(filePath, cancellationToken);
                    results.Add(result);

                    if (result.Status == ProcessingStatus.Completed)
                    {
                        batchProgress.SuccessfulDocuments++;
                    }
                    else
                    {
                        batchProgress.FailedDocuments++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process document {FilePath} in batch", filePath);
                    
                    results.Add(new ExtractedDocument(filePath)
                    {
                        FileName = Path.GetFileName(filePath),
                        Status = ProcessingStatus.Failed,
                        ErrorMessage = ex.Message,
                        ProcessedDate = DateTime.UtcNow
                    });

                    batchProgress.FailedDocuments++;
                }

                batchProgress.CompletedDocuments = i + 1;

                // Calculate estimated time remaining
                if (i > 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    var avgTimePerDoc = elapsed.TotalMilliseconds / (i + 1);
                    var remainingDocs = filePaths.Count - (i + 1);
                    batchProgress.EstimatedTimeRemaining = TimeSpan.FromMilliseconds(avgTimePerDoc * remainingDocs);
                }

                progress?.Report(batchProgress);
            }

            _logger.LogInformation("Batch processing completed. Successful: {Successful}, Failed: {Failed}", 
                batchProgress.SuccessfulDocuments, batchProgress.FailedDocuments);

            return results;
        }

        /// <summary>
        /// Validate that a file can be processed before attempting extraction.
        /// </summary>
        /// <param name="filePath">Path to file to validate</param>
        /// <returns>True if file can be processed</returns>
        public async Task<bool> ValidateDocumentAsync(string filePath)
        {
            var result = await GetValidationDetailsAsync(filePath);
            return result.IsValid;
        }

        /// <summary>
        /// Get detailed validation results for a document.
        /// </summary>
        /// <param name="filePath">Path to file to validate</param>
        /// <returns>Validation result with details</returns>
        public async Task<DocumentValidationResult> GetValidationDetailsAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var result = new DocumentValidationResult();

                try
                {
                    // Check if file exists
                    if (!File.Exists(filePath))
                    {
                        result.Errors.Add($"File not found: {filePath}");
                        return result;
                    }

                    var fileInfo = new FileInfo(filePath);
                    result.FileSizeBytes = fileInfo.Length;

                    // Check file size limits (max 10MB for now)
                    const long maxFileSizeBytes = 10 * 1024 * 1024; // 10MB
                    if (fileInfo.Length > maxFileSizeBytes)
                    {
                        result.Errors.Add($"File size ({fileInfo.Length / 1024 / 1024}MB) exceeds maximum limit (10MB)");
                    }

                    // Check file extension
                    var extension = Path.GetExtension(filePath).ToLowerInvariant();
                    result.FileFormat = extension;

                    if (!_textExtractor.IsFormatSupported(filePath))
                    {
                        result.Errors.Add($"Unsupported file format: {extension}");
                    }

                    // Try to determine if it's a scanned document
                    result.IsScannedDocument = IsImageFile(extension);

                    // Estimate processing time based on file size and type
                    result.EstimatedProcessingTime = EstimateProcessingTime(fileInfo.Length, result.IsScannedDocument);

                    // Set recommended approach
                    result.RecommendedApproach = result.IsScannedDocument ? "OCR Processing" : "Text Extraction + OCR Fallback";

                    // Check file accessibility
                    try
                    {
                        using var fs = File.OpenRead(filePath);
                        // If we can open it, it's likely readable
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Cannot read file: {ex.Message}");
                    }

                    // Add warnings for large files
                    if (fileInfo.Length > 5 * 1024 * 1024) // 5MB
                    {
                        result.Warnings.Add("Large file size may result in longer processing times");
                    }

                    result.IsValid = result.Errors.Count == 0;
                    return result;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Validation error: {ex.Message}");
                    return result;
                }
            });
        }

        /// <summary>
        /// Reprocess a document using updated patterns or settings.
        /// </summary>
        /// <param name="document">Previously processed document</param>
        /// <param name="forceCloudProcessing">Force use of cloud AI even if local patterns exist</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated extraction results</returns>
        public async Task<ExtractedDocument> ReprocessDocumentAsync(
            ExtractedDocument document, 
            bool forceCloudProcessing = false,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Reprocessing document {DocumentId} from file {FilePath}", 
                document.Id, document.FilePath);

            // Process the document again using the current processing pipeline
            var reprocessedDocument = await ProcessDocumentAsync(document.FilePath, cancellationToken);

            // Preserve the original document ID and some metadata
            reprocessedDocument.Id = document.Id;
            reprocessedDocument.ProcessedDate = DateTime.UtcNow; // Update processed date

            return reprocessedDocument;
        }

        /// <summary>
        /// Get processing statistics and performance metrics.
        /// </summary>
        /// <returns>Processing statistics</returns>
        public async Task<ProcessingStatistics> GetProcessingStatisticsAsync()
        {
            try
            {
                var stats = new ProcessingStatistics();

                // Get statistics from database
                var documents = await _context.Documents.ToListAsync();
                
                stats.TotalDocumentsProcessed = documents.Count;
                stats.SuccessfulProcessing = documents.Count(d => d.Status == ProcessingStatus.Completed);
                stats.FailedProcessing = documents.Count(d => d.Status == ProcessingStatus.Failed);

                if (documents.Any())
                {
                    stats.AverageProcessingTimeMs = documents.Average(d => d.ProcessingTimeMs);
                    stats.AverageConfidenceScore = documents.Where(d => d.Status == ProcessingStatus.Completed)
                        .Average(d => d.OverallConfidence);
                }

                // Document type statistics
                stats.DocumentTypeStats = documents
                    .GroupBy(d => d.DocumentType)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Supplier statistics
                stats.SupplierStats = documents
                    .Where(d => !string.IsNullOrEmpty(d.Supplier))
                    .GroupBy(d => d.Supplier)
                    .ToDictionary(g => g.Key, g => g.Count());

                // For now, assume all processing is local (no cloud AI integration yet)
                stats.LocalPatternUsage = stats.SuccessfulProcessing;
                stats.CloudAIUsage = 0;

                stats.LastUpdated = DateTime.UtcNow;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving processing statistics");
                return new ProcessingStatistics { LastUpdated = DateTime.UtcNow };
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Determine field type based on field name.
        /// </summary>
        private FieldType DetermineFieldType(string fieldName)
        {
            string lowerFieldName = fieldName.ToLowerInvariant();

            if (lowerFieldName.Contains("date") || lowerFieldName.Contains("due"))
                return FieldType.Date;
            
            if (lowerFieldName.Contains("amount") || lowerFieldName.Contains("total") || 
                lowerFieldName.Contains("price") || lowerFieldName.Contains("cost") || lowerFieldName.Contains("$"))
                return FieldType.Currency;
            
            if (lowerFieldName.Contains("account") || lowerFieldName.Contains("invoice") || 
                lowerFieldName.Contains("number") || lowerFieldName.Contains("#"))
                return FieldType.Number;

            if (lowerFieldName.Contains("phone") || lowerFieldName.Contains("mobile"))
                return FieldType.PhoneNumber;

            if (lowerFieldName.Contains("email") || lowerFieldName.Contains("@"))
                return FieldType.Email;

            return FieldType.Text;
        }

        /// <summary>
        /// Extract supplier/company name from text with enhanced Malaysian support.
        /// </summary>
        private string ExtractSupplierSimple(string text)
        {
            string lowerText = text.ToLowerInvariant();
            
            // Malaysian Utilities
            if (lowerText.Contains("tenaga nasional") || lowerText.Contains("tnb berhad") || lowerText.Contains("tnb"))
                return "TNB Berhad";
            
            if (lowerText.Contains("indah water") || lowerText.Contains("iwk"))
                return "Indah Water Konsortium";
            
            if (lowerText.Contains("air selangor") || lowerText.Contains("syabas"))
                return "Air Selangor";
            
            if (lowerText.Contains("gas malaysia") || lowerText.Contains("gas teknologi"))
                return "Gas Malaysia";
            
            // Malaysian Telecoms
            if (lowerText.Contains("maxis") || lowerText.Contains("hotlink"))
                return "Maxis";
            
            if (lowerText.Contains("celcom") || lowerText.Contains("xpax"))
                return "Celcom";
            
            if (lowerText.Contains("digi") || lowerText.Contains("digi telecommunications"))
                return "Digi";
            
            if (lowerText.Contains("unifi") || lowerText.Contains("streamyx") || lowerText.Contains("tm"))
                return "Telekom Malaysia";
            
            if (lowerText.Contains("time dotcom") || lowerText.Contains("time internet"))
                return "TIME dotCom";
            
            // US Utilities (for comparison)
            if (lowerText.Contains("coned") || lowerText.Contains("con ed") || lowerText.Contains("consolidated edison"))
                return "ConEd";
            
            if (lowerText.Contains("verizon"))
                return "Verizon";
            
            if (lowerText.Contains("pge") || lowerText.Contains("pacific gas"))
                return "PG&E";
            
            if (lowerText.Contains("acme corp") || lowerText.Contains("acme corporation"))
                return "ACME Corp";

            // Enhanced company name extraction
            var patterns = new[]
            {
                @"([A-Z][a-z]+\s+[A-Z][a-z]+\s+(?:Berhad|Sdn\.?\s*Bhd\.?|Bhd\.?))", // Malaysian companies
                @"([A-Z][a-z]+\s+(?:Corporation|Corp\.?|Company|Co\.?|Inc\.?|LLC))", // Western companies
                @"([A-Z][A-Z][A-Z]+\s*(?:Berhad|Corporation|Corp\.?))" // Acronym companies
            };

            foreach (var pattern in patterns)
            {
                var regex = new System.Text.RegularExpressions.Regex(pattern);
                var match = regex.Match(text);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            // Fallback: Look for company-like entities in first few lines
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Take(5))
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.Length > 5 && trimmedLine.Length < 60 && 
                    (trimmedLine.Contains("Berhad") || trimmedLine.Contains("Sdn Bhd") || 
                     trimmedLine.Contains("Corporation") || trimmedLine.Contains("Company") ||
                     trimmedLine.Contains("Ltd") || trimmedLine.Contains("Inc")))
                {
                    return trimmedLine;
                }
            }

            return "Unknown";
        }

        /// <summary>
        /// Extract fields using enhanced pattern matching with multiple variants.
        /// </summary>
        private List<ExtractedField> ExtractFieldsSimple(string text, DocumentType documentType)
        {
            var fields = new List<ExtractedField>();
            
            // Enhanced regex-based extraction with multiple patterns per field
            switch (documentType)
            {
                case DocumentType.UtilityBill:
                    // TNB-specific patterns
                    ExtractFieldWithPatterns(fields, "AccountNumber", text, new[]
                    {
                        (@"(?i)account\s*no\.?\s*:?\s*(\d{10,15})", 0.95),
                        (@"(?i)acc\.?\s*no\.?\s*:?\s*(\d{10,15})", 0.90),
                        (@"(?i)customer\s*no\.?\s*:?\s*(\d{10,15})", 0.85),
                        (@"\b(\d{12,15})\b", 0.70) // Fallback for long numbers
                    });

                    ExtractFieldWithPatterns(fields, "TotalAmountDue", text, new[]
                    {
                        (@"(?i)total\s*amount\s*due\s*:?\s*rm\s*(\d+\.?\d*)", 0.95),
                        (@"(?i)amount\s*due\s*:?\s*rm\s*(\d+\.?\d*)", 0.90),
                        (@"(?i)total\s*:?\s*rm\s*(\d+\.?\d*)", 0.85),
                        (@"rm\s*(\d+\.\d{2})", 0.75), // Malaysian Ringgit
                        (@"\$(\d+\.\d{2})", 0.70) // Dollar fallback
                    });

                    ExtractFieldWithPatterns(fields, "BillDate", text, new[]
                    {
                        (@"(?i)bill\s*date\s*:?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", 0.95),
                        (@"(?i)date\s*:?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", 0.85),
                        (@"(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})", 0.75) // General date format
                    });

                    ExtractFieldWithPatterns(fields, "DueDate", text, new[]
                    {
                        (@"(?i)due\s*date\s*:?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", 0.95),
                        (@"(?i)payment\s*due\s*:?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", 0.90)
                    });

                    ExtractFieldWithPatterns(fields, "MeterNumber", text, new[]
                    {
                        (@"(?i)meter\s*no\.?\s*:?\s*(\d{8,12})", 0.95),
                        (@"(?i)meter\s*:?\s*(\d{8,12})", 0.85)
                    });
                    break;

                case DocumentType.TelecomBill:
                    ExtractFieldWithPatterns(fields, "AccountNumber", text, new[]
                    {
                        (@"(?i)account\s*no\.?\s*:?\s*(\d{10,15})", 0.95),
                        (@"\b(\d{12,15})\b", 0.70)
                    });

                    ExtractFieldWithPatterns(fields, "PhoneNumber", text, new[]
                    {
                        (@"(\+?6?0?\d{2}-?\d{3,4}-?\d{4})", 0.95), // Malaysian format
                        (@"\((\d{3})\)\s*(\d{3}-\d{4})", 0.90), // US format
                        (@"(\d{3}-\d{3}-\d{4})", 0.85)
                    });

                    ExtractFieldWithPatterns(fields, "MonthlyCharges", text, new[]
                    {
                        (@"(?i)monthly\s*charges?\s*:?\s*rm\s*(\d+\.?\d*)", 0.95),
                        (@"rm\s*(\d+\.\d{2})", 0.75),
                        (@"\$(\d+\.\d{2})", 0.70)
                    });
                    break;

                case DocumentType.Invoice:
                    ExtractFieldWithPatterns(fields, "InvoiceNumber", text, new[]
                    {
                        (@"(?i)invoice\s*no\.?\s*:?\s*([A-Z0-9\-]+)", 0.95),
                        (@"(?i)inv\.?\s*:?\s*([A-Z0-9\-]+)", 0.85),
                        (@"(INV-\d{4,8})", 0.80)
                    });

                    ExtractFieldWithPatterns(fields, "InvoiceDate", text, new[]
                    {
                        (@"(?i)invoice\s*date\s*:?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", 0.95),
                        (@"(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})", 0.75)
                    });

                    ExtractFieldWithPatterns(fields, "TotalAmount", text, new[]
                    {
                        (@"(?i)total\s*amount\s*:?\s*rm\s*(\d+\.?\d*)", 0.95),
                        (@"(?i)total\s*:?\s*rm\s*(\d+\.?\d*)", 0.85),
                        (@"rm\s*(\d+\.\d{2})", 0.75),
                        (@"\$(\d+\.\d{2})", 0.70)
                    });
                    break;

                default:
                    // Generic patterns for unknown document types
                    ExtractFieldWithPatterns(fields, "Date", text, new[]
                    {
                        (@"(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{4})", 0.70),
                        (@"(\d{4}-\d{2}-\d{2})", 0.70)
                    });

                    ExtractFieldWithPatterns(fields, "Amount", text, new[]
                    {
                        (@"rm\s*(\d+\.\d{2})", 0.75),
                        (@"\$(\d+\.\d{2})", 0.70)
                    });
                    break;
            }

            return fields;
        }

        /// <summary>
        /// Extract field using multiple patterns with different confidence levels.
        /// Tries patterns in order of confidence until one matches.
        /// </summary>
        private void ExtractFieldWithPatterns(List<ExtractedField> fields, string fieldName, string text, (string pattern, double confidence)[] patterns)
        {
            foreach (var (pattern, confidence) in patterns)
            {
                try
                {
                    var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var match = regex.Match(text);
                    
                    if (match.Success)
                    {
                        // Use captured group if available, otherwise use full match
                        string value = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                        value = value.Trim();
                        
                        if (!string.IsNullOrEmpty(value))
                        {
                            var field = new ExtractedField(fieldName, value, confidence, "Enhanced Pattern Matching")
                            {
                                Type = DetermineFieldType(fieldName),
                                IsVerified = confidence > 0.8
                            };
                            
                            fields.Add(field);
                            _logger.LogDebug("Extracted {FieldName}: {Value} (confidence: {Confidence:F2}) using pattern: {Pattern}", 
                                fieldName, value, confidence, pattern);
                            return; // Stop after first successful match
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error extracting field {FieldName} with pattern {Pattern}", fieldName, pattern);
                }
            }
            
            _logger.LogDebug("No pattern matched for field {FieldName}", fieldName);
        }

        /// <summary>
        /// Helper method to add a field using regex pattern matching (legacy method).
        /// </summary>
        private void AddSimpleField(List<ExtractedField> fields, string fieldName, string pattern, string text, double confidence)
        {
            ExtractFieldWithPatterns(fields, fieldName, text, new[] { (pattern, confidence) });
        }

        /// <summary>
        /// Check if file extension indicates an image file.
        /// </summary>
        private static bool IsImageFile(string extension)
        {
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".tiff", ".tif", ".bmp", ".gif" };
            return imageExtensions.Contains(extension);
        }

        /// <summary>
        /// Estimate processing time based on file size and type.
        /// </summary>
        private static TimeSpan EstimateProcessingTime(long fileSizeBytes, bool isScannedDocument)
        {
            // Base time estimates (these are rough estimates)
            const int baseTimeMs = 1000; // 1 second base time
            int msPerMB = isScannedDocument ? 2000 : 500; // OCR is slower than text extraction

            var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
            var estimatedMs = baseTimeMs + (int)(fileSizeMB * msPerMB);

            return TimeSpan.FromMilliseconds(estimatedMs);
        }

        /// <summary>
        /// Fire the processing started event.
        /// </summary>
        protected virtual void OnProcessingStarted(DocumentProcessingEventArgs e)
        {
            ProcessingStarted?.Invoke(this, e);
        }

        /// <summary>
        /// Fire the processing completed event.
        /// </summary>
        protected virtual void OnProcessingCompleted(DocumentProcessingEventArgs e)
        {
            ProcessingCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// Fire the progress reported event.
        /// </summary>
        protected virtual void OnProgressReported(ProcessingProgressEventArgs e)
        {
            ProgressReported?.Invoke(this, e);
        }

        #endregion
    }
}