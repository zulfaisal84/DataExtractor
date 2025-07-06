using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DocumentExtractor.Desktop.ViewModels;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Visual Template Mapping View - provides click-to-teach interface for field mapping with Excel-like selection
/// Enhanced with Tab 4 proven selection system: multi-select, keyboard shortcuts, visual feedback
/// </summary>
public partial class TemplateMappingView : UserControl
{
    // Selection state management (from Tab 4)
    private Border? _selectedCell;
    private readonly Dictionary<string, string> _fieldMappings = new();
    private readonly Dictionary<string, Border> _cellBorders = new();
    private readonly Dictionary<string, Border> _rowHeaders = new();
    private readonly Dictionary<string, Border> _columnHeaders = new();
    private Border? _selectAllButton;
    private bool _isMappingMode = false;
    
    // Advanced selection state (Tab 4 features)
    private SelectionType _currentSelection = SelectionType.None;
    private int _selectedColumn = -1;
    private int _selectedRow = -1;
    private readonly HashSet<string> _selectedCells = new();
    
    // Multi-selection state  
    private string? _anchorCell = null; // Last selected cell for range extension
    private bool _shiftClickProcessed = false; // Prevent double processing
    
    public enum SelectionType
    {
        None,
        Cell,
        Column,
        Row,
        All
    }
    
    public TemplateMappingView()
    {
        InitializeComponent();
        
        // Make the UserControl focusable for keyboard events
        this.Focusable = true;
        this.TabIndex = 0;
        
        // Subscribe to ViewModel property changes for Canvas drawing
        this.DataContextChanged += TemplateMappingView_DataContextChanged;
        
        // Add global ESC key handler for clearing selections
        this.KeyDown += TemplateMappingView_KeyDown;
        
        // Set focus when loaded to enable ESC key
        this.Loaded += (s, e) => this.Focus();
        
        Console.WriteLine("🗺️ TemplateMappingView initialized with Excel-like selection system");
    }

    /// <summary>
    /// Global ESC key handler for clearing selections (Tab 4 feature)
    /// </summary>
    private void TemplateMappingView_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Escape)
            {
                Console.WriteLine($"⌨️ ESC DETECTED: Clearing {_currentSelection} mode ({_selectedCells.Count} cells)");
                
                // Clear all selections and return to normal state
                ClearAllSelections();
                
                // Update ViewModel status
                if (DataContext is TemplateMappingViewModel viewModel)
                {
                    viewModel.StatusMessage = "⌨️ ESC pressed - All selections cleared";
                }
                
                Console.WriteLine("✅ ESC: All selections cleared successfully");
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling ESC key: {ex.Message}");
        }
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
    /// Draw Excel grid on Canvas with enhanced Tab 4 selection system
    /// Features: Excel-like headers, select-all, multi-selection, keyboard shortcuts
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

            // Clear existing content and reset state
            canvas.Children.Clear();
            _cellBorders.Clear();
            _rowHeaders.Clear();
            _columnHeaders.Clear();
            _selectedCells.Clear();
            _isMappingMode = true; // Enable mapping by default

            const double cellWidth = 120;  
            const double cellHeight = 30;  
            const double rowHeaderWidth = 50;  // Width for row numbers
            const double startX = 10;
            const double startY = 10;

            // 1. Draw SELECT ALL button (top-left corner) - Tab 4 feature
            _selectAllButton = new Border
            {
                Width = rowHeaderWidth,
                Height = cellHeight,
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Avalonia.Thickness(2),
                Background = Brushes.LightGray,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var selectAllText = new TextBlock
            {
                Text = "📋",
                FontSize = 16,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            _selectAllButton.Child = selectAllText;
            Canvas.SetLeft(_selectAllButton, startX);
            Canvas.SetTop(_selectAllButton, startY);
            _selectAllButton.PointerPressed += (s, e) => OnSelectAllClicked();
            ToolTip.SetTip(selectAllText, "Click to select all cells");
            canvas.Children.Add(_selectAllButton);

            // 2. Draw COLUMN HEADERS with Excel-style letters - Tab 4 feature
            for (int col = 0; col < dataTable.Columns.Count; col++)
            {
                var columnLetter = GetExcelColumnLetter(col);
                var headerBorder = new Border
                {
                    Width = cellWidth,
                    Height = cellHeight,
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Avalonia.Thickness(2),
                    Background = Brushes.LightGray,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = "column_header"
                };

                var headerText = new TextBlock
                {
                    Text = $"{columnLetter}",
                    FontWeight = FontWeight.Bold,
                    FontSize = 12,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    TextAlignment = Avalonia.Media.TextAlignment.Center
                };

                headerBorder.Child = headerText;
                Canvas.SetLeft(headerBorder, startX + rowHeaderWidth + col * cellWidth);
                Canvas.SetTop(headerBorder, startY);
                
                _columnHeaders[columnLetter] = headerBorder;
                
                // Add column selection handler - Tab 4 feature
                var capturedColIndex = col;
                headerBorder.PointerPressed += (s, e) => OnColumnHeaderClicked(capturedColIndex);
                ToolTip.SetTip(headerText, $"Click to select entire column {columnLetter}");
                
                canvas.Children.Add(headerBorder);
            }

            // 3. Draw ROW HEADERS with numbers - Tab 4 feature
            for (int row = 0; row < dataTable.Rows.Count; row++)
            {
                var rowNumber = row + 1;
                var rowHeaderBorder = new Border
                {
                    Width = rowHeaderWidth,
                    Height = cellHeight,
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Avalonia.Thickness(2),
                    Background = Brushes.LightGray,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = "row_header"
                };

                var rowHeaderText = new TextBlock
                {
                    Text = rowNumber.ToString(),
                    FontWeight = FontWeight.Bold,
                    FontSize = 12,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                rowHeaderBorder.Child = rowHeaderText;
                Canvas.SetLeft(rowHeaderBorder, startX);
                Canvas.SetTop(rowHeaderBorder, startY + (row + 1) * cellHeight);
                
                _rowHeaders[rowNumber.ToString()] = rowHeaderBorder;
                
                // Add row selection handler - Tab 4 feature
                var capturedRowIndex = row;
                rowHeaderBorder.PointerPressed += (s, e) => OnRowHeaderClicked(capturedRowIndex);
                ToolTip.SetTip(rowHeaderText, $"Click to select entire row {rowNumber}");
                
                canvas.Children.Add(rowHeaderBorder);
            }

            // 4. Draw DATA CELLS with enhanced click handling - Tab 4 features
            for (int row = 0; row < dataTable.Rows.Count; row++)
            {
                for (int col = 0; col < dataTable.Columns.Count; col++)
                {
                    var cellRef = GetExcelCellReference(row, col);
                    var cellBorder = new Border
                    {
                        Width = cellWidth,
                        Height = cellHeight,
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Avalonia.Thickness(1),
                        Background = Brushes.White,
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Tag = cellRef
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
                    Canvas.SetLeft(cellBorder, startX + rowHeaderWidth + col * cellWidth);
                    Canvas.SetTop(cellBorder, startY + (row + 1) * cellHeight);
                    
                    // Store cell reference for selection system
                    _cellBorders[cellRef] = cellBorder;
                    
                    // Enhanced click handler with Tab 4 multi-selection features
                    cellBorder.PointerPressed += (s, e) => OnCellPointerPressed(cellRef, cellValue, e);
                    
                    // Double-click handler for field mapping
                    cellBorder.DoubleTapped += (s, e) => OnCellDoubleClicked(cellRef, cellValue);
                    
                    canvas.Children.Add(cellBorder);
                }
            }

            // Update canvas size
            canvas.Width = startX * 2 + rowHeaderWidth + dataTable.Columns.Count * cellWidth;
            canvas.Height = startY * 2 + (dataTable.Rows.Count + 1) * cellHeight;

            Console.WriteLine($"🎨 Enhanced Canvas drawing completed: {dataTable.Rows.Count} rows × {dataTable.Columns.Count} columns with Tab 4 selection system");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error drawing enhanced Canvas: {ex.Message}");
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
        return $"{GetExcelColumnLetter(col)}{row + 1}";
    }
    
    /// <summary>
    /// Convert column index to Excel-style letter (A, B, C, etc.) - Tab 4 feature
    /// </summary>
    private string GetExcelColumnLetter(int col)
    {
        string columnLetter = "";
        int tempCol = col;
        
        while (tempCol >= 0)
        {
            columnLetter = (char)('A' + tempCol % 26) + columnLetter;
            tempCol = tempCol / 26 - 1;
        }
        
        return columnLetter;
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
    
    // ===================================================================================
    // TAB 4 SELECTION SYSTEM METHODS - Enhanced Excel-like selection functionality
    // ===================================================================================
    
    /// <summary>
    /// Handle enhanced cell pointer events with Tab 4 multi-selection features
    /// </summary>
    private void OnCellPointerPressed(string cellRef, string cellValue, PointerPressedEventArgs e)
    {
        try
        {
            if (!_isMappingMode) return;

            bool isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            
            // Check for Shift+Click (range extension)
            if (isShiftPressed && _anchorCell != null)
            {
                Console.WriteLine($"🔗 Shift+Click range: {_anchorCell} to {cellRef}");
                _shiftClickProcessed = true;
                HandleShiftClick(cellRef, cellValue);
                e.Handled = true;
                return;
            }

            // Check for Ctrl+Click (non-contiguous selection)
            if (isCtrlPressed)
            {
                Console.WriteLine($"🟢 Ctrl+Click: {cellRef}");
                HandleCtrlClick(cellRef, cellValue);
                e.Handled = true;
                return;
            }

            // Normal single click - only select the cell (no automatic field mapping dialog)
            HandleSingleCellSelection(cellRef, cellValue);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in enhanced cell pointer pressed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle single cell selection without field mapping dialog - Tab 4 enhancement
    /// </summary>
    private void HandleSingleCellSelection(string cellRef, string cellValue)
    {
        try
        {
            if (!_isMappingMode) return;

            // Excel behavior: Single cell click always selects only that cell
            ClearAllSelections();
            _currentSelection = SelectionType.Cell;

            // Highlight the clicked cell
            if (_cellBorders.TryGetValue(cellRef, out var selectedBorder))
            {
                selectedBorder.BorderBrush = Brushes.Blue;
                selectedBorder.BorderThickness = new Avalonia.Thickness(3);
                selectedBorder.Background = Brushes.LightYellow;
                _selectedCell = selectedBorder;
                _selectedCells.Add(cellRef);
                
                Console.WriteLine($"🎯 Cell {cellRef} selected (value: '{cellValue}')");
            }
            
            // Set anchor cell for future Shift+Click operations
            _anchorCell = cellRef;
            
            // Update status without opening field mapping dialog
            UpdateSelectionStatus();
            
            // Note: Don't call viewModel.HandleCellSelection() here as it triggers the dialog
            // Only update selection status for visual feedback
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling single cell selection: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle double-click for field mapping - opens the mapping dialog
    /// </summary>
    private void OnCellDoubleClicked(string cellRef, string cellValue)
    {
        try
        {
            if (!_isMappingMode) return;
            
            Console.WriteLine($"🎯 Double-click detected: {cellRef} ('{cellValue}') - Opening field mapping dialog");
            
            // First ensure the cell is selected
            HandleSingleCellSelection(cellRef, cellValue);
            
            // Then open the field mapping dialog
            var rowIndex = GetRowIndexFromCellRef(cellRef) - 1;
            var colIndex = GetColumnIndexFromCellRef(cellRef);
            
            OnCellClicked(rowIndex, colIndex, cellValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling double-click: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle Ctrl+Click multi-selection - Tab 4 feature
    /// </summary>
    private void HandleCtrlClick(string cellRef, string cellValue)
    {
        try
        {
            if (_selectedCells.Contains(cellRef))
            {
                // Remove cell from selection (toggle off)
                _selectedCells.Remove(cellRef);
                if (_cellBorders.TryGetValue(cellRef, out var border))
                {
                    border.Background = Brushes.White;
                    border.BorderBrush = Brushes.LightGray;
                    border.BorderThickness = new Avalonia.Thickness(1);
                }
                Console.WriteLine($"🟢 Ctrl+Click: {cellRef} removed from selection");
            }
            else
            {
                // Add cell to selection with green highlighting
                _selectedCells.Add(cellRef);
                _anchorCell = cellRef; // Set anchor for Shift+Click
                
                if (_cellBorders.TryGetValue(cellRef, out var border))
                {
                    border.Background = Brushes.LightGreen;
                    border.BorderBrush = Brushes.Green;
                    border.BorderThickness = new Avalonia.Thickness(2);
                }
                Console.WriteLine($"🟢 Ctrl+Click: {cellRef} added to selection");
            }
            
            // Update status
            UpdateSelectionStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in Ctrl+Click: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle Shift+Click range selection - Tab 4 feature
    /// </summary>
    private void HandleShiftClick(string cellRef, string cellValue)
    {
        try
        {
            if (_anchorCell == null) return;
            
            // Parse anchor and target coordinates
            var anchorCoords = ParseCellReference(_anchorCell);
            var targetCoords = ParseCellReference(cellRef);
            
            if (!anchorCoords.HasValue || !targetCoords.HasValue) return;
            
            // Calculate rectangular range
            var minRow = Math.Min(anchorCoords.Value.row, targetCoords.Value.row);
            var maxRow = Math.Max(anchorCoords.Value.row, targetCoords.Value.row);
            var minCol = Math.Min(anchorCoords.Value.col, targetCoords.Value.col);
            var maxCol = Math.Max(anchorCoords.Value.col, targetCoords.Value.col);
            
            // Clear previous selection visuals but keep anchor
            ClearSelectionVisuals();
            
            // Highlight all cells in range with purple/lavender styling
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    var rangeCellRef = GetExcelCellReference(row, col);
                    _selectedCells.Add(rangeCellRef);
                    
                    if (_cellBorders.TryGetValue(rangeCellRef, out var border))
                    {
                        border.Background = Brushes.Lavender;
                        border.BorderBrush = Brushes.Purple;
                        border.BorderThickness = new Avalonia.Thickness(2);
                    }
                }
            }
            
            Console.WriteLine($"🔗 Shift+Click range: {_anchorCell} to {cellRef} ({_selectedCells.Count} cells)");
            UpdateSelectionStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in Shift+Click: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle column header clicks - Tab 4 feature
    /// </summary>
    private void OnColumnHeaderClicked(int columnIndex)
    {
        try
        {
            var columnLetter = GetExcelColumnLetter(columnIndex);
            
            // Smart switching - only clear if not switching between columns
            if (_currentSelection == SelectionType.Column && _selectedColumn == columnIndex)
            {
                Console.WriteLine($"📊 Column {columnLetter} deselected");
                ClearAllSelections();
                return;
            }
            
            // Clear selections only if switching from different selection type
            if (_currentSelection != SelectionType.Column)
            {
                ClearAllSelections();
            }
            else
            {
                ClearSelectionVisuals();
            }
            
            _currentSelection = SelectionType.Column;
            _selectedColumn = columnIndex;
            
            // Highlight column header
            if (_columnHeaders.TryGetValue(columnLetter, out var columnHeader))
            {
                columnHeader.Background = Brushes.LightBlue;
                columnHeader.BorderBrush = Brushes.Blue;
            }
            
            // Highlight all cells in this column
            foreach (var kvp in _cellBorders)
            {
                var cellRef = kvp.Key;
                var border = kvp.Value;
                
                var cellColumnIndex = GetColumnIndexFromCellRef(cellRef);
                if (cellColumnIndex == columnIndex)
                {
                    border.Background = Brushes.LightBlue;
                    border.BorderBrush = Brushes.Blue;
                    border.BorderThickness = new Avalonia.Thickness(2);
                    _selectedCells.Add(cellRef);
                }
            }
            
            Console.WriteLine($"📊 Column {columnLetter} selected ({_selectedCells.Count} cells)");
            UpdateSelectionStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in column selection: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle row header clicks - Tab 4 feature
    /// </summary>
    private void OnRowHeaderClicked(int rowIndex)
    {
        try
        {
            var rowNumber = rowIndex + 1;
            
            // Smart switching - only clear if not switching between rows
            if (_currentSelection == SelectionType.Row && _selectedRow == rowIndex)
            {
                Console.WriteLine($"📄 Row {rowNumber} deselected");
                ClearAllSelections();
                return;
            }
            
            // Clear selections only if switching from different selection type
            if (_currentSelection != SelectionType.Row)
            {
                ClearAllSelections();
            }
            else
            {
                ClearSelectionVisuals();
            }
            
            _currentSelection = SelectionType.Row;
            _selectedRow = rowIndex;
            
            // Highlight row header
            if (_rowHeaders.TryGetValue(rowNumber.ToString(), out var rowHeader))
            {
                rowHeader.Background = Brushes.LightBlue;
                rowHeader.BorderBrush = Brushes.Blue;
            }
            
            // Highlight all cells in this row
            foreach (var kvp in _cellBorders)
            {
                var cellRef = kvp.Key;
                var border = kvp.Value;
                
                var cellRowNumber = GetRowIndexFromCellRef(cellRef);
                if (cellRowNumber == rowNumber)
                {
                    border.Background = Brushes.LightBlue;
                    border.BorderBrush = Brushes.Blue;
                    border.BorderThickness = new Avalonia.Thickness(2);
                    _selectedCells.Add(cellRef);
                }
            }
            
            Console.WriteLine($"📄 Row {rowNumber} selected ({_selectedCells.Count} cells)");
            UpdateSelectionStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in row selection: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle select-all button click with smart toggle - Tab 4 feature
    /// </summary>
    private void OnSelectAllClicked()
    {
        try
        {
            // Smart toggle - if already in select-all mode, deselect everything
            if (_currentSelection == SelectionType.All)
            {
                Console.WriteLine("📋 SELECT ALL deselected - Clearing entire grid");
                ClearAllSelections();
                return;
            }
            
            Console.WriteLine("📋 SELECT ALL clicked - Selecting entire grid");
            
            ClearAllSelections();
            _currentSelection = SelectionType.All;
            
            // Highlight all cells
            foreach (var kvp in _cellBorders)
            {
                var border = kvp.Value;
                border.Background = Brushes.LightBlue;
                border.BorderBrush = Brushes.Blue;
                border.BorderThickness = new Avalonia.Thickness(2);
                _selectedCells.Add(kvp.Key);
            }
            
            // Highlight select-all button
            if (_selectAllButton != null)
            {
                _selectAllButton.Background = Brushes.Blue;
                _selectAllButton.BorderBrush = Brushes.DarkBlue;
            }
            
            Console.WriteLine($"📋 All cells selected ({_selectedCells.Count} cells)");
            UpdateSelectionStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in select all: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Clear selection visuals without resetting state - Tab 4 feature
    /// </summary>
    private void ClearSelectionVisuals()
    {
        try
        {
            // Reset all cells
            foreach (var kvp in _cellBorders)
            {
                var border = kvp.Value;
                border.Background = Brushes.White;
                border.BorderBrush = Brushes.LightGray;
                border.BorderThickness = new Avalonia.Thickness(1);
            }
            
            // Reset column headers
            foreach (var kvp in _columnHeaders)
            {
                var header = kvp.Value;
                header.Background = Brushes.LightGray;
                header.BorderBrush = Brushes.DarkGray;
            }
            
            // Reset row headers
            foreach (var kvp in _rowHeaders)
            {
                var header = kvp.Value;
                header.Background = Brushes.LightGray;
                header.BorderBrush = Brushes.DarkGray;
            }
            
            // Reset select-all button
            if (_selectAllButton != null)
            {
                _selectAllButton.Background = Brushes.LightGray;
                _selectAllButton.BorderBrush = Brushes.DarkGray;
            }
            
            // Clear only visual selection state
            _selectedCells.Clear();
            _selectedCell = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error clearing selection visuals: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Clear all selections and reset state - Tab 4 feature
    /// </summary>
    private void ClearAllSelections()
    {
        try
        {
            // First clear visuals
            ClearSelectionVisuals();
            
            // Then clear all state
            _selectedColumn = -1;
            _selectedRow = -1;
            _currentSelection = SelectionType.None;
            
            // Clear anchor and multi-selection state
            _anchorCell = null;
            _shiftClickProcessed = false;
            
            UpdateSelectionStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error clearing selections: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Update status message with current selection info - Enhanced Tab 4 feature with range detection
    /// </summary>
    private void UpdateSelectionStatus()
    {
        try
        {
            if (DataContext is TemplateMappingViewModel viewModel)
            {
                string status = _currentSelection switch
                {
                    SelectionType.None => "Click cells to select • Double-click to map fields • ESC to clear",
                    SelectionType.Cell when _selectedCells.Count == 1 => 
                        $"🎯 CELL MODE: {_selectedCells.First()} selected | Ctrl+Click for multi-select | ESC to clear",
                    SelectionType.Cell when _selectedCells.Count > 1 => 
                        DetectRangeOrMultiSelection(),
                    SelectionType.Column => 
                        $"📊 COLUMN MODE: {GetExcelColumnLetter(_selectedColumn)} selected ({_selectedCells.Count} cells) | ESC to clear",
                    SelectionType.Row => 
                        $"📄 ROW MODE: {_selectedRow + 1} selected ({_selectedCells.Count} cells) | ESC to clear",
                    SelectionType.All => 
                        $"📋 ALL MODE: Entire grid selected ({_selectedCells.Count} cells) | ESC to clear",
                    _ => "Click cells to select • Double-click to map fields • ESC to clear"
                };
                
                viewModel.StatusMessage = status;
                
                // Update selected cell reference for status bar
                if (_selectedCells.Count == 1)
                {
                    viewModel.SelectedCellReference = _selectedCells.First();
                }
                else if (_selectedCells.Count > 1)
                {
                    viewModel.SelectedCellReference = $"{_selectedCells.Count} cells";
                }
                else
                {
                    viewModel.SelectedCellReference = "";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating selection status: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Detect if selection is a range (Shift+Click) or multi-selection (Ctrl+Click) - Tab 4 feature
    /// </summary>
    private string DetectRangeOrMultiSelection()
    {
        try
        {
            if (_selectedCells.Count <= 1) return CreateMultiSelectionStatus();
            
            // Check if selection forms a rectangular range
            var coordinates = _selectedCells.Select(cellRef => ParseCellReference(cellRef))
                                           .Where(coord => coord.HasValue)
                                           .Select(coord => coord.Value)
                                           .ToList();
            
            if (coordinates.Count != _selectedCells.Count) return CreateMultiSelectionStatus();
            
            var minRow = coordinates.Min(c => c.row);
            var maxRow = coordinates.Max(c => c.row);
            var minCol = coordinates.Min(c => c.col);
            var maxCol = coordinates.Max(c => c.col);
            
            var expectedCount = (maxRow - minRow + 1) * (maxCol - minCol + 1);
            
            if (coordinates.Count == expectedCount && _anchorCell != null)
            {
                // This is a range selection
                var rangeStart = GetExcelCellReference(minRow, minCol);
                var rangeEnd = GetExcelCellReference(maxRow, maxCol);
                return $"🔗 RANGE MODE: {rangeStart}:{rangeEnd} selected ({_selectedCells.Count} cells) | ESC to clear";
            }
            
            return CreateMultiSelectionStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error detecting range selection: {ex.Message}");
            return CreateMultiSelectionStatus();
        }
    }
    
    /// <summary>
    /// Create enhanced multi-selection status message - Tab 4 feature
    /// </summary>
    private string CreateMultiSelectionStatus()
    {
        try
        {
            if (_selectedCells.Count <= 3)
            {
                var cellList = string.Join(", ", _selectedCells.Take(3));
                return $"🟢 MULTI MODE: {_selectedCells.Count} cells selected: {cellList} | ESC to clear";
            }
            else
            {
                var cellList = string.Join(", ", _selectedCells.Take(3));
                var moreCount = _selectedCells.Count - 3;
                return $"🟢 MULTI MODE: {_selectedCells.Count} cells selected: {cellList} (+{moreCount} more) | ESC to clear";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating multi-selection status: {ex.Message}");
            return $"🟢 MULTI MODE: {_selectedCells.Count} cells selected | ESC to clear";
        }
    }
    
    // ===================================================================================
    // HELPER METHODS - Tab 4 utility functions
    // ===================================================================================
    
    /// <summary>
    /// Parse cell reference to row/column coordinates - Tab 4 helper
    /// </summary>
    private (int row, int col)? ParseCellReference(string cellRef)
    {
        try
        {
            if (string.IsNullOrEmpty(cellRef)) return null;
            
            var colIndex = GetColumnIndexFromCellRef(cellRef);
            var rowIndex = GetRowIndexFromCellRef(cellRef) - 1; // Convert to 0-based
            
            return (rowIndex, colIndex);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Extract column index from cell reference - Tab 4 helper
    /// </summary>
    private int GetColumnIndexFromCellRef(string cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return -1;
        
        var columnPart = "";
        foreach (char c in cellRef)
        {
            if (char.IsLetter(c))
                columnPart += c;
            else
                break;
        }
        
        int result = 0;
        for (int i = 0; i < columnPart.Length; i++)
        {
            result = result * 26 + (columnPart[i] - 'A' + 1);
        }
        return result - 1; // Convert to 0-based index
    }
    
    /// <summary>
    /// Extract row number from cell reference - Tab 4 helper
    /// </summary>
    private int GetRowIndexFromCellRef(string cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return -1;
        
        var numberPart = "";
        foreach (char c in cellRef)
        {
            if (char.IsDigit(c))
                numberPart += c;
        }
        
        return int.TryParse(numberPart, out int result) ? result : -1;
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