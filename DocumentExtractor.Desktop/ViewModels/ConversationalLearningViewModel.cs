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

namespace DocumentExtractor.Desktop.ViewModels;

/// <summary>
/// View model for the Conversational Learning interface.
/// Manages chat messages, document uploads, and AI learning interactions.
/// </summary>
public partial class ConversationalLearningViewModel : ViewModelBase
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
    private string _statusMessage = "Ready to learn!";

    [ObservableProperty]
    private ChatSession? _currentSession = null;

    #endregion

    #region Constructor

    public ConversationalLearningViewModel()
    {
        try
        {
            // Initialize services
            _context = new DocumentExtractionContext();
            
            // For now, we'll create a simple mock document processor
            // In the future, this will be injected via dependency injection
            var textExtractorLogger = new SimpleConsoleLogger<TesseractTextExtractor>();
            var textExtractor = new TesseractTextExtractor(textExtractorLogger);
            var processorLogger = new SimpleConsoleLogger<RealDocumentProcessor>();
            _documentProcessor = new RealDocumentProcessor(textExtractor, _context, processorLogger);
            
            // Initialize messages collection
            Messages = new ObservableCollection<ChatMessage>();
            
            Console.WriteLine("🤖 ConversationalLearningViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error initializing ConversationalLearningViewModel: {ex.Message}");
            StatusMessage = $"Initialization error: {ex.Message}";
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage) || IsProcessing)
            return;

        try
        {
            IsProcessing = true;
            CanSendMessage = false;
            
            var userMessage = CurrentMessage.Trim();
            CurrentMessage = string.Empty;

            // Add user message to chat
            AddMessage("You", userMessage, ChatMessageType.User);

            // Process the message and get AI response
            var response = await ProcessUserMessage(userMessage);
            
            // Add AI response to chat
            AddMessage("AI Assistant", response, ChatMessageType.Bot);

            StatusMessage = "Message processed successfully";
        }
        catch (Exception ex)
        {
            AddMessage("System", $"Error processing message: {ex.Message}", ChatMessageType.System);
            StatusMessage = $"Error: {ex.Message}";
            Console.WriteLine($"❌ Error in SendMessage: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
            CanSendMessage = true;
        }
    }

    [RelayCommand]
    private async Task UploadDocument()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Opening file dialog...";

            // Implement Avalonia file picker
            var files = await OpenFilePickerAsync("Select Document or Template", new[] {
                "PDF files (*.pdf)",
                "Image files (*.png;*.jpg;*.jpeg;*.tiff)",
                "Excel files (*.xlsx;*.xls)",
                "Word files (*.docx;*.doc)",
                "All files (*.*)"
            });

            if (files != null && files.Count > 0)
            {
                var filePath = files[0];
                var fileName = Path.GetFileName(filePath);
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                AddMessage("You", $"📎 Uploaded: {fileName}", ChatMessageType.User);

                // Determine if it's a template or document
                if (IsTemplateFile(extension))
                {
                    await HandleTemplateUpload(filePath, fileName);
                }
                else
                {
                    await HandleDocumentUpload(filePath, fileName);
                }
            }
            else
            {
                AddMessage("AI Assistant", "No file selected. Feel free to upload a document or template when you're ready!", ChatMessageType.Bot);
            }

            StatusMessage = "Ready for next upload";
        }
        catch (Exception ex)
        {
            AddMessage("System", $"Error uploading file: {ex.Message}", ChatMessageType.System);
            StatusMessage = $"Upload error: {ex.Message}";
            Console.WriteLine($"❌ Error in UploadDocument: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task UploadTemplate()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Opening template file dialog...";

            var files = await OpenFilePickerAsync("Select Template File", new[] {
                "Excel files (*.xlsx;*.xls)",
                "Word files (*.docx;*.doc)", 
                "PDF files (*.pdf)",
                "All files (*.*)"
            });

            if (files != null && files.Count > 0)
            {
                var filePath = files[0];
                var fileName = Path.GetFileName(filePath);
                
                AddMessage("You", $"📋 Uploaded template: {fileName}", ChatMessageType.User);
                await HandleTemplateUpload(filePath, fileName);
            }

            StatusMessage = "Ready for next template upload";
        }
        catch (Exception ex)
        {
            AddMessage("System", $"Error uploading template: {ex.Message}", ChatMessageType.System);
            StatusMessage = $"Template upload error: {ex.Message}";
            Console.WriteLine($"❌ Error in UploadTemplate: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialize the chat with a welcome message.
    /// </summary>
    public void InitializeWelcomeMessage()
    {
        if (Messages.Count == 0)
        {
            AddMessage("AI Assistant", 
                "👋 Hello! I'm your AI Learning Assistant. I'm here to help you teach me how to:\n\n" +
                "📄 **Extract data from documents** (bills, invoices, reports)\n" +
                "📋 **Fill templates** with the extracted data\n" +
                "🖋️ **Read handwritten text** (medical reports, field notes)\n\n" +
                "Let's start! You can:\n" +
                "• Upload a document and I'll learn to read it\n" +
                "• Tell me about a document type you work with\n" +
                "• Ask me questions about document processing\n\n" +
                "What would you like to do first?", 
                ChatMessageType.Bot);
            
            StatusMessage = "AI Assistant ready to help you learn!";
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Process user message and generate appropriate AI response.
    /// </summary>
    private async Task<string> ProcessUserMessage(string userMessage)
    {
        try
        {
            // Analyze user intent and generate response
            var response = await AnalyzeUserIntent(userMessage);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing user message: {ex.Message}");
            return $"I encountered an error processing your message: {ex.Message}\n\nPlease try rephrasing your question or contact support if the issue persists.";
        }
    }

    /// <summary>
    /// Analyze user intent and generate appropriate response.
    /// </summary>
    private async Task<string> AnalyzeUserIntent(string message)
    {
        await Task.Delay(500); // Simulate processing time
        
        var lowerMessage = message.ToLower();

        // Document learning patterns
        if (lowerMessage.Contains("total") && lowerMessage.Contains("amount"))
        {
            return "💰 Great! You mentioned a total amount. Can you tell me:\n\n" +
                   "1. What type of document is this? (bill, invoice, receipt)\n" +
                   "2. Where exactly do you see the total amount? (top right, bottom, next to specific text)\n" +
                   "3. What currency is it in? (RM, USD, EUR)\n\n" +
                   "This helps me learn the pattern for extracting totals from similar documents!";
        }

        if (lowerMessage.Contains("tnb") || lowerMessage.Contains("electricity"))
        {
            return "⚡ TNB electricity bill detected! I'm learning about Malaysian utility bills. Can you help me understand:\n\n" +
                   "• Account number location?\n" +
                   "• Meter reading section?\n" +
                   "• Due date position?\n" +
                   "• Total amount (usually 'Jumlah Perlu Dibayar')?\n\n" +
                   "Show me any specific text patterns you see, and I'll remember them for future TNB bills!";
        }

        if (lowerMessage.Contains("medical") || lowerMessage.Contains("doctor") || lowerMessage.Contains("handwritten"))
        {
            return "🏥 Medical document learning mode activated! Handwritten medical documents are challenging but I can learn. Help me with:\n\n" +
                   "• Patient name location\n" +
                   "• Diagnosis section\n" +
                   "• Medication details\n" +
                   "• Doctor's signature area\n\n" +
                   "💡 **Pro tip**: I learn doctor's handwriting styles over time. The more corrections you give me, the better I become!";
        }

        if (lowerMessage.Contains("template") || lowerMessage.Contains("fill") || lowerMessage.Contains("export"))
        {
            return "📋 **Template Learning Mode Activated!**\n\n" +
                   "I can learn how to fill your business templates automatically! Here's how we can work together:\n\n" +
                   "**🎯 What I can learn:**\n" +
                   "• Excel spreadsheets (cell mappings like 'D15', 'B8')\n" +
                   "• PDF forms (field positions and names)\n" +
                   "• Word documents (bookmark and table locations)\n\n" +
                   "**📤 How to get started:**\n" +
                   "1. Click the '📋 Template' button to upload your template\n" +
                   "2. Tell me what data goes where (e.g., 'total amount in cell D15')\n" +
                   "3. I'll remember the layout for future use\n\n" +
                   "**🚀 Template examples I work great with:**\n" +
                   "• Monthly expense reports\n" +
                   "• Client billing summaries\n" +
                   "• Regulatory compliance forms\n" +
                   "• Facility management reports\n\n" +
                   "What type of template would you like to teach me about?";
        }

        if (lowerMessage.Contains("help") || lowerMessage.Contains("how") || lowerMessage.Contains("what"))
        {
            return "🆘 I'm here to help! Here's what I can do:\n\n" +
                   "**📖 Document Learning:**\n" +
                   "• Teach me to read bills, invoices, reports\n" +
                   "• Handle handwritten documents\n" +
                   "• Learn specific company formats\n\n" +
                   "**📝 Template Learning:**\n" +
                   "• Show me your Excel/PDF templates\n" +
                   "• Learn where to put extracted data\n" +
                   "• Automate your workflow\n\n" +
                   "**🎯 Just tell me:** 'I have a [document type] and want to extract [specific data]'";
        }

        // Default conversational response
        return "🤔 I understand you said: \"" + message + "\"\n\n" +
               "I'm still learning! Could you help me by being more specific? For example:\n\n" +
               "• 'I have a TNB bill with total amount RM 245.67'\n" +
               "• 'This medical report has handwritten diagnosis'\n" +
               "• 'I need to fill an Excel template with invoice data'\n\n" +
               "The more details you give me, the better I can learn your document patterns! 🚀";
    }

    /// <summary>
    /// Add a message to the chat history.
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
        
        // Keep only last 100 messages for performance
        while (Messages.Count > 100)
        {
            Messages.RemoveAt(0);
        }
        
        Console.WriteLine($"💬 {sender}: {content.Substring(0, Math.Min(50, content.Length))}...");
    }

    /// <summary>
    /// Open file picker dialog for selecting files.
    /// </summary>
    private async Task<List<string>?> OpenFilePickerAsync(string title, string[] filters)
    {
        try
        {
            // Get the main window to use as parent for the file picker
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow : null;

            if (mainWindow == null)
            {
                AddMessage("System", "❌ Cannot open file picker - no main window found", ChatMessageType.System);
                return null;
            }

            // Create file picker options
            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = CreateFileTypeFilters(filters)
            };

            // Open the file picker
            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(options);
            
            if (result != null && result.Count > 0)
            {
                var selectedFile = result[0];
                var filePath = selectedFile.Path.LocalPath;
                
                AddMessage("System", $"📁 File selected: {Path.GetFileName(filePath)}", ChatMessageType.System);
                return new List<string> { filePath };
            }
            else
            {
                AddMessage("System", $"📁 File picker cancelled", ChatMessageType.System);
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error opening file picker: {ex.Message}");
            AddMessage("System", $"❌ Error opening file picker: {ex.Message}", ChatMessageType.System);
            return null;
        }
    }

    /// <summary>
    /// Create file type filters for the file picker.
    /// </summary>
    private List<Avalonia.Platform.Storage.FilePickerFileType> CreateFileTypeFilters(string[] filters)
    {
        var fileTypes = new List<Avalonia.Platform.Storage.FilePickerFileType>();
        
        foreach (var filter in filters)
        {
            if (filter.Contains("PDF"))
            {
                fileTypes.Add(new Avalonia.Platform.Storage.FilePickerFileType("PDF Files")
                {
                    Patterns = new[] { "*.pdf" }
                });
            }
            else if (filter.Contains("Image"))
            {
                fileTypes.Add(new Avalonia.Platform.Storage.FilePickerFileType("Image Files")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.tiff", "*.bmp" }
                });
            }
            else if (filter.Contains("Excel"))
            {
                fileTypes.Add(new Avalonia.Platform.Storage.FilePickerFileType("Excel Files")
                {
                    Patterns = new[] { "*.xlsx", "*.xls" }
                });
            }
            else if (filter.Contains("Word"))
            {
                fileTypes.Add(new Avalonia.Platform.Storage.FilePickerFileType("Word Documents")
                {
                    Patterns = new[] { "*.docx", "*.doc" }
                });
            }
            else if (filter.Contains("All"))
            {
                fileTypes.Add(new Avalonia.Platform.Storage.FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                });
            }
        }
        
        return fileTypes;
    }

    /// <summary>
    /// Check if file extension indicates a template file.
    /// </summary>
    private bool IsTemplateFile(string extension)
    {
        var templateExtensions = new[] { ".xlsx", ".xls", ".docx", ".doc", ".pdf" };
        return templateExtensions.Contains(extension);
    }

    /// <summary>
    /// Handle template file upload and start teaching workflow.
    /// </summary>
    private async Task HandleTemplateUpload(string filePath, string fileName)
    {
        try
        {
            // Validate file exists
            if (!File.Exists(filePath))
            {
                AddMessage("AI Assistant", $"❌ Cannot find the uploaded file: {fileName}", ChatMessageType.Bot);
                return;
            }

            var fileInfo = new FileInfo(filePath);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            AddMessage("AI Assistant", 
                $"📋 **Template Successfully Uploaded!**\n\n" +
                $"✅ **File Details:**\n" +
                $"• Name: {fileName}\n" +
                $"• Type: {GetTemplateTypeDescription(extension)}\n" +
                $"• Size: {fileInfo.Length / 1024:F1} KB\n" +
                $"• Location: {filePath}\n\n" +
                $"🎯 **Next Steps - Template Learning:**\n\n" +
                $"**Tell me about your template:**\n" +
                $"• What type of report is this? (Monthly/Quarterly/Client Report)\n" +
                $"• What extracted data should go into it?\n\n" +
                $"**Examples you can say:**\n" +
                $"• 'This is a monthly expense report'\n" +
                $"• 'Put total amounts in the summary section'\n" +
                $"• 'I need account numbers and dates filled in'\n\n" +
                $"🚀 **Coming Soon:**\n" +
                $"• Visual field mapping interface\n" +
                $"• Click-to-teach cell positions\n" +
                $"• Automatic template filling\n\n" +
                $"**What type of template is this and what data should I learn to extract for it?**", 
                ChatMessageType.Bot);

            // Store template information for future use
            await StoreTemplateInformation(filePath, fileName, extension);
        }
        catch (Exception ex)
        {
            AddMessage("AI Assistant", 
                $"❌ **Error Processing Template**\n\n" +
                $"There was an issue processing '{fileName}':\n{ex.Message}\n\n" +
                $"Please try uploading the template again.", 
                ChatMessageType.Bot);
            
            Console.WriteLine($"❌ Error in HandleTemplateUpload: {ex}");
        }
    }

    /// <summary>
    /// Get user-friendly description of template type.
    /// </summary>
    private string GetTemplateTypeDescription(string extension)
    {
        return extension switch
        {
            ".xlsx" or ".xls" => "Excel Spreadsheet (great for cell mapping)",
            ".docx" or ".doc" => "Word Document (good for form fields)", 
            ".pdf" => "PDF Form (works with field names)",
            _ => "Document Template"
        };
    }

    /// <summary>
    /// Store template information in database for future use.
    /// </summary>
    private async Task StoreTemplateInformation(string filePath, string fileName, string extension)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            
            // Create Template entity
            var template = new Template
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                Description = $"Template uploaded from {fileName}",
                Category = "Uncategorized", // User can categorize later through conversation
                FilePath = filePath,
                FileName = fileName,
                FileExtension = extension,
                FileSizeBytes = fileInfo.Length,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            // Store in database
            _context.Templates.Add(template);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"📋 Template stored in database: {template.Name} (ID: {template.Id})");
            
            // Add success message to chat
            AddMessage("System", 
                $"✅ Template saved to library: '{template.Name}'\n" +
                $"• ID: {template.Id[..8]}...\n" +
                $"• Size: {fileInfo.Length / 1024:F1} KB\n" +
                $"• Ready for field mapping!", 
                ChatMessageType.System);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error storing template: {ex.Message}");
            AddMessage("System", 
                $"❌ Error saving template to database: {ex.Message}", 
                ChatMessageType.System);
        }
    }

    /// <summary>
    /// Handle document upload and start extraction workflow.
    /// </summary>
    private async Task HandleDocumentUpload(string filePath, string fileName)
    {
        await Task.Delay(500); // Simulate processing
        
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        if (extension == ".pdf" || extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tiff")
        {
            AddMessage("AI Assistant", 
                $"🔍 **Document Analysis Started!**\n\n" +
                $"I'm analyzing '{fileName}' for data extraction. Here's what I'll do:\n\n" +
                $"1️⃣ **OCR Processing**: Extract text from your document\n" +
                $"2️⃣ **Pattern Recognition**: Look for known patterns (dates, amounts, account numbers)\n" +
                $"3️⃣ **Learning Questions**: Ask you about uncertain extractions\n\n" +
                $"**Expected processing time**: 30-60 seconds\n\n" +
                $"🎯 **What I'm looking for:**\n" +
                $"• Total amounts and currency\n" +
                $"• Account numbers and references\n" +
                $"• Dates (bill date, due date)\n" +
                $"• Company/supplier information\n\n" +
                $"Once processing completes, I'll show you what I found and ask for your confirmation on uncertain items!", 
                ChatMessageType.Bot);
                
            // Start actual document processing
            _ = ProcessDocumentAsync(filePath, fileName);
        }
        else
        {
            AddMessage("AI Assistant", 
                $"🤔 I see '{fileName}' but I'm not sure how to process this file type.\n\n" +
                $"**I work best with:**\n" +
                $"• PDF documents\n" +
                $"• Images (PNG, JPG, TIFF)\n" +
                $"• Scanned documents\n\n" +
                $"Could you upload a PDF or image file instead?", 
                ChatMessageType.Bot);
        }
    }

    /// <summary>
    /// Process document with OCR and pattern recognition.
    /// </summary>
    private async Task ProcessDocumentAsync(string filePath, string fileName)
    {
        try
        {
            AddMessage("AI Assistant", "⚙️ Starting OCR processing...", ChatMessageType.System);
            
            // Use the real document processor
            var document = await _documentProcessor.ProcessDocumentAsync(filePath);
            
            if (document.Status == ProcessingStatus.Completed)
            {
                AddMessage("AI Assistant", 
                    $"✅ **Processing Complete!**\n\n" +
                    $"📊 **Extraction Results:**\n" +
                    $"• Document Type: {document.DocumentType}\n" +
                    $"• Supplier: {document.Supplier}\n" +
                    $"• Fields Found: {document.Fields.Count}\n" +
                    $"• Confidence: {document.OverallConfidence:P1}\n\n" +
                    $"🔍 **Top Extracted Fields:**\n" +
                    string.Join("\n", document.Fields.Take(5).Select(f => $"• {f.FieldName}: {f.Value} ({f.Confidence:P0} confidence)")) +
                    $"\n\n💬 **Questions for you:**\n" +
                    $"• Are these extractions correct?\n" +
                    $"• Which template should I use to format this data?\n" +
                    $"• Any corrections needed?", 
                    ChatMessageType.Bot);
            }
            else
            {
                AddMessage("AI Assistant", 
                    $"❌ **Processing Failed**\n\n" +
                    $"Error: {document.ErrorMessage}\n\n" +
                    $"**Possible solutions:**\n" +
                    $"• Check if the image is clear and readable\n" +
                    $"• Try a higher resolution scan\n" +
                    $"• Ensure the document is not rotated", 
                    ChatMessageType.Bot);
            }
        }
        catch (Exception ex)
        {
            AddMessage("AI Assistant", 
                $"❌ **Processing Error**\n\n" +
                $"Something went wrong: {ex.Message}\n\n" +
                $"Please try uploading the document again.", 
                ChatMessageType.Bot);
            
            Console.WriteLine($"❌ Error processing document: {ex}");
        }
    }

    #endregion

    #region Cleanup

    public override void Dispose()
    {
        _context?.Dispose();
        base.Dispose();
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