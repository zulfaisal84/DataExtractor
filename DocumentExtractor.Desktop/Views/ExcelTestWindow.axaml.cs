using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OfficeOpenXml;
using SkiaSharp;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace DocumentExtractor.Desktop.Views;

public partial class ExcelTestWindow : Window
{
    private string? _currentFilePath;
    
    public ExcelTestWindow()
    {
        InitializeComponent();
        SetupEventHandlers();
        
        // Set EPPlus license
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    private void SetupEventHandlers()
    {
        LoadFileButton.Click += LoadFileButton_Click;
        RenderButton.Click += RenderButton_Click;
        CanvasSizeCombo.SelectionChanged += OnSettingChanged;
        CellCountCombo.SelectionChanged += OnSettingChanged;
    }

    private async void LoadFileButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Excel File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Excel Files")
                    {
                        Patterns = new[] { "*.xlsx", "*.xls" }
                    }
                }
            });

            if (files.Count > 0)
            {
                _currentFilePath = files[0].Path.LocalPath;
                FileNameText.Text = Path.GetFileName(_currentFilePath);
                RenderButton.IsEnabled = true;
                
                DebugInfo.Text = $"Debug: Loaded {Path.GetFileName(_currentFilePath)}";
                
                // Auto-render when file is loaded
                await RenderExcel();
            }
        }
        catch (Exception ex)
        {
            DebugInfo.Text = $"Debug: Error loading file - {ex.Message}";
        }
    }

    private async void RenderButton_Click(object? sender, RoutedEventArgs e)
    {
        await RenderExcel();
    }

    private async void OnSettingChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_currentFilePath != null && RenderButton.IsEnabled)
        {
            await RenderExcel();
        }
    }

    private async Task RenderExcel()
    {
        if (_currentFilePath == null) return;

        try
        {
            // Get canvas dimensions from combo box
            var canvasSize = GetSelectedCanvasSize();
            var cellCount = GetSelectedCellCount();
            
            // Update canvas size
            TestCanvas.Width = canvasSize.Width;
            TestCanvas.Height = canvasSize.Height;
            
            DebugInfo.Text = $"Debug: Rendering {Path.GetFileName(_currentFilePath)} at {canvasSize.Width}×{canvasSize.Height}";
            
            // Render Excel with test method
            var bitmap = await RenderExcelToFullSizeBitmap(_currentFilePath, (int)canvasSize.Width, (int)canvasSize.Height, cellCount.cols, cellCount.rows);
            
            if (bitmap != null)
            {
                // Clear canvas and add image
                TestCanvas.Children.Clear();
                
                var image = new Image
                {
                    Source = bitmap,
                    Width = canvasSize.Width,
                    Height = canvasSize.Height,
                    Stretch = Stretch.Fill
                };
                
                TestCanvas.Children.Add(image);
                
                BitmapInfo.Text = $"Bitmap: {bitmap.PixelSize.Width}×{bitmap.PixelSize.Height} pixels";
                
                // Calculate cell size
                var cellWidth = (int)(canvasSize.Width / cellCount.cols);
                var cellHeight = (int)(canvasSize.Height / cellCount.rows);
                CellInfo.Text = $"Cells: {cellCount.cols}×{cellCount.rows} = {cellWidth}×{cellHeight}px each";
            }
        }
        catch (Exception ex)
        {
            DebugInfo.Text = $"Debug: Render error - {ex.Message}";
        }
    }

    private Size GetSelectedCanvasSize()
    {
        return CanvasSizeCombo.SelectedIndex switch
        {
            0 => new Size(800, 600),
            1 => new Size(1200, 800),
            2 => new Size(1400, 900),
            3 => new Size(1600, 1000),
            _ => new Size(1400, 900)
        };
    }

    private (int cols, int rows) GetSelectedCellCount()
    {
        return CellCountCombo.SelectedIndex switch
        {
            0 => (4, 3),
            1 => (6, 4),
            2 => (8, 5),
            3 => (10, 6),
            _ => (6, 4)
        };
    }

    /// <summary>
    /// TEST METHOD: Render Excel to full-size bitmap (no tiny previews!)
    /// </summary>
    private async Task<Bitmap?> RenderExcelToFullSizeBitmap(string filePath, int canvasWidth, int canvasHeight, int viewportCols, int viewportRows)
    {
        try
        {
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];
            
            var dimension = worksheet.Dimension;
            if (dimension == null) return null;
            
            // KEY FIX: Create bitmap at FULL canvas size
            var pixelSize = new PixelSize(canvasWidth, canvasHeight);
            var dpi = new Vector(96, 96);
            var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
            
            using var drawingContext = renderTarget.CreateDrawingContext();
            
            // White background
            drawingContext.FillRectangle(Brushes.White, new Rect(0, 0, canvasWidth, canvasHeight));
            
            // LARGE CELL CALCULATIONS
            var headerHeight = 60;
            var margin = 20;
            var availableWidth = canvasWidth - margin * 2;
            var availableHeight = canvasHeight - headerHeight - margin * 2;
            
            var cellWidth = availableWidth / viewportCols;
            var cellHeight = availableHeight / viewportRows;
            
            // Draw header
            var headerRect = new Rect(margin, margin, availableWidth, headerHeight - 10);
            drawingContext.FillRectangle(new SolidColorBrush(Color.FromRgb(0, 120, 215)), headerRect);
            
            var headerText = new FormattedText(
                $"🧪 TEST: {worksheet.Name} | Canvas: {canvasWidth}×{canvasHeight} | Cells: {viewportCols}×{viewportRows} = {cellWidth:F0}×{cellHeight:F0}px each",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
                18,
                Brushes.White
            );
            drawingContext.DrawText(headerText, new Point(margin + 10, margin + 15));
            
            // Draw column headers
            for (int col = 0; col < viewportCols && col < dimension.End.Column; col++)
            {
                var colLetter = GetExcelColumnName(col + 1);
                var colRect = new Rect(
                    margin + col * cellWidth,
                    headerHeight + margin,
                    cellWidth,
                    cellHeight * 0.6
                );
                
                drawingContext.FillRectangle(new SolidColorBrush(Color.FromRgb(240, 240, 240)), colRect);
                drawingContext.DrawRectangle(new Pen(Brushes.Gray, 1), colRect);
                
                var colText = new FormattedText(colLetter, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold), 16, Brushes.Black);
                
                var textX = colRect.X + (colRect.Width - colText.Width) / 2;
                var textY = colRect.Y + (colRect.Height - colText.Height) / 2;
                drawingContext.DrawText(colText, new Point(textX, textY));
            }
            
            // Draw data cells
            for (int row = 0; row < viewportRows && row < dimension.End.Row; row++)
            {
                for (int col = 0; col < viewportCols && col < dimension.End.Column; col++)
                {
                    var cellRect = new Rect(
                        margin + col * cellWidth,
                        headerHeight + margin + cellHeight * 0.6 + row * cellHeight,
                        cellWidth,
                        cellHeight
                    );
                    
                    // Alternate row colors
                    IBrush cellBackground = row % 2 == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(248, 248, 248));
                    drawingContext.FillRectangle(cellBackground, cellRect);
                    drawingContext.DrawRectangle(new Pen(Brushes.LightGray, 1), cellRect);
                    
                    // Cell content
                    var cell = worksheet.Cells[row + 1, col + 1];
                    if (cell.Value != null)
                    {
                        var cellValue = cell.Value.ToString() ?? "";
                        if (cellValue.Length > 20) cellValue = cellValue.Substring(0, 17) + "...";
                        
                        var fontSize = Math.Min(20, cellHeight * 0.4); // Scale font with cell size
                        var cellText = new FormattedText(cellValue, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal), fontSize, Brushes.Black);
                        
                        var textX = cellRect.X + 8;
                        var textY = cellRect.Y + (cellRect.Height - cellText.Height) / 2;
                        drawingContext.DrawText(cellText, new Point(textX, textY));
                    }
                }
            }
            
            return renderTarget;
        }
        catch (Exception ex)
        {
            DebugInfo.Text = $"Debug: Bitmap creation error - {ex.Message}";
            return null;
        }
    }

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
}