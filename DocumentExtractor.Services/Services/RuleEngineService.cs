using DocumentExtractor.Core.Models;
using DocumentExtractor.Data.Context;
using DocumentExtractor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocumentExtractor.Services.Services
{
    /// <summary>
    /// Strategic mapping rule engine implementation.
    /// Handles evaluation and application of mapping rules for automated field mapping.
    /// 
    /// Core Features:
    /// - Pattern-based rule matching (IF document matches pattern THEN apply mappings)
    /// - Priority-based rule resolution for conflicts
    /// - Success rate tracking and machine learning
    /// - Performance monitoring and optimization
    /// </summary>
    public class RuleEngineService : IRuleEngine
    {
        private readonly DocumentExtractionContext _context;

        public RuleEngineService(DocumentExtractionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Find and apply mapping rules that match the given document pattern and template.
        /// This is the core method for strategic automated mapping.
        /// </summary>
        public async Task<List<TemplateFieldMapping>> ApplyMappingRulesAsync(
            DocumentPattern documentPattern, 
            string templateId,
            List<ExtractedField> extractedFields)
        {
            var stopwatch = Stopwatch.StartNew();
            var appliedMappings = new List<TemplateFieldMapping>();

            try
            {
                Console.WriteLine($"🎯 RULE ENGINE: Applying rules for pattern: {documentPattern.SupplierName} | {documentPattern.DocumentType}");

                // 1. Find matching rules ordered by priority
                var matchingRules = await FindMatchingRulesAsync(documentPattern);
                
                if (!matchingRules.Any())
                {
                    Console.WriteLine("🎯 RULE ENGINE: No matching rules found - using manual mapping");
                    return appliedMappings;
                }

                Console.WriteLine($"🎯 RULE ENGINE: Found {matchingRules.Count} matching rules");

                // 2. Apply rules in priority order (highest first)
                foreach (var rule in matchingRules)
                {
                    try
                    {
                        var ruleMappings = await ApplySingleRuleAsync(rule, templateId, extractedFields);
                        appliedMappings.AddRange(ruleMappings);
                        
                        // Record successful application
                        await RecordRuleSuccessAsync(rule.Id);
                        
                        Console.WriteLine($"✅ RULE ENGINE: Applied rule '{rule.Name}' - {ruleMappings.Count} mappings");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ RULE ENGINE: Failed to apply rule '{rule.Name}': {ex.Message}");
                        await RecordRuleFailureAsync(rule.Id);
                    }
                }

                stopwatch.Stop();
                Console.WriteLine($"🎯 RULE ENGINE: Completed in {stopwatch.ElapsedMilliseconds}ms - {appliedMappings.Count} total mappings");

                return appliedMappings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Fatal error applying rules: {ex.Message}");
                return appliedMappings;
            }
        }

        /// <summary>
        /// Apply a single mapping rule to generate field mappings.
        /// </summary>
        private async Task<List<TemplateFieldMapping>> ApplySingleRuleAsync(
            MappingRule rule, 
            string templateId, 
            List<ExtractedField> extractedFields)
        {
            var mappings = new List<TemplateFieldMapping>();

            // Get rule actions
            var actions = await _context.RuleActions
                .Where(a => a.MappingRuleId == rule.Id)
                .OrderBy(a => a.DisplayOrder)
                .ToListAsync();

            foreach (var action in actions)
            {
                // Find matching extracted field
                var sourceField = extractedFields.FirstOrDefault(f => 
                    string.Equals(f.FieldName, action.SourceFieldName, StringComparison.OrdinalIgnoreCase));

                if (sourceField != null || !string.IsNullOrEmpty(action.DefaultValue))
                {
                    var mapping = new TemplateFieldMapping
                    {
                        Id = Guid.NewGuid().ToString(),
                        TemplateId = templateId,
                        FieldName = action.SourceFieldName,
                        TargetLocation = action.TargetLocation,
                        LocationType = action.TargetLocationType ?? "ExcelCell",
                        Description = action.Description ?? $"Auto-mapped by rule: {rule.Name}",
                        FormatInstructions = ExtractFormatInstructions(action.Transformation),
                        IsRequired = action.IsRequired,
                        DisplayOrder = action.DisplayOrder,
                        CreatedDate = DateTime.UtcNow
                    };

                    mappings.Add(mapping);
                }
            }

            return mappings;
        }

        /// <summary>
        /// Find all mapping rules that match the given document pattern.
        /// Returns rules ordered by priority (highest first).
        /// </summary>
        public async Task<List<MappingRule>> FindMatchingRulesAsync(DocumentPattern documentPattern)
        {
            try
            {
                // Get all active rules with their conditions
                var activeRules = await _context.MappingRules
                    .Include(r => r.Conditions)
                    .Include(r => r.Actions)
                    .Where(r => r.IsActive)
                    .OrderByDescending(r => r.Priority)
                    .ThenByDescending(r => r.SuccessRate)
                    .ToListAsync();

                var matchingRules = new List<MappingRule>();

                // Evaluate each rule against the document pattern
                foreach (var rule in activeRules)
                {
                    if (await EvaluateRuleAsync(rule, documentPattern))
                    {
                        matchingRules.Add(rule);
                    }
                }

                return matchingRules;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error finding matching rules: {ex.Message}");
                return new List<MappingRule>();
            }
        }

        /// <summary>
        /// Evaluate whether a specific rule matches the given document pattern.
        /// </summary>
        public async Task<bool> EvaluateRuleAsync(MappingRule rule, DocumentPattern documentPattern)
        {
            var advancedResult = await EvaluateRuleAdvancedAsync(rule, documentPattern);
            return advancedResult.ShouldApply;
        }

        /// <summary>
        /// Advanced rule evaluation with detailed scoring and condition analysis.
        /// Supports complex logical operators, weighted conditions, and confidence scoring.
        /// </summary>
        public async Task<RuleEvaluationResult> EvaluateRuleAdvancedAsync(MappingRule rule, DocumentPattern documentPattern)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new RuleEvaluationResult
            {
                Rule = rule,
                Explanation = $"Evaluating rule: {rule.Name}"
            };

            try
            {
                if (!rule.Conditions.Any())
                {
                    result.Explanation = "Rule has no conditions - cannot evaluate";
                    return result;
                }

                // Group conditions by their groups for nested evaluation
                var conditionGroups = rule.Conditions
                    .GroupBy(c => c.GroupId ?? "default")
                    .OrderBy(g => g.Key)
                    .ToList();

                var groupResults = new List<(string GroupId, bool Result, double Weight)>();

                // Evaluate each condition group
                foreach (var group in conditionGroups)
                {
                    var groupResult = await EvaluateConditionGroup(group, documentPattern);
                    groupResults.Add((group.Key, groupResult.IsTrue, groupResult.Weight));
                    
                    result.ConditionResults.AddRange(groupResult.ConditionResults);
                }

                // Combine group results using advanced logic
                result = CombineGroupResults(result, groupResults);
                
                // Calculate final scores
                result.RequiredConditionsPassed = result.ConditionResults.Count(c => c.IsRequired && c.IsTrue);
                result.TotalRequiredConditions = result.ConditionResults.Count(c => c.IsRequired);
                result.OptionalConditionsPassed = result.ConditionResults.Count(c => !c.IsRequired && c.IsTrue);
                result.TotalOptionalConditions = result.ConditionResults.Count(c => !c.IsRequired);

                // Apply rule logic: all required must pass, optional add weight
                result.ShouldApply = result.RequiredConditionsPassed == result.TotalRequiredConditions && 
                                   result.TotalRequiredConditions > 0;

                // Calculate weighted score including optional conditions
                var totalWeight = result.ConditionResults.Sum(c => c.Weight);
                var passedWeight = result.ConditionResults.Where(c => c.IsTrue).Sum(c => c.Weight);
                
                result.MatchScore = totalWeight > 0 ? passedWeight / totalWeight : 0.0;
                result.Confidence = result.ConditionResults.Any() ? 
                    result.ConditionResults.Average(c => c.Confidence) : 0.0;

                result.Explanation = GenerateEvaluationExplanation(result);
                
                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;

                Console.WriteLine($"🎯 ADVANCED EVAL: {rule.Name} | Should Apply: {result.ShouldApply} | Score: {result.MatchScore:P1} | Time: {result.ExecutionTime.TotalMilliseconds:F1}ms");

                return result;
            }
            catch (Exception ex)
            {
                result.Explanation = $"Error evaluating rule: {ex.Message}";
                Console.WriteLine($"❌ RULE ENGINE: Error in advanced evaluation '{rule.Name}': {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Test a rule with preview mode - shows what would happen without applying.
        /// </summary>
        public async Task<RuleTestResult> TestRuleAsync(MappingRule rule, DocumentPattern documentPattern, List<ExtractedField> extractedFields)
        {
            var testResult = new RuleTestResult();

            try
            {
                // Evaluate the rule
                testResult.EvaluationResult = await EvaluateRuleAdvancedAsync(rule, documentPattern);

                if (!testResult.EvaluationResult.ShouldApply)
                {
                    testResult.Summary = $"Rule '{rule.Name}' does not match current document pattern";
                    testResult.PredictedSuccessRate = 0.0;
                    return testResult;
                }

                // Generate preview mappings
                var actions = await _context.RuleActions
                    .Where(a => a.MappingRuleId == rule.Id)
                    .OrderBy(a => a.DisplayOrder)
                    .ToListAsync();

                foreach (var action in actions)
                {
                    var sourceField = extractedFields.FirstOrDefault(f => 
                        string.Equals(f.FieldName, action.SourceFieldName, StringComparison.OrdinalIgnoreCase));

                    if (sourceField != null)
                    {
                        var previewMapping = new TemplateFieldMapping
                        {
                            Id = Guid.NewGuid().ToString(),
                            TemplateId = "preview-template",
                            FieldName = action.SourceFieldName,
                            TargetLocation = action.TargetLocation,
                            LocationType = action.TargetLocationType ?? "ExcelCell",
                            Description = $"Preview mapping: {action.Description}",
                            CreatedDate = DateTime.UtcNow
                        };

                        testResult.PreviewMappings.Add(previewMapping);
                        testResult.MappedFields.Add(sourceField);
                    }
                    else
                    {
                        // Field not found in extracted data
                        if (string.IsNullOrEmpty(action.DefaultValue))
                        {
                            testResult.Warnings.Add($"Field '{action.SourceFieldName}' not found and no default value specified");
                        }
                        else
                        {
                            // Would use default value
                            var previewMapping = new TemplateFieldMapping
                            {
                                Id = Guid.NewGuid().ToString(),
                                TemplateId = "preview-template",
                                FieldName = action.SourceFieldName,
                                TargetLocation = action.TargetLocation,
                                LocationType = action.TargetLocationType ?? "ExcelCell",
                                Description = $"Preview mapping with default: {action.DefaultValue}",
                                CreatedDate = DateTime.UtcNow
                            };

                            testResult.PreviewMappings.Add(previewMapping);
                        }
                    }
                }

                // Find unmapped fields
                var mappedFieldNames = testResult.MappedFields.Select(f => f.FieldName).ToHashSet();
                testResult.UnmappedFields = extractedFields.Where(f => !mappedFieldNames.Contains(f.FieldName)).ToList();

                // Calculate predictions
                testResult.PredictedSuccessRate = rule.SuccessRate;
                testResult.EstimatedConfidence = testResult.EvaluationResult.Confidence;

                // Generate summary
                testResult.Summary = $"Rule '{rule.Name}' would create {testResult.PreviewMappings.Count} mappings " +
                                   $"from {testResult.MappedFields.Count} extracted fields. " +
                                   $"Confidence: {testResult.EstimatedConfidence:P1}, " +
                                   $"Predicted success: {testResult.PredictedSuccessRate:P1}";

                if (testResult.Warnings.Any())
                {
                    testResult.Summary += $" ({testResult.Warnings.Count} warnings)";
                }

                Console.WriteLine($"🧪 RULE TEST: {rule.Name} | Mappings: {testResult.PreviewMappings.Count} | Warnings: {testResult.Warnings.Count}");

                return testResult;
            }
            catch (Exception ex)
            {
                testResult.Summary = $"Error testing rule: {ex.Message}";
                Console.WriteLine($"❌ RULE ENGINE: Error testing rule '{rule.Name}': {ex.Message}");
                return testResult;
            }
        }

        /// <summary>
        /// Evaluate a single rule condition against document pattern.
        /// </summary>
        private bool EvaluateCondition(RuleCondition condition, DocumentPattern documentPattern)
        {
            try
            {
                // Get the value to evaluate based on condition type
                string? actualValue = condition.ConditionType switch
                {
                    "DocumentSupplier" => documentPattern.SupplierName,
                    "DocumentType" => documentPattern.DocumentType,
                    "TemplatePattern" => documentPattern.TemplatePattern,
                    "TemplateCategory" => documentPattern.TemplateCategory,
                    "FieldExists" => documentPattern.AvailableFields.Contains(condition.FieldName) ? "true" : "false",
                    "FieldValue" => documentPattern.FieldValues.GetValueOrDefault(condition.FieldName),
                    _ => null
                };

                if (actualValue == null && condition.Operator != "IsEmpty")
                {
                    return false;
                }

                // Apply comparison operator
                return condition.Operator switch
                {
                    "Equals" => CompareStrings(actualValue, condition.Value, condition.IsCaseSensitive),
                    "Contains" => ContainsString(actualValue, condition.Value, condition.IsCaseSensitive),
                    "StartsWith" => StartsWithString(actualValue, condition.Value, condition.IsCaseSensitive),
                    "EndsWith" => EndsWithString(actualValue, condition.Value, condition.IsCaseSensitive),
                    "NotEquals" => !CompareStrings(actualValue, condition.Value, condition.IsCaseSensitive),
                    "IsEmpty" => string.IsNullOrWhiteSpace(actualValue),
                    "IsNotEmpty" => !string.IsNullOrWhiteSpace(actualValue),
                    "Matches" => MatchesRegex(actualValue, condition.Value),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error evaluating condition: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create a new mapping rule from current field mappings.
        /// This is called when user clicks "Save as rule" checkbox.
        /// </summary>
        public async Task<MappingRule> CreateRuleFromMappingsAsync(
            string ruleName, 
            string description,
            DocumentPattern documentPattern, 
            List<TemplateFieldMapping> fieldMappings)
        {
            try
            {
                // Create the mapping rule
                var rule = new MappingRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ruleName,
                    Description = description,
                    IsActive = true,
                    Priority = 100, // Default priority
                    CreatedDate = DateTime.UtcNow,
                    UsageCount = 0,
                    SuccessRate = 1.0 // Start with optimistic success rate
                };

                _context.MappingRules.Add(rule);

                // Create conditions based on document pattern
                await CreateConditionsFromPatternAsync(rule.Id, documentPattern);

                // Create actions based on field mappings
                await CreateActionsFromMappingsAsync(rule.Id, fieldMappings);

                await _context.SaveChangesAsync();

                Console.WriteLine($"📝 RULE ENGINE: Created rule '{ruleName}' with {fieldMappings.Count} mappings");

                return rule;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error creating rule: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create rule conditions from document pattern.
        /// </summary>
        private async Task CreateConditionsFromPatternAsync(string ruleId, DocumentPattern documentPattern)
        {
            var conditions = new List<RuleCondition>();
            int order = 0;

            // Add supplier condition if available
            if (!string.IsNullOrWhiteSpace(documentPattern.SupplierName))
            {
                conditions.Add(new RuleCondition
                {
                    Id = Guid.NewGuid().ToString(),
                    MappingRuleId = ruleId,
                    ConditionType = "DocumentSupplier",
                    FieldName = "SupplierName",
                    Operator = "Equals",
                    Value = documentPattern.SupplierName,
                    IsCaseSensitive = false,
                    LogicalOperator = "AND",
                    DisplayOrder = order++,
                    CreatedDate = DateTime.UtcNow
                });
            }

            // Add document type condition if available
            if (!string.IsNullOrWhiteSpace(documentPattern.DocumentType))
            {
                conditions.Add(new RuleCondition
                {
                    Id = Guid.NewGuid().ToString(),
                    MappingRuleId = ruleId,
                    ConditionType = "DocumentType",
                    FieldName = "DocumentType",
                    Operator = "Equals",
                    Value = documentPattern.DocumentType,
                    IsCaseSensitive = false,
                    LogicalOperator = conditions.Any() ? "AND" : null,
                    DisplayOrder = order++,
                    CreatedDate = DateTime.UtcNow
                });
            }

            // Add template pattern condition if available
            if (!string.IsNullOrWhiteSpace(documentPattern.TemplatePattern))
            {
                conditions.Add(new RuleCondition
                {
                    Id = Guid.NewGuid().ToString(),
                    MappingRuleId = ruleId,
                    ConditionType = "TemplatePattern",
                    FieldName = "TemplatePattern",
                    Operator = "Equals",
                    Value = documentPattern.TemplatePattern,
                    IsCaseSensitive = false,
                    LogicalOperator = conditions.Any() ? "AND" : null,
                    DisplayOrder = order++,
                    CreatedDate = DateTime.UtcNow
                });
            }

            _context.RuleConditions.AddRange(conditions);
        }

        /// <summary>
        /// Create rule actions from field mappings.
        /// </summary>
        private async Task CreateActionsFromMappingsAsync(string ruleId, List<TemplateFieldMapping> fieldMappings)
        {
            var actions = new List<RuleAction>();

            for (int i = 0; i < fieldMappings.Count; i++)
            {
                var mapping = fieldMappings[i];

                actions.Add(new RuleAction
                {
                    Id = Guid.NewGuid().ToString(),
                    MappingRuleId = ruleId,
                    ActionType = "MapField",
                    SourceFieldName = mapping.FieldName,
                    TargetLocation = mapping.TargetLocation,
                    TargetLocationType = mapping.LocationType,
                    Transformation = null, // TODO: Add transformation support
                    DefaultValue = null,
                    IsRequired = mapping.IsRequired,
                    DisplayOrder = i,
                    CreatedDate = DateTime.UtcNow,
                    Description = mapping.Description
                });
            }

            _context.RuleActions.AddRange(actions);
        }

        /// <summary>
        /// Record successful application of a mapping rule.
        /// </summary>
        public async Task RecordRuleSuccessAsync(string ruleId)
        {
            try
            {
                var rule = await _context.MappingRules.FirstOrDefaultAsync(r => r.Id == ruleId);
                if (rule != null)
                {
                    rule.RecordSuccess();
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error recording rule success: {ex.Message}");
            }
        }

        /// <summary>
        /// Record failed application of a mapping rule.
        /// </summary>
        public async Task RecordRuleFailureAsync(string ruleId)
        {
            try
            {
                var rule = await _context.MappingRules.FirstOrDefaultAsync(r => r.Id == ruleId);
                if (rule != null)
                {
                    rule.RecordFailure();
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error recording rule failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all active mapping rules ordered by priority and success rate.
        /// </summary>
        public async Task<List<MappingRule>> GetActiveRulesAsync()
        {
            return await _context.MappingRules
                .Include(r => r.Conditions)
                .Include(r => r.Actions)
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.SuccessRate)
                .ToListAsync();
        }

        /// <summary>
        /// Get rule statistics for monitoring and optimization.
        /// </summary>
        public async Task<RuleEngineStatistics> GetStatisticsAsync()
        {
            try
            {
                var rules = await _context.MappingRules.ToListAsync();
                var activeRules = rules.Where(r => r.IsActive).ToList();

                return new RuleEngineStatistics
                {
                    TotalActiveRules = activeRules.Count,
                    TotalRuleApplications = activeRules.Sum(r => r.UsageCount),
                    OverallSuccessRate = activeRules.Any() ? activeRules.Average(r => r.SuccessRate) : 0.0,
                    AverageExecutionTimeMs = 0.0, // TODO: Add performance tracking
                    TopRules = activeRules
                        .OrderByDescending(r => r.UsageCount)
                        .Take(5)
                        .Select(r => new RuleUsageStatistic
                        {
                            RuleId = r.Id,
                            RuleName = r.Name,
                            UsageCount = r.UsageCount,
                            SuccessRate = r.SuccessRate,
                            LastUsed = r.LastModifiedDate
                        })
                        .ToList(),
                    GeneratedDate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error generating statistics: {ex.Message}");
                return new RuleEngineStatistics();
            }
        }

        // Helper methods for string comparison
        private bool CompareStrings(string? actual, string expected, bool caseSensitive)
        {
            if (actual == null) return false;
            return caseSensitive 
                ? actual.Equals(expected, StringComparison.Ordinal)
                : actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool ContainsString(string? actual, string expected, bool caseSensitive)
        {
            if (actual == null) return false;
            return caseSensitive 
                ? actual.Contains(expected, StringComparison.Ordinal)
                : actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool StartsWithString(string? actual, string expected, bool caseSensitive)
        {
            if (actual == null) return false;
            return caseSensitive 
                ? actual.StartsWith(expected, StringComparison.Ordinal)
                : actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool EndsWithString(string? actual, string expected, bool caseSensitive)
        {
            if (actual == null) return false;
            return caseSensitive 
                ? actual.EndsWith(expected, StringComparison.Ordinal)
                : actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesRegex(string? actual, string pattern)
        {
            if (actual == null) return false;
            try
            {
                return Regex.IsMatch(actual, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            catch
            {
                return false;
            }
        }

        private string? ExtractFormatInstructions(string? transformation)
        {
            // TODO: Parse transformation JSON and extract format instructions
            return transformation;
        }

        /// <summary>
        /// Evaluate a group of conditions with advanced logical operators.
        /// </summary>
        private async Task<(bool IsTrue, double Weight, List<ConditionEvaluationResult> ConditionResults)> EvaluateConditionGroup(
            IGrouping<string, RuleCondition> conditionGroup, 
            DocumentPattern documentPattern)
        {
            var conditionResults = new List<ConditionEvaluationResult>();
            var groupWeight = 0.0;

            foreach (var condition in conditionGroup.OrderBy(c => c.DisplayOrder))
            {
                var conditionResult = await EvaluateConditionAdvanced(condition, documentPattern);
                conditionResults.Add(conditionResult);
                groupWeight += conditionResult.Weight;
            }

            // Apply group-level logical operations
            bool groupResult = ApplyGroupLogic(conditionResults);
            
            return (groupResult, groupWeight, conditionResults);
        }

        /// <summary>
        /// Evaluate a single condition with detailed result information.
        /// </summary>
        private async Task<ConditionEvaluationResult> EvaluateConditionAdvanced(
            RuleCondition condition, 
            DocumentPattern documentPattern)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ConditionEvaluationResult
            {
                Weight = condition.Weight,
                IsRequired = condition.IsRequired,
                ExpectedValue = condition.Value,
                Operator = condition.Operator
            };

            try
            {
                // Get the value to evaluate based on condition type
                result.ActualValue = condition.ConditionType switch
                {
                    "DocumentSupplier" => documentPattern.SupplierName,
                    "DocumentType" => documentPattern.DocumentType,
                    "TemplatePattern" => documentPattern.TemplatePattern,
                    "TemplateCategory" => documentPattern.TemplateCategory,
                    "FieldExists" => documentPattern.AvailableFields.Contains(condition.FieldName) ? "true" : "false",
                    "FieldValue" => documentPattern.FieldValues.GetValueOrDefault(condition.FieldName),
                    _ => null
                };

                if (result.ActualValue == null && condition.Operator != "IsEmpty")
                {
                    result.IsTrue = false;
                    result.Confidence = 0.0;
                    result.Explanation = $"Field '{condition.FieldName}' not found in document pattern";
                    return result;
                }

                // Apply comparison operator with confidence scoring
                result.IsTrue = condition.Operator switch
                {
                    "Equals" => CompareStringsWithConfidence(result.ActualValue, condition.Value, condition.IsCaseSensitive, out var eqConf) && (result.Confidence = eqConf) >= 0,
                    "Contains" => ContainsStringWithConfidence(result.ActualValue, condition.Value, condition.IsCaseSensitive, out var contConf) && (result.Confidence = contConf) >= 0,
                    "StartsWith" => StartsWithStringWithConfidence(result.ActualValue, condition.Value, condition.IsCaseSensitive, out var startConf) && (result.Confidence = startConf) >= 0,
                    "EndsWith" => EndsWithStringWithConfidence(result.ActualValue, condition.Value, condition.IsCaseSensitive, out var endConf) && (result.Confidence = endConf) >= 0,
                    "NotEquals" => !CompareStringsWithConfidence(result.ActualValue, condition.Value, condition.IsCaseSensitive, out var neqConf) && (result.Confidence = neqConf) >= 0,
                    "IsEmpty" => string.IsNullOrWhiteSpace(result.ActualValue) && (result.Confidence = 1.0) >= 0,
                    "IsNotEmpty" => !string.IsNullOrWhiteSpace(result.ActualValue) && (result.Confidence = 1.0) >= 0,
                    "Matches" => MatchesRegexWithConfidence(result.ActualValue, condition.Value, out var regexConf) && (result.Confidence = regexConf) >= 0,
                    _ => false
                };

                // Generate explanation
                result.Explanation = GenerateConditionExplanation(result, condition);

                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;

                return result;
            }
            catch (Exception ex)
            {
                result.IsTrue = false;
                result.Confidence = 0.0;
                result.Explanation = $"Error evaluating condition: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Apply logical operations to a group of condition results.
        /// </summary>
        private bool ApplyGroupLogic(List<ConditionEvaluationResult> conditionResults)
        {
            if (!conditionResults.Any()) return false;

            var requiredResults = conditionResults.Where(c => c.IsRequired).ToList();
            var optionalResults = conditionResults.Where(c => !c.IsRequired).ToList();

            // All required conditions must pass
            if (requiredResults.Any() && !requiredResults.All(c => c.IsTrue))
            {
                return false;
            }

            // If no required conditions, at least one optional must pass
            if (!requiredResults.Any() && optionalResults.Any())
            {
                return optionalResults.Any(c => c.IsTrue);
            }

            return requiredResults.Any(); // Has required conditions and all passed
        }

        /// <summary>
        /// Combine results from multiple condition groups.
        /// </summary>
        private RuleEvaluationResult CombineGroupResults(
            RuleEvaluationResult result, 
            List<(string GroupId, bool Result, double Weight)> groupResults)
        {
            if (!groupResults.Any())
            {
                result.ShouldApply = false;
                result.MatchScore = 0.0;
                return result;
            }

            // For now, use simple AND logic between groups
            // TODO: Implement more sophisticated group combination logic
            result.ShouldApply = groupResults.All(g => g.Result);
            
            var totalWeight = groupResults.Sum(g => g.Weight);
            var passedWeight = groupResults.Where(g => g.Result).Sum(g => g.Weight);
            
            result.MatchScore = totalWeight > 0 ? passedWeight / totalWeight : 0.0;
            
            return result;
        }

        /// <summary>
        /// Generate human-readable explanation of rule evaluation.
        /// </summary>
        private string GenerateEvaluationExplanation(RuleEvaluationResult result)
        {
            var explanation = new List<string>();
            
            explanation.Add($"Rule '{result.Rule?.Name}' evaluation:");
            explanation.Add($"- Required conditions: {result.RequiredConditionsPassed}/{result.TotalRequiredConditions} passed");
            
            if (result.TotalOptionalConditions > 0)
            {
                explanation.Add($"- Optional conditions: {result.OptionalConditionsPassed}/{result.TotalOptionalConditions} passed");
            }
            
            explanation.Add($"- Overall match score: {result.MatchScore:P1}");
            explanation.Add($"- Confidence: {result.Confidence:P1}");
            explanation.Add($"- Decision: {(result.ShouldApply ? "APPLY RULE" : "DO NOT APPLY")}");

            return string.Join("\n", explanation);
        }

        /// <summary>
        /// Generate explanation for individual condition evaluation.
        /// </summary>
        private string GenerateConditionExplanation(ConditionEvaluationResult result, RuleCondition condition)
        {
            var status = result.IsTrue ? "✅ PASS" : "❌ FAIL";
            var type = result.IsRequired ? "Required" : "Optional";
            
            return $"{status} ({type}) '{condition.FieldName}' {condition.Operator} '{condition.Value}' " +
                   $"(actual: '{result.ActualValue}', confidence: {result.Confidence:P1})";
        }

        // Enhanced string comparison methods with confidence scoring
        private bool CompareStringsWithConfidence(string? actual, string expected, bool caseSensitive, out double confidence)
        {
            confidence = 1.0;
            if (actual == null) 
            {
                confidence = 0.0;
                return false;
            }
            
            bool match = caseSensitive 
                ? actual.Equals(expected, StringComparison.Ordinal)
                : actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
                
            // Calculate similarity confidence even if not exact match
            if (!match)
            {
                confidence = CalculateStringSimilarity(actual, expected);
            }
            
            return match;
        }

        private bool ContainsStringWithConfidence(string? actual, string expected, bool caseSensitive, out double confidence)
        {
            confidence = 1.0;
            if (actual == null)
            {
                confidence = 0.0;
                return false;
            }
            
            bool contains = caseSensitive 
                ? actual.Contains(expected, StringComparison.Ordinal)
                : actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
                
            if (!contains)
            {
                confidence = CalculateStringSimilarity(actual, expected) * 0.5; // Partial credit for similarity
            }
            
            return contains;
        }

        private bool StartsWithStringWithConfidence(string? actual, string expected, bool caseSensitive, out double confidence)
        {
            confidence = 1.0;
            if (actual == null)
            {
                confidence = 0.0;
                return false;
            }
            
            bool startsWith = caseSensitive 
                ? actual.StartsWith(expected, StringComparison.Ordinal)
                : actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase);
                
            if (!startsWith)
            {
                confidence = CalculateStringSimilarity(actual.Substring(0, Math.Min(actual.Length, expected.Length)), expected) * 0.7;
            }
            
            return startsWith;
        }

        private bool EndsWithStringWithConfidence(string? actual, string expected, bool caseSensitive, out double confidence)
        {
            confidence = 1.0;
            if (actual == null)
            {
                confidence = 0.0;
                return false;
            }
            
            bool endsWith = caseSensitive 
                ? actual.EndsWith(expected, StringComparison.Ordinal)
                : actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase);
                
            if (!endsWith)
            {
                var actualEnd = actual.Length >= expected.Length ? actual.Substring(actual.Length - expected.Length) : actual;
                confidence = CalculateStringSimilarity(actualEnd, expected) * 0.7;
            }
            
            return endsWith;
        }

        private bool MatchesRegexWithConfidence(string? actual, string pattern, out double confidence)
        {
            confidence = 1.0;
            if (actual == null)
            {
                confidence = 0.0;
                return false;
            }
            
            try
            {
                bool matches = Regex.IsMatch(actual, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (!matches)
                {
                    confidence = 0.1; // Small confidence for failed regex
                }
                return matches;
            }
            catch
            {
                confidence = 0.0;
                return false;
            }
        }

        /// <summary>
        /// Calculate string similarity using Levenshtein distance.
        /// </summary>
        private double CalculateStringSimilarity(string str1, string str2)
        {
            if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2)) return 1.0;
            if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2)) return 0.0;

            int maxLength = Math.Max(str1.Length, str2.Length);
            int distance = LevenshteinDistance(str1.ToLower(), str2.ToLower());
            
            return 1.0 - (double)distance / maxLength;
        }

        /// <summary>
        /// Calculate Levenshtein distance between two strings.
        /// </summary>
        private int LevenshteinDistance(string str1, string str2)
        {
            int[,] matrix = new int[str1.Length + 1, str2.Length + 1];

            for (int i = 0; i <= str1.Length; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= str2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= str1.Length; i++)
            {
                for (int j = 1; j <= str2.Length; j++)
                {
                    int cost = str1[i - 1] == str2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[str1.Length, str2.Length];
        }

        /// <summary>
        /// Activate a mapping rule to include it in rule evaluation.
        /// </summary>
        public async Task ActivateRuleAsync(string ruleId)
        {
            try
            {
                var rule = await _context.MappingRules.FirstOrDefaultAsync(r => r.Id == ruleId);
                if (rule != null)
                {
                    rule.IsActive = true;
                    rule.LastModifiedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"✅ RULE ACTIVATED: {rule.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error activating rule {ruleId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Deactivate a mapping rule to exclude it from rule evaluation.
        /// </summary>
        public async Task DeactivateRuleAsync(string ruleId)
        {
            try
            {
                var rule = await _context.MappingRules.FirstOrDefaultAsync(r => r.Id == ruleId);
                if (rule != null)
                {
                    rule.IsActive = false;
                    rule.LastModifiedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"⏸️ RULE DEACTIVATED: {rule.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error deactivating rule {ruleId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle rule activation status.
        /// </summary>
        public async Task<bool> ToggleRuleActivationAsync(string ruleId)
        {
            try
            {
                var rule = await _context.MappingRules.FirstOrDefaultAsync(r => r.Id == ruleId);
                if (rule != null)
                {
                    rule.IsActive = !rule.IsActive;
                    rule.LastModifiedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    var status = rule.IsActive ? "ACTIVATED" : "DEACTIVATED";
                    Console.WriteLine($"🔄 RULE {status}: {rule.Name}");
                    
                    return rule.IsActive;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error toggling rule {ruleId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update rule priority for conflict resolution.
        /// </summary>
        public async Task UpdateRulePriorityAsync(string ruleId, int newPriority)
        {
            try
            {
                var rule = await _context.MappingRules.FirstOrDefaultAsync(r => r.Id == ruleId);
                if (rule != null)
                {
                    var oldPriority = rule.Priority;
                    rule.Priority = newPriority;
                    rule.LastModifiedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"📊 RULE PRIORITY UPDATED: {rule.Name} | {oldPriority} → {newPriority}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error updating rule priority {ruleId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete a mapping rule permanently.
        /// </summary>
        public async Task DeleteRuleAsync(string ruleId)
        {
            try
            {
                var rule = await _context.MappingRules
                    .Include(r => r.Conditions)
                    .Include(r => r.Actions)
                    .FirstOrDefaultAsync(r => r.Id == ruleId);
                
                if (rule != null)
                {
                    var ruleName = rule.Name;
                    
                    // Remove related conditions and actions (cascade delete should handle this)
                    _context.RuleConditions.RemoveRange(rule.Conditions);
                    _context.RuleActions.RemoveRange(rule.Actions);
                    _context.MappingRules.Remove(rule);
                    
                    await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"🗑️ RULE DELETED: {ruleName} (and {rule.Conditions.Count} conditions, {rule.Actions.Count} actions)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RULE ENGINE: Error deleting rule {ruleId}: {ex.Message}");
            }
        }
    }
}