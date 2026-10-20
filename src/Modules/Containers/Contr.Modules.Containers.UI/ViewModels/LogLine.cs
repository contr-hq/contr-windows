using CommunityToolkit.Mvvm.ComponentModel;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>One line of a container's log as the log tab shows it.</summary>
public sealed partial class LogLine : ObservableObject
{
    [ObservableProperty]
    public partial bool IsMatch { get; set; }

    /// <summary>The search text to highlight in a matching line; empty otherwise.</summary>
    [ObservableProperty]
    public partial string Highlight { get; set; }

    /// <param name="time">The engine's time for the line, such as <c>10:41:02.118</c>; may be empty.</param>
    /// <param name="isNote">A line the client adds, such as where the container exited.</param>
    public LogLine(string time, string text, bool isError, bool isNote = false)
    {
        Time = time;
        Text = text;
        IsError = isError;
        IsNote = isNote;
        Highlight = string.Empty;
    }

    public string Time { get; }

    public string Text { get; private set; }

    public bool IsError { get; }

    public bool IsNote { get; }

    public bool HasTime => Time.Length > 0;

    /// <summary>Extends a line the stream delivered in parts.</summary>
    public void Append(string more)
    {
        Text += more;
        OnPropertyChanged(nameof(Text));
    }
}
