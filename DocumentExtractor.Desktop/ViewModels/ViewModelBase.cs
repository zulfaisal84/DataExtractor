using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DocumentExtractor.Desktop.ViewModels;

/// <summary>
/// Base class for all view models in the application.
/// Provides common functionality and proper disposal patterns.
/// </summary>
public class ViewModelBase : ObservableObject, IDisposable
{
    private bool _disposed = false;

    /// <summary>
    /// Virtual dispose method for cleanup in derived classes.
    /// </summary>
    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
