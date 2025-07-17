using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Collections.Generic;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Globalization;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using SkiaSharp;

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
    /// <param name="canvasWidth">Available canvas width for Excel rendering</param>
    /// <param name="canvasHeight">Available canvas height for Excel rendering</param>
    /// <returns>Number of pages loaded, or -1 if failed</returns>
    public async Task<int> LoadDocumentAsync(string filePath, int canvasWidth = 1200, int canvasHeight = 800)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Loading document: {filePath}");
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            Console.WriteLine($"[DEBUG] Extension detected: {extension}");
            
            if (extension == ".pdf")
            {
                Console.WriteLine($"[DEBUG] Loading as PDF");
                return await LoadPdfAsync(filePath);
            }
            else if (extension is ".xlsx" or ".xls")
            {
                Console.WriteLine($"[DEBUG] Loading as Excel with canvas size: {canvasWidth}x{canvasHeight}");
                return await LoadExcelAsync(filePath, canvasWidth, canvasHeight);
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
    /// Render all pages vertically stacked in canvas for scrollable view
    /// </summary>
    /// <param name="filePath">Path to the document</param>
    /// <param name="canvas">Canvas to render to</param>
    /// <param name="zoomLevel">Zoom level for rendering</param>
    public async Task RenderAllPagesToCanvasAsync(string filePath, Canvas canvas, double zoomLevel = 1.0)
    {
        try
        {
            canvas.Children.Clear();
            
            if (!_documentPages.ContainsKey(filePath) || !_documentMetadata.ContainsKey(filePath))
            {
                Console.WriteLine($"[DEBUG] Document not loaded: {filePath}");
                return;
            }

            var pages = _documentPages[filePath];
            var metadata = _documentMetadata[filePath];
            
            if (pages.Count == 0)
            {
                Console.WriteLine($"[DEBUG] No pages found for {filePath}");
                return;
            }

            // Calculate total height needed for all pages
            var pageSpacing = 20; // Space between pages
            var totalHeight = 0.0;
            var maxWidth = 0.0;
            
            // Calculate dimensions for each page with safe canvas sizing
            var pageSizes = new List<Size>();
            var canvasWidth = Math.Max(800, !double.IsNaN(canvas.Width) ? canvas.Width : 800);
            var canvasHeight = Math.Max(600, !double.IsNaN(canvas.Height) ? canvas.Height : 600);
            
            foreach (var page in pages)
            {
                // Calculate page size based on available space
                var pageSize = CalculateDocumentDisplaySizeSafe(page, canvasWidth, canvasHeight, zoomLevel);
                pageSizes.Add(pageSize);
                totalHeight += pageSize.Height + pageSpacing;
                maxWidth = Math.Max(maxWidth, pageSize.Width);
            }
            
            // Set canvas size to accommodate all pages
            var finalWidth = Math.Max(600, maxWidth + 40); // Minimum width with padding
            var finalHeight = Math.Max(400, totalHeight + 40); // Minimum height with padding
            
            canvas.Width = finalWidth;
            canvas.Height = finalHeight;
            
            // Ensure minimum size constraints don't interfere
            canvas.MinWidth = finalWidth;
            canvas.MinHeight = finalHeight;
            
            Console.WriteLine($"[DEBUG] Setting canvas dimensions to: {finalWidth}x{finalHeight}");
            
            // Render each page vertically stacked
            var currentY = 20.0; // Start with top padding
            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                var pageSize = pageSizes[i];
                
                // Create image control for this page
                var imageControl = new Image
                {
                    Source = page,
                    Width = pageSize.Width,
                    Height = pageSize.Height,
                    Stretch = Stretch.Uniform
                };
                
                // Center horizontally, position vertically
                var centerX = Math.Max(20, (canvas.Width - pageSize.Width) / 2);
                Canvas.SetLeft(imageControl, centerX);
                Canvas.SetTop(imageControl, currentY);
                canvas.Children.Add(imageControl);
                
                // Page info panel removed per user request
                
                // Move to next page position
                currentY += pageSize.Height + pageSpacing;
            }
            
            Console.WriteLine($"[DEBUG] Rendered {pages.Count} pages in scrollable canvas. Canvas size: {canvas.Width}x{canvas.Height}");
            Console.WriteLine($"[DEBUG] Total content height: {totalHeight}, Max content width: {maxWidth}");
            Console.WriteLine($"[DEBUG] Final Y position: {currentY}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error rendering all pages: {ex.Message}");
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

            // Handle PDF and Excel documents
            if (metadata.DocumentType == "PDF" && pages.Count == 0)
            {
                // If PDF failed to render, show placeholder
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
            
            // Create image control with clean full sizing (no borders)
            var imageControl = new Image
            {
                Source = bitmap,
                Width = documentSize.Width,
                Height = documentSize.Height,
                Stretch = Stretch.Uniform // Maintain aspect ratio
            };

            // Position image centered in canvas
            Canvas.SetLeft(imageControl, centerOffset.X);
            Canvas.SetTop(imageControl, centerOffset.Y);
            canvas.Children.Add(imageControl);

            // Document info overlay removed per user request
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
    /// Gets the actual width of the document at 100% zoom level.
    /// </summary>
    public double GetDocumentWidth(string filePath)
    {
        if (!_documentPages.ContainsKey(filePath) || _documentPages[filePath].Count == 0)
            return 0;

        // Get the first page to determine document width
        var firstPage = _documentPages[filePath][0];
        return firstPage.Size.Width;
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
    /// Calculate document display size with safe canvas dimensions
    /// </summary>
    private Size CalculateDocumentDisplaySizeSafe(Bitmap bitmap, double canvasWidth, double canvasHeight, double zoomLevel)
    {
        // Get document's natural aspect ratio
        var documentRatio = (double)bitmap.PixelSize.Height / bitmap.PixelSize.Width;
        
        // Calculate maximum size that fits in canvas with padding
        var maxWidth = canvasWidth - 40; // 20px padding on each side
        var maxHeight = canvasHeight - 40; // 20px padding top and bottom
        
        // Start with width that fills available space
        var targetWidth = maxWidth * zoomLevel;
        var targetHeight = targetWidth * documentRatio;
        
        // If height is too large, scale down based on height
        if (targetHeight > maxHeight)
        {
            targetHeight = maxHeight;
            targetWidth = targetHeight / documentRatio;
        }
        
        // Ensure minimum readable size
        targetWidth = Math.Max(targetWidth, 300);
        targetHeight = Math.Max(targetHeight, 400);
        
        return new Size(targetWidth, targetHeight);
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
    private Avalonia.Controls.Border CreateDocumentInfoPanel(DocumentMetadata metadata, Bitmap bitmap, int pageIndex, int totalPages)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 2
        };
        
        // Document format detection
        var format = ISOPageSizes.DetectPageFormat(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var ratio = (double)bitmap.PixelSize.Height / bitmap.PixelSize.Width;
        
        // Page info removed per user request
        
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
        
        return new Avalonia.Controls.Border
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
    private Avalonia.Controls.Border CreatePdfInfoPanel(DocumentMetadata metadata)
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
        
        return new Avalonia.Controls.Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // Semi-transparent black
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            Child = stackPanel
        };
    }

    private async Task<int> LoadPdfAsync(string filePath)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Loading PDF with Docnet.Core: {filePath}");
            
            var pages = new List<Bitmap>();
            
            using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1080, 1920));
            var pageCount = docReader.GetPageCount();
            Console.WriteLine($"[DEBUG] PDF has {pageCount} pages");
            
            // Render each page to bitmap
            for (int i = 0; i < pageCount; i++)
            {
                var bitmap = await RenderPdfPageToBitmap(docReader, i);
                if (bitmap != null)
                {
                    pages.Add(bitmap);
                    Console.WriteLine($"[DEBUG] Rendered page {i + 1}/{pageCount} to bitmap");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Failed to render page {i + 1}/{pageCount}");
                }
            }
            
            _documentPages[filePath] = pages;
            _documentMetadata[filePath] = new DocumentMetadata
            {
                FilePath = filePath,
                PageCount = pageCount,
                DocumentType = "PDF",
                FileSize = new FileInfo(filePath).Length
            };
            
            Console.WriteLine($"[DEBUG] PDF loaded and rendered successfully");
            return pageCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error loading PDF: {ex.Message}");
            
            // Create fallback with placeholder
            _documentPages[filePath] = new List<Bitmap>(); // Empty list signals to use placeholder
            _documentMetadata[filePath] = new DocumentMetadata
            {
                FilePath = filePath,
                PageCount = 1,
                DocumentType = "PDF",
                FileSize = new FileInfo(filePath).Length
            };
            
            return 1;
        }
    }

    private async Task<int> LoadExcelAsync(string filePath, int canvasWidth = 1200, int canvasHeight = 800)
    {
        try
        {
            // Ensure minimum canvas dimensions for readable Excel rendering
            canvasWidth = Math.Max(canvasWidth, 1000);
            canvasHeight = Math.Max(canvasHeight, 700);
            
            Console.WriteLine($"[DEBUG] Loading Excel with EPPlus: {filePath} - Canvas: {canvasWidth}x{canvasHeight}");
            
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            
            if (worksheet == null)
            {
                Console.WriteLine($"[DEBUG] No worksheets found in Excel file");
                return -1;
            }
            
            // Create a visual representation of the Excel sheet with canvas size
            var bitmap = await RenderExcelToImage(worksheet, canvasWidth, canvasHeight);
            
            _documentPages[filePath] = new List<Bitmap> { bitmap };
            _documentMetadata[filePath] = new DocumentMetadata
            {
                FilePath = filePath,
                PageCount = 1,
                DocumentType = "Excel",
                FileSize = new FileInfo(filePath).Length
            };
            
            Console.WriteLine($"[DEBUG] Excel loaded and rendered successfully");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error loading Excel: {ex.Message}");
            return -1;
        }
    }

    private async Task<int> LoadPdfPlaceholderAsync(string filePath)
    {
        try
        {
            // Fallback placeholder for when PDF rendering fails
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
        var border = new Avalonia.Controls.Border
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
            Text = "⚠️ PDF Rendering Failed",
            FontSize = 16,
            Foreground = Brushes.Orange,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var infoText = new TextBlock
        {
            Text = "PDF could not be rendered visually.\nThe file may be corrupted or use unsupported features.\nTry a different PDF file.",
            FontSize = 12,
            Foreground = Brushes.DarkRed,
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


    /// <summary>
    /// Create a placeholder bitmap for PDF preview
    /// </summary>
    private Bitmap CreatePdfPreviewPlaceholder(int width, int height)
    {
        // Create a simple colored bitmap as placeholder
        // This will be improved with actual PDF rendering
        var pixelSize = new PixelSize(width, height);
        var dpi = new Vector(96, 96);
        
        var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
        using var drawingContext = renderTarget.CreateDrawingContext();
        
        // Draw white background
        drawingContext.FillRectangle(Brushes.White, new Rect(0, 0, width, height));
        
        // Draw PDF icon and text
        var iconSize = Math.Min(width, height) * 0.3;
        var iconRect = new Rect(
            (width - iconSize) / 2,
            (height - iconSize) / 2 - 50,
            iconSize,
            iconSize
        );
        
        drawingContext.FillRectangle(Brushes.LightGray, iconRect);
        
        // Add border
        drawingContext.DrawRectangle(new Pen(Brushes.Gray, 2), new Rect(0, 0, width, height));
        
        return renderTarget;
    }

    /// <summary>
    /// Render Excel worksheet to image with improved formatting
    /// </summary>
    private async Task<Bitmap> RenderExcelToImage(ExcelWorksheet worksheet, int canvasWidth = 1200, int canvasHeight = 800)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Rendering Excel worksheet: {worksheet.Name} for canvas {canvasWidth}x{canvasHeight}");
            
            // Get used range with safety checks
            var dimension = worksheet.Dimension;
            if (dimension == null)
            {
                Console.WriteLine($"[DEBUG] No data found in worksheet");
                return CreateExcelPreviewPlaceholder(canvasWidth, canvasHeight);
            }
            
            // VIEWPORT SYSTEM: Show only 10×8 cells at readable size
            var totalRows = dimension.End.Row;
            var totalCols = dimension.End.Column;
            
            // Viewport settings - show first 10×8 cells for readability
            var viewportRows = Math.Min(8, totalRows);
            var viewportCols = Math.Min(10, totalCols);
            
            var startRow = dimension.Start.Row;
            var endRow = startRow + viewportRows - 1;
            var startCol = dimension.Start.Column;
            var endCol = startCol + viewportCols - 1;
            
            Console.WriteLine($"[DEBUG] Viewport system: showing {viewportRows}×{viewportCols} cells (A1:{ GetExcelColumnName(endCol)}{endRow}) of total {totalRows}×{totalCols}");
            
            // READABLE CELL SIZES: Use fixed, large cell dimensions
            var headerHeight = 50; // Larger header
            var margin = 30;
            var cellWidth = 120; // Fixed readable width
            var cellHeight = 35;  // Fixed readable height
            
            // Calculate total dimensions based on viewport
            var width = viewportCols * cellWidth + margin + 150; // Extra space for scrollbar indicator
            var height = viewportRows * cellHeight + headerHeight + margin + 80; // Extra space for status
            
            Console.WriteLine($"[DEBUG] Rendering viewport at {cellWidth}×{cellHeight} per cell, total: {width}×{height}");
            
            var pixelSize = new PixelSize(width, height);
            var dpi = new Vector(96, 96);
            
            var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
            using var drawingContext = renderTarget.CreateDrawingContext();
            
            // Draw white background
            drawingContext.FillRectangle(Brushes.White, new Rect(0, 0, width, height));
            
            var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
            var headerTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
            
            // Draw worksheet header
            var headerRect = new Rect(10, 10, width - 20, headerHeight - 10);
            drawingContext.FillRectangle(new SolidColorBrush(Color.FromRgb(0, 120, 215)), headerRect);
            
            var headerText = new FormattedText(
                $"📊 Excel Template: {worksheet.Name} | Showing A1:{GetExcelColumnName(endCol)}{endRow} of {totalRows}×{totalCols} total",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                headerTypeface,
                16,
                Brushes.White
            );
            drawingContext.DrawText(headerText, new Point(20, 20));
            
            // Draw column headers (A, B, C, etc.)
            for (int col = startCol; col <= endCol; col++)
            {
                var colLetter = GetExcelColumnName(col);
                var colHeaderRect = new Rect(
                    (col - startCol) * cellWidth + 15,
                    headerHeight,
                    cellWidth,
                    cellHeight
                );
                
                drawingContext.FillRectangle(new SolidColorBrush(Color.FromRgb(242, 242, 242)), colHeaderRect);
                drawingContext.DrawRectangle(new Pen(Brushes.Gray, 1), colHeaderRect);
                
                var colHeaderText = new FormattedText(
                    colLetter,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    headerTypeface,
                    12,
                    Brushes.Black
                );
                
                var textX = colHeaderRect.X + (colHeaderRect.Width - colHeaderText.Width) / 2;
                var textY = colHeaderRect.Y + (colHeaderRect.Height - colHeaderText.Height) / 2;
                drawingContext.DrawText(colHeaderText, new Point(textX, textY));
            }
            
            // Draw data cells
            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = startCol; col <= endCol; col++)
                {
                    var cellRect = new Rect(
                        (col - startCol) * cellWidth + 15,
                        (row - startRow) * cellHeight + headerHeight + cellHeight,
                        cellWidth,
                        cellHeight
                    );
                    
                    // Alternate row colors
                    var isEvenRow = (row - startRow) % 2 == 0;
                    IBrush cellBackground = isEvenRow ? Brushes.White : new SolidColorBrush(Color.FromRgb(248, 248, 248));
                    
                    drawingContext.FillRectangle(cellBackground, cellRect);
                    drawingContext.DrawRectangle(new Pen(Brushes.LightGray, 0.5), cellRect);
                    
                    // Draw cell content
                    var cell = worksheet.Cells[row, col];
                    if (cell.Value != null)
                    {
                        var cellValue = cell.Value.ToString() ?? "";
                        
                        // Allow more text with larger cells
                        if (cellValue.Length > 18)
                            cellValue = cellValue.Substring(0, 15) + "...";
                        
                        // Choose font weight based on first row (headers)
                        var fontWeight = (row == startRow) ? FontWeight.Bold : FontWeight.Normal;
                        var fontSize = (row == startRow) ? 14 : 12; // Larger, more readable fonts
                        
                        var cellText = new FormattedText(
                            cellValue,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface("Segoe UI", FontStyle.Normal, fontWeight),
                            fontSize,
                            Brushes.Black
                        );
                        
                        var textX = cellRect.X + 5; // Left padding
                        var textY = cellRect.Y + (cellRect.Height - cellText.Height) / 2;
                        drawingContext.DrawText(cellText, new Point(textX, textY));
                    }
                }
            }
            
            // Add scroll indicators if there's more content
            var hasMoreRight = totalCols > viewportCols;
            var hasMoreDown = totalRows > viewportRows;
            
            if (hasMoreRight || hasMoreDown)
            {
                // Draw scroll indicators
                var indicatorY = headerHeight + (viewportRows * cellHeight) / 2;
                
                if (hasMoreRight)
                {
                    // Right arrow indicator
                    var rightArrowX = 15 + viewportCols * cellWidth + 10;
                    var arrowText = new FormattedText("→ More columns", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, Brushes.Blue);
                    drawingContext.DrawText(arrowText, new Point(rightArrowX, indicatorY));
                }
                
                if (hasMoreDown)
                {
                    // Down arrow indicator
                    var downArrowY = headerHeight + cellHeight + viewportRows * cellHeight + 5;
                    var arrowText = new FormattedText("↓ More rows", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, Brushes.Blue);
                    drawingContext.DrawText(arrowText, new Point(20, downArrowY));
                }
            }
            
            // Enhanced footer with navigation info
            var footerY = height - 40;
            var footerText = new FormattedText(
                $"📋 Viewport: A1:{GetExcelColumnName(endCol)}{endRow} | Total: {totalRows} rows × {totalCols} cols | Large cells for readability",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                11,
                Brushes.DarkBlue
            );
            drawingContext.DrawText(footerText, new Point(15, footerY));
            
            // Add second footer line for scroll hint
            if (hasMoreRight || hasMoreDown)
            {
                var hintY = footerY + 18;
                var hintText = new FormattedText(
                    "💡 Tip: This shows first 10×8 cells for readability. Full navigation coming soon!",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10,
                    Brushes.Gray
                );
                drawingContext.DrawText(hintText, new Point(15, hintY));
            }
            
            Console.WriteLine($"[DEBUG] Excel rendering completed successfully");
            return renderTarget;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error rendering Excel: {ex.Message}");
            return CreateExcelPreviewPlaceholder();
        }
    }
    
    /// <summary>
    /// Convert column number to Excel column name (1=A, 2=B, etc.)
    /// </summary>
    private string GetExcelColumnName(int columnNumber)
    {
        var result = "";
        while (columnNumber > 0)
        {
            columnNumber--;
            result = (char)('A' + columnNumber % 26) + result;
            columnNumber /= 26;
        }
        return result;
    }

    /// <summary>
    /// Create a placeholder bitmap for Excel preview
    /// </summary>
    private Bitmap CreateExcelPreviewPlaceholder(int width = 800, int height = 600)
    {
        var pixelSize = new PixelSize(width, height);
        var dpi = new Vector(96, 96);
        
        var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
        using var drawingContext = renderTarget.CreateDrawingContext();
        
        // Draw white background
        drawingContext.FillRectangle(Brushes.White, new Rect(0, 0, width, height));
        
        // Draw Excel icon
        var iconSize = Math.Min(width, height) * 0.15;
        var iconRect = new Rect((width - iconSize) / 2, (height - iconSize) / 2 - 60, iconSize, iconSize);
        drawingContext.FillRectangle(Brushes.Green, iconRect);
        
        // Draw text
        var typeface = new Typeface("Arial", FontStyle.Normal, FontWeight.Bold);
        var text = new FormattedText(
            "Excel Preview",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            Math.Min(16, width / 50),
            Brushes.DarkGreen
        );
        
        drawingContext.DrawText(text, new Point((width - text.Width) / 2, (height + iconSize) / 2 - 30));
        
        return renderTarget;
    }

    /// <summary>
    /// Render PDF page to bitmap using Docnet.Core
    /// </summary>
    private async Task<Bitmap?> RenderPdfPageToBitmap(IDocReader docReader, int pageIndex)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Rendering page {pageIndex + 1} using Docnet.Core");
            
            // Get page reader
            using var pageReader = docReader.GetPageReader(pageIndex);
            
            // Render page to raw bitmap data
            var rawBytes = pageReader.GetImage();
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            
            Console.WriteLine($"[DEBUG] Page dimensions: {width}x{height}");
            
            // Convert raw bytes to Avalonia bitmap
            return ConvertRawBytesToBitmap(rawBytes, width, height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error rendering PDF page: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Convert raw bytes from Docnet.Core to Avalonia bitmap
    /// </summary>
    private Bitmap? ConvertRawBytesToBitmap(byte[] rawBytes, int width, int height)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Converting raw bytes to bitmap: {width}x{height}, {rawBytes.Length} bytes");
            
            // Docnet.Core provides RGBA format (4 bytes per pixel)
            var expectedSize = width * height * 4;
            if (rawBytes.Length < expectedSize)
            {
                Console.WriteLine($"[DEBUG] Warning: Raw bytes size {rawBytes.Length} < expected {expectedSize}");
            }
            
            // Try different pixel formats to fix color issues
            // First try BGRA (often more accurate for PDFs)
            var bitmap = TryCreateBitmapWithFormat(rawBytes, width, height, Avalonia.Platform.PixelFormat.Bgra8888);
            if (bitmap != null)
            {
                Console.WriteLine($"[DEBUG] BGRA bitmap creation successful");
                return bitmap;
            }
            
            // Fallback to RGBA if BGRA fails
            bitmap = TryCreateBitmapWithFormat(rawBytes, width, height, Avalonia.Platform.PixelFormat.Rgba8888);
            if (bitmap != null)
            {
                Console.WriteLine($"[DEBUG] RGBA bitmap creation successful");
                return bitmap;
            }
            
            throw new Exception("Both BGRA and RGBA formats failed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error in direct bitmap creation: {ex.Message}");
            Console.WriteLine($"[DEBUG] Falling back to SkiaSharp method");
            
            // Fallback to previous method if direct creation fails
            return ConvertRawBytesToBitmapFallback(rawBytes, width, height);
        }
    }
    
    /// <summary>
    /// Try creating bitmap with specific pixel format
    /// </summary>
    private WriteableBitmap? TryCreateBitmapWithFormat(byte[] rawBytes, int width, int height, Avalonia.Platform.PixelFormat format)
    {
        try
        {
            var pixelSize = new PixelSize(width, height);
            var dpi = new Vector(96, 96);
            var writeableBitmap = new WriteableBitmap(pixelSize, dpi, format, Avalonia.Platform.AlphaFormat.Unpremul);
            
            using (var lockedBitmap = writeableBitmap.Lock())
            {
                // Get the destination buffer
                var destPtr = lockedBitmap.Address;
                var destStride = lockedBitmap.RowBytes;
                
                // Copy raw bytes directly to bitmap buffer
                // Handle potential stride differences
                var sourceStride = width * 4; // RGBA = 4 bytes per pixel
                var copyLength = Math.Min(sourceStride, destStride);
                
                for (int y = 0; y < height; y++)
                {
                    var sourceOffset = y * sourceStride;
                    var destOffset = y * destStride;
                    
                    if (sourceOffset + copyLength <= rawBytes.Length)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            rawBytes, 
                            sourceOffset, 
                            IntPtr.Add(destPtr, destOffset), 
                            copyLength
                        );
                    }
                }
            }
            
            return writeableBitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] {format} format failed: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Fallback method using SkiaSharp if direct bitmap creation fails
    /// </summary>
    private Bitmap? ConvertRawBytesToBitmapFallback(byte[] rawBytes, int width, int height)
    {
        try
        {
            var skBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var skPixels = skBitmap.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(rawBytes, 0, skPixels, Math.Min(rawBytes.Length, width * height * 4));
            
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using var canvas = surface.Canvas;
            canvas.DrawBitmap(skBitmap, 0, 0);
            
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Fallback method also failed: {ex.Message}");
            return null;
        }
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