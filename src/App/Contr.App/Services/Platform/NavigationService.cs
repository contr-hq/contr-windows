using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Contr.App.Infrastructure.Observability;
using Contr.UI.Contracts;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.Services.Platform;

/// <summary>
/// Shows pages in the shell's content <see cref="Frame"/>. Pages resolve from the container, so
/// navigation sets <see cref="Frame.Content"/> directly and keeps its own back stack. Each page resolves in its own
/// scope, which disposes the page's view model once another page shows.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Subject<NavigationEvent> _navigated = new();
    private readonly Dictionary<string, Type> _registry = new(StringComparer.Ordinal);
    private readonly Stack<NavigationEntry> _backStack = new();
    private Frame? _frame;
    private NavigationEntry? _current;

    /// <summary>The scope of the page showing.</summary>
    private IServiceScope? _scope;

    public NavigationService(IServiceScopeFactory scopes, IEnumerable<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(modules);
        _scopes = scopes;

        foreach (var module in modules)
        {
            foreach (var item in module.GetNavigationItems())
            {
                Register(item.PageType);
            }

            foreach (var pageType in module.GetPages())
            {
                Register(pageType);
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

        var scope = _scopes.CreateScope();

        if (scope.ServiceProvider.GetService(pageType) is not Page page)
        {
            scope.Dispose();

            return false;
        }

        using var activity = Telemetry.ActivitySource.StartActivity("Navigation.NavigateTo");
        activity?.SetTag("navigation.page", pageType.Name);

        if (_current is not null)
        {
            _backStack.Push(_current);
        }

        _current = new NavigationEntry(pageKey, args.ToArray());
        Show(_frame, page, _current.Parameters, scope);
        _navigated.OnNext(new NavigationEvent(pageKey, pageType, page, _current.Parameters));

        return true;
    }

    public void GoBack()
    {
        if (_frame is null || !_backStack.TryPop(out var entry))
        {
            return;
        }

        var scope = _scopes.CreateScope();

        if (scope.ServiceProvider.GetService(_registry[entry.PageKey]) is not Page page)
        {
            scope.Dispose();

            return;
        }

        _current = entry;
        Show(_frame, page, entry.Parameters, scope);
        _navigated.OnNext(new NavigationEvent(entry.PageKey, page.GetType(), page, entry.Parameters));
    }

    public void ClearBackStack()
    {
        _backStack.Clear();
    }

    /// <summary>Hands the parameters to the page's view model before the page loads, then disposes the page it
    /// replaced with its view model; work that page started goes on where the daemon owns it.</summary>
    private void Show(Frame frame, Page page, object[] parameters, IServiceScope scope)
    {
        if (frame.Content is IHasViewModel previous)
        {
            previous.ViewModel.OnNavigatedFrom();
        }

        (page as IHasViewModel)?.ViewModel.OnNavigatedTo(parameters);
        frame.Content = page;

        var replaced = _scope;
        _scope = scope;
        replaced?.Dispose();
    }

    private sealed record NavigationEntry(string PageKey, object[] Parameters);
}
