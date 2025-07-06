using System;
using System.Data;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DocumentExtractor.Desktop.ViewModels;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Visual Template Mapping View - provides click-to-teach interface for field mapping using HTML preview
/// </summary>
public partial class TemplateMappingView : UserControl
{
    public TemplateMappingView()
    {
        InitializeComponent();
        
        // Subscribe to ViewModel property changes for Canvas drawing
        this.DataContextChanged += TemplateMappingView_DataContextChanged;
        
        Console.WriteLine("🗺️ TemplateMappingView initialized with Canvas drawing support");
    }

    private void TemplateMappingView_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is TemplateMappingViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TemplateMappingViewModel.CanvasStatus) && 
            sender is TemplateMappingViewModel viewModel)
        {
            // Check if canvas drawing was initiated
            if (viewModel.CanvasStatus.Contains("Canvas drawing initiated") && 
                viewModel.ExcelData != null)
            {
                DrawExcelGridOnCanvas(viewModel.ExcelData);
            }
        }
    }

    /// <summary>
    /// Draw Excel grid on Canvas with click-to-teach functionality
    /// </summary>
    private void DrawExcelGridOnCanvas(DataTable dataTable)
    {
        try
        {
            var canvas = this.FindControl<Canvas>("ExcelCanvas");
            if (canvas == null) 
            {
                Console.WriteLine("❌ Canvas control 'ExcelCanvas' not found");
                return;
            }

            // Clear existing content
            canvas.Children.Clear();

            const double cellWidth = 120;  // Wider cells for better readability
            const double cellHeight = 30;  // Taller cells for better text display
            const double startX = 10;
            const double startY = 10;

            // Draw headers
            for (int col = 0; col < dataTable.Columns.Count; col++)
            {
                var headerBorder = new Border
                {
                    Width = cellWidth,
                    Height = cellHeight,
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Avalonia.Thickness(1),
                    Background = Brushes.LightGray,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                var headerText = new TextBlock
                {
                    Text = dataTable.Columns[col].ColumnName,
                    FontWeight = FontWeight.Bold,
                    FontSize = 12,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                headerBorder.Child = headerText;
                Canvas.SetLeft(headerBorder, startX + col * cellWidth);
                Canvas.SetTop(headerBorder, startY);
                
                // Add click handler for header
                headerBorder.PointerPressed += (s, e) => OnCellClicked(-1, col, dataTable.Columns[col].ColumnName);
                
                canvas.Children.Add(headerBorder);
            }

            // Draw data rows
            for (int row = 0; row < dataTable.Rows.Count; row++)
            {
                for (int col = 0; col < dataTable.Columns.Count; col++)
                {
                    var cellBorder = new Border
                    {
                        Width = cellWidth,
                        Height = cellHeight,
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Avalonia.Thickness(1),
                        Background = Brushes.White,
                        Cursor = new Cursor(StandardCursorType.Hand)
                    };

                    var cellValue = dataTable.Rows[row][col]?.ToString() ?? "";
                    var cellText = new TextBlock
                    {
                        Text = cellValue.Length > 15 ? cellValue.Substring(0, 15) + "..." : cellValue,
                        FontSize = 11,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    
                    // Set tooltip for long text
                    if (cellValue.Length > 15)
                    {
                        ToolTip.SetTip(cellText, cellValue);
                    }

                    cellBorder.Child = cellText;
                    Canvas.SetLeft(cellBorder, startX + col * cellWidth);
                    Canvas.SetTop(cellBorder, startY + (row + 1) * cellHeight);
                    
                    // Add click handler for data cell
                    var capturedRow = row;
                    var capturedCol = col;
                    var capturedValue = cellValue;
                    cellBorder.PointerPressed += (s, e) => OnCellClicked(capturedRow, capturedCol, capturedValue);
                    
                    canvas.Children.Add(cellBorder);
                }
            }

            // Update canvas size
            canvas.Width = startX * 2 + dataTable.Columns.Count * cellWidth;
            canvas.Height = startY * 2 + (dataTable.Rows.Count + 1) * cellHeight;

            Console.WriteLine($"🎨 Canvas drawing completed: {dataTable.Rows.Count} rows × {dataTable.Columns.Count} columns with click handlers");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error drawing Canvas: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle cell clicks for field mapping
    /// </summary>
    private void OnCellClicked(int row, int col, string cellValue)
    {
        try
        {
            if (DataContext is TemplateMappingViewModel viewModel)
            {
                // Generate Excel-style cell reference
                var cellRef = GetExcelCellReference(row + 1, col); // +1 because headers are row 0
                
                Console.WriteLine($"🎯 Cell clicked: {cellRef} (Row: {row}, Col: {col}, Value: '{cellValue}')");
                
                // Call the ViewModel's cell selection handler
                viewModel.HandleCellSelection(row, col);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling cell click: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert row/column to Excel-style cell reference (A1, B2, etc.)
    /// </summary>
    private string GetExcelCellReference(int row, int col)
    {
        string columnLetter = "";
        int tempCol = col;
        
        while (tempCol >= 0)
        {
            columnLetter = (char)('A' + tempCol % 26) + columnLetter;
            tempCol = tempCol / 26 - 1;
        }
        
        return $"{columnLetter}{row + 1}";
    }

    /// <summary>
    /// Handle template view loading and WebView setup
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if (DataContext is TemplateMappingViewModel viewModel)
        {
            // Set up WebView message handling
            SetupWebViewCommunication(viewModel);
            
            // Subscribe to HTML content changes
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TemplateMappingViewModel.HtmlPreviewContent) && 
                    !string.IsNullOrEmpty(viewModel.HtmlPreviewContent))
                {
                    LoadHtmlContent(viewModel.HtmlPreviewContent);
                }
            };
            
            Console.WriteLine("🎯 Template mapping view loaded and ready for interaction");
        }
    }

    /// <summary>
    /// Set up communication between WebView JavaScript and C# ViewModel
    /// </summary>
    private void SetupWebViewCommunication(TemplateMappingViewModel viewModel)
    {
        try
        {
            // TODO: WebView functionality temporarily disabled due to compilation errors
            // Need to add proper Avalonia WebView package
            Console.WriteLine("🔗 WebView communication setup skipped (package not available)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error setting up WebView communication: {ex.Message}");
        }
    }

    /// <summary>
    /// Load HTML content into the WebView
    /// </summary>
    private void LoadHtmlContent(string htmlContent)
    {
        try
        {
            // TODO: WebView functionality temporarily disabled due to compilation errors
            // Need to add proper Avalonia WebView package
            Console.WriteLine($"🌐 HTML content ready for WebView ({htmlContent.Length} characters)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading HTML content: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle clicks on the template preview area (fallback for non-Excel templates)
    /// </summary>
    private async void OnTemplateClicked(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (DataContext is TemplateMappingViewModel viewModel && sender is Border border)
            {
                // Get click position relative to the template
                var position = e.GetPosition(border);
                
                // Handle the template click
                await viewModel.HandleTemplateClick(position.X, position.Y);
                
                Console.WriteLine($"🎯 Template clicked at ({position.X:F0}, {position.Y:F0})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling template click: {ex.Message}");
        }
    }
}

/// <summary>
/// Message structure for WebView JavaScript communication
/// </summary>
public class WebViewMessage
{
    public string Action { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }
    public string CellRef { get; set; } = "";
    public string CellValue { get; set; } = "";
}