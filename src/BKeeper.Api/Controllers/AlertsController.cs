using System.Security.Claims;
using BKeeper.Application.Notifications;
using BKeeper.Domain.Alerts;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Notifications;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record AlertListItem(Guid Id, Guid MemberId, string MemberName, AlertFamily Family, AlertSeverity Severity,
    AlertStatus Status, UserRole AssignedRole, Guid? ClaimedBy, DateTimeOffset DueAt, List<string> RuleCodes);
public record ResolveAlertRequest(string Outcome, string? Note);
public record SendOutreachRequest(string? TemplateKey, string? CustomBody);

[ApiController]
[Route("alerts")]
[Authorize]
public class AlertsController(BKeeperDbContext db, EscalationJob escalationJob, OutreachQueueService outreachQueue) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AlertListItem>>> List(
        [FromQuery] AlertStatus? status, [FromQuery] AlertSeverity? severity, [FromQuery] UserRole? role, [FromQuery] Guid? memberId)
    {
        var query = db.Alerts.Include(a => a.Member).AsQueryable();
        if (status.HasValue) query = query.Where(a => a.Status == status);
        else query = query.Where(a => a.Status != AlertStatus.Resolved && a.Status != AlertStatus.AutoResolved);
        if (severity.HasValue) query = query.Where(a => a.Severity == severity);
        if (role.HasValue) query = query.Where(a => a.AssignedRole == role);
        if (memberId.HasValue) query = query.Where(a => a.MemberId == memberId);

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

    /// <summary>Coach one-tap send (plan §10): a template key (rendered + editable client-side before calling)
    /// or free text. Skips the frequency cap and holdout — those only gate automated sends.</summary>
    [HttpPost("{id:guid}/outreach")]
    public async Task<IActionResult> SendOutreach(Guid id, SendOutreachRequest request)
    {
        var alert = await db.Alerts.Include(a => a.Member).FirstOrDefaultAsync(a => a.Id == id);
        if (alert?.Member is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.TemplateKey) && string.IsNullOrWhiteSpace(request.CustomBody))
            return BadRequest("Provide templateKey or customBody.");

        var box = await db.Boxes.FindAsync(alert.BoxId);
        var variables = new Dictionary<string, string>
        {
            ["first_name"] = alert.Member.Name.Split(' ', 2)[0],
            ["box_name"] = box?.Name ?? "your box",
            ["usual_class"] = "your usual class",
            ["coach"] = "your coach",
            ["form_link"] = "",
        };

        var result = await outreachQueue.QueueAsync(alert.BoxId, alert.MemberId, alert.Id, OutreachSentBy.User, CurrentUserId(),
            alert.Severity, request.TemplateKey, alert.Member.Language, variables, request.CustomBody);

        if (result.Outreach is null)
        {
            return BadRequest(result.Failure switch
            {
                OutreachQueueFailure.NoConsentOnAnyChannel => "Member has no consent on any channel (WhatsApp, push, or email).",
                OutreachQueueFailure.UnknownTemplate => "Unknown or unrenderable template key.",
                OutreachQueueFailure.RequiresHuman => "This alert requires a human-sent message.",
                OutreachQueueFailure.DroppedFrequencyCap => "An automated message was already sent to this member in the last 7 days.",
                _ => "Could not queue the message.",
            });
        }

        db.AlertEvents.Add(new AlertEvent { BoxId = alert.BoxId, AlertId = alert.Id, Type = AlertEventType.Outreach, ActorUserId = CurrentUserId() });
        alert.LastActivityAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok();
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;
}
