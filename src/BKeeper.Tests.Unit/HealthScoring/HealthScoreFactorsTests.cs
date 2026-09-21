using BKeeper.Application.HealthScoring;
using Xunit;

namespace BKeeper.Tests.Unit.HealthScoring;

public class HealthScoreFactorsTests
{
    [Fact]
    public void AttendanceScore_NoBaseline_IsNoData()
    {
        var outcome = HealthScoreFactors.AttendanceScore(visitsInWindow: 5, windowDays: 28, baselinePerWeek: 0);
        Assert.False(outcome.Included);
        Assert.Equal("insufficient_history", outcome.Reason);
    }

    [Fact]
    public void AttendanceScore_MatchingBaseline_ScoresHundred()
    {
        // 4 weeks window, 3 visits/week baseline -> 12 visits matches baseline exactly.
        var outcome = HealthScoreFactors.AttendanceScore(visitsInWindow: 12, windowDays: 28, baselinePerWeek: 3);
        Assert.True(outcome.Included);
        Assert.Equal(100, outcome.Score!.Value, 3);
    }

    [Fact]
    public void AttendanceScore_AboveBaseline_ClampsAtHundred()
    {
        var outcome = HealthScoreFactors.AttendanceScore(visitsInWindow: 40, windowDays: 28, baselinePerWeek: 3);
        Assert.Equal(100, outcome.Score!.Value, 3);
    }

    [Fact]
    public void ConsistencyScore_BelowMinWeeks_IsNoData()
    {
        var outcome = HealthScoreFactors.ConsistencyScore([3, 3], minWeeksOfHistory: 4);
        Assert.False(outcome.Included);
        Assert.Equal("insufficient_history", outcome.Reason);
    }

    [Fact]
    public void ConsistencyScore_IdenticalWeeklyCounts_ScoresHundred()
    {
        var outcome = HealthScoreFactors.ConsistencyScore([3, 3, 3, 3], minWeeksOfHistory: 4);
        Assert.True(outcome.Included);
        Assert.Equal(100, outcome.Score!.Value, 3);
    }

    [Fact]
    public void ConsistencyScore_ErraticWeeklyCounts_ScoresLowerThanRegular()
    {
        var regular = HealthScoreFactors.ConsistencyScore([3, 3, 3, 3], minWeeksOfHistory: 4);
        var erratic = HealthScoreFactors.ConsistencyScore([0, 6, 0, 6], minWeeksOfHistory: 4);
        Assert.True(erratic.Score!.Value < regular.Score!.Value);
    }

    [Fact]
    public void BookingBehaviourScore_BelowMinBookings_IsNoData()
    {
        var outcome = HealthScoreFactors.BookingBehaviourScore(noShowsAndLateCancels: 1, bookings: 2, minBookings: 6);
        Assert.False(outcome.Included);
    }

    [Fact]
    public void BookingBehaviourScore_NoNoShows_ScoresHundred()
    {
        var outcome = HealthScoreFactors.BookingBehaviourScore(noShowsAndLateCancels: 0, bookings: 10, minBookings: 6);
        Assert.Equal(100, outcome.Score!.Value, 3);
    }

    [Fact]
    public void BookingBehaviourScore_HalfNoShowRate_ScoresZero()
    {
        var outcome = HealthScoreFactors.BookingBehaviourScore(noShowsAndLateCancels: 5, bookings: 10, minBookings: 6);
        Assert.Equal(0, outcome.Score!.Value, 3);
    }

    [Fact]
    public void ProgressScore_NoGoals_IsNoDataNotZero()
    {
        var outcome = HealthScoreFactors.ProgressScore([], new DateOnly(2026, 6, 1), 90);
        Assert.False(outcome.Included);
        Assert.Null(outcome.Score);
        Assert.Equal("no_goals", outcome.Reason);
    }

    [Fact]
    public void ProgressScore_OnPaceGoal_ScoresHigh()
    {
        var start = new DateOnly(2026, 1, 1);
        var asOf = new DateOnly(2026, 4, 1); // halfway to a 6-month target
        var target = start.AddMonths(6);
        var goal = new GoalProgressSignal(start, target, BaselineValue: 0, TargetValue: 100, LastProgressDate: asOf, LastProgressValue: 55);

        var outcome = HealthScoreFactors.ProgressScore([goal], asOf, windowDays: 90);
        Assert.True(outcome.Included);
        Assert.True(outcome.Score!.Value > 70, $"expected a high score for a goal ahead of pace, got {outcome.Score}");
    }

    [Fact]
    public void ProgressScore_StaleGoalWithNoRecentUpdate_ScoresLow()
    {
        var start = new DateOnly(2025, 1, 1);
        var asOf = new DateOnly(2026, 6, 1);
        var goal = new GoalProgressSignal(start, null, null, null, LastProgressDate: null, LastProgressValue: null);

        var outcome = HealthScoreFactors.ProgressScore([goal], asOf, windowDays: 90);
        Assert.True(outcome.Included);
        Assert.Equal(0, outcome.Score!.Value, 3);
    }

    [Fact]
    public void EngagementScore_NoResponses_IsNoData()
    {
        var outcome = HealthScoreFactors.EngagementScore([]);
        Assert.False(outcome.Included);
        Assert.Equal("no_evaluations", outcome.Reason);
    }

    [Fact]
    public void EngagementScore_TopOfScale_ScoresHundred()
    {
        var outcome = HealthScoreFactors.EngagementScore([5, 5]);
        Assert.Equal(100, outcome.Score!.Value, 3);
    }

    [Fact]
    public void EngagementScore_BottomOfScale_ScoresZero()
    {
        var outcome = HealthScoreFactors.EngagementScore([1, 1]);
        Assert.Equal(0, outcome.Score!.Value, 3);
    }

    [Fact]
    public void EngagementScore_MidScale_ScoresFifty()
    {
        var outcome = HealthScoreFactors.EngagementScore([3]);
        Assert.Equal(50, outcome.Score!.Value, 3);
    }
}
