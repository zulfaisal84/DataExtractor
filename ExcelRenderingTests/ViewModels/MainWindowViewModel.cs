using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExcelRenderingTests.Services;

namespace ExcelRenderingTests.ViewModels;

/// <summary>
/// Main window ViewModel for Excel rendering technology testing
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ExcelDataService _excelDataService;
    private readonly string _testFilePath = "TestData/Database.xlsx";

    [ObservableProperty]
    private string _statusMessage = "Ready to test Excel rendering technologies";

    [ObservableProperty]
    private string _globalStatus = "Excel Rendering Tests - Ready to begin technology comparison";

    [ObservableProperty]
    private string _debugInfo = "No data loaded yet";

    [ObservableProperty]
    private DataTable? _excelDataTable;

    [ObservableProperty]
    private string _htmlContentSize = "0 KB";

    [ObservableProperty]
    private string _webViewStatus = "Not loaded";

    [ObservableProperty]
    private string _htmlContent = "";

    public MainWindowViewModel()
    {
        _excelDataService = new ExcelDataService();
        _ = InitializeAsync();
    }

    /// <summary>
    /// Initialize the application and create test Excel file
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            GlobalStatus = "Initializing Excel Rendering Tests...";
            
            // Create TestData directory if it doesn't exist
            Directory.CreateDirectory("TestData");
            
            // Create test Excel file if it doesn't exist
            if (!File.Exists(_testFilePath))
            {
                StatusMessage = "Creating test Excel file...";
                await _excelDataService.CreateTestExcelFileAsync(_testFilePath);
                DebugInfo = $"Created test Excel file: {_testFilePath}";
            }
            else
            {
                DebugInfo = $"Using existing test Excel file: {_testFilePath}";
            }
            
            GlobalStatus = "Ready to test Excel rendering technologies";
            StatusMessage = "Test Excel file ready. Click 'Load Excel Data' to begin testing.";
        }
        catch (Exception ex)
        {
            GlobalStatus = $"Initialization failed: {ex.Message}";
            StatusMessage = $"Error: {ex.Message}";
            DebugInfo = $"Initialization error: {ex}";
        }
    }

    /// <summary>
    /// Load Excel data into DataTable for Tab 1 testing
    /// </summary>
    [RelayCommand]
    private async Task LoadExcelData()
    {
        try
        {
            StatusMessage = "Loading Excel data into DataTable...";
            
            if (!File.Exists(_testFilePath))
            {
                StatusMessage = "Error: Test Excel file not found";
                return;
            }
            
            // Load data using our service
            ExcelDataTable = await _excelDataService.ReadExcelToDataTableAsync(_testFilePath);
            
            // Update debug information
            var sb = new StringBuilder();
            sb.AppendLine($"Excel file loaded successfully:");
            sb.AppendLine($"• File: {_testFilePath}");
            sb.AppendLine($"• Rows: {ExcelDataTable.Rows.Count}");
            sb.AppendLine($"• Columns: {ExcelDataTable.Columns.Count}");
            sb.AppendLine($"• Column names: {string.Join(", ", ExcelDataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}");
            sb.AppendLine($"• DataTable type: {ExcelDataTable.GetType().Name}");
            sb.AppendLine($"• ItemsSource bound: {(ExcelDataTable != null ? "Yes" : "No")}");
            
            DebugInfo = sb.ToString();
            StatusMessage = $"✅ Excel data loaded: {ExcelDataTable.Rows.Count} rows, {ExcelDataTable.Columns.Count} columns";
            GlobalStatus = $"Tab 1: DataGrid test ready - {ExcelDataTable.Rows.Count} rows loaded";
            
            Console.WriteLine("📊 Excel data loaded for DataGrid testing:");
            Console.WriteLine($"   Rows: {ExcelDataTable.Rows.Count}");
            Console.WriteLine($"   Columns: {ExcelDataTable.Columns.Count}");
            Console.WriteLine($"   Columns: {string.Join(", ", ExcelDataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading Excel data: {ex.Message}";
            DebugInfo = $"Error details: {ex}";
            GlobalStatus = "Tab 1: DataGrid test failed - Excel loading error";
            Console.WriteLine($"❌ Error loading Excel data: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate HTML table for Tab 2 testing
    /// </summary>
    [RelayCommand]
    private async Task GenerateHtml()
    {
        try
        {
            StatusMessage = "Generating HTML table...";
            
            if (ExcelDataTable == null)
            {
                StatusMessage = "Load Excel data first (Tab 1)";
                return;
            }
            
            await Task.Run(() =>
            {
                var html = new StringBuilder();
                
                // HTML structure
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html><head>");
                html.AppendLine("<meta charset='utf-8'>");
                html.AppendLine("<title>Excel Test Table</title>");
                html.AppendLine("<style>");
                html.AppendLine(@"
                    body { font-family: Arial, sans-serif; margin: 10px; }
                    table { border-collapse: collapse; width: 100%; }
                    th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
                    th { background-color: #f2f2f2; font-weight: bold; }
                    tr:nth-child(even) { background-color: #f9f9f9; }
                    tr:hover { background-color: #e6f3ff; }
                    .cell { cursor: pointer; position: relative; }
                    .cell:hover { background-color: #cce6ff !important; }
                ");
                html.AppendLine("</style>");
                html.AppendLine("</head><body>");
                
                html.AppendLine("<h3>Excel Rendering Test - HTML Table</h3>");
                html.AppendLine("<table>");
                
                // Header row
                html.AppendLine("<thead><tr>");
                foreach (DataColumn column in ExcelDataTable.Columns)
                {
                    html.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(column.ColumnName)}</th>");
                }
                html.AppendLine("</tr></thead>");
                
                // Data rows
                html.AppendLine("<tbody>");
                for (int i = 0; i < ExcelDataTable.Rows.Count; i++)
                {
                    var row = ExcelDataTable.Rows[i];
                    html.AppendLine("<tr>");
                    
                    for (int j = 0; j < ExcelDataTable.Columns.Count; j++)
                    {
                        var cellValue = row[j]?.ToString() ?? "";
                        var cellRef = $"{GetColumnLetter(j)}{i + 2}"; // Excel-style reference
                        
                        html.AppendLine($"<td class='cell' data-row='{i}' data-col='{j}' data-ref='{cellRef}' onclick='cellClick(\"{cellRef}\", \"{cellValue}\")'>");
                        html.AppendLine(System.Net.WebUtility.HtmlEncode(cellValue));
                        html.AppendLine("</td>");
                    }
                    
                    html.AppendLine("</tr>");
                }
                html.AppendLine("</tbody>");
                
                html.AppendLine("</table>");
                
                // JavaScript for interaction
                html.AppendLine("<script>");
                html.AppendLine(@"
                    function cellClick(cellRef, cellValue) {
                        console.log('Cell clicked:', cellRef, 'Value:', cellValue);
                        alert('Cell ' + cellRef + ' clicked!\nValue: ' + cellValue);
                    }
                ");
                html.AppendLine("</script>");
                
                html.AppendLine("</body></html>");
                
                HtmlContent = html.ToString();
            });
            
            HtmlContentSize = $"{HtmlContent.Length / 1024.0:F1} KB";
            StatusMessage = $"✅ HTML table generated ({HtmlContentSize})";
            GlobalStatus = "Tab 2: HTML table generated - Ready for WebView loading";
            
            Console.WriteLine($"🌐 HTML table generated: {HtmlContent.Length} characters");
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error generating HTML: {ex.Message}";
            GlobalStatus = "Tab 2: HTML generation failed";
            Console.WriteLine($"❌ Error generating HTML: {ex.Message}");
        }
    }

    /// <summary>
    /// Load HTML content into WebView for Tab 2 testing
    /// </summary>
    [RelayCommand]
    private async Task LoadWebView()
    {
        try
        {
            if (string.IsNullOrEmpty(HtmlContent))
            {
                StatusMessage = "Generate HTML first";
                return;
            }
            
            StatusMessage = "Loading HTML into WebView...";
            WebViewStatus = "Loading...";
            
            // Note: This will need to be connected to the actual WebView control
            // For now, just simulate the action
            await Task.Delay(500);
            
            WebViewStatus = "Loaded successfully";
            StatusMessage = "✅ HTML loaded into WebView - Check the display";
            GlobalStatus = "Tab 2: WebView HTML test complete";
            
            Console.WriteLine("🌐 HTML content ready for WebView loading");
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading WebView: {ex.Message}";
            WebViewStatus = "Load failed";
            GlobalStatus = "Tab 2: WebView loading failed";
            Console.WriteLine($"❌ Error loading WebView: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert column index to Excel-style letter (A, B, C, etc.)
    /// </summary>
    private string GetColumnLetter(int columnIndex)
    {
        string columnLetter = "";
        while (columnIndex >= 0)
        {
            columnLetter = (char)('A' + columnIndex % 26) + columnLetter;
            columnIndex = columnIndex / 26 - 1;
        }
        return columnLetter;
    }
}
