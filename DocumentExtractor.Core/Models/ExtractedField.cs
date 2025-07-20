using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentExtractor.Core.Models
{
    /// <summary>
    /// Represents a single piece of data extracted from a document.
    /// Each field has a name (like "AccountNumber"), value (like "1234567890"), 
    /// and confidence score indicating how certain we are about the extraction.
    /// 
    /// VB.NET to C# Notes:
    /// - C# uses [Attribute] syntax instead of <Attribute()>
    /// - Data annotations help with validation and database mapping
    /// - String interpolation uses $ prefix: $"text {variable}" instead of VB's String.Format
    /// </summary>
    public class ExtractedField
    {
        /// <summary>
        /// Unique identifier for this field extraction.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The name/type of this field (e.g., "AccountNumber", "TotalAmount", "DueDate").
        /// This should be consistent across all documents of the same type.
        /// 
        /// Required attribute ensures this field must have a value.
        /// MaxLength prevents extremely long field names.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// The actual extracted value (e.g., "1234567890", "$245.67", "2024-01-15").
        /// This is the raw extracted text before any formatting or validation.
        /// </summary>
        [MaxLength(1000)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score from 0.0 (no confidence) to 1.0 (completely confident).
        /// This helps users identify which extractions might need manual verification.
        /// 
        /// Range attribute ensures the value stays within valid bounds.
        /// </summary>
        [Range(0.0, 1.0)]
        public double Confidence { get; set; } = 0.0;

        /// <summary>
        /// The data type of this field - affects validation and formatting.
        /// </summary>
        public FieldType Type { get; set; } = FieldType.Text;

        /// <summary>
        /// How this field was extracted: local pattern, cloud AI, or user correction.
        /// This helps track the effectiveness of different extraction methods.
        /// </summary>
        [MaxLength(50)]
        public string Source { get; set; } = "Unknown";

        /// <summary>
        /// The bounding box coordinates where this field was found in the document.
        /// Format: "x,y,width,height" - useful for highlighting fields in UI.
        /// Null if position information is not available.
        /// </summary>
        public string? BoundingBox { get; set; }

        /// <summary>
        /// The position and size of this field in the document
        /// Used by the hybrid AI architecture for visual feedback and highlighting
        /// </summary>
        public FieldPosition? Position { get; set; }

        /// <summary>
        /// Alias for Type property to maintain compatibility with hybrid services
        /// The data type of this field - affects validation and formatting
        /// </summary>
        public FieldType FieldType 
        { 
            get => Type; 
            set => Type = value; 
        }

        /// <summary>
        /// Alias for Source property to maintain compatibility with hybrid services
        /// How this field was extracted: local pattern, cloud AI, or user correction
        /// </summary>
        [MaxLength(50)]
        public string ExtractionMethod 
        { 
            get => Source; 
            set => Source = value; 
        }

        /// <summary>
        /// When this field was extracted.
        /// </summary>
        public DateTime ExtractedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The original text context around this field (for debugging).
        /// Example: If we extracted "1234567890" from "Account Number: 1234567890",
        /// this would store the full context string.
        /// </summary>
        [MaxLength(500)]
        public string? OriginalContext { get; set; }

        /// <summary>
        /// Whether this field has been manually verified/corrected by a user.
        /// Fields marked as verified should have higher confidence in future processing.
        /// </summary>
        public bool IsVerified { get; set; } = false;

        /// <summary>
        /// If this field was corrected by a user, store the original extracted value.
        /// This helps improve our extraction patterns through machine learning.
        /// </summary>
        [MaxLength(1000)]
        public string? OriginalValue { get; set; }

        /// <summary>
        /// Default constructor.
        /// C# Note: Parameterless constructors are implicit if no other constructors are defined.
        /// </summary>
        public ExtractedField()
        {
            // All initialization is done via property initializers above
        }

        /// <summary>
        /// Constructor for creating a field with basic information.
        /// Demonstrates constructor overloading in C#.
        /// </summary>
        /// <param name="fieldName">Name of the field (e.g., "AccountNumber")</param>
        /// <param name="value">Extracted value</param>
        /// <param name="confidence">Confidence score (0.0 to 1.0)</param>
        /// <param name="source">Extraction source</param>
        public ExtractedField(string fieldName, string value, double confidence, string source = "Unknown")
        {
            FieldName = fieldName;
            Value = value;
            Confidence = Math.Max(0.0, Math.Min(1.0, confidence)); // Clamp between 0 and 1
            Source = source;
            
            // Try to determine field type automatically based on value
            Type = DetermineFieldType(value);
        }

        /// <summary>
        /// Constructor for creating a field with position information
        /// Used by the hybrid AI architecture
        /// </summary>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="value">Extracted value</param>
        /// <param name="confidence">Confidence score</param>
        /// <param name="position">Field position in document</param>
        /// <param name="source">Extraction source</param>
        public ExtractedField(string fieldName, string value, double confidence, FieldPosition position, string source = "Unknown") 
            : this(fieldName, value, confidence, source)
        {
            Position = position;
            BoundingBox = position?.ToBoundingBox();
        }

        /// <summary>
        /// Update the position and automatically sync with BoundingBox
        /// </summary>
        /// <param name="position">New position</param>
        public void UpdatePosition(FieldPosition? position)
        {
            Position = position;
            BoundingBox = position?.ToBoundingBox();
        }

        /// <summary>
        /// Update from bounding box string and automatically create Position
        /// </summary>
        /// <param name="boundingBox">Bounding box string</param>
        public void UpdateFromBoundingBox(string? boundingBox)
        {
            BoundingBox = boundingBox;
            Position = FieldPosition.FromBoundingBox(boundingBox);
        }

        /// <summary>
        /// Attempts to automatically determine the field type based on the value.
        /// This method demonstrates pattern matching and regular expressions in C#.
        /// </summary>
        /// <param name="value">The field value to analyze</param>
        /// <returns>The most likely field type</returns>
        private static FieldType DetermineFieldType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return FieldType.Text;

            // Check for currency patterns (starts with $ or ends with currency symbols)
            if (value.StartsWith("$") || value.Contains("USD") || value.Contains("€") || value.Contains("£"))
                return FieldType.Currency;

            // Check for date patterns (various formats)
            if (DateTime.TryParse(value, out _))
                return FieldType.Date;

            // Check for phone number patterns
            if (IsPhoneNumber(value))
                return FieldType.PhoneNumber;

            // Check for email patterns
            if (value.Contains("@") && value.Contains("."))
                return FieldType.Email;

            // Check if it's purely numeric
            if (double.TryParse(value.Replace(",", ""), out _))
                return FieldType.Number;

            // Default to text
            return FieldType.Text;
        }

        /// <summary>
        /// Helper method to detect phone number patterns.
        /// Demonstrates basic regular expression usage in C#.
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <returns>True if value appears to be a phone number</returns>
        private static bool IsPhoneNumber(string value)
        {
            // Remove common phone number formatting characters
            string cleanValue = value.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Replace(".", "");
            
            // Check if remaining characters are digits and length is reasonable for phone numbers
            return cleanValue.All(char.IsDigit) && cleanValue.Length >= 10 && cleanValue.Length <= 15;
        }

        /// <summary>
        /// Format the field value according to its type.
        /// This method demonstrates switch expressions (C# 8.0+ feature).
        /// </summary>
        /// <returns>Formatted value string</returns>
        public string GetFormattedValue()
        {
            return Type switch
            {
                // Switch expressions are more concise than traditional switch statements
                // VB.NET equivalent would be Select Case with return statements
                FieldType.Currency => FormatCurrency(Value),
                FieldType.Date => FormatDate(Value),
                FieldType.PhoneNumber => FormatPhoneNumber(Value),
                FieldType.Number => FormatNumber(Value),
                _ => Value // Default case using discard pattern _
            };
        }

        /// <summary>
        /// Format currency values consistently.
        /// </summary>
        private static string FormatCurrency(string value)
        {
            // Try to parse as decimal and format properly
            string cleanValue = value.Replace("$", "").Replace(",", "").Trim();
            if (decimal.TryParse(cleanValue, out decimal amount))
            {
                return amount.ToString("C"); // C format specifier for currency
            }
            return value; // Return original if parsing fails
        }

        /// <summary>
        /// Format date values consistently.
        /// </summary>
        private static string FormatDate(string value)
        {
            if (DateTime.TryParse(value, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd"); // ISO 8601 format
            }
            return value;
        }

        /// <summary>
        /// Format phone numbers consistently.
        /// </summary>
        private static string FormatPhoneNumber(string value)
        {
            string cleanValue = value.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Replace(".", "");
            if (cleanValue.Length == 10)
            {
                return $"({cleanValue.Substring(0, 3)}) {cleanValue.Substring(3, 3)}-{cleanValue.Substring(6, 4)}";
            }
            return value;
        }

        /// <summary>
        /// Format numeric values with appropriate decimal places.
        /// </summary>
        private static string FormatNumber(string value)
        {
            string cleanValue = value.Replace(",", "");
            if (double.TryParse(cleanValue, out double number))
            {
                // Format with commas for thousands separator
                return number.ToString("N2");
            }
            return value;
        }

        /// <summary>
        /// Override ToString for better debugging and logging.
        /// C# Note: override keyword is used instead of VB.NET's Overrides.
        /// </summary>
        /// <returns>String representation of this field</returns>
        public override string ToString()
        {
            return $"{FieldName}: {Value} (Confidence: {Confidence:P1}, Source: {Source})";
        }
    }
}