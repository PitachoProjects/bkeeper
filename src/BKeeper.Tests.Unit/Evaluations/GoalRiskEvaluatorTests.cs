using BKeeper.Application.Evaluations;
using Xunit;

namespace BKeeper.Tests.Unit.Evaluations;

public class GoalRiskEvaluatorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    [Fact]
    public void NoProgressEver_AndOlderThan8Weeks_IsAtRisk()
    {
        var goal = new GoalRiskInput(TargetDate: null, BaselineValue: null, TargetValue: null,
            LastProgressDate: null, LastProgressValue: null, StartDate: Start);
        Assert.True(GoalRiskEvaluator.IsAtRisk(goal, Start.AddDays(60)));
    }

    [Fact]
    public void NoProgressUpdateFor8Weeks_IsAtRisk()
    {
        var goal = new GoalRiskInput(TargetDate: Start.AddDays(200), BaselineValue: 0, TargetValue: 10,
            LastProgressDate: Start.AddDays(10), LastProgressValue: 5, StartDate: Start);
        Assert.True(GoalRiskEvaluator.IsAtRisk(goal, Start.AddDays(10 + 57)));
    }

    [Fact]
    public void RecentProgress_FarFromTargetDate_NotAtRisk()
    {
        var goal = new GoalRiskInput(TargetDate: Start.AddDays(200), BaselineValue: 0, TargetValue: 10,
            LastProgressDate: Start.AddDays(50), LastProgressValue: 3, StartDate: Start);
        // target is 150 days away — beyond the 6-week "due soon" window, so pace isn't checked yet.
        Assert.False(GoalRiskEvaluator.IsAtRisk(goal, Start.AddDays(50)));
    }

    [Fact]
    public void DueSoon_OnPace_NotAtRisk()
    {
        // 100-day goal, 0->10. At day 50 (50% elapsed), value should be ~5.
        var goal = new GoalRiskInput(TargetDate: Start.AddDays(100), BaselineValue: 0, TargetValue: 10,
            LastProgressDate: Start.AddDays(70), LastProgressValue: 5, StartDate: Start);
        Assert.False(GoalRiskEvaluator.IsAtRisk(goal, Start.AddDays(70)));
    }

    [Fact]
    public void DueSoon_BehindPace_IsAtRisk()
    {
        // 100-day goal, 0->10. At day 70 (70% elapsed, within the 6-week window), value is only 1 (10%).
        var goal = new GoalRiskInput(TargetDate: Start.AddDays(100), BaselineValue: 0, TargetValue: 10,
            LastProgressDate: Start.AddDays(70), LastProgressValue: 1, StartDate: Start);
        Assert.True(GoalRiskEvaluator.IsAtRisk(goal, Start.AddDays(70)));
    }

    [Fact]
    public void AlreadyPastTargetDate_NotFlaggedByPaceCheck()
    {
        var goal = new GoalRiskInput(TargetDate: Start.AddDays(50), BaselineValue: 0, TargetValue: 10,
            LastProgressDate: Start.AddDays(45), LastProgressValue: 9, StartDate: Start);
        Assert.False(GoalRiskEvaluator.IsAtRisk(goal, Start.AddDays(60)));
    }
}
