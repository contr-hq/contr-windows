using System;
using Microsoft.Win32;

namespace Contr.App.Services.Platform;

/// <summary>Whether Windows starts Contr, in the notification area, when the user signs in.</summary>
public interface ISignInStartup
{
    bool IsEnabled { get; set; }
}

/// <summary>The <see cref="ISignInStartup"/> over an entry under the user's <c>Run</c> key.</summary>
/// <remarks>Nothing outside the user's own registry hive changes.</remarks>
public sealed class SignInStartup : ISignInStartup
{
    /// <summary>The argument that starts Contr in the notification area without its window.</summary>
    public const string BackgroundArgument = "--background";

    private const string _runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string _valueName = "Contr";

    public bool IsEnabled
    {
        get {
            using var key = Registry.CurrentUser.OpenSubKey(_runKey);

            return key?.GetValue(_valueName) is string command && command.Length > 0;
        }
        set {
            using var key = Registry.CurrentUser.CreateSubKey(_runKey);

            if (value && Environment.ProcessPath is { } executable)
            {
                key.SetValue(_valueName, $"\"{executable}\" {BackgroundArgument}");
            }
            else
            {
                key.DeleteValue(_valueName, throwOnMissingValue: false);
            }
        }
    }

    /// <summary>Whether this process was started to stay in the notification area.</summary>
    public static bool IsBackgroundLaunch()
    {
        return Array.Exists(
            Environment.GetCommandLineArgs(),
            argument => string.Equals(argument, BackgroundArgument, StringComparison.OrdinalIgnoreCase));
    }
}
