using System.Security.Claims;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record AlertListItem(Guid Id, Guid MemberId, string MemberName, AlertFamily Family, AlertSeverity Severity,
    AlertStatus Status, DateTimeOffset DueAt, List<string> RuleCodes);
public record ResolveAlertRequest(string Outcome, string? Note);

[ApiController]
[Route("alerts")]
[Authorize]
public class AlertsController(BKeeperDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AlertListItem>>> List([FromQuery] AlertStatus? status, [FromQuery] AlertSeverity? severity)
    {
        var query = db.Alerts.Include(a => a.Member).AsQueryable();
        if (status.HasValue) query = query.Where(a => a.Status == status);
        else query = query.Where(a => a.Status != AlertStatus.Resolved && a.Status != AlertStatus.AutoResolved);
        if (severity.HasValue) query = query.Where(a => a.Severity == severity);

        var alerts = await query.OrderByDescending(a => a.Severity).ThenBy(a => a.DueAt)
            .Select(a => new AlertListItem(a.Id, a.MemberId, a.Member!.Name, a.Family, a.Severity, a.Status, a.DueAt, a.RuleCodes))
            .ToListAsync();
        return Ok(alerts);
    }

    [HttpPost("{id:guid}/claim")]
    public async Task<IActionResult> Claim(Guid id)
    {
        var alert = await db.Alerts.FindAsync(id);
        if (alert is null) return NotFound();

        alert.Status = AlertStatus.Claimed;
        alert.ClaimedBy = CurrentUserId();
        alert.LastActivityAt = DateTimeOffset.UtcNow;
        alert.UpdatedAt = DateTimeOffset.UtcNow;
        db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.Claimed, ActorUserId = CurrentUserId() });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/snooze")]
    public async Task<IActionResult> Snooze(Guid id, [FromQuery] DateTimeOffset until)
    {
        var alert = await db.Alerts.FindAsync(id);
        if (alert is null) return NotFound();

        alert.Status = AlertStatus.Snoozed;
        alert.SnoozeUntil = until;
        alert.UpdatedAt = DateTimeOffset.UtcNow;
        db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.Snoozed, ActorUserId = CurrentUserId() });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, ResolveAlertRequest request)
    {
        var alert = await db.Alerts.FindAsync(id);
        if (alert is null) return NotFound();

        alert.Status = AlertStatus.Resolved;
        alert.Outcome = request.Outcome;
        alert.OutcomeNote = request.Note;
        alert.ResolvedAt = DateTimeOffset.UtcNow;
        alert.UpdatedAt = DateTimeOffset.UtcNow;
        db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.Resolved, ActorUserId = CurrentUserId() });

        await db.SaveChangesAsync();
        return NoContent();
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;
}
