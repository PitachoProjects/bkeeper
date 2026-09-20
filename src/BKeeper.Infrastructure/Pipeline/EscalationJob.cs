using BKeeper.Application.Alerts;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Pipeline;

/// <summary>
/// The 15-minute escalation job (plan §6.4): promotes unclaimed alerts up the SLA ladder, releases
/// claimed-but-idle alerts back to the pool, auto-expires stale info alerts, and auto-resolves alerts
/// for members who've returned (2 visits in 10 days).
/// </summary>
public class EscalationJob(BKeeperDbContext db, CurrentBoxAccessor currentBox, ILogger<EscalationJob> logger)
{
    public async Task RunForAllBoxesAsync(CancellationToken ct = default)
    {
        var boxIds = await db.Boxes.Select(b => b.Id).ToListAsync(ct);
        foreach (var boxId in boxIds)
        {
            using (currentBox.Use(boxId))
            {
                await RunForBoxAsync(boxId, DateTimeOffset.UtcNow, ct);
            }
        }
    }

    public async Task RunForBoxAsync(Guid boxId, DateTimeOffset now, CancellationToken ct = default)
    {
        await WakeSnoozedAlertsAsync(now, ct);
        var escalated = await ApplyEscalationsAsync(now, ct);
        var autoResolved = await AutoResolveReturnedMembersAsync(now, ct);
        await db.SaveChangesAsync(ct);

        if (escalated > 0 || autoResolved > 0)
            logger.LogInformation("Escalation job for box {BoxId}: {Escalated} escalated/released/expired, {AutoResolved} auto-resolved", boxId, escalated, autoResolved);
    }

    private async Task WakeSnoozedAlertsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var due = await db.Alerts.Where(a => a.Status == AlertStatus.Snoozed && a.SnoozeUntil <= now).ToListAsync(ct);
        foreach (var alert in due)
        {
            alert.Status = AlertStatus.New;
            alert.SnoozeUntil = null;
            alert.UpdatedAt = now;
        }
    }

    private async Task<int> ApplyEscalationsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var openAlerts = await db.Alerts
            .Where(a => a.Status == AlertStatus.New || a.Status == AlertStatus.Escalated
                     || a.Status == AlertStatus.Claimed || a.Status == AlertStatus.InProgress)
            .ToListAsync(ct);

        var count = 0;
        foreach (var alert in openAlerts)
        {
            var snapshot = new AlertSnapshot(alert.Severity, alert.Status, alert.AssignedRole, alert.CreatedAt, alert.LastActivityAt ?? alert.CreatedAt);
            var action = EscalationPolicy.Evaluate(snapshot, now);
            if (action == EscalationAction.None) continue;

            switch (action)
            {
                case EscalationAction.EscalateToManager:
                    alert.AssignedRole = UserRole.Manager;
                    alert.Status = AlertStatus.Escalated;
                    db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.Escalated, Payload = "to Manager (SLA breach 1)" });
                    break;
                case EscalationAction.EscalateToOwner:
                    alert.AssignedRole = UserRole.Owner;
                    alert.Status = AlertStatus.Escalated;
                    db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.Escalated, Payload = "to Owner (SLA breach 2)" });
                    break;
                case EscalationAction.ReleaseIdleClaim:
                    alert.Status = AlertStatus.New;
                    alert.ClaimedBy = null;
                    db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.Reopened, Payload = "released after idle claim" });
                    break;
                case EscalationAction.AutoExpire:
                    alert.Status = AlertStatus.Resolved;
                    alert.Outcome = "false_positive_no_action";
                    alert.OutcomeNote = "auto-expired after 14 days with no action";
                    alert.ResolvedAt = now;
                    db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.Resolved, Payload = "auto-expired" });
                    break;
            }

            alert.UpdatedAt = now;
            count++;
        }

        return count;
    }

    private async Task<int> AutoResolveReturnedMembersAsync(DateTimeOffset now, CancellationToken ct)
    {
        var openAlerts = await db.Alerts
            .Where(a => a.Status == AlertStatus.New || a.Status == AlertStatus.Escalated
                     || a.Status == AlertStatus.Claimed || a.Status == AlertStatus.InProgress)
            .ToListAsync(ct);
        if (openAlerts.Count == 0) return 0;

        var since = DateOnly.FromDateTime(now.AddDays(-10).Date);
        var count = 0;

        foreach (var memberGroup in openAlerts.GroupBy(a => a.MemberId))
        {
            var recentVisits = await db.Bookings
                .Where(b => b.MemberId == memberGroup.Key && b.Status == BookingStatus.Attended)
                .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => s.StartsAt)
                .CountAsync(startsAt => startsAt >= since.ToDateTime(TimeOnly.MinValue), ct);

            if (recentVisits < 2) continue;

            foreach (var alert in memberGroup)
            {
                alert.Status = AlertStatus.AutoResolved;
                alert.Outcome = "returned";
                alert.OutcomeNote = "auto-resolved: 2+ visits in the last 10 days";
                alert.ResolvedAt = now;
                alert.UpdatedAt = now;
                db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.AutoResolved });
                count++;
            }
        }

        return count;
    }
}
