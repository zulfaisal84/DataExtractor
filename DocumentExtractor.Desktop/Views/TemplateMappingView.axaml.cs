using System;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DocumentExtractor.Desktop.ViewModels;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Visual Template Mapping View - provides click-to-teach interface for field mapping using HTML preview
/// </summary>
public partial class TemplateMappingView : UserControl
{
    public TemplateMappingView()
    {
        InitializeComponent();
        Console.WriteLine("🗺️ TemplateMappingView initialized");
    }

    /// <summary>
    /// Handle template view loading and WebView setup
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if (DataContext is TemplateMappingViewModel viewModel)
        {
            // Set up WebView message handling
            SetupWebViewCommunication(viewModel);
            
            // Subscribe to HTML content changes
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TemplateMappingViewModel.HtmlPreviewContent) && 
                    !string.IsNullOrEmpty(viewModel.HtmlPreviewContent))
                {
                    LoadHtmlContent(viewModel.HtmlPreviewContent);
                }
            };
            
            Console.WriteLine("🎯 Template mapping view loaded and ready for interaction");
        }
    }

    /// <summary>
    /// Set up communication between WebView JavaScript and C# ViewModel
    /// </summary>
    private void SetupWebViewCommunication(TemplateMappingViewModel viewModel)
    {
        try
        {
            var webView = this.FindControl<WebView>("TemplateWebView");
            if (webView != null)
            {
                // Set up WebView navigation completed handler
                webView.NavigationCompleted += (sender, args) =>
                {
                    Console.WriteLine("🌐 WebView navigation completed");
                };

                // Set up message handler for JavaScript communication
                webView.WebViewMessageReceived += async (sender, args) =>
                {
                    try
                    {
                        var message = JsonSerializer.Deserialize<WebViewMessage>(args.Message);
                        
                        if (message?.Action == "cellClicked")
                        {
                            Console.WriteLine($"📊 HTML cell clicked: {message.CellRef} (Row {message.Row}, Col {message.Col})");
                            
                            // Call the existing cell selection handler
                            await viewModel.HandleHtmlCellClick(message.Row, message.Col, message.CellRef, message.CellValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error handling WebView message: {ex.Message}");
                    }
                };

                Console.WriteLine("🔗 WebView communication setup complete");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error setting up WebView communication: {ex.Message}");
        }
    }

    /// <summary>
    /// Load HTML content into the WebView
    /// </summary>
    private void LoadHtmlContent(string htmlContent)
    {
        try
        {
            var webView = this.FindControl<WebView>("TemplateWebView");
            if (webView != null && !string.IsNullOrEmpty(htmlContent))
            {
                webView.NavigateToString(htmlContent);
                Console.WriteLine($"🌐 HTML content loaded into WebView ({htmlContent.Length} characters)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading HTML content: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle clicks on the template preview area (fallback for non-Excel templates)
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
}

/// <summary>
/// Message structure for WebView JavaScript communication
/// </summary>
public class WebViewMessage
{
    public string Action { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }
    public string CellRef { get; set; } = "";
    public string CellValue { get; set; } = "";
}