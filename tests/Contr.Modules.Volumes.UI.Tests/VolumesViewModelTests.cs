using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Messaging;
using Contr.Application.Abstractions.Watching;
using Contr.Modules.Volumes.Application.Volumes;
using Contr.Modules.Volumes.Application.Volumes.Commands.CreateVolume;
using Contr.Modules.Volumes.Application.Volumes.Commands.RemoveVolume;
using Contr.Modules.Volumes.Application.Volumes.Queries.GetVolumeUsage;
using Contr.Modules.Volumes.Application.Volumes.Queries.WatchVolumes;
using Contr.Modules.Volumes.Domain;
using Contr.Modules.Volumes.UI.ViewModels;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
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
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IDaemonConnection _daemon = Substitute.For<IDaemonConnection>();

    public VolumesViewModelTests()
    {
        var running = new DaemonStatus(DaemonConnectionState.Connected, EngineState.Running, string.Empty);
        _daemon.Status.Returns(running);
        _daemon.StatusChanges.Returns(Observable.Return(running));
    }

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

        await CreateViewModel().RemoveAsync(new VolumeItem(Row("pgdata", string.Empty), [], IsSelected: false));

        await _dialogs.Received(1).ShowConfirmAsync(
            "Delete volume", "Delete pgdata and the data in it?", "Delete", "Cancel");
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<RemoveVolumeCommand>(), default);
    }

    [Fact]
    public void Activate_ShowsWhichContainersUseEachVolume()
    {
        IAsyncEnumerable<ListChange<VolumeDto>> volumes = TestStreams.Of<ListChange<VolumeDto>>(
            new ListChange<VolumeDto>.Snapshot([Dto("shop_pgdata", "shop"), Dto("cache", string.Empty)]));
        _sender
            .Send(Arg.Any<WatchVolumesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(volumes));
        IReadOnlyList<VolumeMountDto> mounts = [new VolumeMountDto("shop_pgdata", "db", "/var/lib/postgresql", true)];
        _sender
            .Send(Arg.Any<GetVolumeUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(mounts));
        var viewModel = CreateViewModel();

        viewModel.Activate();

        var usedBy = viewModel.Items
            .Select(item => item.UsedBy)
            .Order();

        Assert.Equal(["Not used", "db"], usedBy);
        Assert.Equal(["2 volumes", "1 not used by any container"], viewModel.SummaryPills.Select(pill => pill.Text));
        Assert.NotNull(viewModel.Selected);
    }

    [Fact]
    public void Activate_SelectsTheVolumeASearchAskedFor()
    {
        IAsyncEnumerable<ListChange<VolumeDto>> volumes = TestStreams.Of<ListChange<VolumeDto>>(
            new ListChange<VolumeDto>.Snapshot([Dto("cache", string.Empty), Dto("shop_pgdata", "shop")]));
        _sender
            .Send(Arg.Any<WatchVolumesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(volumes));
        _sender
            .Send(Arg.Any<GetVolumeUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<VolumeMountDto>>([]));
        var viewModel = CreateViewModel();

        viewModel.OnNavigatedTo("cache");
        viewModel.Activate();

        Assert.Equal("cache", viewModel.Selected?.Name);
    }

    [Fact]
    public void Compare_PutsComposeProjectsFirst()
    {
        Assert.True(VolumeRow.Compare(Row("shop_pgdata", "shop"), Row("cache", string.Empty)) < 0);
        Assert.True(VolumeRow.Compare(Row("a", string.Empty), Row("B", string.Empty)) < 0);
    }

    [Fact]
    public void From_NamesAnAnonymousVolume()
    {
        var row = Row(new string('a', 64), string.Empty);

        Assert.True(row.IsAnonymous);
        Assert.Equal("Anonymous volume", row.Subtitle);
        Assert.Equal("shop project", Row("shop_pgdata", "shop").Subtitle);
    }

    [Fact]
    public void Item_ExplainsWhyAVolumeInUseCannotBeDeleted()
    {
        var item = new VolumeItem(
            Row("pgdata", string.Empty),
            [new VolumeMountDto("pgdata", "db", "/data", true)],
            IsSelected: true);

        Assert.True(item.InUse);
        Assert.Equal("It's in use by db. Delete db before you delete this volume.", item.DeleteNote);
    }

    private static VolumeRow Row(string name, string project)
    {
        return VolumeRow.From(Dto(name, project));
    }

    private static VolumeDto Dto(string name, string project)
    {
        return new VolumeDto(
            new VolumeName(name),
            "local",
            "/var/lib/docker/volumes",
            null,
            project,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private VolumesViewModel CreateViewModel()
    {
        return new VolumesViewModel(_sender, _dialogs, _notifications, _clipboard, _daemon);
    }
}
