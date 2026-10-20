using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Sdk;
using Contr.Sdk.Integration.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Distribution = Contr.UI.Contracts.Services.Daemon.WslDistribution;
using IntegrationState = Contr.UI.Contracts.Services.Daemon.WslIntegrationState;
using ToolVersions = Contr.UI.Contracts.Services.Daemon.CliTool;
using WireDistribution = Contr.Sdk.Integration.V1.WslDistribution;
using WireState = Contr.Sdk.Integration.V1.WslIntegrationState;

namespace Contr.Daemon.UI;

/// <summary>Both <see cref="ICliTools"/> and <see cref="IWslDistributions"/>, over IntegrationService.</summary>
public sealed class IntegrationClient : ICliTools, IWslDistributions
{
    private readonly IDaemonClient _daemon;

    public IntegrationClient(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public async Task<Result<CliToolsStatus>> GetAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                async (client, token) => (await client.Integration
                    .GetCliToolsAsync(new GetCliToolsRequest(), cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false)).CliTools,
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, tools => ToStatus(tools ?? new CliTools()));
    }

    public async Task<Result<CliToolsStatus>> InstallAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                async (client, token) => (await client.Integration
                    .InstallCliToolsAsync(new InstallCliToolsRequest(), cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false)).CliTools,
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, tools => ToStatus(tools ?? new CliTools()));
    }

    public async Task<Result<IReadOnlyList<Distribution>>> ListAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Integration
                    .ListWslDistributionsAsync(new ListWslDistributionsRequest(), cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return Map<ListWslDistributionsResponse, IReadOnlyList<Distribution>>(
            result, response => [.. response.Distributions.Select(ToDistribution)]);
    }

    public async Task<Result<Distribution>> SetIntegrationAsync(
        string name, bool isEnabled, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var request = new SetWslIntegrationRequest { Name = name, Enabled = isEnabled };
        var result = await _daemon
            .CallAsync(
                async (client, token) => (await client.Integration
                    .SetWslIntegrationAsync(request, cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false)).Distribution,
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, distribution => ToDistribution(distribution ?? new WireDistribution { Name = name }));
    }

    public static CliToolsStatus ToStatus(CliTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        return new CliToolsStatus(
            [.. tools.Tools.Select(tool => new ToolVersions(tool.Name, tool.BundledVersion, tool.InstalledVersion))],
            tools.BinDir,
            tools.OnPath,
            tools.ShadowedBy);
    }

    public static Distribution ToDistribution(WireDistribution distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        return new Distribution(
            distribution.Name,
            (int)distribution.WslVersion,
            distribution.IsDefault,
            distribution.IntegrationState switch {
                WireState.Pending => IntegrationState.Pending,
                WireState.Active => IntegrationState.Active,
                WireState.Failed => IntegrationState.Failed,
                _ => IntegrationState.Disabled,
            },
            distribution.Failure);
    }

    private static Result<TOut> Map<TIn, TOut>(Result<TIn> result, Func<TIn, TOut> map)
    {
        return result.IsFailure ? Result.Failure<TOut>(result.Error) : Result.Success(map(result.Value));
    }
}
