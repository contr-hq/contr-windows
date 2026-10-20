using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Application.Volumes.Commands.CreateVolume;
using Contr.Modules.Volumes.Application.Volumes.Commands.RemoveVolume;
using Contr.Modules.Volumes.Application.Volumes.Queries.WatchVolumes;
using Contr.UI.Common.Lists;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.Modules.Volumes.UI.ViewModels;

/// <summary>The volumes list, creating named volumes, and removal.</summary>
public sealed partial class VolumesViewModel : LiveListViewModel<VolumeDto, VolumeRow>
{
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;

    public VolumesViewModel(ISender sender, IDialogService dialogs, INotificationService notifications)
        : base(
            new LiveCollection<VolumeDto, VolumeRow>(VolumeRow.From, row => row.Name, VolumeRow.Compare),
            _retryDelay)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        _sender = sender;
        _dialogs = dialogs;
        _notifications = notifications;
    }

    /// <summary>Asks for a name, then creates the volume.</summary>
    public async Task PromptAndCreateAsync()
    {
        var name = await _dialogs.ShowPromptAsync("Create volume", "Name", string.Empty, "Create");

        if (name is not null)
        {
            await CreateAsync(name);
        }
    }

    public async Task CreateAsync(string name)
    {
        var result = await _sender.Send(new CreateVolumeCommand(name));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Could not create the volume");
        }
    }

    /// <summary>Asks first; deleting a volume deletes its data.</summary>
    public async Task RemoveAsync(VolumeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var confirmed = await _dialogs.ShowConfirmAsync(
            "Delete volume", $"Delete {row.Name} and the data in it?", "Delete", "Cancel");

        if (!confirmed)
        {
            return;
        }

        var result = await _sender.Send(new RemoveVolumeCommand(row.VolumeName));

        if (result.IsFailure)
        {
            _notifications.Show(result.Error.Message, InfoBarSeverity.Error, $"Could not delete {row.Name}");
        }
    }

    protected override Task<IAsyncEnumerable<ListChange<VolumeDto>>> OpenAsync(CancellationToken cancellationToken)
    {
        return _sender.Send(new WatchVolumesQuery(), cancellationToken);
    }
}
