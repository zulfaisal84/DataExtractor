using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DocumentExtractor.Desktop.ViewModels;
using DocumentExtractor.Desktop.Models;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Avalonia;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Simplified main view - ChatGPT style interface for document intelligence
/// </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        
        // Set up document canvas reference for rendering
        if (DataContext is MainViewModel vm)
        {
            // Check if InputDocumentCanvas exists (new 4-panel layout)
            var inputCanvas = this.FindControl<Canvas>("InputDocumentCanvas");
            if (inputCanvas != null)
            {
                vm.DocumentCanvas = inputCanvas;
            }
            else
            {
                // Fallback to old DocumentCanvas if it exists
                var oldCanvas = this.FindControl<Canvas>("DocumentCanvas");
                if (oldCanvas != null)
                {
                    vm.DocumentCanvas = oldCanvas;
                }
            }

            // Set up output canvas reference for Excel template rendering
            var outputCanvas = this.FindControl<Canvas>("OutputDocumentCanvas");
            if (outputCanvas != null)
            {
                vm.OutputCanvas = outputCanvas;
            }
        }
        
        // Set up global drag-and-drop after control loads
        this.Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SetupGlobalDragDrop();
        SetupPanelSpecificDragDrop();
    }
    
    /// <summary>
    /// Set up global drag-and-drop for the entire control
    /// </summary>
    private void SetupGlobalDragDrop()
    {
        // Make the entire control accept drag-and-drop
        this.AddHandler(DragDrop.DragOverEvent, OnGlobalDragOver);
        this.AddHandler(DragDrop.DropEvent, OnGlobalDrop);
        Console.WriteLine("✅ Global drag-and-drop handlers registered");
    }

    /// <summary>
    /// Set up panel-specific drag-and-drop handlers
    /// </summary>
    private void SetupPanelSpecificDragDrop()
    {
        var inputPanel = this.FindControl<Border>("InputDocumentPanel");
        var outputPanel = this.FindControl<Border>("OutputDocumentPanel");

        if (inputPanel != null)
        {
            inputPanel.AddHandler(DragDrop.DragOverEvent, OnInputPanelDragOver);
            inputPanel.AddHandler(DragDrop.DropEvent, OnInputPanelDrop);
            Console.WriteLine("✅ Input panel drag-and-drop handlers registered");
        }

        if (outputPanel != null)
        {
            outputPanel.AddHandler(DragDrop.DragOverEvent, OnOutputPanelDragOver);
            outputPanel.AddHandler(DragDrop.DropEvent, OnOutputPanelDrop);
            Console.WriteLine("✅ Output panel drag-and-drop handlers registered");
        }
    }

    #region Global Drag and Drop Events

    private void OnGlobalDragOver(object? sender, DragEventArgs e)
    {
        Console.WriteLine("🔍 Global drag detected");
        
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null && files.Any(f => IsValidFileType(f.Path.LocalPath)))
            {
                Console.WriteLine($"✅ Valid files found: {files.Count()}");
                e.DragEffects = DragDropEffects.Copy;
                
                // Visual feedback - highlight the entire area
                this.Opacity = 0.8;
            }
            else
            {
                Console.WriteLine("❌ No valid files found");
                e.DragEffects = DragDropEffects.None;
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnGlobalDrop(object? sender, DragEventArgs e)
    {
        Console.WriteLine("🎯 Global drop triggered!");
        
        // Reset visual feedback
        this.Opacity = 1.0;

        if (e.Data.Contains(DataFormats.Files) && DataContext is MainViewModel vm)
        {
            Console.WriteLine("📂 Processing dropped files...");
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var filePaths = files.Where(f => IsValidFileType(f.Path.LocalPath))
                                    .Select(f => f.Path.LocalPath)
                                    .ToList();
                
                Console.WriteLine($"📋 File paths extracted: {filePaths.Count} files");
                
                if (filePaths.Any())
                {
                    Console.WriteLine("🚀 Calling HandleDroppedFiles...");
                    await vm.HandleDroppedFiles(filePaths);
                    
                    Console.WriteLine("🖼️ Rendering document...");
                    if (vm.DocumentCanvas != null)
                    {
                        await vm.RenderCurrentDocumentAsync(vm.DocumentCanvas);
                    }
                    Console.WriteLine("✅ Drop processing complete");
                }
            }
        }
    }

    private static bool IsValidFileType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".pdf" or ".xlsx" or ".xls" or ".tiff" or ".bmp";
    }

    private static bool IsInputDocumentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".pdf" or ".tiff" or ".bmp";
    }

    private static bool IsOutputTemplateType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".xlsx" or ".xls";
    }

    #endregion

    #region Panel-Specific Drag and Drop Events

    private void OnInputPanelDragOver(object? sender, DragEventArgs e)
    {
        Console.WriteLine("🔍 Input panel drag detected");
        
        if (e.Data.Contains(DataFormats.Files) && sender is Border inputPanel)
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var hasValidInputFiles = files.Any(f => IsInputDocumentType(f.Path.LocalPath));
                var hasInvalidFiles = files.Any(f => !IsInputDocumentType(f.Path.LocalPath) && IsValidFileType(f.Path.LocalPath));
                
                if (hasValidInputFiles && !hasInvalidFiles)
                {
                    // All files are valid input documents
                    e.DragEffects = DragDropEffects.Copy;
                    inputPanel.BorderBrush = Avalonia.Media.Brushes.Green;
                    inputPanel.BorderThickness = new Thickness(3);
                    Console.WriteLine("✅ Valid input files - showing green border");
                }
                else if (hasInvalidFiles)
                {
                    // Wrong file types for input panel
                    e.DragEffects = DragDropEffects.None;
                    inputPanel.BorderBrush = Avalonia.Media.Brushes.Red;
                    inputPanel.BorderThickness = new Thickness(3);
                    Console.WriteLine("❌ Invalid files for input panel - showing red border");
                }
                else
                {
                    e.DragEffects = DragDropEffects.None;
                }
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnInputPanelDrop(object? sender, DragEventArgs e)
    {
        Console.WriteLine("🎯 Input panel drop triggered!");
        
        // Reset visual feedback
        if (sender is Border inputPanel)
        {
            inputPanel.BorderBrush = Avalonia.Media.Brushes.LightBlue;
            inputPanel.BorderThickness = new Thickness(0, 0, 1, 0);
        }

        if (e.Data.Contains(DataFormats.Files) && DataContext is MainViewModel vm)
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var inputFiles = files.Where(f => IsInputDocumentType(f.Path.LocalPath))
                                     .Select(f => f.Path.LocalPath)
                                     .ToList();
                
                if (inputFiles.Any())
                {
                    Console.WriteLine($"📋 Processing {inputFiles.Count} input document(s)");
                    await vm.HandleInputDocuments(inputFiles);
                    
                    // Render the document
                    if (vm.DocumentCanvas != null)
                    {
                        await vm.RenderCurrentDocumentAsync(vm.DocumentCanvas);
                    }
                }
                else
                {
                    await vm.AddChatMessage("❌ No valid input documents found. Please drop PDFs or images.", false);
                }
            }
        }
    }

    private void OnOutputPanelDragOver(object? sender, DragEventArgs e)
    {
        Console.WriteLine("🔍 Output panel drag detected");
        
        if (e.Data.Contains(DataFormats.Files) && sender is Border outputPanel)
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var hasValidOutputFiles = files.Any(f => IsOutputTemplateType(f.Path.LocalPath));
                var hasInvalidFiles = files.Any(f => !IsOutputTemplateType(f.Path.LocalPath) && IsValidFileType(f.Path.LocalPath));
                
                if (hasValidOutputFiles && !hasInvalidFiles)
                {
                    // All files are valid output templates
                    e.DragEffects = DragDropEffects.Copy;
                    outputPanel.BorderBrush = Avalonia.Media.Brushes.Green;
                    outputPanel.BorderThickness = new Thickness(3);
                    Console.WriteLine("✅ Valid output templates - showing green border");
                }
                else if (hasInvalidFiles)
                {
                    // Wrong file types for output panel
                    e.DragEffects = DragDropEffects.None;
                    outputPanel.BorderBrush = Avalonia.Media.Brushes.Red;
                    outputPanel.BorderThickness = new Thickness(3);
                    Console.WriteLine("❌ Invalid files for output panel - showing red border");
                }
                else
                {
                    e.DragEffects = DragDropEffects.None;
                }
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnOutputPanelDrop(object? sender, DragEventArgs e)
    {
        Console.WriteLine("🎯 Output panel drop triggered!");
        
        // Reset visual feedback
        if (sender is Border outputPanel)
        {
            outputPanel.BorderBrush = Avalonia.Media.Brushes.LightGreen;
            outputPanel.BorderThickness = new Thickness(0);
        }

        if (e.Data.Contains(DataFormats.Files) && DataContext is MainViewModel vm)
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var templateFiles = files.Where(f => IsOutputTemplateType(f.Path.LocalPath))
                                        .Select(f => f.Path.LocalPath)
                                        .ToList();
                
                if (templateFiles.Any())
                {
                    Console.WriteLine($"📋 Processing {templateFiles.Count} output template(s)");
                    await vm.HandleOutputTemplates(templateFiles);
                }
                else
                {
                    await vm.AddChatMessage("❌ No valid Excel templates found. Please drop .xlsx or .xls files.", false);
                }
            }
        }
    }

    #endregion

    #region Document Navigation - Removed per user request

    // Navigation event handlers removed per user request

    #endregion

    #region Zoom Controls

    private void OnZoomOut(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ZoomOut();
        }
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ZoomIn();
        }
    }

    private void OnZoomFit(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ZoomToFit();
        }
    }

    #endregion

    #region Chat Events

    private async void OnSendMessage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.SendMessage();
        }
    }

    private async void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && DataContext is MainViewModel vm)
        {
            e.Handled = true;
            await vm.SendMessage();
        }
    }

    #endregion

    #region New 4-Panel Layout Events

    private void OnToggleInputView(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ToggleInputView();
        }
    }

    private void OnToggleOutputView(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ToggleOutputView();
        }
    }

    private ChatWindow? _chatWindow;

    private void OnPopOutChat(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            Console.WriteLine("🚀 Opening chat in floating window");
            
            // Set chat as undocked
            vm.IsChatUndocked = true;
            vm.UpdateLayoutDimensions();
            
            // Create and show floating chat window
            _chatWindow = new ChatWindow(vm, OnDockChatBack);
            _chatWindow.Show();
            
            Console.WriteLine("✅ Chat window opened and undocked");
        }
    }

    private void OnDockChatBack()
    {
        if (DataContext is MainViewModel vm)
        {
            Console.WriteLine("🔄 Docking chat back to main window");
            
            // Set chat as docked
            vm.IsChatUndocked = false;
            vm.UpdateLayoutDimensions();
            
            // Clear the reference without explicitly closing (window may already be closing)
            if (_chatWindow != null)
            {
                _chatWindow = null;
            }
            
            Console.WriteLine("✅ Chat docked back to main window");
        }
    }

    // UpdateViewLayout method removed - layout is now handled by UpdateLayoutDimensions in ViewModel

    #endregion

    #region Document Rendering

    private async Task RenderCurrentDocument()
    {
        if (DataContext is MainViewModel vm && vm.DocumentCanvas != null)
        {
            await vm.RenderCurrentDocumentAsync(vm.DocumentCanvas);
        }
    }

    #endregion
}