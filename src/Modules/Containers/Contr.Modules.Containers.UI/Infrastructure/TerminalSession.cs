using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Contr.Application.Abstractions.Daemon;
using Contr.Daemon.UI;
using Contr.Modules.Containers.Application.Abstractions;
using Contr.Modules.Containers.Application.Containers;
using Contr.Sdk;
using Contr.SharedKernel;
using Google.Protobuf;
using Grpc.Core;
using ContainersV1 = Contr.Sdk.Containers.V1;
using OutputCase = Contr.Sdk.Containers.V1.ExecResponse.OutputOneofCase;

namespace Contr.Modules.Containers.UI.Infrastructure;

/// <summary>An <see cref="ITerminalSession"/> over the daemon's Exec call.</summary>
/// <remarks>gRPC allows one write at a time on a request stream; writes queue on a lock.</remarks>
internal sealed class TerminalSession : ITerminalSession
{
    private readonly AsyncDuplexStreamingCall<ContainersV1.ExecRequest, ContainersV1.ExecResponse> _call;
    private readonly SemaphoreSlim _writes = new(1, 1);
    private int _disposed;

    private TerminalSession(AsyncDuplexStreamingCall<ContainersV1.ExecRequest, ContainersV1.ExecResponse> call)
    {
        _call = call;
    }

    public static async Task<Result<ITerminalSession>> OpenAsync(
        IDaemonClient daemon, ContainersV1.ExecStart start, CancellationToken cancellationToken)
    {
        try
        {
            var client = await daemon.GetAsync(cancellationToken).ConfigureAwait(false);

            // The session outlives the open call; disposing the session cancels the call.
            var call = client.Containers.Exec();
            await call.RequestStream
                .WriteAsync(new ContainersV1.ExecRequest { Start = start }, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success<ITerminalSession>(new TerminalSession(call));
        }
        catch (ContrException exception)
        {
            return Result.Failure<ITerminalSession>(exception.ToError());
        }
        catch (RpcException exception)
        {
            return Result.Failure<ITerminalSession>(exception.ToError());
        }
    }

    public async IAsyncEnumerable<TerminalOutput> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            var next = await MoveNextAsync(cancellationToken).ConfigureAwait(false);

            if (next.IsFailure)
            {
                yield return new TerminalOutput.Failed(next.Error);
                yield break;
            }

            if (!next.Value)
            {
                yield break;
            }

            var response = _call.ResponseStream.Current;
            switch (response.OutputCase)
            {
                case OutputCase.Stdout:
                    yield return new TerminalOutput.Data(response.Stdout.ToByteArray());
                    break;
                case OutputCase.Stderr:
                    yield return new TerminalOutput.Data(response.Stderr.ToByteArray());
                    break;
                case OutputCase.Exited:
                    yield return new TerminalOutput.Exited(response.Exited.ExitCode);
                    yield break;
            }
        }
    }

    public Task<Result> WriteAsync(byte[] input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        return SendAsync(new ContainersV1.ExecRequest { Stdin = ByteString.CopyFrom(input) }, cancellationToken);
    }

    public Task<Result> ResizeAsync(int columns, int rows, CancellationToken cancellationToken)
    {
        var request = new ContainersV1.ExecRequest { Resize = ContainersGateway.Size(columns, rows) };

        return SendAsync(request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await SendAsync(
                new ContainersV1.ExecRequest { CloseStdin = new ContainersV1.ExecCloseStdin() },
                CancellationToken.None)
            .ConfigureAwait(false);
        _call.Dispose();
        _writes.Dispose();
    }

    private async Task<Result<bool>> MoveNextAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Cancelled)
        {
            return false;
        }
        catch (RpcException exception)
        {
            return Result.Failure<bool>(exception.ToError());
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            return false;
        }
    }

    private async Task<Result> SendAsync(ContainersV1.ExecRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _writes.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _call.RequestStream.WriteAsync(request, cancellationToken).ConfigureAwait(false);

                return Result.Success();
            }
            finally
            {
                _writes.Release();
            }
        }
        catch (RpcException exception)
        {
            return Result.Failure(exception.ToError());
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException
            or OperationCanceledException)
        {
            return Result.Failure(DaemonErrors.Unavailable);
        }
    }
}
