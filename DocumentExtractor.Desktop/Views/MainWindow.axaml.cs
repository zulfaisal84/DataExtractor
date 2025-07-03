using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.ApplicationLifetimes;
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
}