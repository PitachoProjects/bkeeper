namespace BKeeper.Application.Evaluations;

/// <summary>Pure input for the R10 "goal at risk" check — no EF types, trivially testable.</summary>
public record GoalRiskInput(
    DateOnly? TargetDate,
    double? BaselineValue,
    double? TargetValue,
    DateOnly? LastProgressDate,
    double? LastProgressValue,
    DateOnly StartDate);

/// <summary>R10 (plan §7): goal due within 6 weeks and required pace not met, or no progress update for 8 weeks.</summary>
public static class GoalRiskEvaluator
{
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(42);
    private static readonly TimeSpan StaleProgressWindow = TimeSpan.FromDays(56);
    private const double PaceTolerance = 0.2; // 20 pp behind the straight-line expected pace counts as "at risk"

    public static bool IsAtRisk(GoalRiskInput goal, DateOnly asOf)
    {
        if (goal.LastProgressDate is null || asOf.DayNumber - goal.LastProgressDate.Value.DayNumber >= StaleProgressWindow.Days)
            return true;

        if (goal.TargetDate is null) return false;
        var daysToTarget = goal.TargetDate.Value.DayNumber - asOf.DayNumber;
        if (daysToTarget < 0 || daysToTarget > DueSoonWindow.Days) return false;

        if (goal.BaselineValue is null || goal.TargetValue is null || goal.LastProgressValue is null) return false;
        var totalSpan = goal.TargetDate.Value.DayNumber - goal.StartDate.DayNumber;
        if (totalSpan <= 0) return false;

        var elapsedFraction = Math.Clamp((double)(asOf.DayNumber - goal.StartDate.DayNumber) / totalSpan, 0, 1);
        var targetDelta = goal.TargetValue.Value - goal.BaselineValue.Value;
        if (targetDelta == 0) return false;

        var actualFraction = (goal.LastProgressValue.Value - goal.BaselineValue.Value) / targetDelta;
        return actualFraction < elapsedFraction - PaceTolerance;
    }
}
