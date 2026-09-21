using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record MemberListItem(Guid Id, string Name, string? Email, MemberStatus Status, DateOnly JoinDate);
public record MemberDetail(Guid Id, string Name, string? Email, string? Phone, MemberStatus Status, DateOnly JoinDate, DateOnly? AwayUntil, DateOnly? InjuryFlagUntil);
public record MemberNoteDto(Guid Id, string Text, NoteSource Source, bool IsActive, DateTimeOffset CreatedAt);
public record CreateMemberNoteRequest(string Text);
public record TimelineItem(string Type, DateTimeOffset At, string Summary);
public record ConsentDto(NotificationChannel Channel, bool Granted);
public record SetConsentRequest(NotificationChannel Channel, bool Granted);
public record OutreachDto(Guid Id, NotificationChannel Channel, string? TemplateKey, string Body, OutreachSentBy SentBy,
    OutreachStatus Status, bool IsHoldout, DateTimeOffset CreatedAt);
public record WorkoutMixItem(string Tag, int Count);

[ApiController]
[Route("members")]
[Authorize]
public class MembersController(BKeeperDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MemberListItem>>> List([FromQuery] MemberStatus? status, [FromQuery] string? search)
    {
        var query = db.Members.AsQueryable();
        if (status.HasValue) query = query.Where(m => m.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(m => m.Name.Contains(search));

        var members = await query.OrderBy(m => m.Name)
            .Select(m => new MemberListItem(m.Id, m.Name, m.Email, m.Status, m.JoinDate))
            .ToListAsync();
        return Ok(members);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MemberDetail>> Get(Guid id)
    {
        var member = await db.Members.FindAsync(id);
        if (member is null) return NotFound();
        return Ok(new MemberDetail(member.Id, member.Name, member.Email, member.PhoneE164, member.Status, member.JoinDate, member.AwayUntil, member.InjuryFlagUntil));
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<List<TimelineItem>>> Timeline(Guid id)
    {
        var bookings = await db.Bookings.Where(b => b.MemberId == id)
            .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new TimelineItem(b.Status.ToString(), s.StartsAt, $"{b.Status} — {s.ClassType} ({s.Window})"))
            .ToListAsync();

        var notes = await db.MemberNotes.Where(n => n.MemberId == id && n.IsActive)
            .Select(n => new TimelineItem("note", n.CreatedAt, n.Text))
            .ToListAsync();

        var timeline = bookings.Concat(notes).OrderByDescending(t => t.At).ToList();
        return Ok(timeline);
    }

    /// <summary>The member's list of notes — free-text context like "recovering from a knee injury",
    /// entered by staff or delivered by an import (Notes sheet, source=Import).</summary>
    [HttpGet("{id:guid}/notes")]
    public async Task<ActionResult<List<MemberNoteDto>>> GetNotes(Guid id)
    {
        var notes = await db.MemberNotes.Where(n => n.MemberId == id)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new MemberNoteDto(n.Id, n.Text, n.Source, n.IsActive, n.CreatedAt))
            .ToListAsync();
        return Ok(notes);
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<MemberNoteDto>> AddNote(Guid id, CreateMemberNoteRequest request)
    {
        var member = await db.Members.FindAsync(id);
        if (member is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Text)) return BadRequest("Note text is required.");

        var note = new MemberNote { BoxId = member.BoxId, MemberId = id, Text = request.Text.Trim(), Source = NoteSource.Coach };
        db.MemberNotes.Add(note);
        await db.SaveChangesAsync();

        return Ok(new MemberNoteDto(note.Id, note.Text, note.Source, note.IsActive, note.CreatedAt));
    }

    [HttpDelete("{id:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> DeactivateNote(Guid id, Guid noteId)
    {
        var note = await db.MemberNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.MemberId == id);
        if (note is null) return NotFound();
        note.IsActive = false;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Channel consent, defaulting to granted (no row = opt-in) — see MemberConsent's doc comment.</summary>
    [HttpGet("{id:guid}/consent")]
    public async Task<ActionResult<List<ConsentDto>>> GetConsent(Guid id)
    {
        var rows = await db.MemberConsents.Where(c => c.MemberId == id).ToDictionaryAsync(c => c.Channel, c => c.Granted);
        var all = Enum.GetValues<NotificationChannel>().Select(ch => new ConsentDto(ch, rows.GetValueOrDefault(ch, true)));
        return Ok(all.ToList());
    }

    [HttpPut("{id:guid}/consent")]
    public async Task<IActionResult> SetConsent(Guid id, SetConsentRequest request)
    {
        var member = await db.Members.FindAsync(id);
        if (member is null) return NotFound();

        var row = await db.MemberConsents.FirstOrDefaultAsync(c => c.MemberId == id && c.Channel == request.Channel);
        if (row is null)
        {
            db.MemberConsents.Add(new MemberConsent { BoxId = member.BoxId, MemberId = id, Channel = request.Channel, Granted = request.Granted });
        }
        else
        {
            row.Granted = request.Granted;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/outreach")]
    public async Task<ActionResult<List<OutreachDto>>> GetOutreach(Guid id)
    {
        var history = await db.Outreaches.Where(o => o.MemberId == id)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OutreachDto(o.Id, o.Channel, o.TemplateKey, o.Body, o.SentBy, o.Status, o.IsHoldout, o.CreatedAt))
            .ToListAsync();
        return Ok(history);
    }

    /// <summary>Per-member workout-type mix (plan-adjacent: same tag-weighted classification as the box-wide
    /// Dashboards/Workouts heatmap, just scoped to one member) for the "trends/habits" radar chart.
    /// Zero-fills every <see cref="WorkoutTagType"/> so the chart always has a consistent set of axes.</summary>
    [HttpGet("{id:guid}/workout-mix")]
    public async Task<ActionResult<List<WorkoutMixItem>>> WorkoutMix(Guid id, [FromQuery] int days = 120)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var visits = await db.Bookings.Where(b => b.MemberId == id && b.Status == BookingStatus.Attended)
            .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { s.WorkoutId, s.StartsAt })
            .Where(v => v.StartsAt >= since)
            .ToListAsync();

        var tags = await db.WorkoutTags.ToListAsync();
        var tagsByWorkout = tags.GroupBy(t => t.WorkoutId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Weight).First().Tag);

        var counts = visits
            .Where(v => v.WorkoutId.HasValue && tagsByWorkout.ContainsKey(v.WorkoutId.Value))
            .GroupBy(v => tagsByWorkout[v.WorkoutId!.Value])
            .ToDictionary(g => g.Key, g => g.Count());

        var mix = Enum.GetValues<WorkoutTagType>()
            .Select(tag => new WorkoutMixItem(tag.ToString(), counts.GetValueOrDefault(tag, 0)))
            .ToList();
        return Ok(mix);
    }
}
