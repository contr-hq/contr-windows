using Contr.Daemon.UI;
using Contr.UI.Contracts.Services.Daemon;
using Xunit;
using Wire = Contr.Sdk.Integration.V1;

namespace Contr.Daemon.UI.Tests;

public sealed class IntegrationClientTests
{
    [Theory]
    [InlineData(Wire.WslIntegrationState.Unspecified, WslIntegrationState.Disabled)]
    [InlineData(Wire.WslIntegrationState.Disabled, WslIntegrationState.Disabled)]
    [InlineData(Wire.WslIntegrationState.Pending, WslIntegrationState.Pending)]
    [InlineData(Wire.WslIntegrationState.Active, WslIntegrationState.Active)]
    [InlineData(Wire.WslIntegrationState.Failed, WslIntegrationState.Failed)]
    public void ToDistribution_MapsTheState(Wire.WslIntegrationState state, WslIntegrationState expected)
    {
        var distribution = IntegrationClient.ToDistribution(new Wire.WslDistribution {
            Name = "Ubuntu-24.04",
            WslVersion = 2,
            IsDefault = true,
            IntegrationState = state,
            Failure = "socket closed",
        });

        Assert.Equal(new WslDistribution("Ubuntu-24.04", 2, true, expected, "socket closed"), distribution);
    }
}
