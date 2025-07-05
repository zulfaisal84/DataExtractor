using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DocumentExtractor.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Field Mapping Dialog.
/// Handles field selection, validation, and mapping configuration.
/// </summary>
public partial class FieldMappingDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _cellReference = "";

    [ObservableProperty]
    private string _cellValue = "";

    [ObservableProperty]
    private string _gridPosition = "";

    [ObservableProperty]
    private string? _selectedFieldType;

    [ObservableProperty]
    private string _customFieldName = "";

    [ObservableProperty]
    private bool _hasExistingMapping = false;

    [ObservableProperty]
    private string _validationMessage = "";

    /// <summary>
    /// Available predefined field types for mapping
    /// </summary>
    public ObservableCollection<string> AvailableFieldTypes { get; } = new()
    {
        "Total Amount",
        "Invoice Number",
        "Invoice Date", 
        "Due Date",
        "Company Name",
        "Company Address",
        "Account Number",
        "Reference Number",
        "Tax Amount",
        "Subtotal",
        "Discount Amount",
        "Payment Terms",
        "Currency",
        "Description",
        "Quantity",
        "Unit Price",
        "Line Total",
        "Contact Person",
        "Phone Number",
        "Email Address",
        "Service Period",
        "Meter Reading",
        "Usage Amount",
        "Previous Balance",
        "Payment Received",
        "Current Balance"
    };

    /// <summary>
    /// Whether a valid mapping can be saved
    /// </summary>
    public bool CanSaveMapping => 
        !string.IsNullOrWhiteSpace(SelectedFieldType) || 
        !string.IsNullOrWhiteSpace(CustomFieldName);

    /// <summary>
    /// Constructor for the field mapping dialog view model
    /// </summary>
    public FieldMappingDialogViewModel()
    {
        // Set up property change notifications for validation
        PropertyChanged += OnPropertyChanged;
    }

    /// <summary>
    /// Initialize the dialog with cell information
    /// </summary>
    public void Initialize(string cellReference, string cellValue, int row, int column, bool hasExistingMapping = false)
    {
        CellReference = cellReference;
        CellValue = string.IsNullOrWhiteSpace(cellValue) ? "(empty)" : cellValue;
        GridPosition = $"Row {row + 1}, Column {GetColumnLetter(column)}";
        HasExistingMapping = hasExistingMapping;

        Console.WriteLine($"🗺️ Field Mapping Dialog initialized for cell {cellReference}");
    }

    /// <summary>
    /// Initialize the dialog with existing mapping data
    /// </summary>
    public void InitializeWithExistingMapping(string cellReference, string cellValue, int row, int column, string existingFieldName)
    {
        Initialize(cellReference, cellValue, row, column, true);
        
        // Try to match existing field name to predefined types
        if (AvailableFieldTypes.Contains(existingFieldName))
        {
            SelectedFieldType = existingFieldName;
        }
        else
        {
            CustomFieldName = existingFieldName;
        }

        Console.WriteLine($"🔄 Field Mapping Dialog loaded with existing mapping: {existingFieldName}");
    }

    /// <summary>
    /// Update validation state
    /// </summary>
    public void UpdateValidation()
    {
        ValidationMessage = "";
        
        if (!CanSaveMapping)
        {
            ValidationMessage = "Please select a field type or enter a custom field name";
        }
        else if (!string.IsNullOrWhiteSpace(SelectedFieldType) && !string.IsNullOrWhiteSpace(CustomFieldName))
        {
            ValidationMessage = "Please choose either a predefined field OR enter a custom field name, not both";
        }

        // Notify UI that CanSaveMapping may have changed
        OnPropertyChanged(nameof(CanSaveMapping));
    }

    /// <summary>
    /// Handle property changes for validation updates
    /// </summary>
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedFieldType) || e.PropertyName == nameof(CustomFieldName))
        {
            UpdateValidation();
        }
    }

    /// <summary>
    /// Convert column index to Excel column letter(s)
    /// </summary>
    private static string GetColumnLetter(int columnIndex)
    {
        string columnLetter = "";
        while (columnIndex >= 0)
        {
            columnLetter = (char)('A' + (columnIndex % 26)) + columnLetter;
            columnIndex = (columnIndex / 26) - 1;
        }
        return columnLetter;
    }
}