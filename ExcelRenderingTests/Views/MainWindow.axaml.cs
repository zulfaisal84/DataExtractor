using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ExcelRenderingTests.ViewModels;

namespace ExcelRenderingTests.Views;

public partial class MainWindow : Window
{
    private Border? _selectedCell;
    private readonly Dictionary<string, string> _fieldMappings = new();
    private readonly Dictionary<string, Border> _cellBorders = new();
    private readonly Dictionary<string, Border> _rowHeaders = new();
    private readonly Dictionary<string, Border> _columnHeaders = new();
    private Border? _selectAllButton;
    private bool _isMappingMode = false;
    
    // Selection state
    private SelectionType _currentSelection = SelectionType.None;
    private int _selectedColumn = -1;
    private int _selectedRow = -1;
    private readonly HashSet<string> _selectedCells = new();
    
    // Drag selection state
    private bool _isDragging = false;
    private string? _dragStartCell = null;
    private string? _dragCurrentCell = null;
    
    // Shift+Click range extension state
    private string? _anchorCell = null; // Last selected cell for range extension
    private bool _shiftClickProcessed = false; // PHASE 2A: Prevent double processing
    
    public enum SelectionType
    {
        None,
        Cell,
        Column,
        Row,
        All
    }
    
    public MainWindow()
    {
        InitializeComponent();
        
        // Subscribe to ViewModel property changes for Canvas drawing
        this.DataContextChanged += MainWindow_DataContextChanged;
        
        // PHASE 1A: Add global ESC key handler for clearing selections
        this.KeyDown += MainWindow_KeyDown;
        
        Console.WriteLine("🎯 Tab 4: Excel-like Canvas with advanced selection");
    }

    private void MainWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    /// <summary>
    /// PHASE 1A: Global ESC key handler for clearing selections
    /// </summary>
    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Escape)
            {
                Console.WriteLine($"⌨️ ESC: Clearing {_currentSelection} mode ({_selectedCells.Count} cells)");
                
                // Clear all selections and return to normal state
                ClearAllSelections();
                
                // Update status to show ESC worked
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.CanvasStatus = "⌨️ ESC pressed - All selections cleared";
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

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CanvasStatus) && 
            sender is MainWindowViewModel viewModel)
        {
            // Check if canvas drawing was initiated
            if (viewModel.CanvasStatus.Contains("Canvas drawing initiated") && 
                viewModel.ExcelDataTable != null)
            {
                DrawExcelGridOnCanvas(viewModel.ExcelDataTable);
            }
        }
    }

    private void DrawExcelGridOnCanvas(DataTable dataTable)
    {
        try
        {
            var canvas = this.FindControl<Canvas>("ExcelCanvas");
            if (canvas == null) return;

            // Clear existing content and mappings
            canvas.Children.Clear();
            _cellBorders.Clear();
            _rowHeaders.Clear();
            _columnHeaders.Clear();
            _selectedCells.Clear();

            const double cellWidth = 120;  
            const double cellHeight = 30;  
            const double rowHeaderWidth = 50;  // Width for row numbers
            const double startX = 10;
            const double startY = 10;

            // 1. Draw SELECT ALL button (top-left corner)
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

            // 2. Draw COLUMN HEADERS with Excel-style letters
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
                    Text = $"{columnLetter}\n{dataTable.Columns[col].ColumnName}",
                    FontWeight = FontWeight.Bold,
                    FontSize = 10,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    TextAlignment = Avalonia.Media.TextAlignment.Center
                };

                headerBorder.Child = headerText;
                Canvas.SetLeft(headerBorder, startX + rowHeaderWidth + col * cellWidth);
                Canvas.SetTop(headerBorder, startY);
                
                _columnHeaders[columnLetter] = headerBorder;
                
                // Capture column index for closure to avoid issues
                var capturedColIndex = col;
                var capturedColLetter = columnLetter;
                headerBorder.PointerPressed += (s, e) => {
                    Console.WriteLine($"🔍 DEBUG: Column header clicked - Visual: {capturedColLetter}, Index: {capturedColIndex}");
                    OnColumnHeaderClicked(capturedColIndex);
                };
                ToolTip.SetTip(headerText, $"Click to select entire column {columnLetter}");
                
                canvas.Children.Add(headerBorder);
            }

            // 3. Draw ROW HEADERS (numbered)
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
                
                // Capture row index for closure to avoid issues
                var capturedRowIndex = row;
                var capturedRowNumber = rowNumber;
                rowHeaderBorder.PointerPressed += (s, e) => {
                    Console.WriteLine($"🔍 DEBUG: Row header clicked - Visual: {capturedRowNumber}, Index: {capturedRowIndex}");
                    OnRowHeaderClicked(capturedRowIndex);
                };
                ToolTip.SetTip(rowHeaderText, $"Click to select entire row {rowNumber}");
                
                canvas.Children.Add(rowHeaderBorder);
            }

            // 4. Draw DATA CELLS
            for (int row = 0; row < dataTable.Rows.Count; row++)
            {
                for (int col = 0; col < dataTable.Columns.Count; col++)
                {
                    var cellValue = dataTable.Rows[row][col]?.ToString() ?? "";
                    var cellRef = GetExcelCellReference(row + 1, col);
                    
                    var cellBorder = new Border
                    {
                        Width = cellWidth,
                        Height = cellHeight,
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Avalonia.Thickness(1),
                        Background = Brushes.White,
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Tag = "data_cell"
                    };

                    var cellText = new TextBlock
                    {
                        Text = cellValue.Length > 12 ? cellValue.Substring(0, 12) + "..." : cellValue,
                        FontSize = 11,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    
                    if (cellValue.Length > 12)
                    {
                        ToolTip.SetTip(cellText, cellValue);
                    }

                    cellBorder.Child = cellText;
                    Canvas.SetLeft(cellBorder, startX + rowHeaderWidth + col * cellWidth);
                    Canvas.SetTop(cellBorder, startY + (row + 1) * cellHeight);
                    
                    _cellBorders[cellRef] = cellBorder;
                    
                    // PHASE 2A: Add drag selection support
                    cellBorder.PointerPressed += (s, e) => OnCellPointerPressed(cellRef, cellValue, e);
                    cellBorder.PointerMoved += (s, e) => OnCellPointerMoved(cellRef, e);
                    cellBorder.PointerReleased += (s, e) => OnCellPointerReleased(cellRef, cellValue, e);
                    
                    canvas.Children.Add(cellBorder);
                }
            }

            // Update canvas size
            canvas.Width = startX * 2 + rowHeaderWidth + dataTable.Columns.Count * cellWidth;
            canvas.Height = startY * 2 + (dataTable.Rows.Count + 1) * cellHeight;

            Console.WriteLine($"🎨 Excel grid ready: {dataTable.Rows.Count} rows × {dataTable.Columns.Count} columns");
            
            _isMappingMode = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error drawing Excel Canvas: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle SELECT ALL button click
    /// </summary>
    private void OnSelectAllClicked()
    {
        try
        {
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
            
            // PHASE 1C: Enhanced status feedback
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"📋 ALL MODE: Entire grid selected ({_selectedCells.Count} cells) | ESC to clear";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in select all: {ex.Message}");
        }
    }

    /// <summary>
    /// PHASE 1B: Handle COLUMN HEADER click with smart switching
    /// </summary>
    private void OnColumnHeaderClicked(int columnIndex)
    {
        try
        {
            var columnLetter = GetExcelColumnLetter(columnIndex);
            
            // Smart switching - only clear if not switching between columns
            if (_currentSelection == SelectionType.Column && _selectedColumn == columnIndex)
            {
                // Clicking same column - deselect it (Excel-like behavior)
                Console.WriteLine($"📊 Column {columnLetter} deselected");
                ClearAllSelections();
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.CanvasStatus = $"📊 Column {columnLetter} deselected";
                }
                return;
            }
            
            // Clear selections only if switching from different selection type
            if (_currentSelection != SelectionType.Column)
            {
                ClearAllSelections();
            }
            else
            {
                // Just clear visual state, don't reset all state
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
                
                // Check if this cell is in the selected column
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
            
            // PHASE 1C: Enhanced status feedback
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"📊 COLUMN MODE: {columnLetter} selected ({_selectedCells.Count} cells) | ESC to clear";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in column selection: {ex.Message}");
        }
    }

    /// <summary>
    /// PHASE 1B: Handle ROW HEADER click with smart switching
    /// </summary>
    private void OnRowHeaderClicked(int rowIndex)
    {
        try
        {
            var rowNumber = rowIndex + 1;
            
            // Smart switching - only clear if not switching between rows
            if (_currentSelection == SelectionType.Row && _selectedRow == rowIndex)
            {
                // Clicking same row - deselect it (Excel-like behavior)
                Console.WriteLine($"📄 Row {rowNumber} deselected");
                ClearAllSelections();
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.CanvasStatus = $"📄 Row {rowNumber} deselected";
                }
                return;
            }
            
            // Clear selections only if switching from different selection type
            if (_currentSelection != SelectionType.Row)
            {
                ClearAllSelections();
            }
            else
            {
                // Just clear visual state, don't reset all state
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
                
                // Check if this cell is in the selected row
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
            
            // PHASE 1C: Enhanced status feedback
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"📄 ROW MODE: {rowNumber} selected ({_selectedCells.Count} cells) | ESC to clear";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in row selection: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle individual CELL click (Excel-like behavior: single selection, deselect previous)
    /// </summary>
    private void OnCellClicked(string cellRef, string cellValue, bool isHeader)
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
                
                Console.WriteLine($"🎯 Cell {cellRef} selected");
            }
            
            // Set anchor cell for future Shift+Click operations
            _anchorCell = cellRef;
            
            // PHASE 1C: Enhanced status feedback
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"🎯 CELL MODE: {cellRef} selected ('{cellValue}') | ESC to clear";
            }
            
            // Show field mapping dialog for single cells
            ShowFieldMappingDialog(cellRef, cellValue, isHeader);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling cell click: {ex.Message}");
        }
    }

    /// <summary>
    /// PHASE 2A, 2B & 2C: Handle pointer pressed on cell (drag, Ctrl+Click, Shift+Click)
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
                _shiftClickProcessed = true; // Mark as processed to prevent double handling
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

            // Start potential drag operation (normal click)
            _isDragging = false; // Will become true if mouse moves
            _dragStartCell = cellRef;
            _dragCurrentCell = cellRef;
            
            // For now, treat as single click (will be handled on release if no drag occurred)
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in pointer pressed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle pointer moved on cell (drag selection) - Note: Currently limited by Avalonia event model
    /// </summary>
    private void OnCellPointerMoved(string cellRef, PointerEventArgs e)
    {
        try
        {
            if (!_isMappingMode || _dragStartCell == null) return;
            
            var currentPoint = e.GetCurrentPoint(null);
            bool isLeftPressed = currentPoint.Properties.IsLeftButtonPressed;
            bool isDifferentCell = cellRef != _dragStartCell;
            
            // Check if this is a drag operation (mouse is pressed and moved to different cell)
            if (isLeftPressed && isDifferentCell)
            {
                if (!_isDragging)
                {
                    _isDragging = true;
                    Console.WriteLine($"🖱️ Drag started: {_dragStartCell} to {cellRef}");
                }
                
                _dragCurrentCell = cellRef;
                UpdateDragSelection();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in pointer moved: {ex.Message}");
        }
    }
    
    /// <summary>
    /// PHASE 2A: Handle pointer released on cell (complete selection)
    /// </summary>
    private void OnCellPointerReleased(string cellRef, string cellValue, PointerReleasedEventArgs e)
    {
        try
        {
            if (!_isMappingMode) return;

            // Check if Shift+Click was already processed
            if (_shiftClickProcessed)
            {
                _shiftClickProcessed = false; // Reset flag
                e.Handled = true;
                return;
            }

            if (_isDragging && _dragStartCell != null && _dragCurrentCell != null)
            {
                // Complete drag selection
                Console.WriteLine($"🖱️ Drag complete: {_dragStartCell}:{_dragCurrentCell}");
                CompleteDragSelection();
            }
            else if (_dragStartCell == cellRef)
            {
                // Single cell click (no drag occurred)
                OnCellClicked(cellRef, cellValue, false);
            }
            
            // Reset drag state
            _isDragging = false;
            _dragStartCell = null;
            _dragCurrentCell = null;
            
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in pointer released: {ex.Message}");
        }
    }
    
    /// <summary>
    /// PHASE 2A: Update visual feedback during drag selection
    /// </summary>
    private void UpdateDragSelection()
    {
        try
        {
            if (_dragStartCell == null || _dragCurrentCell == null) return;
            
            // Clear previous selections
            ClearAllSelections();
            _currentSelection = SelectionType.Cell;
            
            // Get start and end coordinates
            var startCoords = ParseCellReference(_dragStartCell);
            var endCoords = ParseCellReference(_dragCurrentCell);
            
            if (startCoords == null || endCoords == null) return;
            
            // Calculate rectangular range
            var minRow = Math.Min(startCoords.Value.row, endCoords.Value.row);
            var maxRow = Math.Max(startCoords.Value.row, endCoords.Value.row);
            var minCol = Math.Min(startCoords.Value.col, endCoords.Value.col);
            var maxCol = Math.Max(startCoords.Value.col, endCoords.Value.col);
            
            // Highlight all cells in the range
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    var rangeCell = GetExcelCellReference(row, col);
                    if (_cellBorders.TryGetValue(rangeCell, out var border))
                    {
                        border.BorderBrush = Brushes.Blue;
                        border.BorderThickness = new Avalonia.Thickness(2);
                        border.Background = Brushes.LightBlue;
                        _selectedCells.Add(rangeCell);
                    }
                }
            }
            
            // Update status
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"🖱️ Selecting range: {_dragStartCell}:{_dragCurrentCell} ({_selectedCells.Count} cells)";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating drag selection: {ex.Message}");
        }
    }
    
    /// <summary>
    /// PHASE 2A: Complete drag selection operation
    /// </summary>
    private void CompleteDragSelection()
    {
        try
        {
            Console.WriteLine($"✅ RANGE SELECTED: {_dragStartCell}:{_dragCurrentCell} ({_selectedCells.Count} cells)");
            
            // Update final status
            if (DataContext is MainWindowViewModel viewModel)
            {
                if (_selectedCells.Count == 1)
                {
                    var firstCell = _selectedCells.FirstOrDefault();
                    viewModel.CanvasStatus = $"🎯 Cell {firstCell} selected";
                }
                else
                {
                    viewModel.CanvasStatus = $"📊 Range {_dragStartCell}:{_dragCurrentCell} selected ({_selectedCells.Count} cells)";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error completing drag selection: {ex.Message}");
        }
    }
    
    /// <summary>
    /// PHASE 2B: Handle Ctrl+Click for non-contiguous selection
    /// </summary>
    private void HandleCtrlClick(string cellRef, string cellValue)
    {
        try
        {
            // Excel behavior: Ctrl+Click toggles individual cells
            if (_selectedCells.Contains(cellRef))
            {
                // Remove cell from selection (toggle off)
                _selectedCells.Remove(cellRef);
                
                if (_cellBorders.TryGetValue(cellRef, out var deselectedBorder))
                {
                    deselectedBorder.BorderBrush = Brushes.LightGray;
                    deselectedBorder.BorderThickness = new Avalonia.Thickness(1);
                    deselectedBorder.Background = Brushes.White;
                }
                
                Console.WriteLine($"🔄 Ctrl+Click: {cellRef} removed ({_selectedCells.Count} cells remain)");
            }
            else
            {
                // Add cell to selection (toggle on)
                _selectedCells.Add(cellRef);
                
                if (_cellBorders.TryGetValue(cellRef, out var selectedBorder))
                {
                    selectedBorder.BorderBrush = Brushes.Green;
                    selectedBorder.BorderThickness = new Avalonia.Thickness(3);
                    selectedBorder.Background = Brushes.LightGreen;
                    _selectedCell = selectedBorder; // Keep reference to last selected
                }
                
                Console.WriteLine($"✅ Ctrl+Click: {cellRef} added ({_selectedCells.Count} cells total)");
                
                // Set anchor for future Shift+Click operations
                _anchorCell = cellRef;
            }
            
            _currentSelection = _selectedCells.Count > 0 ? SelectionType.Cell : SelectionType.None;
            
            // Enhanced status feedback for multi-selection
            if (DataContext is MainWindowViewModel viewModel)
            {
                if (_selectedCells.Count == 0)
                {
                    viewModel.CanvasStatus = "📋 No selection | Click cells, headers, or corner to select";
                }
                else if (_selectedCells.Count == 1)
                {
                    var singleCell = _selectedCells.FirstOrDefault();
                    viewModel.CanvasStatus = $"🎯 CELL MODE: {singleCell} selected | Ctrl+Click for multi-select | ESC to clear";
                }
                else
                {
                    var cellList = string.Join(", ", _selectedCells.Take(3));
                    var moreText = _selectedCells.Count > 3 ? $" (+{_selectedCells.Count - 3} more)" : "";
                    viewModel.CanvasStatus = $"🟢 MULTI MODE: {_selectedCells.Count} cells selected: {cellList}{moreText} | ESC to clear";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in Ctrl+Click handling: {ex.Message}");
        }
    }
    
    /// <summary>
    /// PHASE 2C: Handle Shift+Click for range extension
    /// </summary>
    private void HandleShiftClick(string cellRef, string cellValue)
    {
        try
        {
            if (_anchorCell == null) return;
            
            Console.WriteLine($"🔗 Range selection: {_anchorCell}:{cellRef}");
            
            // Don't call ClearAllSelections as it resets anchor - just clear visual state
            ClearSelectionVisuals();
            _currentSelection = SelectionType.Cell;
            
            // Get coordinates for range calculation
            var anchorCoords = ParseCellReference(_anchorCell);
            var targetCoords = ParseCellReference(cellRef);
            
            if (anchorCoords == null || targetCoords == null) return;
            
            // Calculate rectangular range
            var minRow = Math.Min(anchorCoords.Value.row, targetCoords.Value.row);
            var maxRow = Math.Max(anchorCoords.Value.row, targetCoords.Value.row);
            var minCol = Math.Min(anchorCoords.Value.col, targetCoords.Value.col);
            var maxCol = Math.Max(anchorCoords.Value.col, targetCoords.Value.col);
            
            // Select all cells in the range
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    var rangeCell = GetExcelCellReference(row, col);
                    if (_cellBorders.TryGetValue(rangeCell, out var border))
                    {
                        border.BorderBrush = Brushes.Purple;
                        border.BorderThickness = new Avalonia.Thickness(2);
                        border.Background = Brushes.Lavender;
                        _selectedCells.Add(rangeCell);
                    }
                }
            }
            
            // Update status
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"🔗 RANGE MODE: {_anchorCell}:{cellRef} selected ({_selectedCells.Count} cells) | ESC to clear";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in Shift+Click handling: {ex.Message}");
        }
    }
    
    /// <summary>
    /// PHASE 2A: Parse cell reference to row/column coordinates
    /// </summary>
    private (int row, int col)? ParseCellReference(string cellRef)
    {
        try
        {
            var row = GetRowIndexFromCellRef(cellRef);
            var col = GetColumnIndexFromCellRef(cellRef);
            
            if (row > 0 && col >= 0)
            {
                return (row, col);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// PHASE 1B: Clear only visual state (for switching between same selection types)
    /// </summary>
    private void ClearSelectionVisuals()
    {
        try
        {
            // Reset all cells to normal state
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
            
            // Clear only visual selection state (keep selection type and indexes for switching)
            _selectedCells.Clear();
            _selectedCell = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error clearing selection visuals: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all selections and reset visual state
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
            
            // Clear anchor and drag state
            _anchorCell = null;
            _isDragging = false;
            _dragStartCell = null;
            _dragCurrentCell = null;
            _shiftClickProcessed = false; // PHASE 2A: Reset Shift+Click flag
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error clearing selections: {ex.Message}");
        }
    }

    /// <summary>
    /// Show field mapping dialog (simplified demo version)
    /// </summary>
    private void ShowFieldMappingDialog(string cellRef, string cellValue, bool isHeader)
    {
        try
        {
            var fieldTypes = new[] { "Total Amount", "Invoice Number", "Date", "Company Name", "Account Number", "Custom Field" };
            var random = new Random();
            var selectedField = fieldTypes[random.Next(fieldTypes.Length)];
            
            // Simulate field mapping
            _fieldMappings[cellRef] = selectedField;
            
            // Apply visual indicator
            if (_cellBorders.TryGetValue(cellRef, out var border))
            {
                border.Background = Brushes.LightGreen;
                border.BorderBrush = Brushes.Green;
                border.BorderThickness = new Avalonia.Thickness(2);
                
                // Add field label
                if (border.Child is TextBlock textBlock)
                {
                    ToolTip.SetTip(textBlock, $"Mapped to: {selectedField}\nCell: {cellRef}\nValue: {cellValue}");
                }
            }
            
            Console.WriteLine($"✅ Field mapped: {cellRef} → {selectedField}");
            Console.WriteLine($"📊 Total mappings: {_fieldMappings.Count}");
            
            // Update ViewModel status
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"✅ Field mapped: {cellRef} → {selectedField} (Total: {_fieldMappings.Count})";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in field mapping dialog: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert column index to Excel-style letter (A, B, C, etc.)
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
    /// Convert row/column to Excel-style cell reference (A1, B2, etc.)
    /// </summary>
    private string GetExcelCellReference(int row, int col)
    {
        return $"{GetExcelColumnLetter(col)}{row}";
    }

    /// <summary>
    /// Extract column index from cell reference (e.g., "A1" → 0, "B5" → 1)
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
    /// Extract row number from cell reference (e.g., "A1" → 1, "B5" → 5)
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

    /// <summary>
    /// Toggle mapping mode on/off
    /// </summary>
    private void OnToggleMappingMode(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            _isMappingMode = !_isMappingMode;
            
            if (sender is Button button)
            {
                if (_isMappingMode)
                {
                    button.Content = "🛑 Disable Mapping";
                    button.Background = Brushes.Red;
                    Console.WriteLine("🎯 Mapping mode ENABLED - Click cells to map fields");
                }
                else
                {
                    button.Content = "🎯 Enable Mapping Mode";
                    button.Background = Brushes.Blue;
                    Console.WriteLine("🛑 Mapping mode DISABLED");
                    
                    // Clear selection
                    if (_selectedCell != null)
                    {
                        _selectedCell.BorderBrush = Brushes.LightGray;
                        _selectedCell.BorderThickness = new Avalonia.Thickness(1);
                        _selectedCell = null;
                    }
                }
            }
            
            // Update ViewModel status
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = _isMappingMode ? 
                    "🎯 Mapping mode active - Click cells to map fields" : 
                    "🛑 Mapping mode disabled";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error toggling mapping mode: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all field mappings and selections
    /// </summary>
    private void OnClearMappings(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var clearedCount = _fieldMappings.Count;
            
            // Clear all selections first
            ClearAllSelections();
            
            // Clear field mappings
            _fieldMappings.Clear();
            
            // Clear mapping tooltips from all cells
            foreach (var kvp in _cellBorders)
            {
                if (kvp.Value.Child is TextBlock textBlock)
                {
                    var originalTooltip = textBlock.Text;
                    if (originalTooltip.Length > 12)
                    {
                        ToolTip.SetTip(textBlock, originalTooltip); // Keep original tooltip
                    }
                    else
                    {
                        ToolTip.SetTip(textBlock, null); // Clear mapping tooltip
                    }
                }
            }
            
            Console.WriteLine($"🗑️ Cleared {clearedCount} field mappings and all selections");
            
            // Update ViewModel status
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"🗑️ Cleared {clearedCount} mappings and selections - Ready for new Excel-like operations";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error clearing mappings: {ex.Message}");
        }
    }

    /// <summary>
    /// Show mapping report
    /// </summary>
    private void OnShowMappingReport(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (_fieldMappings.Count == 0)
            {
                Console.WriteLine("📋 No field mappings to report");
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.CanvasStatus = "📋 No field mappings found - Map some fields first";
                }
                return;
            }
            
            Console.WriteLine("📋 FIELD MAPPING REPORT:");
            Console.WriteLine("========================");
            
            foreach (var mapping in _fieldMappings)
            {
                Console.WriteLine($"📍 {mapping.Key} → {mapping.Value}");
            }
            
            Console.WriteLine($"📊 Total mappings: {_fieldMappings.Count}");
            Console.WriteLine("========================");
            
            // Update ViewModel status
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CanvasStatus = $"📋 Mapping report generated - {_fieldMappings.Count} mappings found";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error generating mapping report: {ex.Message}");
        }
    }
}