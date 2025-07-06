using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using OfficeOpenXml;

namespace ExcelRenderingTests.Services;

/// <summary>
/// Service for reading Excel files and converting to various data structures for testing
/// </summary>
public class ExcelDataService
{
    public ExcelDataService()
    {
        // Set EPPlus license context
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// Read Excel file and return DataTable
    /// </summary>
    public async Task<DataTable> ReadExcelToDataTableAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var dataTable = new DataTable();
            
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];
            
            // Add columns
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var columnName = worksheet.Cells[1, col].Value?.ToString() ?? $"Column{col}";
                dataTable.Columns.Add(columnName);
            }
            
            // Add rows
            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                var dataRow = dataTable.NewRow();
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    dataRow[col - 1] = worksheet.Cells[row, col].Value?.ToString() ?? "";
                }
                dataTable.Rows.Add(dataRow);
            }
            
            return dataTable;
        });
    }

    /// <summary>
    /// Read Excel file and return ObservableCollection for MVVM binding
    /// </summary>
    public async Task<ExcelTestData> ReadExcelToObservableCollectionAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var testData = new ExcelTestData();
            
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];
            
            // Get column names
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var columnName = worksheet.Cells[1, col].Value?.ToString() ?? $"Column{col}";
                testData.ColumnNames.Add(columnName);
            }
            
            // Get row data
            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                var rowData = new ExcelRowTestData();
                rowData.RowNumber = row - 1;
                
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                    rowData.CellValues.Add(cellValue);
                }
                
                testData.Rows.Add(rowData);
            }
            
            testData.WorksheetName = worksheet.Name;
            testData.TotalRows = testData.Rows.Count;
            testData.TotalColumns = testData.ColumnNames.Count;
            
            return testData;
        });
    }

    /// <summary>
    /// Create test Excel file if it doesn't exist
    /// </summary>
    public async Task CreateTestExcelFileAsync(string filePath)
    {
        await Task.Run(() =>
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("TestData");
            
            // Headers
            var headers = new[] { "ID", "Name", "Amount", "Date", "Status", "Category", "Notes" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            }
            
            // Sample data
            var sampleData = new object[,]
            {
                { 1, "John Doe", 1250.50, "2025-01-15", "Active", "Utilities", "Electric bill payment" },
                { 2, "Jane Smith", 875.25, "2025-01-16", "Pending", "Telecom", "Internet service" },
                { 3, "ACME Corp", 2340.00, "2025-01-17", "Completed", "Invoice", "Monthly service fee" },
                { 4, "Bob Johnson", 456.75, "2025-01-18", "Active", "Utilities", "Water bill" },
                { 5, "Alice Brown", 1680.30, "2025-01-19", "Pending", "Telecom", "Phone service" },
                { 6, "XYZ Ltd", 3250.00, "2025-01-20", "Completed", "Invoice", "Consulting services" },
                { 7, "Tom Wilson", 725.80, "2025-01-21", "Active", "Utilities", "Gas bill" },
                { 8, "Sarah Davis", 1125.45, "2025-01-22", "Pending", "Telecom", "Cable TV" },
                { 9, "DEF Company", 4500.00, "2025-01-23", "Completed", "Invoice", "Software license" },
                { 10, "Mike Taylor", 890.20, "2025-01-24", "Active", "Utilities", "Electric bill" },
                { 11, "Lisa Garcia", 1450.75, "2025-01-25", "Pending", "Telecom", "Mobile service" },
                { 12, "GHI Industries", 2800.50, "2025-01-26", "Completed", "Invoice", "Equipment rental" }
            };
            
            // Add data
            for (int row = 0; row < sampleData.GetLength(0); row++)
            {
                for (int col = 0; col < sampleData.GetLength(1); col++)
                {
                    worksheet.Cells[row + 2, col + 1].Value = sampleData[row, col];
                }
            }
            
            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();
            
            // Save file
            var fileInfo = new FileInfo(filePath);
            package.SaveAs(fileInfo);
        });
    }
}

/// <summary>
/// Test data structure for Excel content
/// </summary>
public class ExcelTestData
{
    public string WorksheetName { get; set; } = "";
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public ObservableCollection<string> ColumnNames { get; } = new();
    public ObservableCollection<ExcelRowTestData> Rows { get; } = new();
}

/// <summary>
/// Test data structure for Excel row
/// </summary>
public class ExcelRowTestData
{
    public int RowNumber { get; set; }
    public ObservableCollection<string> CellValues { get; } = new();
    
    public string GetCellValue(int columnIndex)
    {
        return columnIndex < CellValues.Count ? CellValues[columnIndex] : "";
    }
}