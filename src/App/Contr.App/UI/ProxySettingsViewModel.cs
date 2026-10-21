using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Contr.UI.Contracts.Services.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Contr.App.UI;

/// <summary>
/// Backs Settings' Proxies and certificates tab: edits of the daemon's proxy, certificate and registry mirror
/// settings, kept apart from the saved ones until the user applies them.
/// </summary>
public sealed partial class ProxySettingsViewModel : ObservableObject
{
    /// <summary>The first API revision with <see cref="DaemonSetting.PassProxyToContainers"/>.</summary>
    private const uint _passToContainersRevision = 14;

    private const int _manualIndex = 1;
    private const int _noneIndex = 2;

    private readonly IDaemonSettings _settings;
    private readonly IDaemonConnection _daemon;
    private readonly INotificationService _notifications;
    private readonly DispatcherQueue? _dispatcher;
    private IDisposable? _status;

    /// <summary>0 uses Windows' proxy, 1 the servers below, 2 none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManual), nameof(CanEditHttp), nameof(CanEditHttps), nameof(CanEditExclude))]
    [NotifyPropertyChangedFor(nameof(ChangeCount), nameof(HasChanges), nameof(ChangesText), nameof(CanApply))]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeCount), nameof(HasChanges), nameof(ChangesText), nameof(CanApply))]
    public partial string Http { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeCount), nameof(HasChanges), nameof(ChangesText), nameof(CanApply))]
    public partial string Https { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeCount), nameof(HasChanges), nameof(ChangesText), nameof(CanApply))]
    public partial string Exclude { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeCount), nameof(HasChanges), nameof(ChangesText), nameof(CanApply))]
    public partial bool PassesToContainers { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeCount), nameof(HasChanges), nameof(ChangesText), nameof(CanApply))]
    public partial bool TrustsSystemCertificates { get; set; }

    /// <summary>The first of the daemon's registry mirrors, the one the design edits.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeCount), nameof(HasChanges), nameof(ChangesText), nameof(CanApply))]
    public partial string Mirror { get; set; }

    /// <summary>The settings the daemon keeps; absent while it keeps none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanEditMode), nameof(CanEditHttp), nameof(CanEditHttps))]
    [NotifyPropertyChangedFor(nameof(CanEditExclude), nameof(CanEditTrust), nameof(CanEditMirror))]
    [NotifyPropertyChangedFor(nameof(IsModeLocked), nameof(IsHttpLocked), nameof(IsHttpsLocked))]
    [NotifyPropertyChangedFor(nameof(IsExcludeLocked), nameof(IsTrustLocked), nameof(IsMirrorLocked))]
    [NotifyPropertyChangedFor(nameof(CanEditPass), nameof(IsPassLocked))]
    [NotifyPropertyChangedFor(nameof(ChangeCount), nameof(HasChanges), nameof(ChangesText), nameof(CanApply))]
    public partial DaemonSettings? Saved { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanEditMode), nameof(CanEditHttp), nameof(CanEditHttps))]
    [NotifyPropertyChangedFor(nameof(CanEditExclude), nameof(CanEditTrust), nameof(CanEditMirror), nameof(CanApply))]
    [NotifyPropertyChangedFor(nameof(CanEditPass))]
    public partial bool IsApplying { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApplyText), nameof(ApplyHint))]
    public partial bool EngineRuns { get; set; }

    public ProxySettingsViewModel(
        IDaemonSettings settings, IDaemonConnection daemon, INotificationService notifications)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(notifications);
        _settings = settings;
        _daemon = daemon;
        _notifications = notifications;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        IsOffered = daemon.Identity?.Capabilities.Contains(DaemonCapability.EngineConfiguration) == true;
        OffersPassToContainers = IsOffered && daemon.Identity?.ApiRevision >= _passToContainersRevision;
        EngineRuns = daemon.Status.Engine == EngineState.Running;
        Http = string.Empty;
        Https = string.Empty;
        Exclude = string.Empty;
        Mirror = string.Empty;
        TrustsSystemCertificates = true;
    }

    /// <summary>Whether the daemon gives these settings to its engine; the WSL backend does.</summary>
    public bool IsOffered { get; }

    public bool LacksOffer => !IsOffered;

    /// <summary>Whether the daemon can give new containers and builds the engine's proxy.</summary>
    public bool OffersPassToContainers { get; }

    public bool IsManual => ModeIndex == _manualIndex;

    public bool CanEdit => IsOffered && Saved is not null && !IsApplying;

    public bool CanEditMode => CanEdit && !IsModeLocked;

    public bool CanEditHttp => CanEdit && IsManual && !IsHttpLocked;

    public bool CanEditHttps => CanEdit && IsManual && !IsHttpsLocked;

    public bool CanEditExclude => CanEdit && IsManual && !IsExcludeLocked;

    public bool CanEditPass => CanEdit && OffersPassToContainers && !IsPassLocked;

    public bool CanEditTrust => CanEdit && !IsTrustLocked;

    public bool CanEditMirror => CanEdit && !IsMirrorLocked;

    public bool IsModeLocked => Saved?.IsLocked(DaemonSetting.ProxyMode) == true;

    public bool IsHttpLocked => Saved?.IsLocked(DaemonSetting.ProxyHttp) == true;

    public bool IsHttpsLocked => Saved?.IsLocked(DaemonSetting.ProxyHttps) == true;

    public bool IsExcludeLocked => Saved?.IsLocked(DaemonSetting.ProxyExclude) == true;

    public bool IsPassLocked => Saved?.IsLocked(DaemonSetting.PassProxyToContainers) == true;

    public bool IsTrustLocked => Saved?.IsLocked(DaemonSetting.TrustSystemCertificates) == true;

    public bool IsMirrorLocked => Saved?.IsLocked(DaemonSetting.RegistryMirrors) == true;

    /// <summary>How many settings the edits change.</summary>
    public int ChangeCount => Edit()
        .Settings()
        .Count();

    public bool HasChanges => ChangeCount > 0;

    public bool CanApply => HasChanges && !IsApplying;

    /// <summary>Such as <c>1 unsaved change.</c></summary>
    public string ChangesText => ChangeCount == 1 ? "1 unsaved change." : $"{ChangeCount} unsaved changes.";

    public string ApplyHint => EngineRuns
        ? "Applying restarts the engine. Running containers stop and start again."
        : "The engine uses them when it starts.";

    public string ApplyText => EngineRuns ? "Apply and restart engine" : "Apply";

    /// <summary>Follows the engine's state, which decides whether applying restarts it.</summary>
    public void Activate()
    {
        _status ??= _daemon.StatusChanges.Subscribe(status => {
            // StatusChanges emits on a background thread.
            var runs = status.Engine == EngineState.Running;

            if (_dispatcher is null)
            {
                EngineRuns = runs;
            }
            else
            {
                _dispatcher.TryEnqueue(() => EngineRuns = runs);
            }
        });
    }

    public void Deactivate()
    {
        _status?.Dispose();
        _status = null;
    }

    /// <summary>Shows the daemon's settings; edits the user has not applied stay.</summary>
    public void Show(DaemonSettings? settings)
    {
        var keepsEdits = HasChanges && settings is not null;
        Saved = settings;

        if (!keepsEdits)
        {
            Load(settings);
        }
    }

    public void Discard()
    {
        Load(Saved);
    }

    /// <summary>The settings the edits change, compared with the saved ones.</summary>
    public DaemonSettingsEdit Edit()
    {
        if (Saved is not { } saved)
        {
            return new DaemonSettingsEdit();
        }

        var mode = ModeIndex switch {
            _manualIndex => ProxyMode.Manual,
            _noneIndex => ProxyMode.None,
            _ => ProxyMode.System,
        };
        string[] mirrors = Mirror.Trim() is { Length: > 0 } first
            ? [first, .. saved.RegistryMirrors.Skip(1)]
            : [.. saved.RegistryMirrors.Skip(1)];

        return new DaemonSettingsEdit {
            ProxyMode = mode != saved.Proxy.Mode ? mode : null,
            ProxyHttp = Changed(Http, saved.Proxy.Http),
            ProxyHttps = Changed(Https, saved.Proxy.Https),
            ProxyExclude = Changed(Exclude, saved.Proxy.Exclude),
            PassProxyToContainers = PassesToContainers != saved.Proxy.PassToContainers ? PassesToContainers : null,
            TrustSystemCertificates = TrustsSystemCertificates != saved.TrustSystemCertificates
                ? TrustsSystemCertificates
                : null,
            RegistryMirrors = mirrors.SequenceEqual(saved.RegistryMirrors) ? null : mirrors,
        };
    }

    /// <summary>Saves the edits, all or none, and restarts a running engine that applies them only at its start.
    /// </summary>
    public async Task ApplyAsync()
    {
        var edit = Edit();

        if (!edit
            .Settings()
            .Any())
        {
            return;
        }

        IsApplying = true;
        try
        {
            var result = await _settings.UpdateAsync(edit, CancellationToken.None);

            if (result.IsFailure)
            {
                _notifications.Show(Refusal(result.Error), InfoBarSeverity.Error, "Nothing was saved");

                return;
            }

            Saved = result.Value.Settings;
            Load(Saved);

            if (result.Value.RestartRequired.Count == 0 || !EngineRuns)
            {
                _notifications.Show(
                    "Saved. The engine uses the new settings when it starts.", InfoBarSeverity.Success);

                return;
            }

            var restarted = await _daemon.RestartEngineAsync(CancellationToken.None);

            if (restarted.IsSuccess)
            {
                _notifications.Show("The engine restarted with the new settings.", InfoBarSeverity.Success);
            }
            else
            {
                _notifications.Show(
                    restarted.Error.Message, InfoBarSeverity.Error, "Saved, but the engine didn't restart");
            }
        }
        finally
        {
            IsApplying = false;
        }
    }

    /// <summary>Why the daemon refused the edits, naming the field the way the page does.</summary>
    public static string Refusal(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.Code == IDaemonSettings.SettingLockedCode)
        {
            return "Your organization manages one of these settings.";
        }

        if (error.Code != IDaemonSettings.InvalidValueCode
            || DaemonSettingPaths.Refused(error.Message) is not { } refused)
        {
            return error.Message;
        }

        var field = refused.Setting switch {
            DaemonSetting.ProxyHttp => "HTTP proxy",
            DaemonSetting.ProxyHttps => "HTTPS proxy",
            DaemonSetting.ProxyExclude => "Don't use the proxy for",
            DaemonSetting.RegistryMirrors => "Registry mirror",
            DaemonSetting.ProxyMode => "How the engine finds your proxy",
            DaemonSetting.PassProxyToContainers => "Pass these settings to containers",
            _ => DaemonSettingPaths.Of(refused.Setting),
        };

        return $"{field}: {refused.Reason}";
    }

    private static string? Changed(string edited, string saved)
    {
        var value = edited.Trim();

        return value == saved ? null : value;
    }

    private void Load(DaemonSettings? settings)
    {
        var proxy = settings?.Proxy ?? ProxySettings.Default;
        ModeIndex = proxy.Mode switch {
            ProxyMode.Manual => _manualIndex,
            ProxyMode.None => _noneIndex,
            _ => 0,
        };
        Http = proxy.Http;
        Https = proxy.Https;
        Exclude = proxy.Exclude;
        PassesToContainers = proxy.PassToContainers;
        TrustsSystemCertificates = settings?.TrustSystemCertificates ?? true;
        Mirror = settings?.RegistryMirrors.FirstOrDefault() ?? string.Empty;
    }
}
