namespace BKeeper.Application.Dashboards;

public record CohortMember(DateOnly JoinDate, DateOnly? CancelDate);
public record CohortCurve(string CohortLabel, int CohortSize, IReadOnlyList<double?> RetentionByMonth);
public record HistogramBucket(string Label, int Count);

/// <summary>
/// Pure retention/cohort math (plan §9): grouped-by-join-month retention curves and a
/// tenure-at-churn histogram. No I/O — the caller (a dashboard endpoint) supplies the raw
/// member facts already loaded from the database.
/// </summary>
public static class CohortAnalysis
{
    /// <summary>For each join-month cohort, the fraction still active N months after joining, for N = 0..maxMonths.</summary>
    public static IReadOnlyList<CohortCurve> BuildCohorts(IReadOnlyList<CohortMember> members, DateOnly asOf, int maxMonths = 12)
    {
        var cohorts = members
            .GroupBy(m => new DateOnly(m.JoinDate.Year, m.JoinDate.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var cohortMonth = g.Key;
                var monthsSinceCohortStart = MonthsBetween(cohortMonth, asOf);
                var curve = new List<double?>();

                for (var m = 0; m <= maxMonths; m++)
                {
                    if (m > monthsSinceCohortStart) { curve.Add(null); continue; } // not enough time has passed yet

                    var checkpoint = cohortMonth.AddMonths(m + 1); // end of month m (exclusive)
                    var retained = g.Count(x => x.CancelDate is null || x.CancelDate >= checkpoint);
                    curve.Add(g.Count() == 0 ? null : (double)retained / g.Count());
                }

                return new CohortCurve($"{cohortMonth:yyyy-MM}", g.Count(), curve);
            })
            .ToList();

        return cohorts;
    }

    public static IReadOnlyList<HistogramBucket> TenureAtChurnHistogram(IReadOnlyList<(DateOnly JoinDate, DateOnly CancelDate)> churned, int bucketMonths = 1, int maxBuckets = 24)
    {
        var buckets = new int[maxBuckets + 1]; // last bucket = overflow ("N+ months")

        foreach (var (join, cancel) in churned)
        {
            var months = MonthsBetween(new DateOnly(join.Year, join.Month, 1), cancel);
            var bucketIndex = Math.Min(months / bucketMonths, maxBuckets);
            buckets[bucketIndex]++;
        }

        var result = new List<HistogramBucket>();
        for (var i = 0; i < maxBuckets; i++)
        {
            var lo = i * bucketMonths;
            var hi = lo + bucketMonths;
            result.Add(new HistogramBucket($"{lo}-{hi}mo", buckets[i]));
        }
        result.Add(new HistogramBucket($"{maxBuckets * bucketMonths}mo+", buckets[maxBuckets]));
        return result;
    }

    private static int MonthsBetween(DateOnly from, DateOnly to) =>
        Math.Max(0, (to.Year - from.Year) * 12 + to.Month - from.Month);
}
