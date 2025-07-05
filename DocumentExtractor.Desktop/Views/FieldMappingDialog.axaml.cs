using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DocumentExtractor.Desktop.ViewModels;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Dialog for mapping template fields to document data fields.
/// Provides user interface for click-to-teach field mapping.
/// </summary>
public partial class FieldMappingDialog : Window
{
    /// <summary>
    /// Result of the dialog operation
    /// </summary>
    public FieldMappingResult? Result { get; private set; }

    public FieldMappingDialog()
    {
        InitializeComponent();
        SetupEventHandlers();
        Console.WriteLine("🗺️ Field Mapping Dialog initialized");
    }

    /// <summary>
    /// Set up event handlers for dialog buttons
    /// </summary>
    private void SetupEventHandlers()
    {
        // Save button click
        if (SaveButton != null)
        {
            SaveButton.Click += OnSaveClicked;
        }

        // Cancel button click
        if (CancelButton != null)
        {
            CancelButton.Click += OnCancelClicked;
        }

        // Clear button click
        if (ClearButton != null)
        {
            ClearButton.Click += OnClearClicked;
        }

        // Field type selection change
        if (FieldTypeComboBox != null)
        {
            FieldTypeComboBox.SelectionChanged += OnFieldTypeChanged;
        }

        // Custom field text change
        if (CustomFieldTextBox != null)
        {
            CustomFieldTextBox.TextChanged += OnCustomFieldChanged;
        }
    }

    /// <summary>
    /// Handle save button click - create field mapping
    /// </summary>
    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is FieldMappingDialogViewModel viewModel)
            {
                var fieldName = !string.IsNullOrWhiteSpace(viewModel.CustomFieldName) 
                    ? viewModel.CustomFieldName 
                    : viewModel.SelectedFieldType;

                if (!string.IsNullOrWhiteSpace(fieldName))
                {
                    Result = new FieldMappingResult
                    {
                        IsSuccess = true,
                        FieldName = fieldName,
                        CellReference = viewModel.CellReference,
                        IsCustomField = !string.IsNullOrWhiteSpace(viewModel.CustomFieldName)
                    };

                    Console.WriteLine($"💾 Field mapping saved: {fieldName} → {viewModel.CellReference}");
                    Close(Result);
                }
                else
                {
                    // Show validation message
                    viewModel.ValidationMessage = "Please select a field type or enter a custom field name";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error saving field mapping: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle cancel button click - close without saving
    /// </summary>
    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = new FieldMappingResult { IsSuccess = false };
        Console.WriteLine("❌ Field mapping canceled");
        Close(Result);
    }

    /// <summary>
    /// Handle clear button click - remove existing mapping
    /// </summary>
    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is FieldMappingDialogViewModel viewModel)
            {
                Result = new FieldMappingResult
                {
                    IsSuccess = true,
                    IsClearOperation = true,
                    CellReference = viewModel.CellReference
                };

                Console.WriteLine($"🗑️ Field mapping cleared for {viewModel.CellReference}");
                Close(Result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error clearing field mapping: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle field type selection change
    /// </summary>
    private void OnFieldTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is FieldMappingDialogViewModel viewModel && 
            CustomFieldTextBox != null)
        {
            // Clear custom field when predefined field is selected
            if (!string.IsNullOrWhiteSpace(viewModel.SelectedFieldType))
            {
                viewModel.CustomFieldName = "";
            }
            
            // Update validation
            viewModel.UpdateValidation();
        }
    }

    /// <summary>
    /// Handle custom field text change
    /// </summary>
    private void OnCustomFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is FieldMappingDialogViewModel viewModel && 
            FieldTypeComboBox != null)
        {
            // Clear predefined selection when custom field is entered
            if (!string.IsNullOrWhiteSpace(viewModel.CustomFieldName))
            {
                viewModel.SelectedFieldType = null;
            }
            
            // Update validation
            viewModel.UpdateValidation();
        }
    }
}

/// <summary>
/// Result data from the field mapping dialog
/// </summary>
public class FieldMappingResult
{
    public bool IsSuccess { get; set; }
    public string FieldName { get; set; } = "";
    public string CellReference { get; set; } = "";
    public bool IsCustomField { get; set; }
    public bool IsClearOperation { get; set; }
}