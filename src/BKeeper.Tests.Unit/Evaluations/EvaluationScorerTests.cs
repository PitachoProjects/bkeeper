using BKeeper.Application.Evaluations;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using Xunit;

namespace BKeeper.Tests.Unit.Evaluations;

public class EvaluationScorerTests
{
    private static readonly List<EvaluationQuestion> Schema =
    [
        new() { Key = "satisfaction", Type = EvaluationQuestionType.Scale1To5, FlagsOnNegative = true },
        new() { Key = "nps", Type = EvaluationQuestionType.Nps0To10, FlagsOnNegative = true },
        new() { Key = "has_pain", Type = EvaluationQuestionType.YesNo, FlagsHealthOnYes = true },
        new() { Key = "notes", Type = EvaluationQuestionType.Text },
        new() { Key = "conditional", Type = EvaluationQuestionType.Text, ShowIfQuestionKey = "has_pain", ShowIfEquals = "yes" },
    ];

    [Fact]
    public void ComputeFlags_LowScale_RaisesNegative()
    {
        var answers = new Dictionary<string, string> { ["satisfaction"] = "2" };
        var flags = EvaluationScorer.ComputeFlags(Schema, answers);
        Assert.True(flags.Negative);
        Assert.False(flags.Health);
    }

    [Fact]
    public void ComputeFlags_HighScale_DoesNotRaiseNegative()
    {
        var answers = new Dictionary<string, string> { ["satisfaction"] = "4" };
        Assert.False(EvaluationScorer.ComputeFlags(Schema, answers).Negative);
    }

    [Fact]
    public void ComputeFlags_LowNps_RaisesNegative()
    {
        var answers = new Dictionary<string, string> { ["nps"] = "6" };
        Assert.True(EvaluationScorer.ComputeFlags(Schema, answers).Negative);
    }

    [Fact]
    public void ComputeFlags_HighNps_DoesNotRaiseNegative()
    {
        var answers = new Dictionary<string, string> { ["nps"] = "7" };
        Assert.False(EvaluationScorer.ComputeFlags(Schema, answers).Negative);
    }

    [Fact]
    public void ComputeFlags_PainYes_RaisesHealth()
    {
        var answers = new Dictionary<string, string> { ["has_pain"] = "yes" };
        Assert.True(EvaluationScorer.ComputeFlags(Schema, answers).Health);
    }

    [Fact]
    public void ComputeFlags_PainNo_DoesNotRaiseHealth()
    {
        var answers = new Dictionary<string, string> { ["has_pain"] = "no" };
        Assert.False(EvaluationScorer.ComputeFlags(Schema, answers).Health);
    }

    [Fact]
    public void ComputeScores_OnlyIncludesNumericAnswers()
    {
        var answers = new Dictionary<string, string> { ["satisfaction"] = "3", ["notes"] = "great box" };
        var scores = EvaluationScorer.ComputeScores(Schema, answers);
        Assert.Equal(3, scores["satisfaction"]);
        Assert.False(scores.ContainsKey("notes"));
    }

    [Fact]
    public void EngagementIndex_AveragesScaleAnswersOnly()
    {
        var schema = new List<EvaluationQuestion>
        {
            new() { Key = "a", Type = EvaluationQuestionType.Scale1To5 },
            new() { Key = "b", Type = EvaluationQuestionType.Scale1To5 },
            new() { Key = "nps", Type = EvaluationQuestionType.Nps0To10 },
        };
        var answers = new Dictionary<string, string> { ["a"] = "4", ["b"] = "2", ["nps"] = "9" };
        Assert.Equal(3, EvaluationScorer.EngagementIndex(schema, answers));
    }

    [Fact]
    public void EngagementIndex_NoScaleAnswers_ReturnsNull()
    {
        var schema = new List<EvaluationQuestion> { new() { Key = "nps", Type = EvaluationQuestionType.Nps0To10 } };
        Assert.Null(EvaluationScorer.EngagementIndex(schema, new Dictionary<string, string> { ["nps"] = "9" }));
    }

    [Fact]
    public void ShouldShow_NoCondition_AlwaysTrue()
    {
        var q = new EvaluationQuestion { Key = "x", Type = EvaluationQuestionType.Text };
        Assert.True(EvaluationScorer.ShouldShow(q, new Dictionary<string, string>()));
    }

    [Fact]
    public void ShouldShow_ConditionMet_True()
    {
        var q = Schema.First(x => x.Key == "conditional");
        Assert.True(EvaluationScorer.ShouldShow(q, new Dictionary<string, string> { ["has_pain"] = "yes" }));
    }

    [Fact]
    public void ShouldShow_ConditionNotMet_False()
    {
        var q = Schema.First(x => x.Key == "conditional");
        Assert.False(EvaluationScorer.ShouldShow(q, new Dictionary<string, string> { ["has_pain"] = "no" }));
        Assert.False(EvaluationScorer.ShouldShow(q, new Dictionary<string, string>()));
    }
}
