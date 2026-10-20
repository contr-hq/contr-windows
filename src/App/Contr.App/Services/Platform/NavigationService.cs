using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Contr.App.Infrastructure.Observability;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.Services.Platform;

/// <summary>
/// Shows pages in the shell's content <see cref="Frame"/>. Pages resolve from the container, so
/// navigation sets <see cref="Frame.Content"/> directly and keeps its own back stack.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Subject<NavigationEvent> _navigated = new();
    private readonly Dictionary<string, Type> _registry = new(StringComparer.Ordinal);
    private readonly Stack<NavigationEntry> _backStack = new();
    private Frame? _frame;
    private NavigationEntry? _current;

    public NavigationService(IServiceProvider services, IEnumerable<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(modules);
        _services = services;

        foreach (var module in modules)
        {
            foreach (var item in module.GetNavigationItems())
            {
                Register(item.PageType);
            }
        }
    }

    public IObservable<NavigationEvent> OnNavigated => _navigated.AsObservable();

    public bool CanGoBack => _backStack.Count > 0;

    public void Initialize(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frame = frame;
    }

    public void Register<TPage>() where TPage : Page
    {
        Register(typeof(TPage));
    }

    public void Register(params ReadOnlySpan<Type> pageTypes)
    {
        foreach (var pageType in pageTypes)
        {
            _registry[GetPageKey(pageType)] = pageType;
        }
    }

    public string GetPageKey(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        const string suffix = "Page";
        string pageClassName = pageType.Name;

        if (!pageClassName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Page type name '{pageClassName}' must end with 'Page' to derive a navigation key.",
                nameof(pageType));
        }

        return pageClassName[..^suffix.Length];
    }

    public bool NavigateTo(string pageKey, params ReadOnlySpan<object> args)
    {
        if (_frame is null || !_registry.TryGetValue(pageKey, out var pageType))
        {
            return false;
        }

        if (_services.GetService(pageType) is not Page page)
        {
            return false;
        }

        using var activity = Telemetry.ActivitySource.StartActivity("Navigation.NavigateTo");
        activity?.SetTag("navigation.page", pageType.Name);

        if (_current is not null)
        {
            _backStack.Push(_current);
        }

        _current = new NavigationEntry(pageKey, args.ToArray());
        _frame.Content = page;
        _navigated.OnNext(new NavigationEvent(pageKey, pageType, page, _current.Parameters));

        return true;
    }

    public void GoBack()
    {
        if (_frame is null || !_backStack.TryPop(out var entry))
        {
            return;
        }

        if (_services.GetService(_registry[entry.PageKey]) is not Page page)
        {
            return;
        }

        _current = entry;
        _frame.Content = page;
        _navigated.OnNext(new NavigationEvent(entry.PageKey, page.GetType(), page, entry.Parameters));
    }

    public void ClearBackStack()
    {
        _backStack.Clear();
    }

    private sealed record NavigationEntry(string PageKey, object[] Parameters);
}
