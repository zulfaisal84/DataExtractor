using System;
using System.ComponentModel;

namespace DocumentExtractor.Core.Models
{
    /// <summary>
    /// Types of documents our system can process.
    /// This enum helps determine which extraction patterns and fields to look for.
    /// 
    /// VB.NET to C# Notes:
    /// - C# enums don't require explicit integer values unless you want to specify them
    /// - Description attribute can be used for user-friendly display names
    /// - Enum values are accessed with dot notation: DocumentType.UtilityBill
    /// </summary>
    public enum DocumentType
    {
        [Description("Unknown Document Type")]
        Unknown = 0,

        [Description("Utility Bill")]
        UtilityBill = 1,

        [Description("Telecommunications Bill")]
        TelecomBill = 2,

        [Description("Invoice")]
        Invoice = 3,

        [Description("Receipt")]
        Receipt = 4,

        [Description("Medical Bill")]
        MedicalBill = 5,

        [Description("Bank Statement")]
        BankStatement = 6,

        [Description("Insurance Document")]
        InsuranceDocument = 7,

        [Description("Tax Document")]
        TaxDocument = 8,

        [Description("Legal Document")]
        LegalDocument = 9,

        [Description("Contract")]
        Contract = 10
    }

    /// <summary>
    /// Types of data fields we can extract from documents.
    /// This helps with validation, formatting, and user interface design.
    /// </summary>
    public enum FieldType
    {
        [Description("Text")]
        Text = 0,

        [Description("Number")]
        Number = 1,

        [Description("Currency Amount")]
        Currency = 2,

        [Description("Date")]
        Date = 3,

        [Description("Phone Number")]
        PhoneNumber = 4,

        [Description("Email Address")]
        Email = 5,

        [Description("Account Number")]
        AccountNumber = 6,

        [Description("Address")]
        Address = 7,

        [Description("Percentage")]
        Percentage = 8,

        [Description("URL/Website")]
        Url = 9,

        [Description("Tax ID/SSN")]
        TaxId = 10,

        [Description("Boolean (Yes/No)")]
        Boolean = 11
    }

    /// <summary>
    /// Current processing status of a document.
    /// This enables batch processing and progress tracking.
    /// </summary>
    public enum ProcessingStatus
    {
        [Description("Pending Processing")]
        Pending = 0,

        [Description("Currently Processing")]
        Processing = 1,

        [Description("Processing Completed")]
        Completed = 2,

        [Description("Processing Failed")]
        Failed = 3,

        [Description("Needs Manual Review")]
        NeedsReview = 4,

        [Description("Cancelled")]
        Cancelled = 5,

        [Description("Skipped")]
        Skipped = 6
    }

    /// <summary>
    /// Source of field extraction - helps track effectiveness of different methods.
    /// This information is crucial for improving our machine learning patterns.
    /// </summary>
    public enum ExtractionSource
    {
        [Description("Unknown Source")]
        Unknown = 0,

        [Description("Local Pattern Match")]
        LocalPattern = 1,

        [Description("Azure Form Recognizer")]
        AzureFormRecognizer = 2,

        [Description("Tesseract OCR")]
        TesseractOCR = 3,

        [Description("User Manual Entry")]
        UserManual = 4,

        [Description("User Correction")]
        UserCorrection = 5,

        [Description("Machine Learning Model")]
        MachineLearning = 6,

        [Description("Rule-Based Extraction")]
        RuleBased = 7
    }

    /// <summary>
    /// Priority levels for document processing.
    /// Higher priority documents are processed first in batch operations.
    /// </summary>
    public enum ProcessingPriority
    {
        [Description("Low Priority")]
        Low = 0,

        [Description("Normal Priority")]
        Normal = 1,

        [Description("High Priority")]
        High = 2,

        [Description("Urgent Priority")]
        Urgent = 3,

        [Description("Critical Priority")]
        Critical = 4
    }

    /// <summary>
    /// Confidence levels for user interface display.
    /// Makes it easier to show meaningful confidence indicators to users.
    /// </summary>
    public enum ConfidenceLevel
    {
        [Description("Very Low Confidence")]
        VeryLow = 0,    // 0.0 - 0.2

        [Description("Low Confidence")]
        Low = 1,        // 0.2 - 0.4

        [Description("Medium Confidence")]
        Medium = 2,     // 0.4 - 0.6

        [Description("High Confidence")]
        High = 3,       // 0.6 - 0.8

        [Description("Very High Confidence")]
        VeryHigh = 4    // 0.8 - 1.0
    }

    /// <summary>
    /// Static helper class for working with enumerations.
    /// Demonstrates static classes and extension-like functionality in C#.
    /// </summary>
    public static class EnumHelper
    {
        /// <summary>
        /// Get the description attribute value for an enum value.
        /// This is useful for displaying user-friendly names in the UI.
        /// 
        /// C# Note: This method uses reflection to get attribute values.
        /// The generic constraint 'where T : Enum' ensures only enum types can be used.
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="enumValue">The enum value to get description for</param>
        /// <returns>Description or enum name if no description attribute found</returns>
        public static string GetDescription<T>(T enumValue) where T : Enum
        {
            // Get the field info for this enum value using reflection
            var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
            
            if (fieldInfo == null)
                return enumValue.ToString();

            // Look for DescriptionAttribute on this field
            var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
            
            // Return description if found, otherwise return the enum name
            return attributes.Length > 0 ? attributes[0].Description : enumValue.ToString();
        }

        /// <summary>
        /// Convert a confidence score (0.0 to 1.0) to a confidence level enum.
        /// This makes it easier to display meaningful confidence indicators in the UI.
        /// </summary>
        /// <param name="confidence">Confidence score between 0.0 and 1.0</param>
        /// <returns>Corresponding confidence level</returns>
        public static ConfidenceLevel GetConfidenceLevel(double confidence)
        {
            // C# switch expression (available in C# 8.0+)
            // This is more concise than traditional switch statements
            return confidence switch
            {
                >= 0.8 => ConfidenceLevel.VeryHigh,
                >= 0.6 => ConfidenceLevel.High,
                >= 0.4 => ConfidenceLevel.Medium,
                >= 0.2 => ConfidenceLevel.Low,
                _ => ConfidenceLevel.VeryLow
            };
        }

        /// <summary>
        /// Get a user-friendly color name for confidence level display.
        /// This helps with UI styling and visual feedback.
        /// </summary>
        /// <param name="confidence">Confidence score</param>
        /// <returns>Color name for UI styling</returns>
        public static string GetConfidenceColor(double confidence)
        {
            var level = GetConfidenceLevel(confidence);
            
            return level switch
            {
                ConfidenceLevel.VeryHigh => "Green",
                ConfidenceLevel.High => "LightGreen",
                ConfidenceLevel.Medium => "Orange",
                ConfidenceLevel.Low => "Red",
                ConfidenceLevel.VeryLow => "DarkRed",
                _ => "Gray"
            };
        }

        /// <summary>
        /// Get all enum values as a list.
        /// Useful for populating dropdowns and selection lists in the UI.
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <returns>List of all enum values</returns>
        public static List<T> GetAllValues<T>() where T : Enum
        {
            // Enum.GetValues returns Array, we convert to List<T> for easier usage
            return Enum.GetValues(typeof(T)).Cast<T>().ToList();
        }

        /// <summary>
        /// Get all enum values with their descriptions as key-value pairs.
        /// Perfect for binding to UI controls like ComboBox or DropDown.
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <returns>Dictionary with enum values as keys and descriptions as values</returns>
        public static Dictionary<T, string> GetValueDescriptionPairs<T>() where T : Enum
        {
            var result = new Dictionary<T, string>();
            
            foreach (T value in GetAllValues<T>())
            {
                result[value] = GetDescription(value);
            }
            
            return result;
        }
    }
}