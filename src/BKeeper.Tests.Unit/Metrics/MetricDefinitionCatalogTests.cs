using BKeeper.Application.Metrics;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BKeeper.Tests.Unit.Metrics;

/// <summary>
/// Guards the metric definition registry against drift: every key this test lists is a metric
/// DashboardsController actually returns (retention/alerts/workouts DTOs) or a member-level input
/// AttendanceMetrics/MetricsBuilder actually compute (R01-R04's rate/median inputs) — if a new
/// dashboard number ships without a matching catalog entry, or a key gets renamed/removed from the
/// catalog, this fails.
/// </summary>
public class MetricDefinitionCatalogTests
{
    /// <summary>Keys DashboardsController's RetentionOverviewDto, AlertOperationsDto and WorkoutMixDto surface.</summary>
    private static readonly string[] DashboardKeys =
    [
        "active_members",
        "new_members_this_month",
        "churned_members_this_month",
        "net_member_change",
        "monthly_churn_rate_pct",
        "lapsed_members",
        "cohort_retention_curve",
        "tenure_at_churn_histogram",
        "alert_sla_compliance_rate_pct",
        "alert_avg_time_to_claim_hours",
        "alert_save_rate_pct",
        "alert_return_rate_holdout_vs_treated_pct",
        "workout_window_type_mix",
        "class_fill_rate_pct",
    ];

    /// <summary>Keys for the member-level rule inputs computed in AttendanceMetrics/MetricsBuilder (R01-R04).</summary>
    private static readonly string[] AttendanceMetricKeys =
    [
        "member_baseline_visits_per_week",
        "member_recent_attendance_ratio",
        "member_no_show_rate_8w",
        "member_median_gap_days",
        "member_median_booking_gap_days",
    ];

    [Fact]
    public void Catalog_ContainsEveryDashboardMetric()
    {
        var keys = MetricDefinitionCatalog.All.Select(d => d.Key).ToHashSet();
        foreach (var expected in DashboardKeys)
            Assert.Contains(expected, keys);
    }

    [Fact]
    public void Catalog_ContainsEveryAttendanceRuleMetric()
    {
        var keys = MetricDefinitionCatalog.All.Select(d => d.Key).ToHashSet();
        foreach (var expected in AttendanceMetricKeys)
            Assert.Contains(expected, keys);
    }

    [Fact]
    public void Catalog_KeysAreUnique()
    {
        var keys = MetricDefinitionCatalog.All.Select(d => d.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Catalog_IdsAreUnique()
    {
        var ids = MetricDefinitionCatalog.All.Select(d => d.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllDefinitions))]
    public void Definition_HasAllExplainThisFields(string key)
    {
        var definition = MetricDefinitionCatalog.All.Single(d => d.Key == key);

        Assert.False(string.IsNullOrWhiteSpace(definition.Name));
        Assert.False(string.IsNullOrWhiteSpace(definition.Category));
        Assert.False(string.IsNullOrWhiteSpace(definition.Unit));
        Assert.False(string.IsNullOrWhiteSpace(definition.Aggregation));
        Assert.False(string.IsNullOrWhiteSpace(definition.TimeWindow));
        Assert.False(string.IsNullOrWhiteSpace(definition.Description));
        Assert.False(string.IsNullOrWhiteSpace(definition.Formula));
        Assert.False(string.IsNullOrWhiteSpace(definition.WhyItMatters));
        Assert.False(string.IsNullOrWhiteSpace(definition.Limitations));
        Assert.True(definition.IsActive);
        Assert.True(definition.Version >= 1);
    }

    public static IEnumerable<object[]> AllDefinitions() =>
        MetricDefinitionCatalog.All.Select(d => new object[] { d.Key });

    /// <summary>The catalog's HasData seed actually lands in the database and is queryable, box-unscoped.</summary>
    [Fact]
    public async Task Seed_IsQueryableThroughDbContext()
    {
        var options = new DbContextOptionsBuilder<BKeeperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new BKeeperDbContext(options, new CurrentBoxAccessor());
        await db.Database.EnsureCreatedAsync();

        var seeded = await db.MetricDefinitions.ToListAsync();

        Assert.Equal(MetricDefinitionCatalog.All.Count, seeded.Count);
        Assert.Contains(seeded, d => d.Key == "lapsed_members");
        var lapsed = seeded.Single(d => d.Key == "lapsed_members");
        Assert.Equal(MetricDefinitionCatalog.All.Single(d => d.Key == "lapsed_members").Formula, lapsed.Formula);
    }
}
