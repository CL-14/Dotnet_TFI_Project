using TfiBus.Core;

namespace TfiBus.Tests;

// Tests that times are shown in Irish local time, including the summer-time switch.
public class IrishTimeTests
{
    [Fact]
    public void FormatUnix_InWinter_IsTheSameAsUtc()
    {
        // 15 Jan 2027 12:00 UTC. Ireland uses GMT in winter, so the clock reads 12:00.
        long winterNoon = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        Assert.Equal("12:00", IrishTime.FormatUnix(winterNoon, "HH:mm"));
    }

    [Fact]
    public void FormatUnix_InSummer_IsOneHourAheadOfUtc()
    {
        // 15 Jul 2027 12:00 UTC. Irish Summer Time is UTC+1, so the clock reads 13:00.
        long summerNoon = new DateTimeOffset(2027, 7, 15, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        Assert.Equal("13:00", IrishTime.FormatUnix(summerNoon, "HH:mm"));
    }
}
