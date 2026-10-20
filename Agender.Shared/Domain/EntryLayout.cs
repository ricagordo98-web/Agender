namespace Agender.Shared.Domain;

public static class EntryLayout
{
    public static IReadOnlyDictionary<Guid, LaneAssignment> Compute(IEnumerable<TimeEntry> entries)
    {
        Dictionary<Guid, LaneAssignment> result = [];
        List<(TimeEntry Entry, int Lane)> cluster = [];
        List<int> laneEnds = [];
        int clusterEnd = 0;

        foreach (TimeEntry entry in entries.OrderBy(e => e.StartMinute).ThenBy(e => e.EndMinute))
        {
            if (cluster.Count > 0 && entry.StartMinute >= clusterEnd)
            {
                Flush(cluster, laneEnds.Count, result);
                laneEnds.Clear();
            }

            int lane = laneEnds.FindIndex(end => end <= entry.StartMinute);
            if (lane < 0)
            {
                laneEnds.Add(entry.EndMinute);
                lane = laneEnds.Count - 1;
            }
            else
            {
                laneEnds[lane] = entry.EndMinute;
            }

            clusterEnd = cluster.Count == 0 ? entry.EndMinute : Math.Max(clusterEnd, entry.EndMinute);
            cluster.Add((entry, lane));
        }

        Flush(cluster, laneEnds.Count, result);
        return result;
    }

    private static void Flush(
        List<(TimeEntry Entry, int Lane)> cluster,
        int laneCount,
        Dictionary<Guid, LaneAssignment> result)
    {
        foreach ((TimeEntry entry, int lane) in cluster)
        {
            result[entry.Id] = new LaneAssignment(lane, laneCount);
        }

        cluster.Clear();
    }
}
