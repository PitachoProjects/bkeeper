using BKeeper.Application.Coaches;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record CoachDto(Guid Id, string Name, string? Email, CoachStatus Status, Guid? ApplicationUserId);
public record CreateCoachRequest(string Name, string? Email, Guid? ApplicationUserId);
public record UpdateCoachRequest(string Name, string? Email, CoachStatus Status, Guid? ApplicationUserId);

/// <summary>Minimal CRUD for the Coach entity (promoted out of ClassSession.CoachName free text —
/// see Coach's doc comment). List/get is open to any authenticated staff role; create/update is
/// Owner/Manager only, matching RiskScoresController's gating pattern.</summary>
[ApiController]
[Route("coaches")]
[Authorize]
public class CoachesController(BKeeperDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CoachDto>>> List([FromQuery] CoachStatus? status)
    {
        var query = db.Coaches.AsQueryable();
        if (status.HasValue) query = query.Where(c => c.Status == status);

        var coaches = await query.OrderBy(c => c.Name)
            .Select(c => new CoachDto(c.Id, c.Name, c.Email, c.Status, c.ApplicationUserId))
            .ToListAsync();
        return Ok(coaches);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CoachDto>> Get(Guid id)
    {
        var coach = await db.Coaches.FindAsync(id);
        if (coach is null) return NotFound();
        return Ok(new CoachDto(coach.Id, coach.Name, coach.Email, coach.Status, coach.ApplicationUserId));
    }

    [HttpPost]
    public async Task<ActionResult<CoachDto>> Create(CreateCoachRequest request)
    {
        if (!IsManagerOrOwner()) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");

        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        var coach = new Coach { BoxId = boxId, Name = request.Name.Trim(), Email = request.Email, ApplicationUserId = request.ApplicationUserId };
        db.Coaches.Add(coach);
        await db.SaveChangesAsync();

        return Ok(new CoachDto(coach.Id, coach.Name, coach.Email, coach.Status, coach.ApplicationUserId));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CoachDto>> Update(Guid id, UpdateCoachRequest request)
    {
        if (!IsManagerOrOwner()) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");

        var coach = await db.Coaches.FindAsync(id);
        if (coach is null) return NotFound();

        coach.Name = request.Name.Trim();
        coach.Email = request.Email;
        coach.Status = request.Status;
        coach.ApplicationUserId = request.ApplicationUserId;
        coach.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new CoachDto(coach.Id, coach.Name, coach.Email, coach.Status, coach.ApplicationUserId));
    }

    /// <summary>On-demand re-run of the historical-data backfill the migration already does once at
    /// deploy time (see AddCoachesAndBackfillFromSessions) — useful after a later Excel import brings in
    /// new CoachName values that never got linked. Idempotent: only creates coaches for names with no
    /// existing Coach row, and only links sessions whose CoachId is still null.</summary>
    [HttpPost("backfill")]
    public async Task<ActionResult<object>> Backfill()
    {
        if (!IsManagerOrOwner()) return Forbid();

        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);

        var sessionCoachNames = await db.ClassSessions.Where(s => s.BoxId == boxId && s.CoachId == null && s.CoachName != null)
            .Select(s => new ClassSessionCoachName(s.BoxId, s.CoachName))
            .ToListAsync();
        var existingCoaches = await db.Coaches.Where(c => c.BoxId == boxId)
            .Select(c => new ExistingCoach(c.BoxId, c.Name))
            .ToListAsync();

        var toCreate = CoachBackfillPlanner.Plan(sessionCoachNames, existingCoaches);
        foreach (var c in toCreate)
        {
            db.Coaches.Add(new Coach { BoxId = c.BoxId, Name = c.Name });
        }
        await db.SaveChangesAsync();

        var coachesByName = await db.Coaches.Where(c => c.BoxId == boxId).ToDictionaryAsync(c => c.Name);
        var unlinkedSessions = await db.ClassSessions.Where(s => s.BoxId == boxId && s.CoachId == null && s.CoachName != null).ToListAsync();
        var linked = 0;
        foreach (var session in unlinkedSessions)
        {
            var name = session.CoachName!.Trim();
            if (name.Length == 0 || !coachesByName.TryGetValue(name, out var coach)) continue;
            session.CoachId = coach.Id;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            linked++;
        }
        await db.SaveChangesAsync();

        return Ok(new { coachesCreated = toCreate.Count, sessionsLinked = linked });
    }

    private bool IsManagerOrOwner()
    {
        var role = User.FindFirst("role")?.Value;
        return role is "Manager" or "Owner";
    }
}
