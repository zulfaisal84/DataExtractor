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
/// Main view implementing the single-tab, three-panel conversational interface
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
            vm.DocumentCanvas = DocumentCanvas;
        }
        
        // Wire up drag-and-drop events
        SetupDragAndDrop();
    }
    
    private void SetupDragAndDrop()
    {
        // Add drag-and-drop event handlers to the chat scroll viewer
        ChatScrollViewer.AddHandler(DragDrop.DragOverEvent, OnChatDragOver);
        ChatScrollViewer.AddHandler(DragDrop.DropEvent, OnChatDrop);
    }

    #region File Management Events

    private void OnFileStatusClicked(object? sender, PointerPressedEventArgs e)
    {
        // Show file list popup or expand details
        if (DataContext is MainViewModel vm)
        {
            vm.ShowFileDetails();
        }
    }

    private void OnTemplateStatusClicked(object? sender, PointerPressedEventArgs e)
    {
        // Show template list popup or expand details
        if (DataContext is MainViewModel vm)
        {
            vm.ShowTemplateDetails();
        }
    }

    private void OnDocumentAreaClicked(object? sender, PointerPressedEventArgs e)
    {
        // Handle click on document area (maybe select for annotation)
        if (DataContext is MainViewModel vm)
        {
            vm.SelectDocumentArea(e.GetPosition(sender as Control));
        }
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

    private void OnClearChat(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ClearChatHistory();
        }
    }

    private void OnScreenshotClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.TakeScreenshot();
        }
    }

    private void OnCalibrateClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StartCalibration();
        }
    }

    private async void OnImportFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.ImportFolder();
        }
    }

    private void OnProcessClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StartProcessing();
        }
    }

    private async void OnSendMessage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.SendMessage();
        }
    }

    private async void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel vm)
        {
            await vm.SendMessage();
        }
    }

    #endregion

    #region Drag and Drop Events

    private void OnChatDragOver(object? sender, DragEventArgs e)
    {
        // Check if dragged content contains files
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null && files.Any(f => IsValidFileType(f.Path.LocalPath)))
            {
                e.DragEffects = DragDropEffects.Copy;
                
                // Visual feedback - change background color
                if (sender is ScrollViewer scrollViewer)
                {
                    scrollViewer.Background = Avalonia.Media.Brushes.LightBlue;
                }
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnChatDrop(object? sender, DragEventArgs e)
    {
        // Reset background color
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.Background = Avalonia.Media.Brush.Parse("#F8F9FA");
        }

        if (e.Data.Contains(DataFormats.Files) && DataContext is MainViewModel vm)
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                // Extract file paths and use batch processing
                var filePaths = files.Where(f => IsValidFileType(f.Path.LocalPath))
                                    .Select(f => f.Path.LocalPath)
                                    .ToList();
                
                if (filePaths.Any())
                {
                    // Use batch processing for multiple files
                    await vm.HandleDroppedFiles(filePaths);
                    
                    // Render the current document (latest loaded)
                    await RenderCurrentDocument();
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

    #region Document Rendering

    /// <summary>
    /// Renders the current document to the canvas
    /// </summary>
    private async Task RenderCurrentDocument()
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.RenderCurrentDocumentAsync(DocumentCanvas);
        }
    }

    #endregion

    #region Template Events

    private void OnPreviewTemplate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PreviewTemplate();
        }
    }

    #endregion
}