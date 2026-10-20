using Agender.Shared.Components;
using FluentAssertions;

namespace Agender.Tests.Domain;

public class TimeTextTests
{
    [Fact]
    public void Elapsed_HoursMinutesSeconds_FormatsWithLeadingZeros()
    {
        TimeText.Elapsed(new TimeSpan(1, 5, 9)).Should().Be("01:05:09");
    }

    [Fact]
    public void Elapsed_MoreThanADay_KeepsCountingHours()
    {
        TimeText.Elapsed(new TimeSpan(1, 2, 3, 4)).Should().Be("26:03:04");
    }

    [Fact]
    public void Elapsed_Negative_ClampsToZero()
    {
        TimeText.Elapsed(TimeSpan.FromSeconds(-5)).Should().Be("00:00:00");
    }
}
