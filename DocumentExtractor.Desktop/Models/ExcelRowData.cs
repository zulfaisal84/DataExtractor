using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DocumentExtractor.Desktop.Models;

/// <summary>
/// Represents a single row of Excel data for Avalonia DataGrid binding.
/// Each row contains a dictionary of column values keyed by column name (A, B, C, etc.).
/// </summary>
public partial class ExcelRowData : ObservableObject
{
    /// <summary>
    /// Dictionary containing cell values keyed by column name.
    /// Example: Cells["A"] = "Invoice #", Cells["B"] = "12345"
    /// </summary>
    public Dictionary<string, string> Cells { get; set; } = new();

    /// <summary>
    /// Row index in the original Excel file (1-based).
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Indexer to access cell values by column name.
    /// Returns empty string if column doesn't exist.
    /// </summary>
    /// <param name="columnName">Column name (A, B, C, etc.)</param>
    /// <returns>Cell value or empty string</returns>
    public string this[string columnName] 
    {
        get => Cells.TryGetValue(columnName, out var value) ? value : "";
        set => Cells[columnName] = value ?? "";
    }

    /// <summary>
    /// Get cell value by column index (0-based).
    /// </summary>
    /// <param name="columnIndex">0-based column index</param>
    /// <param name="columnNames">List of column names</param>
    /// <returns>Cell value or empty string</returns>
    public string GetCellByIndex(int columnIndex, List<string> columnNames)
    {
        if (columnIndex >= 0 && columnIndex < columnNames.Count)
        {
            return this[columnNames[columnIndex]];
        }
        return "";
    }

    /// <summary>
    /// Set cell value by column index (0-based).
    /// </summary>
    /// <param name="columnIndex">0-based column index</param>
    /// <param name="value">Cell value</param>
    /// <param name="columnNames">List of column names</param>
    public void SetCellByIndex(int columnIndex, string value, List<string> columnNames)
    {
        if (columnIndex >= 0 && columnIndex < columnNames.Count)
        {
            this[columnNames[columnIndex]] = value;
        }
    }

    /// <summary>
    /// Get all cell values as a list in column order.
    /// </summary>
    /// <param name="columnNames">Ordered list of column names</param>
    /// <returns>List of cell values in column order</returns>
    public List<string> GetCellValues(List<string> columnNames)
    {
        var values = new List<string>();
        foreach (var columnName in columnNames)
        {
            values.Add(this[columnName]);
        }
        return values;
    }

    /// <summary>
    /// Check if row has any non-empty cells.
    /// </summary>
    public bool HasData => Cells.Values.Any(v => !string.IsNullOrWhiteSpace(v));
}