using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Contracts.Services.Daemon;

namespace Contr.App.UI;

/// <summary>A WSL distribution with a switch for its engine access, as setup and Settings list it.</summary>
public sealed partial class WslDistributionItem : ObservableObject
{
    private bool _isQuiet;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Detail), nameof(CanChange), nameof(DefaultText))]
    public partial WslDistribution Distribution { get; set; }

    /// <summary>Whether engine access is wanted; it can differ from <see cref="Distribution"/> until applied.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    public WslDistributionItem(WslDistribution distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        Distribution = distribution;
        IsOn = IsIntegrated(distribution);
    }

    /// <summary>Raised when the user flips <see cref="IsOn"/>, not when the daemon's answer sets it.</summary>
    public event EventHandler? Switched;

    public string Name => Distribution.Name;

    /// <summary>Only a WSL 2 distribution can reach the engine.</summary>
    public bool CanChange => Distribution.Version >= 2;

    /// <summary>Such as <c>· default</c> after the name of the distribution <c>wsl.exe</c> starts.</summary>
    public string DefaultText => Distribution.IsDefault ? "· default" : string.Empty;

    public string Detail => Distribution switch {
        { Version: < 2 } => "WSL 1 distro · integration needs WSL 2",
        { State: WslIntegrationState.Active } => "docker in this distro reaches Contr's engine",
        { State: WslIntegrationState.Pending } => "On · waiting for the engine or for the distro to start",
        { State: WslIntegrationState.Failed, Failure.Length: > 0 } failed => $"Not connected: {failed.Failure}",
        { State: WslIntegrationState.Failed } => "Not connected yet · Contr tries again",
        _ => "Off",
    };

    /// <summary>The default distribution first, then by name.</summary>
    public static IEnumerable<WslDistribution> Ordered(IEnumerable<WslDistribution> distributions)
    {
        return distributions
            .OrderByDescending(distribution => distribution.IsDefault)
            .ThenBy(distribution => distribution.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether the daemon has engine access on for <paramref name="distribution"/>, working or not.</summary>
    public static bool IsIntegrated(WslDistribution distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        return distribution.State != WslIntegrationState.Disabled;
    }

    /// <summary>Shows the daemon's answer, and the switch as it now stands.</summary>
    public void Show(WslDistribution distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        Distribution = distribution;
        SetQuietly(IsIntegrated(distribution));
    }

    /// <summary>Sets the switch without raising <see cref="Switched"/>.</summary>
    public void SetQuietly(bool isOn)
    {
        _isQuiet = true;
        IsOn = isOn;
        _isQuiet = false;
    }

    partial void OnIsOnChanged(bool value)
    {
        if (!_isQuiet)
        {
            Switched?.Invoke(this, EventArgs.Empty);
        }
    }
}
