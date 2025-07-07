using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DocumentExtractor.Core.Models
{
    /// <summary>
    /// Represents a strategic mapping rule that automates field mapping based on document patterns.
    /// Rules contain conditions (IF document matches pattern) and actions (THEN apply these mappings).
    /// This enables bulk document processing with intelligent automation.
    /// </summary>
    public class MappingRule
    {
        /// <summary>
        /// Unique identifier for this mapping rule.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// User-friendly name for this rule (e.g., "TNB Monthly Report Mapping").
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what this rule does and when it applies.
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether this rule is currently active and should be applied.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Priority order for rule application (higher numbers = higher priority).
        /// Used for conflict resolution when multiple rules match.
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// When this rule was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this rule was last modified.
        /// </summary>
        public DateTime? LastModifiedDate { get; set; }

        /// <summary>
        /// How many times this rule has been successfully applied.
        /// </summary>
        public int UsageCount { get; set; } = 0;

        /// <summary>
        /// Success rate of this rule (0.0 to 1.0).
        /// Tracks how often the rule produces correct results.
        /// </summary>
        public double SuccessRate { get; set; } = 1.0;

        /// <summary>
        /// Pattern conditions that determine when this rule applies.
        /// </summary>
        public virtual List<RuleCondition> Conditions { get; set; } = new();

        /// <summary>
        /// Field mapping actions to apply when conditions are met.
        /// </summary>
        public virtual List<RuleAction> Actions { get; set; } = new();

        /// <summary>
        /// Record successful application of this rule.
        /// </summary>
        public void RecordSuccess()
        {
            UsageCount++;
            // Update success rate using exponential moving average
            SuccessRate = (SuccessRate * 0.9) + (1.0 * 0.1);
            LastModifiedDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Record failed application of this rule.
        /// </summary>
        public void RecordFailure()
        {
            UsageCount++;
            // Update success rate using exponential moving average
            SuccessRate = (SuccessRate * 0.9) + (0.0 * 0.1);
            LastModifiedDate = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a condition that must be met for a mapping rule to apply.
    /// Conditions use pattern matching to identify document characteristics.
    /// </summary>
    public class RuleCondition
    {
        /// <summary>
        /// Unique identifier for this condition.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The mapping rule this condition belongs to.
        /// </summary>
        [Required]
        public string MappingRuleId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property to the mapping rule.
        /// </summary>
        public virtual MappingRule? MappingRule { get; set; }

        /// <summary>
        /// Type of condition (DocumentSupplier, TemplatePattern, FieldExists, etc.).
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ConditionType { get; set; } = string.Empty;

        /// <summary>
        /// Field or property to evaluate (e.g., "SupplierName", "DocumentType").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Operator for comparison (Equals, Contains, StartsWith, etc.).
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Operator { get; set; } = string.Empty;

        /// <summary>
        /// Value to compare against.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Whether this condition is case-sensitive.
        /// </summary>
        public bool IsCaseSensitive { get; set; } = false;

        /// <summary>
        /// Logical operator to combine with next condition (AND, OR).
        /// </summary>
        [MaxLength(5)]
        public string? LogicalOperator { get; set; }

        /// <summary>
        /// Display order for conditions.
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Group identifier for nested condition logic.
        /// Conditions with the same group are evaluated together.
        /// Example: (Group1: A AND B) OR (Group2: C AND D)
        /// </summary>
        [MaxLength(50)]
        public string? GroupId { get; set; }

        /// <summary>
        /// Nesting level for complex conditional structures.
        /// 0 = root level, 1 = first nested level, etc.
        /// </summary>
        public int NestingLevel { get; set; } = 0;

        /// <summary>
        /// Parent condition ID for hierarchical conditions.
        /// Null for root-level conditions.
        /// </summary>
        [MaxLength(50)]
        public string? ParentConditionId { get; set; }

        /// <summary>
        /// Weight/priority of this condition (0.0 to 1.0).
        /// Higher weights are more important for matching.
        /// </summary>
        public double Weight { get; set; } = 1.0;

        /// <summary>
        /// Whether this condition is required (must be true) or optional.
        /// Optional conditions add weight but don't block rule application.
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// Condition description for user-friendly display.
        /// Example: "Document supplier must be TNB"
        /// </summary>
        [MaxLength(200)]
        public string? Description { get; set; }

        /// <summary>
        /// When this condition was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents an action to take when mapping rule conditions are met.
    /// Actions define how to map extracted fields to template locations.
    /// </summary>
    public class RuleAction
    {
        /// <summary>
        /// Unique identifier for this action.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The mapping rule this action belongs to.
        /// </summary>
        [Required]
        public string MappingRuleId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property to the mapping rule.
        /// </summary>
        public virtual MappingRule? MappingRule { get; set; }

        /// <summary>
        /// Type of action (MapField, TransformField, CombineFields, etc.).
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// Source field name from extracted document.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string SourceFieldName { get; set; } = string.Empty;

        /// <summary>
        /// Target location in template (e.g., "D15", "totalAmount").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string TargetLocation { get; set; } = string.Empty;

        /// <summary>
        /// Type of target location (ExcelCell, PDFField, etc.).
        /// </summary>
        [MaxLength(50)]
        public string TargetLocationType { get; set; } = string.Empty;

        /// <summary>
        /// Transformation to apply to field value (format, convert, etc.).
        /// JSON string containing transformation parameters.
        /// </summary>
        public string? Transformation { get; set; }

        /// <summary>
        /// Default value if source field is empty or missing.
        /// </summary>
        [MaxLength(200)]
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Whether this action is required for successful mapping.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Display order for actions.
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// When this action was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User-friendly description of this action.
        /// </summary>
        [MaxLength(200)]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Types of rule conditions for pattern matching.
    /// </summary>
    public enum RuleConditionType
    {
        DocumentSupplier,    // Match supplier name (TNB, ConEd, etc.)
        DocumentType,        // Match document type (UtilityBill, Invoice, etc.)
        TemplatePattern,     // Match template pattern (E-commerce, Financial, etc.)
        FieldExists,         // Check if specific field exists in extracted data
        FieldValue,          // Check specific field value
        FieldRange,          // Check if field value is in range
        Custom               // Custom condition logic
    }

    /// <summary>
    /// Types of comparison operators for conditions.
    /// </summary>
    public enum RuleOperator
    {
        Equals,              // Exact match
        Contains,            // Contains substring
        StartsWith,          // Starts with text
        EndsWith,            // Ends with text
        GreaterThan,         // Numeric comparison
        LessThan,            // Numeric comparison
        GreaterThanOrEqual,  // Numeric comparison
        LessThanOrEqual,     // Numeric comparison
        Matches,             // Regex pattern match
        NotEquals,           // Not equal to
        IsEmpty,             // Field is empty
        IsNotEmpty,          // Field has value
        Custom               // Custom operator logic
    }

    /// <summary>
    /// Types of actions for field mapping.
    /// </summary>
    public enum RuleActionType
    {
        MapField,            // Direct field mapping
        TransformField,      // Transform field value before mapping
        CombineFields,       // Combine multiple fields into one
        SplitField,          // Split one field into multiple
        CalculateField,      // Calculate value from multiple fields
        ConditionalMap,      // Map based on conditional logic
        Custom               // Custom action logic
    }

    /// <summary>
    /// Types of field transformations.
    /// </summary>
    public enum TransformationType
    {
        FormatCurrency,      // Format as currency (RM 245.67)
        FormatDate,          // Format date (DD/MM/YYYY)
        ConvertCurrency,     // Convert between currencies
        ToUpperCase,         // Convert to uppercase
        ToLowerCase,         // Convert to lowercase
        TitleCase,           // Convert to title case
        AddPrefix,           // Add prefix to value
        AddSuffix,           // Add suffix to value
        RemoveSpaces,        // Remove all spaces
        TrimWhitespace,      // Trim leading/trailing whitespace
        ReplaceText,         // Replace specific text
        RegexReplace,        // Replace using regex
        Custom               // Custom transformation logic
    }

    /// <summary>
    /// Advanced logical operators for complex rule conditions.
    /// </summary>
    public enum AdvancedLogicalOperator
    {
        AND,                 // Standard AND operation
        OR,                  // Standard OR operation
        NOT,                 // Negation operator
        XOR,                 // Exclusive OR
        NAND,                // NOT AND
        NOR,                 // NOT OR
        IF_THEN,             // Conditional implication
        IF_THEN_ELSE,        // Conditional with alternative
        WEIGHTED_OR,         // OR with weight consideration
        MAJORITY,            // True if majority of conditions are true
        ALL_REQUIRED,        // All conditions must be true
        ANY_OPTIONAL         // At least one optional condition true
    }

    /// <summary>
    /// Condition evaluation result with detailed information.
    /// </summary>
    public class ConditionEvaluationResult
    {
        /// <summary>
        /// Whether the condition evaluated to true.
        /// </summary>
        public bool IsTrue { get; set; }

        /// <summary>
        /// Confidence score of the evaluation (0.0 to 1.0).
        /// </summary>
        public double Confidence { get; set; } = 1.0;

        /// <summary>
        /// Weight contribution of this condition.
        /// </summary>
        public double Weight { get; set; } = 1.0;

        /// <summary>
        /// Detailed explanation of why condition passed/failed.
        /// </summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>
        /// Actual value that was evaluated.
        /// </summary>
        public string? ActualValue { get; set; }

        /// <summary>
        /// Expected value for comparison.
        /// </summary>
        public string? ExpectedValue { get; set; }

        /// <summary>
        /// Operator used for evaluation.
        /// </summary>
        public string? Operator { get; set; }

        /// <summary>
        /// Whether this was a required condition.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Execution time for this condition evaluation.
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }
    }

    /// <summary>
    /// Rule evaluation result with comprehensive scoring.
    /// </summary>
    public class RuleEvaluationResult
    {
        /// <summary>
        /// Whether the rule should be applied.
        /// </summary>
        public bool ShouldApply { get; set; }

        /// <summary>
        /// Overall match score (0.0 to 1.0).
        /// </summary>
        public double MatchScore { get; set; } = 0.0;

        /// <summary>
        /// Confidence in the rule application.
        /// </summary>
        public double Confidence { get; set; } = 0.0;

        /// <summary>
        /// Individual condition evaluation results.
        /// </summary>
        public List<ConditionEvaluationResult> ConditionResults { get; set; } = new();

        /// <summary>
        /// Detailed explanation of rule evaluation.
        /// </summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>
        /// Total execution time for rule evaluation.
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Rule that was evaluated.
        /// </summary>
        public MappingRule? Rule { get; set; }

        /// <summary>
        /// Number of required conditions that passed.
        /// </summary>
        public int RequiredConditionsPassed { get; set; }

        /// <summary>
        /// Total number of required conditions.
        /// </summary>
        public int TotalRequiredConditions { get; set; }

        /// <summary>
        /// Number of optional conditions that passed.
        /// </summary>
        public int OptionalConditionsPassed { get; set; }

        /// <summary>
        /// Total number of optional conditions.
        /// </summary>
        public int TotalOptionalConditions { get; set; }
    }
}