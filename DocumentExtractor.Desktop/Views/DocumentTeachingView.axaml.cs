using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DocumentExtractor.Desktop.Views;

/// <summary>
/// Code-behind for the Document Teaching View.
/// Handles document upload, drag-and-drop functionality, and teaching workflow interactions.
/// </summary>
public partial class DocumentTeachingView : UserControl
{
    public DocumentTeachingView()
    {
        InitializeComponent();
        Console.WriteLine("🎓 Document Teaching View initialized");
    }
}