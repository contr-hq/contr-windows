using System;
using System.Collections.Generic;
using Contr.Modules.Containers.Domain;

namespace Contr.Modules.Containers.Application.Containers;

/// <summary>A container as inspection describes it.</summary>
/// <param name="StartedAt">Absent when the container never started.</param>
/// <param name="FinishedAt">Absent when the container never exited.</param>
/// <param name="Environment">The <c>VAR=value</c> entries the container runs with.</param>
/// <param name="RawJson">The engine's own inspection document.</param>
public sealed record ContainerDetailsDto(
    ContainerDto Container,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int ExitCode,
    int RestartCount,
    ContainerHealth Health,
    string WorkingDirectory,
    IReadOnlyList<string> Environment,
    IReadOnlyList<MountDto> Mounts,
    string RawJson);

/// <summary>A filesystem mounted into a container.</summary>
/// <param name="Kind">Such as <c>bind</c>, <c>volume</c> or <c>tmpfs</c>.</param>
/// <param name="Name">The volume's name; empty for other kinds.</param>
public sealed record MountDto(string Kind, string Name, string Source, string Destination, bool ReadOnly);
