using DocumentExtractor.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocumentExtractor.Services.Interfaces
{
    /// <summary>
    /// Interface for the strategic mapping rule engine.
    /// Handles evaluation and application of mapping rules for automated field mapping.
    /// </summary>
    public interface IRuleEngine
    {
        /// <summary>
        /// Find and apply mapping rules that match the given document pattern and template.
        /// This is the core method for strategic automated mapping.
        /// </summary>
        /// <param name="documentPattern">Document characteristics for pattern matching</param>
        /// <param name="templateId">Target template ID</param>
        /// <param name="extractedFields">Fields extracted from the document</param>
        /// <returns>Applied field mappings or empty list if no rules match</returns>
        Task<List<TemplateFieldMapping>> ApplyMappingRulesAsync(
            DocumentPattern documentPattern, 
            string templateId,
            List<ExtractedField> extractedFields);

        /// <summary>
        /// Find all mapping rules that match the given document pattern.
        /// Returns rules ordered by priority (highest first).
        /// </summary>
        /// <param name="documentPattern">Document characteristics for pattern matching</param>
        /// <returns>Matching rules ordered by priority</returns>
        Task<List<MappingRule>> FindMatchingRulesAsync(DocumentPattern documentPattern);

        /// <summary>
        /// Evaluate whether a specific rule matches the given document pattern.
        /// </summary>
        /// <param name="rule">Rule to evaluate</param>
        /// <param name="documentPattern">Document characteristics</param>
        /// <returns>True if rule conditions are met</returns>
        Task<bool> EvaluateRuleAsync(MappingRule rule, DocumentPattern documentPattern);

        /// <summary>
        /// Advanced rule evaluation with detailed scoring and condition analysis.
        /// Supports complex logical operators, weighted conditions, and confidence scoring.
        /// </summary>
        /// <param name="rule">Rule to evaluate</param>
        /// <param name="documentPattern">Document characteristics</param>
        /// <returns>Detailed evaluation result with scoring and explanations</returns>
        Task<RuleEvaluationResult> EvaluateRuleAdvancedAsync(MappingRule rule, DocumentPattern documentPattern);

        /// <summary>
        /// Test a rule with preview mode - shows what would happen without applying.
        /// </summary>
        /// <param name="rule">Rule to test</param>
        /// <param name="documentPattern">Document pattern to test against</param>
        /// <param name="extractedFields">Fields that would be mapped</param>
        /// <returns>Preview of rule application results</returns>
        Task<RuleTestResult> TestRuleAsync(MappingRule rule, DocumentPattern documentPattern, List<ExtractedField> extractedFields);

        /// <summary>
        /// Create a new mapping rule from current field mappings.
        /// This is called when user clicks "Save as rule" checkbox.
        /// </summary>
        /// <param name="ruleName">User-friendly name for the rule</param>
        /// <param name="description">Rule description</param>
        /// <param name="documentPattern">Document pattern conditions</param>
        /// <param name="fieldMappings">Field mappings to include in the rule</param>
        /// <returns>Created mapping rule</returns>
        Task<MappingRule> CreateRuleFromMappingsAsync(
            string ruleName, 
            string description,
            DocumentPattern documentPattern, 
            List<TemplateFieldMapping> fieldMappings);

        /// <summary>
        /// Record successful application of a mapping rule.
        /// Updates rule success rate and usage statistics.
        /// </summary>
        /// <param name="ruleId">Rule that was successfully applied</param>
        Task RecordRuleSuccessAsync(string ruleId);

        /// <summary>
        /// Record failed application of a mapping rule.
        /// Updates rule success rate for machine learning improvement.
        /// </summary>
        /// <param name="ruleId">Rule that failed to apply correctly</param>
        Task RecordRuleFailureAsync(string ruleId);

        /// <summary>
        /// Get all active mapping rules ordered by priority and success rate.
        /// </summary>
        /// <returns>Active mapping rules</returns>
        Task<List<MappingRule>> GetActiveRulesAsync();

        /// <summary>
        /// Get rule statistics for monitoring and optimization.
        /// </summary>
        /// <returns>Rule engine statistics</returns>
        Task<RuleEngineStatistics> GetStatisticsAsync();

        /// <summary>
        /// Activate a mapping rule to include it in rule evaluation.
        /// </summary>
        /// <param name="ruleId">Rule to activate</param>
        Task ActivateRuleAsync(string ruleId);

        /// <summary>
        /// Deactivate a mapping rule to exclude it from rule evaluation.
        /// </summary>
        /// <param name="ruleId">Rule to deactivate</param>
        Task DeactivateRuleAsync(string ruleId);

        /// <summary>
        /// Toggle rule activation status.
        /// </summary>
        /// <param name="ruleId">Rule to toggle</param>
        /// <returns>New activation status</returns>
        Task<bool> ToggleRuleActivationAsync(string ruleId);

        /// <summary>
        /// Update rule priority for conflict resolution.
        /// </summary>
        /// <param name="ruleId">Rule to update</param>
        /// <param name="newPriority">New priority value (higher = more important)</param>
        Task UpdateRulePriorityAsync(string ruleId, int newPriority);

        /// <summary>
        /// Delete a mapping rule permanently.
        /// </summary>
        /// <param name="ruleId">Rule to delete</param>
        Task DeleteRuleAsync(string ruleId);
    }

    /// <summary>
    /// Represents document characteristics used for pattern matching.
    /// Contains all the information needed to evaluate rule conditions.
    /// </summary>
    public class DocumentPattern
    {
        /// <summary>
        /// Document supplier/company name (e.g., "TNB", "ConEd", "Verizon").
        /// </summary>
        public string? SupplierName { get; set; }

        /// <summary>
        /// Type of document (e.g., "UtilityBill", "Invoice", "TelecomBill").
        /// </summary>
        public string? DocumentType { get; set; }

        /// <summary>
        /// Template pattern detected (e.g., "E-commerce", "Financial", "Invoice").
        /// </summary>
        public string? TemplatePattern { get; set; }

        /// <summary>
        /// Template category (e.g., "Monthly Reports", "Client Reports").
        /// </summary>
        public string? TemplateCategory { get; set; }

        /// <summary>
        /// List of field names found in the extracted document.
        /// Used for FieldExists conditions.
        /// </summary>
        public List<string> AvailableFields { get; set; } = new();

        /// <summary>
        /// Key-value pairs of field values for pattern matching.
        /// Used for FieldValue conditions.
        /// </summary>
        public Dictionary<string, string> FieldValues { get; set; } = new();

        /// <summary>
        /// Additional metadata for custom pattern matching.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// When this pattern was created (for tracking).
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistics about rule engine performance and usage.
    /// </summary>
    public class RuleEngineStatistics
    {
        /// <summary>
        /// Total number of active mapping rules.
        /// </summary>
        public int TotalActiveRules { get; set; }

        /// <summary>
        /// Total number of rule applications.
        /// </summary>
        public int TotalRuleApplications { get; set; }

        /// <summary>
        /// Overall success rate across all rules.
        /// </summary>
        public double OverallSuccessRate { get; set; }

        /// <summary>
        /// Average rule execution time in milliseconds.
        /// </summary>
        public double AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// Most frequently used rules.
        /// </summary>
        public List<RuleUsageStatistic> TopRules { get; set; } = new();

        /// <summary>
        /// Rule performance by pattern type.
        /// </summary>
        public Dictionary<string, double> SuccessRateByPattern { get; set; } = new();

        /// <summary>
        /// When these statistics were generated.
        /// </summary>
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Usage statistics for individual mapping rules.
    /// </summary>
    public class RuleUsageStatistic
    {
        /// <summary>
        /// Rule ID.
        /// </summary>
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Rule name.
        /// </summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Number of times this rule has been used.
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// Success rate for this rule.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// When this rule was last used.
        /// </summary>
        public DateTime? LastUsed { get; set; }
    }

    /// <summary>
    /// Result of testing a rule in preview mode.
    /// </summary>
    public class RuleTestResult
    {
        /// <summary>
        /// Rule evaluation result.
        /// </summary>
        public RuleEvaluationResult EvaluationResult { get; set; } = new();

        /// <summary>
        /// Field mappings that would be created.
        /// </summary>
        public List<TemplateFieldMapping> PreviewMappings { get; set; } = new();

        /// <summary>
        /// Fields that would be successfully mapped.
        /// </summary>
        public List<ExtractedField> MappedFields { get; set; } = new();

        /// <summary>
        /// Fields that could not be mapped.
        /// </summary>
        public List<ExtractedField> UnmappedFields { get; set; } = new();

        /// <summary>
        /// Warnings or issues found during testing.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Success rate prediction for this rule application.
        /// </summary>
        public double PredictedSuccessRate { get; set; }

        /// <summary>
        /// Estimated confidence of successful application.
        /// </summary>
        public double EstimatedConfidence { get; set; }

        /// <summary>
        /// Human-readable summary of test results.
        /// </summary>
        public string Summary { get; set; } = string.Empty;
    }
}