using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Daemon;
using Contr.Sdk;
using Contr.SharedKernel;
using Grpc.Core;

namespace Contr.Daemon.UI;

/// <summary>Daemon calls that report failure as a <see cref="Result"/> or a last stream item.</summary>
public static class DaemonCalls
{
    /// <summary>Runs a call that returns a value.</summary>
    public static async Task<Result<T>> CallAsync<T>(
        this IDaemonClient daemon,
        Func<ContrClient, CancellationToken, Task<T>> call,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(call);

        try
        {
            var client = await daemon.GetAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(await call(client, cancellationToken).ConfigureAwait(false));
        }
        catch (RpcException exception)
        {
            return Result.Failure<T>(exception.ToError());
        }
        catch (ContrException exception)
        {
            return Result.Failure<T>(exception.ToError());
        }
        catch (ObjectDisposedException)
        {
            return Result.Failure<T>(DaemonErrors.Unavailable);
        }
    }

    /// <summary>Runs a call whose response carries nothing the caller needs.</summary>
    public static async Task<Result> RunAsync(
        this IDaemonClient daemon,
        Func<ContrClient, CancellationToken, Task> call,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);

        var result = await daemon
            .CallAsync(
                async (client, token) => {
                    await call(client, token).ConfigureAwait(false);

                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    /// <summary>
    /// Reads a server stream; a broken stream ends with one item made by <paramref name="fail"/>,
    /// a cancelled one ends without it.
    /// </summary>
    /// <param name="map">Returns <see langword="null"/> for a response kind this client does not know.</param>
    public static async IAsyncEnumerable<TItem> StreamAsync<TResponse, TItem>(
        this IDaemonClient daemon,
        Func<ContrClient, CancellationToken, AsyncServerStreamingCall<TResponse>> open,
        Func<TResponse, TItem?> map,
        Func<Error, TItem> fail,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(fail);

        var opened = await OpenAsync(daemon, open, cancellationToken).ConfigureAwait(false);

        if (opened.IsFailure)
        {
            yield return fail(opened.Error);
            yield break;
        }

        using var call = opened.Value;
        while (true)
        {
            var next = await MoveNextAsync(call.ResponseStream, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (next.IsFailure)
            {
                yield return fail(next.Error);
                yield break;
            }

            if (!next.Value)
            {
                yield break;
            }

            if (map(call.ResponseStream.Current) is { } item)
            {
                yield return item;
            }
        }
    }

    private static async Task<Result<AsyncServerStreamingCall<TResponse>>> OpenAsync<TResponse>(
        IDaemonClient daemon,
        Func<ContrClient, CancellationToken, AsyncServerStreamingCall<TResponse>> open,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = await daemon.GetAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(open(client, cancellationToken));
        }
        catch (ContrException exception)
        {
            return Result.Failure<AsyncServerStreamingCall<TResponse>>(exception.ToError());
        }
        catch (ObjectDisposedException)
        {
            return Result.Failure<AsyncServerStreamingCall<TResponse>>(DaemonErrors.Unavailable);
        }
    }

    private static async Task<Result<bool>> MoveNextAsync<TResponse>(
        IAsyncStreamReader<TResponse> stream, CancellationToken cancellationToken)
    {
        try
        {
            return await stream.MoveNext(cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException exception)
        {
            return Result.Failure<bool>(exception.ToError());
        }
        catch (ObjectDisposedException)
        {
            return Result.Failure<bool>(DaemonErrors.Unavailable);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<bool>(DaemonErrors.Unavailable);
        }
    }
}
