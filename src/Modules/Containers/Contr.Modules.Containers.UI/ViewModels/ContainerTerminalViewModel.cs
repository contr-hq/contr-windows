using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.OpenTerminal;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The exec tab: a shell in the running container, shown by a terminal view.</summary>
/// <remarks>Use it on the UI thread: output resumes on the captured context.</remarks>
public sealed partial class ContainerTerminalViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly string[] _shells = ["/bin/sh", "/bin/bash", "/bin/ash"];
    private static readonly string[] _users = ["", "root"];

    private readonly ISender _sender;
    private ITerminalSession? _session;
    private CancellationTokenSource? _reading;
    private int _columns = 80;
    private int _rows = 24;

    /// <summary>An index into the shells the tab offers: <c>/bin/sh</c>, <c>/bin/bash</c>, <c>/bin/ash</c>.</summary>
    [ObservableProperty]
    public partial int ShellIndex { get; set; }

    /// <summary>0 runs as the container's own user, 1 as root.</summary>
    [ObservableProperty]
    public partial int UserIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDisconnected))]
    public partial bool IsConnected { get; set; }

    /// <summary>Such as <c>Connected · 120 × 32</c> or <c>Exited with code 0</c>.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; }

    public ContainerTerminalViewModel(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
        StatusText = "Not connected";
    }

    /// <summary>Raised with output for the terminal view to draw.</summary>
    public event EventHandler<byte[]>? OutputReceived;

    /// <summary>Raised when the terminal view should forget what it shows.</summary>
    public event EventHandler? Cleared;

    public bool IsDisconnected => !IsConnected;

    /// <summary>Starts the chosen shell in <paramref name="id"/>, ending any session in progress.</summary>
    public async Task ConnectAsync(ContainerId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        await DisconnectAsync();
        Cleared?.Invoke(this, EventArgs.Empty);
        StatusText = "Connecting";

        var shell = _shells[Math.Clamp(ShellIndex, 0, _shells.Length - 1)];
        var user = _users[Math.Clamp(UserIndex, 0, _users.Length - 1)];
        var opened = await _sender.Send(new OpenTerminalQuery(id, shell, user, _columns, _rows));

        if (opened.IsFailure)
        {
            StatusText = opened.Error.Message;

            return;
        }

        _session = opened.Value;
        _reading = new CancellationTokenSource();
        IsConnected = true;
        ShowSize();
        _ = ReadAsync(_session, _reading.Token);
    }

    public async Task DisconnectAsync()
    {
        _reading?.Cancel();
        _reading?.Dispose();
        _reading = null;

        if (_session is { } session)
        {
            _session = null;
            await session.DisposeAsync();
        }

        if (IsConnected)
        {
            IsConnected = false;
            StatusText = "Disconnected";
        }
    }

    /// <summary>Sends what the user typed or pasted; nothing happens without a session.</summary>
    public async Task SendAsync(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_session is { } session)
        {
            await session.WriteAsync(Encoding.UTF8.GetBytes(input), CancellationToken.None);
        }
    }

    /// <summary>Records the terminal view's size and passes it to the running command.</summary>
    public async Task ResizeAsync(int columns, int rows)
    {
        _columns = Math.Max(1, columns);
        _rows = Math.Max(1, rows);

        if (_session is { } session)
        {
            ShowSize();
            await session.ResizeAsync(_columns, _rows, CancellationToken.None);
        }
    }

    public void Clear()
    {
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private void ShowSize()
    {
        var size = $"{_columns.ToString(CultureInfo.CurrentCulture)} × {_rows.ToString(CultureInfo.CurrentCulture)}";
        StatusText = $"Connected · {size}";
    }

    private async Task ReadAsync(ITerminalSession session, CancellationToken cancellationToken)
    {
        await foreach (var output in session.ReadAsync(cancellationToken))
        {
            switch (output)
            {
                case TerminalOutput.Data data:
                    OutputReceived?.Invoke(this, data.Bytes);
                    break;
                case TerminalOutput.Exited exited:
                    StatusText = $"Exited with code {exited.ExitCode.ToString(CultureInfo.CurrentCulture)}";
                    break;
                case TerminalOutput.Failed failed:
                    StatusText = failed.Error.Message;
                    break;
            }
        }

        if (!cancellationToken.IsCancellationRequested && ReferenceEquals(session, _session))
        {
            IsConnected = false;
            _session = null;
            await session.DisposeAsync();
        }
    }
}
