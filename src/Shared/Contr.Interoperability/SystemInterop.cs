using System.Runtime.Intrinsics.X86;
using Windows.Win32;
using Windows.Win32.System.Threading;

namespace Contr.Interoperability;

/// <summary>Facts about this PC the setup checks before the engine needs them.</summary>
public static class SystemInterop
{
    /// <summary>The CPUID leaf 1 ECX bit a hypervisor sets for the operating system it runs.</summary>
    private const int _hypervisorPresentBit = 31;

    /// <summary>
    /// Whether virtualization is on: a hypervisor runs Windows, or the firmware enables virtualization; null on a
    /// processor this cannot tell for.
    /// </summary>
    /// <remarks>
    /// Windows hides the firmware flag from itself once a hypervisor runs it, so the hypervisor bit is read first.
    /// </remarks>
    public static bool? IsVirtualizationEnabled()
    {
        if (!X86Base.IsSupported)
        {
            return null;
        }

        var (_, _, ecx, _) = X86Base.CpuId(1, 0);

        if ((ecx & (1 << _hypervisorPresentBit)) != 0)
        {
            return true;
        }

        return PInvoke.IsProcessorFeaturePresent(PROCESSOR_FEATURE_ID.PF_VIRT_FIRMWARE_ENABLED);
    }
}
