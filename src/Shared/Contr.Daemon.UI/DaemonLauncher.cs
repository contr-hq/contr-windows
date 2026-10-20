using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Contr.Sdk;
using Microsoft.Extensions.Options;

namespace Contr.Daemon.UI;

/// <summary>Starts <c>contrd.exe</c> as a detached process that outlives the app.</summary>
public sealed class DaemonLauncher
{
    private const string _daemonFileName = "contrd.exe";

    private readonly string[] _arguments;

    public DaemonLauncher(IOptions<DaemonOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Executable = Resolve(options.Value.Path);
        _arguments = options.Value.Arguments;
    }

    /// <summary>The configured path, the bundled daemon, or the bare file name PATH resolves.</summary>
    public string Executable { get; }

    /// <exception cref="DaemonUnavailableException">The executable cannot be run.</exception>
    public void Start()
    {
        var startInfo = new ProcessStartInfo(Executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in _arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
        }
        catch (Win32Exception exception)
        {
            throw new DaemonUnavailableException($"Cannot run {Executable}: {exception.Message}", exception);
        }
    }

    private static string Resolve(string configured)
    {
        if (configured.Length > 0)
        {
            return configured;
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, _daemonFileName);

        return File.Exists(bundled) ? bundled : _daemonFileName;
    }
}
