using DocumentExtractor.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentExtractor.Core.Interfaces
{
    /// <summary>
    /// Interface for extracting specific data fields from document text.
    /// This is where the "intelligence" happens - finding account numbers, amounts, dates, etc.
    /// 
    /// The field extraction process uses multiple strategies:
    /// 1. Local learned patterns (fastest, most accurate for known suppliers)
    /// 2. Rule-based extraction (general patterns for common fields)
    /// 3. Cloud AI services (Azure Form Recognizer for complex documents)
    /// 4. Statistical analysis (position-based extraction for tables)
    /// 
    /// VB.NET to C# Notes:
    /// - Dictionary<TKey, TValue> is equivalent to VB.NET's Dictionary(Of TKey, TValue)
    /// - Task<T> methods enable async/await pattern for better UI responsiveness
    /// - Nullable reference types (string?) help prevent null reference exceptions
    /// </summary>
    public interface IFieldExtractor
    {
        /// <summary>
        /// Extract all relevant fields from document text.
        /// This is the main method that coordinates all extraction strategies.
        /// 
        /// Extraction Process:
        /// 1. Detect supplier/company from text
        /// 2. Look up learned patterns for this supplier
        /// 3. Apply local patterns first (highest priority)
        /// 4. Apply general rule-based patterns
        /// 5. Use cloud AI for remaining fields
        /// 6. Validate and score all extractions
        /// </summary>
        /// <param name="text">Full document text from OCR</param>
        /// <param name="docType">Detected document type</param>
        /// <param name="supplier">Known supplier (optional - will auto-detect if null)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of extracted fields with confidence scores</returns>
        Task<List<ExtractedField>> ExtractFieldsAsync(
            string text, 
            DocumentType docType, 
            string? supplier = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Detect the supplier/company from document text.
        /// Uses keyword matching, regex patterns, and learned associations.
        /// 
        /// Detection strategies:
        /// - Known company name patterns
        /// - Logo text recognition
        /// - Address matching
        /// - Phone number lookup
        /// - Website/email domain matching
        /// </summary>
        /// <param name="text">Document text to analyze</param>
        /// <param name="docType">Document type (helps narrow search)</param>
        /// <returns>Detected supplier name (standardized)</returns>
        Task<string> DetectSupplierAsync(string text, DocumentType docType);

        /// <summary>
        /// Detect supplier with confidence scores for multiple possibilities.
        /// Useful when supplier detection is ambiguous.
        /// </summary>
        /// <param name="text">Document text to analyze</param>
        /// <param name="docType">Document type</param>
        /// <returns>Dictionary of possible suppliers with confidence scores</returns>
        Task<Dictionary<string, double>> DetectSupplierWithConfidenceAsync(string text, DocumentType docType);

        /// <summary>
        /// Extract specific fields using targeted patterns.
        /// Use this when you know exactly which fields you need.
        /// </summary>
        /// <param name="text">Document text</param>
        /// <param name="fieldNames">Specific fields to extract</param>
        /// <param name="supplier">Known supplier (helps choose patterns)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extracted fields (only requested fields)</returns>
        Task<List<ExtractedField>> ExtractSpecificFieldsAsync(
            string text,
            List<string> fieldNames,
            string? supplier = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Extract fields using only cloud AI services.
        /// Use this when local patterns are insufficient or for training new patterns.
        /// </summary>
        /// <param name="text">Document text</param>
        /// <param name="docType">Document type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cloud-extracted fields</returns>
        Task<List<ExtractedField>> ExtractFieldsUsingCloudAIAsync(
            string text,
            DocumentType docType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the standard field names for a document type.
        /// These are the fields our system attempts to extract for each document type.
        /// </summary>
        /// <param name="docType">Document type</param>
        /// <returns>List of standard field names</returns>
        List<string> GetStandardFieldNames(DocumentType docType);

        /// <summary>
        /// Validate extracted field values for consistency and accuracy.
        /// Checks data types, formats, and business rules.
        /// </summary>
        /// <param name="fields">Fields to validate</param>
        /// <param name="docType">Document type (for validation rules)</param>
        /// <returns>Validation results with corrected values if possible</returns>
        Task<FieldValidationResult> ValidateExtractedFieldsAsync(List<ExtractedField> fields, DocumentType docType);

        /// <summary>
        /// Get extraction statistics for performance monitoring.
        /// </summary>
        /// <returns>Field extraction statistics</returns>
        Task<FieldExtractionStatistics> GetExtractionStatisticsAsync();

        /// <summary>
        /// Event fired when field extraction starts.
        /// </summary>
        event EventHandler<FieldExtractionEventArgs>? ExtractionStarted;

        /// <summary>
        /// Event fired when field extraction completes.
        /// </summary>
        event EventHandler<FieldExtractionEventArgs>? ExtractionCompleted;

        /// <summary>
        /// Event fired when a specific field is extracted.
        /// Useful for real-time UI updates during processing.
        /// </summary>
        event EventHandler<FieldExtractedEventArgs>? FieldExtracted;
    }

    /// <summary>
    /// Standard field names for different document types.
    /// This helps ensure consistency across the application.
    /// </summary>
    public static class StandardFieldNames
    {
        /// <summary>
        /// Common fields found in utility bills.
        /// </summary>
        public static readonly List<string> UtilityBill = new List<string>
        {
            "AccountNumber",
            "CustomerName", 
            "ServiceAddress",
            "BillingAddress",
            "BillDate",
            "DueDate",
            "CurrentCharges",
            "PreviousBalance",
            "TotalAmountDue",
            "UsageAmount",
            "UsagePeriod",
            "MeterNumber",
            "CustomerServicePhone"
        };

        /// <summary>
        /// Common fields found in telecom bills.
        /// </summary>
        public static readonly List<string> TelecomBill = new List<string>
        {
            "AccountNumber",
            "CustomerName",
            "BillingAddress",
            "PhoneNumber",
            "BillDate",
            "DueDate",
            "MonthlyCharges",
            "UsageCharges",
            "TaxesAndFees",
            "TotalAmountDue",
            "PlanName",
            "DataUsage",
            "MinutesUsed"
        };

        /// <summary>
        /// Common fields found in invoices.
        /// </summary>
        public static readonly List<string> Invoice = new List<string>
        {
            "InvoiceNumber",
            "InvoiceDate",
            "DueDate",
            "VendorName",
            "VendorAddress",
            "BillToName",
            "BillToAddress",
            "Subtotal",
            "TaxAmount",
            "TotalAmount",
            "PaymentTerms",
            "PurchaseOrderNumber"
        };

        /// <summary>
        /// Get standard field names for a document type.
        /// </summary>
        /// <param name="docType">Document type</param>
        /// <returns>List of standard field names</returns>
        public static List<string> GetFieldNames(DocumentType docType)
        {
            return docType switch
            {
                DocumentType.UtilityBill => UtilityBill,
                DocumentType.TelecomBill => TelecomBill,
                DocumentType.Invoice => Invoice,
                DocumentType.Receipt => new List<string> { "Date", "Vendor", "Total", "PaymentMethod" },
                DocumentType.MedicalBill => new List<string> { "PatientName", "DateOfService", "Provider", "TotalCharges", "InsurancePayment", "PatientResponsibility" },
                DocumentType.BankStatement => new List<string> { "AccountNumber", "StatementDate", "BeginningBalance", "EndingBalance", "AccountHolder" },
                _ => new List<string>()
            };
        }
    }

    /// <summary>
    /// Result of field validation operation.
    /// </summary>
    public class FieldValidationResult
    {
        /// <summary>
        /// Whether all fields passed validation.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Original fields that were validated.
        /// </summary>
        public List<ExtractedField> OriginalFields { get; set; } = new List<ExtractedField>();

        /// <summary>
        /// Corrected/normalized fields after validation.
        /// </summary>
        public List<ExtractedField> CorrectedFields { get; set; } = new List<ExtractedField>();

        /// <summary>
        /// Validation errors found.
        /// </summary>
        public List<FieldValidationError> Errors { get; set; } = new List<FieldValidationError>();

        /// <summary>
        /// Validation warnings (non-blocking issues).
        /// </summary>
        public List<FieldValidationWarning> Warnings { get; set; } = new List<FieldValidationWarning>();

        /// <summary>
        /// Fields that were automatically corrected.
        /// </summary>
        public List<string> AutoCorrectedFields { get; set; } = new List<string>();
    }

    /// <summary>
    /// Validation error for a specific field.
    /// </summary>
    public class FieldValidationError
    {
        /// <summary>
        /// Name of the field with the error.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Original extracted value.
        /// </summary>
        public string OriginalValue { get; set; } = string.Empty;

        /// <summary>
        /// Error message describing the issue.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Suggested correction (if available).
        /// </summary>
        public string? SuggestedValue { get; set; }

        /// <summary>
        /// Severity of the error.
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
    }

    /// <summary>
    /// Validation warning for a specific field.
    /// </summary>
    public class FieldValidationWarning
    {
        /// <summary>
        /// Name of the field with the warning.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Warning message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Suggested action to address the warning.
        /// </summary>
        public string? Suggestion { get; set; }
    }

    /// <summary>
    /// Severity levels for validation issues.
    /// </summary>
    public enum ValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    /// <summary>
    /// Statistics about field extraction performance.
    /// </summary>
    public class FieldExtractionStatistics
    {
        /// <summary>
        /// Total number of field extractions attempted.
        /// </summary>
        public int TotalExtractions { get; set; }

        /// <summary>
        /// Number of successful extractions.
        /// </summary>
        public int SuccessfulExtractions { get; set; }

        /// <summary>
        /// Success rate (0.0 to 1.0).
        /// </summary>
        public double SuccessRate => TotalExtractions > 0 ? (double)SuccessfulExtractions / TotalExtractions : 0.0;

        /// <summary>
        /// Average confidence score for successful extractions.
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Statistics by extraction method.
        /// </summary>
        public Dictionary<string, ExtractionMethodStats> MethodStats { get; set; } = new Dictionary<string, ExtractionMethodStats>();

        /// <summary>
        /// Statistics by field type.
        /// </summary>
        public Dictionary<string, FieldTypeStats> FieldStats { get; set; } = new Dictionary<string, FieldTypeStats>();

        /// <summary>
        /// Most commonly extracted suppliers.
        /// </summary>
        public Dictionary<string, int> SupplierStats { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// When these statistics were last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistics for a specific extraction method.
    /// </summary>
    public class ExtractionMethodStats
    {
        /// <summary>
        /// Method name (e.g., "LocalPattern", "CloudAI").
        /// </summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>
        /// Number of extractions using this method.
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// Success rate for this method.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average confidence score.
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Average processing time in milliseconds.
        /// </summary>
        public double AverageProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Statistics for a specific field type.
    /// </summary>
    public class FieldTypeStats
    {
        /// <summary>
        /// Field name (e.g., "AccountNumber", "TotalAmount").
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Number of extraction attempts for this field.
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Number of successful extractions.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Success rate for this field type.
        /// </summary>
        public double SuccessRate => AttemptCount > 0 ? (double)SuccessCount / AttemptCount : 0.0;

        /// <summary>
        /// Average confidence for successful extractions.
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Most common extraction method for this field.
        /// </summary>
        public string? PreferredMethod { get; set; }
    }

    /// <summary>
    /// Event arguments for field extraction events.
    /// </summary>
    public class FieldExtractionEventArgs : EventArgs
    {
        /// <summary>
        /// Document type being processed.
        /// </summary>
        public DocumentType DocumentType { get; set; }

        /// <summary>
        /// Supplier name (if detected).
        /// </summary>
        public string? Supplier { get; set; }

        /// <summary>
        /// Number of fields to extract.
        /// </summary>
        public int FieldCount { get; set; }

        /// <summary>
        /// Processing start time.
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Processing end time (if completed).
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
    }

    /// <summary>
    /// Event arguments for individual field extraction.
    /// </summary>
    public class FieldExtractedEventArgs : EventArgs
    {
        /// <summary>
        /// The extracted field.
        /// </summary>
        public ExtractedField Field { get; set; } = new ExtractedField();

        /// <summary>
        /// Extraction method used.
        /// </summary>
        public string ExtractionMethod { get; set; } = string.Empty;

        /// <summary>
        /// Time taken to extract this field.
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
    }
}