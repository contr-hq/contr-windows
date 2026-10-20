using System;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Daemon;

namespace Contr.App.UI;

/// <summary>Backs the Kubernetes page, whose controls wait for a daemon that runs a cluster.</summary>
public sealed class KubernetesViewModel : ViewModel
{
    public KubernetesViewModel(IDaemonConnection daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        IsOffered = daemon.Identity?.Capabilities.Contains(DaemonCapability.Kubernetes) == true;
    }

    /// <summary>Whether the connected daemon says it can run a cluster.</summary>
    public bool IsOffered { get; }

    public bool IsNotOffered => !IsOffered;
}
