using System;
using Contr.UI.Common.Controls;
using Contr.UI.Common.Helpers;
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

    public OnboardingViewModel ViewModel { get; }

    public static Visibility WhenState(OnboardingStepState state, string shown)
    {
        return state.ToString() == shown ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility WhenNotDone(OnboardingStepState state)
    {
        return state == OnboardingStepState.Done ? Visibility.Collapsed : Visibility.Visible;
    }

    public static FontWeight TitleWeight(OnboardingStepState state)
    {
        return state == OnboardingStepState.Current ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public static StatusKind StepKind(bool isDone)
    {
        return isDone ? StatusKind.Good : StatusKind.Busy;
    }

    public static Visibility Not(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    public static bool Both(bool first, bool second)
    {
        return first && second;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The theme service themes the main window; this window takes the theme it shows.
        if (App.MainWindow.Content is FrameworkElement main)
        {
            Root.RequestedTheme = main.ActualTheme;
            TitleBarHelper.UpdateTitleBarAppearance(this, main.ActualTheme);
        }

        ViewModel.Start();
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

    private void OnCopyWslCommand(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyWslCommand();
    }

    private void OnFinished(object? sender, EventArgs e)
    {
        ViewModel.Finished -= OnFinished;
        App.Current.ShowMainWindow();
        Close();
    }
}
