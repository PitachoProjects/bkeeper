using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record RiskScoreDto(Guid MemberId, string MemberName, DateOnly SnapshotWeek, double PChurn28d, string Band, List<string> TopReasons, string ModelVersion);

/// <summary>Shadow-mode churn risk (plan §8, R13) — Manager/Owner only, per the plan ("shown to Manager only").</summary>
[ApiController]
[Route("risk-scores")]
[Authorize]
public class RiskScoresController(BKeeperDbContext db, MlScoringJob scoringJob) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RiskScoreDto>>> List()
    {
        if (!IsManagerOrOwner()) return Forbid();

        var latestWeek = await db.RiskScores.OrderByDescending(r => r.SnapshotWeek).Select(r => (DateOnly?)r.SnapshotWeek).FirstOrDefaultAsync();
        if (latestWeek is null) return Ok(new List<RiskScoreDto>());

        // Order before projecting into the record DTO — EF Core's SQL translator can't always map
        // an OrderBy back to a column once the shape is a projected record rather than the entity.
        var scores = await db.RiskScores.Where(r => r.SnapshotWeek == latestWeek)
            .OrderByDescending(r => r.PChurn28d)
            .Join(db.Members, r => r.MemberId, m => m.Id, (r, m) => new RiskScoreDto(r.MemberId, m.Name, r.SnapshotWeek, r.PChurn28d, r.Band, r.TopReasons, r.ModelVersion))
            .ToListAsync();
        return Ok(scores);
    }

    [HttpGet("members/{memberId:guid}")]
    public async Task<ActionResult<RiskScoreDto?>> ForMember(Guid memberId)
    {
        if (!IsManagerOrOwner()) return Forbid();

        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var latest = await db.RiskScores.Where(r => r.MemberId == memberId).OrderByDescending(r => r.SnapshotWeek).FirstOrDefaultAsync();
        if (latest is null) return Ok(null);

        return Ok(new RiskScoreDto(latest.MemberId, member.Name, latest.SnapshotWeek, latest.PChurn28d, latest.Band, latest.TopReasons, latest.ModelVersion));
    }

    /// <summary>Runs the weekly shadow-mode scoring for the caller's box on demand (also runs Sundays 23:30 via the Worker).</summary>
    [HttpPost("run")]
    public async Task<ActionResult<object>> Run()
    {
        if (!IsManagerOrOwner()) return Forbid();

        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        var scored = await scoringJob.RunForBoxAsync(boxId, DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(new { scored });
    }

    private bool IsManagerOrOwner()
    {
        var role = User.FindFirst("role")?.Value;
        return role is "Manager" or "Owner";
    }
}
