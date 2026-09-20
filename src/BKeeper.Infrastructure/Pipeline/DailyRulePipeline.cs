using BKeeper.Application.Alerts;
using BKeeper.Application.Metrics;
using BKeeper.Application.Rules;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Domain.Rules;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Pipeline;

/// <summary>
/// The daily 05:30 job (plan §6.1, steps S4/S8/S9): evaluate every active member against the
/// enabled rules and create/update alerts. Notification/outreach (S11/S12) and the ML step
/// (S5-S7) are not built in this pass — see docs/OPEN_QUESTIONS.md.
/// </summary>
public class DailyRulePipeline(BKeeperDbContext db, CurrentBoxAccessor currentBox, IEnumerable<IRule> rules, ILogger<DailyRulePipeline> logger)
{
    public async Task RunForAllBoxesAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var boxIds = await db.Boxes.Select(b => b.Id).ToListAsync(ct);
        foreach (var boxId in boxIds)
        {
            using (currentBox.Use(boxId))
            {
                await RunForBoxAsync(boxId, asOf, ct);
            }
        }
    }

    public async Task<int> RunForBoxAsync(Guid boxId, DateOnly asOf, CancellationToken ct = default)
    {
        var ruleConfigs = await db.RuleConfigs.ToDictionaryAsync(r => r.RuleCode, ct);

        var members = await db.Members
            .Where(m => m.Status == MemberStatus.Active)
            .ToListAsync(ct);

        var alertsCreated = 0;

        foreach (var member in members)
        {
            var facts = await db.Bookings
                .Where(b => b.MemberId == member.Id)
                .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new BookingFact(DateOnly.FromDateTime(s.StartsAt.Date), b.Status, b.BookedAt))
                .ToListAsync(ct);

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

            alertsCreated += await ApplyDecisionsAsync(boxId, member.Id, hits, ruleConfigs, ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Daily rule pipeline for box {BoxId}: {Count} new alerts", boxId, alertsCreated);
        return alertsCreated;
    }

    private async Task<int> ApplyDecisionsAsync(Guid boxId, Guid memberId, List<RuleHit> hits,
        Dictionary<string, RuleConfig> ruleConfigs, CancellationToken ct)
    {
        var openAlerts = await db.Alerts
            .Where(a => a.MemberId == memberId && a.Status != AlertStatus.Resolved && a.Status != AlertStatus.AutoResolved)
            .ToListAsync(ct);
        var openByFamily = openAlerts.ToDictionary(a => a.Family, a => new OpenAlertState(a.Id, a.Severity));

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
                        AssignedRole = decision.Severity == AlertSeverity.Red ? UserRole.Manager : UserRole.Coach,
                        DueAt = DateTimeOffset.UtcNow.Add(SlaFor(decision.Severity)),
                        Evidence = decision.Evidence.ToDictionary(kv => kv.Key, kv => kv.Value),
                        Fingerprint = $"{memberId}:{decision.Family}",
                        LastActivityAt = DateTimeOffset.UtcNow,
                    };
                    db.Alerts.Add(alert);
                    db.AlertEvents.Add(new AlertEvent { BoxId = boxId, Alert = alert, Type = AlertEventType.Created });
                    created++;
                    break;

                case FamilyAction.AppendToExisting:
                    var existing = openAlerts.First(a => a.Family == decision.Family);
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

    private static TimeSpan SlaFor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Red => TimeSpan.FromHours(24),
        AlertSeverity.Amber => TimeSpan.FromDays(3),
        _ => TimeSpan.FromDays(7),
    };
}
