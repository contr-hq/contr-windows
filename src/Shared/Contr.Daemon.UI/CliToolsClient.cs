using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Contr.Sdk;
using Contr.Sdk.Integration.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Grpc.Net.Client;
using ToolVersions = Contr.UI.Contracts.Services.Daemon.CliTool;

namespace Contr.Daemon.UI;

/// <summary>The <see cref="ICliTools"/> over the daemon's IntegrationService.</summary>
/// <remarks>
/// <see cref="ContrClient"/> exposes no IntegrationService client, so this one opens a channel of its own to the
/// endpoint of each connection and keeps it as long as that connection lives.
/// </remarks>
public sealed class CliToolsClient : ICliTools
{
    private readonly IDaemonClient _daemon;
    private readonly ConditionalWeakTable<ContrClient, GrpcChannel> _channels = [];

    public CliToolsClient(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public Task<Result<CliToolsStatus>> GetAsync(CancellationToken cancellationToken)
    {
        return CallAsync(
            async (service, token) => (await service
                .GetCliToolsAsync(new GetCliToolsRequest(), cancellationToken: token)
                .ResponseAsync
                .ConfigureAwait(false)).CliTools,
            cancellationToken);
    }

    public Task<Result<CliToolsStatus>> InstallAsync(CancellationToken cancellationToken)
    {
        return CallAsync(
            async (service, token) => (await service
                .InstallCliToolsAsync(new InstallCliToolsRequest(), cancellationToken: token)
                .ResponseAsync
                .ConfigureAwait(false)).CliTools,
            cancellationToken);
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

    private async Task<Result<CliToolsStatus>> CallAsync(
        Func<IntegrationService.IntegrationServiceClient, CancellationToken, Task<CliTools?>> call,
        CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                (client, token) => call(Service(client), token),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Failure<CliToolsStatus>(result.Error);
        }

        return Result.Success(ToStatus(result.Value ?? new CliTools()));
    }

    private IntegrationService.IntegrationServiceClient Service(ContrClient client)
    {
        var channel = _channels.GetValue(client, connected => ContrChannel.Create(connected.Endpoint));

        return new IntegrationService.IntegrationServiceClient(channel);
    }
}
