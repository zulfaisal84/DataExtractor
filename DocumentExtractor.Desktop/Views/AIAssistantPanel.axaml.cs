using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using System;
using DocumentExtractor.Desktop.Services;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Code-behind for the AI Assistant Panel user control.
/// Handles keyboard shortcuts and UI interactions for the global AI assistant.
/// </summary>
public partial class AIAssistantPanel : UserControl
{
    private bool _isDragging = false;
    private Point _dragStartPoint;
    
    public AIAssistantPanel()
    {
        InitializeComponent();
        
        // Set up drag functionality
        SetupDragHandlers();
    }
    
    private void SetupDragHandlers()
    {
        if (DragHandle != null)
        {
            DragHandle.PointerPressed += OnDragHandlePressed;
            DragHandle.PointerMoved += OnDragHandleMoved;
            DragHandle.PointerReleased += OnDragHandleReleased;
        }
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
    
    #region Drag Functionality
    
    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            
            // Capture the pointer to continue receiving events even when outside the control
            e.Pointer.Capture(DragHandle);
            e.Handled = true;
        }
    }
    
    private void OnDragHandleMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && Parent is Grid parentGrid)
        {
            var currentPosition = e.GetPosition(parentGrid);
            var deltaX = currentPosition.X - _dragStartPoint.X;
            var deltaY = currentPosition.Y - _dragStartPoint.Y;
            
            // Update the panel position using margins
            var newMarginLeft = Math.Max(0, Math.Min(parentGrid.Bounds.Width - this.Width, deltaX));
            var newMarginTop = Math.Max(0, Math.Min(parentGrid.Bounds.Height - this.Height, deltaY));
            
            this.Margin = new Thickness(newMarginLeft, newMarginTop, 0, 0);
            
            e.Handled = true;
        }
    }
    
    private void OnDragHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
    
    #endregion
}