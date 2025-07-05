using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.ApplicationLifetimes;
using DocumentExtractor.Desktop.ViewModels;
using System;

namespace DocumentExtractor.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Handle window closing event to prevent accidental closure
        this.Closing += MainWindow_Closing;
        
        // Set initial window state
        this.WindowState = WindowState.Normal;
        this.ShowInTaskbar = true;
        
        Console.WriteLine("🖥️  Desktop window initialized successfully");
        Console.WriteLine("🔗 Window should now be visible and persistent");
    }
    
    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        // Prevent accidental window closure - require explicit close button
        e.Cancel = true;
        this.WindowState = WindowState.Minimized;
        Console.WriteLine("⚠️  Window minimized instead of closed. Use 'Close App' button to exit.");
    }
    
    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
        Console.WriteLine("📋 Window minimized to taskbar");
    }
    
    private void CloseApplication(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("👋 Application closing by user request");
        
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }
    
    /// <summary>
    /// Handle tab selection changes to update AI Assistant context
    /// </summary>
    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TabControl tabControl && DataContext is MainWindowViewModel viewModel)
        {
            var selectedTab = tabControl.SelectedItem as TabItem;
            var tabHeader = selectedTab?.Header?.ToString() ?? "Unknown";
            
            // Extract context name from tab header
            var context = tabHeader switch
            {
                "🎓 Document Teaching" => "Document Teaching",
                "🗺️ Template Mapping" => "Template Mapping", 
                "📊 Dashboard" => "Dashboard",
                "📋 Documents" => "Documents",
                "📋 Templates" => "Templates",
                _ => "Dashboard"
            };
            
            // Update AI Assistant context
            viewModel.GlobalAI.UpdateContext(context);
            
            Console.WriteLine($"🔄 Tab changed to: {context}");
        }
    }
}