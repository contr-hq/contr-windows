using System;
using Contr.UI.Common.Helpers;
using Contr.UI.Common.Icons;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Windows.UI.Text;
using WinUIEx;

namespace Contr.App.UI.Onboarding;

/// <summary>The first-run setup window; it closes when the user finishes.</summary>
public sealed partial class OnboardingWindow : WindowEx
{
    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(SetupTitleBar);
        WindowHelper.TrySetAppIcon(this);
        WindowHelper.FitToWorkArea(this);
        ViewModel.Finished += OnFinished;
        Root.Loaded += OnLoaded;
    }

    public static string WslCommand => OnboardingViewModel.InstallWslCommand;

    public static string TryCommand => OnboardingViewModel.TryCommand;

    public OnboardingViewModel ViewModel { get; }

    /// <summary>The command and its prompt, as a terminal shows them.</summary>
    public static string TryPrompt => $"PS> {OnboardingViewModel.TryCommand}";

    /// <summary>A setup check's icon, by what it checks.</summary>
    public static string CheckGlyph(string name)
    {
        return name switch {
            "Windows version" => ContrIcons.Desktop,
            "Virtualization" => ContrIcons.Processor,
            "Free disk space" => ContrIcons.Disk,
            _ => ContrIcons.Info,
        };
    }

    /// <summary>The step in progress stands out from the finished ones.</summary>
    public static FontWeight StepWeight(bool isDone)
    {
        return isDone ? FontWeights.Normal : FontWeights.SemiBold;
    }

    public static bool Not(bool value)
    {
        return !value;
    }

    public static Visibility WhenNot(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    public static bool Both(bool first, bool second)
    {
        return first && second;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The theme service themes the main window; this window takes the theme it shows.
        if (App.MainWindow.Content is FrameworkElement main)
        {
            Root.RequestedTheme = main.ActualTheme;
            TitleBarHelper.UpdateTitleBarAppearance(this, main.ActualTheme);
        }

        await ViewModel.StartAsync();
    }

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        await ViewModel.NextAsync();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        ViewModel.Back();
    }

    private async void OnRetryEngine(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryEngineAsync();
    }

    private void OnNotNow(object sender, RoutedEventArgs e)
    {
        ViewModel.Postpone();
    }

    private void OnCopyItInstructions(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyItInstructions();
    }

    private void OnCopyTryCommand(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyTryCommand();
    }

    private void OnFinished(object? sender, EventArgs e)
    {
        ViewModel.Finished -= OnFinished;
        App.Current.ShowMainWindow();
        Close();
    }
}
