using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// Service to generate simple ASCII tables for data preview
/// No complex Excel rendering - just clean text tables
/// </summary>
public class SimpleTableGeneratorService
{
    /// <summary>
    /// Generate a simple ASCII table from column mappings and sample data
    /// </summary>
    public string GenerateTable(Dictionary<string, List<string>> columnData, int maxRows = 5)
    {
        if (columnData == null || columnData.Count == 0)
            return "No data to display";

        var sb = new StringBuilder();
        var columns = columnData.Keys.ToList();
        var columnWidths = new Dictionary<string, int>();

        // Calculate column widths
        foreach (var col in columns)
        {
            var maxLength = col.Length;
            if (columnData[col].Any())
            {
                maxLength = Math.Max(maxLength, columnData[col].Max(v => v?.Length ?? 0));
            }
            columnWidths[col] = Math.Min(maxLength + 2, 20); // Cap at 20 chars
        }

        // Build top border
        sb.Append("┌");
        for (int i = 0; i < columns.Count; i++)
        {
            sb.Append(new string('─', columnWidths[columns[i]]));
            if (i < columns.Count - 1)
                sb.Append("┬");
        }
        sb.AppendLine("┐");

        // Build header row
        sb.Append("│");
        foreach (var col in columns)
        {
            var paddedCol = PadCenter(col, columnWidths[col]);
            sb.Append(paddedCol);
            sb.Append("│");
        }
        sb.AppendLine();

        // Build middle border
        sb.Append("├");
        for (int i = 0; i < columns.Count; i++)
        {
            sb.Append(new string('─', columnWidths[columns[i]]));
            if (i < columns.Count - 1)
                sb.Append("┼");
        }
        sb.AppendLine("┤");

        // Build data rows
        var maxDataRows = columnData.Values.Max(v => v.Count);
        var rowsToShow = Math.Min(maxDataRows, maxRows);

        for (int row = 0; row < rowsToShow; row++)
        {
            sb.Append("│");
            foreach (var col in columns)
            {
                var value = row < columnData[col].Count ? columnData[col][row] : "";
                var paddedValue = PadLeft(TruncateString(value, columnWidths[col] - 2), columnWidths[col]);
                sb.Append(paddedValue);
                sb.Append("│");
            }
            sb.AppendLine();
        }

        // Build bottom border
        sb.Append("└");
        for (int i = 0; i < columns.Count; i++)
        {
            sb.Append(new string('─', columnWidths[columns[i]]));
            if (i < columns.Count - 1)
                sb.Append("┴");
        }
        sb.AppendLine("┘");

        // Add row count info if there are more rows
        if (maxDataRows > rowsToShow)
        {
            sb.AppendLine($"\n... and {maxDataRows - rowsToShow} more rows");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate a simple table with column letters and field mappings
    /// </summary>
    public string GenerateExcelPreviewTable(Dictionary<string, string> fieldMappings, Dictionary<string, List<string>> sampleData)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Output Preview:");
        sb.AppendLine();

        // Create column data with Excel-style headers
        var columnData = new Dictionary<string, List<string>>();
        
        foreach (var mapping in fieldMappings.OrderBy(m => m.Key))
        {
            var column = mapping.Key; // e.g., "A", "B", "C"
            var fieldName = mapping.Value; // e.g., "Invoice #", "Amount"
            
            var values = new List<string> { fieldName };
            if (sampleData.ContainsKey(fieldName))
            {
                values.AddRange(sampleData[fieldName]);
            }
            
            columnData[$"Column {column}"] = values;
        }

        return sb.ToString() + GenerateTable(columnData);
    }

    private string PadCenter(string text, int width)
    {
        if (text.Length >= width) return text.Substring(0, width);
        int leftPadding = (width - text.Length) / 2;
        int rightPadding = width - text.Length - leftPadding;
        return new string(' ', leftPadding) + text + new string(' ', rightPadding);
    }

    private string PadLeft(string text, int width)
    {
        if (text.Length >= width) return text.Substring(0, width);
        return " " + text.PadRight(width - 1);
    }

    private string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= maxLength) return value;
        return value.Substring(0, maxLength - 3) + "...";
    }
}