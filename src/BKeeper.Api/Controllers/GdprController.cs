using System.Security.Claims;
using BKeeper.Infrastructure.Audit;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

/// <summary>Plan §11/§14: GDPR data export and the anonymization job's on-demand trigger.</summary>
[ApiController]
[Route("members/{memberId:guid}/gdpr")]
[Authorize]
public class GdprController(BKeeperDbContext db, AuditLogger audit) : ControllerBase
{
    /// <summary>Every piece of personal data BKeeper holds on this member, in one bundle (plan §14: "export contains all personal data").</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(Guid memberId)
    {
        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var bundle = new
        {
            member.Id,
            member.ExternalId,
            member.Name,
            member.Email,
            member.PhoneE164,
            member.BirthYear,
            member.JoinDate,
            member.Status,
            member.CancelDate,
            member.CancelReason,
            member.Language,
            Notes = await db.MemberNotes.Where(n => n.MemberId == memberId)
                .Select(n => new { n.Text, n.Source, n.CreatedAt, n.IsActive }).ToListAsync(),
            Memberships = await db.Memberships.Where(m => m.MemberId == memberId)
                .Select(m => new { m.PlanName, m.StartDate, m.EndDate, m.Status }).ToListAsync(),
            Bookings = await db.Bookings.Where(b => b.MemberId == memberId)
                .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { s.StartsAt, b.Status, b.BookedAt }).ToListAsync(),
            Goals = await db.Goals.Where(g => g.MemberId == memberId)
                .Select(g => new { g.Category, g.Description, g.Status, g.TargetDate }).ToListAsync(),
            EvaluationResponses = await db.EvaluationResponses.Where(r => r.MemberId == memberId)
                .Select(r => new { r.AnsweredAt, r.Answers, r.Scores }).ToListAsync(),
            Outreach = await db.Outreaches.Where(o => o.MemberId == memberId)
                .Select(o => new { o.Channel, o.Body, o.SentBy, o.Status, o.CreatedAt }).ToListAsync(),
            Consent = await db.MemberConsents.Where(c => c.MemberId == memberId)
                .Select(c => new { c.Channel, c.Granted }).ToListAsync(),
        };

        audit.Log(member.BoxId, CurrentUserId(), "gdpr_export", $"member:{memberId}");
        await db.SaveChangesAsync();

        return Ok(bundle);
    }

    /// <summary>Runs the anonymization job for this member on demand (also runs daily for all eligible members).</summary>
    [HttpPost("anonymize")]
    public async Task<IActionResult> Anonymize(Guid memberId, [FromServices] AnonymizationJob job)
    {
        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        member.Name = "Anonymized member";
        member.Email = null;
        member.PhoneE164 = null;
        member.BirthYear = null;
        member.UpdatedAt = DateTimeOffset.UtcNow;
        audit.Log(member.BoxId, CurrentUserId(), "anonymize_manual", $"member:{memberId}");
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Runs the retention-driven anonymization sweep (plan §11/§14: 24+ months cancelled) for the caller's box on demand.</summary>
    [HttpPost("/gdpr/anonymize/run")]
    public async Task<ActionResult<object>> RunAnonymizationSweep([FromServices] AnonymizationJob job)
    {
        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        var anonymized = await job.RunForBoxAsync(boxId, DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(new { anonymized });
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;
}
