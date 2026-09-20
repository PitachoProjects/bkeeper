using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;

namespace BKeeper.Application.Evaluations;

/// <summary>The 6 seed forms from plan §9. Each box gets its own copy (per-box overrides are then possible).</summary>
public static class EvaluationFormSeeder
{
    public record SeedForm(string Key, string Cadence, List<EvaluationQuestion> Schema);

    public static IReadOnlyList<SeedForm> DefaultForms =>
    [
        new("ONBOARDING", "day 0", [
            new EvaluationQuestion { Key = "main_goal", Label = "What's your main goal?", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "experience", Label = "Your training experience", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "injuries", Label = "Any injuries or limitations we should know about?", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "has_injury", Label = "Do you have an injury or health limitation?", Type = EvaluationQuestionType.YesNo, FlagsHealthOnYes = true },
            new EvaluationQuestion { Key = "preferred_times", Label = "Preferred days/times", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "found_us", Label = "How did you find us?", Type = EvaluationQuestionType.Text },
        ]),
        new("PULSE_D30", "day 30", [
            new EvaluationQuestion { Key = "satisfaction", Label = "How satisfied are you so far? (1-5)", Type = EvaluationQuestionType.Scale1To5, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "welcome", Label = "How welcome do you feel? (1-5)", Type = EvaluationQuestionType.Scale1To5, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "class_time_fit", Label = "Do the class times work for you? (1-5)", Type = EvaluationQuestionType.Scale1To5 },
            new EvaluationQuestion { Key = "open_feedback", Label = "Anything else you'd like to share?", Type = EvaluationQuestionType.Text },
        ]),
        new("PULSE_D90", "day 90", [
            new EvaluationQuestion { Key = "satisfaction", Label = "How satisfied are you so far? (1-5)", Type = EvaluationQuestionType.Scale1To5, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "welcome", Label = "How welcome do you feel? (1-5)", Type = EvaluationQuestionType.Scale1To5, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "class_time_fit", Label = "Do the class times work for you? (1-5)", Type = EvaluationQuestionType.Scale1To5 },
            new EvaluationQuestion { Key = "nps", Label = "How likely are you to recommend us? (0-10)", Type = EvaluationQuestionType.Nps0To10, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "goal_progress", Label = "How is progress on your goal?", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "open_feedback", Label = "Anything else you'd like to share?", Type = EvaluationQuestionType.Text },
        ]),
        new("QUARTERLY", "every 8 weeks", [
            new EvaluationQuestion { Key = "goal_progress", Label = "How is progress on your active goal(s)?", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "energy", Label = "Energy / motivation (1-5)", Type = EvaluationQuestionType.Scale1To5, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "recovery", Label = "Recovery / sleep (1-5)", Type = EvaluationQuestionType.Scale1To5, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "has_pain", Label = "Any pain or injury right now?", Type = EvaluationQuestionType.YesNo, FlagsHealthOnYes = true },
            new EvaluationQuestion { Key = "pain_detail", Label = "Tell us more", Type = EvaluationQuestionType.Text, ShowIfQuestionKey = "has_pain", ShowIfEquals = "yes" },
            new EvaluationQuestion { Key = "coach_satisfaction", Label = "Coach & class satisfaction (1-5)", Type = EvaluationQuestionType.Scale1To5, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "nps", Label = "How likely are you to recommend us? (0-10)", Type = EvaluationQuestionType.Nps0To10, FlagsOnNegative = true },
            new EvaluationQuestion { Key = "change_request", Label = "Anything we should change?", Type = EvaluationQuestionType.Text },
        ]),
        new("BENCHMARK", "every 12 weeks", [
            new EvaluationQuestion { Key = "back_squat_1rm", Label = "Back squat 1RM (kg)", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "deadlift_1rm", Label = "Deadlift 1RM (kg)", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "row_2k_time", Label = "2k row time", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "run_5k_time", Label = "5k run time", Type = EvaluationQuestionType.Text },
        ]),
        new("EXIT", "on cancellation", [
            new EvaluationQuestion { Key = "reason", Label = "What's the main reason you're leaving?", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "what_would_have_kept_you", Label = "What would have kept you training with us?", Type = EvaluationQuestionType.Text },
            new EvaluationQuestion { Key = "nps", Label = "How likely are you to recommend us to a friend? (0-10)", Type = EvaluationQuestionType.Nps0To10 },
        ]),
    ];

    public static List<EvaluationForm> Build(Guid boxId) =>
        DefaultForms.Select(f => new EvaluationForm { BoxId = boxId, Key = f.Key, Version = 1, Schema = f.Schema, Cadence = f.Cadence }).ToList();
}
