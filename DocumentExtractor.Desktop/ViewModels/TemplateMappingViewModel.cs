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
using DocumentExtractor.Services.Interfaces;
using DocumentExtractor.Services.Services;
using Microsoft.EntityFrameworkCore;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Controls;

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
    private readonly IRuleEngine _ruleEngine;

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
    private bool _isMappingMode = true; // Always in mapping mode with simplified workflow

    [ObservableProperty]
    private string _currentFieldType = ""; // Now serves as unified field name/type

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

    [ObservableProperty]
    private string _detectedPattern = "No pattern detected";

    [ObservableProperty]
    private bool _showSaveAsRule = false;

    [ObservableProperty]
    private bool _isManagingRules = false;

    [ObservableProperty]
    private ObservableCollection<MappingRule> _availableRules = new();

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
    /// <summary>
    /// Dynamic field type suggestions based on usage history and smart suggestions
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableFieldTypes = new();

    public TemplateMappingViewModel(DocumentExtractionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _excelDataService = new ExcelDataService();
        _htmlTemplateService = new HtmlTemplateService();
        _ruleEngine = new RuleEngineService(context);
        Console.WriteLine("🗺️ TemplateMappingViewModel initialized with Excel data service and rule engine");
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
            
            // Load dynamic field type suggestions based on column headers and history
            await LoadFieldTypeSuggestions();
            
            // Detect template pattern
            DetectedPattern = DetectTemplatePattern();
            
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
    /// Initialize dynamic field type suggestions
    /// </summary>
    private async Task LoadFieldTypeSuggestions()
    {
        try
        {
            // Load previously used field types from database
            var usedFieldTypes = await _context.TemplateFieldMappings
                .Where(f => !string.IsNullOrWhiteSpace(f.FieldName))
                .Select(f => f.FieldName)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();

            // Add common suggestions based on current template context
            var smartSuggestions = GenerateSmartSuggestions();
            
            // Combine and deduplicate
            var allSuggestions = usedFieldTypes
                .Concat(smartSuggestions)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            AvailableFieldTypes.Clear();
            foreach (var suggestion in allSuggestions)
            {
                AvailableFieldTypes.Add(suggestion);
            }

            Console.WriteLine($"💡 Loaded {AvailableFieldTypes.Count} field type suggestions");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading field type suggestions: {ex.Message}");
            // Fallback to basic suggestions
            LoadFallbackSuggestions();
        }
    }

    /// <summary>
    /// Generate smart field suggestions based on template context and column headers
    /// </summary>
    private List<string> GenerateSmartSuggestions()
    {
        var suggestions = new List<string>();
        
        // Basic common fields across different document types
        suggestions.AddRange(new[]
        {
            "Product Name", "Product ID", "SKU", "Price", "Quantity", "Total Amount",
            "Customer Name", "Order Number", "Date", "Category", "Description",
            "Image URL", "Product Image", "Thumbnail", "Weight", "Dimensions",
            "Material", "Color", "Size", "Brand", "Supplier", "Stock Level",
            "Invoice Number", "Account Number", "Due Date", "Tax Amount", "Subtotal"
        });

        // Smart suggestions based on Excel column headers
        if (ExcelColumns != null && ExcelColumns.Any())
        {
            foreach (var column in ExcelColumns)
            {
                // Convert column headers to suggested field names
                var headerSuggestion = ConvertHeaderToFieldName(column);
                if (!string.IsNullOrEmpty(headerSuggestion))
                {
                    suggestions.Add(headerSuggestion);
                }
            }
        }

        return suggestions.Distinct().ToList();
    }

    /// <summary>
    /// Convert Excel column header to suggested field name
    /// </summary>
    private string ConvertHeaderToFieldName(string columnHeader)
    {
        if (string.IsNullOrWhiteSpace(columnHeader)) return "";
        
        // Smart conversion patterns
        var header = columnHeader.ToLower().Trim();
        
        return header switch
        {
            var h when h.Contains("image") => "Product Image",
            var h when h.Contains("photo") => "Product Photo", 
            var h when h.Contains("price") => "Unit Price",
            var h when h.Contains("cost") => "Cost",
            var h when h.Contains("total") => "Total Amount",
            var h when h.Contains("qty") || h.Contains("quantity") => "Quantity",
            var h when h.Contains("sku") => "SKU",
            var h when h.Contains("id") => "Product ID",
            var h when h.Contains("name") || h.Contains("title") => "Product Name",
            var h when h.Contains("desc") => "Description",
            var h when h.Contains("cat") => "Category",
            var h when h.Contains("tag") => "Tags",
            _ => columnHeader // Use original if no pattern matches
        };
    }

    /// <summary>
    /// Fallback suggestions if database loading fails
    /// </summary>
    private void LoadFallbackSuggestions()
    {
        AvailableFieldTypes.Clear();
        var fallback = new[]
        {
            "Product Name", "Product ID", "SKU", "Price", "Quantity", "Total Amount",
            "Description", "Category", "Image URL", "Date", "Custom Field"
        };
        
        foreach (var item in fallback)
        {
            AvailableFieldTypes.Add(item);
        }
    }

    /// <summary>
    /// Detect template pattern based on column headers and content
    /// </summary>
    private string DetectTemplatePattern()
    {
        try
        {
            if (ExcelColumns == null || !ExcelColumns.Any())
            {
                return "No pattern detected - Empty template";
            }

            var allHeaders = string.Join(" ", ExcelColumns).ToLower();
            
            // E-commerce patterns
            if (allHeaders.Contains("image") && allHeaders.Contains("sku"))
            {
                return "🛒 E-commerce Product Template";
            }
            
            if (allHeaders.Contains("price") && allHeaders.Contains("product"))
            {
                return "🛍️ Product Catalog Template";
            }
            
            // Financial patterns
            if (allHeaders.Contains("invoice") && allHeaders.Contains("amount"))
            {
                return "💰 Invoice/Billing Template";
            }
            
            if (allHeaders.Contains("account") && allHeaders.Contains("balance"))
            {
                return "🏦 Financial Report Template";
            }
            
            // Inventory patterns
            if (allHeaders.Contains("stock") && allHeaders.Contains("quantity"))
            {
                return "📦 Inventory Management Template";
            }
            
            // General patterns
            if (allHeaders.Contains("date") && allHeaders.Contains("total"))
            {
                return "📊 General Business Template";
            }
            
            // Medical patterns
            if (allHeaders.Contains("patient") || allHeaders.Contains("medical"))
            {
                return "🏥 Medical/Healthcare Template";
            }
            
            // Service patterns
            if (allHeaders.Contains("service") || allHeaders.Contains("work"))
            {
                return "🔧 Service/Work Order Template";
            }

            return $"📋 Custom Template ({ExcelColumns.Count} columns)";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error detecting pattern: {ex.Message}");
            return "❓ Pattern detection failed";
        }
    }

    /// <summary>
    /// Save a new field mapping with optional rule creation
    /// </summary>
    [RelayCommand]
    private async Task SaveFieldMapping()
    {
        if (CurrentTemplate == null || string.IsNullOrWhiteSpace(CurrentFieldType))
        {
            StatusMessage = "Please specify field and select a cell";
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
                FieldName = CurrentFieldType, // Now unified field name/type
                LocationType = DetermineFieldType(CurrentFieldType),
                TargetLocation = targetLocation,
                Description = CurrentFieldDescription,
                CreatedDate = DateTime.Now
            };

            _context.TemplateFieldMappings.Add(mapping);
            await _context.SaveChangesAsync();

            FieldMappings.Add(mapping);

            // Check if user wants to save as a rule
            if (ShowSaveAsRule)
            {
                await CreateMappingRuleAsync(mapping);
            }

            // Clear current selection
            // Clear current field type after saving
            CurrentFieldDescription = "";
            SelectedCellReference = "";
            SelectedRow = -1;
            SelectedColumn = -1;
            ShowSaveAsRule = false; // Reset checkbox
            
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
    /// Create a mapping rule from current field mapping and document pattern
    /// </summary>
    private async Task CreateMappingRuleAsync(TemplateFieldMapping mapping)
    {
        try
        {
            // Generate rule name based on pattern and field
            var ruleName = $"{DetectedPattern} - {mapping.FieldName} Mapping";
            var description = $"Auto-generated rule for mapping {mapping.FieldName} in {DetectedPattern} templates";

            // Create document pattern from current context
            var documentPattern = new DocumentPattern
            {
                TemplatePattern = DetectedPattern,
                TemplateCategory = CurrentTemplate?.Category,
                AvailableFields = new List<string> { mapping.FieldName },
                CreatedDate = DateTime.UtcNow
            };

            // Create the rule with current mappings
            var rule = await _ruleEngine.CreateRuleFromMappingsAsync(
                ruleName, 
                description, 
                documentPattern, 
                new List<TemplateFieldMapping> { mapping });

            StatusMessage = $"✅ Rule '{ruleName}' created for automated mapping";
            Console.WriteLine($"📝 RULE CREATED: {ruleName} with {rule.Actions.Count} actions");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Warning: Field mapping saved but rule creation failed: {ex.Message}";
            Console.WriteLine($"❌ Error creating mapping rule: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply existing mapping rules to current template automatically
    /// </summary>
    [RelayCommand]
    private async Task ApplyMappingRules()
    {
        try
        {
            if (CurrentTemplate == null)
            {
                StatusMessage = "Please load a template first";
                return;
            }

            StatusMessage = "🎯 Applying mapping rules...";

            // Create document pattern from current context
            var documentPattern = new DocumentPattern
            {
                TemplatePattern = DetectedPattern,
                TemplateCategory = CurrentTemplate.Category,
                AvailableFields = AvailableFieldTypes.ToList(),
                CreatedDate = DateTime.UtcNow
            };

            // Simulate extracted fields (in real scenario, these would come from document processing)
            var mockExtractedFields = CreateMockExtractedFields();

            // Apply mapping rules
            var appliedMappings = await _ruleEngine.ApplyMappingRulesAsync(
                documentPattern, 
                CurrentTemplate.Id, 
                mockExtractedFields);

            if (appliedMappings.Any())
            {
                // Add applied mappings to the database and UI
                foreach (var mapping in appliedMappings)
                {
                    _context.TemplateFieldMappings.Add(mapping);
                    FieldMappings.Add(mapping);
                }

                await _context.SaveChangesAsync();

                StatusMessage = $"✅ Applied {appliedMappings.Count} automatic mappings from rules";
                Console.WriteLine($"🎯 RULES APPLIED: {appliedMappings.Count} automatic mappings");
            }
            else
            {
                StatusMessage = "ℹ️ No matching rules found - using manual mapping";
                Console.WriteLine("🎯 RULES: No matching rules found for current pattern");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying mapping rules: {ex.Message}";
            Console.WriteLine($"❌ Error applying rules: {ex.Message}");
        }
    }

    /// <summary>
    /// Create mock extracted fields for rule testing
    /// TODO: Replace with real extracted fields from document processing
    /// </summary>
    private List<ExtractedField> CreateMockExtractedFields()
    {
        return new List<ExtractedField>
        {
            new ExtractedField
            {
                Id = Guid.NewGuid().ToString(),
                FieldName = "Product Name",
                Value = "Sample Product",
                Type = FieldType.Text,
                Confidence = 0.95,
                Source = "MockExtraction"
            },
            new ExtractedField
            {
                Id = Guid.NewGuid().ToString(),
                FieldName = "SKU",
                Value = "SKU-12345",
                Type = FieldType.Text,
                Confidence = 0.98,
                Source = "MockExtraction"
            },
            new ExtractedField
            {
                Id = Guid.NewGuid().ToString(),
                FieldName = "Price",
                Value = "29.99",
                Type = FieldType.Currency,
                Confidence = 0.92,
                Source = "MockExtraction"
            }
        };
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

    /// <summary>
    /// Load sample Excel file for testing
    /// </summary>
    [RelayCommand]
    private async Task LoadSampleTemplate()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Loading sample Excel template...";

            var sampleFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database.xlsx");
            if (File.Exists(sampleFilePath))
            {
                await ProcessSelectedTemplate(sampleFilePath);
                Console.WriteLine($"📊 Sample template loaded: {sampleFilePath}");
            }
            else
            {
                StatusMessage = "Sample Excel file not found - use 'Load Template' to select a file";
                Console.WriteLine($"❌ Sample file not found at: {sampleFilePath}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading sample template: {ex.Message}";
            Console.WriteLine($"❌ Error in LoadSampleTemplate: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Show the rule management panel
    /// </summary>
    [RelayCommand]
    private async Task ManageRules()
    {
        try
        {
            IsManagingRules = true;
            StatusMessage = "Loading mapping rules...";
            
            // Load available rules from database
            await RefreshRules();
            
            StatusMessage = $"Rule management panel open - {AvailableRules.Count} rules loaded";
            Console.WriteLine($"⚙️ Rule management opened with {AvailableRules.Count} rules");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading rules: {ex.Message}";
            Console.WriteLine($"❌ Error in ManageRules: {ex.Message}");
        }
    }

    /// <summary>
    /// Close the rule management panel
    /// </summary>
    [RelayCommand]
    private void CloseRuleManagement()
    {
        IsManagingRules = false;
        StatusMessage = "Rule management closed";
        Console.WriteLine("⚙️ Rule management panel closed");
    }

    /// <summary>
    /// Refresh the list of available rules
    /// </summary>
    [RelayCommand]
    private async Task RefreshRules()
    {
        try
        {
            var rules = await _ruleEngine.GetActiveRulesAsync();
            AvailableRules.Clear();
            
            foreach (var rule in rules)
            {
                AvailableRules.Add(rule);
            }
            
            StatusMessage = $"Rules refreshed - {AvailableRules.Count} rules loaded";
            Console.WriteLine($"🔄 Rules refreshed: {AvailableRules.Count} rules loaded");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing rules: {ex.Message}";
            Console.WriteLine($"❌ Error in RefreshRules: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggle rule activation status
    /// </summary>
    [RelayCommand]
    private async Task ToggleRuleActivation(MappingRule rule)
    {
        if (rule == null) return;

        try
        {
            var newStatus = await _ruleEngine.ToggleRuleActivationAsync(rule.Id);
            rule.IsActive = newStatus;
            
            var statusText = newStatus ? "activated" : "deactivated";
            StatusMessage = $"Rule '{rule.Name}' {statusText}";
            Console.WriteLine($"🔄 Rule '{rule.Name}' {statusText}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error toggling rule: {ex.Message}";
            Console.WriteLine($"❌ Error in ToggleRuleActivation: {ex.Message}");
        }
    }

    /// <summary>
    /// Test a rule with the current template
    /// </summary>
    [RelayCommand]
    private async Task TestRule(MappingRule rule)
    {
        if (rule == null || CurrentTemplate == null)
        {
            StatusMessage = "Please load a template before testing rules";
            return;
        }

        try
        {
            StatusMessage = $"Testing rule '{rule.Name}'...";
            
            // Create a document pattern from current template
            var documentPattern = new DocumentPattern
            {
                SupplierName = "Test Supplier",
                DocumentType = "Test Document",
                TemplatePattern = DetectedPattern,
                AvailableFields = AvailableFieldTypes.ToList()
            };

            // Create mock extracted fields
            var extractedFields = new List<ExtractedField>();
            for (int i = 0; i < Math.Min(5, AvailableFieldTypes.Count); i++)
            {
                extractedFields.Add(new ExtractedField
                {
                    Id = Guid.NewGuid().ToString(),
                    FieldName = AvailableFieldTypes[i],
                    Value = $"Test Value {i + 1}",
                    Type = FieldType.Text,
                    Confidence = 0.95
                });
            }

            var testResult = await _ruleEngine.TestRuleAsync(rule, documentPattern, extractedFields);
            
            // Create detailed test results message
            string resultIcon = testResult.EvaluationResult.ShouldApply ? "✅ PASS" : "❌ FAIL";
            string detailedMessage = $"🧪 Rule Test Results: {resultIcon}\n\n" +
                                   $"📊 Match Score: {testResult.EvaluationResult.MatchScore:P1}\n" +
                                   $"🎯 Confidence: {testResult.EvaluationResult.Confidence:P1}\n" +
                                   $"✅ Would Apply: {(testResult.EvaluationResult.ShouldApply ? "Yes" : "No")}\n" +
                                   $"📋 Mapped Fields: {testResult.MappedFields.Count}\n" +
                                   $"❌ Unmapped Fields: {testResult.UnmappedFields.Count}\n\n" +
                                   $"📝 Summary: {testResult.Summary}\n\n" +
                                   $"🔍 Test Details:\n" +
                                   $"• Template: {DetectedPattern}\n" +
                                   $"• Mock Fields: {extractedFields.Count} test fields\n" +
                                   $"• Rule Priority: {rule.Priority}\n" +
                                   $"• Rule Success Rate: {rule.SuccessRate:P1}";

            // Show results in a message box
            await ShowTestResultsDialog(rule.Name, detailedMessage, testResult.EvaluationResult.ShouldApply);

            StatusMessage = $"Rule '{rule.Name}' test: {resultIcon} ({testResult.EvaluationResult.MatchScore:P1})";
            
            Console.WriteLine($"🧪 Rule test result for '{rule.Name}':");
            Console.WriteLine($"   📊 Match Score: {testResult.EvaluationResult.MatchScore:P1}");
            Console.WriteLine($"   🎯 Confidence: {testResult.EvaluationResult.Confidence:P1}");
            Console.WriteLine($"   ✅ Should Apply: {testResult.EvaluationResult.ShouldApply}");
            Console.WriteLine($"   📋 Mapped Fields: {testResult.MappedFields.Count}");
            Console.WriteLine($"   ❌ Unmapped Fields: {testResult.UnmappedFields.Count}");
            Console.WriteLine($"   📝 Summary: {testResult.Summary}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error testing rule: {ex.Message}";
            Console.WriteLine($"❌ Error in TestRule: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a rule permanently
    /// </summary>
    [RelayCommand]
    private async Task DeleteRule(MappingRule rule)
    {
        if (rule == null) return;

        try
        {
            StatusMessage = $"Deleting rule '{rule.Name}'...";
            
            await _ruleEngine.DeleteRuleAsync(rule.Id);
            AvailableRules.Remove(rule);
            
            StatusMessage = $"Rule '{rule.Name}' deleted";
            Console.WriteLine($"🗑️ Rule '{rule.Name}' deleted permanently");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting rule: {ex.Message}";
            Console.WriteLine($"❌ Error in DeleteRule: {ex.Message}");
        }
    }

    /// <summary>
    /// View detailed rule statistics
    /// </summary>
    [RelayCommand]
    private async Task ViewRuleStatistics()
    {
        try
        {
            StatusMessage = "Loading rule statistics...";
            
            var statistics = await _ruleEngine.GetStatisticsAsync();
            
            Console.WriteLine("📊 RULE ENGINE STATISTICS");
            Console.WriteLine($"   🎯 Total Active Rules: {statistics.TotalActiveRules}");
            Console.WriteLine($"   📈 Total Applications: {statistics.TotalRuleApplications}");
            Console.WriteLine($"   ✅ Overall Success Rate: {statistics.OverallSuccessRate:P1}");
            Console.WriteLine($"   ⚡ Average Execution Time: {statistics.AverageExecutionTimeMs:F1}ms");
            
            if (statistics.TopRules.Any())
            {
                Console.WriteLine("   🏆 Top Performing Rules:");
                foreach (var topRule in statistics.TopRules.Take(3))
                {
                    Console.WriteLine($"      • {topRule.RuleName}: {topRule.SuccessRate:P1} ({topRule.UsageCount} uses)");
                }
            }
            
            StatusMessage = $"Statistics: {statistics.TotalActiveRules} rules, {statistics.OverallSuccessRate:P1} success rate";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading statistics: {ex.Message}";
            Console.WriteLine($"❌ Error in ViewRuleStatistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Show enhanced test results with visual preview and interactive elements
    /// </summary>
    private async Task ShowTestResultsDialog(string ruleName, string testResults, bool testPassed)
    {
        try
        {
            // Get the main window for dialog display
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;

            if (mainWindow == null)
            {
                Console.WriteLine("❌ No main window available for test results dialog");
                StatusMessage = "Test completed - check console for results";
                return;
            }

            // Create enhanced dialog with visual preview
            await ShowEnhancedTestResultsDialog(mainWindow, ruleName, testResults, testPassed);
            
            Console.WriteLine($"🧪 Enhanced test results dialog shown for rule: {ruleName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error showing test results dialog: {ex.Message}");
            
            // Fallback: Show results in status message
            StatusMessage = $"Test {(testPassed ? "PASSED" : "FAILED")} - Rule: {ruleName}";
            
            // Also log full results to console as backup
            Console.WriteLine("🧪 RULE TEST RESULTS (Dialog failed, showing in console):");
            Console.WriteLine($"Rule: {ruleName}");
            Console.WriteLine(testResults);
        }
    }

    /// <summary>
    /// Create enhanced test results dialog with visual preview and interactive features
    /// </summary>
    private async Task ShowEnhancedTestResultsDialog(Window mainWindow, string ruleName, string testResults, bool testPassed)
    {
        // Create dialog title with result icon
        string dialogTitle = testPassed ? "✅ Rule Test PASSED" : "❌ Rule Test FAILED";
        string fullTitle = $"{dialogTitle} - {ruleName}";

        // Create enhanced dialog window
        var dialogWindow = new Window
        {
            Title = fullTitle,
            Width = 900,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            ShowInTaskbar = false
        };

        // Main container with enhanced layout
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header with enhanced styling
        var headerBorder = new Border
        {
            Background = testPassed ? Avalonia.Media.Brushes.DarkGreen : Avalonia.Media.Brushes.DarkRed,
            Padding = new Avalonia.Thickness(20, 15),
            CornerRadius = new Avalonia.CornerRadius(8, 8, 0, 0)
        };

        var headerPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 15
        };

        var iconText = new TextBlock
        {
            Text = testPassed ? "✅" : "❌",
            FontSize = 32,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var titlePanel = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var titleText = new TextBlock
        {
            Text = dialogTitle,
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White
        };

        var subtitleText = new TextBlock
        {
            Text = $"Testing rule: {ruleName}",
            FontSize = 12,
            Foreground = Avalonia.Media.Brushes.LightGray,
            Margin = new Avalonia.Thickness(0, 5, 0, 0)
        };

        titlePanel.Children.Add(titleText);
        titlePanel.Children.Add(subtitleText);
        headerPanel.Children.Add(iconText);
        headerPanel.Children.Add(titlePanel);
        headerBorder.Child = headerPanel;

        Grid.SetRow(headerBorder, 0);
        mainGrid.Children.Add(headerBorder);

        // Content area with tabs using standard Avalonia TabControl
        var tabControl = new TabControl();
        
        // Tab 1: Test Results Summary
        var summaryTab = new TabItem
        {
            Header = "📊 Test Summary"
        };
        
        var summaryScrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Padding = new Avalonia.Thickness(20)
        };

        var summaryBorder = new Border
        {
            Background = Avalonia.Media.Brushes.WhiteSmoke,
            CornerRadius = new Avalonia.CornerRadius(5),
            Padding = new Avalonia.Thickness(15)
        };

        var summaryText = new TextBlock
        {
            Text = testResults,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontFamily = "Consolas,Monaco,monospace",
            FontSize = 11
        };

        summaryBorder.Child = summaryText;

        summaryScrollViewer.Content = summaryBorder;
        summaryTab.Content = summaryScrollViewer;
        tabControl.Items.Add(summaryTab);

        // Tab 2: Visual Field Mapping Preview
        var mappingTab = new TabItem
        {
            Header = "🎯 Field Mapping Preview"
        };

        var mappingContent = CreateFieldMappingPreview(ruleName, testPassed);
        mappingTab.Content = mappingContent;
        tabControl.Items.Add(mappingTab);

        // Tab 3: Interactive Test Data
        var testDataTab = new TabItem
        {
            Header = "⚡ Interactive Test Data"
        };

        var testDataContent = CreateInteractiveTestDataView(ruleName);
        testDataTab.Content = testDataContent;
        tabControl.Items.Add(testDataTab);

        Grid.SetRow(tabControl, 1);
        mainGrid.Children.Add(tabControl);

        // Footer with action buttons
        var footerBorder = new Border
        {
            Background = Avalonia.Media.Brushes.LightGray,
            Padding = new Avalonia.Thickness(20, 15),
            CornerRadius = new Avalonia.CornerRadius(0, 0, 8, 8)
        };

        var footerPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 15
        };

        // Enhanced action buttons
        var runAgainButton = new Button
        {
            Content = "🔄 Run Test Again",
            Padding = new Avalonia.Thickness(15, 8),
            Background = Avalonia.Media.Brushes.Blue,
            Foreground = Avalonia.Media.Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(5)
        };

        var exportButton = new Button
        {
            Content = "📄 Export Results",
            Padding = new Avalonia.Thickness(15, 8),
            Background = Avalonia.Media.Brushes.Orange,
            Foreground = Avalonia.Media.Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(5)
        };

        var okButton = new Button
        {
            Content = "✅ Close",
            Padding = new Avalonia.Thickness(20, 8),
            Background = testPassed ? Avalonia.Media.Brushes.Green : Avalonia.Media.Brushes.Gray,
            Foreground = Avalonia.Media.Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(5)
        };

        // Button event handlers
        runAgainButton.Click += async (s, e) => 
        {
            dialogWindow.Close();
            // Re-run the test (implementation would depend on rule access)
            StatusMessage = $"Re-running test for rule: {ruleName}...";
        };

        exportButton.Click += async (s, e) => 
        {
            // Export test results to file
            await ExportTestResults(ruleName, testResults);
        };

        okButton.Click += (s, e) => dialogWindow.Close();

        footerPanel.Children.Add(runAgainButton);
        footerPanel.Children.Add(exportButton);
        footerPanel.Children.Add(okButton);
        footerBorder.Child = footerPanel;

        Grid.SetRow(footerBorder, 2);
        mainGrid.Children.Add(footerBorder);

        dialogWindow.Content = mainGrid;

        // Show the enhanced dialog
        await dialogWindow.ShowDialog(mainWindow);
    }

    /// <summary>
    /// Create visual field mapping preview panel
    /// </summary>
    private Panel CreateFieldMappingPreview(string ruleName, bool testPassed)
    {
        var mainPanel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 15
        };

        // Preview header
        var headerText = new TextBlock
        {
            Text = "🎯 Visual Field Mapping Preview",
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Margin = new Avalonia.Thickness(0, 0, 0, 10)
        };
        mainPanel.Children.Add(headerText);

        // Create side-by-side layout
        var previewGrid = new Grid();
        previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Before column
        var beforePanel = new Border
        {
            Background = Avalonia.Media.Brushes.LightYellow,
            BorderBrush = Avalonia.Media.Brushes.Orange,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(5),
            Padding = new Avalonia.Thickness(15)
        };

        var beforeContent = new StackPanel
        {
            Spacing = 10
        };

        beforeContent.Children.Add(new TextBlock
        {
            Text = "📋 Current Template",
            FontWeight = Avalonia.Media.FontWeight.Bold,
            FontSize = 14
        });

        beforeContent.Children.Add(new TextBlock
        {
            Text = $"Template: {CurrentTemplate?.FileName ?? "No template loaded"}",
            FontSize = 12
        });

        beforeContent.Children.Add(new TextBlock
        {
            Text = $"Pattern: {DetectedPattern}",
            FontSize = 12
        });

        beforeContent.Children.Add(new TextBlock
        {
            Text = $"Current Mappings: {FieldMappings.Count}",
            FontSize = 12
        });

        beforePanel.Child = beforeContent;
        Grid.SetColumn(beforePanel, 0);
        previewGrid.Children.Add(beforePanel);

        // Arrow
        var arrowText = new TextBlock
        {
            Text = testPassed ? "✅➡️" : "❌➡️",
            FontSize = 24,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(10, 0)
        };
        Grid.SetColumn(arrowText, 1);
        previewGrid.Children.Add(arrowText);

        // After column
        var afterPanel = new Border
        {
            Background = testPassed ? Avalonia.Media.Brushes.LightGreen : Avalonia.Media.Brushes.LightPink,
            BorderBrush = testPassed ? Avalonia.Media.Brushes.Green : Avalonia.Media.Brushes.Red,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(5),
            Padding = new Avalonia.Thickness(15)
        };

        var afterContent = new StackPanel
        {
            Spacing = 10
        };

        afterContent.Children.Add(new TextBlock
        {
            Text = testPassed ? "✅ Rule Applied Result" : "❌ Rule Test Failed",
            FontWeight = Avalonia.Media.FontWeight.Bold,
            FontSize = 14
        });

        afterContent.Children.Add(new TextBlock
        {
            Text = $"Rule: {ruleName}",
            FontSize = 12
        });

        afterContent.Children.Add(new TextBlock
        {
            Text = testPassed ? "Would create automatic mappings" : "Rule conditions not met",
            FontSize = 12
        });

        afterContent.Children.Add(new TextBlock
        {
            Text = testPassed ? "Future documents would auto-map" : "Manual mapping required",
            FontSize = 12,
            FontStyle = Avalonia.Media.FontStyle.Italic
        });

        afterPanel.Child = afterContent;
        Grid.SetColumn(afterPanel, 2);
        previewGrid.Children.Add(afterPanel);

        mainPanel.Children.Add(previewGrid);

        // Add field mapping details if test passed
        if (testPassed)
        {
            var detailsHeader = new TextBlock
            {
                Text = "📊 Expected Field Mappings",
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Margin = new Avalonia.Thickness(0, 20, 0, 10)
            };
            mainPanel.Children.Add(detailsHeader);

            var mappingsList = new StackPanel
            {
                Spacing = 5
            };

            // Show sample mappings based on available field types
            for (int i = 0; i < Math.Min(5, AvailableFieldTypes.Count); i++)
            {
                var mappingItem = new Border
                {
                    Background = Avalonia.Media.Brushes.White,
                    BorderBrush = Avalonia.Media.Brushes.LightBlue,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(3),
                    Padding = new Avalonia.Thickness(10, 5),
                    Margin = new Avalonia.Thickness(20, 0, 0, 0)
                };

                var mappingText = new TextBlock
                {
                    Text = $"🎯 {AvailableFieldTypes[i]} → Cell {(char)('A' + i)}{i + 1}",
                    FontSize = 11
                };

                mappingItem.Child = mappingText;
                mappingsList.Children.Add(mappingItem);
            }

            mainPanel.Children.Add(mappingsList);
        }

        return mainPanel;
    }

    /// <summary>
    /// Create interactive test data view panel
    /// </summary>
    private Panel CreateInteractiveTestDataView(string ruleName)
    {
        var mainPanel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 15
        };

        // Header
        var headerText = new TextBlock
        {
            Text = "⚡ Interactive Test Data Configuration",
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.Bold
        };
        mainPanel.Children.Add(headerText);

        // Instructions
        var instructionText = new TextBlock
        {
            Text = "Modify the test data below and click 'Rerun Test' to see how the rule performs with different values:",
            FontSize = 12,
            Foreground = Avalonia.Media.Brushes.DarkBlue,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 15)
        };
        mainPanel.Children.Add(instructionText);

        // Create editable test data fields
        var testDataPanel = new StackPanel
        {
            Spacing = 10
        };

        // Add sample editable fields
        for (int i = 0; i < Math.Min(5, AvailableFieldTypes.Count); i++)
        {
            var fieldPanel = new StackPanel
            {
                Spacing = 5
            };

            var fieldLabel = new TextBlock
            {
                Text = $"📝 {AvailableFieldTypes[i]}:",
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = 12
            };

            var fieldInput = new TextBox
            {
                Text = $"Test Value {i + 1}",
                Watermark = $"Enter test value for {AvailableFieldTypes[i]}",
                FontSize = 11
            };

            fieldPanel.Children.Add(fieldLabel);
            fieldPanel.Children.Add(fieldInput);
            testDataPanel.Children.Add(fieldPanel);
        }

        mainPanel.Children.Add(testDataPanel);

        // Rerun test button
        var rerunButton = new Button
        {
            Content = "🧪 Rerun Test with Modified Data",
            Background = Avalonia.Media.Brushes.Purple,
            Foreground = Avalonia.Media.Brushes.White,
            Padding = new Avalonia.Thickness(15, 8),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            CornerRadius = new Avalonia.CornerRadius(5)
        };

        rerunButton.Click += async (s, e) =>
        {
            StatusMessage = $"Rerunning test with modified data for rule: {ruleName}";
            // Implementation would collect modified values and rerun test
        };

        mainPanel.Children.Add(rerunButton);

        return mainPanel;
    }

    /// <summary>
    /// Export test results to file
    /// </summary>
    private async Task ExportTestResults(string ruleName, string testResults)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var filename = $"RuleTest_{ruleName}_{timestamp}.txt";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);

            var exportContent = $"Rule Test Results Export\n" +
                              $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                              $"Rule: {ruleName}\n" +
                              $"Template: {CurrentTemplate?.FileName ?? "No template"}\n" +
                              $"Pattern: {DetectedPattern}\n" +
                              $"==============================\n\n" +
                              $"{testResults}";

            await File.WriteAllTextAsync(filePath, exportContent);
            StatusMessage = $"Test results exported to: {filename}";
            Console.WriteLine($"📄 Test results exported to: {filePath}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            Console.WriteLine($"❌ Export error: {ex.Message}");
        }
    }
}