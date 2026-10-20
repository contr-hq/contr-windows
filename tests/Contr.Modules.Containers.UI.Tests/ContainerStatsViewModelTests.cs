using System;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace Contr.Modules.Containers.UI.Tests;

public sealed class ContainerStatsViewModelTests
{
    [Fact]
    public void Add_TurnsCountersIntoRates()
    {
        var viewModel = new ContainerStatsViewModel(Substitute.For<ISender>());
        var start = DateTimeOffset.UnixEpoch;

        viewModel.Add(Sample(start, received: 1_000, written: 0));
        viewModel.Add(Sample(start.AddSeconds(2), received: 5_000, written: 8_000));

        Assert.Equal([2_000.0], viewModel.Received);
        Assert.Equal([4_000.0], viewModel.Written);
        Assert.Equal("↓ 2 kB/s · ↑ 0 B/s", viewModel.NetworkText);
        Assert.True(viewModel.HasSamples);
    }

    [Fact]
    public void Add_ReadsAResetCounterAsZero()
    {
        var viewModel = new ContainerStatsViewModel(Substitute.For<ISender>());
        var start = DateTimeOffset.UnixEpoch;

        viewModel.Add(Sample(start, received: 5_000, written: 0));
        viewModel.Add(Sample(start.AddSeconds(1), received: 10, written: 0));

        Assert.Equal([0.0], viewModel.Received);
    }

    private static ContainerStatsDto Sample(DateTimeOffset at, long received, long written)
    {
        return new ContainerStatsDto(at, 3.2, 142_000_000, 8_000_000_000, received, 0, 0, written, 4);
    }
}
