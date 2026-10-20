using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Networks.Application.Networks;
using Contr.Modules.Networks.Application.Networks.Commands.RemoveNetwork;
using Contr.Modules.Networks.Application.Networks.Queries.WatchNetworks;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Networks.UI.ViewModels;

/// <summary>The networks list and removal of user-defined networks.</summary>
public sealed partial class NetworksViewModel : LiveListViewModel<NetworkDto, NetworkRow>
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;

    public NetworksViewModel(ISender sender, IDialogService dialogs, INotificationService notifications)
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

    protected override Task<IAsyncEnumerable<ListChange<NetworkDto>>> OpenAsync(CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchNetworksQuery(), cancellationToken);
    }
}
