using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Infrastructure.Alerts;

/// <summary>
/// Create-or-append for the single-rule-hit call sites (R10/R11, EVAL_NEG/HEALTH form flags) that
/// don't need <see cref="Application.Alerts.AlertOrchestrator"/>'s batched multi-hit grouping —
/// DailyRulePipeline still uses that directly for R01-R08, which can fire several rules at once.
/// </summary>
public class SimpleAlertService(BKeeperDbContext db)
{
    public async Task<bool> CreateOrAppendAsync(Guid boxId, Guid memberId, string ruleCode, AlertFamily family,
        AlertSeverity severity, IReadOnlyDictionary<string, object> evidence, CancellationToken ct = default)
    {
        var open = await db.Alerts.Where(a => a.MemberId == memberId && a.Family == family
            && a.Status != AlertStatus.Resolved && a.Status != AlertStatus.AutoResolved).ToListAsync(ct);

        if (open.Count > 0)
        {
            var existing = open[0];
            if (!existing.RuleCodes.Contains(ruleCode)) existing.RuleCodes.Add(ruleCode);
            if (severity > existing.Severity) existing.Severity = severity;
            foreach (var (k, v) in evidence) existing.Evidence[k] = v;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.LastActivityAt = DateTimeOffset.UtcNow;
            return false;
        }

        var alert = new Alert
        {
            BoxId = boxId,
            MemberId = memberId,
            Family = family,
            RuleCodes = [ruleCode],
            Severity = severity,
            Status = AlertStatus.New,
            AssignedRole = UserRole.Coach,
            DueAt = DateTimeOffset.UtcNow.AddDays(severity == AlertSeverity.Red ? 1 : 3),
            Evidence = evidence.ToDictionary(kv => kv.Key, kv => kv.Value),
            Fingerprint = $"{memberId}:{family}",
            LastActivityAt = DateTimeOffset.UtcNow,
        };
        db.Alerts.Add(alert);
        db.AlertEvents.Add(new AlertEvent { BoxId = boxId, Alert = alert, Type = AlertEventType.Created });
        return true;
    }
}
