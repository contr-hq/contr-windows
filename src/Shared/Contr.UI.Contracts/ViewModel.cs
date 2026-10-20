using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Contr.UI.Contracts;

/// <summary>
/// Base class for view models: change notification plus a navigation and disposal lifecycle.
/// </summary>
public abstract class ViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Called when the page showing this view model is navigated to, with the navigation argument.
    /// </summary>
    /// <remarks>
    /// The navigation service calls it before the page loads, and only for an <see cref="IHasViewModel"/> page.
    /// </remarks>
    public virtual void OnNavigatedTo(params ReadOnlySpan<object> args) { }

    /// <summary>Called when the page showing this view model is navigated away from.</summary>
    public virtual void OnNavigatedFrom() { }

    /// <summary>Releases what the view model holds. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Override to release derived state; runs once.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing) { }
}
