using Contr.SharedKernel;

namespace Contr.Modules.Containers.Application.Containers;

/// <summary>One event of a terminal session in a container.</summary>
public abstract record TerminalOutput
{
    private TerminalOutput()
    {
    }

    /// <summary>Bytes the command wrote; a terminal combines its output and error streams.</summary>
    public sealed record Data(byte[] Bytes) : TerminalOutput;

    /// <summary>The last event of a session whose command ended.</summary>
    public sealed record Exited(int ExitCode) : TerminalOutput;

    /// <summary>The last event of a session that broke.</summary>
    public sealed record Failed(Error Error) : TerminalOutput;
}
