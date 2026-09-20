using System.Text.RegularExpressions;
using BKeeper.Domain.Enums;

namespace BKeeper.Application.Import;

/// <summary>
/// Keyword-rule workout classifier (plan §7, step 1 only — the LLM fallback for low-confidence
/// text is not built in this pass; low-confidence workouts are left untagged, see docs/OPEN_QUESTIONS.md).
/// </summary>
public static partial class WorkoutClassifier
{
    private static readonly (WorkoutTagType Tag, Regex Pattern)[] Rules =
    [
        (WorkoutTagType.Strength, StrengthRegex()),
        (WorkoutTagType.Metcon, MetconRegex()),
        (WorkoutTagType.Gymnastics, GymnasticsRegex()),
        (WorkoutTagType.Endurance, EnduranceRegex()),
        (WorkoutTagType.Hybrid, HybridRegex()),
    ];

    public static IReadOnlyDictionary<WorkoutTagType, double> Classify(string? title, string? description)
    {
        var text = $"{title} {description}";
        var hits = Rules.Where(r => r.Pattern.IsMatch(text)).Select(r => r.Tag).Distinct().ToList();
        if (hits.Count == 0) return new Dictionary<WorkoutTagType, double>();

        var weight = 1.0 / hits.Count;
        return hits.ToDictionary(t => t, _ => weight);
    }

    public static double Confidence(IReadOnlyDictionary<WorkoutTagType, double> classification) =>
        classification.Count == 0 ? 0 : 1.0;

    [GeneratedRegex(@"squat|deadlift|press|clean|snatch|1rm|5x5", RegexOptions.IgnoreCase)]
    private static partial Regex StrengthRegex();

    [GeneratedRegex(@"amrap|emom|for time|rounds", RegexOptions.IgnoreCase)]
    private static partial Regex MetconRegex();

    [GeneratedRegex(@"muscle-up|handstand|hspu|ring|pull-up progression|skill", RegexOptions.IgnoreCase)]
    private static partial Regex GymnasticsRegex();

    [GeneratedRegex(@"run|row|bike|ski|erg|\d+ ?m\b", RegexOptions.IgnoreCase)]
    private static partial Regex EnduranceRegex();

    [GeneratedRegex(@"partner|team|hero", RegexOptions.IgnoreCase)]
    private static partial Regex HybridRegex();
}
