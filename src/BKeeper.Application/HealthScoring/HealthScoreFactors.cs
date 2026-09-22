namespace BKeeper.Application.HealthScoring;

/// <summary>A single factor's raw 0-100 score, or an explicit "couldn't score this" with why — never a
/// fabricated 0 for missing data (see docs/PLAN.md's cold-start rule, applied per-factor here too).</summary>
public record FactorOutcome(double? Score, bool Included, string? Reason)
{
    public static FactorOutcome NoData(string reason) => new(null, false, reason);
    public static FactorOutcome Of(double score) => new(Math.Clamp(score, 0, 100), true, null);
}

/// <summary>Pure input for scoring one goal's contribution to the Progress factor.</summary>
public record GoalProgressSignal(
    DateOnly StartDate,
    DateOnly? TargetDate,
    double? BaselineValue,
    double? TargetValue,
    DateOnly? LastProgressDate,
    double? LastProgressValue);

/// <summary>
/// Pure per-factor scoring for the Athlete Health Score. Each method takes already-fetched, plain
/// data (counts/rates/dates) and returns a 0-100 outcome or an explicit no-data reason — no EF types,
/// no I/O, trivially unit-testable, matching the style of <see cref="BKeeper.Application.Metrics.AttendanceMetrics"/>.
/// </summary>
public static class HealthScoreFactors
{
    /// <summary>
    /// Attendance: visit rate over the configured window relative to the member's own baseline
    /// (mean weekly visits, the plan §5 "baseline_26w" definition). Self-normalizing per member —
    /// same idea R03 (frequency drop) uses — rather than an invented absolute "N classes/week" rule
    /// this codebase has no source data for.
    /// </summary>
    public static FactorOutcome AttendanceScore(int visitsInWindow, int windowDays, double baselinePerWeek)
    {
        if (baselinePerWeek <= 0) return FactorOutcome.NoData("insufficient_history");

        var windowWeeks = windowDays / 7.0;
        var currentRatePerWeek = visitsInWindow / windowWeeks;
        return FactorOutcome.Of(100 * currentRatePerWeek / baselinePerWeek);
    }

    /// <summary>
    /// Consistency: week-to-week regularity of attendance. A low coefficient of variation
    /// (stdev/mean of weekly visit counts) scores high; a spiky, irregular pattern scores low.
    /// </summary>
    public static FactorOutcome ConsistencyScore(IReadOnlyList<int> weeklyVisitCounts, int minWeeksOfHistory)
    {
        if (weeklyVisitCounts.Count < minWeeksOfHistory) return FactorOutcome.NoData("insufficient_history");

        var mean = weeklyVisitCounts.Average();
        if (mean <= 0) return FactorOutcome.NoData("no_visits");

        var variance = weeklyVisitCounts.Sum(v => Math.Pow(v - mean, 2)) / weeklyVisitCounts.Count;
        var coefficientOfVariation = Math.Sqrt(variance) / mean;
        return FactorOutcome.Of(100 * (1 - Math.Clamp(coefficientOfVariation, 0, 1)));
    }

    /// <summary>
    /// Booking behaviour: no-show + late-cancel rate over the window — the same composite rate R04
    /// (no-show streak) uses. A clean record scores 100; a rate of 50% or more scores 0.
    /// </summary>
    public static FactorOutcome BookingBehaviourScore(int noShowsAndLateCancels, int bookings, int minBookings)
    {
        if (bookings < minBookings) return FactorOutcome.NoData("insufficient_history");

        var rate = (double)noShowsAndLateCancels / bookings;
        return FactorOutcome.Of(100 - rate * 200);
    }

    /// <summary>
    /// Progress: goal-progress trend, standing in for the spec's Benchmark/PR data (no such entity
    /// exists in this codebase yet — see Goal/GoalProgress). A member with no goals gets an explicit
    /// "no data" outcome, never a fabricated failing score.
    /// </summary>
    public static FactorOutcome ProgressScore(IReadOnlyList<GoalProgressSignal> goals, DateOnly asOf, int windowDays)
    {
        if (goals.Count == 0) return FactorOutcome.NoData("no_goals");

        return FactorOutcome.Of(goals.Select(g => ScoreOneGoal(g, asOf, windowDays)).Average());
    }

    private static double ScoreOneGoal(GoalProgressSignal goal, DateOnly asOf, int windowDays)
    {
        var recency = goal.LastProgressDate is { } last
            ? 100.0 * Math.Clamp(1 - (double)(asOf.DayNumber - last.DayNumber) / (windowDays * 2.0), 0, 1)
            : 0.0;

        if (goal.BaselineValue is null || goal.TargetValue is null || goal.LastProgressValue is null || goal.TargetDate is null)
            return recency;

        var totalSpan = goal.TargetDate.Value.DayNumber - goal.StartDate.DayNumber;
        if (totalSpan <= 0) return recency;

        var elapsedFraction = Math.Clamp((double)(asOf.DayNumber - goal.StartDate.DayNumber) / totalSpan, 0, 1);
        var targetDelta = goal.TargetValue.Value - goal.BaselineValue.Value;
        if (targetDelta == 0 || elapsedFraction <= 0) return recency;

        var actualFraction = (goal.LastProgressValue.Value - goal.BaselineValue.Value) / targetDelta;
        var paceScore = Math.Clamp(actualFraction / elapsedFraction * 100, 0, 100);
        return (recency + paceScore) / 2;
    }

    /// <summary>
    /// Engagement: mean evaluation-form engagement index (see EvaluationScorer.EngagementIndex, a
    /// 1-5 scale) across responses in the window, rescaled to 0-100. No responses -> "no data".
    /// </summary>
    public static FactorOutcome EngagementScore(IReadOnlyList<double> engagementIndices)
    {
        if (engagementIndices.Count == 0) return FactorOutcome.NoData("no_evaluations");

        var avg = engagementIndices.Average();
        return FactorOutcome.Of((avg - 1) / 4 * 100);
    }
}
