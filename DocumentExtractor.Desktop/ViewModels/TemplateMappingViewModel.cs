using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentExtractor.Core.Models;
using DocumentExtractor.Data.Context;
using DocumentExtractor.Desktop.Models;
using DocumentExtractor.Desktop.Services;
using Microsoft.EntityFrameworkCore;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace DocumentExtractor.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Visual Template Mapping interface.
/// Handles template preview, click-to-teach field mapping, and visual position overlay.
/// </summary>
public partial class TemplateMappingViewModel : ViewModelBase
{
    private readonly DocumentExtractionContext _context;
    private readonly ExcelDataService _excelDataService;
    private readonly HtmlTemplateService _htmlTemplateService;

    [ObservableProperty]
    private string _statusMessage = "Ready for template mapping";

    [ObservableProperty]
    private bool _isProcessing = false;

    [ObservableProperty]
    private Template? _currentTemplate;

    [ObservableProperty]
    private string? _templatePreviewPath;

    [ObservableProperty]
    private bool _isTemplateLoaded = false;

    [ObservableProperty]
    private string _mappingInstructions = "Load a template to begin click-to-teach field mapping";

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private bool _isMappingMode = true; // Always in mapping mode with simplified workflow

    [ObservableProperty]
    private string _currentFieldName = "";

    [ObservableProperty]
    private string _currentFieldDescription = "";

    [ObservableProperty]
    private DataTable? _excelData;

    [ObservableProperty]
    private ExcelGridData? _excelGridData;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExcelData))]
    private ObservableCollection<ExcelRowData> _excelRows = new();

    [ObservableProperty]
    private List<string> _excelColumns = new();

    [ObservableProperty]
    private string _selectedCellReference = "";

    [ObservableProperty]
    private int _selectedRow = -1;

    [ObservableProperty]
    private int _selectedColumn = -1;

    [ObservableProperty]
    private string _htmlPreviewContent = "";

    [ObservableProperty]
    private string _htmlContentStatus = "No Excel data loaded";

    [ObservableProperty]
    private bool _isWebViewReady = false;

    [ObservableProperty]
    private string _canvasStatus = "Canvas ready for Excel rendering";

    /// <summary>
    /// Whether Excel data is available for display
    /// </summary>
    public bool HasExcelData => ExcelRows != null && ExcelRows.Count > 0;

    /// <summary>
    /// Collection of field mappings for the current template
    /// </summary>
    public ObservableCollection<TemplateFieldMapping> FieldMappings { get; } = new();

    /// <summary>
    /// Collection of available field types for mapping
    /// </summary>
    public ObservableCollection<string> AvailableFieldTypes { get; } = new()
    {
        "Total Amount",
        "Invoice Number", 
        "Date",
        "Company Name",
        "Account Number",
        "Due Date",
        "Tax Amount",
        "Subtotal",
        "Custom Field"
    };

    public TemplateMappingViewModel(DocumentExtractionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _excelDataService = new ExcelDataService();
        _htmlTemplateService = new HtmlTemplateService();
        Console.WriteLine("🗺️ TemplateMappingViewModel initialized with Excel data service");
    }

    /// <summary>
    /// Load a template for visual mapping
    /// </summary>
    [RelayCommand]
    private async Task LoadTemplate()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Opening template selection...";

            // Always open file picker instead of auto-loading existing template
            var files = await OpenFilePickerAsync("Select Template for Visual Mapping", new[] {
                "Excel files (*.xlsx;*.xls)",
                "PDF files (*.pdf)",
                "Word files (*.docx;*.doc)",
                "Image files (*.png;*.jpg;*.jpeg)",
                "All files (*.*)"
            });

            if (files != null && files.Count > 0)
            {
                var filePath = files[0];
                await ProcessSelectedTemplate(filePath);
            }
            else
            {
                StatusMessage = "Template selection cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading template: {ex.Message}";
            Console.WriteLine($"❌ Error in LoadTemplate: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Process the selected template file
    /// </summary>
    private async Task ProcessSelectedTemplate(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            StatusMessage = $"Processing template: {fileName}";

            // Check if template already exists in database
            var existingTemplate = await _context.Templates
                .FirstOrDefaultAsync(t => t.FileName == fileName);

            if (existingTemplate != null)
            {
                CurrentTemplate = existingTemplate;
                await LoadExistingMappings();
                StatusMessage = $"Loaded existing template: {fileName}";
            }
            else
            {
                // Create new template entry
                var newTemplate = new Template
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    Description = $"Template for visual field mapping: {fileName}",
                    Category = DetermineTemplateCategory(extension),
                    FilePath = filePath,
                    FileName = fileName,
                    FileSizeBytes = new FileInfo(filePath).Length,
                    CreatedDate = DateTime.Now,
                    LastUsedDate = DateTime.Now,
                    UsageCount = 0,
                    IsActive = true
                };

                _context.Templates.Add(newTemplate);
                await _context.SaveChangesAsync();

                CurrentTemplate = newTemplate;
                StatusMessage = $"New template created: {fileName}";
            }

            // Set up template preview
            TemplatePreviewPath = filePath;
            
            // Load Excel data if it's an Excel file
            if (extension == ".xlsx" || extension == ".xls")
            {
                await LoadExcelData(filePath);
            }
            
            IsTemplateLoaded = true;
            MappingInstructions = GetMappingInstructions(extension);
            IsMappingMode = true; // Enable mapping mode when template is loaded

            Console.WriteLine($"📋 Template loaded successfully: {fileName}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error processing template: {ex.Message}";
            Console.WriteLine($"❌ Error in ProcessSelectedTemplate: {ex.Message}");
        }
    }

    /// <summary>
    /// Load existing field mappings for the current template
    /// </summary>
    private async Task LoadExistingMappings()
    {
        if (CurrentTemplate == null) return;

        try
        {
            var mappings = await _context.TemplateFieldMappings
                .Where(m => m.TemplateId == CurrentTemplate.Id)
                .OrderBy(m => m.CreatedDate)
                .ToListAsync();

            FieldMappings.Clear();
            foreach (var mapping in mappings)
            {
                FieldMappings.Add(mapping);
            }

            Console.WriteLine($"📊 Loaded {mappings.Count} existing field mappings");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading existing mappings: {ex.Message}");
        }
    }

    /// <summary>
    /// Load Excel data from file into DataTable for grid display
    /// </summary>
    private async Task LoadExcelData(string filePath)
    {
        try
        {
            StatusMessage = "Loading Excel data...";
            
            ExcelGridData = await _excelDataService.ReadExcelFileAsync(filePath);
            ExcelData = ExcelGridData.DataTable;
            
            // Clear and repopulate ExcelRows instead of replacing the collection
            ExcelRows.Clear();
            foreach (var row in ExcelGridData.ExcelRows)
            {
                ExcelRows.Add(row);
            }
            
            ExcelColumns = ExcelGridData.ColumnNames;
            
            // Generate HTML preview using new ExcelDataService method
            HtmlPreviewContent = ExcelDataService.GenerateHtmlTable(ExcelGridData, enableClickToTeach: true);
            HtmlContentStatus = $"HTML generated: {HtmlPreviewContent.Length / 1024.0:F1} KB";
            
            // Notify property changes
            OnPropertyChanged(nameof(HasExcelData));
            
            StatusMessage = $"Excel data loaded: {ExcelRows.Count} rows, {ExcelColumns.Count} columns";
            Console.WriteLine($"📊 Excel grid data ready: {ExcelGridData.WorksheetName} worksheet");
            Console.WriteLine($"🔍 HasExcelData: {HasExcelData}");
            Console.WriteLine($"🔍 ExcelRows count: {ExcelRows.Count}");
            Console.WriteLine($"🔍 ExcelColumns: {string.Join(", ", ExcelColumns)}");
            Console.WriteLine($"🌐 HTML preview generated ({HtmlPreviewContent.Length} characters)");
            
            // Trigger Canvas drawing after data is loaded (for Template Preview)
            CanvasStatus = $"✅ Canvas drawing initiated for {ExcelRows.Count} rows × {ExcelColumns.Count} columns";
            
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading Excel data: {ex.Message}";
            Console.WriteLine($"❌ Error loading Excel data: {ex.Message}");
            
            // Create empty collections as fallback
            ExcelData = new DataTable();
            ExcelRows = new ObservableCollection<ExcelRowData>();
            ExcelColumns = new List<string>();
            ExcelGridData = new ExcelGridData 
            { 
                DataTable = ExcelData,
                ExcelRows = ExcelRows,
                ColumnNames = ExcelColumns
            };
        }
    }




    /// <summary>
    /// Generate HTML preview for Excel template
    /// </summary>
    [RelayCommand]
    private async Task GenerateHtmlPreview()
    {
        try
        {
            if (ExcelGridData == null)
            {
                StatusMessage = "No Excel data available - load a template first";
                return;
            }

            StatusMessage = "Generating HTML preview...";
            
            await Task.Run(() =>
            {
                HtmlPreviewContent = ExcelDataService.GenerateHtmlTable(ExcelGridData, enableClickToTeach: true);
                HtmlContentStatus = $"HTML generated: {HtmlPreviewContent.Length / 1024.0:F1} KB • Click cells to map fields";
            });
            
            StatusMessage = "HTML preview generated - ready for click-to-teach mapping";
            Console.WriteLine($"🌐 HTML preview manually generated: {HtmlPreviewContent.Length} characters");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating HTML preview: {ex.Message}";
            Console.WriteLine($"❌ Error in GenerateHtmlPreview: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle HTML cell click from WebView JavaScript
    /// </summary>
    public async Task HandleHtmlCellClick(int row, int column, string cellReference, string cellValue)
    {
        if (ExcelGridData == null) return; // Removed IsMappingMode check - always enabled

        try
        {
            SelectedRow = row;
            SelectedColumn = column;
            SelectedCellReference = cellReference;
            
            StatusMessage = $"Selected cell {cellReference}" + 
                          (string.IsNullOrEmpty(cellValue) ? "" : $" ('{cellValue}')");
            
            Console.WriteLine($"🎯 HTML Cell selected: {cellReference} at grid position ({row}, {column})");
            
            // Show field mapping dialog
            await ShowFieldMappingDialog(cellReference, cellValue, row, column);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error handling HTML cell click: {ex.Message}";
            Console.WriteLine($"❌ Error in HandleHtmlCellClick: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle DataGrid cell selection for Excel mapping (legacy support)
    /// </summary>
    public async void HandleCellSelection(int row, int column)
    {
        if (ExcelGridData == null) return; // Removed IsMappingMode check - always enabled

        try
        {
            SelectedRow = row;
            SelectedColumn = column;
            
            // Convert DataGrid position to Excel cell reference
            SelectedCellReference = ExcelDataService.GetCellReferenceFromGridPosition(
                row, column, ExcelGridData.StartRow, ExcelGridData.StartColumn);
            
            // Get cell value for display
            var cellValue = "";
            if (ExcelRows != null && row < ExcelRows.Count && column < ExcelColumns.Count)
            {
                cellValue = ExcelRows[row].GetCellByIndex(column, ExcelColumns);
            }
            
            StatusMessage = $"Selected cell {SelectedCellReference}" + 
                          (string.IsNullOrEmpty(cellValue) ? "" : $" ('{cellValue}')");
            
            Console.WriteLine($"🎯 Cell selected: {SelectedCellReference} at grid position ({row}, {column})");
            
            // Show field mapping dialog
            await ShowFieldMappingDialog(SelectedCellReference, cellValue, row, column);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting cell: {ex.Message}";
            Console.WriteLine($"❌ Error in HandleCellSelection: {ex.Message}");
        }
    }

    /// <summary>
    /// Show field mapping dialog for the selected cell
    /// </summary>
    private async Task ShowFieldMappingDialog(string cellReference, string cellValue, int row, int column)
    {
        try
        {
            if (CurrentTemplate == null) return;

            // Check if there's an existing mapping for this cell
            var existingMapping = FieldMappings.FirstOrDefault(m => m.TargetLocation == cellReference);
            
            // Create dialog view model
            var dialogViewModel = new FieldMappingDialogViewModel();
            
            if (existingMapping != null)
            {
                dialogViewModel.InitializeWithExistingMapping(cellReference, cellValue, row, column, existingMapping.FieldName);
            }
            else
            {
                dialogViewModel.Initialize(cellReference, cellValue, row, column);
            }

            // Show dialog
            var dialog = new Views.FieldMappingDialog
            {
                DataContext = dialogViewModel
            };

            // Get the main window to set as parent
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var result = await dialog.ShowDialog<Views.FieldMappingResult?>(desktop.MainWindow);
                
                if (result?.IsSuccess == true)
                {
                    if (result.IsClearOperation)
                    {
                        await ClearFieldMapping(cellReference);
                    }
                    else
                    {
                        await SaveFieldMapping(cellReference, result.FieldName, result.IsCustomField);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error showing field mapping dialog: {ex.Message}";
            Console.WriteLine($"❌ Error in ShowFieldMappingDialog: {ex.Message}");
        }
    }

    /// <summary>
    /// Save field mapping to database
    /// </summary>
    private async Task SaveFieldMapping(string cellReference, string fieldName, bool isCustomField)
    {
        try
        {
            if (CurrentTemplate == null) return;

            // Check if mapping already exists
            var existingMapping = await _context.TemplateFieldMappings
                .FirstOrDefaultAsync(m => m.TemplateId == CurrentTemplate.Id && m.TargetLocation == cellReference);

            if (existingMapping != null)
            {
                // Update existing mapping
                existingMapping.FieldName = fieldName;
                existingMapping.Description = isCustomField ? "Custom field" : "Predefined field";
                existingMapping.FormatInstructions = "";
                
                Console.WriteLine($"📝 Updated field mapping: {cellReference} → {fieldName}");
            }
            else
            {
                // Create new mapping
                var newMapping = new TemplateFieldMapping
                {
                    Id = Guid.NewGuid().ToString(),
                    TemplateId = CurrentTemplate.Id,
                    FieldName = fieldName,
                    TargetLocation = cellReference,
                    LocationType = "ExcelCell",
                    Description = isCustomField ? "Custom field" : "Predefined field",
                    FormatInstructions = "",
                    IsRequired = false,
                    DisplayOrder = FieldMappings.Count + 1,
                    CreatedDate = DateTime.UtcNow
                };

                _context.TemplateFieldMappings.Add(newMapping);
                FieldMappings.Add(newMapping);
                
                Console.WriteLine($"➕ Created field mapping: {cellReference} → {fieldName}");
            }

            await _context.SaveChangesAsync();
            
            StatusMessage = $"Field mapping saved: {fieldName} → {cellReference}";
            
            // Refresh mappings display
            await LoadFieldMappings();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving field mapping: {ex.Message}";
            Console.WriteLine($"❌ Error in SaveFieldMapping: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear field mapping from database
    /// </summary>
    private async Task ClearFieldMapping(string cellReference)
    {
        try
        {
            if (CurrentTemplate == null) return;

            var mapping = await _context.TemplateFieldMappings
                .FirstOrDefaultAsync(m => m.TemplateId == CurrentTemplate.Id && m.TargetLocation == cellReference);

            if (mapping != null)
            {
                _context.TemplateFieldMappings.Remove(mapping);
                await _context.SaveChangesAsync();
                
                // Remove from local collection
                var localMapping = FieldMappings.FirstOrDefault(m => m.TargetLocation == cellReference);
                if (localMapping != null)
                {
                    FieldMappings.Remove(localMapping);
                }
                
                StatusMessage = $"Field mapping cleared for {cellReference}";
                Console.WriteLine($"🗑️ Cleared field mapping for {cellReference}");
                
                // Refresh mappings display
                await LoadFieldMappings();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing field mapping: {ex.Message}";
            Console.WriteLine($"❌ Error in ClearFieldMapping: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle click on template coordinate (legacy support)
    /// </summary>
    public async Task HandleTemplateClick(double x, double y)
    {
        if (CurrentTemplate == null) return; // Removed IsMappingMode check - always enabled

        try
        {
            StatusMessage = $"Clicked at position ({x:F0}, {y:F0})";
            
            // For non-Excel templates, keep coordinate-based mapping
            Console.WriteLine($"🎯 Template clicked at ({x:F0}, {y:F0}) - Ready for field mapping");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error handling template click: {ex.Message}";
            Console.WriteLine($"❌ Error in HandleTemplateClick: {ex.Message}");
        }
    }

    /// <summary>
    /// Load field mappings for the current template
    /// </summary>
    private async Task LoadFieldMappings()
    {
        try
        {
            if (CurrentTemplate == null) return;

            var mappings = await _context.TemplateFieldMappings
                .Where(m => m.TemplateId == CurrentTemplate.Id)
                .OrderBy(m => m.CreatedDate)
                .ToListAsync();

            FieldMappings.Clear();
            foreach (var mapping in mappings)
            {
                FieldMappings.Add(mapping);
            }

            Console.WriteLine($"📊 Loaded {mappings.Count} existing field mappings");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading field mappings: {ex.Message}");
        }
    }

    /// <summary>
    /// Save a new field mapping
    /// </summary>
    [RelayCommand]
    private async Task SaveFieldMapping()
    {
        if (CurrentTemplate == null || string.IsNullOrWhiteSpace(CurrentFieldName))
        {
            StatusMessage = "Please specify field name and select a cell";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCellReference) && SelectedRow == -1)
        {
            StatusMessage = "Please select a cell to map this field to";
            return;
        }

        try
        {
            var targetLocation = !string.IsNullOrWhiteSpace(SelectedCellReference) 
                ? SelectedCellReference 
                : $"Row{SelectedRow}Col{SelectedColumn}";

            var mapping = new TemplateFieldMapping
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = CurrentTemplate.Id,
                FieldName = CurrentFieldName,
                LocationType = DetermineFieldType(CurrentFieldName),
                TargetLocation = targetLocation,
                Description = CurrentFieldDescription,
                CreatedDate = DateTime.Now
            };

            _context.TemplateFieldMappings.Add(mapping);
            await _context.SaveChangesAsync();

            FieldMappings.Add(mapping);

            // Clear current selection
            CurrentFieldName = "";
            CurrentFieldDescription = "";
            SelectedCellReference = "";
            SelectedRow = -1;
            SelectedColumn = -1;
            
            StatusMessage = $"Field mapping saved to {targetLocation}";

            Console.WriteLine($"💾 Field mapping saved: {mapping.FieldName} → {targetLocation}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving field mapping: {ex.Message}";
            Console.WriteLine($"❌ Error in SaveFieldMapping: {ex.Message}");
        }
    }

    /// <summary>
    /// Zoom in on template preview
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel + 0.25, 3.0);
        StatusMessage = $"Zoom: {ZoomLevel:P0}";
    }

    /// <summary>
    /// Zoom out on template preview
    /// </summary>
    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel - 0.25, 0.25);
        StatusMessage = $"Zoom: {ZoomLevel:P0}";
    }

    /// <summary>
    /// Reset zoom to 100%
    /// </summary>
    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        StatusMessage = "Zoom reset to 100%";
    }

    /// <summary>
    /// Get mapping instructions based on file type
    /// </summary>
    private string GetMappingInstructions(string extension)
    {
        return extension switch
        {
            ".xlsx" or ".xls" => "🎯 CLICK-TO-TEACH EXCEL MAPPING\n\n• Click any cell to teach field mapping\n• Select field type from dropdown menu\n• Mappings saved automatically to database\n• Ready for automated data extraction",
            ".pdf" => "🎯 CLICK-TO-TEACH PDF MAPPING\n\n• Click on form fields or text areas\n• Teach where each data field belongs\n• Perfect for fillable PDF forms",
            ".docx" or ".doc" => "🎯 CLICK-TO-TEACH WORD MAPPING\n\n• Click on tables or document sections\n• Map data fields to specific locations\n• Great for structured reports",
            _ => "🎯 CLICK-TO-TEACH MAPPING\n\n• Click anywhere to define field positions\n• Teach the system where data belongs\n• Build intelligent automation patterns"
        };
    }

    /// <summary>
    /// Determine template category from file extension
    /// </summary>
    private string DetermineTemplateCategory(string extension)
    {
        return extension switch
        {
            ".xlsx" or ".xls" => "Excel Spreadsheet",
            ".pdf" => "PDF Form",
            ".docx" or ".doc" => "Word Document",
            ".png" or ".jpg" or ".jpeg" => "Image Template",
            _ => "General Template"
        };
    }

    /// <summary>
    /// Determine field type from field name
    /// </summary>
    private string DetermineFieldType(string fieldName)
    {
        var lower = fieldName.ToLowerInvariant();
        
        if (lower.Contains("total") || lower.Contains("amount")) return "Currency";
        if (lower.Contains("date")) return "Date";
        if (lower.Contains("number") || lower.Contains("id")) return "Text";
        if (lower.Contains("name") || lower.Contains("company")) return "Text";
        
        return "Text";
    }

    /// <summary>
    /// Open file picker using Avalonia's file picker
    /// </summary>
    private async Task<IReadOnlyList<string>?> OpenFilePickerAsync(string title, string[] filters)
    {
        try
        {
            // Get the main window
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;

            if (mainWindow == null)
            {
                Console.WriteLine("❌ No main window available for file picker");
                return null;
            }

            // Create file type filters for Avalonia
            var fileTypeFilters = new List<Avalonia.Platform.Storage.FilePickerFileType>();
            
            // Excel files
            fileTypeFilters.Add(new Avalonia.Platform.Storage.FilePickerFileType("Excel Files")
            {
                Patterns = new[] { "*.xlsx", "*.xls" }
            });
            
            // PDF files
            fileTypeFilters.Add(new Avalonia.Platform.Storage.FilePickerFileType("PDF Files")
            {
                Patterns = new[] { "*.pdf" }
            });
            
            // Word files
            fileTypeFilters.Add(new Avalonia.Platform.Storage.FilePickerFileType("Word Files")
            {
                Patterns = new[] { "*.docx", "*.doc" }
            });
            
            // Image files
            fileTypeFilters.Add(new Avalonia.Platform.Storage.FilePickerFileType("Image Files")
            {
                Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tiff" }
            });
            
            // All files
            fileTypeFilters.Add(new Avalonia.Platform.Storage.FilePickerFileType("All Files")
            {
                Patterns = new[] { "*.*" }
            });

            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = fileTypeFilters
            };

            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(options);
            
            if (result?.Count > 0)
            {
                var filePaths = result.Select(f => f.Path.LocalPath).ToList();
                Console.WriteLine($"📁 File selected: {filePaths[0]}");
                return filePaths;
            }
            
            Console.WriteLine($"🗂️ File picker cancelled: {title}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in file picker: {ex.Message}");
            return null;
        }
    }
}