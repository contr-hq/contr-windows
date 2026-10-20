using System.Linq;
using Contr.App.UI.Onboarding;
using Contr.UI.Common.Controls;
using Xunit;

namespace Contr.App.Tests.UI;

public sealed class OnboardingViewModelTests
{
    [Fact]
    public void CheckThisPc_PassesAReadyPc()
    {
        var checks = OnboardingViewModel.CheckThisPc(26100, virtualization: true, 112_000_000_000, "C:");

        Assert.All(checks, check => Assert.Equal(StatusKind.Good, check.Kind));
        var disk = checks.Single(check => check.Name == "Free disk space");
        Assert.Equal("112 GB free on C:", disk.Detail);
        Assert.Equal("At least 10 GB for images", disk.Requirement);
    }

    [Fact]
    public void CheckThisPc_FailsVirtualizationTurnedOffAndWarnsOfLowDisk()
    {
        var checks = OnboardingViewModel.CheckThisPc(26100, virtualization: false, 4_000_000_000, "C:");

        Assert.Equal(StatusKind.Critical, checks.Single(check => check.Name == "Virtualization").Kind);
        Assert.Equal(StatusKind.Caution, checks.Single(check => check.Name == "Free disk space").Kind);
    }

    [Fact]
    public void CheckThisPc_LeavesVirtualizationToTheEngineWhereItCannotTell()
    {
        var checks = OnboardingViewModel.CheckThisPc(26100, virtualization: null, 112_000_000_000, "C:");

        Assert.Equal(StatusKind.Neutral, checks.Single(check => check.Name == "Virtualization").Kind);
    }
}
