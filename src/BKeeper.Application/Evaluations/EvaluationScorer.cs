using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;

namespace BKeeper.Application.Evaluations;

public record EvaluationFlags(bool Negative, bool Health);

/// <summary>
/// Pure scoring for a submitted evaluation (plan §9): flags (scale <= 2, NPS <= 6, pain/health = yes),
/// per-question numeric scores, and a generic engagement index (mean of all scale-1-5 answers — the
/// plan's own formula names specific questions like "satisfaction, welcome, energy, coach"; this
/// generalises it to whichever scale questions a form actually has, rather than hardcoding keys).
/// </summary>
public static class EvaluationScorer
{
    public static EvaluationFlags ComputeFlags(IReadOnlyList<EvaluationQuestion> schema, IReadOnlyDictionary<string, string> answers)
    {
        var negative = false;
        var health = false;

        foreach (var q in schema)
        {
            if (!answers.TryGetValue(q.Key, out var raw)) continue;

            if (q.FlagsOnNegative)
            {
                if (q.Type == EvaluationQuestionType.Scale1To5 && double.TryParse(raw, out var s) && s <= 2) negative = true;
                if (q.Type == EvaluationQuestionType.Nps0To10 && double.TryParse(raw, out var n) && n <= 6) negative = true;
            }
            if (q.FlagsHealthOnYes && q.Type == EvaluationQuestionType.YesNo && string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase))
                health = true;
        }

        return new EvaluationFlags(negative, health);
    }

    public static Dictionary<string, double> ComputeScores(IReadOnlyList<EvaluationQuestion> schema, IReadOnlyDictionary<string, string> answers)
    {
        var scores = new Dictionary<string, double>();
        foreach (var q in schema)
            if (answers.TryGetValue(q.Key, out var raw) && double.TryParse(raw, out var value))
                scores[q.Key] = value;
        return scores;
    }

    public static double? EngagementIndex(IReadOnlyList<EvaluationQuestion> schema, IReadOnlyDictionary<string, string> answers)
    {
        var values = schema.Where(q => q.Type == EvaluationQuestionType.Scale1To5)
            .Select(q => answers.TryGetValue(q.Key, out var raw) && double.TryParse(raw, out var v) ? v : (double?)null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return values.Count == 0 ? null : values.Average();
    }

    /// <summary>A question is visible only if its ShowIf condition (if any) is met by prior answers.</summary>
    public static bool ShouldShow(EvaluationQuestion question, IReadOnlyDictionary<string, string> answersSoFar) =>
        question.ShowIfQuestionKey is null ||
        (answersSoFar.TryGetValue(question.ShowIfQuestionKey, out var v) &&
         string.Equals(v, question.ShowIfEquals, StringComparison.OrdinalIgnoreCase));
}
