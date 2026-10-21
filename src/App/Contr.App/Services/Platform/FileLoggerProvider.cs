using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Contr.App.Services.Platform;

/// <summary>
/// Writes the application log to a file under <c>%LOCALAPPDATA%\Contr\logs</c>.
/// </summary>
/// <remarks>
/// Invariants: a logging call never blocks the UI thread on disk, a failed write is swallowed
/// rather than becoming the crash, and this code never rotates or deletes log files.
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>(), 4096);
    private readonly string _path;
    private readonly Thread _writer;

    public FileLoggerProvider(LogLevel minimum)
    {
        Minimum = minimum;

        Directory.CreateDirectory(LogDirectory);
        _path = Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");

        // IsBackground: the writer never keeps a closing app alive, so lines queued at exit are lost.
        _writer = new Thread(Drain) { IsBackground = true, Name = "Contr.FileLog" };
        _writer.Start();
    }

    /// <summary>Where the client writes its logs, one <c>app-yyyy-MM-dd.log</c> per day it starts.</summary>
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Contr", "logs");

    /// <summary>The lowest level written. Everything below it is dropped before it is formatted.</summary>
    public LogLevel Minimum { get; }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(this, categoryName);
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        // Bounded: a writer stuck on a locked file must not stop the process from exiting.
        _writer.Join(TimeSpan.FromSeconds(2));
        _queue.Dispose();
    }

    internal void Enqueue(string line)
    {
        // A full queue drops the line rather than blocking the caller.
        _ = _queue.TryAdd(line);
    }

    private void Drain()
    {
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try
            {
                File.AppendAllText(_path, line, Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A locked or full disk has nowhere to report to.
            }
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _provider.Minimum && logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            ArgumentNullException.ThrowIfNull(formatter);

            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [")
                .Append(Level(logLevel))
                .Append("] ")
                .Append(_category)
                .Append(": ")
                .Append(formatter(state, exception))
                .AppendLine();

            if (exception is not null)
            {
                line.AppendLine(exception.ToString());
            }

            _provider.Enqueue(line.ToString());
        }

        private static string Level(LogLevel level)
        {
            return level switch {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "???",
            };
        }
    }
}
