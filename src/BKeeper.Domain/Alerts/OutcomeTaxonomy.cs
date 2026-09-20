namespace BKeeper.Domain.Alerts;

/// <summary>The fixed outcome vocabulary from plan §6.4 — mandatory to resolve an alert.</summary>
public static class OutcomeTaxonomy
{
    public static readonly IReadOnlyList<string> Values =
    [
        "returned",
        "contacted_replied_will_return",
        "contacted_no_reply",
        "unreachable",
        "schedule_change_needed",
        "injury_or_health_pause",
        "price_or_budget",
        "moved_away",
        "lost_motivation",
        "coach_or_class_issue",
        "cancelled_membership",
        "false_positive_no_action",
        "other",
    ];

    public static bool IsValid(string outcome) => Values.Contains(outcome);
}
