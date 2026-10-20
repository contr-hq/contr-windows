using Contr.UI.Common.Helpers;
using WinUIEx;

namespace Contr.App.UI;

/// <summary>Shown while the startup orchestrators run.</summary>
public sealed partial class SplashWindow : WindowEx
{
    public SplashWindow(SplashViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();

        this.CenterOnScreen();
        WindowHelper.TrySetAppIcon(this);
    }

    public SplashViewModel ViewModel { get; }
}
