using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DocumentExtractor.Desktop.Services;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Code-behind for the AI Assistant Panel user control.
/// Handles keyboard shortcuts and UI interactions for the global AI assistant.
/// </summary>
public partial class AIAssistantPanel : UserControl
{
    public AIAssistantPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handle Enter key press in message input to send message
    /// </summary>
    private async void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            
            // Get the DataContext which should be GlobalAIAssistantService
            if (DataContext is GlobalAIAssistantService aiService)
            {
                await aiService.SendMessageAsync(aiService.CurrentMessage);
            }
        }
    }
}