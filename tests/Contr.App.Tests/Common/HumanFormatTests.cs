using System;
using Contr.UI.Common.Formatting;
using Xunit;

namespace Contr.App.Tests.Common;

public sealed class HumanFormatTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1_500, "1.5 kB")]
    [InlineData(8_400_000, "8.4 MB")]
    [InlineData(192_000_000, "192 MB")]
    [InlineData(12_340_000_000, "12.34 GB")]
    [InlineData(-1, "")]
    public void Bytes_UsesDecimalUnits(long bytes, string expected)
    {
        Assert.Equal(expected, HumanFormat.Bytes(bytes));
    }

    [Theory]
    [InlineData(0, "Just now")]
    [InlineData(1, "1 minute ago")]
    [InlineData(150, "2 hours ago")]
    [InlineData(60 * 24 * 3, "3 days ago")]
    [InlineData(60 * 24 * 21, "3 weeks ago")]
    [InlineData(60 * 24 * 100, "3 months ago")]
    [InlineData(60 * 24 * 800, "2 years ago")]
    public void Age_CountsTheLargestWholeUnit(int minutesAgo, string expected)
    {
        Assert.Equal(expected, HumanFormat.Age(_now.AddMinutes(-minutesAgo), _now));
    }
}
