using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Contr.Modules.Containers.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Contr.Modules.Containers.UI.Views;

/// <summary>An xterm.js terminal in WebView2, wired to a <see cref="ContainerTerminalViewModel"/>.</summary>
/// <remarks>The page comes from the app folder through a virtual host; it may navigate nowhere else.</remarks>
public sealed partial class TerminalView : UserControl
{
    private const string _host = "terminal.contr.example";
    private const string _folder = "ContainerTerminal";

    private readonly WebView2 _web = new() { DefaultBackgroundColor = Microsoft.UI.Colors.Transparent };
    private ContainerTerminalViewModel? _terminal;
    private Task? _initializing;

    public TerminalView()
    {
        Content = _web;
        ActualThemeChanged += OnActualThemeChanged;
    }

    /// <summary>Raised once the page reports its size; connecting before that starts at 80 by 24.</summary>
    public event EventHandler? Ready;

    /// <summary>Raised with the text the user selected, after <see cref="RequestCopy"/>.</summary>
    public event EventHandler<string>? Copied;

    /// <summary>Shows <paramref name="terminal"/>'s session; call it once, from the page's <c>Loaded</c>.</summary>
    public Task AttachAsync(ContainerTerminalViewModel terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        if (_terminal is null)
        {
            _terminal = terminal;
            terminal.OutputReceived += OnOutput;
            terminal.Cleared += OnCleared;
        }

        return _initializing ??= InitializeAsync();
    }

    public void Detach()
    {
        if (_terminal is { } terminal)
        {
            terminal.OutputReceived -= OnOutput;
            terminal.Cleared -= OnCleared;
            _terminal = null;
        }
    }

    public void RequestCopy()
    {
        Post(new { type = "copy" });
    }

    public void FocusTerminal()
    {
        _web.Focus(FocusState.Programmatic);
        Post(new { type = "focus" });
    }

    private static string Text(JsonElement message, string name)
    {
        var value = message.GetProperty(name);

        return value.GetString() ?? string.Empty;
    }

    private static int Number(JsonElement message, string name)
    {
        var value = message.GetProperty(name);

        return value.GetInt32();
    }

    private static string UserDataFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Contr", "WebView2");
    }

    private async Task InitializeAsync()
    {
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
            null, UserDataFolder(), new CoreWebView2EnvironmentOptions());
        await _web.EnsureCoreWebView2Async(environment);

        var core = _web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.SetVirtualHostNameToFolderMapping(
            _host, Path.Combine(AppContext.BaseDirectory, _folder), CoreWebView2HostResourceAccessKind.Deny);
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.WebMessageReceived += OnWebMessageReceived;
        core.Navigate($"https://{_host}/index.html");
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        args.Cancel = !args.Uri.StartsWith($"https://{_host}/", StringComparison.OrdinalIgnoreCase);
    }

    private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
    }

    private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using var message = JsonDocument.Parse(args.WebMessageAsJson);
            await HandleAsync(message.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or System.Collections.Generic.KeyNotFoundException)
        {
            // The page sends only the messages below; anything else is ignored.
        }
    }

    private async Task HandleAsync(JsonElement root)
    {
        var type = root.TryGetProperty("type", out var value) ? value.GetString() : null;

        switch (type)
        {
            case "ready":
                SendTheme();
                await ResizeAsync(root);
                Ready?.Invoke(this, EventArgs.Empty);
                break;
            case "resize":
                await ResizeAsync(root);
                break;
            case "input" when _terminal is { } terminal:
                await terminal.SendAsync(Text(root, "data"));
                break;
            case "copied":
                Copied?.Invoke(this, Text(root, "text"));
                break;
        }
    }

    private async Task ResizeAsync(JsonElement size)
    {
        if (_terminal is { } terminal)
        {
            await terminal.ResizeAsync(Number(size, "columns"), Number(size, "rows"));
        }
    }

    private void OnOutput(object? sender, byte[] output)
    {
        Post(new { type = "output", data = Convert.ToBase64String(output) });
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        Post(new { type = "clear" });
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        SendTheme();
    }

    private void SendTheme()
    {
        Post(new { type = "theme", theme = ActualTheme == ElementTheme.Light ? "light" : "dark" });
    }

    private void Post(object message)
    {
        _web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }
}
