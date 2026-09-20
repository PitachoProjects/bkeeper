using System.Security.Claims;
using BKeeper.Domain.Alerts;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record AlertListItem(Guid Id, Guid MemberId, string MemberName, AlertFamily Family, AlertSeverity Severity,
    AlertStatus Status, UserRole AssignedRole, Guid? ClaimedBy, DateTimeOffset DueAt, List<string> RuleCodes);
public record ResolveAlertRequest(string Outcome, string? Note);

[ApiController]
[Route("alerts")]
[Authorize]
public class AlertsController(BKeeperDbContext db, EscalationJob escalationJob) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AlertListItem>>> List(
        [FromQuery] AlertStatus? status, [FromQuery] AlertSeverity? severity, [FromQuery] UserRole? role)
    {
        var query = db.Alerts.Include(a => a.Member).AsQueryable();
        if (status.HasValue) query = query.Where(a => a.Status == status);
        else query = query.Where(a => a.Status != AlertStatus.Resolved && a.Status != AlertStatus.AutoResolved);
        if (severity.HasValue) query = query.Where(a => a.Severity == severity);
        if (role.HasValue) query = query.Where(a => a.AssignedRole == role);

        var alerts = await query.OrderByDescending(a => a.Severity).ThenBy(a => a.DueAt)
            .Select(a => new AlertListItem(a.Id, a.MemberId, a.Member!.Name, a.Family, a.Severity, a.Status, a.AssignedRole, a.ClaimedBy, a.DueAt, a.RuleCodes))
            .ToListAsync();
        return Ok(alerts);
    }

    /// <summary>The fixed outcome vocabulary (plan §6.4) for the resolve dialog.</summary>
    [HttpGet("outcomes")]
    public ActionResult<IReadOnlyList<string>> Outcomes() => Ok(OutcomeTaxonomy.Values);

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
        if (!OutcomeTaxonomy.IsValid(request.Outcome))
            return BadRequest($"Outcome must be one of: {string.Join(", ", OutcomeTaxonomy.Values)}");

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

    /// <summary>Runs the escalation/auto-resolve sweep for the caller's box on demand (also runs every 15 min via the Worker).</summary>
    [HttpPost("escalate/run")]
    public async Task<IActionResult> RunEscalation()
    {
        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        await escalationJob.RunForBoxAsync(boxId, DateTimeOffset.UtcNow);
        return NoContent();
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;
}
