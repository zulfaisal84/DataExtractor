using DocumentExtractor.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentExtractor.Core.Interfaces
{
    /// <summary>
    /// Interface for exporting extracted document data to various formats.
    /// This service handles creating Excel spreadsheets, CSV files, JSON exports, and reports.
    /// 
    /// Key Features:
    /// - Multiple export formats (Excel, CSV, JSON, PDF reports)
    /// - Customizable templates and formatting
    /// - Batch export capabilities
    /// - Data filtering and sorting
    /// - Professional report generation
    /// 
    /// VB.NET to C# Notes:
    /// - Stream handling is very similar between languages
    /// - Using statements ensure proper resource disposal
    /// - Async file operations improve UI responsiveness
    /// </summary>
    public interface IDataExporter
    {
        /// <summary>
        /// Export documents to Excel spreadsheet with professional formatting.
        /// Creates a comprehensive spreadsheet with multiple sheets and pivot tables.
        /// 
        /// Excel Export Features:
        /// - Summary sheet with statistics
        /// - Detail sheet with all extracted fields
        /// - Pivot tables for analysis
        /// - Charts for visual representation
        /// - Professional formatting and styling
        /// </summary>
        /// <param name="documents">Documents to export</param>
        /// <param name="outputPath">Full path for the Excel file</param>
        /// <param name="options">Export customization options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result with file info</returns>
        Task<ExportResult> ExportToExcelAsync(
            List<ExtractedDocument> documents, 
            string outputPath,
            ExcelExportOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Export documents to CSV format.
        /// Returns CSV data as byte array for flexible handling.
        /// </summary>
        /// <param name="documents">Documents to export</param>
        /// <param name="options">CSV export options</param>
        /// <returns>CSV data as byte array</returns>
        Task<byte[]> ExportToCsvAsync(List<ExtractedDocument> documents, CsvExportOptions? options = null);

        /// <summary>
        /// Export documents to JSON format.
        /// Useful for API integration and data interchange.
        /// </summary>
        /// <param name="documents">Documents to export</param>
        /// <param name="options">JSON export options</param>
        /// <returns>JSON string</returns>
        Task<string> ExportToJsonAsync(List<ExtractedDocument> documents, JsonExportOptions? options = null);

        /// <summary>
        /// Generate a professional PDF report from extracted documents.
        /// Creates a formatted report suitable for clients or management.
        /// </summary>
        /// <param name="documents">Documents to include in report</param>
        /// <param name="outputPath">Full path for the PDF file</param>
        /// <param name="options">Report generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result</returns>
        Task<ExportResult> GeneratePdfReportAsync(
            List<ExtractedDocument> documents,
            string outputPath,
            PdfReportOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Export to a custom template format.
        /// Allows users to define their own export templates.
        /// </summary>
        /// <param name="documents">Documents to export</param>
        /// <param name="templatePath">Path to template file</param>
        /// <param name="outputPath">Output file path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result</returns>
        Task<ExportResult> ExportToTemplateAsync(
            List<ExtractedDocument> documents,
            string templatePath,
            string outputPath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available export formats.
        /// </summary>
        /// <returns>List of supported export formats</returns>
        List<ExportFormat> GetSupportedFormats();

        /// <summary>
        /// Get available export templates.
        /// </summary>
        /// <returns>List of available templates</returns>
        Task<List<ExportTemplate>> GetAvailableTemplatesAsync();

        /// <summary>
        /// Create a custom export template.
        /// </summary>
        /// <param name="template">Template definition</param>
        /// <returns>True if template was created successfully</returns>
        Task<bool> CreateTemplateAsync(ExportTemplate template);

        /// <summary>
        /// Validate export options before performing export.
        /// </summary>
        /// <param name="documents">Documents to validate</param>
        /// <param name="format">Export format</param>
        /// <param name="outputPath">Destination path</param>
        /// <returns>Validation result</returns>
        Task<ExportValidationResult> ValidateExportAsync(List<ExtractedDocument> documents, ExportFormat format, string outputPath);

        /// <summary>
        /// Event fired when export starts.
        /// </summary>
        event EventHandler<ExportEventArgs>? ExportStarted;

        /// <summary>
        /// Event fired when export completes.
        /// </summary>
        event EventHandler<ExportEventArgs>? ExportCompleted;

        /// <summary>
        /// Event fired to report export progress.
        /// </summary>
        event EventHandler<ExportProgressEventArgs>? ExportProgress;
    }

    /// <summary>
    /// Result of an export operation.
    /// </summary>
    public class ExportResult
    {
        /// <summary>
        /// Whether export was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Path to the exported file.
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>
        /// Size of the exported file in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Number of documents exported.
        /// </summary>
        public int DocumentCount { get; set; }

        /// <summary>
        /// Number of fields exported.
        /// </summary>
        public int FieldCount { get; set; }

        /// <summary>
        /// Time taken for export in milliseconds.
        /// </summary>
        public int ProcessingTimeMs { get; set; }

        /// <summary>
        /// Export format used.
        /// </summary>
        public ExportFormat Format { get; set; }

        /// <summary>
        /// Any warnings generated during export.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Error message if export failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Additional metadata about the export.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Supported export formats.
    /// </summary>
    public enum ExportFormat
    {
        Excel = 0,
        Csv = 1,
        Json = 2,
        PdfReport = 3,
        XmlData = 4,
        CustomTemplate = 5
    }

    /// <summary>
    /// Options for Excel export.
    /// </summary>
    public class ExcelExportOptions
    {
        /// <summary>
        /// Include summary sheet with statistics.
        /// </summary>
        public bool IncludeSummarySheet { get; set; } = true;

        /// <summary>
        /// Include pivot tables for data analysis.
        /// </summary>
        public bool IncludePivotTables { get; set; } = true;

        /// <summary>
        /// Include charts and visualizations.
        /// </summary>
        public bool IncludeCharts { get; set; } = true;

        /// <summary>
        /// Apply professional formatting.
        /// </summary>
        public bool ApplyFormatting { get; set; } = true;

        /// <summary>
        /// Fields to include in export (null = all fields).
        /// </summary>
        public List<string>? FieldsToInclude { get; set; }

        /// <summary>
        /// Fields to exclude from export.
        /// </summary>
        public List<string> FieldsToExclude { get; set; } = new List<string>();

        /// <summary>
        /// Group documents by supplier.
        /// </summary>
        public bool GroupBySupplier { get; set; } = false;

        /// <summary>
        /// Group documents by document type.
        /// </summary>
        public bool GroupByDocumentType { get; set; } = false;

        /// <summary>
        /// Include confidence scores.
        /// </summary>
        public bool IncludeConfidenceScores { get; set; } = true;

        /// <summary>
        /// Include processing metadata.
        /// </summary>
        public bool IncludeMetadata { get; set; } = false;

        /// <summary>
        /// Company name for report header.
        /// </summary>
        public string? CompanyName { get; set; }

        /// <summary>
        /// Report title.
        /// </summary>
        public string ReportTitle { get; set; } = "Document Extraction Report";

        /// <summary>
        /// Date range for filtering (start date).
        /// </summary>
        public DateTime? DateRangeStart { get; set; }

        /// <summary>
        /// Date range for filtering (end date).
        /// </summary>
        public DateTime? DateRangeEnd { get; set; }
    }

    /// <summary>
    /// Options for CSV export.
    /// </summary>
    public class CsvExportOptions
    {
        /// <summary>
        /// Field delimiter (default: comma).
        /// </summary>
        public string Delimiter { get; set; } = ",";

        /// <summary>
        /// Text qualifier (default: double quote).
        /// </summary>
        public string TextQualifier { get; set; } = "\"";

        /// <summary>
        /// Include header row.
        /// </summary>
        public bool IncludeHeaders { get; set; } = true;

        /// <summary>
        /// Fields to include (null = all fields).
        /// </summary>
        public List<string>? FieldsToInclude { get; set; }

        /// <summary>
        /// Fields to exclude.
        /// </summary>
        public List<string> FieldsToExclude { get; set; } = new List<string>();

        /// <summary>
        /// Include confidence scores.
        /// </summary>
        public bool IncludeConfidenceScores { get; set; } = true;

        /// <summary>
        /// Date format for date fields.
        /// </summary>
        public string DateFormat { get; set; } = "yyyy-MM-dd";

        /// <summary>
        /// Number format for numeric fields.
        /// </summary>
        public string NumberFormat { get; set; } = "F2";

        /// <summary>
        /// Text encoding.
        /// </summary>
        public System.Text.Encoding Encoding { get; set; } = System.Text.Encoding.UTF8;
    }

    /// <summary>
    /// Options for JSON export.
    /// </summary>
    public class JsonExportOptions
    {
        /// <summary>
        /// Pretty-print JSON (indented formatting).
        /// </summary>
        public bool PrettyPrint { get; set; } = true;

        /// <summary>
        /// Include null values in output.
        /// </summary>
        public bool IncludeNullValues { get; set; } = false;

        /// <summary>
        /// Fields to include (null = all fields).
        /// </summary>
        public List<string>? FieldsToInclude { get; set; }

        /// <summary>
        /// Fields to exclude.
        /// </summary>
        public List<string> FieldsToExclude { get; set; } = new List<string>();

        /// <summary>
        /// Include processing metadata.
        /// </summary>
        public bool IncludeMetadata { get; set; } = true;

        /// <summary>
        /// Include confidence scores.
        /// </summary>
        public bool IncludeConfidenceScores { get; set; } = true;

        /// <summary>
        /// Group documents by supplier.
        /// </summary>
        public bool GroupBySupplier { get; set; } = false;

        /// <summary>
        /// Date format for JSON serialization.
        /// </summary>
        public string DateFormat { get; set; } = "yyyy-MM-ddTHH:mm:ssZ";
    }

    /// <summary>
    /// Options for PDF report generation.
    /// </summary>
    public class PdfReportOptions
    {
        /// <summary>
        /// Report title.
        /// </summary>
        public string Title { get; set; } = "Document Extraction Report";

        /// <summary>
        /// Company name for header.
        /// </summary>
        public string? CompanyName { get; set; }

        /// <summary>
        /// Company logo path.
        /// </summary>
        public string? LogoPath { get; set; }

        /// <summary>
        /// Include executive summary.
        /// </summary>
        public bool IncludeExecutiveSummary { get; set; } = true;

        /// <summary>
        /// Include detailed document listings.
        /// </summary>
        public bool IncludeDocumentDetails { get; set; } = true;

        /// <summary>
        /// Include charts and graphs.
        /// </summary>
        public bool IncludeCharts { get; set; } = true;

        /// <summary>
        /// Include confidence analysis.
        /// </summary>
        public bool IncludeConfidenceAnalysis { get; set; } = true;

        /// <summary>
        /// Group documents by supplier.
        /// </summary>
        public bool GroupBySupplier { get; set; } = true;

        /// <summary>
        /// Page orientation.
        /// </summary>
        public PageOrientation PageOrientation { get; set; } = PageOrientation.Portrait;

        /// <summary>
        /// Page size.
        /// </summary>
        public PageSize PageSize { get; set; } = PageSize.Letter;

        /// <summary>
        /// Fields to include in detailed listings.
        /// </summary>
        public List<string>? FieldsToInclude { get; set; }

        /// <summary>
        /// Report footer text.
        /// </summary>
        public string? FooterText { get; set; }
    }

    /// <summary>
    /// Page orientation options.
    /// </summary>
    public enum PageOrientation
    {
        Portrait = 0,
        Landscape = 1
    }

    /// <summary>
    /// Page size options.
    /// </summary>
    public enum PageSize
    {
        Letter = 0,
        Legal = 1,
        A4 = 2,
        A3 = 3
    }

    /// <summary>
    /// Export template definition.
    /// </summary>
    public class ExportTemplate
    {
        /// <summary>
        /// Template ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Template name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Template description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Template file path.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Supported export format.
        /// </summary>
        public ExportFormat Format { get; set; }

        /// <summary>
        /// Template variables/placeholders.
        /// </summary>
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// When template was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Template author.
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Template version.
        /// </summary>
        public string Version { get; set; } = "1.0";
    }

    /// <summary>
    /// Export validation result.
    /// </summary>
    public class ExportValidationResult
    {
        /// <summary>
        /// Whether export can proceed.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Validation errors.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Validation warnings.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Estimated file size in bytes.
        /// </summary>
        public long EstimatedFileSizeBytes { get; set; }

        /// <summary>
        /// Estimated processing time.
        /// </summary>
        public TimeSpan EstimatedProcessingTime { get; set; }

        /// <summary>
        /// Number of documents to export.
        /// </summary>
        public int DocumentCount { get; set; }

        /// <summary>
        /// Number of fields to export.
        /// </summary>
        public int FieldCount { get; set; }
    }

    /// <summary>
    /// Event arguments for export events.
    /// </summary>
    public class ExportEventArgs : EventArgs
    {
        /// <summary>
        /// Export format.
        /// </summary>
        public ExportFormat Format { get; set; }

        /// <summary>
        /// Number of documents being exported.
        /// </summary>
        public int DocumentCount { get; set; }

        /// <summary>
        /// Output file path.
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>
        /// Export start time.
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Export end time (if completed).
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Whether export was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if export failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Event arguments for export progress.
    /// </summary>
    public class ExportProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Current progress (0.0 to 1.0).
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Current processing phase.
        /// </summary>
        public string Phase { get; set; } = string.Empty;

        /// <summary>
        /// Documents processed so far.
        /// </summary>
        public int ProcessedDocuments { get; set; }

        /// <summary>
        /// Total documents to process.
        /// </summary>
        public int TotalDocuments { get; set; }

        /// <summary>
        /// Additional status message.
        /// </summary>
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Estimated time remaining.
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }
}