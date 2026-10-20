using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Application.Volumes.Commands.CreateVolume;
using Contr.Modules.Volumes.Application.Volumes.Commands.RemoveVolume;
using Contr.Modules.Volumes.Domain;
using Contr.Modules.Volumes.UI.ViewModels;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Volumes.UI.Tests;

public sealed class VolumesViewModelTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    [Fact]
    public async Task Create_ShowsWhyTheNameIsRefused()
    {
        _sender
            .Send(Arg.Is<CreateVolumeCommand>(command => command != null), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Volumes.Name.Invalid", "bad name")));

        await CreateViewModel().CreateAsync("-x");

        _notifications.Received(1).Show("bad name", InfoBarSeverity.Error, "Could not create the volume");
    }

    [Fact]
    public async Task Remove_AsksBeforeDeletingData()
    {
        _dialogs.ShowConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        await CreateViewModel().RemoveAsync(Row("pgdata", string.Empty));

        await _dialogs.Received(1).ShowConfirmAsync(
            "Delete volume", "Delete pgdata and the data in it?", "Delete", "Cancel");
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<RemoveVolumeCommand>(), default);
    }

    [Fact]
    public void Compare_PutsComposeProjectsFirst()
    {
        Assert.True(VolumeRow.Compare(Row("shop_pgdata", "shop"), Row("cache", string.Empty)) < 0);
        Assert.True(VolumeRow.Compare(Row("a", string.Empty), Row("B", string.Empty)) < 0);
    }

    [Fact]
    public void From_LeavesAnUnknownCreationTimeEmpty()
    {
        Assert.Equal(string.Empty, Row("cache", string.Empty).Created);
    }

    private static VolumeRow Row(string name, string project)
    {
        return VolumeRow.From(new VolumeDto(new VolumeName(name), "local", "/var/lib/docker/volumes", null, project));
    }

    private VolumesViewModel CreateViewModel()
    {
        return new VolumesViewModel(_sender, _dialogs, _notifications);
    }
}
