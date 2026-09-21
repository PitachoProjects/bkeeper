using BKeeper.Domain.Enums;

namespace BKeeper.Application.HealthScoring;

/// <summary>Plain snapshot of a <c>HealthScoreConfiguration</c> row, decoupled from EF so the composer
/// below stays a pure function (same reasoning as RuleConfig's Params dictionary feeding pure IRule.Evaluate).</summary>
public record HealthScoreWeights
{
    public double AttendanceWeight { get; init; } = 35;
    public double ConsistencyWeight { get; init; } = 25;
    public double BookingBehaviourWeight { get; init; } = 15;
    public double ProgressWeight { get; init; } = 15;
    public double EngagementWeight { get; init; } = 10;

    public bool AttendanceEnabled { get; init; } = true;
    public bool ConsistencyEnabled { get; init; } = true;
    public bool BookingBehaviourEnabled { get; init; } = true;
    public bool ProgressEnabled { get; init; } = true;
    public bool EngagementEnabled { get; init; } = true;

    public int MinTenureDays { get; init; } = 14;
    public int MinSessions { get; init; } = 3;
}

public record HealthScoreFactorResult(double? Score, double Weight, double Contribution, bool Included, string? Reason);

public record HealthScoreResult(bool InsufficientData, double? OverallScore, IReadOnlyDictionary<HealthScoreFactor, HealthScoreFactorResult> Factors);

/// <summary>
/// Combines the five pure factor outcomes (<see cref="HealthScoreFactors"/>) into one composite score:
/// weighted sum of enabled, data-having factors, re-normalized to 100 so a disabled or no-data factor
/// never silently counts as a zero. Below the config's cold-start thresholds, no numeric score is
/// produced at all (<see cref="HealthScoreResult.InsufficientData"/>).
/// </summary>
public static class HealthScoreComposer
{
    public static HealthScoreResult Compose(
        HealthScoreWeights config,
        IReadOnlyDictionary<HealthScoreFactor, FactorOutcome> outcomes,
        int tenureDays,
        int sessionCount)
    {
        if (tenureDays < config.MinTenureDays || sessionCount < config.MinSessions)
            return new HealthScoreResult(true, null, outcomes.ToDictionary(
                kv => kv.Key, kv => new HealthScoreFactorResult(kv.Value.Score, 0, 0, false, "insufficient_data")));

        var configuredWeight = ConfiguredWeights(config);
        var enabled = EnabledFlags(config);

        var totalIncludedWeight = outcomes
            .Where(kv => enabled[kv.Key] && kv.Value.Included && kv.Value.Score.HasValue)
            .Sum(kv => configuredWeight[kv.Key]);

        var breakdown = new Dictionary<HealthScoreFactor, HealthScoreFactorResult>();
        double overall = 0;

        foreach (var (factor, outcome) in outcomes)
        {
            var isIncluded = enabled[factor] && outcome.Included && outcome.Score.HasValue && totalIncludedWeight > 0;
            if (!isIncluded)
            {
                var reason = !enabled[factor] ? "disabled" : outcome.Reason;
                breakdown[factor] = new HealthScoreFactorResult(outcome.Score, 0, 0, false, reason);
                continue;
            }

            var renormalizedWeight = configuredWeight[factor] / totalIncludedWeight * 100;
            var contribution = outcome.Score!.Value * renormalizedWeight / 100;
            overall += contribution;
            breakdown[factor] = new HealthScoreFactorResult(outcome.Score, renormalizedWeight, contribution, true, null);
        }

        if (totalIncludedWeight <= 0)
            return new HealthScoreResult(true, null, breakdown);

        return new HealthScoreResult(false, Math.Round(overall, 1), breakdown);
    }

    private static Dictionary<HealthScoreFactor, double> ConfiguredWeights(HealthScoreWeights config) => new()
    {
        [HealthScoreFactor.Attendance] = config.AttendanceWeight,
        [HealthScoreFactor.Consistency] = config.ConsistencyWeight,
        [HealthScoreFactor.BookingBehaviour] = config.BookingBehaviourWeight,
        [HealthScoreFactor.Progress] = config.ProgressWeight,
        [HealthScoreFactor.Engagement] = config.EngagementWeight,
    };

    private static Dictionary<HealthScoreFactor, bool> EnabledFlags(HealthScoreWeights config) => new()
    {
        [HealthScoreFactor.Attendance] = config.AttendanceEnabled,
        [HealthScoreFactor.Consistency] = config.ConsistencyEnabled,
        [HealthScoreFactor.BookingBehaviour] = config.BookingBehaviourEnabled,
        [HealthScoreFactor.Progress] = config.ProgressEnabled,
        [HealthScoreFactor.Engagement] = config.EngagementEnabled,
    };
}
