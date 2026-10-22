using System;

namespace Contr.UI.Contracts.Services.Platform;

/// <summary>Where the app's accent colour comes from.</summary>
public enum AccentSource
{
    /// <summary>The accent the user chose in Windows.</summary>
    Windows = 0,

    /// <summary>Contr's own accent, from <c>Branding:Accent</c>. The default when configuration sets one.</summary>
    App = 1,
}

/// <summary>Reads, applies and persists the accent choice.</summary>
public interface IAccentService : IUiContractService, ISingletonDependency
{
    /// <summary>The currently applied source.</summary>
    AccentSource Accent { get; }

    /// <summary>Whether configuration declares an accent; without one the setting has a single choice.</summary>
    bool HasAppAccent { get; }

    /// <summary>Emits the new source whenever it changes.</summary>
    IObservable<AccentSource> OnAccentChanged { get; }

    /// <summary>Loads the persisted choice and applies it. Call once, before the first window exists.</summary>
    void Initialize();

    /// <summary>Applies and persists the given source.</summary>
    void SetAccent(AccentSource source);
}
