using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Domain;

public class TimerSplitterTests
{
    [Fact]
    public void Split_SameDay_ReturnsOneSegmentWithTruncatedMinutes()
    {
        IReadOnlyList<TimerSegment> result = TimerSplitter.Split(new DateTime(2026, 10, 14, 9, 0, 40), new DateTime(2026, 10, 14, 10, 30, 59));

        result.Should().Equal(new TimerSegment(new DateOnly(2026, 10, 14), 540, 630));
    }

    [Fact]
    public void Split_UnderOneMinute_RoundsUpToOneMinute()
    {
        IReadOnlyList<TimerSegment> result = TimerSplitter.Split(new DateTime(2026, 10, 14, 9, 0, 10), new DateTime(2026, 10, 14, 9, 0, 40));

        result.Should().Equal(new TimerSegment(new DateOnly(2026, 10, 14), 540, 541));
    }

    [Fact]
    public void Split_UnderOneMinuteAtLastMinuteOfDay_EndsAtMidnight()
    {
        IReadOnlyList<TimerSegment> result = TimerSplitter.Split(new DateTime(2026, 10, 14, 23, 59, 10), new DateTime(2026, 10, 14, 23, 59, 50));

        result.Should().Equal(new TimerSegment(new DateOnly(2026, 10, 14), 1439, 1440));
    }

    [Fact]
    public void Split_CrossesMidnight_ReturnsOneSegmentPerDay()
    {
        IReadOnlyList<TimerSegment> result = TimerSplitter.Split(new DateTime(2026, 10, 14, 22, 0, 0), new DateTime(2026, 10, 15, 1, 30, 0));

        result.Should().Equal(
            new TimerSegment(new DateOnly(2026, 10, 14), 1320, 1440),
            new TimerSegment(new DateOnly(2026, 10, 15), 0, 90));
    }

    [Fact]
    public void Split_SeveralDays_FillsTheFullDaysInBetween()
    {
        IReadOnlyList<TimerSegment> result = TimerSplitter.Split(new DateTime(2026, 10, 14, 23, 0, 0), new DateTime(2026, 10, 16, 0, 30, 0));

        result.Should().Equal(
            new TimerSegment(new DateOnly(2026, 10, 14), 1380, 1440),
            new TimerSegment(new DateOnly(2026, 10, 15), 0, 1440),
            new TimerSegment(new DateOnly(2026, 10, 16), 0, 30));
    }

    [Fact]
    public void Split_StoppedExactlyAtMidnight_DropsTheEmptyLastDay()
    {
        IReadOnlyList<TimerSegment> result = TimerSplitter.Split(new DateTime(2026, 10, 14, 23, 0, 0), new DateTime(2026, 10, 15, 0, 0, 30));

        result.Should().Equal(new TimerSegment(new DateOnly(2026, 10, 14), 1380, 1440));
    }

    [Fact]
    public void Split_EndBeforeStart_BehavesAsAnInstantStop()
    {
        IReadOnlyList<TimerSegment> result = TimerSplitter.Split(new DateTime(2026, 10, 14, 9, 0, 0), new DateTime(2026, 10, 14, 8, 0, 0));

        result.Should().Equal(new TimerSegment(new DateOnly(2026, 10, 14), 540, 541));
    }
}
