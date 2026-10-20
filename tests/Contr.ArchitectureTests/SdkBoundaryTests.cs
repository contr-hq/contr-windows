using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Contr.ArchitectureTests;

/// <summary>
/// The daemon SDK stays behind the <c>*.UI</c> adapter projects: no other project references it,
/// and no other project's source names it, transitively or not.
/// </summary>
/// <remarks>
/// Source is read rather than assemblies loaded, so the WinUI projects need no build for the check.
/// </remarks>
public sealed partial class SdkBoundaryTests
{
    private const string _adapterSuffix = ".UI";
    private const string _sdkProject = "Contr.Sdk.csproj";

    private static readonly string _sourceRoot = Path.Combine(RepositoryRoot(), "src");

    [Fact]
    public void OnlyUiProjects_ReferenceTheSdk()
    {
        var offenders = Projects()
            .Where(project => !IsAdapter(project))
            .Where(ReferencesSdk)
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void OnlyUiProjects_NameSdkTypes()
    {
        var offenders = Projects()
            .Where(project => !IsAdapter(project))
            .SelectMany(project => Directory.EnumerateFiles(
                Path.GetDirectoryName(project)!, "*.*", SearchOption.AllDirectories))
            .Where(file => Path.GetExtension(file) is ".cs" or ".xaml")
            .Where(file => !IsBuildOutput(file))
            .Where(file => SdkNamespace().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(_sourceRoot, file))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheSdkIsReferenced_AtLeastByOneAdapter()
    {
        Assert.Contains(Projects(), project => IsAdapter(project) && ReferencesSdk(project));
    }

    private static IEnumerable<string> Projects()
    {
        return Directory.EnumerateFiles(_sourceRoot, "*.csproj", SearchOption.AllDirectories);
    }

    private static bool IsAdapter(string project)
    {
        return Path
            .GetFileNameWithoutExtension(project)
            .EndsWith(_adapterSuffix, StringComparison.Ordinal);
    }

    private static bool ReferencesSdk(string project)
    {
        var text = File.ReadAllText(project);

        return text.Contains(_sdkProject, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuildOutput(string file)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var segments = file.Split(separators);

        return segments.Contains("bin") || segments.Contains("obj");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Contr.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Contr.slnx was not found above the test assembly.");
    }

    [GeneratedRegex(@"\bContr\.Sdk\b")]
    private static partial Regex SdkNamespace();
}
