using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contr.SharedKernel;

namespace Contr.UI.Contracts.Services.Daemon;

/// <summary>The Docker CLI tools the daemon bundles and installs for the user.</summary>
public interface ICliTools
{
    /// <summary>What the daemon bundles and what is installed for this user.</summary>
    Task<Result<CliToolsStatus>> GetAsync(CancellationToken cancellationToken);

    /// <summary>Installs the bundled tools for this user and adds them to the user's PATH.</summary>
    /// <returns>A failure with code <c>Daemon.FeatureUnavailable</c> where the daemon bundles none.</returns>
    Task<Result<CliToolsStatus>> InstallAsync(CancellationToken cancellationToken);
}

/// <summary>The Docker CLI tools for this user.</summary>
/// <param name="Tools">Each tool with its bundled and installed versions.</param>
/// <param name="BinFolder">The folder that holds the installed <c>docker</c>; empty when none is installed.</param>
/// <param name="IsOnPath">Whether a new shell finds the installed <c>docker</c>.</param>
/// <param name="ShadowedBy">Another <c>docker</c> a new shell finds first; empty when none.</param>
public sealed record CliToolsStatus(IReadOnlyList<CliTool> Tools, string BinFolder, bool IsOnPath, string ShadowedBy);

/// <summary>One Docker CLI tool.</summary>
/// <param name="Name">Such as <c>docker</c>, <c>compose</c> or <c>buildx</c>.</param>
/// <param name="BundledVersion">The version the daemon bundles; empty when it bundles none.</param>
/// <param name="InstalledVersion">The version installed for the user; empty when not installed.</param>
public sealed record CliTool(string Name, string BundledVersion, string InstalledVersion);
