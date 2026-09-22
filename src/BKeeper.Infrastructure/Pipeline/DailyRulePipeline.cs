using BKeeper.Application.Alerts;
using BKeeper.Application.Metrics;
using BKeeper.Application.Notifications;
using BKeeper.Application.Rules;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Domain.Rules;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Notifications;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Pipeline;

/// <summary>
/// The daily 05:30 job (plan §6.1, steps S4/S8/S9): evaluate every active member against the
/// enabled rules, create/update alerts, and queue the automatic outreach the rule catalogue defines
/// for R01/R02/R03 amber hits (S11). The ML step (S5-S7) is not built in this pass — see
/// docs/OPEN_QUESTIONS.md. Queued outreach is actually sent by <see cref="OutreachDispatcher"/>.
/// </summary>
public class DailyRulePipeline(
    BKeeperDbContext db, CurrentBoxAccessor currentBox, IEnumerable<IRule> rules,
    OutreachQueueService outreachQueue, ILogger<DailyRulePipeline> logger)
{
    public async Task RunForAllBoxesAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var boxIds = await db.Boxes.Select(b => b.Id).ToListAsync(ct);
        foreach (var boxId in boxIds)
        {
            using (currentBox.Use(boxId))
            {
                await RunForBoxAsync(boxId, asOf, ct: ct);
            }
        }
    }

    /// <summary><paramref name="memberId"/> scopes the run to a single member — the manual "recompute for
    /// this athlete" trigger on their profile — instead of every active member in the box.</summary>
    public async Task<int> RunForBoxAsync(Guid boxId, DateOnly asOf, Guid? memberId = null, CancellationToken ct = default)
    {
        var ruleConfigs = await db.RuleConfigs.ToDictionaryAsync(r => r.RuleCode, ct);
        var box = await db.Boxes.FindAsync([boxId], ct);

        var members = await db.Members
            .Where(m => m.Status == MemberStatus.Active && (memberId == null || m.Id == memberId))
            .ToListAsync(ct);

        var alertsCreated = 0;

        foreach (var member in members)
        {
            var facts = await db.Bookings
                .Where(b => b.MemberId == member.Id)
                .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new BookingFact(DateOnly.FromDateTime(s.StartsAt.Date), b.Status, b.BookedAt))
                .ToListAsync(ct);

            await UpdateConsistencyGoalProgressAsync(boxId, member.Id, facts, asOf, ct);

            var metrics = MetricsBuilder.Build(member.Id, member.JoinDate, asOf, facts, member.AwayUntil);
            var isFrozenOrAway = member.Status != MemberStatus.Active || (member.InjuryFlagUntil.HasValue && member.InjuryFlagUntil >= asOf);
            if (AlertOrchestrator.IsSuppressed(metrics, isFrozenOrAway)) continue;

            var hits = rules
                .Where(r => !ruleConfigs.TryGetValue(r.Code, out var cfg) || cfg.Enabled)
                .Select(r => r.Evaluate(metrics, ruleConfigs.TryGetValue(r.Code, out var cfg2) ? cfg2.Params : new Dictionary<string, object>()))
                .Where(h => h is not null)
                .Select(h => h!)
                .ToList();

            if (hits.Count == 0) continue;

            alertsCreated += await ApplyDecisionsAsync(boxId, member, box?.Name ?? "your box", hits, ruleConfigs, ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Daily rule pipeline for box {BoxId}: {Count} new alerts", boxId, alertsCreated);
        return alertsCreated;
    }

    private async Task<int> ApplyDecisionsAsync(Guid boxId, Member member, string boxName, List<RuleHit> hits,
        Dictionary<string, RuleConfig> ruleConfigs, CancellationToken ct)
    {
        var memberId = member.Id;
        var openAlerts = await db.Alerts
            .Where(a => a.MemberId == memberId && a.Status != AlertStatus.Resolved && a.Status != AlertStatus.AutoResolved)
            .ToListAsync(ct);
        // GroupBy, not ToDictionary(a => a.Family, ...): SimpleAlertService.CreateOrAppendAsync can leave two
        // open alerts in the same family (two calls in one request, before either is saved, both see "no
        // existing alert" and both create one) — a plain ToDictionary throws on the duplicate key and takes
        // the whole daily run down for every member after this one. Picking the most severe/most recent as
        // canonical keeps the pipeline running even while that data exists.
        var canonicalOpenByFamily = openAlerts.GroupBy(a => a.Family)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Severity).ThenByDescending(a => a.CreatedAt).First());
        var openByFamily = canonicalOpenByFamily.ToDictionary(kv => kv.Key, kv => new OpenAlertState(kv.Value.Id, kv.Value.Severity));

        var lastResolvedByFamily = await db.Alerts
            .Where(a => a.MemberId == memberId && a.ResolvedAt != null)
            .GroupBy(a => a.Family)
            .Select(g => new { Family = g.Key, LastResolvedAt = g.Max(a => a.ResolvedAt!.Value) })
            .ToDictionaryAsync(x => x.Family, x => x.LastResolvedAt, ct);

        var decisions = AlertOrchestrator.Decide(hits, openByFamily, lastResolvedByFamily, hasHumanContactLast7d: false, DateTimeOffset.UtcNow);
        var created = 0;

        foreach (var decision in decisions)
        {
            switch (decision.Action)
            {
                case FamilyAction.CreateNew:
                case FamilyAction.CreateAsInfo:
                    var alert = new Alert
                    {
                        BoxId = boxId,
                        MemberId = memberId,
                        Family = decision.Family,
                        RuleCodes = decision.RuleCodes.ToList(),
                        Severity = decision.Severity,
                        Status = AlertStatus.New,
                        AssignedRole = UserRole.Coach,
                        DueAt = DateTimeOffset.UtcNow.Add(SlaFor(decision.Severity)),
                        Evidence = decision.Evidence.ToDictionary(kv => kv.Key, kv => kv.Value),
                        Fingerprint = $"{memberId}:{decision.Family}",
                        LastActivityAt = DateTimeOffset.UtcNow,
                    };
                    db.Alerts.Add(alert);
                    db.AlertEvents.Add(new AlertEvent { BoxId = boxId, Alert = alert, Type = AlertEventType.Created });
                    created++;

                    if (decision.Action == FamilyAction.CreateNew && decision.Severity == AlertSeverity.Amber)
                        await QueueAutoMessageAsync(boxId, member, boxName, alert.Id, decision, ct);
                    break;

                case FamilyAction.AppendToExisting:
                    var existing = canonicalOpenByFamily[decision.Family];
                    existing.Severity = decision.Severity;
                    existing.RuleCodes = existing.RuleCodes.Union(decision.RuleCodes).ToList();
                    foreach (var (k, v) in decision.Evidence) existing.Evidence[k] = v;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                    existing.LastActivityAt = DateTimeOffset.UtcNow;
                    break;
            }
        }

        return created;
    }

    /// <summary>Rule catalogue auto_message column (§7): R01/R03 amber -> MISS_YOU_SOFT, R02 -> SCHEDULE_NUDGE. R04/R08 never auto-send.</summary>
    private async Task QueueAutoMessageAsync(Guid boxId, Member member, string boxName, Guid alertId, FamilyDecision decision, CancellationToken ct)
    {
        string templateKey;
        if (decision.RuleCodes.Contains("R02")) templateKey = TemplateCatalog.ScheduleNudge;
        else if (decision.RuleCodes.Contains("R01") || decision.RuleCodes.Contains("R03")) templateKey = TemplateCatalog.MissYouSoft;
        else return;

        var variables = new Dictionary<string, string>
        {
            ["first_name"] = member.Name.Split(' ', 2)[0],
            ["box_name"] = boxName,
            ["usual_class"] = "your usual class", // MemberProfile isn't populated yet — see docs/OPEN_QUESTIONS.md
            ["coach"] = "your coach",
        };

        await outreachQueue.QueueAsync(boxId, member.Id, alertId, OutreachSentBy.System, null,
            decision.Severity, templateKey, member.Language, variables, ct: ct);
    }

    /// <summary>Plan §9: "consistency goals computed from attendance" — one GoalProgress row per ISO week, auto-updated.</summary>
    private async Task UpdateConsistencyGoalProgressAsync(Guid boxId, Guid memberId, List<BookingFact> facts, DateOnly asOf, CancellationToken ct)
    {
        var consistencyGoals = await db.Goals
            .Where(g => g.MemberId == memberId && g.Status == GoalStatus.Active && g.Category == GoalCategory.Consistency)
            .ToListAsync(ct);
        if (consistencyGoals.Count == 0) return;

        var weekStart = AttendanceMetrics.IsoWeekStart(asOf);
        var visitsThisWeek = facts.Count(f => f.Status == BookingStatus.Attended && f.SessionDate >= weekStart && f.SessionDate <= asOf);

        foreach (var goal in consistencyGoals)
        {
            var existing = await db.GoalProgresses.FirstOrDefaultAsync(p => p.GoalId == goal.Id && p.Date == weekStart, ct);
            if (existing is null)
            {
                db.GoalProgresses.Add(new GoalProgress { BoxId = boxId, GoalId = goal.Id, Date = weekStart, Value = visitsThisWeek, Source = GoalProgressSource.Automatic });
            }
            else if (existing.Source == GoalProgressSource.Automatic)
            {
                existing.Value = visitsThisWeek;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private static TimeSpan SlaFor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Red => TimeSpan.FromHours(24),
        AlertSeverity.Amber => TimeSpan.FromDays(3),
        _ => TimeSpan.FromDays(7),
    };
}
