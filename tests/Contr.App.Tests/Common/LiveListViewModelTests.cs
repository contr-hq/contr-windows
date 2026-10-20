using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Daemon;
using Contr.Application.Abstractions.Watching;
using Contr.UI.Common.Lists;
using Xunit;

namespace Contr.App.Tests.Common;

public sealed class LiveListViewModelTests
{
    [Fact]
    public async Task Watch_ShowsTheSnapshotThenTheFailure()
    {
        using var stop = new CancellationTokenSource();
        using var viewModel = new ScriptedViewModel(stop, [
            [
                new ListChange<string>.Snapshot(["b", "a"]),
                new ListChange<string>.Failed(DaemonErrors.EngineNotRunning),
            ],
        ]);

        await viewModel.WatchAsync(stop.Token);

        Assert.Equal(["a", "b"], viewModel.Rows);
        Assert.True(viewModel.IsLoaded);
        Assert.Equal(DaemonErrors.EngineNotRunning.Message, viewModel.ErrorMessage);
        Assert.True(viewModel.IsEngineDown);
    }

    [Fact]
    public async Task Watch_RecoversOnTheNextStream()
    {
        using var stop = new CancellationTokenSource();
        using var viewModel = new ScriptedViewModel(stop, [
            [new ListChange<string>.Failed(DaemonErrors.Unavailable)],
            [new ListChange<string>.Snapshot([])],
        ]);

        await viewModel.WatchAsync(stop.Token);

        Assert.False(viewModel.HasError);
        Assert.False(viewModel.IsEngineDown);
        Assert.True(viewModel.IsEmpty);
        Assert.Equal(3, viewModel.Opened);
    }

    /// <summary>Plays one scripted stream per watch, then stops the watch.</summary>
    private sealed partial class ScriptedViewModel : LiveListViewModel<string, string>
    {
        private readonly CancellationTokenSource _stop;
        private readonly IReadOnlyList<ListChange<string>[]> _streams;

        public ScriptedViewModel(CancellationTokenSource stop, IReadOnlyList<ListChange<string>[]> streams)
            : base(new LiveCollection<string, string>(item => item, row => row, string.CompareOrdinal), TimeSpan.Zero)
        {
            _stop = stop;
            _streams = streams;
        }

        public int Opened { get; private set; }

        protected override Task<IAsyncEnumerable<ListChange<string>>> OpenAsync(CancellationToken cancellationToken)
        {
            var index = Opened++;

            if (index >= _streams.Count)
            {
                _stop.Cancel();

                return Task.FromResult(Play([], cancellationToken));
            }

            return Task.FromResult(Play(_streams[index], cancellationToken));
        }

        private static async IAsyncEnumerable<ListChange<string>> Play(
            ListChange<string>[] changes, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var change in changes)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();

                yield return change;
            }
        }
    }
}
