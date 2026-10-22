using System;
using System.Threading;
using System.Threading.Tasks;
using Contr.Sdk.Updates.V1;
using Contr.SharedKernel;
using Contr.UI.Contracts.Services.Daemon;
using Google.Protobuf.WellKnownTypes;
using UpdateStatus = Contr.UI.Contracts.Services.Daemon.UpdateStatus;
using WireStatus = Contr.Sdk.Updates.V1.UpdateStatus;

namespace Contr.Daemon.UI;

/// <summary><see cref="IDaemonUpdates"/> over UpdatesService.</summary>
public sealed class UpdatesClient : IDaemonUpdates
{
    private readonly IDaemonClient _daemon;

    public UpdatesClient(IDaemonClient daemon)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        _daemon = daemon;
    }

    public async Task<Result<UpdateStatus>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Updates
                    .GetUpdateStatusAsync(new GetUpdateStatusRequest(), cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<UpdateStatus>(result.Error)
            : Result.Success(ToStatus(result.Value.Status ?? new WireStatus()));
    }

    public async Task<Result<UpdateStatus>> CheckAsync(CancellationToken cancellationToken)
    {
        var result = await _daemon
            .CallAsync(
                async (client, token) => await client.Updates
                    .CheckForUpdatesAsync(new CheckForUpdatesRequest(), cancellationToken: token)
                    .ResponseAsync
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<UpdateStatus>(result.Error)
            : Result.Success(ToStatus(result.Value.Status ?? new WireStatus()));
    }

    public static UpdateStatus ToStatus(WireStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var available = status.Available is { Version.Length: > 0 } release
            ? new AvailableRelease(release.Version, Page(release.Url), Time(release.PublishedAt), release.Notes)
            : null;

        return new UpdateStatus(status.CurrentVersion, available, Time(status.CheckedAt), status.Error);
    }

    /// <summary>The release page, which the app hands to the browser; anything but an https address is dropped.
    /// </summary>
    private static Uri? Page(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var page) && page.Scheme == Uri.UriSchemeHttps ? page : null;
    }

    private static DateTimeOffset? Time(Timestamp? timestamp)
    {
        return timestamp is null ? null : timestamp.ToDateTimeOffset();
    }
}
