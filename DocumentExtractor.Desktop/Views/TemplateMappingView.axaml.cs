using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DocumentExtractor.Desktop.ViewModels;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Visual Template Mapping View - provides click-to-teach interface for field mapping
/// </summary>
public partial class TemplateMappingView : UserControl
{
    public TemplateMappingView()
    {
        InitializeComponent();
        Console.WriteLine("🗺️ TemplateMappingView initialized");
    }

    /// <summary>
    /// Handle template click events for field mapping
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if (DataContext is TemplateMappingViewModel viewModel)
        {
            // Set up template click handling
            // This will be enhanced with actual coordinate tracking
            Console.WriteLine("🎯 Template mapping view loaded and ready for interaction");
        }
    }

    /// <summary>
    /// Handle clicks on the template preview area
    /// </summary>
    private async void OnTemplateClicked(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (DataContext is TemplateMappingViewModel viewModel && sender is Border border)
            {
                // Get click position relative to the template
                var position = e.GetPosition(border);
                
                // Handle the template click
                await viewModel.HandleTemplateClick(position.X, position.Y);
                
                Console.WriteLine($"🎯 Template clicked at ({position.X:F0}, {position.Y:F0})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling template click: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle clicks on DataGrid cells for Excel mapping
    /// </summary>
    private void OnCellClicked(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        try
        {
            if (DataContext is TemplateMappingViewModel viewModel && 
                sender is DataGrid dataGrid)
            {
                var row = e.Row.Index;
                var column = e.Column.DisplayIndex;
                
                // Handle the cell selection
                viewModel.HandleCellSelection(row, column);
                
                Console.WriteLine($"📊 Excel cell clicked: Row {row}, Column {column}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling cell click: {ex.Message}");
        }
    }
}