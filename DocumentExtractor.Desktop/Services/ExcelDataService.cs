using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
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
        var endCol = worksheet.Dimension?.End.Column ?? 1;

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