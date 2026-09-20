using BKeeper.Application.Dashboards;
using Xunit;

namespace BKeeper.Tests.Unit.Dashboards;

public class CohortAnalysisTests
{
    [Fact]
    public void BuildCohorts_GroupsByJoinMonth()
    {
        var members = new List<CohortMember>
        {
            new(new DateOnly(2026, 1, 5), null),
            new(new DateOnly(2026, 1, 20), null),
            new(new DateOnly(2026, 2, 3), null),
        };

        var cohorts = CohortAnalysis.BuildCohorts(members, new DateOnly(2026, 3, 1));

        Assert.Equal(2, cohorts.Count);
        Assert.Equal("2026-01", cohorts[0].CohortLabel);
        Assert.Equal(2, cohorts[0].CohortSize);
        Assert.Equal("2026-02", cohorts[1].CohortLabel);
        Assert.Equal(1, cohorts[1].CohortSize);
    }

    [Fact]
    public void BuildCohorts_Month0RetentionIsAlways100Percent()
    {
        var members = new List<CohortMember> { new(new DateOnly(2026, 1, 1), null), new(new DateOnly(2026, 1, 15), null) };
        var cohorts = CohortAnalysis.BuildCohorts(members, new DateOnly(2026, 1, 20));
        Assert.Equal(1.0, cohorts[0].RetentionByMonth[0]);
    }

    [Fact]
    public void BuildCohorts_CancelledMemberDropsOutOfLaterMonths()
    {
        var members = new List<CohortMember>
        {
            new(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 15)), // cancels partway through month 1
            new(new DateOnly(2026, 1, 1), null),
        };

        var cohorts = CohortAnalysis.BuildCohorts(members, new DateOnly(2026, 4, 1));

        Assert.Equal(1.0, cohorts[0].RetentionByMonth[0]); // month 0: both still in
        Assert.Equal(0.5, cohorts[0].RetentionByMonth[2]); // month 2: one has cancelled by then
    }

    [Fact]
    public void BuildCohorts_FutureMonthsNotYetElapsed_AreNull()
    {
        var members = new List<CohortMember> { new(new DateOnly(2026, 1, 1), null) };
        var cohorts = CohortAnalysis.BuildCohorts(members, new DateOnly(2026, 1, 10), maxMonths: 6);

        Assert.Equal(1.0, cohorts[0].RetentionByMonth[0]);
        Assert.Null(cohorts[0].RetentionByMonth[3]); // 3 months haven't happened yet
    }

    [Fact]
    public void TenureAtChurnHistogram_BucketsByMonthsSinceJoin()
    {
        var churned = new List<(DateOnly JoinDate, DateOnly CancelDate)>
        {
            (new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 20)),  // ~0 months
            (new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 5)),   // ~2 months
            (new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 20)),  // ~2 months
        };

        var histogram = CohortAnalysis.TenureAtChurnHistogram(churned, bucketMonths: 1, maxBuckets: 5);

        Assert.Equal(1, histogram[0].Count); // 0-1mo
        Assert.Equal(2, histogram[2].Count); // 2-3mo
    }

    [Fact]
    public void TenureAtChurnHistogram_OverflowGoesToLastBucket()
    {
        var churned = new List<(DateOnly JoinDate, DateOnly CancelDate)> { (new DateOnly(2020, 1, 1), new DateOnly(2026, 1, 1)) };
        var histogram = CohortAnalysis.TenureAtChurnHistogram(churned, bucketMonths: 1, maxBuckets: 5);

        Assert.Equal(1, histogram[^1].Count);
        Assert.Equal("5mo+", histogram[^1].Label);
    }
}
