using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.UI.Common.Formatting;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>
/// Backs Settings' Updates tab: what the daemon's update check found, and the settings that decide whether and for
/// what it checks.
/// </summary>
public sealed partial class UpdatesSettingsViewModel : ObservableObject
{
    /// <summary>The first API revision with the daemon's update check.</summary>
    private const uint _updatesRevision = 21;

    private const int _stableIndex = 0;
    private const int _prereleasesIndex = 1;

    private readonly IDaemonSettings _settings;
    private readonly IDaemonUpdates _updates;
    private readonly IDaemonConnection _daemon;
    private readonly INotificationService _notifications;

    /// <summary>The settings the daemon keeps; absent while it keeps none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Checks), nameof(ChannelIndex), nameof(IsCheckLocked), nameof(IsChannelLocked))]
    [NotifyPropertyChangedFor(nameof(CanChooseCheck), nameof(CanChooseChannel), nameof(CanCheckNow))]
    [NotifyPropertyChangedFor(nameof(CheckedText))]
    public partial DaemonSettings? Saved { get; set; }

    /// <summary>What the latest check found; absent until the daemon said.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailable), nameof(AvailableTitle), nameof(AvailableDetail))]
    [NotifyPropertyChangedFor(nameof(CurrentVersion), nameof(WhatsNewText), nameof(ReleasePage))]
    [NotifyPropertyChangedFor(nameof(HasReleasePage), nameof(CheckedText), nameof(AvailableVersion))]
    public partial UpdateStatus? Status { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseCheck), nameof(CanChooseChannel), nameof(CanCheckNow))]
    [NotifyPropertyChangedFor(nameof(CheckedText))]
    public partial bool IsBusy { get; set; }

    /// <summary>Whether the daemon checks for updates; it does from API revision 21.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LacksOffer), nameof(CanChooseCheck), nameof(CanChooseChannel))]
    [NotifyPropertyChangedFor(nameof(CanCheckNow))]
    public partial bool IsOffered { get; set; }

    public UpdatesSettingsViewModel(
        IDaemonSettings settings,
        IDaemonUpdates updates,
        IDaemonConnection daemon,
        INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(notifications);
        _settings = settings;
        _updates = updates;
        _daemon = daemon;
        _notifications = notifications;
    }

    public bool LacksOffer => !IsOffered;

    /// <summary>Whether the daemon checks once a day and on request.</summary>
    public bool Checks => Saved?.Updates.Check == true;

    /// <summary>0 counts stable releases, 1 prereleases too.</summary>
    public int ChannelIndex => Saved?.Updates.Prereleases == true ? _prereleasesIndex : _stableIndex;

    public bool IsCheckLocked => Saved?.IsLocked(DaemonSetting.UpdatesCheck) == true;

    public bool IsChannelLocked => Saved?.IsLocked(DaemonSetting.UpdatesPrereleases) == true;

    public bool CanChooseCheck => IsOffered && Saved is not null && !IsBusy && !IsCheckLocked;

    public bool CanChooseChannel => IsOffered && Saved is not null && !IsBusy && !IsChannelLocked;

    /// <summary>The daemon refuses a check while checking is off.</summary>
    public bool CanCheckNow => IsOffered && Checks && !IsBusy;

    public bool HasAvailable => Status?.Available is not null;

    /// <summary>Such as <c>0.2.0</c>; empty without a newer release.</summary>
    public string AvailableVersion => Status?.Available?.Version ?? string.Empty;

    /// <summary>Such as <c>Contr 0.2.0 is available</c>.</summary>
    public string AvailableTitle => HasAvailable ? $"Contr {AvailableVersion} is available" : string.Empty;

    /// <summary>Such as <c>Published 3d ago · you have </c>, before <see cref="CurrentVersion"/>.</summary>
    public string AvailableDetail => Status?.Available?.PublishedAt is { } published
        ? $"Published {HumanFormat.Ago(published, DateTimeOffset.Now)} · you have "
        : "You have ";

    /// <summary>The running daemon's version, such as <c>0.1.0-alpha.2</c>.</summary>
    public string CurrentVersion => Status?.CurrentVersion ?? string.Empty;

    /// <summary>Such as <c>What's new in 0.2.0</c>.</summary>
    public string WhatsNewText => HasAvailable ? $"What's new in {AvailableVersion}" : string.Empty;

    public Uri? ReleasePage => Status?.Available?.Url;

    public bool HasReleasePage => ReleasePage is not null;

    /// <summary>Such as <c>Checked 2h ago</c>, or why the last check failed.</summary>
    public string CheckedText => (IsBusy, Checks, Status) switch {
        (true, _, _) => "Checking…",
        (_, false, _) => "Turn on the daily check to check now.",
        (_, _, { Error.Length: > 0 } status) => $"The last check failed: {status.Error}",
        (_, _, { CheckedAt: { } at }) when DateTimeOffset.Now - at < TimeSpan.FromMinutes(1) => "Checked just now",
        (_, _, { CheckedAt: { } at }) => $"Checked {HumanFormat.Ago(at, DateTimeOffset.Now)}",
        _ => "Not checked yet.",
    };

    /// <summary>Reads the latest check's result once the daemon offers checks.</summary>
    public async Task ActivateAsync()
    {
        IsOffered = _daemon.Identity?.ApiRevision >= _updatesRevision;

        if (!IsOffered)
        {
            return;
        }

        var status = await _updates.GetStatusAsync(CancellationToken.None);

        if (status.IsSuccess)
        {
            Status = status.Value;
        }
    }

    public void Show(DaemonSettings? settings)
    {
        Saved = settings;
    }

    public async Task CheckNowAsync()
    {
        if (!CanCheckNow)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _updates.CheckAsync(CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(result.Error.Message, InfoBarSeverity.Error, "Contr couldn't check for updates");

                return;
            }

            Status = result.Value;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task ChooseCheckAsync(bool check)
    {
        return check == Checks ? Task.CompletedTask : UpdateAsync(new DaemonSettingsEdit { UpdatesCheck = check });
    }

    public Task ChooseChannelAsync(int index)
    {
        return index == ChannelIndex
            ? Task.CompletedTask
            : UpdateAsync(new DaemonSettingsEdit { UpdatesPrereleases = index == _prereleasesIndex });
    }

    private async Task UpdateAsync(DaemonSettingsEdit edit)
    {
        IsBusy = true;
        try
        {
            var result = await _settings.UpdateAsync(edit, CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(
                    result.Error.Code == IDaemonSettings.SettingLockedCode
                        ? "Your organization manages this setting."
                        : result.Error.Message,
                    InfoBarSeverity.Error,
                    "The update settings didn't change");

                return;
            }

            Saved = result.Value.Settings;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
