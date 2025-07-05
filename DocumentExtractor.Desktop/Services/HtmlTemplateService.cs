using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentExtractor.Core.Models;
using DocumentExtractor.Desktop.Models;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// Service for generating HTML previews of Excel templates with interactive cell mapping.
/// Provides a reliable alternative to DataGrid for template visualization.
/// </summary>
public class HtmlTemplateService
{
    /// <summary>
    /// Generate HTML table from Excel data with click-to-teach functionality
    /// </summary>
    public string GenerateHtmlPreview(ExcelGridData excelData, List<TemplateFieldMapping> mappings)
    {
        if (excelData?.ExcelRows == null || !excelData.ExcelRows.Any())
        {
            return GenerateEmptyStateHtml();
        }

        var html = new StringBuilder();
        
        // HTML structure with CSS and JavaScript
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>Template Preview</title>");
        html.AppendLine("<style>");
        html.AppendLine(GetCssStyles());
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // Excel table
        html.AppendLine("<div class='excel-container'>");
        html.AppendLine("<table class='excel-table' id='excelTable'>");
        
        // Generate header row
        html.AppendLine("<thead>");
        html.AppendLine("<tr>");
        html.AppendLine("<th class='row-header'></th>"); // Empty corner cell
        
        foreach (var column in excelData.ColumnNames)
        {
            html.AppendLine($"<th class='column-header'>{column}</th>");
        }
        
        html.AppendLine("</tr>");
        html.AppendLine("</thead>");
        
        // Generate data rows
        html.AppendLine("<tbody>");
        
        for (int rowIndex = 0; rowIndex < excelData.ExcelRows.Count; rowIndex++)
        {
            var row = excelData.ExcelRows[rowIndex];
            html.AppendLine("<tr>");
            
            // Row number header
            html.AppendLine($"<td class='row-header'>{rowIndex + 1}</td>");
            
            // Data cells
            for (int colIndex = 0; colIndex < excelData.ColumnNames.Count; colIndex++)
            {
                var columnName = excelData.ColumnNames[colIndex];
                var cellValue = row.GetCellByIndex(colIndex, excelData.ColumnNames);
                var cellReference = $"{columnName}{rowIndex + 1}";
                
                // Check if this cell is mapped
                var mapping = mappings.FirstOrDefault(m => m.TargetLocation == cellReference);
                var cssClass = mapping != null ? "cell mapped-cell" : "cell";
                var tooltip = mapping != null ? $"Mapped to: {mapping.FieldName}" : "Click to map this cell";
                
                html.AppendLine($"<td class='{cssClass}' " +
                              $"data-row='{rowIndex}' " +
                              $"data-col='{colIndex}' " +
                              $"data-ref='{cellReference}' " +
                              $"title='{tooltip}' " +
                              $"onclick='handleCellClick({rowIndex}, {colIndex}, \"{cellReference}\", \"{cellValue}\")'>");
                
                html.AppendLine($"<span class='cell-content'>{System.Net.WebUtility.HtmlEncode(cellValue)}</span>");
                
                // Show field mapping indicator
                if (mapping != null)
                {
                    html.AppendLine($"<div class='mapping-indicator'>{mapping.FieldName}</div>");
                }
                
                html.AppendLine("</td>");
            }
            
            html.AppendLine("</tr>");
        }
        
        html.AppendLine("</tbody>");
        html.AppendLine("</table>");
        html.AppendLine("</div>");
        
        // Status area
        html.AppendLine("<div class='status-area' id='statusArea'>");
        html.AppendLine("<div class='mapping-status'>Ready for field mapping</div>");
        html.AppendLine("</div>");
        
        // JavaScript for interaction
        html.AppendLine("<script>");
        html.AppendLine(GetJavaScript());
        html.AppendLine("</script>");
        
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }

    /// <summary>
    /// Generate empty state HTML when no template is loaded
    /// </summary>
    private string GenerateEmptyStateHtml()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Template Preview</title>
    <style>
        body { 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif; 
            margin: 0; padding: 40px; background: #f8f9fa; 
        }
        .empty-state { 
            text-align: center; color: #6c757d; 
        }
        .empty-icon { 
            font-size: 48px; margin-bottom: 16px; 
        }
        .empty-title { 
            font-size: 18px; font-weight: 600; margin-bottom: 8px; 
        }
        .empty-subtitle { 
            font-size: 14px; 
        }
    </style>
</head>
<body>
    <div class='empty-state'>
        <div class='empty-icon'>📂</div>
        <div class='empty-title'>No Template Loaded</div>
        <div class='empty-subtitle'>Click 'Load Template' to begin visual field mapping</div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// CSS styles for Excel table and mapping interface
    /// </summary>
    private string GetCssStyles()
    {
        return @"
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif;
    margin: 0;
    padding: 12px;
    background: #ffffff;
    font-size: 13px;
}

.excel-container {
    overflow: auto;
    border: 1px solid #e8e8e8;
    border-radius: 8px;
    background: white;
}

.excel-table {
    border-collapse: collapse;
    width: 100%;
    background: white;
}

.column-header, .row-header {
    background: #f8f9fa;
    border: 1px solid #e8e8e8;
    padding: 8px 12px;
    font-weight: 600;
    font-size: 12px;
    color: #2c3e50;
    text-align: center;
    min-width: 80px;
}

.row-header {
    background: #f1f3f4;
    width: 40px;
    min-width: 40px;
}

.cell {
    border: 1px solid #e8e8e8;
    padding: 6px 8px;
    position: relative;
    cursor: pointer;
    transition: all 0.2s ease;
    min-width: 80px;
    max-width: 200px;
    overflow: hidden;
}

.cell:hover {
    background: #e3f2fd;
    border-color: #3498db;
}

.cell.mapped-cell {
    background: #d5edda;
    border-color: #27ae60;
}

.cell.mapped-cell:hover {
    background: #c3e6cb;
}

.cell-content {
    display: block;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.mapping-indicator {
    position: absolute;
    top: 2px;
    right: 2px;
    background: #27ae60;
    color: white;
    font-size: 9px;
    padding: 1px 4px;
    border-radius: 2px;
    font-weight: 600;
    max-width: 60px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.status-area {
    margin-top: 12px;
    padding: 10px;
    background: #f8f9fa;
    border-radius: 6px;
    border: 1px solid #e8e8e8;
}

.mapping-status {
    font-size: 12px;
    color: #6c757d;
    text-align: center;
}

/* Mapping mode styles */
.mapping-mode .cell {
    cursor: crosshair;
}

.mapping-mode .cell:hover {
    background: #fff3cd;
    border-color: #ffc107;
    transform: scale(1.02);
}

.cell.selected {
    background: #cce5ff;
    border-color: #0066cc;
    border-width: 2px;
}";
    }

    /// <summary>
    /// JavaScript for cell interaction and C# communication
    /// </summary>
    private string GetJavaScript()
    {
        return @"
let isMappingMode = false;
let selectedCell = null;

// Enable/disable mapping mode
function setMappingMode(enabled) {
    isMappingMode = enabled;
    const table = document.getElementById('excelTable');
    if (enabled) {
        table.classList.add('mapping-mode');
        updateStatus('🎯 MAPPING MODE - Click any cell to map a field');
    } else {
        table.classList.remove('mapping-mode');
        updateStatus('Ready for field mapping');
    }
}

// Handle cell click
function handleCellClick(row, col, cellRef, cellValue) {
    if (!isMappingMode) {
        return;
    }
    
    // Remove previous selection
    if (selectedCell) {
        selectedCell.classList.remove('selected');
    }
    
    // Select current cell
    const cell = document.querySelector(`[data-ref='${cellRef}']`);
    if (cell) {
        cell.classList.add('selected');
        selectedCell = cell;
    }
    
    updateStatus(`Selected cell ${cellRef}: '${cellValue}'`);
    
    // Call C# method via bridge
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            action: 'cellClicked',
            row: row,
            col: col,
            cellRef: cellRef,
            cellValue: cellValue
        });
    }
}

// Update status message
function updateStatus(message) {
    const statusArea = document.getElementById('statusArea');
    if (statusArea) {
        statusArea.querySelector('.mapping-status').textContent = message;
    }
}

// Update cell mapping indicator
function updateCellMapping(cellRef, fieldName) {
    const cell = document.querySelector(`[data-ref='${cellRef}']`);
    if (cell) {
        cell.classList.add('mapped-cell');
        cell.title = `Mapped to: ${fieldName}`;
        
        // Add or update mapping indicator
        let indicator = cell.querySelector('.mapping-indicator');
        if (!indicator) {
            indicator = document.createElement('div');
            indicator.className = 'mapping-indicator';
            cell.appendChild(indicator);
        }
        indicator.textContent = fieldName;
    }
}

// Clear cell mapping
function clearCellMapping(cellRef) {
    const cell = document.querySelector(`[data-ref='${cellRef}']`);
    if (cell) {
        cell.classList.remove('mapped-cell');
        cell.title = 'Click to map this cell';
        
        const indicator = cell.querySelector('.mapping-indicator');
        if (indicator) {
            indicator.remove();
        }
    }
}

// Initialize
document.addEventListener('DOMContentLoaded', function() {
    updateStatus('Template loaded - Click ""Start Mapping"" to begin');
});";
    }
}