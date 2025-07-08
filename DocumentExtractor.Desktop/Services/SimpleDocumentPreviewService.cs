using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using System.Linq;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// Simplified document preview service that focuses on image support.
/// PDF support can be added later once we have proper library integration.
/// </summary>
public class SimpleDocumentPreviewService
{
    private readonly Dictionary<string, List<Bitmap>> _documentPages = new();
    private readonly Dictionary<string, DocumentMetadata> _documentMetadata = new();

    /// <summary>
    /// Loads a document and prepares it for preview rendering.
    /// </summary>
    /// <param name="filePath">Path to the document file</param>
    /// <returns>Number of pages loaded, or -1 if failed</returns>
    public async Task<int> LoadDocumentAsync(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            if (extension == ".pdf")
            {
                return await LoadPdfPlaceholderAsync(filePath);
            }
            else if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff")
            {
                return await LoadImageAsync(filePath);
            }
            return -1;
        }
        catch (Exception ex)
        {
            // Log error silently for production
            return -1;
        }
    }

    /// <summary>
    /// Renders a specific page of a document to a Canvas.
    /// </summary>
    /// <param name="filePath">Path to the document</param>
    /// <param name="pageIndex">Page index (0-based)</param>
    /// <param name="canvas">Canvas to render to</param>
    /// <param name="zoomLevel">Zoom level for rendering</param>
    public async Task RenderPageToCanvasAsync(string filePath, int pageIndex, Canvas canvas, double zoomLevel = 1.0)
    {
        try
        {
            if (!_documentPages.ContainsKey(filePath))
            {
                await LoadDocumentAsync(filePath);
            }

            if (!_documentPages.ContainsKey(filePath))
            {
                return;
            }

            var pages = _documentPages[filePath];
            var metadata = _documentMetadata[filePath];

            // Clear existing content
            canvas.Children.Clear();

            // Handle PDF placeholder
            if (metadata.DocumentType == "PDF")
            {
                await RenderPdfPlaceholder(canvas, filePath, pageIndex);
                return;
            }

            // For non-PDF files, check if we have pages
            if (pageIndex < 0 || pageIndex >= pages.Count)
            {
                return;
            }

            var bitmap = pages[pageIndex];

            // Calculate scaled dimensions
            var scaledWidth = bitmap.PixelSize.Width * zoomLevel;
            var scaledHeight = bitmap.PixelSize.Height * zoomLevel;

            // Update canvas size
            canvas.Width = Math.Max(scaledWidth, 400);
            canvas.Height = Math.Max(scaledHeight, 500);

            // Create image control
            var imageControl = new Image
            {
                Source = bitmap,
                Width = scaledWidth,
                Height = scaledHeight,
                Stretch = Stretch.Fill
            };

            // Position image at top-left
            Canvas.SetLeft(imageControl, 0);
            Canvas.SetTop(imageControl, 0);

            // Add to canvas
            canvas.Children.Add(imageControl);

            // Add page number indicator if multi-page
            if (pages.Count > 1)
            {
                var pageIndicator = new TextBlock
                {
                    Text = $"Page {pageIndex + 1} of {pages.Count}",
                    Background = Brushes.Black,
                    Foreground = Brushes.White,
                    Padding = new Thickness(8, 4),
                    FontSize = 12,
                    FontWeight = FontWeight.Bold
                };

                Canvas.SetRight(pageIndicator, 10);
                Canvas.SetTop(pageIndicator, 10);
                canvas.Children.Add(pageIndicator);
            }
        }
        catch (Exception ex)
        {
            // Show error message on canvas
            canvas.Children.Clear();
            var errorText = new TextBlock
            {
                Text = $"Error loading document: {ex.Message}",
                Foreground = Brushes.Red,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 350
            };
            
            Canvas.SetLeft(errorText, 20);
            Canvas.SetTop(errorText, 20);
            canvas.Children.Add(errorText);
        }
    }

    /// <summary>
    /// Gets the number of pages in a loaded document.
    /// </summary>
    public int GetPageCount(string filePath)
    {
        return _documentPages.ContainsKey(filePath) ? _documentPages[filePath].Count : 0;
    }

    /// <summary>
    /// Gets metadata for a loaded document.
    /// </summary>
    public DocumentMetadata? GetDocumentMetadata(string filePath)
    {
        return _documentMetadata.ContainsKey(filePath) ? _documentMetadata[filePath] : null;
    }

    /// <summary>
    /// Handles click events on document canvas for annotation/teaching.
    /// </summary>
    public Point GetDocumentCoordinates(Point canvasPoint, string filePath, double zoomLevel)
    {
        // Convert canvas coordinates to document coordinates
        var documentX = canvasPoint.X / zoomLevel;
        var documentY = canvasPoint.Y / zoomLevel;
        
        return new Point(documentX, documentY);
    }

    /// <summary>
    /// Clears cached document data.
    /// </summary>
    public void ClearDocument(string filePath)
    {
        if (_documentPages.ContainsKey(filePath))
        {
            // Dispose bitmaps
            foreach (var bitmap in _documentPages[filePath])
            {
                bitmap.Dispose();
            }
            _documentPages.Remove(filePath);
        }
        
        _documentMetadata.Remove(filePath);
    }

    /// <summary>
    /// Clears all cached documents.
    /// </summary>
    public void ClearAllDocuments()
    {
        foreach (var pages in _documentPages.Values)
        {
            foreach (var bitmap in pages)
            {
                bitmap.Dispose();
            }
        }
        
        _documentPages.Clear();
        _documentMetadata.Clear();
    }

    private async Task<int> LoadPdfPlaceholderAsync(string filePath)
    {
        try
        {
            // For PDF files, we'll just store metadata and render UI placeholder
            _documentPages[filePath] = new List<Bitmap>();
            _documentMetadata[filePath] = new DocumentMetadata
            {
                FilePath = filePath,
                PageCount = 1,
                DocumentType = "PDF",
                FileSize = new FileInfo(filePath).Length
            };
            
            return 1;
        }
        catch (Exception ex)
        {
            return -1;
        }
    }

    private async Task<int> LoadImageAsync(string filePath)
    {
        try
        {
            var bitmap = new Bitmap(filePath);
            var pages = new List<Bitmap> { bitmap };
            
            _documentPages[filePath] = pages;
            _documentMetadata[filePath] = new DocumentMetadata
            {
                FilePath = filePath,
                PageCount = 1,
                DocumentType = "Image",
                FileSize = new FileInfo(filePath).Length
            };
            
            return 1;
        }
        catch (Exception ex)
        {
            return -1;
        }
    }


    private async Task RenderPdfPlaceholder(Canvas canvas, string filePath, int pageIndex)
    {
        canvas.Children.Clear();
        canvas.Width = 600;
        canvas.Height = 800;
        
        // Create PDF placeholder UI
        var border = new Border
        {
            Width = 580,
            Height = 780,
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5)
        };

        var stackPanel = new StackPanel
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var pdfIcon = new TextBlock
        {
            Text = "📄",
            FontSize = 80,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var fileName = new TextBlock
        {
            Text = Path.GetFileName(filePath),
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var typeText = new TextBlock
        {
            Text = "PDF Document",
            FontSize = 14,
            Foreground = Brushes.Gray,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var comingSoonText = new TextBlock
        {
            Text = "PDF Preview Coming Soon",
            FontSize = 16,
            Foreground = Brushes.Orange,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var infoText = new TextBlock
        {
            Text = "For now, you can drag PDF files to chat\nand I'll help you extract data from them!",
            FontSize = 12,
            Foreground = Brushes.DarkBlue,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        stackPanel.Children.Add(pdfIcon);
        stackPanel.Children.Add(fileName);
        stackPanel.Children.Add(typeText);
        stackPanel.Children.Add(comingSoonText);
        stackPanel.Children.Add(infoText);

        border.Child = stackPanel;

        Canvas.SetLeft(border, 10);
        Canvas.SetTop(border, 10);
        canvas.Children.Add(border);
    }
}

/// <summary>
/// Metadata for a loaded document.
/// </summary>
public class DocumentMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LoadTime { get; set; } = DateTime.Now;
}