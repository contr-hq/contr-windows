using System.Globalization;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>What the client tells the user once a fix ended.</summary>
public sealed record FixNotice(string Message, InfoBarSeverity Severity, string? Title);

/// <summary>A fix the daemon offers for a check, in the words the client shows for it.</summary>
public sealed record CheckFix(Remediation Remediation)
{
    public string ActionId => Remediation.ActionId;

    /// <summary>Windows marks an action that asks for administrator approval with a shield.</summary>
    public bool RequiresElevation => Remediation.RequiresElevation;

    /// <summary>The button's text, such as <c>Install WSL</c>.</summary>
    public string Label => ActionId switch {
        Remediation.InstallWslId => "Install WSL",
        _ => Sentence(ActionId),
    };

    /// <summary>What shows until the daemon reports the fix's first step.</summary>
    public string Running => ActionId switch {
        Remediation.InstallWslId => "Installing WSL",
        _ => "Running the fix",
    };

    /// <summary>What the fix did once it finished, such as <c>WSL is installed.</c></summary>
    public string Done => ActionId switch {
        Remediation.InstallWslId => "WSL is installed.",
        _ => "The fix finished.",
    };

    /// <summary>The title over why the fix failed.</summary>
    public string FailedTitle => ActionId switch {
        Remediation.InstallWslId => "WSL wasn't installed",
        _ => "The fix didn't finish",
    };

    /// <summary>What to tell the user about how the fix ended.</summary>
    public FixNotice Notice(Result<RemediationOutcome> result)
    {
        if (result.IsFailure)
        {
            var reason = result.Error.Code == IDiagnostics.ElevationDeclinedCode
                ? "Windows didn't get administrator approval, so nothing changed. Try again when you or your IT "
                    + "team can approve it."
                : result.Error.Message;

            return new FixNotice(reason, InfoBarSeverity.Error, FailedTitle);
        }

        return result.Value switch {
            { RestartRequired: true } => new FixNotice(
                $"{Done} Save your work, then restart when it suits you.",
                InfoBarSeverity.Informational,
                "Restart Windows to finish"),
            { SignOutRequired: true } => new FixNotice(
                $"{Done} Sign out of Windows and back in when it suits you.",
                InfoBarSeverity.Informational,
                "Sign out to finish"),
            _ => new FixNotice(Done, InfoBarSeverity.Success, null),
        };
    }

    /// <summary>An action id the client has no words for, as a label: <c>join-docker-group</c> reads
    /// <c>Join docker group</c>.</summary>
    private static string Sentence(string actionId)
    {
        var words = actionId.Replace('-', ' ');

        return words.Length == 0 ? words : char.ToUpper(words[0], CultureInfo.CurrentCulture) + words[1..];
    }
}
