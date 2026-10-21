using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;

namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>The user's own WSL distributions and their access to the engine.</summary>
/// <remarks>Offered when the daemon reports <see cref="DaemonCapability.WslIntegration"/>.</remarks>
public interface IWslDistributions
{
    /// <summary>The user's distributions, without Contr's own.</summary>
    /// <returns>A failure with code <c>Daemon.FeatureUnavailable</c> where the engine does not run in WSL.</returns>
    Task<Result<IReadOnlyList<WslDistribution>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Turns engine access from one distribution on or off.</summary>
    /// <remarks>The daemon keeps the choice across restarts.</remarks>
    /// <param name="name">The distribution's name.</param>
    /// <param name="isEnabled">Whether <c>docker</c> in the distribution reaches the engine.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The distribution afterwards; a failure for a WSL 1 or unregistered distribution.</returns>
    Task<Result<WslDistribution>> SetIntegrationAsync(
        string name, bool isEnabled, CancellationToken cancellationToken);
}

/// <summary>Where a distribution's engine access stands.</summary>
public enum WslIntegrationState
{
    /// <summary>Engine access is off.</summary>
    Disabled,

    /// <summary>Engine access is on and waits for the engine or for the distribution to start.</summary>
    Pending,

    /// <summary><c>docker</c> in the distribution reaches the engine.</summary>
    Active,

    /// <summary>Engine access is on, but the last attempt failed; the daemon retries.</summary>
    Failed,
}

/// <summary>One of the user's WSL distributions.</summary>
/// <param name="Name">Such as <c>Ubuntu-24.04</c>.</param>
/// <param name="Version">1 or 2; only version 2 can reach the engine.</param>
/// <param name="IsDefault">Whether <c>wsl.exe</c> starts it when none is named.</param>
/// <param name="State">Where its engine access stands.</param>
/// <param name="Failure">Why the last attempt failed; empty unless <paramref name="State"/> is failed.</param>
public sealed record WslDistribution(
    string Name, int Version, bool IsDefault, WslIntegrationState State, string Failure);
