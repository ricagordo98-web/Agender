using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Domain;

public class TimeSlotTests
{
    [Theory]
    [InlineData(60, 60)]
    [InlineData(90, 60)]
    [InlineData(-1, 30)]
    [InlineData(0, 1441)]
    public void Constructor_InvalidRange_Throws(int start, int end)
    {
        Action act = () => _ = new TimeSlot(start, end);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Duration_ValidRange_ReturnsMinutes()
    {
        new TimeSlot(540, 615).Duration.Should().Be(75);
    }

    [Theory]
    [InlineData(60, 120, 90, 150, true)]
    [InlineData(60, 120, 120, 180, false)]
    [InlineData(60, 120, 0, 60, false)]
    [InlineData(60, 180, 90, 120, true)]
    public void OverlapsWith_Slots_ReturnsExpected(int s1, int e1, int s2, int e2, bool expected)
    {
        new TimeSlot(s1, e1).OverlapsWith(new TimeSlot(s2, e2)).Should().Be(expected);
    }

    [Theory]
    [InlineData(62, 5, 60)]
    [InlineData(63, 5, 65)]
    [InlineData(67, 15, 60)]
    [InlineData(68, 15, 75)]
    [InlineData(61, 1, 61)]
    public void Snap_Minute_RoundsToNearestStep(int minute, int step, int expected)
    {
        TimeSlot.Snap(minute, step).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(75, "01:15")]
    [InlineData(1440, "24:00")]
    public void FormatMinute_Minute_ReturnsClockText(int minute, string expected)
    {
        TimeSlot.FormatMinute(minute).Should().Be(expected);
    }

    [Theory]
    [InlineData("09:30", true, 570)]
    [InlineData("00:00", true, 0)]
    [InlineData("", false, 0)]
    [InlineData("abc", false, 0)]
    public void TryParseClock_Text_ParsesMinutes(string text, bool expectedResult, int expectedMinute)
    {
        bool result = TimeSlot.TryParseClock(text, out int minute);

        result.Should().Be(expectedResult);
        minute.Should().Be(expectedMinute);
    }
}
