namespace BKeeper.Application.Dashboards;

public record CohortCurveDto(string CohortLabel, int CohortSize, List<double?> RetentionByMonth);
public record HistogramBucketDto(string Label, int Count);

public record RetentionOverviewDto(
    int ActiveCount, int NewThisMonth, int ChurnedThisMonth, int NetChange, double MonthlyChurnRatePct,
    int LapsedCount, List<CohortCurveDto> Cohorts, List<HistogramBucketDto> TenureAtChurnHistogram);
