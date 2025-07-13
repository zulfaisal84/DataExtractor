using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DocumentExtractor.Desktop.ViewModels;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

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
        }
        
        // Set up global drag-and-drop after control loads
        this.Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SetupGlobalDragDrop();
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

    #endregion

    #region Document Navigation

    private async void OnPreviousDocument(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.NavigateToPreviousDocument();
            await RenderCurrentDocument();
        }
    }

    private async void OnNextDocument(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.NavigateToNextDocument();
            await RenderCurrentDocument();
        }
    }

    private async void OnPreviousPage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.NavigateToPreviousPage();
            await RenderCurrentDocument();
        }
    }

    private async void OnNextPage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.NavigateToNextPage();
            await RenderCurrentDocument();
        }
    }

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

    private void OnToggleInputView(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsInputExpanded = !vm.IsInputExpanded;
            UpdateViewLayout(vm);
        }
    }

    private void OnToggleOutputView(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsOutputExpanded = !vm.IsOutputExpanded;
            UpdateViewLayout(vm);
        }
    }

    private void OnPopOutChat(object? sender, RoutedEventArgs e)
    {
        // TODO: Implement pop-out window for chat in Week 2
        if (DataContext is MainViewModel vm)
        {
            vm.ChatMessages.Add(new Models.ChatMessage
            {
                Content = "Pop-out chat window coming in Week 2!",
                SenderName = "AI Assistant",
                Type = Models.ChatMessageType.Bot,
                Timestamp = DateTime.Now
            });
        }
    }

    private void UpdateViewLayout(MainViewModel vm)
    {
        // Update grid column definitions based on expanded states
        if (vm.IsInputExpanded && !vm.IsOutputExpanded)
        {
            // Input full-screen
            vm.InputDocumentWidth = new GridLength(1, GridUnitType.Star);
            vm.OutputDocumentWidth = new GridLength(0, GridUnitType.Pixel);
            vm.IsInputVisible = true;
            vm.IsOutputVisible = false;
        }
        else if (!vm.IsInputExpanded && vm.IsOutputExpanded)
        {
            // Output full-screen  
            vm.InputDocumentWidth = new GridLength(0, GridUnitType.Pixel);
            vm.OutputDocumentWidth = new GridLength(1, GridUnitType.Star);
            vm.IsInputVisible = false;
            vm.IsOutputVisible = true;
        }
        else
        {
            // Split view (default)
            vm.InputDocumentWidth = new GridLength(1, GridUnitType.Star);
            vm.OutputDocumentWidth = new GridLength(1, GridUnitType.Star);
            vm.IsInputVisible = true;
            vm.IsOutputVisible = true;
            vm.IsInputExpanded = false;
            vm.IsOutputExpanded = false;
        }
    }

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