using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentExtractor.Desktop.Models;
using DocumentExtractor.Desktop.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System;

namespace DocumentExtractor.Desktop.ViewModels;

/// <summary>
/// Preview mode for the document interface
/// </summary>
public enum ViewMode
{
    InputOnly,
    OutputOnly,
    SplitView
}

/// <summary>
/// Document workflow state for context-aware layout
/// </summary>
public enum DocumentState
{
    NoDocument,
    DocumentLoaded,
    AfterTeaching,
    Processing,
    ReviewResults
}

// BoolToBorderThicknessConverter removed per user request - no longer needed

/// <summary>
/// Main ViewModel for the single-tab, three-panel conversational interface
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    #region Observable Properties

    // File Management
    [ObservableProperty]
    private int _inputFileCount = 0;

    [ObservableProperty]
    private int _templateCount = 0;

    [ObservableProperty]
    private bool _hasDocuments = false;

    [ObservableProperty]
    private bool _hasTemplates = false;

    [ObservableProperty]
    private bool _hasOutputData = false;

    [ObservableProperty]
    private string _outputTableText = "";

    [ObservableProperty]
    private string _outputDataInfo = "No data generated";

    [ObservableProperty]
    private bool _canExport = false;

    [ObservableProperty]
    private string _currentDocumentInfo = "No document loaded";

    [ObservableProperty]
    private string _currentTemplateInfo = "No template loaded";

    // Chat
    [ObservableProperty]
    private ObservableCollection<ChatMessage> _chatMessages = new();

    [ObservableProperty]
    private string _currentMessage = string.Empty;

    [ObservableProperty]
    private bool _hasChatMessages = false;

    // Document Viewing
    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // View Management
    [ObservableProperty]
    private ViewMode _currentViewMode = ViewMode.SplitView;

    [ObservableProperty]
    private DocumentState _currentDocumentState = DocumentState.NoDocument;

    [ObservableProperty]
    private bool _showOutputPanel = false;

    // Layout properties simplified - always show both panels

    [ObservableProperty]
    private bool _isInputVisible = true;

    [ObservableProperty]
    private bool _isOutputVisible = true;

    [ObservableProperty]
    private bool _isChatVisible = true;


    // Grid Length Properties
    [ObservableProperty]
    private GridLength _inputDocumentWidth = new GridLength(1, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _outputDocumentWidth = new GridLength(1, GridUnitType.Star);

    // Dynamic row heights for chat undocking
    [ObservableProperty]
    private GridLength _documentAreaHeight = new GridLength(7, GridUnitType.Star);

    [ObservableProperty]
    private GridLength _chatAreaHeight = new GridLength(3, GridUnitType.Star);



    // Panel Toggle Properties
    [ObservableProperty]
    private bool _isInputExpanded = false;

    [ObservableProperty]
    private bool _isOutputExpanded = false;

    [ObservableProperty]
    private string _inputToggleIcon = ">";

    [ObservableProperty]
    private string _outputToggleIcon = "<";

    [ObservableProperty]
    private bool _showInputToggle = true;

    [ObservableProperty]
    private bool _showOutputToggle = true;

    [ObservableProperty]
    private Thickness _inputPanelBorderThickness = new Thickness(0, 0, 1, 0);

    [ObservableProperty]
    private bool _isChatUndocked = false;

    // Document Preview
    [ObservableProperty]
    private int _currentPageIndex = 0;

    [ObservableProperty]
    private int _totalPages = 0;

    [ObservableProperty]
    private string _currentDocumentPath = string.Empty;

    // Services
    private readonly SimpleDocumentPreviewService _documentPreviewService = new();
    private readonly SimpleTableGeneratorService _tableGenerator = new();
    private readonly List<string> _loadedDocuments = new();

    #endregion

    #region Constructor

    public MainViewModel()
    {
        // Initialize with welcome message
        ChatMessages.Add(new ChatMessage
        {
            Content = "Welcome! I'm your AI document assistant. Upload documents and I'll help you extract data using conversation and screenshots.",
            SenderName = "AI Assistant",
            Type = ChatMessageType.Bot,
            Timestamp = DateTime.Now
        });

        // Generate sample output table for testing
        GenerateSampleOutputTable();

        // Initialize sample data for development
        InputFileCount = 0;
        TemplateCount = 0;
        UpdateVisibilityFlags();
        UpdateLayoutDimensions();
    }

    #endregion

    #region File Management

    public async Task BrowseAndLoadFiles()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        Title = "Select documents to process",
                        AllowMultiple = true,
                        FileTypeFilter = new[]
                        {
                            FilePickerFileTypes.Pdf,
                            FilePickerFileTypes.ImagePng,
                            FilePickerFileTypes.ImageJpg
                        }
                    });

                if (files.Any())
                {
                    await LoadDocuments(files);
                }
            }
        }
        catch (Exception ex)
        {
            await AddChatMessage($"Error loading files: {ex.Message}", false);
        }
    }

    private async Task LoadDocuments(IReadOnlyList<IStorageFile> files)
    {
        InputFileCount = files.Count;
        HasDocuments = true;
        
        // Load documents into preview service
        foreach (var file in files)
        {
            var filePath = file.Path.LocalPath;
            _loadedDocuments.Add(filePath);
            
            // Load document for preview
            var pageCount = await _documentPreviewService.LoadDocumentAsync(filePath);
            if (pageCount > 0)
            {
                await AddChatMessage($"✅ Loaded {file.Name} ({pageCount} page{(pageCount > 1 ? "s" : "")})", false);
            }
            else
            {
                await AddChatMessage($"❌ Failed to load {file.Name}", false);
            }
        }
        
        // Set current document to first loaded document
        if (_loadedDocuments.Count > 0)
        {
            CurrentDocumentPath = _loadedDocuments[0];
            CurrentPageIndex = 0;
            TotalPages = _documentPreviewService.GetPageCount(CurrentDocumentPath);
            
            if (files.Count == 1)
            {
                CurrentDocumentInfo = $"{files[0].Name}";
            }
            else
            {
                CurrentDocumentInfo = $"{files[0].Name} (1 of {files.Count})";
            }
        }

        await AddChatMessage($"Loaded {files.Count} document(s). I can see them now! What would you like me to extract?", false);
        
        StatusMessage = $"Loaded {files.Count} documents";
    }

    public void ShowFileDetails()
    {
        // TODO: Show popup with file list details
        AddChatMessage($"You have {InputFileCount} documents loaded. Click 'Browse Files' to add more.", false);
    }

    public void ShowTemplateDetails()
    {
        // TODO: Show popup with template mapping details
        AddChatMessage($"You have {TemplateCount} templates ready. Click 'Load Template' to add Excel templates.", false);
    }

    #endregion

    #region Document Navigation

    public void NavigateToPreviousDocument()
    {
        if (_loadedDocuments.Count > 0)
        {
            var currentIndex = _loadedDocuments.IndexOf(CurrentDocumentPath);
            if (currentIndex > 0)
            {
                CurrentDocumentPath = _loadedDocuments[currentIndex - 1];
                CurrentPageIndex = 0;
                TotalPages = _documentPreviewService.GetPageCount(CurrentDocumentPath);
                
                var fileName = System.IO.Path.GetFileName(CurrentDocumentPath);
                // Show the new position (currentIndex is 0-based, so currentIndex = new 1-based position)
                CurrentDocumentInfo = $"{fileName} ({currentIndex} of {_loadedDocuments.Count})";
                
                AddChatMessage($"Switched to previous document: {fileName}", false);
            }
            else
            {
                AddChatMessage("Already at the first document", false);
            }
        }
    }

    public void NavigateToNextDocument()
    {
        if (_loadedDocuments.Count > 0)
        {
            var currentIndex = _loadedDocuments.IndexOf(CurrentDocumentPath);
            if (currentIndex < _loadedDocuments.Count - 1)
            {
                CurrentDocumentPath = _loadedDocuments[currentIndex + 1];
                CurrentPageIndex = 0;
                TotalPages = _documentPreviewService.GetPageCount(CurrentDocumentPath);
                
                var fileName = System.IO.Path.GetFileName(CurrentDocumentPath);
                // Show the new position (currentIndex + 1 is 0-based, so +2 gives 1-based position)
                CurrentDocumentInfo = $"{fileName} ({currentIndex + 2} of {_loadedDocuments.Count})";
                
                AddChatMessage($"Switched to next document: {fileName}", false);
            }
            else
            {
                AddChatMessage("Already at the last document", false);
            }
        }
    }

    public void NavigateToPreviousPage()
    {
        if (!string.IsNullOrEmpty(CurrentDocumentPath) && CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            AddChatMessage($"Switched to page {CurrentPageIndex + 1}", false);
        }
    }

    public void NavigateToNextPage()
    {
        if (!string.IsNullOrEmpty(CurrentDocumentPath) && CurrentPageIndex < TotalPages - 1)
        {
            CurrentPageIndex++;
            AddChatMessage($"Switched to page {CurrentPageIndex + 1}", false);
        }
    }

    public void SelectDocumentArea(Point position)
    {
        if (!string.IsNullOrEmpty(CurrentDocumentPath))
        {
            // Convert canvas coordinates to document coordinates
            var docCoords = _documentPreviewService.GetDocumentCoordinates(position, CurrentDocumentPath, ZoomLevel);
            
            AddChatMessage($"You clicked at position ({docCoords.X:F0}, {docCoords.Y:F0}). Take a screenshot to teach me about this area!", false);
        }
        else
        {
            AddChatMessage("Please load a document first", false);
        }
    }

    #endregion

    #region Zoom Controls

    public async void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel * 1.2, 3.0);
        await OnZoomLevelChanged();
    }

    public async void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel / 1.2, 0.5);
        await OnZoomLevelChanged();
    }

    public async void ZoomToFit()
    {
        if (DocumentCanvas == null || string.IsNullOrEmpty(CurrentDocumentPath))
        {
            ZoomLevel = 1.0;
            await OnZoomLevelChanged();
            return;
        }

        // Get the actual document width from the preview service at 100% zoom
        var actualDocumentWidth = _documentPreviewService.GetDocumentWidth(CurrentDocumentPath);
        
        if (actualDocumentWidth <= 0)
        {
            // Fallback if we can't get document dimensions
            ZoomLevel = 1.0;
            await OnZoomLevelChanged();
            return;
        }

        // Calculate available width based on the grid layout state
        // Use fixed realistic panel widths to avoid the canvas feedback loop
        double availableWidth;
        
        if (IsInputExpanded)
        {
            // Input panel is expanded - assume typical window width of 1400px
            availableWidth = 1300; // Full window width minus margins/borders
        }
        else if (IsOutputExpanded)
        {
            // Output panel is expanded - input panel is hidden, so no fit needed
            Console.WriteLine("[DEBUG] ZoomToFit: Output panel expanded - input panel not visible");
            return;
        }
        else
        {
            // Split view - assume half of typical window width
            availableWidth = 650; // Half window width minus borders/margins
        }
        
        // Calculate zoom to fit document width to available panel width
        var optimalZoom = availableWidth / actualDocumentWidth;
        
        // Clamp zoom between reasonable limits (min 25%, max 300%)
        var newZoomLevel = Math.Max(0.25, Math.Min(3.0, optimalZoom));
        
        // Only update if zoom level actually changes to avoid unnecessary re-renders
        if (Math.Abs(ZoomLevel - newZoomLevel) > 0.01)
        {
            ZoomLevel = newZoomLevel;
            Console.WriteLine($"[DEBUG] ZoomToFit: Mode: {(IsInputExpanded ? "Input Expanded" : "Split View")}, Available width: {availableWidth}, Document width: {actualDocumentWidth}, Setting zoom: {ZoomLevel}");
            await OnZoomLevelChanged();
        }
        else
        {
            Console.WriteLine($"[DEBUG] ZoomToFit: Already at optimal zoom ({ZoomLevel:F3}), no change needed");
        }
    }

    #endregion

    #region Chat Functionality

    public async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        var userMessage = CurrentMessage.Trim();
        CurrentMessage = string.Empty;

        // Add user message
        await AddChatMessage(userMessage, true);

        // Process AI response (placeholder for now)
        await ProcessAIResponse(userMessage);
    }

    private async Task ProcessAIResponse(string userMessage)
    {
        try
        {
            // Use AIService for intelligent responses
            var aiService = new Services.AIService();
            
            // Build context with loaded documents
            var documentPaths = HasDocuments ? _loadedDocuments.ToList() : null;
            var context = HasDocuments ? "MainView - Documents Loaded" : "MainView - No Documents";
            
            var response = await aiService.ProcessChatMessageAsync(userMessage, context, documentPaths);
            await AddChatMessage(response.Content, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing AI response: {ex.Message}");
            await AddChatMessage("I encountered an error processing your request. Please try again.", false);
        }
    }

    public async Task AddChatMessage(string content, bool isFromUser)
    {
        await Task.Run(() =>
        {
            ChatMessages.Add(new ChatMessage
            {
                Content = content,
                SenderName = isFromUser ? "You" : "AI Assistant",
                Type = isFromUser ? ChatMessageType.User : ChatMessageType.Bot,
                Timestamp = DateTime.Now
            });
            HasChatMessages = ChatMessages.Count > 0;
        });
    }

    public void ClearChatHistory()
    {
        ChatMessages.Clear();
        AddChatMessage("Chat cleared! How can I help you with document extraction?", false);
    }

    #endregion

    #region Quick Actions

    public void TakeScreenshot()
    {
        AddChatMessage("Screenshot feature coming soon! You'll be able to take screenshots and draw annotations to teach me field locations.", false);
    }

    public void StartCalibration()
    {
        AddChatMessage("Calibration will test my accuracy on sample documents before processing your entire batch. Feature coming soon!", false);
    }

    public void StartProcessing()
    {
        if (!HasDocuments)
        {
            AddChatMessage("Please upload documents first before processing.", false);
            return;
        }

        if (!HasTemplates)
        {
            AddChatMessage("Please load an Excel template to show me where to put the extracted data.", false);
            return;
        }

        AddChatMessage("Processing feature coming soon! I'll extract data from all your documents using the patterns you taught me.", false);
    }

    #endregion

    #region Template Management

    public async Task LoadTemplate()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        Title = "Select Excel template",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Excel Files") { Patterns = new[] { "*.xlsx", "*.xls" } }
                        }
                    });

                if (files.Any())
                {
                    var template = files[0];
                    TemplateCount = 1;
                    HasTemplates = true;
                    CurrentTemplateInfo = template.Name;
                    
                    await AddChatMessage($"Loaded template: {template.Name}. Now show me where to put the extracted data using screenshots!", false);
                }
            }
        }
        catch (Exception ex)
        {
            await AddChatMessage($"Error loading template: {ex.Message}", false);
        }
    }

    public void PreviewTemplate()
    {
        AddChatMessage("Template preview feature coming soon! You'll see exactly where your extracted data will be placed.", false);
    }

    #endregion

    #region Drag and Drop Support

    /// <summary>
    /// Handles input documents dropped into the Input Document panel
    /// </summary>
    public async Task HandleInputDocuments(IEnumerable<string> filePaths)
    {
        try
        {
            var validFiles = filePaths.Where(f => IsInputDocumentType(f)).ToList();
            
            if (!validFiles.Any())
            {
                await AddChatMessage("❌ No valid input documents found. Supported formats: PDF, PNG, JPG, TIFF, BMP", false);
                return;
            }

            var fileCount = validFiles.Count;
            
            // Process input documents
            foreach (var filePath in validFiles)
            {
                var fileName = System.IO.Path.GetFileName(filePath);
                
                // Load the document into the preview system
                _loadedDocuments.Add(filePath);
                
                // Load document for preview
                var pageCount = await _documentPreviewService.LoadDocumentAsync(filePath);
                if (pageCount > 0)
                {
                    // Always set as current document (latest file becomes active)
                    CurrentDocumentPath = filePath;
                    CurrentPageIndex = 0;
                    TotalPages = pageCount;
                    
                    // Update document info to show position (1-based indexing)
                    var currentIndex = _loadedDocuments.Count;
                    CurrentDocumentInfo = $"{fileName} ({currentIndex} of {_loadedDocuments.Count})";
                    
                    // Update counters
                    InputFileCount = _loadedDocuments.Count;
                    HasDocuments = true;
                }
            }
            
            if (fileCount == 1)
            {
                await AddChatMessage($"✅ Loaded {validFiles[0].Split('/').Last()} for processing.", false);
            }
            else
            {
                await AddChatMessage($"✅ Loaded {fileCount} input documents successfully!", false);
            }
            
            UpdateVisibilityFlags();
            SetDocumentState(DocumentState.DocumentLoaded);
        }
        catch (Exception ex)
        {
            await AddChatMessage($"Sorry, I had trouble processing the input documents: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Handles output templates dropped into the Output Document panel
    /// </summary>
    public async Task HandleOutputTemplates(IEnumerable<string> filePaths)
    {
        try
        {
            var validFiles = filePaths.Where(f => IsOutputTemplateType(f)).ToList();
            
            if (!validFiles.Any())
            {
                await AddChatMessage("❌ No valid Excel templates found. Supported formats: XLSX, XLS", false);
                return;
            }

            // For now, handle only the first template (single pattern per session)
            var templateFile = validFiles.First();
            var fileName = System.IO.Path.GetFileName(templateFile);
            
            // Load the Excel template into the preview service with canvas dimensions
            // Use default large canvas size if canvas isn't initialized yet
            var canvasWidth = OutputCanvas != null && OutputCanvas.Width > 0 ? (int)OutputCanvas.Width : 1400;
            var canvasHeight = OutputCanvas != null && OutputCanvas.Height > 0 ? (int)OutputCanvas.Height : 900;
            Console.WriteLine($"[DEBUG] Using canvas dimensions: {canvasWidth}x{canvasHeight} for Excel rendering");
            var pageCount = await _documentPreviewService.LoadDocumentAsync(templateFile, canvasWidth, canvasHeight);
            if (pageCount > 0)
            {
                TemplateCount = 1;
                HasTemplates = true;
                CurrentTemplateInfo = fileName;
                
                // Render to output canvas if available
                if (OutputCanvas != null)
                {
                    await _documentPreviewService.RenderPageToCanvasAsync(templateFile, 0, OutputCanvas);
                }
                
                await AddChatMessage($"✅ Loaded template: {fileName} with preview. Ready for data extraction mapping!", false);
            }
            else
            {
                await AddChatMessage($"❌ Failed to load template preview for {fileName}", false);
                return;
            }
            
            if (validFiles.Count > 1)
            {
                await AddChatMessage($"ℹ️ Note: Multiple templates detected, but I'm using '{fileName}' for this session. Use 'New Session' to switch templates.", false);
            }
            
            UpdateVisibilityFlags();
        }
        catch (Exception ex)
        {
            await AddChatMessage($"Sorry, I had trouble processing the output template: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Handles multiple files dropped into the chat area for teaching/learning
    /// </summary>
    public async Task HandleDroppedFiles(IEnumerable<string> filePaths)
    {
        try
        {
            var validFiles = filePaths.Where(f => IsValidFileType(f)).ToList();
            
            if (!validFiles.Any())
            {
                await AddChatMessage("❌ No valid files found. Supported formats: PNG, JPG, PDF, XLSX", false);
                return;
            }

            var fileCount = validFiles.Count;
            
            // Show batch processing message
            if (fileCount == 1)
            {
                // Single file - use detailed processing
                await HandleDroppedFile(validFiles[0]);
                return;
            }
            
            // Multiple files - use batch processing
            await AddChatMessage($"📎 Processing {fileCount} files...", true);
            
            var successCount = 0;
            var failedFiles = new List<string>();
            var documentFiles = new List<string>();
            var templateFiles = new List<string>();
            
            foreach (var filePath in validFiles)
            {
                try
                {
                    var fileName = System.IO.Path.GetFileName(filePath);
                    var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                    
                    // Check if this is a document file (images, PDFs)
                    if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff" or ".pdf")
                    {
                        // Load the document into the preview system
                        _loadedDocuments.Add(filePath);
                        
                        // Load document for preview
                        var pageCount = await _documentPreviewService.LoadDocumentAsync(filePath);
                        if (pageCount > 0)
                        {
                            // Always set as current document (latest file becomes active)
                            CurrentDocumentPath = filePath;
                            CurrentPageIndex = 0;
                            TotalPages = pageCount;
                            
                            // Update document info to show position (1-based indexing)
                            var currentIndex = _loadedDocuments.Count;
                            CurrentDocumentInfo = $"{fileName} ({currentIndex} of {_loadedDocuments.Count})";
                            
                            // Update counters
                            InputFileCount = _loadedDocuments.Count;
                            HasDocuments = true;
                            
                            documentFiles.Add(fileName);
                            successCount++;
                        }
                        else
                        {
                            failedFiles.Add(fileName);
                        }
                    }
                    // Check if this is a template file (Excel)
                    else if (extension is ".xlsx" or ".xls")
                    {
                        TemplateCount = 1;
                        HasTemplates = true;
                        CurrentTemplateInfo = fileName;
                        
                        templateFiles.Add(fileName);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    failedFiles.Add(System.IO.Path.GetFileName(filePath));
                    // Log error silently for production
                }
            }
            
            // Provide batch summary
            if (successCount > 0)
            {
                if (documentFiles.Any())
                {
                    await AddChatMessage($"✅ Loaded {documentFiles.Count} document{(documentFiles.Count > 1 ? "s" : "")} successfully!", false);
                }
                
                if (templateFiles.Any())
                {
                    await AddChatMessage($"✅ Loaded {templateFiles.Count} template{(templateFiles.Count > 1 ? "s" : "")} successfully!", false);
                }
                
                // Show smart guidance based on what was loaded
                if (documentFiles.Any() && templateFiles.Any())
                {
                    await AddChatMessage("💡 Great! You have documents and templates loaded. You can now start mapping fields or ask me to extract data!", false);
                }
                else if (documentFiles.Any())
                {
                    await AddChatMessage("💡 Documents loaded! Use the navigation buttons to browse between them, or tell me what fields to extract.", false);
                }
                else if (templateFiles.Any())
                {
                    await AddChatMessage("💡 Templates loaded! Drop some documents to extract data into these templates.", false);
                }
            }
            
            if (failedFiles.Any())
            {
                await AddChatMessage($"❌ Failed to load {failedFiles.Count} file{(failedFiles.Count > 1 ? "s" : "")}: {string.Join(", ", failedFiles.Take(3))}{(failedFiles.Count > 3 ? "..." : "")}", false);
            }
            
            UpdateVisibilityFlags();
            
            // Update document state after successful load
            if (HasDocuments)
            {
                SetDocumentState(DocumentState.DocumentLoaded);
            }
        }
        catch (Exception ex)
        {
            await AddChatMessage($"Sorry, I had trouble processing the files: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Handles files dropped into the chat area for teaching/learning
    /// </summary>
    public async Task HandleDroppedFile(string filePath)
    {
        try
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            
            // Add user message showing the dropped file
            await AddChatMessage($"📎 {fileName}", true);
            
            // Check if this is a screenshot for teaching (by filename pattern)
            if (IsTeachingScreenshot(fileName))
            {
                await AddChatMessage($"📸 Teaching screenshot detected: {fileName}", false);
                await AddChatMessage("🎯 **Field Teaching Mode**", false);
                await AddChatMessage("I can see this is a screenshot for teaching me field locations! The annotation tools for drawing colored boxes around fields are coming in Week 2 of development.", false);
                await AddChatMessage("💡 For now, you can tell me in text what fields to extract: 'Extract the account number from the top-right' or 'Find the amount in the bottom section'", false);
                return;
            }
            
            // Check if this is a document file (images, PDFs) - load into preview
            if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff" or ".pdf")
            {
                // Load the document into the preview system
                _loadedDocuments.Add(filePath);
                
                // Load document for preview
                var pageCount = await _documentPreviewService.LoadDocumentAsync(filePath);
                if (pageCount > 0)
                {
                    // Always set as current document (latest file becomes active)
                    CurrentDocumentPath = filePath;
                    CurrentPageIndex = 0;
                    TotalPages = pageCount;
                    
                    // Update document info to show position (1-based indexing)
                    var currentIndex = _loadedDocuments.Count;
                    CurrentDocumentInfo = $"{fileName} ({currentIndex} of {_loadedDocuments.Count})";
                    
                    // Update counters
                    InputFileCount = _loadedDocuments.Count;
                    HasDocuments = true;
                    
                    await AddChatMessage($"✅ Loaded {fileName} ({pageCount} page{(pageCount > 1 ? "s" : "")}) into document preview!", false);
                    
                    // Render the document to the canvas
                    if (DocumentCanvas != null)
                    {
                        await RenderCurrentDocumentAsync(DocumentCanvas);
                    }
                    
                    await AddChatMessage("💡 You can now see the document in the preview panel! Tell me what fields to extract or click areas to teach me.", false);
                }
                else
                {
                    await AddChatMessage($"❌ Failed to load {fileName} for preview", false);
                }
            }
            // Check if this is a template file (Excel) - load into template preview
            else if (extension is ".xlsx" or ".xls")
            {
                TemplateCount = 1;
                HasTemplates = true;
                CurrentTemplateInfo = fileName;
                
                await AddChatMessage($"✅ Loaded template: {fileName}. This will be used for output formatting!", false);
                await AddChatMessage("💡 You could say: 'Map the extracted data to this template' or 'Show me the template structure'", false);
            }
            else
            {
                // For other file types, provide general guidance
                await AddChatMessage($"I can see you've uploaded: {fileName}. What would you like me to do with this file?", false);
            }
            
            UpdateVisibilityFlags();
        }
        catch (Exception ex)
        {
            await AddChatMessage($"Sorry, I had trouble processing that file: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Determines if a file is a screenshot intended for teaching field locations
    /// </summary>
    private static bool IsTeachingScreenshot(string fileName)
    {
        var lowerName = fileName.ToLowerInvariant();
        
        // Common screenshot naming patterns
        return lowerName.Contains("screenshot") ||
               lowerName.Contains("screen shot") ||
               lowerName.Contains("capture") ||
               lowerName.StartsWith("shot") ||
               lowerName.Contains("snap") ||
               // macOS default pattern: "Screenshot 2025-07-10 at 11.07.04 PM.png"
               (lowerName.StartsWith("screenshot") && lowerName.Contains("at")) ||
               // Windows default pattern: "Screenshot (1).png"
               lowerName.Contains("screenshot (");
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Opens folder picker for bulk file import
    /// </summary>
    public async Task ImportFolder()
    {
        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (lifetime?.MainWindow == null) return;

            var folderPicker = await lifetime.MainWindow.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Select folder containing documents",
                    AllowMultiple = false
                });

            if (folderPicker?.Count > 0)
            {
                var folder = folderPicker[0];
                await AddChatMessage($"📁 Selected folder: {folder.Name}", true);
                
                // Get all files from the folder
                var items = new List<IStorageItem>();
                await foreach (var item in folder.GetItemsAsync())
                {
                    items.Add(item);
                }
                
                var supportedFiles = items.OfType<IStorageFile>()
                    .Where(f => IsValidFileType(f.Path.LocalPath))
                    .ToList();
                
                if (supportedFiles.Any())
                {
                    await AddChatMessage($"Found {supportedFiles.Count} supported files. Processing...", false);
                    
                    // Process each file
                    foreach (var file in supportedFiles.Take(100)) // Limit to first 100 for now
                    {
                        await HandleDroppedFile(file.Path.LocalPath);
                    }
                    
                    if (supportedFiles.Count > 100)
                    {
                        await AddChatMessage($"Loaded first 100 files. {supportedFiles.Count - 100} more files available.", false);
                    }
                }
                else
                {
                    await AddChatMessage("No supported files found in the selected folder. Supported types: PDF, PNG, JPG, XLSX", false);
                }
            }
        }
        catch (Exception ex)
        {
            await AddChatMessage($"Error importing folder: {ex.Message}", false);
        }
    }

    private static bool IsValidFileType(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".pdf" or ".xlsx" or ".xls" or ".tiff" or ".bmp";
    }

    private static bool IsInputDocumentType(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".pdf" or ".tiff" or ".bmp";
    }

    private static bool IsOutputTemplateType(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".xlsx" or ".xls";
    }

    #endregion

    #region Document Preview Methods

    /// <summary>
    /// Renders all pages of the current document to the specified canvas for scrollable view
    /// </summary>
    public async Task RenderCurrentDocumentAsync(Canvas canvas)
    {
        if (!string.IsNullOrEmpty(CurrentDocumentPath))
        {
            // Use new all-pages rendering for scrollable view
            await _documentPreviewService.RenderAllPagesToCanvasAsync(
                CurrentDocumentPath, 
                canvas, 
                ZoomLevel);
        }
    }

    /// <summary>
    /// Gets the canvas reference for document rendering
    /// </summary>
    public Canvas? DocumentCanvas { get; set; }

    /// <summary>
    /// Gets the canvas reference for output template rendering
    /// </summary>
    public Canvas? OutputCanvas { get; set; }

    /// <summary>
    /// Called when zoom level changes to re-render the document
    /// </summary>
    private async Task OnZoomLevelChanged()
    {
        if (DocumentCanvas != null)
        {
            await RenderCurrentDocumentAsync(DocumentCanvas);
        }
    }

    #endregion

    #region View Mode Management

    /// <summary>
    /// Changes the current view mode
    /// </summary>
    public void SetViewMode(ViewMode mode)
    {
        CurrentViewMode = mode;
        UpdateViewLayout();
    }

    /// <summary>
    /// Updates the document state and automatically adjusts layout
    /// </summary>
    public void SetDocumentState(DocumentState state)
    {
        CurrentDocumentState = state;
        UpdateViewLayoutBasedOnState();
    }

    /// <summary>
    /// Generate sample output table for demonstration
    /// </summary>
    private void GenerateSampleOutputTable()
    {
        // Sample field mappings
        var fieldMappings = new Dictionary<string, string>
        {
            { "A", "Invoice #" },
            { "B", "Amount" },
            { "C", "Date" },
            { "D", "Customer" }
        };

        // Sample data
        var sampleData = new Dictionary<string, List<string>>
        {
            { "Invoice #", new List<string> { "INV-2024-001", "INV-2024-002", "INV-2024-003", "INV-2024-004" } },
            { "Amount", new List<string> { "$1,234.56", "$567.89", "$890.12", "$2,345.67" } },
            { "Date", new List<string> { "15/07/2024", "16/07/2024", "17/07/2024", "18/07/2024" } },
            { "Customer", new List<string> { "ABC Corp", "XYZ Ltd", "Tech Inc", "Global Co" } }
        };

        // Generate the table
        OutputTableText = _tableGenerator.GenerateExcelPreviewTable(fieldMappings, sampleData);
        HasOutputData = true;
        OutputDataInfo = "4 documents processed";
        CanExport = true;
    }

    /// <summary>
    /// Export the analysis results to Excel
    /// </summary>
    [RelayCommand]
    private async Task ExportToExcel()
    {
        await AddChatMessage("Excel export feature coming soon! This will save the extracted data to an Excel file.", false);
    }

    /// <summary>
    /// Updates the view layout based on current view mode
    /// </summary>
    private void UpdateViewLayout()
    {
        switch (CurrentViewMode)
        {
            case ViewMode.InputOnly:
                ShowOutputPanel = false;
                StatusMessage = "Viewing document";
                break;
            case ViewMode.OutputOnly:
                ShowOutputPanel = true;
                StatusMessage = "Viewing extraction results";
                break;
            case ViewMode.SplitView:
                ShowOutputPanel = true;
                StatusMessage = "Split view - document and results";
                break;
        }
    }

    /// <summary>
    /// Context-aware layout switching based on document workflow state
    /// </summary>
    private void UpdateViewLayoutBasedOnState()
    {
        switch (CurrentDocumentState)
        {
            case DocumentState.NoDocument:
                SetViewMode(ViewMode.InputOnly);
                ShowOutputPanel = false;
                break;
            case DocumentState.DocumentLoaded:
                SetViewMode(ViewMode.InputOnly);
                ShowOutputPanel = false;
                break;
            case DocumentState.AfterTeaching:
                SetViewMode(ViewMode.SplitView);
                ShowOutputPanel = true;
                break;
            case DocumentState.Processing:
                SetViewMode(ViewMode.SplitView);
                ShowOutputPanel = true;
                break;
            case DocumentState.ReviewResults:
                SetViewMode(ViewMode.OutputOnly);
                ShowOutputPanel = true;
                break;
        }
    }


    [RelayCommand]
    public void CycleViewMode()
    {
        var nextMode = CurrentViewMode switch
        {
            ViewMode.InputOnly => ViewMode.SplitView,
            ViewMode.SplitView => ViewMode.OutputOnly,
            ViewMode.OutputOnly => ViewMode.InputOnly,
            _ => ViewMode.InputOnly
        };
        SetViewMode(nextMode);
    }

    #endregion

    #region New 4-Panel Layout Management

    /// <summary>
    /// Toggles the input panel between expanded and split view
    /// </summary>
    public void ToggleInputView()
    {
        IsInputExpanded = !IsInputExpanded;
        UpdateLayoutDimensions();
    }

    /// <summary>
    /// Toggles the output panel between expanded and split view
    /// </summary>
    public void ToggleOutputView()
    {
        IsOutputExpanded = !IsOutputExpanded;
        UpdateLayoutDimensions();
    }

    /// <summary>
    /// Updates grid layout dimensions based on panel toggle states
    /// </summary>
    public void UpdateLayoutDimensions()
    {
        // Handle panel expansions
        if (IsInputExpanded)
        {
            // Input panel expanded - hide output
            InputDocumentWidth = new GridLength(1, GridUnitType.Star);
            OutputDocumentWidth = new GridLength(0, GridUnitType.Pixel);
            IsInputVisible = true;
            IsOutputVisible = false;
            
            InputToggleIcon = "<<";
            ShowInputToggle = true;
            ShowOutputToggle = false;
            InputPanelBorderThickness = new Thickness(0);
        }
        else if (IsOutputExpanded)
        {
            // Output panel expanded - hide input
            InputDocumentWidth = new GridLength(0, GridUnitType.Pixel);
            OutputDocumentWidth = new GridLength(1, GridUnitType.Star);
            IsInputVisible = false;
            IsOutputVisible = true;
            
            OutputToggleIcon = ">>";
            ShowInputToggle = false;
            ShowOutputToggle = true;
            InputPanelBorderThickness = new Thickness(0);
        }
        else
        {
            // Split view - show both panels
            InputDocumentWidth = new GridLength(1, GridUnitType.Star);
            OutputDocumentWidth = new GridLength(1, GridUnitType.Star);
            IsInputVisible = true;
            IsOutputVisible = true;
            
            InputToggleIcon = ">";
            OutputToggleIcon = "<";
            ShowInputToggle = true;
            ShowOutputToggle = true;
            InputPanelBorderThickness = new Thickness(0, 0, 1, 0);
        }
        
        // Handle chat undocking - Input and Analysis panels expand when chat is undocked
        if (IsChatUndocked)
        {
            // Chat is undocked, so document area takes full height and chat area collapses
            DocumentAreaHeight = new GridLength(1, GridUnitType.Star);
            ChatAreaHeight = new GridLength(0, GridUnitType.Pixel);
            IsChatVisible = false;
        }
        else
        {
            // Chat is docked, normal 70/30 split
            DocumentAreaHeight = new GridLength(7, GridUnitType.Star);
            ChatAreaHeight = new GridLength(3, GridUnitType.Star);
            IsChatVisible = true;
        }
    }

    #endregion

    #region Helper Methods

    private void UpdateVisibilityFlags()
    {
        HasDocuments = InputFileCount > 0;
        HasTemplates = TemplateCount > 0;
    }

    #endregion
}