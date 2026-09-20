using BKeeper.Application.Metrics;
using Xunit;

namespace BKeeper.Tests.Unit.Metrics;

public class AttendanceMetricsTests
{
    [Fact]
    public void MedianGapDays_OddGapCount_ReturnsMiddleValue()
    {
        var dates = new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 8), new DateOnly(2026, 1, 9) };
        // gaps: 2, 5, 1 -> sorted 1,2,5 -> middle = 2
        Assert.Equal(2, AttendanceMetrics.MedianGapDays(dates));
    }

    [Fact]
    public void MedianGapDays_EvenGapCount_AveragesMiddleTwo()
    {
        var dates = new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 8) };
        // gaps: 2, 5 -> average = 3.5
        Assert.Equal(3.5, AttendanceMetrics.MedianGapDays(dates));
    }

    [Fact]
    public void MedianGapDays_FewerThanTwoDates_ReturnsNull()
    {
        Assert.Null(AttendanceMetrics.MedianGapDays([new DateOnly(2026, 1, 1)]));
        Assert.Null(AttendanceMetrics.MedianGapDays([]));
    }

    [Fact]
    public void BaselinePerWeek_AveragesWeeklyCounts()
    {
        Assert.Equal(2.5, AttendanceMetrics.BaselinePerWeek([2, 3, 2, 3]));
    }

    [Fact]
    public void BaselinePerWeek_NoWeeks_ReturnsZero()
    {
        Assert.Equal(0, AttendanceMetrics.BaselinePerWeek([]));
    }

    [Fact]
    public void ApplySeasonalFactor_ChristmasWeekAppliesStrongestDiscount()
    {
        Assert.Equal(6.0, AttendanceMetrics.ApplySeasonalFactor(10, isAugust: true, isChristmasWeek: true));
        Assert.Equal(8.0, AttendanceMetrics.ApplySeasonalFactor(10, isAugust: true, isChristmasWeek: false));
        Assert.Equal(10.0, AttendanceMetrics.ApplySeasonalFactor(10, isAugust: false, isChristmasWeek: false));
    }

    [Fact]
    public void JensenShannonDivergence_IdenticalDistributions_IsZero()
    {
        var p = new Dictionary<string, double> { ["strength"] = 5, ["metcon"] = 5 };
        Assert.Equal(0, AttendanceMetrics.JensenShannonDivergence(p, p), precision: 6);
    }

    [Fact]
    public void JensenShannonDivergence_DisjointDistributions_IsOne()
    {
        var p = new Dictionary<string, double> { ["strength"] = 1, ["metcon"] = 0 };
        var q = new Dictionary<string, double> { ["strength"] = 0, ["metcon"] = 1 };
        Assert.Equal(1, AttendanceMetrics.JensenShannonDivergence(p, q), precision: 6);
    }

    [Fact]
    public void IsoWeekStart_ReturnsMonday()
    {
        // Sunday 2026-01-04 (ISO) -> Monday 2025-12-29
        Assert.Equal(new DateOnly(2025, 12, 29), AttendanceMetrics.IsoWeekStart(new DateOnly(2026, 1, 4)));
        // Wednesday 2026-01-07 -> Monday 2026-01-05
        Assert.Equal(new DateOnly(2026, 1, 5), AttendanceMetrics.IsoWeekStart(new DateOnly(2026, 1, 7)));
    }
}
