using Contr.Modules.Containers.Domain;
using Xunit;

namespace Contr.Modules.Containers.Domain.Tests;

public sealed class ContainerStatusTextTests
{
    [Theory]
    [InlineData("Up 2 hours (healthy)", ContainerHealth.Healthy)]
    [InlineData("Up 2 hours (unhealthy)", ContainerHealth.Unhealthy)]
    [InlineData("Up 3 seconds (health: starting)", ContainerHealth.Starting)]
    [InlineData("Up 2 hours", ContainerHealth.None)]
    public void Health_ReadsTheHealthSuffix(string status, ContainerHealth expected)
    {
        Assert.Equal(expected, ContainerStatusText.Health(status));
    }

    [Theory]
    [InlineData("Exited (1) 14 minutes ago", 1)]
    [InlineData("Exited (0) 3 days ago", 0)]
    [InlineData("Exited (-1) 3 days ago", -1)]
    [InlineData("Up 2 hours", null)]
    [InlineData("Created", null)]
    public void ExitCode_ReadsOnlyExitedStatuses(string status, int? expected)
    {
        Assert.Equal(expected, ContainerStatusText.ExitCode(status));
    }

    [Theory]
    [InlineData("Up 2 hours", "2 h")]
    [InlineData("Up 2 hours (healthy)", "2 h")]
    [InlineData("Up 5 hours (Paused)", "5 h")]
    [InlineData("Up About an hour", "1 h")]
    [InlineData("Up About a minute", "1 min")]
    [InlineData("Up Less than a second", "now")]
    [InlineData("Up 1 second", "1 s")]
    [InlineData("Exited (1) 14 minutes ago", "14 min")]
    [InlineData("Exited (0) 3 days ago", "3 d")]
    [InlineData("Restarting (1) 5 seconds ago", "5 s")]
    [InlineData("Up 2 weeks", "2 wk")]
    [InlineData("Created", "")]
    [InlineData("Removal In Progress", "")]
    public void Elapsed_ShortensTheDuration(string status, string expected)
    {
        Assert.Equal(expected, ContainerStatusText.Elapsed(status));
    }
}
