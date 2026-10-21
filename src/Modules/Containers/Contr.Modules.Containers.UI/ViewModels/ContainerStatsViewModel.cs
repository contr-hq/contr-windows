using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Application.Abstractions.Messaging;
using Contr.Modules.Containers.Application.Containers;
using Contr.Modules.Containers.Application.Containers.Queries.StreamContainerStats;
using Contr.Modules.Containers.Domain;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The stats tab: charts of a running container's samples since the tab opened.</summary>
/// <remarks>Use it on the UI thread: samples resume on the captured context.</remarks>
public sealed partial class ContainerStatsViewModel : ObservableObject, IDisposable
{
    /// <summary>The engine sends about one sample per second; the longest window holds fifteen minutes.</summary>
    private const int _capacity = 900;

    private static readonly int[] _windowSeconds = [60, 300, 900];

    private readonly ISender _sender;
    private readonly List<ContainerStatsDto> _samples = [];
    private CancellationTokenSource? _stream;
    private ContainerId? _sampled;

    /// <summary>0 shows the last minute, 1 the last five minutes, 2 the last fifteen.</summary>
    [ObservableProperty]
    public partial int WindowIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSamples))]
    public partial bool HasSamples { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double> Cpu { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double> Memory { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double> Received { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double> Sent { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double> Read { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double> Written { get; set; }

    [ObservableProperty]
    public partial double NetworkMaximum { get; set; }

    [ObservableProperty]
    public partial double DiskMaximum { get; set; }

    [ObservableProperty]
    public partial string CpuText { get; set; }

    [ObservableProperty]
    public partial string CpuDetail { get; set; }

    [ObservableProperty]
    public partial string MemoryText { get; set; }

    [ObservableProperty]
    public partial string MemoryDetail { get; set; }

    /// <summary>The latest memory use as a percentage of the limit; zero without one.</summary>
    [ObservableProperty]
    public partial double MemoryShare { get; set; }

    [ObservableProperty]
    public partial string NetworkText { get; set; }

    [ObservableProperty]
    public partial string NetworkDetail { get; set; }

    [ObservableProperty]
    public partial string DiskText { get; set; }

    [ObservableProperty]
    public partial string DiskDetail { get; set; }

    public ContainerStatsViewModel(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
        WindowIndex = 1;
        Cpu = [];
        Memory = [];
        Received = [];
        Sent = [];
        Read = [];
        Written = [];
        CpuText = string.Empty;
        CpuDetail = string.Empty;
        MemoryText = string.Empty;
        MemoryDetail = string.Empty;
        NetworkText = string.Empty;
        NetworkDetail = string.Empty;
        DiskText = string.Empty;
        DiskDetail = string.Empty;
    }

    public bool HasNoSamples => !HasSamples;

    /// <summary>Samples <paramref name="id"/> until stopped; another container starts a new history.</summary>
    public void Start(ContainerId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_stream is not null && id == _sampled)
        {
            return;
        }

        Stop();
        _sampled = id;
        _samples.Clear();
        Error = null;
        Show();
        _stream = new CancellationTokenSource();
        _ = SampleAsync(id, _stream.Token);
    }

    public void Stop()
    {
        _stream?.Cancel();
        _stream?.Dispose();
        _stream = null;
        _sampled = null;
    }

    public void Dispose()
    {
        Stop();
    }

    /// <summary>Adds a sample and redraws; the stream calls it once per sample.</summary>
    public void Add(ContainerStatsDto sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        _samples.Add(sample);

        if (_samples.Count > _capacity)
        {
            _samples.RemoveAt(0);
        }

        Show();
    }

    partial void OnWindowIndexChanged(int value)
    {
        Show();
    }

    /// <summary>A counter's per-second change between consecutive samples; a reset counter reads as zero.</summary>
    private static List<double> Rates(List<ContainerStatsDto> window, Func<ContainerStatsDto, long> counter)
    {
        var rates = new List<double>(Math.Max(0, window.Count - 1));
        for (var index = 1; index < window.Count; index++)
        {
            var elapsed = window[index].ReadAt - window[index - 1].ReadAt;
            var change = counter(window[index]) - counter(window[index - 1]);
            rates.Add(elapsed <= TimeSpan.Zero || change < 0 ? 0 : change / elapsed.TotalSeconds);
        }

        return rates;
    }

    private static string ShareOfLimit(ContainerStatsDto sample)
    {
        return Percent(100.0 * sample.MemoryUsageBytes / sample.MemoryLimitBytes);
    }

    private static string Rate(double bytesPerSecond)
    {
        return $"{HumanFormat.Bytes((long)bytesPerSecond)}/s";
    }

    private static string Percent(double value)
    {
        return $"{value.ToString("0.0", CultureInfo.CurrentCulture)}%";
    }

    private async Task SampleAsync(ContainerId id, CancellationToken cancellationToken)
    {
        try
        {
            var events = await _sender.Send(new StreamContainerStatsQuery(id), cancellationToken);
            await foreach (var statsEvent in events.WithCancellation(cancellationToken))
            {
                switch (statsEvent)
                {
                    case ContainerStatsEvent.Sample sample:
                        Add(sample.Stats);
                        break;
                    case ContainerStatsEvent.Failed failed:
                        Error = failed.Error.Message;
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The tab closed or the container stopped.
        }
    }

    private void Show()
    {
        var window = _samples
            .TakeLast(_windowSeconds[Math.Clamp(WindowIndex, 0, _windowSeconds.Length - 1)])
            .ToList();
        HasSamples = window.Count > 0;

        if (window.Count == 0)
        {
            return;
        }

        var latest = window[^1];
        Cpu = [.. window.Select(sample => sample.CpuPercent)];
        Memory = [.. window.Select(sample => (double)sample.MemoryUsageBytes)];
        CpuText = Percent(latest.CpuPercent);
        CpuDetail = $"Peak {Percent(Cpu.Max())} · 100% is one full core";
        MemoryText = HumanFormat.Bytes(latest.MemoryUsageBytes);
        MemoryShare = latest.MemoryLimitBytes > 0 ? 100.0 * latest.MemoryUsageBytes / latest.MemoryLimitBytes : 0;
        MemoryDetail = latest.MemoryLimitBytes > 0
            ? $"Of {HumanFormat.Bytes(latest.MemoryLimitBytes)} · {ShareOfLimit(latest)}"
            : string.Empty;

        Received = Rates(window, sample => sample.NetworkRxBytes);
        Sent = Rates(window, sample => sample.NetworkTxBytes);
        Read = Rates(window, sample => sample.BlockReadBytes);
        Written = Rates(window, sample => sample.BlockWriteBytes);
        NetworkMaximum = Received
            .Concat(Sent)
            .DefaultIfEmpty(0)
            .Max();
        DiskMaximum = Read
            .Concat(Written)
            .DefaultIfEmpty(0)
            .Max();

        NetworkText = $"↓ {Rate(Received.LastOrDefault())} · ↑ {Rate(Sent.LastOrDefault())}";
        NetworkDetail = $"Since start: received {HumanFormat.Bytes(latest.NetworkRxBytes)}"
            + $" · sent {HumanFormat.Bytes(latest.NetworkTxBytes)}";
        DiskText = $"Read {Rate(Read.LastOrDefault())} · Write {Rate(Written.LastOrDefault())}";
        DiskDetail = $"Since start: read {HumanFormat.Bytes(latest.BlockReadBytes)}"
            + $" · written {HumanFormat.Bytes(latest.BlockWriteBytes)}";
    }
}
