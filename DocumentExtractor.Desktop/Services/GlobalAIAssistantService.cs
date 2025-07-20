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
    private readonly AIService _aiService;

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
            
            // Initialize AI Service for intelligent responses
            _aiService = new AIService();
            
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

    /// <summary>
    /// Provide real-time template mapping assistance based on current state
    /// </summary>
    public void ProvideMappingAssistance(string templateName, string selectedCell, int mappingCount, string detectedPattern)
    {
        if (CurrentContext != "TemplateMappingView" && CurrentContext != "Template Mapping")
            return;

        string message = "";

        // Provide contextual assistance based on state
        if (!string.IsNullOrEmpty(selectedCell) && mappingCount == 0)
        {
            message = $"🎯 **Great start!** You selected cell **{selectedCell}**\n\n" +
                     "💡 **Next steps**:\n" +
                     "1. Choose a field type from the 54 smart suggestions\n" +
                     "2. Click '💾 Save Field Mapping' to create your first mapping\n" +
                     "3. Check '💡 Save as reusable mapping rule' for automation\n\n" +
                     $"📊 **Template**: {templateName}\n" +
                     $"🎨 **Pattern**: {detectedPattern}";
        }
        else if (mappingCount > 0 && mappingCount < 3)
        {
            message = $"📈 **Good progress!** You have **{mappingCount} mappings** created\n\n" +
                     "🚀 **Suggestions**:\n" +
                     "• Add more field mappings for complete automation\n" +
                     "• Try '🎯 Apply Mapping Rules' to see rule engine in action\n" +
                     "• Use '⚙️ Manage Rules' to view and test your rules\n\n" +
                     $"🎯 **Current selection**: {selectedCell ?? "None"}";
        }
        else if (mappingCount >= 3)
        {
            message = $"🎉 **Excellent!** You have **{mappingCount} mappings** - Template is ready!\n\n" +
                     "⚡ **Advanced features**:\n" +
                     "• Test your rules with '🧪 Test' for visual previews\n" +
                     "• Export test results to Desktop\n" +
                     "• Use Tab 4 Excel selection system for bulk operations\n\n" +
                     "🏆 **You're becoming an expert at template mapping!**";
        }

        if (!string.IsNullOrEmpty(message))
        {
            AddMessage("AI Assistant", message, ChatMessageType.Bot);
        }
    }

    /// <summary>
    /// Provide rule management guidance
    /// </summary>
    public void ProvideRuleGuidance(int activeRules, int totalRules, double successRate)
    {
        if (CurrentContext != "TemplateMappingView" && CurrentContext != "Template Mapping")
            return;

        var message = $"⚙️ **Rule Engine Status**\n\n" +
                     $"🎯 **Active Rules**: {activeRules}/{totalRules}\n" +
                     $"📈 **Success Rate**: {successRate:P1}\n\n" +
                     "🔧 **Available Actions**:\n" +
                     "• **🧪 Test Rules**: See visual previews with 3-tab dialog\n" +
                     "• **🟢/🔴 Toggle**: Activate/deactivate rules\n" +
                     "• **📊 Statistics**: View performance insights\n" +
                     "• **🗑️ Delete**: Remove unused rules\n\n" +
                     "💡 **Pro tip**: Test rules before applying to ensure accuracy!";

        AddMessage("AI Assistant", message, ChatMessageType.System);
    }

    /// <summary>
    /// Announce new features and improvements
    /// </summary>
    public void AnnounceEnhancements()
    {
        if (CurrentContext != "TemplateMappingView" && CurrentContext != "Template Mapping")
            return;

        var message = "🚀 **NEW in Step 5: Template Mapping as AI Teaching Ground**\n\n" +
                     "✨ **Revolutionary Features**:\n\n" +
                     "🎓 **Conversational Learning**: Teach me through natural conversation\n" +
                     "🖼️ **Visual Document Teaching**: Drag & drop screenshots to train patterns\n" +
                     "📋 **Template Automation**: I learn your layout preferences permanently\n" +
                     "🔄 **Complete Workflow**: From document reading to template filling\n\n" +
                     "🎯 **How to Use the AI Teaching Ground**:\n" +
                     "• Upload documents → I learn extraction patterns\n" +
                     "• Map template fields → I remember layouts forever\n" +
                     "• Create rules → Automate bulk processing\n" +
                     "• Export results → Ready for client delivery\n\n" +
                     "💬 **Examples you can say**:\n" +
                     "• 'I have a TNB bill with total amount RM 245.67'\n" +
                     "• 'Put invoice totals in cell D15 for monthly reports'\n" +
                     "• 'Learn this Excel layout for client summaries'\n\n" +
                     "🌟 **This is the future of document automation!**";

        AddMessage("AI Assistant", message, ChatMessageType.Bot);
    }

    /// <summary>
    /// Provide conversational document learning assistance
    /// </summary>
    public void ProvideDocumentLearningGuidance(string documentType, int extractedFields, double confidence)
    {
        if (CurrentContext != "TemplateMappingView" && CurrentContext != "Template Mapping")
            return;

        var message = $"🎓 **Document Learning Mode Activated**\n\n" +
                     $"📄 **Document**: {documentType}\n" +
                     $"🔍 **Fields Extracted**: {extractedFields}\n" +
                     $"📊 **Confidence**: {confidence:P1}\n\n" +
                     $"💬 **Teach me more**:\n" +
                     $"• 'The total amount is RM 245.67 in the bottom right'\n" +
                     $"• 'Account number is always after \\'Account No:\\' '\n" +
                     $"• 'Due date format is DD/MM/YYYY'\n\n" +
                     $"🎯 **Next Steps**:\n" +
                     $"1. Confirm/correct my extractions\n" +
                     $"2. Teach me any missing patterns\n" +
                     $"3. Choose template for automatic filling\n\n" +
                     $"💡 **Pro tip**: The more you teach me, the smarter I become!";

        AddMessage("AI Assistant", message, ChatMessageType.Bot);
    }

    /// <summary>
    /// Provide template learning workflow guidance
    /// </summary>
    public void ProvideTemplateLearningGuidance(string templateName, string templateType, int mappedFields)
    {
        if (CurrentContext != "TemplateMappingView" && CurrentContext != "Template Mapping")
            return;

        var message = $"📋 **Template Learning Mode**\n\n" +
                     $"✅ **Template**: {templateName}\n" +
                     $"🎨 **Type**: {templateType}\n" +
                     $"📊 **Mapped Fields**: {mappedFields}\n\n" +
                     $"🎯 **Conversational Template Teaching**:\n\n" +
                     $"**Tell me what goes where:**\n" +
                     $"• 'Put total amounts in cell D15'\n" +
                     $"• 'Company names go in the header section'\n" +
                     $"• 'Due dates fill the right column'\n\n" +
                     $"**Ask me to help:**\n" +
                     $"• 'How do I automate monthly reports?'\n" +
                     $"• 'What template patterns do you know?'\n" +
                     $"• 'Show me bulk processing options'\n\n" +
                     $"🚀 **I'm learning your business workflow!**";

        AddMessage("AI Assistant", message, ChatMessageType.Bot);
    }

    /// <summary>
    /// Provide unified workflow guidance between document learning and template mapping
    /// </summary>
    public void ProvideUnifiedWorkflowGuidance()
    {
        if (CurrentContext != "TemplateMappingView" && CurrentContext != "Template Mapping")
            return;

        var message = "🌟 **Complete Document Workflow Automation**\n\n" +
                     "🔄 **How Everything Connects**:\n\n" +
                     "**1. Document Learning** 📖\n" +
                     "• Upload documents (TNB bills, invoices, reports)\n" +
                     "• I learn extraction patterns through conversation\n" +
                     "• Patterns saved permanently for future use\n\n" +
                     "**2. Template Mapping** 📋\n" +
                     "• Upload your business templates (Excel, PDF, Word)\n" +
                     "• Teach me layout through click-to-map or conversation\n" +
                     "• Create reusable mapping rules for automation\n\n" +
                     "**3. Automated Processing** ⚡\n" +
                     "• Bulk process hundreds of documents\n" +
                     "• Auto-fill templates with extracted data\n" +
                     "• Export ready-for-delivery reports\n\n" +
                     "💼 **Real Business Example**:\n" +
                     "1. Upload 100 TNB bills → I extract all totals/accounts\n" +
                     "2. Map monthly expense template → I learn cell positions\n" +
                     "3. Auto-generate 100 filled reports → Ready for clients\n\n" +
                     "🎯 **Try saying**: 'Show me the complete workflow for my business'";

        AddMessage("AI Assistant", message, ChatMessageType.Bot);
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
            // Use AI Service for intelligent responses
            var response = await _aiService.ProcessChatMessageAsync(userMessage, context);
            
            // If AI response is not specific enough, check for context-specific responses
            if (response.Length < 50 || response.Contains("I understand you're asking"))
            {
                var contextResponse = GetContextSpecificResponse(userMessage, context);
                if (!string.IsNullOrEmpty(contextResponse))
                {
                    return contextResponse;
                }
            }
            
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
            case "document teaching":
                if (lowerMessage.Contains("help") || lowerMessage.Contains("how"))
                {
                    return "🎓 **Document Teaching Hub Help**\n\n" +
                           "This is your AI training workspace! Here's how to use it:\n\n" +
                           "1️⃣ **Upload Documents**: Drag and drop documents for AI training\n" +
                           "2️⃣ **Choose Document Type**: Categorize (Bills, Invoices, Medical, etc.)\n" +
                           "3️⃣ **Review Extraction**: Check what I extracted and teach corrections\n" +
                           "4️⃣ **Train Patterns**: I learn from your feedback and improve\n\n" +
                           "💡 **Pro tip**: Upload multiple examples of the same document type for better learning!";
                }
                if (lowerMessage.Contains("upload") || lowerMessage.Contains("document"))
                {
                    return "📄 **Document Upload Training**\n\n" +
                           "I can learn from these document types:\n\n" +
                           "• **Utility Bills**: TNB, electricity, water, gas bills\n" +
                           "• **Invoices**: Business invoices, purchase orders\n" +
                           "• **Medical Reports**: Prescriptions, lab results, doctor notes\n" +
                           "• **Financial Documents**: Statements, receipts, tax forms\n\n" +
                           "**Upload multiple examples** of each type for better pattern learning!";
                }
                break;

            case "templatemapping":
                if (lowerMessage.Contains("help") || lowerMessage.Contains("how"))
                {
                    return "🗺️ **Template Mapping as AI Teaching Ground** (Step 5)\n\n" +
                           "🎓 **CONVERSATIONAL LEARNING WORKFLOW**:\n\n" +
                           "**1. Document Learning** 📖\n" +
                           "• Say: 'I have a TNB bill with total RM 245.67'\n" +
                           "• I learn extraction patterns through conversation\n" +
                           "• Upload documents → I extract and ask for feedback\n\n" +
                           "**2. Template Teaching** 📋\n" +
                           "• Say: 'Put total amounts in cell D15'\n" +
                           "• Click cells + tell me what goes there\n" +
                           "• I remember layouts permanently\n\n" +
                           "**3. Complete Automation** ⚡\n" +
                           "• Bulk process hundreds of documents\n" +
                           "• Auto-fill templates with learned patterns\n" +
                           "• Export ready reports for clients\n\n" +
                           "🌟 **Revolutionary**: Teach me like a human assistant!";
                }
                if (lowerMessage.Contains("learn") || lowerMessage.Contains("teach") || lowerMessage.Contains("conversation"))
                {
                    return "🎓 **Conversational AI Learning**\n\n" +
                           "💬 **Natural Teaching Examples**:\n\n" +
                           "**Document Learning**:\n" +
                           "• 'The total amount is RM 245.67 in bottom right'\n" +
                           "• 'Account numbers are always after \"Account No:\"'\n" +
                           "• 'TNB bills have meter readings in the middle'\n\n" +
                           "**Template Learning**:\n" +
                           "• 'Put company names in the header section'\n" +
                           "• 'Total amounts go in cell D15 for monthly reports'\n" +
                           "• 'Due dates fill column B'\n\n" +
                           "**Workflow Questions**:\n" +
                           "• 'How do I automate 100 TNB bills?'\n" +
                           "• 'Show me the complete business workflow'\n" +
                           "• 'What patterns do you know for invoices?'\n\n" +
                           "🚀 **I learn like a human - just tell me what you see!**";
                }
                if (lowerMessage.Contains("excel") || lowerMessage.Contains("cell"))
                {
                    return "📊 **Excel Intelligence System**\n\n" +
                           "🎯 **I understand your Excel perfectly!** Here's what I can do:\n\n" +
                           "• **Smart Cell Recognition**: A1, B5, D15, etc. with exact positioning\n" +
                           "• **Pattern Learning**: Remember your layout preferences across templates\n" +
                           "• **Auto-Detection**: Recognize E-commerce, Invoice, Financial patterns\n" +
                           "• **Rule Engine**: 100% success rate with smart mapping automation\n\n" +
                           "💡 **Pro Tips**:\n" +
                           "• Use column/row selection for bulk mapping\n" +
                           "• ESC key clears any selection\n" +
                           "• Rules apply automatically to similar templates";
                }
                if (lowerMessage.Contains("rule") || lowerMessage.Contains("mapping"))
                {
                    return "⚙️ **Advanced Rule System** (Step 4 Integration)\n\n" +
                           "🎯 **Rule Management Features**:\n\n" +
                           "• **Visual Rule Testing**: 3-tab enhanced dialog (📊 Summary | 🎯 Preview | ⚡ Interactive)\n" +
                           "• **Smart Suggestions**: 54 dynamic field types vs 9 hardcoded\n" +
                           "• **Pattern Detection**: Auto-detects template patterns\n" +
                           "• **Export Results**: Test results saved to Desktop\n\n" +
                           "🔧 **Quick Actions**:\n" +
                           "• Click '⚙️ Manage Rules' → View all rules\n" +
                           "• Click '🧪 Test' → See visual preview\n" +
                           "• Toggle 🟢/🔴 → Activate/deactivate rules\n" +
                           "• '📊 Rule Statistics' → Performance insights";
                }
                if (lowerMessage.Contains("field") || lowerMessage.Contains("type"))
                {
                    return "🎯 **Smart Field Mapping** (Enhanced UX)\n\n" +
                           "✨ **Unified Field System**:\n\n" +
                           "• **Single Input**: Field Name + Type combined (no redundancy)\n" +
                           "• **54 Smart Suggestions**: Database-driven field types\n" +
                           "• **Usage History**: Learn from your previous mappings\n" +
                           "• **AutoCompleteBox**: Type to find 'Invoice Total', 'Product SKU', etc.\n\n" +
                           "🚀 **Recent Improvements**:\n" +
                           "• Removed redundant Field Name input\n" +
                           "• Enhanced pattern detection\n" +
                           "• Real-time field suggestions\n" +
                           "• Context-aware validation";
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

        // Step 5: Conversational Learning Patterns
        if (lowerMessage.Contains("tnb") || lowerMessage.Contains("electricity") || lowerMessage.Contains("bill"))
        {
            return $"⚡ **TNB Bill Learning Mode** (Context: {context})\n\n" +
                   "I'm learning Malaysian utility bills! Tell me more:\n\n" +
                   "💬 **What I need to learn**:\n" +
                   "• Where is the total amount? ('Bottom right corner')\n" +
                   "• Account number location? ('Top left after Account No:')\n" +
                   "• Meter reading position? ('Middle section table')\n" +
                   "• Due date format? ('DD/MM/YYYY below amount')\n\n" +
                   "🎯 **Example teaching**: 'The total amount RM 245.67 is in bottom right corner next to Jumlah Perlu Dibayar'\n\n" +
                   "🚀 **Next**: Once I learn the pattern, I can process 100s of TNB bills automatically!";
        }

        if (lowerMessage.Contains("put") && (lowerMessage.Contains("cell") || lowerMessage.Contains("column") || lowerMessage.Contains("row")))
        {
            return $"📋 **Template Layout Learning** (Context: {context})\n\n" +
                   "Perfect! You're teaching me template layouts. I understand:\n\n" +
                   "🎯 **What you said**: \"{message}\"\n\n" +
                   "💡 **I'm learning**:\n" +
                   "• Field placement preferences\n" +
                   "• Cell/column/row positioning\n" +
                   "• Layout automation rules\n\n" +
                   "📝 **To make this permanent**:\n" +
                   "1. Click the Excel cell you mentioned\n" +
                   "2. Select the field type from dropdown\n" +
                   "3. Check '💡 Save as reusable mapping rule'\n\n" +
                   "🚀 **Result**: I'll remember this layout for all similar templates!";
        }

        if (lowerMessage.Contains("amount") && (lowerMessage.Contains("rm") || lowerMessage.Contains("usd") || lowerMessage.Contains("$")))
        {
            return $"💰 **Currency Pattern Learning** (Context: {context})\n\n" +
                   "Great! You mentioned a specific amount. I'm learning:\n\n" +
                   "🔍 **What I detected**: \"{message}\"\n\n" +
                   "📚 **Currency patterns I'm learning**:\n" +
                   "• RM amounts (Malaysian Ringgit)\n" +
                   "• USD amounts (US Dollars)\n" +
                   "• Amount positioning relative to text\n" +
                   "• Number format variations\n\n" +
                   "🎯 **Help me learn more**:\n" +
                   "• Where exactly do you see this amount?\n" +
                   "• What text appears before/after it?\n" +
                   "• Is this the total, subtotal, or tax amount?\n\n" +
                   "💡 **I'll remember this pattern for future documents!**";
        }

        if (lowerMessage.Contains("automate") || lowerMessage.Contains("bulk") || lowerMessage.Contains("100") || lowerMessage.Contains("many"))
        {
            return $"⚡ **Bulk Automation Workflow** (Context: {context})\n\n" +
                   "🚀 **You want bulk processing!** Here's how my learning enables automation:\n\n" +
                   "**📖 Step 1: Teach Document Patterns**\n" +
                   "• Upload 1-3 sample documents\n" +
                   "• Tell me: 'Total amount is here', 'Account number is there'\n" +
                   "• I learn the extraction patterns permanently\n\n" +
                   "**📋 Step 2: Teach Template Layout**\n" +
                   "• Upload your business template\n" +
                   "• Say: 'Put totals in D15', 'Put accounts in B8'\n" +
                   "• I learn the layout permanently\n\n" +
                   "**⚡ Step 3: Automated Processing**\n" +
                   "• Upload 100s of similar documents\n" +
                   "• I extract data using learned patterns\n" +
                   "• I fill templates using learned layouts\n" +
                   "• Export ready reports for your clients\n\n" +
                   "🎯 **Result**: 10 minutes of teaching = Hours of automation!";
        }

        if (lowerMessage.Contains("complete workflow") || lowerMessage.Contains("business workflow") || (lowerMessage.Contains("workflow") && lowerMessage.Contains("business")))
        {
            // Call the unified workflow guidance method
            ProvideUnifiedWorkflowGuidance();
            return "🔄 Complete workflow guidance provided above! Let me know which step you'd like to start with.";
        }

        if (lowerMessage.Contains("help") || lowerMessage.Contains("how") || lowerMessage.Contains("what"))
        {
            return $"🆘 **Global AI Assistant Help** (Context: {context})\n\n" +
                   "I'm here to help you on ANY tab! Here's what I can do:\n\n" +
                   "**🎓 Document Teaching**: Upload documents, train me on patterns\n" +
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
            
            "Document Teaching" => 
                "🎓 **Document Teaching Hub** - Upload documents and train me to read your specific document types!",
            
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
                "• 'Help me map Excel fields'\n• 'Show me rule management'\n• 'How do I create mapping rules?'\n• 'Explain the 54 field suggestions'\n• 'What's new in the enhanced system?'",
            
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