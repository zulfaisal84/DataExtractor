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
/// ISO page size constants for document aspect ratios
/// </summary>
public static class ISOPageSizes
{
    /// <summary>ISO A4 size in millimeters (210 × 297)</summary>
    public static readonly Size A4_MM = new Size(210, 297);
    
    /// <summary>ISO A3 size in millimeters (297 × 420)</summary>
    public static readonly Size A3_MM = new Size(297, 420);
    
    /// <summary>US Letter size in millimeters (216 × 279)</summary>
    public static readonly Size Letter_MM = new Size(216, 279);
    
    /// <summary>A4 aspect ratio (√2 ≈ 1.414)</summary>
    public const double A4_RATIO = 1.414213562373095;
    
    /// <summary>US Letter aspect ratio (11/8.5 ≈ 1.294)</summary>
    public const double LETTER_RATIO = 1.294117647058824;
    
    /// <summary>
    /// Calculate canvas size maintaining A4 aspect ratio
    /// </summary>
    /// <param name="availableWidth">Available width for the canvas</param>
    /// <param name="maxHeight">Maximum height constraint</param>
    /// <returns>Size with A4 aspect ratio that fits constraints</returns>
    public static Size CalculateA4CanvasSize(double availableWidth, double maxHeight = double.MaxValue)
    {
        var idealHeight = availableWidth * A4_RATIO;
        
        if (idealHeight <= maxHeight)
        {
            return new Size(availableWidth, idealHeight);
        }
        else
        {
            // Height constrained - calculate width from height
            var constrainedWidth = maxHeight / A4_RATIO;
            return new Size(constrainedWidth, maxHeight);
        }
    }
    
    /// <summary>
    /// Detect document format based on aspect ratio
    /// </summary>
    /// <param name="width">Document width</param>
    /// <param name="height">Document height</param>
    /// <returns>Detected page format</returns>
    public static string DetectPageFormat(double width, double height)
    {
        var ratio = height / width;
        var tolerance = 0.05; // 5% tolerance
        
        if (Math.Abs(ratio - A4_RATIO) < tolerance)
            return "A4";
        else if (Math.Abs(ratio - LETTER_RATIO) < tolerance)
            return "US Letter";
        else if (ratio > 1.5)
            return "Portrait";
        else if (ratio < 0.8)
            return "Landscape";
        else
            return "Custom";
    }
}

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
            Console.WriteLine($"[DEBUG] Loading document: {filePath}");
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            Console.WriteLine($"[DEBUG] Extension detected: {extension}");
            
            if (extension == ".pdf")
            {
                Console.WriteLine($"[DEBUG] Loading as PDF");
                return await LoadPdfPlaceholderAsync(filePath);
            }
            else if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff")
            {
                Console.WriteLine($"[DEBUG] Loading as image");
                var result = await LoadImageAsync(filePath);
                Console.WriteLine($"[DEBUG] Image load result: {result}");
                return result;
            }
            Console.WriteLine($"[DEBUG] Unsupported extension: {extension}");
            return -1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error in LoadDocumentAsync: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Renders a specific page of a document to a Canvas with proper aspect ratio.
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
            
            // Set canvas background to light gray to show document bounds
            canvas.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));

            // Calculate optimal canvas size for A4 aspect ratio
            var canvasSize = CalculateOptimalCanvasSize(canvas, metadata);
            canvas.Width = canvasSize.Width;
            canvas.Height = canvasSize.Height;

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
            
            // Calculate document dimensions and scaling
            var documentSize = CalculateDocumentDisplaySize(bitmap, canvas, zoomLevel);
            var centerOffset = CalculateCenterOffset(canvas, documentSize);
            
            // Create white document background
            var documentBackground = new Border
            {
                Width = documentSize.Width,
                Height = documentSize.Height,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };
            
            Canvas.SetLeft(documentBackground, centerOffset.X);
            Canvas.SetTop(documentBackground, centerOffset.Y);
            canvas.Children.Add(documentBackground);

            // Create image control with proper sizing
            var imageControl = new Image
            {
                Source = bitmap,
                Width = documentSize.Width - 2, // Account for border
                Height = documentSize.Height - 2,
                Stretch = Stretch.Uniform // Maintain aspect ratio
            };

            // Position image centered on document background
            Canvas.SetLeft(imageControl, centerOffset.X + 1);
            Canvas.SetTop(imageControl, centerOffset.Y + 1);
            canvas.Children.Add(imageControl);

            // Add document info overlay
            var infoPanel = CreateDocumentInfoPanel(metadata, bitmap, pageIndex, pages.Count);
            Canvas.SetRight(infoPanel, 10);
            Canvas.SetTop(infoPanel, 10);
            canvas.Children.Add(infoPanel);
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

    /// <summary>
    /// Calculate optimal canvas size for document display
    /// </summary>
    private Size CalculateOptimalCanvasSize(Canvas canvas, DocumentMetadata metadata)
    {
        // Get available space (with some padding)
        var availableWidth = Math.Max(400, canvas.Parent is Control parent ? parent.Bounds.Width - 20 : 600);
        var availableHeight = Math.Max(500, canvas.Parent is Control parentControl ? parentControl.Bounds.Height - 20 : 800);
        
        // Use most of the available space for better readability
        return new Size(availableWidth, availableHeight);
    }

    /// <summary>
    /// Calculate document display size maintaining aspect ratio and fitting in canvas
    /// </summary>
    private Size CalculateDocumentDisplaySize(Bitmap bitmap, Canvas canvas, double zoomLevel)
    {
        // Get document's natural aspect ratio
        var documentRatio = (double)bitmap.PixelSize.Height / bitmap.PixelSize.Width;
        
        // Calculate maximum size that fits in canvas with minimal padding for maximum readability
        var maxWidth = canvas.Width - 20; // 10px padding on each side
        var maxHeight = canvas.Height - 20;
        
        // Start with width that fills most of the available space
        var targetWidth = maxWidth * zoomLevel;
        var targetHeight = targetWidth * documentRatio;
        
        // If height is too big, scale down based on height constraint
        if (targetHeight > maxHeight * zoomLevel)
        {
            targetHeight = maxHeight * zoomLevel;
            targetWidth = targetHeight / documentRatio;
        }
        
        // Ensure minimum readable size - much larger than before
        var minWidth = Math.Min(400, maxWidth * 0.8);
        var minHeight = minWidth * documentRatio;
        
        return new Size(
            Math.Max(targetWidth, minWidth), 
            Math.Max(targetHeight, minHeight)
        );
    }

    /// <summary>
    /// Calculate offset to center document in canvas
    /// </summary>
    private Point CalculateCenterOffset(Canvas canvas, Size documentSize)
    {
        var x = (canvas.Width - documentSize.Width) / 2;
        var y = (canvas.Height - documentSize.Height) / 2;
        return new Point(Math.Max(0, x), Math.Max(0, y));
    }

    /// <summary>
    /// Create document information panel
    /// </summary>
    private Border CreateDocumentInfoPanel(DocumentMetadata metadata, Bitmap bitmap, int pageIndex, int totalPages)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 2
        };
        
        // Document format detection
        var format = ISOPageSizes.DetectPageFormat(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var ratio = (double)bitmap.PixelSize.Height / bitmap.PixelSize.Width;
        
        // Page info
        if (totalPages > 1)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Page {pageIndex + 1} of {totalPages}",
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.Bold
            });
        }
        
        // Format info
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"{format} | {bitmap.PixelSize.Width}×{bitmap.PixelSize.Height}",
            Foreground = Brushes.White,
            FontSize = 10
        });
        
        // Aspect ratio
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"Ratio: {ratio:F2}",
            Foreground = Brushes.White,
            FontSize = 9
        });
        
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // Semi-transparent black
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            Child = stackPanel
        };
    }

    /// <summary>
    /// Create PDF document information panel
    /// </summary>
    private Border CreatePdfInfoPanel(DocumentMetadata metadata)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 2
        };
        
        // Document type
        stackPanel.Children.Add(new TextBlock
        {
            Text = "PDF Document",
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeight.Bold
        });
        
        // Assumed format (since we can't read PDF dimensions yet)
        stackPanel.Children.Add(new TextBlock
        {
            Text = "A4 | 210×297mm",
            Foreground = Brushes.White,
            FontSize = 10
        });
        
        // File size
        var fileSizeKB = metadata.FileSize / 1024;
        stackPanel.Children.Add(new TextBlock
        {
            Text = $"Size: {fileSizeKB:N0} KB",
            Foreground = Brushes.White,
            FontSize = 9
        });
        
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // Semi-transparent black
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            Child = stackPanel
        };
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
            Console.WriteLine($"[DEBUG] Creating bitmap from: {filePath}");
            var bitmap = new Bitmap(filePath);
            Console.WriteLine($"[DEBUG] Bitmap created successfully. Size: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
            var pages = new List<Bitmap> { bitmap };
            
            _documentPages[filePath] = pages;
            _documentMetadata[filePath] = new DocumentMetadata
            {
                FilePath = filePath,
                PageCount = 1,
                DocumentType = "Image",
                FileSize = new FileInfo(filePath).Length
            };
            
            Console.WriteLine($"[DEBUG] Image metadata stored successfully");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error in LoadImageAsync: {ex.Message}");
            Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
            return -1;
        }
    }


    private async Task RenderPdfPlaceholder(Canvas canvas, string filePath, int pageIndex)
    {
        // Canvas sizing is already handled by RenderPageToCanvasAsync
        
        // Calculate PDF placeholder size (80% of canvas for nice padding)
        var placeholderWidth = canvas.Width * 0.8;
        var placeholderHeight = canvas.Height * 0.8;
        var centerX = (canvas.Width - placeholderWidth) / 2;
        var centerY = (canvas.Height - placeholderHeight) / 2;
        
        // Create PDF placeholder with A4 proportions
        var border = new Border
        {
            Width = placeholderWidth,
            Height = placeholderHeight,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
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

        Canvas.SetLeft(border, centerX);
        Canvas.SetTop(border, centerY);
        canvas.Children.Add(border);
        
        // Add document info for PDF
        var metadata = _documentMetadata[filePath];
        var pdfInfoPanel = CreatePdfInfoPanel(metadata);
        Canvas.SetRight(pdfInfoPanel, 10);
        Canvas.SetTop(pdfInfoPanel, 10);
        canvas.Children.Add(pdfInfoPanel);
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