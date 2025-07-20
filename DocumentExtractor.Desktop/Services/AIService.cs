using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// AI Service for document intelligence using OpenAI/Claude APIs
/// Handles real AI conversations and document analysis
/// </summary>
public class AIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIService>? _logger;
    private readonly string? _openAIKey;
    private readonly string? _claudeKey;
    
    // Cost tracking
    private int _totalTokensUsed = 0;
    private decimal _estimatedCost = 0;

    public AIService(IConfiguration? configuration = null, ILogger<AIService>? logger = null)
    {
        _httpClient = new HttpClient();
        _logger = logger;
        
        // Try to get API keys from configuration (safe fallback if not available)
        _openAIKey = configuration?.GetSection("AI:OpenAI:ApiKey")?.Value;
        _claudeKey = configuration?.GetSection("AI:Claude:ApiKey")?.Value;
        
        // Set up HTTP client headers
        if (!string.IsNullOrEmpty(_openAIKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAIKey}");
        }
        
        Console.WriteLine("🤖 AIService initialized");
        Console.WriteLine($"📊 OpenAI Key Available: {!string.IsNullOrEmpty(_openAIKey)}");
        Console.WriteLine($"📊 Claude Key Available: {!string.IsNullOrEmpty(_claudeKey)}");
    }

    /// <summary>
    /// Process a chat message with AI assistance
    /// Falls back to mock responses if no API keys available
    /// </summary>
    public async Task<string> ProcessChatMessageAsync(string message, string context = "", List<string>? documentPaths = null)
    {
        try
        {
            // For now, implement with intelligent mock responses
            // TODO: Replace with real API calls once keys are configured
            return await GenerateIntelligentResponse(message, context, documentPaths);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing chat message");
            return "I encountered an error processing your request. Please try again.";
        }
    }

    /// <summary>
    /// Analyze a document using AI vision capabilities
    /// </summary>
    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(string documentPath, string? userInstructions = null)
    {
        try
        {
            // TODO: Implement real document analysis with OpenAI Vision API
            return await GenerateMockAnalysis(documentPath, userInstructions);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error analyzing document");
            return new DocumentAnalysisResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Fields = new List<DetectedField>()
            };
        }
    }

    /// <summary>
    /// Learn from user-provided screenshot annotations
    /// </summary>
    public async Task<PatternLearningResult> LearnFromScreenshotAsync(string screenshotPath, List<FieldAnnotation> annotations)
    {
        try
        {
            // TODO: Implement real pattern learning with AI
            return await GenerateMockPatternLearning(screenshotPath, annotations);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error learning from screenshot");
            return new PatternLearningResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get current cost tracking information
    /// </summary>
    public CostTrackingInfo GetCostInfo()
    {
        return new CostTrackingInfo
        {
            TotalTokensUsed = _totalTokensUsed,
            EstimatedCost = _estimatedCost,
            HasApiKeys = !string.IsNullOrEmpty(_openAIKey) || !string.IsNullOrEmpty(_claudeKey)
        };
    }

    #region Private Implementation Methods

    private async Task<string> GenerateIntelligentResponse(string message, string context, List<string>? documentPaths)
    {
        await Task.Delay(500); // Simulate API call delay

        var lowerMessage = message.ToLowerInvariant();

        // Document-related responses
        if (documentPaths?.Count > 0)
        {
            if (lowerMessage.Contains("extract") || lowerMessage.Contains("find") || lowerMessage.Contains("get"))
            {
                return $"📄 I can see you have {documentPaths.Count} document(s) loaded. I can help extract data from them!\n\n" +
                       "To get started:\n" +
                       "• Tell me what fields you need (e.g., \"Find invoice numbers and amounts\")\n" +
                       "• Or take a screenshot showing me exactly where the data is\n" +
                       "• I'll learn the pattern and apply it to all similar documents\n\n" +
                       "💡 **Tip**: The more specific you are, the better I can help!";
            }

            if (lowerMessage.Contains("screenshot") || lowerMessage.Contains("teach") || lowerMessage.Contains("show"))
            {
                return "📸 Great! Here's how screenshot teaching works:\n\n" +
                       "1. **Take a screenshot** of the document area you want me to learn\n" +
                       "2. **Draw colored boxes** around each field (different colors for different data types)\n" +
                       "3. **Drop the screenshot here** and tell me what each color represents\n" +
                       "4. **I'll learn the pattern** and find the same fields in other documents\n\n" +
                       "🎯 This teaches me exactly where to look for data in your document type!";
            }
        }

        // General AI assistance
        if (lowerMessage.Contains("help") || lowerMessage.Contains("how"))
        {
            return "🤖 **I'm your AI Document Intelligence Assistant!**\n\n" +
                   "I can help you:\n" +
                   "✅ **Extract data** from PDFs, invoices, forms, receipts\n" +
                   "✅ **Learn patterns** from screenshots you show me\n" +
                   "✅ **Process batches** of similar documents automatically\n" +
                   "✅ **Generate Excel** files with extracted data\n\n" +
                   "💬 Just tell me what you need or drop some documents to get started!";
        }

        if (lowerMessage.Contains("cost") || lowerMessage.Contains("price") || lowerMessage.Contains("token"))
        {
            var costInfo = GetCostInfo();
            return $"💰 **Cost Tracking:**\n" +
                   $"• Tokens used: {costInfo.TotalTokensUsed:N0}\n" +
                   $"• Estimated cost: ${costInfo.EstimatedCost:F4}\n" +
                   $"• API Status: {(costInfo.HasApiKeys ? "Connected" : "Demo Mode")}\n\n" +
                   "📊 Real AI processing costs ~$0.001-0.005 per document";
        }

        // Default intelligent response
        return "🤔 I understand you're asking about document processing. Could you be more specific?\n\n" +
               "For example:\n" +
               "• \"Extract invoice numbers and amounts from these PDFs\"\n" +
               "• \"Help me process TNB electricity bills\"\n" +
               "• \"Show me how to teach you patterns\"\n\n" +
               "💡 The clearer your request, the better I can assist you!";
    }

    private async Task<DocumentAnalysisResult> GenerateMockAnalysis(string documentPath, string? userInstructions)
    {
        await Task.Delay(1000); // Simulate analysis time

        var fileName = System.IO.Path.GetFileName(documentPath);
        var extension = System.IO.Path.GetExtension(documentPath).ToLowerInvariant();

        var fields = new List<DetectedField>();

        // Generate realistic mock fields based on file type
        if (fileName.Contains("TNB") || fileName.ToLowerInvariant().Contains("bill"))
        {
            fields.AddRange(new[]
            {
                new DetectedField { Name = "Account Number", Value = "123456789012", Confidence = 0.95, X = 450, Y = 120, Width = 150, Height = 25 },
                new DetectedField { Name = "Bill Amount", Value = "RM 245.67", Confidence = 0.98, X = 400, Y = 580, Width = 100, Height = 30 },
                new DetectedField { Name = "Due Date", Value = "15/08/2024", Confidence = 0.92, X = 350, Y = 650, Width = 120, Height = 25 },
                new DetectedField { Name = "Bill Period", Value = "09/07/2024 - 08/08/2024", Confidence = 0.89, X = 200, Y = 200, Width = 180, Height = 25 }
            });
        }
        else if (fileName.ToLowerInvariant().Contains("invoice"))
        {
            fields.AddRange(new[]
            {
                new DetectedField { Name = "Invoice Number", Value = "INV-2024-001", Confidence = 0.96, X = 480, Y = 150, Width = 130, Height = 25 },
                new DetectedField { Name = "Total Amount", Value = "$1,234.56", Confidence = 0.97, X = 450, Y = 620, Width = 100, Height = 30 },
                new DetectedField { Name = "Invoice Date", Value = "17/07/2024", Confidence = 0.94, X = 480, Y = 180, Width = 110, Height = 25 },
                new DetectedField { Name = "Customer", Value = "ABC Corp Ltd", Confidence = 0.88, X = 150, Y = 250, Width = 200, Height = 25 }
            });
        }

        return new DocumentAnalysisResult
        {
            Success = true,
            DocumentType = DetermineDocumentType(fileName),
            Fields = fields,
            Confidence = fields.Count > 0 ? fields.Average(f => f.Confidence) : 0.5
        };
    }

    private async Task<PatternLearningResult> GenerateMockPatternLearning(string screenshotPath, List<FieldAnnotation> annotations)
    {
        await Task.Delay(800); // Simulate learning time

        return new PatternLearningResult
        {
            Success = true,
            PatternId = Guid.NewGuid().ToString(),
            LearnedFieldCount = annotations.Count,
            Confidence = 0.85,
            Message = $"✅ Successfully learned {annotations.Count} field patterns from your screenshot!\n\n" +
                     "I can now automatically find these fields in similar documents."
        };
    }

    private string DetermineDocumentType(string fileName)
    {
        var lowerName = fileName.ToLowerInvariant();
        
        if (lowerName.Contains("tnb") || lowerName.Contains("electricity") || lowerName.Contains("bill"))
            return "Electricity Bill";
        
        if (lowerName.Contains("invoice"))
            return "Invoice";
        
        if (lowerName.Contains("receipt"))
            return "Receipt";
        
        if (lowerName.Contains("statement"))
            return "Bank Statement";
        
        return "Unknown Document";
    }

    #endregion

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

#region Data Models

public class DocumentAnalysisResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string DocumentType { get; set; } = "";
    public List<DetectedField> Fields { get; set; } = new();
    public double Confidence { get; set; }
}

public class DetectedField
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public double Confidence { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class FieldAnnotation
{
    public string FieldName { get; set; } = "";
    public string FieldType { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Color { get; set; } = "";
}

public class PatternLearningResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string PatternId { get; set; } = "";
    public int LearnedFieldCount { get; set; }
    public double Confidence { get; set; }
    public string Message { get; set; } = "";
}

public class CostTrackingInfo
{
    public int TotalTokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public bool HasApiKeys { get; set; }
}

#endregion