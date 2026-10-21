using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.Modules.Containers.Application.Containers;
using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Abstractions;

/// <summary>A command running in a container with a terminal; disposing it ends the session.</summary>
public interface ITerminalSession : IAsyncDisposable
{
    /// <summary>The command's output until it exits or the session breaks; read it once.</summary>
    IAsyncEnumerable<TerminalOutput> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Sends keystrokes or pasted text to the command.</summary>
    Task<Result> WriteAsync(byte[] input, CancellationToken cancellationToken);

    /// <summary>Gives the command its terminal size, <paramref name="columns"/> by <paramref name="rows"/>.</summary>
    Task<Result> ResizeAsync(int columns, int rows, CancellationToken cancellationToken);
}
