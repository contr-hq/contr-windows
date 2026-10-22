using System;
using Contr.Daemon.UI;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using Wire = Contr.Sdk.Updates.V1;

namespace Contr.Daemon.UI.Tests;

public sealed class UpdatesClientTests
{
    [Fact]
    public void ToStatus_ReadsTheReleaseAndWhenItChecked()
    {
        var checkedAt = new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);
        var status = new Wire.UpdateStatus {
            CurrentVersion = "0.1.0-alpha.2",
            CheckedAt = Timestamp.FromDateTimeOffset(checkedAt),
            Available = new Wire.Release {
                Version = "0.2.0",
                Url = "https://github.com/contr-hq/contr/releases/tag/v0.2.0",
                Notes = "## Fixes",
            },
        };

        var mapped = UpdatesClient.ToStatus(status);

        Assert.Equal(
            ("0.1.0-alpha.2", checkedAt, string.Empty), (mapped.CurrentVersion, mapped.CheckedAt, mapped.Error));
        Assert.Equal("0.2.0", mapped.Available?.Version);
        Assert.Equal(new Uri("https://github.com/contr-hq/contr/releases/tag/v0.2.0"), mapped.Available?.Url);
        Assert.Null(mapped.Available?.PublishedAt);
    }

    [Fact]
    public void ToStatus_KeepsOnlyAnHttpsReleasePage()
    {
        var status = new Wire.UpdateStatus {
            Available = new Wire.Release { Version = "0.2.0", Url = "file:///C:/Windows/notepad.exe" },
        };

        Assert.Null(UpdatesClient.ToStatus(status).Available?.Url);
        Assert.Null(UpdatesClient.ToStatus(new Wire.UpdateStatus { Error = "offline" }).Available);
    }
}
