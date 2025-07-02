using DocumentExtractor.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentExtractor.Core.Interfaces
{
    /// <summary>
    /// Interface for the pattern learning system - the "intelligence" that gets smarter over time.
    /// This is the core of our competitive advantage: local learning that improves accuracy.
    /// 
    /// The pattern learning system:
    /// 1. Stores successful extraction patterns locally
    /// 2. Learns from user corrections to improve patterns
    /// 3. Adapts to layout changes from suppliers
    /// 4. Prioritizes patterns based on success rates
    /// 5. Generates new patterns automatically from user feedback
    /// 
    /// VB.NET to C# Notes:
    /// - Generic constraints (where T : class) work similarly to VB.NET
    /// - Async/await pattern is nearly identical
    /// - LINQ methods have slightly different syntax but same functionality
    /// </summary>
    public interface IPatternLearning
    {
        /// <summary>
        /// Try to extract fields using locally learned patterns.
        /// This is the first extraction method tried - fastest and most accurate for known suppliers.
        /// 
        /// Process:
        /// 1. Find all patterns for this supplier
        /// 2. Sort by success rate and priority
        /// 3. Try each pattern until successful extraction
        /// 4. Update pattern statistics
        /// 5. Return best results with confidence scores
        /// </summary>
        /// <param name="text">Document text to extract from</param>
        /// <param name="supplier">Supplier name (must match pattern records)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of extracted fields using local patterns</returns>
        Task<List<ExtractedField>> TryLocalPatternsAsync(
            string text, 
            string supplier,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Try to extract specific fields using learned patterns.
        /// Use this when you only need certain fields, not all available patterns.
        /// </summary>
        /// <param name="text">Document text</param>
        /// <param name="supplier">Supplier name</param>
        /// <param name="fieldNames">Specific fields to extract</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extracted fields (only requested ones)</returns>
        Task<List<ExtractedField>> TryLocalPatternsForFieldsAsync(
            string text,
            string supplier,
            List<string> fieldNames,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Learn from a user correction to improve future extractions.
        /// This is called when users manually correct extracted values.
        /// 
        /// Learning Process:
        /// 1. Analyze the original text context around the field
        /// 2. Compare original extraction with corrected value
        /// 3. Generate or improve regex pattern for this field
        /// 4. Test new pattern against historical data
        /// 5. Store improved pattern with higher priority
        /// </summary>
        /// <param name="supplier">Supplier name</param>
        /// <param name="fieldName">Field that was corrected</param>
        /// <param name="originalText">Full document text</param>
        /// <param name="originalValue">What we originally extracted (incorrect)</param>
        /// <param name="correctValue">User's correction (ground truth)</param>
        /// <returns>Information about the learning result</returns>
        Task<PatternLearningResult> LearnFromCorrectionAsync(
            string supplier,
            string fieldName,
            string originalText,
            string originalValue,
            string correctValue);

        /// <summary>
        /// Learn from a successful extraction to reinforce good patterns.
        /// Called when users confirm that an extraction is correct.
        /// </summary>
        /// <param name="supplier">Supplier name</param>
        /// <param name="fieldName">Field that was correct</param>
        /// <param name="originalText">Full document text</param>
        /// <param name="extractedValue">Value we extracted (confirmed correct)</param>
        /// <param name="patternUsed">Pattern ID that was used</param>
        /// <returns>Learning result</returns>
        Task<PatternLearningResult> LearnFromSuccessAsync(
            string supplier,
            string fieldName,
            string originalText,
            string extractedValue,
            string patternUsed);

        /// <summary>
        /// Get current accuracy for patterns of a specific supplier and field.
        /// Helps users understand how reliable extractions are.
        /// </summary>
        /// <param name="supplier">Supplier name</param>
        /// <param name="fieldName">Field name</param>
        /// <returns>Accuracy score (0.0 to 1.0)</returns>
        Task<double> GetPatternAccuracyAsync(string supplier, string fieldName);

        /// <summary>
        /// Get all learned patterns for a supplier.
        /// Useful for pattern management and debugging.
        /// </summary>
        /// <param name="supplier">Supplier name</param>
        /// <returns>List of learned patterns</returns>
        Task<List<LearnedPattern>> GetPatternsForSupplierAsync(string supplier);

        /// <summary>
        /// Get patterns for a specific field across all suppliers.
        /// Useful for understanding how different suppliers format the same field.
        /// </summary>
        /// <param name="fieldName">Field name</param>
        /// <returns>List of patterns for this field</returns>
        Task<List<LearnedPattern>> GetPatternsForFieldAsync(string fieldName);

        /// <summary>
        /// Delete or deactivate a pattern that's performing poorly.
        /// This helps clean up patterns that are no longer working.
        /// </summary>
        /// <param name="patternId">Pattern ID to remove</param>
        /// <param name="deactivateOnly">If true, deactivate instead of delete (preserves history)</param>
        /// <returns>True if pattern was removed/deactivated successfully</returns>
        Task<bool> RemovePatternAsync(string patternId, bool deactivateOnly = true);

        /// <summary>
        /// Manually add a pattern (for expert users or initial seeding).
        /// This allows power users to create patterns directly.
        /// </summary>
        /// <param name="pattern">Pattern to add</param>
        /// <returns>True if pattern was added successfully</returns>
        Task<bool> AddPatternAsync(LearnedPattern pattern);

        /// <summary>
        /// Test a pattern against historical data to validate it.
        /// Use this before promoting a new pattern to active status.
        /// </summary>
        /// <param name="pattern">Pattern to test</param>
        /// <param name="testTexts">Historical document texts to test against</param>
        /// <returns>Test results with accuracy metrics</returns>
        Task<PatternTestResult> TestPatternAsync(LearnedPattern pattern, List<string> testTexts);

        /// <summary>
        /// Get statistics about the pattern learning system performance.
        /// </summary>
        /// <returns>Learning system statistics</returns>
        Task<PatternLearningStatistics> GetLearningStatisticsAsync();

        /// <summary>
        /// Export patterns for backup or sharing.
        /// Useful for deploying patterns across multiple installations.
        /// </summary>
        /// <param name="supplier">Specific supplier (optional - exports all if null)</param>
        /// <returns>JSON string containing patterns</returns>
        Task<string> ExportPatternsAsync(string? supplier = null);

        /// <summary>
        /// Import patterns from backup or another installation.
        /// </summary>
        /// <param name="patternsJson">JSON string containing patterns</param>
        /// <param name="mergeStrategy">How to handle conflicts with existing patterns</param>
        /// <returns>Import result with statistics</returns>
        Task<PatternImportResult> ImportPatternsAsync(string patternsJson, PatternMergeStrategy mergeStrategy = PatternMergeStrategy.SkipExisting);

        /// <summary>
        /// Automatically generate initial patterns for a new supplier.
        /// Uses heuristics and common patterns to create a starting point.
        /// </summary>
        /// <param name="supplier">Supplier name</param>
        /// <param name="documentType">Type of documents from this supplier</param>
        /// <param name="sampleTexts">Sample document texts for pattern generation</param>
        /// <returns>List of generated patterns</returns>
        Task<List<LearnedPattern>> GenerateInitialPatternsAsync(
            string supplier, 
            DocumentType documentType, 
            List<string> sampleTexts);

        /// <summary>
        /// Event fired when a new pattern is learned.
        /// </summary>
        event EventHandler<PatternLearnedEventArgs>? PatternLearned;

        /// <summary>
        /// Event fired when a pattern is improved.
        /// </summary>
        event EventHandler<PatternImprovedEventArgs>? PatternImproved;

        /// <summary>
        /// Event fired when pattern accuracy changes significantly.
        /// </summary>
        event EventHandler<PatternAccuracyChangedEventArgs>? PatternAccuracyChanged;
    }

    /// <summary>
    /// Result of a pattern learning operation.
    /// </summary>
    public class PatternLearningResult
    {
        /// <summary>
        /// Whether learning was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Type of learning that occurred.
        /// </summary>
        public PatternLearningType LearningType { get; set; }

        /// <summary>
        /// The pattern that was created or modified.
        /// </summary>
        public LearnedPattern? Pattern { get; set; }

        /// <summary>
        /// Previous accuracy before learning.
        /// </summary>
        public double PreviousAccuracy { get; set; }

        /// <summary>
        /// New accuracy after learning.
        /// </summary>
        public double NewAccuracy { get; set; }

        /// <summary>
        /// Improvement in accuracy (NewAccuracy - PreviousAccuracy).
        /// </summary>
        public double AccuracyImprovement => NewAccuracy - PreviousAccuracy;

        /// <summary>
        /// Explanation of what was learned.
        /// </summary>
        public string? Explanation { get; set; }

        /// <summary>
        /// Any warnings or issues during learning.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Whether this learning should be reviewed by a user.
        /// </summary>
        public bool RequiresReview { get; set; } = false;
    }

    /// <summary>
    /// Types of pattern learning that can occur.
    /// </summary>
    public enum PatternLearningType
    {
        NewPattern = 0,         // Completely new pattern created
        PatternImproved = 1,    // Existing pattern was refined
        PatternReinforced = 2,  // Existing pattern was confirmed/strengthened
        PatternCorrected = 3,   // Pattern was fixed due to error
        PatternGenerated = 4,   // Pattern auto-generated from heuristics
        PatternMerged = 5       // Multiple patterns were combined
    }

    /// <summary>
    /// Result of testing a pattern against historical data.
    /// </summary>
    public class PatternTestResult
    {
        /// <summary>
        /// Total number of texts tested.
        /// </summary>
        public int TotalTests { get; set; }

        /// <summary>
        /// Number of successful matches.
        /// </summary>
        public int SuccessfulMatches { get; set; }

        /// <summary>
        /// Success rate (0.0 to 1.0).
        /// </summary>
        public double SuccessRate => TotalTests > 0 ? (double)SuccessfulMatches / TotalTests : 0.0;

        /// <summary>
        /// Average confidence score for successful matches.
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Examples of successful matches.
        /// </summary>
        public List<PatternMatchExample> SuccessExamples { get; set; } = new List<PatternMatchExample>();

        /// <summary>
        /// Examples of failed matches (for debugging).
        /// </summary>
        public List<PatternMatchExample> FailureExamples { get; set; } = new List<PatternMatchExample>();

        /// <summary>
        /// Recommendation for this pattern.
        /// </summary>
        public PatternRecommendation Recommendation { get; set; }

        /// <summary>
        /// Explanation of the recommendation.
        /// </summary>
        public string? RecommendationReason { get; set; }
    }

    /// <summary>
    /// Example of a pattern match (successful or failed).
    /// </summary>
    public class PatternMatchExample
    {
        /// <summary>
        /// Source text that was tested.
        /// </summary>
        public string SourceText { get; set; } = string.Empty;

        /// <summary>
        /// Whether the pattern matched.
        /// </summary>
        public bool Matched { get; set; }

        /// <summary>
        /// Extracted value (if matched).
        /// </summary>
        public string? ExtractedValue { get; set; }

        /// <summary>
        /// Confidence score (if matched).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Error message (if failed).
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Recommendations for pattern usage.
    /// </summary>
    public enum PatternRecommendation
    {
        Approve = 0,        // Pattern works well, use it
        Review = 1,         // Pattern needs human review
        Improve = 2,        // Pattern needs refinement
        Reject = 3,         // Pattern doesn't work, don't use
        Merge = 4           // Similar pattern exists, consider merging
    }

    /// <summary>
    /// Statistics about the pattern learning system.
    /// </summary>
    public class PatternLearningStatistics
    {
        /// <summary>
        /// Total number of patterns learned.
        /// </summary>
        public int TotalPatterns { get; set; }

        /// <summary>
        /// Number of active patterns.
        /// </summary>
        public int ActivePatterns { get; set; }

        /// <summary>
        /// Number of suppliers with learned patterns.
        /// </summary>
        public int SuppliersWithPatterns { get; set; }

        /// <summary>
        /// Average pattern accuracy across all patterns.
        /// </summary>
        public double AveragePatternAccuracy { get; set; }

        /// <summary>
        /// Number of user corrections processed.
        /// </summary>
        public int UserCorrectionsProcessed { get; set; }

        /// <summary>
        /// Patterns by supplier (supplier name -> pattern count).
        /// </summary>
        public Dictionary<string, int> PatternsBySupplier { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Patterns by field type (field name -> pattern count).
        /// </summary>
        public Dictionary<string, int> PatternsByField { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Recent learning activity.
        /// </summary>
        public List<RecentLearningActivity> RecentActivity { get; set; } = new List<RecentLearningActivity>();

        /// <summary>
        /// When these statistics were generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Recent learning activity item.
    /// </summary>
    public class RecentLearningActivity
    {
        /// <summary>
        /// When the learning occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Type of learning activity.
        /// </summary>
        public PatternLearningType ActivityType { get; set; }

        /// <summary>
        /// Supplier involved.
        /// </summary>
        public string Supplier { get; set; } = string.Empty;

        /// <summary>
        /// Field involved.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Brief description of the activity.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Accuracy improvement achieved.
        /// </summary>
        public double AccuracyImprovement { get; set; }
    }

    /// <summary>
    /// Strategies for merging imported patterns with existing ones.
    /// </summary>
    public enum PatternMergeStrategy
    {
        SkipExisting = 0,       // Skip patterns that already exist
        OverwriteExisting = 1,  // Replace existing patterns
        MergeByAccuracy = 2,    // Keep pattern with higher accuracy
        CreateNewVersion = 3    // Create new version of existing patterns
    }

    /// <summary>
    /// Result of importing patterns.
    /// </summary>
    public class PatternImportResult
    {
        /// <summary>
        /// Whether import was successful overall.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Total patterns in import data.
        /// </summary>
        public int TotalPatterns { get; set; }

        /// <summary>
        /// Patterns successfully imported.
        /// </summary>
        public int ImportedPatterns { get; set; }

        /// <summary>
        /// Patterns skipped due to conflicts.
        /// </summary>
        public int SkippedPatterns { get; set; }

        /// <summary>
        /// Patterns that failed to import.
        /// </summary>
        public int FailedPatterns { get; set; }

        /// <summary>
        /// Detailed messages about import process.
        /// </summary>
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>
        /// Errors encountered during import.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Event arguments for pattern learning events.
    /// </summary>
    public class PatternLearnedEventArgs : EventArgs
    {
        /// <summary>
        /// The pattern that was learned.
        /// </summary>
        public LearnedPattern Pattern { get; set; } = new LearnedPattern();

        /// <summary>
        /// Type of learning that occurred.
        /// </summary>
        public PatternLearningType LearningType { get; set; }

        /// <summary>
        /// Learning result details.
        /// </summary>
        public PatternLearningResult Result { get; set; } = new PatternLearningResult();
    }

    /// <summary>
    /// Event arguments for pattern improvement events.
    /// </summary>
    public class PatternImprovedEventArgs : EventArgs
    {
        /// <summary>
        /// The improved pattern.
        /// </summary>
        public LearnedPattern ImprovedPattern { get; set; } = new LearnedPattern();

        /// <summary>
        /// The original pattern (before improvement).
        /// </summary>
        public LearnedPattern? OriginalPattern { get; set; }

        /// <summary>
        /// Accuracy improvement achieved.
        /// </summary>
        public double AccuracyImprovement { get; set; }

        /// <summary>
        /// Explanation of the improvement.
        /// </summary>
        public string? ImprovementDescription { get; set; }
    }

    /// <summary>
    /// Event arguments for pattern accuracy changes.
    /// </summary>
    public class PatternAccuracyChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Supplier name.
        /// </summary>
        public string Supplier { get; set; } = string.Empty;

        /// <summary>
        /// Field name.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Previous accuracy.
        /// </summary>
        public double PreviousAccuracy { get; set; }

        /// <summary>
        /// New accuracy.
        /// </summary>
        public double NewAccuracy { get; set; }

        /// <summary>
        /// Change in accuracy.
        /// </summary>
        public double AccuracyChange => NewAccuracy - PreviousAccuracy;

        /// <summary>
        /// Whether this is a significant change (threshold-based).
        /// </summary>
        public bool IsSignificantChange { get; set; }
    }
}