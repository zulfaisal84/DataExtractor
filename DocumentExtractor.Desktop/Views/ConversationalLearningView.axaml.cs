using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DocumentExtractor.Desktop.ViewModels;
using System;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Code-behind for the Conversational Learning View.
/// Handles UI interactions for the AI chat interface.
/// </summary>
public partial class ConversationalLearningView : UserControl
{
    public ConversationalLearningView()
    {
        InitializeComponent();
        
        // Set up the view model if not already set
        if (DataContext == null)
        {
            DataContext = new ConversationalLearningViewModel();
        }
        
        Console.WriteLine("🤖 Conversational Learning View initialized");
    }
    
    /// <summary>
    /// Handle Enter key press in message text box to send messages.
    /// </summary>
    private void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ConversationalLearningViewModel viewModel)
        {
            if (viewModel.CanSendMessage && viewModel.SendMessageCommand.CanExecute(null))
            {
                viewModel.SendMessageCommand.Execute(null);
                e.Handled = true;
                
                // Scroll to bottom after sending message
                ScrollToBottom();
            }
        }
    }
    
    /// <summary>
    /// Scroll chat to bottom to show latest messages.
    /// </summary>
    private void ScrollToBottom()
    {
        try
        {
            if (this.FindControl<ScrollViewer>("ChatScrollViewer") is ScrollViewer scrollViewer)
            {
                // Schedule scroll to bottom after UI update
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    scrollViewer.ScrollToEnd();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scrolling chat: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Called when the view is attached to the visual tree.
    /// Used to initialize chat with welcome message.
    /// </summary>
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Initialize welcome message when view is first shown
        if (DataContext is ConversationalLearningViewModel viewModel)
        {
            viewModel.InitializeWelcomeMessage();
            ScrollToBottom();
        }
    }
}