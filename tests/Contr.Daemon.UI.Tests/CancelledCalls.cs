using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contr.Daemon.UI;
using Contr.Sdk;
using Grpc.Core;
using NSubstitute;

namespace Contr.Daemon.UI.Tests;

/// <summary>A daemon client whose calls wait for their token, then fail as gRPC fails a call its token cancelled.
/// </summary>
internal sealed class CancelledCalls : IDisposable
{
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _failDelay = TimeSpan.FromMilliseconds(50);

    private readonly string _detail = $"Call cancelled {Guid.NewGuid():N}";
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _escaped;

    public CancelledCalls()
    {
        Daemon
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(call => FailOnCancelAsync(call.Arg<CancellationToken>()));
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public IDaemonClient Daemon { get; } = Substitute.For<IDaemonClient>();

    /// <summary>Completes once a watch has called the daemon.</summary>
    public Task Started => _started.Task.WaitAsync(_startTimeout);

    /// <summary>Whether a cancelled call's exception reached the finalizer without anyone observing it.</summary>
    public async Task<bool> EscapedAsync()
    {
        for (var attempt = 0; attempt < 20 && !_escaped; attempt++)
        {
            await Task.Delay(_failDelay);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return _escaped;
    }

    public void Dispose()
    {
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    private async ValueTask<ContrClient> FailOnCancelAsync(CancellationToken cancellationToken)
    {
        _started.TrySetResult();
        await Task
            .Delay(Timeout.Infinite, cancellationToken)
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        // gRPC fails a cancelled call later, on another thread, once the subscription has let go of the watch.
        await Task.Delay(_failDelay);

        throw new RpcException(new Status(StatusCode.Cancelled, _detail));
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _escaped |= e.Exception.InnerExceptions
            .OfType<RpcException>()
            .Any(exception => exception.Status.Detail == _detail);
    }
}
