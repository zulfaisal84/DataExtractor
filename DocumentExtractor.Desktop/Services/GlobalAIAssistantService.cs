using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentExtractor.Desktop.Models;
using DocumentExtractor.Core.Interfaces;
using DocumentExtractor.Core.Models;
using DocumentExtractor.Services;
using DocumentExtractor.Data.Context;
using Microsoft.Extensions.Logging;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// Global AI Assistant service that provides context-aware chat functionality across all tabs.
/// Extracts and centralizes chat logic for universal access throughout the application.
/// </summary>
public partial class GlobalAIAssistantService : ObservableObject
{
    private readonly IDocumentProcessor _documentProcessor;
    private readonly DocumentExtractionContext _context;

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    [ObservableProperty]
    private string _currentMessage = string.Empty;

    [ObservableProperty]
    private bool _canSendMessage = true;

    [ObservableProperty]
    private bool _isProcessing = false;

    [ObservableProperty]
    private string _statusMessage = "AI Assistant ready to help!";

    [ObservableProperty]
    private string _currentContext = "Dashboard";

    [ObservableProperty]
    private bool _isVisible = false;

    #endregion

    #region Events

    /// <summary>
    /// Event fired when the current context changes (e.g., user switches tabs)
    /// </summary>
    public event Action<string>? ContextChanged;

    /// <summary>
    /// Event fired when a new message is added to chat
    /// </summary>
    public event Action<ChatMessage>? MessageAdded;

    #endregion

    #region Constructor

    public GlobalAIAssistantService(DocumentExtractionContext context)
    {
        try
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            
            // Initialize document processor with simple logging
            var textExtractorLogger = new SimpleConsoleLogger<TesseractTextExtractor>();
            var textExtractor = new TesseractTextExtractor(textExtractorLogger);
            var processorLogger = new SimpleConsoleLogger<RealDocumentProcessor>();
            _documentProcessor = new RealDocumentProcessor(textExtractor, _context, processorLogger);
            
            // Initialize messages collection
            Messages = new ObservableCollection<ChatMessage>();
            
            Console.WriteLine("🌐 GlobalAIAssistantService initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error initializing GlobalAIAssistantService: {ex.Message}");
            StatusMessage = $"AI Assistant initialization error: {ex.Message}";
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task SendMessage()
    {
        if (!string.IsNullOrWhiteSpace(CurrentMessage))
        {
            await SendMessageAsync(CurrentMessage);
        }
    }

    [RelayCommand]
    private async Task SendMessageWithParameter(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            await SendMessageAsync(message);
        }
    }

    [RelayCommand]
    private void Toggle()
    {
        ToggleVisibility();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialize the AI Assistant with a context-aware welcome message
    /// </summary>
    public void InitializeWelcomeMessage()
    {
        if (Messages.Count == 0)
        {
            AddMessage("AI Assistant", 
                "👋 **Hello! I'm your Global AI Assistant!**\n\n" +
                "I'm available on ALL tabs to help you with:\n\n" +
                "🗺️ **Template Mapping**: Click-to-teach field mapping, Excel layouts\n" +
                "📊 **Dashboard**: Processing stats, document insights\n" +
                "📋 **Documents**: Upload, review, re-process documents\n" +
                "📁 **Templates**: Manage template library, categorization\n" +
                "🤖 **AI Learning**: Conversational document teaching\n\n" +
                "💡 **Smart Help**: I provide context-aware assistance based on where you are!\n\n" +
                "What can I help you with today?", 
                ChatMessageType.Bot);
            
            StatusMessage = "Global AI Assistant ready to help across all tabs!";
        }
    }

    /// <summary>
    /// Update the current context (tab) and provide relevant help
    /// </summary>
    public void UpdateContext(string newContext)
    {
        var previousContext = CurrentContext;
        CurrentContext = newContext;
        ContextChanged?.Invoke(newContext);

        // Provide context-specific welcome message
        if (previousContext != newContext)
        {
            var contextMessage = GetContextWelcomeMessage(newContext);
            if (!string.IsNullOrEmpty(contextMessage))
            {
                AddMessage("AI Assistant", contextMessage, ChatMessageType.System);
            }
        }

        Console.WriteLine($"🔄 Context changed: {previousContext} → {newContext}");
    }

    /// <summary>
    /// Send a message with context awareness
    /// </summary>
    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || IsProcessing)
            return;

        try
        {
            IsProcessing = true;
            CanSendMessage = false;
            
            var userMessage = message.Trim();
            CurrentMessage = string.Empty;

            // Add user message to chat
            AddMessage("You", userMessage, ChatMessageType.User);

            // Process the message with current context
            var response = await ProcessUserMessageWithContext(userMessage, CurrentContext);
            
            // Add AI response to chat
            AddMessage("AI Assistant", response, ChatMessageType.Bot);

            StatusMessage = "Message processed successfully";
        }
        catch (Exception ex)
        {
            AddMessage("System", $"Error processing message: {ex.Message}", ChatMessageType.System);
            StatusMessage = $"Error: {ex.Message}";
            Console.WriteLine($"❌ Error in SendMessageAsync: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
            CanSendMessage = true;
        }
    }

    /// <summary>
    /// Toggle AI Assistant visibility
    /// </summary>
    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
        
        if (IsVisible && Messages.Count == 0)
        {
            InitializeWelcomeMessage();
        }
        
        Console.WriteLine($"🎨 AI Assistant visibility: {IsVisible}");
    }

    /// <summary>
    /// Add a quick suggestion based on current context
    /// </summary>
    public void AddContextualSuggestion()
    {
        var suggestion = GetContextualSuggestion(CurrentContext);
        if (!string.IsNullOrEmpty(suggestion))
        {
            AddMessage("AI Assistant", suggestion, ChatMessageType.Bot);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Process user message with context awareness
    /// </summary>
    private async Task<string> ProcessUserMessageWithContext(string userMessage, string context)
    {
        try
        {
            // First check for context-specific responses
            var contextResponse = GetContextSpecificResponse(userMessage, context);
            if (!string.IsNullOrEmpty(contextResponse))
            {
                return contextResponse;
            }

            // Fall back to general AI analysis
            var response = await AnalyzeUserIntentWithContext(userMessage, context);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing user message: {ex.Message}");
            return $"I encountered an error processing your message: {ex.Message}\n\nPlease try rephrasing your question or let me know if you need help with a specific task on the {context} tab.";
        }
    }

    /// <summary>
    /// Get context-specific response for user messages
    /// </summary>
    private string GetContextSpecificResponse(string message, string context)
    {
        var lowerMessage = message.ToLower();

        switch (context.ToLower())
        {
            case "templatemapping":
                if (lowerMessage.Contains("help") || lowerMessage.Contains("how"))
                {
                    return "🗺️ **Template Mapping Help**\n\n" +
                           "Here's how to use this tab:\n\n" +
                           "1️⃣ **Load Template**: Click 'Load Template' to upload your Excel/PDF template\n" +
                           "2️⃣ **Start Mapping**: Click 'Start Mapping' to enter teaching mode\n" +
                           "3️⃣ **Click Cells**: Click on template cells to map data fields\n" +
                           "4️⃣ **Save Mappings**: Define field names and save your mappings\n\n" +
                           "💡 **Pro tip**: I work best with Excel templates - I can understand cell references like 'D15', 'B8'!";
                }
                if (lowerMessage.Contains("excel") || lowerMessage.Contains("cell"))
                {
                    return "📊 **Excel Template Mapping**\n\n" +
                           "I'm great at learning Excel layouts! Here's what I can do:\n\n" +
                           "• Map data to specific cells (A1, B5, D15, etc.)\n" +
                           "• Remember your layout preferences\n" +
                           "• Auto-fill templates with extracted data\n\n" +
                           "**Current Status**: Load a template and start mapping to begin teaching me your layout!";
                }
                break;

            case "dashboard":
                if (lowerMessage.Contains("stats") || lowerMessage.Contains("data"))
                {
                    return "📊 **Dashboard Insights**\n\n" +
                           "Your current statistics show:\n\n" +
                           "• Document processing activity\n" +
                           "• Pattern learning success rates\n" +
                           "• Recent processing history\n\n" +
                           "💡 **Want more data?** Upload more documents through the AI Learning tab or use the Dashboard action buttons!";
                }
                break;

            case "documents":
                if (lowerMessage.Contains("upload") || lowerMessage.Contains("process"))
                {
                    return "📋 **Document Management**\n\n" +
                           "This tab shows your document library. To process new documents:\n\n" +
                           "1️⃣ **Upload**: Go to AI Learning tab and click 'Upload Document'\n" +
                           "2️⃣ **Process**: I'll extract data using OCR and pattern recognition\n" +
                           "3️⃣ **Review**: Come back here to see results and export data\n\n" +
                           "**Need to re-process?** Just ask me and I can help!";
                }
                break;

            case "templates":
                if (lowerMessage.Contains("template") || lowerMessage.Contains("library"))
                {
                    return "📁 **Template Library Management**\n\n" +
                           "Organize your templates by:\n\n" +
                           "• **Categories**: Monthly Reports, Client Invoices, etc.\n" +
                           "• **Usage**: Track which templates you use most\n" +
                           "• **Mapping**: Each template remembers its field mappings\n\n" +
                           "💡 **Get started**: Upload a template through AI Learning or Template Mapping tabs!";
                }
                break;
        }

        return string.Empty; // No context-specific response found
    }

    /// <summary>
    /// Analyze user intent with context awareness
    /// </summary>
    private async Task<string> AnalyzeUserIntentWithContext(string message, string context)
    {
        await Task.Delay(500); // Simulate processing time
        
        var lowerMessage = message.ToLower();

        // Enhanced responses with context awareness
        if (lowerMessage.Contains("total") && lowerMessage.Contains("amount"))
        {
            return $"💰 **Total Amount Extraction** (Context: {context})\n\n" +
                   "Great! You mentioned a total amount. Here's how I can help:\n\n" +
                   "🔍 **Data Extraction**: I can find total amounts in bills, invoices, receipts\n" +
                   "🗺️ **Template Mapping**: I can map totals to specific cells (like D15 or B8)\n" +
                   "💱 **Multi-Currency**: I work with RM, USD, EUR, and other currencies\n\n" +
                   GetContextualNextStep(context, "total amount extraction");
        }

        if (lowerMessage.Contains("template") || lowerMessage.Contains("fill") || lowerMessage.Contains("export"))
        {
            return $"📋 **Template Management** (Context: {context})\n\n" +
                   "I can help you automate template filling! Here's the workflow:\n\n" +
                   "**🎯 Template Types I Support:**\n" +
                   "• Excel spreadsheets (best for cell mapping)\n" +
                   "• PDF forms (field-based mapping)\n" +
                   "• Word documents (bookmark mapping)\n\n" +
                   "**📤 Complete Workflow:**\n" +
                   "1. Upload documents → I extract data\n" +
                   "2. Upload templates → You teach me layouts\n" +
                   "3. I automatically fill templates with extracted data\n\n" +
                   GetContextualNextStep(context, "template management");
        }

        if (lowerMessage.Contains("help") || lowerMessage.Contains("how") || lowerMessage.Contains("what"))
        {
            return $"🆘 **Global AI Assistant Help** (Context: {context})\n\n" +
                   "I'm here to help you on ANY tab! Here's what I can do:\n\n" +
                   "**🤖 AI Learning**: Upload documents, teach me patterns\n" +
                   "**🗺️ Template Mapping**: Click-to-teach Excel cell mapping\n" +
                   "**📊 Dashboard**: Insights and statistics\n" +
                   "**📋 Documents**: Review processed documents\n" +
                   "**📁 Templates**: Manage template library\n\n" +
                   GetContextualNextStep(context, "general help");
        }

        // Default context-aware response
        return $"🤔 **I understand** (Context: {context}): \"{message}\"\n\n" +
               "I'm still learning! Since you're on the **{context}** tab, here are some things I can help with:\n\n" +
               GetContextualSuggestions(context) + "\n\n" +
               "💬 **Be specific!** Tell me exactly what you want to do and I'll guide you step by step!";
    }

    /// <summary>
    /// Get welcome message when switching to a new context
    /// </summary>
    private string GetContextWelcomeMessage(string context)
    {
        return context switch
        {
            "TemplateMappingView" or "Template Mapping" => 
                "🗺️ **Template Mapping Tab** - I can help you teach me Excel layouts and field mappings!",
            
            "Dashboard" => 
                "📊 **Dashboard Tab** - Here are your processing statistics and insights!",
            
            "Documents" => 
                "📋 **Documents Tab** - Review your processed documents or ask me to re-process any file!",
            
            "Templates" => 
                "📁 **Templates Tab** - Manage your template library and organize by categories!",
            
            "AI Learning" => 
                "🤖 **AI Learning Tab** - Upload documents and teach me your document patterns!",
            
            _ => string.Empty
        };
    }

    /// <summary>
    /// Get contextual suggestions based on current tab
    /// </summary>
    private string GetContextualSuggestions(string context)
    {
        return context switch
        {
            "TemplateMappingView" or "Template Mapping" => 
                "• 'Load my Excel template'\n• 'Help me map fields to cells'\n• 'How do I teach you layouts?'",
            
            "Dashboard" => 
                "• 'Show me my processing stats'\n• 'Upload more documents'\n• 'Export my data to Excel'",
            
            "Documents" => 
                "• 'Re-process this document'\n• 'Show me extraction results'\n• 'Upload a new document'",
            
            "Templates" => 
                "• 'Organize my templates'\n• 'Create a new template category'\n• 'Show template usage stats'",
            
            _ => "• Ask me about any document processing task\n• Tell me what you want to achieve\n• I'll guide you to the right tab and features"
        };
    }

    /// <summary>
    /// Get next step suggestion based on context and current task
    /// </summary>
    private string GetContextualNextStep(string context, string taskType)
    {
        return context switch
        {
            "TemplateMappingView" or "Template Mapping" when taskType.Contains("template") => 
                "**Next Step**: Click 'Load Template' to upload your Excel file, then 'Start Mapping' to begin teaching!",
            
            "Dashboard" when taskType.Contains("total") => 
                "**Next Step**: Go to AI Learning tab to upload a document with total amounts!",
            
            _ => "**Next Step**: Let me know what specific task you want to accomplish and I'll guide you!"
        };
    }

    /// <summary>
    /// Get contextual suggestion for proactive help
    /// </summary>
    private string GetContextualSuggestion(string context)
    {
        return context switch
        {
            "TemplateMappingView" when Messages.Count > 5 => 
                "💡 **Quick Tip**: Upload an Excel template and I'll show you how to map data to specific cells!",
            
            "Dashboard" when Messages.Count > 3 => 
                "📈 **Insight**: Your pattern success rate shows how well I'm learning your document types!",
            
            _ => string.Empty
        };
    }

    /// <summary>
    /// Add a message to the chat history with event notification
    /// </summary>
    private void AddMessage(string sender, string content, ChatMessageType type)
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderName = sender,
            Content = content,
            Type = type,
            Timestamp = DateTime.Now,
            BackgroundColor = type switch
            {
                ChatMessageType.User => "#E3F2FD",
                ChatMessageType.Bot => "#F3E5F5", 
                ChatMessageType.System => "#FFF3E0",
                _ => "#F5F5F5"
            },
            Alignment = type == ChatMessageType.User ? 
                Avalonia.Layout.HorizontalAlignment.Right : 
                Avalonia.Layout.HorizontalAlignment.Left
        };

        Messages.Add(message);
        MessageAdded?.Invoke(message);
        
        // Keep only last 100 messages for performance
        while (Messages.Count > 100)
        {
            Messages.RemoveAt(0);
        }
        
        Console.WriteLine($"💬 [{CurrentContext}] {sender}: {content.Substring(0, Math.Min(50, content.Length))}...");
    }

    #endregion
}

/// <summary>
/// Simple console logger implementation for desktop application.
/// </summary>
public class SimpleConsoleLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var typeName = typeof(T).Name;
        Console.WriteLine($"[{timestamp}] [{typeName}] [{logLevel}] {message}");
        
        if (exception != null)
        {
            Console.WriteLine($"Exception: {exception}");
        }
    }
}