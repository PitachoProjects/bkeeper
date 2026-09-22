using BKeeper.Application.HealthScoring;
using BKeeper.Domain.Enums;
using Xunit;

namespace BKeeper.Tests.Unit.HealthScoring;

public class HealthScoreComposerTests
{
    private static readonly HealthScoreWeights DefaultWeights = new(); // 35/25/15/15/10, all enabled, min 14d/3 sessions

    private static Dictionary<HealthScoreFactor, FactorOutcome> AllIncluded(double attendance = 88, double consistency = 70,
        double bookingBehaviour = 90, double progress = 60, double engagement = 50) => new()
    {
        [HealthScoreFactor.Attendance] = FactorOutcome.Of(attendance),
        [HealthScoreFactor.Consistency] = FactorOutcome.Of(consistency),
        [HealthScoreFactor.BookingBehaviour] = FactorOutcome.Of(bookingBehaviour),
        [HealthScoreFactor.Progress] = FactorOutcome.Of(progress),
        [HealthScoreFactor.Engagement] = FactorOutcome.Of(engagement),
    };

    [Fact]
    public void Compose_AllFactorsIncluded_WeightedSumAndContributionsMatchTheSpecExample()
    {
        var result = HealthScoreComposer.Compose(DefaultWeights, AllIncluded(), tenureDays: 200, sessionCount: 50);

        Assert.False(result.InsufficientData);
        // 88*0.35 + 70*0.25 + 90*0.15 + 60*0.15 + 50*0.10 = 30.8 + 17.5 + 13.5 + 9.0 + 5.0 = 75.8
        Assert.Equal(75.8, result.OverallScore!.Value, 3);

        var attendance = result.Factors[HealthScoreFactor.Attendance];
        Assert.Equal(35, attendance.Weight, 3);
        Assert.Equal(30.8, attendance.Contribution, 3);
        Assert.True(attendance.Included);
    }

    [Fact]
    public void Compose_FactorWithoutData_IsExcludedAndOthersRenormalizeTo100()
    {
        var outcomes = AllIncluded();
        outcomes[HealthScoreFactor.Engagement] = FactorOutcome.NoData("no_evaluations");

        var result = HealthScoreComposer.Compose(DefaultWeights, outcomes, tenureDays: 200, sessionCount: 50);

        var included = result.Factors.Values.Where(f => f.Included).ToList();
        Assert.Equal(100, included.Sum(f => f.Weight), 3); // renormalized weights of included factors sum to 100

        var engagement = result.Factors[HealthScoreFactor.Engagement];
        Assert.False(engagement.Included);
        Assert.Equal(0, engagement.Weight);
        Assert.Equal(0, engagement.Contribution);
        Assert.Equal("no_evaluations", engagement.Reason);

        // Attendance's renormalized weight grows from 35 to 35/90*100 once Engagement (weight 10) drops out.
        Assert.Equal(35.0 / 90 * 100, result.Factors[HealthScoreFactor.Attendance].Weight, 3);
        Assert.Equal(78.7, result.OverallScore!.Value, 1);
    }

    [Fact]
    public void Compose_DisabledFactor_IsExcludedWithReasonDisabled()
    {
        var weights = DefaultWeights with { EngagementEnabled = false };
        var result = HealthScoreComposer.Compose(weights, AllIncluded(), tenureDays: 200, sessionCount: 50);

        var engagement = result.Factors[HealthScoreFactor.Engagement];
        Assert.False(engagement.Included);
        Assert.Equal("disabled", engagement.Reason);
        Assert.False(result.InsufficientData);
    }

    [Fact]
    public void Compose_BelowTenureThreshold_IsInsufficientDataRegardlessOfFactorScores()
    {
        var result = HealthScoreComposer.Compose(DefaultWeights, AllIncluded(), tenureDays: 5, sessionCount: 50);

        Assert.True(result.InsufficientData);
        Assert.Null(result.OverallScore);
    }

    [Fact]
    public void Compose_BelowSessionCountThreshold_IsInsufficientData()
    {
        var result = HealthScoreComposer.Compose(DefaultWeights, AllIncluded(), tenureDays: 200, sessionCount: 1);

        Assert.True(result.InsufficientData);
        Assert.Null(result.OverallScore);
    }

    [Fact]
    public void Compose_EveryFactorDisabledOrMissing_IsInsufficientDataEvenAboveColdStartThresholds()
    {
        var outcomes = new Dictionary<HealthScoreFactor, FactorOutcome>
        {
            [HealthScoreFactor.Attendance] = FactorOutcome.NoData("insufficient_history"),
            [HealthScoreFactor.Consistency] = FactorOutcome.NoData("insufficient_history"),
            [HealthScoreFactor.BookingBehaviour] = FactorOutcome.NoData("insufficient_history"),
            [HealthScoreFactor.Progress] = FactorOutcome.NoData("no_goals"),
            [HealthScoreFactor.Engagement] = FactorOutcome.NoData("no_evaluations"),
        };

        var result = HealthScoreComposer.Compose(DefaultWeights, outcomes, tenureDays: 200, sessionCount: 50);

        Assert.True(result.InsufficientData);
        Assert.Null(result.OverallScore);
    }
}
