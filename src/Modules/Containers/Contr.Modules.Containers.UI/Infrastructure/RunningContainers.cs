using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Commands.StopContainer;
using Contr.Modules.Containers.Application.Containers.Queries.WatchContainers;
using Contr.Modules.Containers.Domain;
using Contr.Modules.Containers.UI.ViewModels;
using Contr.UI.Contracts.Services.Containers;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>The <see cref="IRunningContainers"/> the Containers module offers to the shell.</summary>
public sealed class RunningContainers : IRunningContainers
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(5);

    private readonly ISender _sender;
    private readonly INotificationService _notifications;

    public RunningContainers(ISender sender, INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(notifications);
        _sender = sender;
        _notifications = notifications;
    }

    public IObservable<IReadOnlyList<RunningContainer>> Watch()
    {
        return Observable.Create<IReadOnlyList<RunningContainer>>(FollowAsync);
    }

    public async Task StopAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var result = await _sender.Send(new StopContainerCommand(new ContainerId(id), ContainerState.Running));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Could not stop the container");
        }
    }

    /// <summary>The running ones among <paramref name="containers"/>, by name.</summary>
    public static IReadOnlyList<RunningContainer> Running(IEnumerable<ContainerDto> containers)
    {
        ArgumentNullException.ThrowIfNull(containers);

        return [.. containers
            .Where(container => container.State == ContainerState.Running)
            .OrderBy(container => container.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToRunning)];
    }

    private static RunningContainer ToRunning(ContainerDto container)
    {
        var port = PortLink
            .From(container.Ports)
            .FirstOrDefault();

        return new RunningContainer(
            container.Id.Value,
            container.Name,
            container.ComposeProject,
            port?.Address ?? string.Empty,
            port?.Uri);
    }

    private async Task FollowAsync(
        IObserver<IReadOnlyList<RunningContainer>> observer, CancellationToken cancellationToken)
    {
        var containers = new Dictionary<string, ContainerDto>(StringComparer.Ordinal);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var changes = await _sender
                    .Send(new WatchContainersQuery(), cancellationToken)
                    .ConfigureAwait(false);
                await foreach (var change in changes.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    Apply(containers, change);
                    observer.OnNext(Running(containers.Values));
                }

                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static void Apply(Dictionary<string, ContainerDto> containers, ListChange<ContainerDto> change)
    {
        switch (change)
        {
            case ListChange<ContainerDto>.Snapshot snapshot:
                containers.Clear();
                foreach (var container in snapshot.Items)
                {
                    containers[container.Id.Value] = container;
                }

                break;
            case ListChange<ContainerDto>.Upserted upserted:
                containers[upserted.Item.Id.Value] = upserted.Item;
                break;
            case ListChange<ContainerDto>.Removed removed:
                containers.Remove(removed.Key);
                break;
            case ListChange<ContainerDto>.Failed:
                containers.Clear();
                break;
        }
    }
}
