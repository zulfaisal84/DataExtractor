using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DocumentExtractor.Desktop.ViewModels;
using System;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Floating chat window for undocked AI chatbox
/// </summary>
public partial class ChatWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Action _onDockBackCallback;
    private bool _isClosingProgrammatically = false;

    public ChatWindow(MainViewModel viewModel, Action onDockBackCallback)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _onDockBackCallback = onDockBackCallback;
        DataContext = viewModel;
        
        // Set window position relative to main window
        this.WindowStartupLocation = WindowStartupLocation.Manual;
        this.Position = new Avalonia.PixelPoint(100, 100);
        
        // Handle window closing
        this.Closing += OnWindowClosing;
        
        Console.WriteLine("✅ ChatWindow created and initialized");
    }

    private void OnDockBack(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("🔄 Docking chat back to main window");
        _isClosingProgrammatically = true;
        _onDockBackCallback?.Invoke();
        this.Close();
    }

    private async void OnSendMessage(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.SendMessage();
            
            // Auto-scroll to bottom after sending message
            if (ChatScrollViewer != null)
            {
                ChatScrollViewer.ScrollToEnd();
            }
        }
    }

    private async void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _viewModel != null)
        {
            e.Handled = true;
            await _viewModel.SendMessage();
            
            // Auto-scroll to bottom after sending message
            if (ChatScrollViewer != null)
            {
                ChatScrollViewer.ScrollToEnd();
            }
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Only dock back if user closed the window (not programmatically)
        if (!_isClosingProgrammatically)
        {
            Console.WriteLine("🔄 ChatWindow closing - docking back to main window");
            _onDockBackCallback?.Invoke();
        }
        else
        {
            Console.WriteLine("✅ ChatWindow closing programmatically - no callback needed");
        }
    }
}