using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Domain;

public class EntryLayoutTests
{
    private static TimeEntry Entry(int start, int end) => new() { StartMinute = start, EndMinute = end };

    [Fact]
    public void Compute_NonOverlapping_UsesSingleLane()
    {
        TimeEntry a = Entry(60, 120);
        TimeEntry b = Entry(120, 180);

        IReadOnlyDictionary<Guid, LaneAssignment> result = EntryLayout.Compute([a, b]);

        result[a.Id].Should().Be(new LaneAssignment(0, 1));
        result[b.Id].Should().Be(new LaneAssignment(0, 1));
    }

    [Fact]
    public void Compute_Overlapping_SplitsIntoLanes()
    {
        TimeEntry a = Entry(60, 180);
        TimeEntry b = Entry(90, 150);

        IReadOnlyDictionary<Guid, LaneAssignment> result = EntryLayout.Compute([a, b]);

        result[a.Id].Should().Be(new LaneAssignment(0, 2));
        result[b.Id].Should().Be(new LaneAssignment(1, 2));
    }

    [Fact]
    public void Compute_FreedLane_IsReused()
    {
        TimeEntry a = Entry(60, 120);
        TimeEntry b = Entry(90, 200);
        TimeEntry c = Entry(130, 180);

        IReadOnlyDictionary<Guid, LaneAssignment> result = EntryLayout.Compute([a, b, c]);

        result[c.Id].Lane.Should().Be(0);
        result[a.Id].LaneCount.Should().Be(2);
    }

    [Fact]
    public void Compute_SeparateClusters_HaveIndependentLaneCounts()
    {
        TimeEntry a = Entry(60, 120);
        TimeEntry b = Entry(90, 150);
        TimeEntry c = Entry(300, 360);

        IReadOnlyDictionary<Guid, LaneAssignment> result = EntryLayout.Compute([a, b, c]);

        result[c.Id].Should().Be(new LaneAssignment(0, 1));
    }
}
