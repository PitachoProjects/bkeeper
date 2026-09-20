using BKeeper.Domain.Enums;
using BKeeper.Domain.Rules;

namespace BKeeper.Application.Alerts;

public enum FamilyAction { None, AppendToExisting, CreateNew, CreateAsInfo }

public record FamilyDecision(
    AlertFamily Family,
    FamilyAction Action,
    AlertSeverity Severity,
    IReadOnlyList<string> RuleCodes,
    IReadOnlyDictionary<string, object> Evidence);

/// <summary>Minimal state about a member's currently-open alert in one family, needed for grouping/cooldown.</summary>
public record OpenAlertState(Guid AlertId, AlertSeverity Severity);

/// <summary>
/// Pure decision logic for turning rule hits into alert actions (plan §6.3: suppression, grouping,
/// dedupe, cooldown, human-contact deferral). Database I/O happens outside this class.
/// </summary>
public static class AlertOrchestrator
{
    public static bool IsSuppressed(MemberMetrics metrics, bool isFrozenOrCancelled) =>
        isFrozenOrCancelled || metrics.IsAway || (!metrics.IsOnboarding && metrics.TenureWeeks < 4);

    public static IReadOnlyList<FamilyDecision> Decide(
        IEnumerable<RuleHit> hits,
        IReadOnlyDictionary<AlertFamily, OpenAlertState> openAlertsByFamily,
        IReadOnlyDictionary<AlertFamily, DateTimeOffset> lastResolvedByFamily,
        bool hasHumanContactLast7d,
        DateTimeOffset now,
        int cooldownAmberDays = 14,
        int cooldownRedDays = 7)
    {
        var decisions = new List<FamilyDecision>();

        foreach (var group in hits.GroupBy(h => h.Family))
        {
            var family = group.Key;
            var maxSeverity = group.Max(h => h.Severity);
            var ruleCodes = group.Select(h => h.RuleCode).ToList();
            var evidence = MergeEvidence(group);

            if (openAlertsByFamily.TryGetValue(family, out var open))
            {
                var severity = (AlertSeverity)Math.Max((int)open.Severity, (int)maxSeverity);
                decisions.Add(new FamilyDecision(family, FamilyAction.AppendToExisting, severity, ruleCodes, evidence));
                continue;
            }

            if (lastResolvedByFamily.TryGetValue(family, out var resolvedAt))
            {
                var cooldownDays = maxSeverity == AlertSeverity.Red ? cooldownRedDays : cooldownAmberDays;
                if (now < resolvedAt.AddDays(cooldownDays))
                {
                    decisions.Add(new FamilyDecision(family, FamilyAction.None, maxSeverity, ruleCodes, evidence));
                    continue;
                }
            }

            if (hasHumanContactLast7d)
            {
                decisions.Add(new FamilyDecision(family, FamilyAction.CreateAsInfo, AlertSeverity.Info, ruleCodes, evidence));
                continue;
            }

            decisions.Add(new FamilyDecision(family, FamilyAction.CreateNew, maxSeverity, ruleCodes, evidence));
        }

        return decisions;
    }

    /// <summary>§6.3 priority score, used to sort the inbox and trim to the weekly alert budget.</summary>
    public static double PriorityScore(AlertSeverity severity, double? pChurn28d, int tenureWeeks, bool goalAtRisk, bool recentContact)
    {
        var severityWeight = severity switch { AlertSeverity.Red => 100, AlertSeverity.Amber => 60, _ => 20 };
        var riskComponent = (pChurn28d ?? 0) * 100;
        var tenureBonus = tenureWeeks >= 52 ? 10 : 0;
        var goalBonus = goalAtRisk ? 10 : 0;
        var contactPenalty = recentContact ? -30 : 0;
        return severityWeight + riskComponent + tenureBonus + goalBonus + contactPenalty;
    }

    private static Dictionary<string, object> MergeEvidence(IEnumerable<RuleHit> hits)
    {
        var merged = new Dictionary<string, object>();
        foreach (var hit in hits)
        {
            merged[hit.RuleCode] = hit.Evidence;
        }
        return merged;
    }
}
