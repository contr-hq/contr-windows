namespace Contr.UI.Contracts;

/// <summary>A page whose view model receives the navigation parameters.</summary>
public interface IHasViewModel
{
    /// <summary>The view model behind the page.</summary>
    ViewModel ViewModel { get; }
}
