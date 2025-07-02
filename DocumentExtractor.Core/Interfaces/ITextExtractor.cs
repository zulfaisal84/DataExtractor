using DocumentExtractor.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentExtractor.Core.Interfaces
{
    /// <summary>
    /// Interface for extracting text from various document formats.
    /// This service handles OCR operations and document type detection.
    /// 
    /// Supports:
    /// - PDF files (both text-based and scanned)
    /// - Image files (JPG, PNG, TIFF, etc.)
    /// - Multi-page documents
    /// - Various image preprocessing for better OCR accuracy
    /// 
    /// VB.NET to C# Notes:
    /// - byte[] is equivalent to VB.NET's Byte()
    /// - Stream handling is very similar between VB.NET and C#
    /// - Using statements for IDisposable resources work the same way
    /// </summary>
    public interface ITextExtractor
    {
        /// <summary>
        /// Extract text from a document file.
        /// Automatically detects file type and applies appropriate extraction method.
        /// 
        /// For PDF files:
        /// - First tries to extract text directly (for text-based PDFs)
        /// - Falls back to OCR if no text is found (for scanned PDFs)
        /// 
        /// For image files:
        /// - Applies image preprocessing (noise reduction, contrast enhancement)
        /// - Uses Tesseract OCR for text extraction
        /// </summary>
        /// <param name="filePath">Path to the document file</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>Extracted text content</returns>
        Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extract text from a document provided as byte array.
        /// Useful when document is already loaded in memory or comes from a stream.
        /// </summary>
        /// <param name="documentBytes">Document content as byte array</param>
        /// <param name="fileName">Original filename (used to determine format)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extracted text content</returns>
        Task<string> ExtractTextAsync(byte[] documentBytes, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extract text from a document stream.
        /// Useful for processing documents from web uploads or other streams.
        /// </summary>
        /// <param name="documentStream">Stream containing document data</param>
        /// <param name="fileName">Original filename (used to determine format)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extracted text content</returns>
        Task<string> ExtractTextAsync(Stream documentStream, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extract text with detailed metadata about the extraction process.
        /// Returns additional information like confidence scores, processing time, etc.
        /// </summary>
        /// <param name="filePath">Path to the document file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed extraction result</returns>
        Task<TextExtractionResult> ExtractTextWithMetadataAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Detect the document type based on extracted text content.
        /// Uses pattern matching and keyword analysis to classify documents.
        /// 
        /// Detection methods:
        /// - Keyword analysis (utility companies, medical terms, etc.)
        /// - Layout pattern recognition
        /// - Header/footer analysis
        /// - Statistical text analysis
        /// </summary>
        /// <param name="text">Extracted text to analyze</param>
        /// <returns>Detected document type</returns>
        Task<DocumentType> DetectDocumentTypeAsync(string text);

        /// <summary>
        /// Detect document type with confidence scores for each possible type.
        /// Useful when multiple document types are possible.
        /// </summary>
        /// <param name="text">Extracted text to analyze</param>
        /// <returns>Dictionary of document types with confidence scores</returns>
        Task<Dictionary<DocumentType, double>> DetectDocumentTypeWithConfidenceAsync(string text);

        /// <summary>
        /// Get list of supported file formats for text extraction.
        /// </summary>
        /// <returns>List of supported file extensions</returns>
        List<string> GetSupportedFormats();

        /// <summary>
        /// Check if a file format is supported for text extraction.
        /// </summary>
        /// <param name="fileName">Filename or extension to check</param>
        /// <returns>True if format is supported</returns>
        bool IsFormatSupported(string fileName);

        /// <summary>
        /// Preprocess an image to improve OCR accuracy.
        /// Applies various image enhancement techniques.
        /// </summary>
        /// <param name="imagePath">Path to image file</param>
        /// <returns>Path to preprocessed image</returns>
        Task<string> PreprocessImageAsync(string imagePath);

        /// <summary>
        /// Get OCR engine information and status.
        /// Useful for diagnostics and troubleshooting.
        /// </summary>
        /// <returns>OCR engine information</returns>
        Task<OcrEngineInfo> GetOcrEngineInfoAsync();

        /// <summary>
        /// Event fired when text extraction starts.
        /// </summary>
        event EventHandler<TextExtractionEventArgs>? ExtractionStarted;

        /// <summary>
        /// Event fired when text extraction completes.
        /// </summary>
        event EventHandler<TextExtractionEventArgs>? ExtractionCompleted;

        /// <summary>
        /// Event fired to report extraction progress.
        /// </summary>
        event EventHandler<TextExtractionProgressEventArgs>? ExtractionProgress;
    }

    /// <summary>
    /// Detailed result from text extraction operation.
    /// Contains text content plus metadata about the extraction process.
    /// </summary>
    public class TextExtractionResult
    {
        /// <summary>
        /// The extracted text content.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Overall confidence score for the text extraction (0.0 to 1.0).
        /// </summary>
        public double Confidence { get; set; } = 0.0;

        /// <summary>
        /// Time taken to extract text in milliseconds.
        /// </summary>
        public int ProcessingTimeMs { get; set; } = 0;

        /// <summary>
        /// Method used for text extraction.
        /// </summary>
        public TextExtractionMethod Method { get; set; } = TextExtractionMethod.Unknown;

        /// <summary>
        /// Number of pages processed (for multi-page documents).
        /// </summary>
        public int PageCount { get; set; } = 1;

        /// <summary>
        /// Text extracted from each page separately.
        /// Useful for page-specific processing.
        /// </summary>
        public List<PageTextResult> PageResults { get; set; } = new List<PageTextResult>();

        /// <summary>
        /// Detected language of the text.
        /// </summary>
        public string? DetectedLanguage { get; set; }

        /// <summary>
        /// Any warnings or issues encountered during extraction.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Whether the document appears to be a scanned image vs. text-based.
        /// </summary>
        public bool IsScannedDocument { get; set; } = false;

        /// <summary>
        /// Image preprocessing steps applied (if any).
        /// </summary>
        public List<string> PreprocessingSteps { get; set; } = new List<string>();

        /// <summary>
        /// OCR engine version used.
        /// </summary>
        public string? OcrEngineVersion { get; set; }
    }

    /// <summary>
    /// Text extraction result for a single page.
    /// </summary>
    public class PageTextResult
    {
        /// <summary>
        /// Page number (1-based).
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Text extracted from this page.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score for this page.
        /// </summary>
        public double Confidence { get; set; } = 0.0;

        /// <summary>
        /// Word-level confidence scores and positions.
        /// Useful for highlighting low-confidence areas.
        /// </summary>
        public List<WordResult>? WordResults { get; set; }
    }

    /// <summary>
    /// Individual word extraction result with position and confidence.
    /// </summary>
    public class WordResult
    {
        /// <summary>
        /// The extracted word.
        /// </summary>
        public string Word { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score for this word.
        /// </summary>
        public double Confidence { get; set; } = 0.0;

        /// <summary>
        /// Bounding box for this word (x, y, width, height).
        /// </summary>
        public Rectangle BoundingBox { get; set; } = new Rectangle();
    }

    /// <summary>
    /// Simple rectangle structure for bounding boxes.
    /// </summary>
    public class Rectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectangle() { }

        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return $"{X},{Y},{Width},{Height}";
        }
    }

    /// <summary>
    /// Methods used for text extraction.
    /// </summary>
    public enum TextExtractionMethod
    {
        Unknown = 0,
        DirectPdfText = 1,      // Text extracted directly from PDF
        TesseractOCR = 2,       // OCR using Tesseract
        AzureOCR = 3,           // Azure Computer Vision OCR
        HybridOCR = 4,          // Combination of methods
        ManualEntry = 5         // Manually entered text
    }

    /// <summary>
    /// Information about the OCR engine.
    /// </summary>
    public class OcrEngineInfo
    {
        /// <summary>
        /// OCR engine name (e.g., "Tesseract").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// OCR engine version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Supported languages.
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new List<string>();

        /// <summary>
        /// Whether the OCR engine is available and working.
        /// </summary>
        public bool IsAvailable { get; set; } = false;

        /// <summary>
        /// Path to OCR engine installation.
        /// </summary>
        public string? InstallationPath { get; set; }

        /// <summary>
        /// Any configuration or setup issues.
        /// </summary>
        public List<string> Issues { get; set; } = new List<string>();
    }

    /// <summary>
    /// Event arguments for text extraction events.
    /// </summary>
    public class TextExtractionEventArgs : EventArgs
    {
        /// <summary>
        /// Path to the document being processed.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Extraction start time.
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Extraction end time (if completed).
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Whether extraction was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if extraction failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Extraction method used.
        /// </summary>
        public TextExtractionMethod Method { get; set; } = TextExtractionMethod.Unknown;
    }

    /// <summary>
    /// Event arguments for text extraction progress.
    /// </summary>
    public class TextExtractionProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Current progress percentage (0.0 to 1.0).
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Current processing phase.
        /// </summary>
        public string Phase { get; set; } = string.Empty;

        /// <summary>
        /// Current page being processed (for multi-page documents).
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Total number of pages.
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Additional status message.
        /// </summary>
        public string? StatusMessage { get; set; }
    }
}