using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

/// <summary>One question in a form's schema. Conditional: only shown if ShowIfQuestionKey's answer equals ShowIfEquals.</summary>
public record EvaluationQuestion
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public EvaluationQuestionType Type { get; init; }
    public string? ShowIfQuestionKey { get; init; }
    public string? ShowIfEquals { get; init; }
    /// <summary>If true, an answer <= 2 (scale) or <= 6 (NPS) or "yes" (yes/no framed as a risk) raises a flag.</summary>
    public bool FlagsOnNegative { get; init; }
    public bool FlagsHealthOnYes { get; init; }
}

public class EvaluationForm : BoxScopedEntity
{
    public string Key { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public List<EvaluationQuestion> Schema { get; set; } = new();
    /// <summary>Human-readable cadence description (plan §9) — e.g. "day 30", "every 8 weeks". Scheduling itself isn't automated in this pass.</summary>
    public string Cadence { get; set; } = string.Empty;

    public List<EvaluationResponse> Responses { get; set; } = new();
}

public class EvaluationResponse : BoxScopedEntity
{
    public Guid FormId { get; set; }
    public Guid MemberId { get; set; }
    public DateTimeOffset AnsweredAt { get; set; }
    public Dictionary<string, string> Answers { get; set; } = new();
    public Dictionary<string, double> Scores { get; set; } = new();
    public Guid? CoachId { get; set; }

    public EvaluationForm? Form { get; set; }
    public Member? Member { get; set; }
}

/// <summary>A signed, single-use, short-lived link sent to a member so they can answer a form without logging in.</summary>
public class EvaluationFormLink : BoxScopedEntity
{
    public Guid FormId { get; set; }
    public Guid MemberId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
