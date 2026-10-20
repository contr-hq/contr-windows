using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Contr.UI.Contracts.Services.Containers;

/// <summary>The running containers, for surfaces outside the Containers module such as the tray.</summary>
public interface IRunningContainers
{
    /// <summary>The running containers, then the list again after every change, while subscribed.</summary>
    /// <remarks>Emits on a background thread; an engine that is not running emits an empty list.</remarks>
    IObservable<IReadOnlyList<RunningContainer>> Watch();

    /// <summary>Stops a running container; failures go to the notification bar.</summary>
    /// <param name="id">The container's engine ID.</param>
    Task StopAsync(string id);
}

/// <summary>A running container as the tray lists it.</summary>
/// <param name="Id">The container's engine ID.</param>
/// <param name="Name">The container's name.</param>
/// <param name="Project">The Compose project that created it; empty for others.</param>
/// <param name="Port">Its first published port, such as <c>localhost:8080</c>; empty without one.</param>
/// <param name="Link">An http address for that port; absent when it is not TCP or there is none.</param>
public sealed record RunningContainer(string Id, string Name, string Project, string Port, Uri? Link);
