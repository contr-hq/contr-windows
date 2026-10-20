using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.App.UI;
using Contr.UI.Contracts.Services.Platform;
using Contr.UI.Contracts.Services.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml.Controls;
using NSubstitute;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class GlobalSearchTests
{
    private readonly RecordingNavigation _navigation = new();

    [Fact]
    public async Task Search_ListsEverySourcesHitsAndSkipsOneThatFails()
    {
        var containers = Source(Hit("shop-web", "ContainerDetail"));
        var broken = Substitute.For<ISearchSource>();
        broken
            .SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<SearchHit>>(new InvalidOperationException("down")));
        var volumes = Source(Hit("shop_pgdata", "Volumes"));

        var hits = await Create(containers, broken, volumes).SearchAsync("  shop ", CancellationToken.None);

        Assert.Equal(["shop-web", "shop_pgdata"], hits.Select(hit => hit.Title));
        await containers.Received(1).SearchAsync("shop", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_AsksNobodyForBlankText()
    {
        var source = Source(Hit("web", "Images"));

        Assert.Empty(await Create(source).SearchAsync("   ", CancellationToken.None));
        await source.DidNotReceiveWithAnyArgs().SearchAsync(default!, default);
    }

    [Fact]
    public void Open_ShowsTheHitsPageWithItsArguments()
    {
        Create().Open(new SearchHit("shop_pgdata", "Volume", "\uEDA2", "Volumes", ["shop_pgdata"]));

        var call = Assert.Single(_navigation.Calls);
        Assert.Equal("Volumes", call.PageKey);
        Assert.Equal(new object[] { "shop_pgdata" }, call.Arguments);
    }

    private static SearchHit Hit(string title, string pageKey)
    {
        return new SearchHit(title, string.Empty, "\uE721", pageKey, []);
    }

    private static ISearchSource Source(params SearchHit[] hits)
    {
        var source = Substitute.For<ISearchSource>();
        source
            .SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SearchHit>>(hits));

        return source;
    }

    private GlobalSearch Create(params ISearchSource[] sources)
    {
        return new GlobalSearch(sources, _navigation, NullLogger<GlobalSearch>.Instance);
    }

    /// <summary>Records navigation; a substitute cannot match the span its arguments arrive in.</summary>
    private sealed class RecordingNavigation : INavigationService
    {
        public List<(string PageKey, object[] Arguments)> Calls { get; } = [];

        public IObservable<NavigationEvent> OnNavigated => Observable.Empty<NavigationEvent>();

        public bool CanGoBack => false;

        public void Initialize(Frame frame)
        {
        }

        public void Register<TPage>()
            where TPage : Page
        {
        }

        public void Register(params ReadOnlySpan<Type> pageTypes)
        {
        }

        public string GetPageKey(Type pageType)
        {
            return pageType.Name;
        }

        public bool NavigateTo(string pageKey, params ReadOnlySpan<object> args)
        {
            Calls.Add((pageKey, args.ToArray()));

            return true;
        }

        public void GoBack()
        {
        }

        public void ClearBackStack()
        {
        }
    }
}
