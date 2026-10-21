using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Contr.UI.Common.Lists;

/// <summary>
/// Kakehashi's five-row pages for a list inside a card: the rows of the current page, the card's caption with the
/// range shown, and the pager's state.
/// </summary>
/// <typeparam name="TItem">The listed item.</typeparam>
public sealed partial class Paging<TItem> : ObservableObject
{
    /// <summary>Rows per page.</summary>
    public const int PageSize = 5;

    private readonly string _caption;
    private readonly Func<TItem, string> _key;
    private IReadOnlyList<TItem> _all = [];

    /// <summary>The page shown, from 1.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText), nameof(IsLastPage))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand), nameof(NextCommand))]
    public partial int Page { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText), nameof(HasPages), nameof(IsLastPage))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand), nameof(NextCommand))]
    public partial int PageCount { get; set; }

    /// <summary>Such as <c>IMAGES · 1–5 OF 9</c>, or <c>NETWORKS · 5</c> when one page holds them all.</summary>
    [ObservableProperty]
    public partial string Caption { get; set; }

    /// <param name="caption">The card's caption in upper case, such as <c>IMAGES</c>.</param>
    /// <param name="key">What a row is kept by across updates.</param>
    public Paging(string caption, Func<TItem, string> key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caption);
        ArgumentNullException.ThrowIfNull(key);
        _caption = caption;
        _key = key;
        Page = 1;
        PageCount = 1;
        Caption = caption;
    }

    /// <summary>The rows of the current page.</summary>
    public ObservableCollection<TItem> Items { get; } = [];

    /// <summary>Whether there is a second page; the pager shows only then.</summary>
    public bool HasPages => PageCount > 1;

    /// <summary>Where a row that ends the list, such as a summary, belongs.</summary>
    public bool IsLastPage => Page == PageCount;

    /// <summary>Such as <c>1 / 2</c>.</summary>
    public string PageText => $"{Number(Page)} / {Number(PageCount)}";

    /// <summary>Shows <paramref name="all"/> on the same page, or on the last one when fewer pages remain.</summary>
    public void Show(IReadOnlyList<TItem> all)
    {
        ArgumentNullException.ThrowIfNull(all);
        _all = all;
        PageCount = Math.Max(1, (all.Count + PageSize - 1) / PageSize);
        Page = Math.Clamp(Page, 1, PageCount);
        Fill();
    }

    /// <summary>Turns to the page that holds <paramref name="item"/>.</summary>
    public void Reveal(TItem item)
    {
        var index = _all
            .Select((candidate, position) => (candidate, position))
            .FirstOrDefault(pair => _key(pair.candidate) == _key(item), (default!, -1))
            .position;

        if (index >= 0)
        {
            Page = (index / PageSize) + 1;
            Fill();
        }
    }

    private static string Number(int value)
    {
        return value.ToString(CultureInfo.CurrentCulture);
    }

    private bool CanGoBack()
    {
        return Page > 1;
    }

    private bool CanGoOn()
    {
        return Page < PageCount;
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Previous()
    {
        Page--;
        Fill();
    }

    [RelayCommand(CanExecute = nameof(CanGoOn))]
    private void Next()
    {
        Page++;
        Fill();
    }

    private void Fill()
    {
        var first = (Page - 1) * PageSize;
        var shown = _all
            .Skip(first)
            .Take(PageSize)
            .ToList();
        CollectionSync.Sync(Items, shown, _key);
        Caption = _all.Count <= PageSize
            ? $"{_caption} · {Number(_all.Count)}"
            : $"{_caption} · {Number(first + 1)}–{Number(first + shown.Count)} OF {Number(_all.Count)}";
    }
}
