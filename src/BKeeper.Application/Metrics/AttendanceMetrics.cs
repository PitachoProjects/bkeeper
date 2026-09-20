namespace BKeeper.Application.Metrics;

/// <summary>
/// Pure metric calculations used by the weekly feature builder (§5, §Week3 of the plan).
/// Kept dependency-free and deterministic so they can be unit-tested against hand-computed fixtures.
/// </summary>
public static class AttendanceMetrics
{
    /// <summary>Median gap in days between consecutive dates in a window (dates need not be sorted).</summary>
    public static double? MedianGapDays(IEnumerable<DateOnly> visitDates)
    {
        var ordered = visitDates.Distinct().OrderBy(d => d).ToList();
        if (ordered.Count < 2) return null;

        var gaps = new List<int>();
        for (var i = 1; i < ordered.Count; i++)
            gaps.Add(ordered[i].DayNumber - ordered[i - 1].DayNumber);

        gaps.Sort();
        var mid = gaps.Count / 2;
        return gaps.Count % 2 == 0 ? (gaps[mid - 1] + gaps[mid]) / 2.0 : gaps[mid];
    }

    /// <summary>Mean weekly visit rate over the given weekly visit counts (baseline_26w style).</summary>
    public static double BaselinePerWeek(IEnumerable<int> weeklyVisitCounts)
    {
        var list = weeklyVisitCounts.ToList();
        return list.Count == 0 ? 0 : list.Average();
    }

    /// <summary>Applies the seasonal factor (August/Christmas dips do not count fully against baseline).</summary>
    public static double ApplySeasonalFactor(double baseline, bool isAugust, bool isChristmasWeek)
    {
        if (isChristmasWeek) return baseline * 0.6;
        if (isAugust) return baseline * 0.8;
        return baseline;
    }

    /// <summary>Jensen-Shannon divergence between two categorical distributions (workout-type mix shift, R06).</summary>
    public static double JensenShannonDivergence(IReadOnlyDictionary<string, double> p, IReadOnlyDictionary<string, double> q)
    {
        var keys = p.Keys.Union(q.Keys).ToList();
        var pn = Normalize(p, keys);
        var qn = Normalize(q, keys);
        var m = keys.ToDictionary(k => k, k => (pn[k] + qn[k]) / 2.0);

        double Kl(Dictionary<string, double> a, Dictionary<string, double> b) =>
            keys.Where(k => a[k] > 0).Sum(k => a[k] * Math.Log(a[k] / b[k], 2));

        return (Kl(pn, m) + Kl(qn, m)) / 2.0;
    }

    private static Dictionary<string, double> Normalize(IReadOnlyDictionary<string, double> dist, List<string> keys)
    {
        var total = keys.Sum(k => dist.GetValueOrDefault(k, 0));
        return keys.ToDictionary(k => k, k => total <= 0 ? 0 : dist.GetValueOrDefault(k, 0) / total);
    }

    /// <summary>ISO week Monday for a given date (D10: week = Monday-Sunday).</summary>
    public static DateOnly IsoWeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7; // Monday=0 ... Sunday=6
        return date.AddDays(-diff);
    }
}
