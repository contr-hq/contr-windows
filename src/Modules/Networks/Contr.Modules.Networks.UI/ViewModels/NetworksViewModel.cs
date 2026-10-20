using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Application.Networks.Commands.CreateNetwork;
using Contr.Modules.Networks.Application.Networks.Commands.PruneNetworks;
using Contr.Modules.Networks.Application.Networks.Commands.RemoveNetwork;
using Contr.Modules.Networks.Application.Networks.Queries.InspectNetwork;
using Contr.Modules.Networks.Application.Networks.Queries.WatchNetworks;
using Contr.UI.Common.Engine;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Networks.UI.ViewModels;

/// <summary>The networks list, a details panel with the connected containers, creation and pruning.</summary>
/// <remarks>Use it on the UI thread: the list and the details resume on the captured context.</remarks>
public sealed partial class NetworksViewModel : LiveListViewModel<NetworkDto, NetworkRow>
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private int _endpointsRequest;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial NetworkRow? Selected { get; set; }

    /// <summary>Such as <c>5 networks · 3 built in</c>.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; set; }

    [ObservableProperty]
    public partial string EndpointsNote { get; set; }

    public NetworksViewModel(
        ISender sender,
        IDialogService dialogs,
        INotificationService notifications,
        IDaemonConnection daemon)
        : base(
            new LiveCollection<NetworkDto, NetworkRow>(NetworkRow.From, row => row.Id, NetworkRow.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        _sender = sender;
        _dialogs = dialogs;
        _notifications = notifications;
        Engine = new EngineGateViewModel(daemon);
        Subtitle = string.Empty;
        EndpointsNote = string.Empty;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public EngineGateViewModel Engine { get; }

    public ObservableCollection<NetworkRow> Items { get; } = [];

    /// <summary>The running containers connected to the selected network.</summary>
    public ObservableCollection<NetworkEndpointDto> Endpoints { get; } = [];

    public bool HasSelection => Selected is not null;

    public void Activate()
    {
        Engine.Start();
        StartWatching();
    }

    public void Deactivate()
    {
        StopWatching();
        Engine.Stop();
    }

    public void Select(NetworkRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        Show(row.Id);
    }

    /// <summary>Asks for a name, then creates a bridge network.</summary>
    public async Task PromptAndCreateAsync()
    {
        var name = await _dialogs.ShowPromptAsync("Create network", "Name", string.Empty, "Create");

        if (name is null)
        {
            return;
        }

        var result = await _sender.Send(new CreateNetworkCommand(name));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Could not create the network");
        }
    }

    /// <summary>Asks first; the engine refuses while a container is connected.</summary>
    public async Task RemoveAsync(NetworkRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!await _dialogs.ShowConfirmAsync("Delete network", $"Delete {row.Name}?", "Delete", "Cancel"))
        {
            return;
        }

        var result = await _sender.Send(new RemoveNetworkCommand(row.NetworkId, row.Name));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, $"Could not delete {row.Name}");
        }
    }

    /// <summary>Deletes the user-defined networks no container is connected to, after asking.</summary>
    public async Task RemoveUnusedAsync()
    {
        var confirmed = await _dialogs.ShowConfirmAsync(
            "Remove unused networks",
            "Delete every network you or Compose created that no container is connected to?",
            "Delete",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        var result = await _sender.Send(new PruneNetworksCommand());

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Could not remove the networks");

            return;
        }

        var deleted = result.Value == 1 ? "1 network" : $"{Count(result.Value)} networks";
        _notifications.Show($"Deleted {deleted}.", InfoBarSeverity.Success);
    }

    protected override Task<IAsyncEnumerable<ListChange<NetworkDto>>> OpenAsync(CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchNetworksQuery(), cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Rows.CollectionChanged -= OnRowsChanged;
            Deactivate();
        }

        base.Dispose(disposing);
    }

    private static string Count(int count)
    {
        return count.ToString(CultureInfo.CurrentCulture);
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Show(Selected?.Id);
    }

    /// <summary>Rebuilds the rows with <paramref name="selectedId"/> selected, or the first network.</summary>
    private void Show(string? selectedId)
    {
        var previous = Selected?.Id;
        var selected = Rows.Any(row => row.Id == selectedId) ? selectedId : Rows.FirstOrDefault()?.Id;
        var items = Rows
            .Select(row => row with { IsSelected = row.Id == selected })
            .ToList();

        CollectionSync.Sync(Items, items, row => row.Id);
        Selected = items.FirstOrDefault(row => row.IsSelected);

        var builtIn = items.Count(row => row.IsBuiltIn);
        var networks = items.Count == 1 ? "1 network" : $"{Count(items.Count)} networks";
        Subtitle = items.Count == 0 ? string.Empty : $"{networks} · {Count(builtIn)} built in";

        if (Selected is { } row && (row.Id != previous || Endpoints.Count == 0))
        {
            _ = LoadEndpointsAsync(row);
        }
    }

    private async Task LoadEndpointsAsync(NetworkRow row)
    {
        var request = ++_endpointsRequest;
        var result = await _sender.Send(new GetNetworkEndpointsQuery(row.NetworkId));

        if (request != _endpointsRequest)
        {
            return;
        }

        Endpoints.Clear();

        if (result.IsFailure)
        {
            EndpointsNote = result.Error.Message;

            return;
        }

        foreach (var endpoint in result.Value)
        {
            Endpoints.Add(endpoint);
        }

        EndpointsNote = Endpoints.Count == 0 ? "No running container is connected." : string.Empty;
    }
}
