using BKeeper.Application.Rules;
using BKeeper.Domain.Enums;
using BKeeper.Domain.Rules;
using Xunit;

namespace BKeeper.Tests.Unit.Rules;

public class AttendanceRulesTests
{
    private static readonly IReadOnlyDictionary<string, object> NoConfig = new Dictionary<string, object>();

    private static MemberMetrics Base(Guid? id = null) => new()
    {
        MemberId = id ?? Guid.NewGuid(),
        AsOf = new DateOnly(2026, 6, 1),
        TenureWeeks = 52,
        IsOnboarding = false,
        IsAway = false,
        BaselinePerWeek = 3,
        MedianGapDays = 3,
    };

    [Fact]
    public void R01_Miss_WhenRecentVisit()
    {
        var m = Base() with { DaysSinceLastVisit = 2 };
        Assert.Null(new R01AbsenceGap().Evaluate(m, NoConfig));
    }

    [Fact]
    public void R01_Amber_AtThreshold()
    {
        // min_days=10 default, gap_multiplier=2.5, median gap=3 -> threshold = max(10, 7.5) = 10
        var m = Base() with { DaysSinceLastVisit = 10 };
        var hit = new R01AbsenceGap().Evaluate(m, NoConfig);
        Assert.NotNull(hit);
        Assert.Equal(AlertSeverity.Amber, hit!.Severity);
    }

    [Fact]
    public void R01_Red_BeyondRedDaysCap()
    {
        var m = Base() with { DaysSinceLastVisit = 22 };
        var hit = new R01AbsenceGap().Evaluate(m, NoConfig);
        Assert.NotNull(hit);
        Assert.Equal(AlertSeverity.Red, hit!.Severity);
    }

    [Fact]
    public void R01_Miss_BelowMinBaseline()
    {
        var m = Base() with { DaysSinceLastVisit = 30, BaselinePerWeek = 0.5 };
        Assert.Null(new R01AbsenceGap().Evaluate(m, NoConfig));
    }

    [Fact]
    public void R03_Miss_WhenRateHoldsAboveAmberThreshold()
    {
        var m = Base() with { Last2WeekRate = 2.0, BaselinePerWeek = 3.0 }; // ratio 0.67 > 0.6
        Assert.Null(new R03FrequencyDrop().Evaluate(m, NoConfig));
    }

    [Fact]
    public void R03_Amber_WhenRateDropsBelowThreshold()
    {
        var m = Base() with { Last2WeekRate = 1.0, BaselinePerWeek = 3.0 }; // ratio 0.33
        var hit = new R03FrequencyDrop().Evaluate(m, NoConfig);
        Assert.NotNull(hit);
        Assert.Equal(AlertSeverity.Amber, hit!.Severity);
    }

    [Fact]
    public void R03_Red_WhenRateCollapses()
    {
        var m = Base() with { Last2WeekRate = 0.5, BaselinePerWeek = 3.0 }; // ratio 0.17 < 0.3
        var hit = new R03FrequencyDrop().Evaluate(m, NoConfig);
        Assert.NotNull(hit);
        Assert.Equal(AlertSeverity.Red, hit!.Severity);
    }

    [Fact]
    public void R04_Hits_OnNoShowStreak()
    {
        var m = Base() with { NoShowsLast14d = 2 };
        Assert.NotNull(new R04NoShowStreak().Evaluate(m, NoConfig));
    }

    [Fact]
    public void R04_Miss_BelowStreakAndRate()
    {
        var m = Base() with { NoShowsLast14d = 1, NoShowsLast8w = 1, BookingsLast8w = 8 };
        Assert.Null(new R04NoShowStreak().Evaluate(m, NoConfig));
    }

    [Fact]
    public void R08_Red_WhenNoVisitByDay7()
    {
        var m = Base() with { DaysSinceJoin = 7, VisitsSinceJoin = 0, TenureWeeks = 1, IsOnboarding = true };
        var hit = new R08OnboardingNoFirstVisit().Evaluate(m, NoConfig);
        Assert.NotNull(hit);
        Assert.Equal(AlertSeverity.Red, hit!.Severity);
    }

    [Fact]
    public void R08_Miss_WhenAlreadyVisited()
    {
        var m = Base() with { DaysSinceJoin = 7, VisitsSinceJoin = 1, TenureWeeks = 1, IsOnboarding = true };
        Assert.Null(new R08OnboardingNoFirstVisit().Evaluate(m, NoConfig));
    }
}
