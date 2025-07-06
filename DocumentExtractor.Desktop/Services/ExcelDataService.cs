using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentExtractor.Desktop.Models;
using OfficeOpenXml;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// Service for reading Excel files and converting them to DataTable format for DataGrid display.
/// Provides Excel-like grid functionality with proper cell references (A1, B2, etc.).
/// </summary>
public class ExcelDataService
{
    /// <summary>
    /// Read Excel file and convert to DataTable for DataGrid binding.
    /// Includes proper column headers (A, B, C...) and row indexing.
    /// </summary>
    /// <param name="filePath">Path to the Excel file</param>
    /// <param name="worksheetIndex">Worksheet index (0-based, default is first sheet)</param>
    /// <returns>DataTable with Excel data and proper headers</returns>
    public async Task<ExcelGridData> ReadExcelFileAsync(string filePath, int worksheetIndex = 0)
    {
        try
        {
            // Set EPPlus license context (required for EPPlus 5.0+)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            Console.WriteLine($"📊 Reading Excel file: {Path.GetFileName(filePath)}");

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[worksheetIndex];
            
            if (worksheet == null)
            {
                throw new InvalidOperationException($"Worksheet {worksheetIndex} not found in Excel file");
            }

            var result = await Task.Run(() => ConvertWorksheetToDataTable(worksheet));
            
            Console.WriteLine($"✅ Excel data loaded: {result.DataTable.Rows.Count} rows, {result.DataTable.Columns.Count} columns");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error reading Excel file: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Convert EPPlus worksheet to DataTable with Excel-style headers and ObservableCollection for Avalonia.
    /// </summary>
    private ExcelGridData ConvertWorksheetToDataTable(ExcelWorksheet worksheet)
    {
        var dataTable = new DataTable();
        var cellData = new Dictionary<string, ExcelCellInfo>();
        var excelRows = new ObservableCollection<ExcelRowData>();
        var columnNames = new List<string>();

        // Get the used range of the worksheet
        var startRow = worksheet.Dimension?.Start.Row ?? 1;
        var endRow = worksheet.Dimension?.End.Row ?? 1;
        var startCol = worksheet.Dimension?.Start.Column ?? 1;
        
        // Use Excel's actual dimension or force a wider range to show all possible columns
        var endCol = Math.Max(worksheet.Dimension?.End.Column ?? 50, 25); // Force minimum 25 columns (up to Y)
        
        Console.WriteLine($"📊 Loading complete Excel range: Rows {startRow}-{endRow}, Columns {startCol}-{endCol} (Sheet: {worksheet.Name})");

        // Create columns with Excel-style headers (A, B, C, D...)
        for (int col = startCol; col <= endCol; col++)
        {
            var columnName = GetExcelColumnName(col);
            dataTable.Columns.Add(columnName, typeof(string));
            columnNames.Add(columnName);
        }

        // Add rows with data
        for (int row = startRow; row <= endRow; row++)
        {
            var dataRow = dataTable.NewRow();
            var excelRowData = new ExcelRowData { RowIndex = row };
            
            for (int col = startCol; col <= endCol; col++)
            {
                var cell = worksheet.Cells[row, col];
                var cellValue = cell.Value?.ToString() ?? string.Empty;
                var columnName = GetExcelColumnName(col);
                var cellReference = GetExcelCellReference(row, col);
                
                // Debug: Log non-empty cells in columns H, I, J
                if (!string.IsNullOrEmpty(cellValue) && (col == 8 || col == 9 || col == 10))
                {
                    Console.WriteLine($"🔍 Found data in {cellReference}: '{cellValue}'");
                }
                
                // Add to DataTable (legacy support)
                dataRow[columnName] = cellValue;
                
                // Add to ExcelRowData (Avalonia support)
                excelRowData[columnName] = cellValue;
                
                // Store cell metadata for mapping purposes
                cellData[cellReference] = new ExcelCellInfo
                {
                    CellReference = cellReference,
                    Value = cellValue,
                    Row = row,
                    Column = col,
                    DataGridRow = row - startRow,
                    DataGridColumn = col - startCol
                };
            }
            
            dataTable.Rows.Add(dataRow);
            excelRows.Add(excelRowData);
        }

        return new ExcelGridData
        {
            DataTable = dataTable,
            ExcelRows = excelRows,
            ColumnNames = columnNames,
            CellData = cellData,
            StartRow = startRow,
            StartColumn = startCol,
            WorksheetName = worksheet.Name
        };
    }


    /// <summary>
    /// Convert column number to Excel column name (1=A, 2=B, 26=Z, 27=AA, etc.).
    /// </summary>
    public static string GetExcelColumnName(int columnNumber)
    {
        string columnName = "";
        
        while (columnNumber > 0)
        {
            columnNumber--;
            columnName = (char)('A' + (columnNumber % 26)) + columnName;
            columnNumber /= 26;
        }
        
        return columnName;
    }

    /// <summary>
    /// Get Excel cell reference from row and column numbers (e.g., row 1, col 1 = "A1").
    /// </summary>
    public static string GetExcelCellReference(int row, int column)
    {
        return GetExcelColumnName(column) + row.ToString();
    }

    /// <summary>
    /// Convert DataGrid position to Excel cell reference.
    /// </summary>
    public static string GetCellReferenceFromGridPosition(int gridRow, int gridColumn, int startRow, int startColumn)
    {
        var excelRow = gridRow + startRow;
        var excelColumn = gridColumn + startColumn;
        return GetExcelCellReference(excelRow, excelColumn);
    }

    /// <summary>
    /// Parse Excel cell reference to row and column numbers (e.g., "B5" = row 5, col 2).
    /// </summary>
    public static (int Row, int Column) ParseCellReference(string cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            throw new ArgumentException("Cell reference cannot be empty");

        var columnPart = "";
        var rowPart = "";
        
        foreach (char c in cellReference.ToUpperInvariant())
        {
            if (char.IsLetter(c))
                columnPart += c;
            else if (char.IsDigit(c))
                rowPart += c;
        }

        if (string.IsNullOrEmpty(columnPart) || string.IsNullOrEmpty(rowPart))
            throw new ArgumentException($"Invalid cell reference: {cellReference}");

        var column = 0;
        foreach (char c in columnPart)
        {
            column = column * 26 + (c - 'A' + 1);
        }

        var row = int.Parse(rowPart);
        
        return (row, column);
    }

    /// <summary>
    /// Generate HTML table from Excel grid data with Excel-like styling and click-to-teach functionality.
    /// Uses proven approach from test application with enhanced styling for professional appearance.
    /// </summary>
    /// <param name="excelData">Excel grid data to convert to HTML</param>
    /// <param name="enableClickToTeach">Enable click-to-teach JavaScript functionality</param>
    /// <returns>Complete HTML document with embedded CSS and JavaScript</returns>
    public static string GenerateHtmlTable(ExcelGridData excelData, bool enableClickToTeach = true)
    {
        var html = new StringBuilder();
        
        // HTML structure with professional Excel-like styling
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>Excel Template Preview</title>");
        html.AppendLine("<style>");
        html.AppendLine(@"
            body { 
                font-family: 'Segoe UI', Arial, sans-serif; 
                margin: 0; 
                padding: 10px; 
                background-color: #f8f9fa;
            }
            .container {
                background-color: white;
                border-radius: 8px;
                box-shadow: 0 2px 8px rgba(0,0,0,0.1);
                padding: 15px;
                overflow-x: auto;
            }
            .header {
                margin-bottom: 15px;
                padding-bottom: 10px;
                border-bottom: 2px solid #2C3E50;
            }
            .title {
                color: #2C3E50;
                font-size: 18px;
                font-weight: 600;
                margin: 0;
            }
            .subtitle {
                color: #7f8c8d;
                font-size: 12px;
                margin: 5px 0 0 0;
            }
            table { 
                border-collapse: collapse; 
                width: 100%; 
                background-color: white;
                font-size: 13px;
            }
            th, td { 
                border: 1px solid #d1d5db; 
                padding: 8px 12px; 
                text-align: left;
                vertical-align: middle;
            }
            th { 
                background-color: #f8f9fa; 
                font-weight: 600;
                color: #374151;
                position: sticky;
                top: 0;
                z-index: 10;
            }
            tr:nth-child(even) { 
                background-color: #f9fafb; 
            }
            tr:hover { 
                background-color: #e6f3ff; 
            }
            .cell { 
                cursor: pointer; 
                position: relative;
                transition: background-color 0.2s ease;
            }
            .cell:hover { 
                background-color: #cce6ff !important; 
                border-color: #3b82f6 !important;
            }
            .cell.selected {
                background-color: #dbeafe !important;
                border: 2px solid #3b82f6 !important;
                box-shadow: inset 0 0 0 1px #3b82f6;
            }
            .status-bar {
                margin-top: 10px;
                padding: 8px 12px;
                background-color: #f3f4f6;
                border-radius: 4px;
                font-size: 12px;
                color: #6b7280;
                display: flex;
                justify-content: space-between;
                align-items: center;
            }
            .cell-ref {
                font-weight: 600;
                color: #2563eb;
            }
        ");
        html.AppendLine("</style>");
        html.AppendLine("</head><body>");
        
        // Container and header
        html.AppendLine("<div class='container'>");
        html.AppendLine("<div class='header'>");
        html.AppendLine("<h3 class='title'>📊 Excel Template Preview</h3>");
        html.AppendLine($"<p class='subtitle'>{excelData.RowCount} rows × {excelData.ColumnCount} columns • Worksheet: {excelData.WorksheetName}</p>");
        html.AppendLine("</div>");
        
        // Table
        html.AppendLine("<table>");
        
        // Header row with Excel column names
        html.AppendLine("<thead><tr>");
        foreach (var columnName in excelData.ColumnNames)
        {
            html.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(columnName)}</th>");
        }
        html.AppendLine("</tr></thead>");
        
        // Data rows
        html.AppendLine("<tbody>");
        for (int i = 0; i < excelData.ExcelRows.Count; i++)
        {
            var excelRow = excelData.ExcelRows[i];
            html.AppendLine("<tr>");
            
            for (int j = 0; j < excelData.ColumnNames.Count; j++)
            {
                var columnName = excelData.ColumnNames[j];
                var cellValue = excelRow[columnName] ?? "";
                var cellRef = GetExcelCellReference(i + excelData.StartRow, j + excelData.StartColumn);
                
                var clickHandler = enableClickToTeach ? 
                    $"onclick='cellClick(\"{cellRef}\", \"{System.Net.WebUtility.HtmlEncode(cellValue)}\", this)'" : "";
                
                html.AppendLine($"<td class='cell' data-row='{i}' data-col='{j}' data-ref='{cellRef}' {clickHandler}>");
                html.AppendLine(System.Net.WebUtility.HtmlEncode(cellValue));
                html.AppendLine("</td>");
            }
            
            html.AppendLine("</tr>");
        }
        html.AppendLine("</tbody>");
        html.AppendLine("</table>");
        
        // Status bar
        html.AppendLine("<div class='status-bar'>");
        html.AppendLine("<span>Click any cell to map fields</span>");
        html.AppendLine("<span id='selected-cell'>No cell selected</span>");
        html.AppendLine("</div>");
        
        html.AppendLine("</div>"); // Close container
        
        // JavaScript for click-to-teach functionality
        if (enableClickToTeach)
        {
            html.AppendLine("<script>");
            html.AppendLine("let selectedCell = null;");
            html.AppendLine("");
            html.AppendLine("function cellClick(cellRef, cellValue, element) {");
            html.AppendLine("    console.log('Cell clicked:', cellRef, 'Value:', cellValue);");
            html.AppendLine("    ");
            html.AppendLine("    // Clear previous selection");
            html.AppendLine("    if (selectedCell) {");
            html.AppendLine("        selectedCell.classList.remove('selected');");
            html.AppendLine("    }");
            html.AppendLine("    ");
            html.AppendLine("    // Select new cell");
            html.AppendLine("    element.classList.add('selected');");
            html.AppendLine("    selectedCell = element;");
            html.AppendLine("    ");
            html.AppendLine("    // Update status bar");
            html.AppendLine("    var statusEl = document.getElementById('selected-cell');");
            html.AppendLine("    statusEl.innerHTML = '<span class=\"cell-ref\">' + cellRef + '</span>: ' + cellValue;");
            html.AppendLine("    ");
            html.AppendLine("    // Notify parent application (if WebView integration exists)");
            html.AppendLine("    if (window.chrome && window.chrome.webview) {");
            html.AppendLine("        // Microsoft Edge WebView2");
            html.AppendLine("        window.chrome.webview.postMessage({");
            html.AppendLine("            type: 'cellClick',");
            html.AppendLine("            cellRef: cellRef,");
            html.AppendLine("            cellValue: cellValue,");
            html.AppendLine("            row: element.dataset.row,");
            html.AppendLine("            col: element.dataset.col");
            html.AppendLine("        });");
            html.AppendLine("    } else if (window.webkit && window.webkit.messageHandlers) {");
            html.AppendLine("        // Safari WebView");
            html.AppendLine("        window.webkit.messageHandlers.cellClick.postMessage({");
            html.AppendLine("            cellRef: cellRef,");
            html.AppendLine("            cellValue: cellValue,");
            html.AppendLine("            row: element.dataset.row,");
            html.AppendLine("            col: element.dataset.col");
            html.AppendLine("        });");
            html.AppendLine("    }");
            html.AppendLine("}");
            html.AppendLine("");
            html.AppendLine("// Initialize");
            html.AppendLine("document.addEventListener('DOMContentLoaded', function() {");
            html.AppendLine("    console.log('Excel HTML template loaded successfully');");
            html.AppendLine("});");
            html.AppendLine("</script>");
        }
        
        html.AppendLine("</body></html>");
        
        return html.ToString();
    }
}

/// <summary>
/// Contains Excel grid data with ObservableCollection for Avalonia DataGrid binding.
/// </summary>
public class ExcelGridData
{
    /// <summary>
    /// DataTable containing Excel data (legacy support).
    /// </summary>
    public DataTable DataTable { get; set; } = new();

    /// <summary>
    /// ObservableCollection of Excel rows for Avalonia DataGrid binding.
    /// </summary>
    public ObservableCollection<ExcelRowData> ExcelRows { get; set; } = new();

    /// <summary>
    /// List of column names in order (A, B, C, D...).
    /// </summary>
    public List<string> ColumnNames { get; set; } = new();

    /// <summary>
    /// Dictionary mapping cell references (A1, B2) to cell information.
    /// </summary>
    public Dictionary<string, ExcelCellInfo> CellData { get; set; } = new();

    /// <summary>
    /// Starting row number from Excel (usually 1).
    /// </summary>
    public int StartRow { get; set; }

    /// <summary>
    /// Starting column number from Excel (usually 1).
    /// </summary>
    public int StartColumn { get; set; }

    /// <summary>
    /// Name of the Excel worksheet.
    /// </summary>
    public string WorksheetName { get; set; } = string.Empty;

    /// <summary>
    /// Number of rows with data.
    /// </summary>
    public int RowCount => ExcelRows.Count;

    /// <summary>
    /// Number of columns with data.
    /// </summary>
    public int ColumnCount => ColumnNames.Count;
}

/// <summary>
/// Information about a specific Excel cell.
/// </summary>
public class ExcelCellInfo
{
    /// <summary>
    /// Excel cell reference (A1, B2, etc.).
    /// </summary>
    public string CellReference { get; set; } = string.Empty;

    /// <summary>
    /// Cell value as string.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Excel row number (1-based).
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// Excel column number (1-based).
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// DataGrid row index (0-based).
    /// </summary>
    public int DataGridRow { get; set; }

    /// <summary>
    /// DataGrid column index (0-based).
    /// </summary>
    public int DataGridColumn { get; set; }
}