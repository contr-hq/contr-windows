using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Contr.Modules.Containers.Application.Containers;
using Contr.UI.Common.Formatting;

namespace Contr.Modules.Containers.UI.ViewModels;

/// <summary>The latest CPU and memory sample of one container; every version of its row shares it.</summary>
public sealed partial class ContainerUsage : ObservableObject
{
    private const string _none = "—";

    [ObservableProperty]
    public partial string CpuText { get; set; }

    [ObservableProperty]
    public partial string MemoryText { get; set; }

    public ContainerUsage()
    {
        CpuText = _none;
        MemoryText = _none;
    }

    public void Show(ContainerStatsDto stats)
    {
        CpuText = $"{stats.CpuPercent.ToString("0.0", CultureInfo.CurrentCulture)}%";
        MemoryText = HumanFormat.Bytes(stats.MemoryUsageBytes);
    }

    public void Clear()
    {
        CpuText = _none;
        MemoryText = _none;
    }
}
