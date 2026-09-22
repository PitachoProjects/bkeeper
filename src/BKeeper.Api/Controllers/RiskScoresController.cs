using BKeeper.Application.Ml;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record RiskScoreDto(Guid MemberId, string MemberName, DateOnly SnapshotWeek, double PChurn28d, string Band, List<string> TopReasons, string ModelVersion, string ModelType);

public record RiskScoreComparisonDto(Guid MemberId, string MemberName, RiskScoreDto? Primary, RiskScoreDto? Comparison);

/// <summary>Shadow-mode churn risk (plan §8, R13) — Manager/Owner only, per the plan ("shown to Manager only").
/// Two models are scored every run (see MlScoringJob): the LightGBM ensemble (Stage C) is the
/// primary/default view everywhere in this controller unless "logistic" or "compare" is named
/// explicitly; the logistic regression (Stage B) is additive, comparison-only, and — like the
/// LightGBM ensemble — never creates an alert by itself.</summary>
[ApiController]
[Route("risk-scores")]
[Authorize]
public class RiskScoresController(BKeeperDbContext db, MlScoringJob scoringJob) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RiskScoreDto>>> List([FromQuery] string modelType = MlModelTypes.LightGbmEnsemble)
    {
        if (!IsManagerOrOwner()) return Forbid();

        var latestWeek = await db.RiskScores.Where(r => r.ModelType == modelType)
            .OrderByDescending(r => r.SnapshotWeek).Select(r => (DateOnly?)r.SnapshotWeek).FirstOrDefaultAsync();
        if (latestWeek is null) return Ok(new List<RiskScoreDto>());

        // Order before projecting into the record DTO — EF Core's SQL translator can't always map
        // an OrderBy back to a column once the shape is a projected record rather than the entity.
        var scores = await db.RiskScores.Where(r => r.SnapshotWeek == latestWeek && r.ModelType == modelType)
            .OrderByDescending(r => r.PChurn28d)
            .Join(db.Members, r => r.MemberId, m => m.Id, (r, m) => new RiskScoreDto(r.MemberId, m.Name, r.SnapshotWeek, r.PChurn28d, r.Band, r.TopReasons, r.ModelVersion, r.ModelType))
            .ToListAsync();
        return Ok(scores);
    }

    [HttpGet("members/{memberId:guid}")]
    public async Task<ActionResult<RiskScoreDto?>> ForMember(Guid memberId, [FromQuery] string modelType = MlModelTypes.LightGbmEnsemble)
    {
        if (!IsManagerOrOwner()) return Forbid();

        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var latest = await db.RiskScores.Where(r => r.MemberId == memberId && r.ModelType == modelType)
            .OrderByDescending(r => r.SnapshotWeek).FirstOrDefaultAsync();
        if (latest is null) return Ok(null);

        return Ok(new RiskScoreDto(latest.MemberId, member.Name, latest.SnapshotWeek, latest.PChurn28d, latest.Band, latest.TopReasons, latest.ModelVersion, latest.ModelType));
    }

    /// <summary>Both models' latest score for one member, side by side — the Stage B (logistic
    /// regression) comparison view, including its top contributing factors by coefficient
    /// (<see cref="RiskScoreDto.TopReasons"/>, from ml/app/explain.py's `top_reasons_linear`)
    /// next to Stage C's (LightGBM ensemble, SHAP-derived) reasons.</summary>
    [HttpGet("members/{memberId:guid}/compare")]
    public async Task<ActionResult<RiskScoreComparisonDto>> CompareForMember(Guid memberId)
    {
        if (!IsManagerOrOwner()) return Forbid();

        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var scores = await db.RiskScores.Where(r => r.MemberId == memberId).ToListAsync();
        var primary = scores.Where(r => r.ModelType == MlModelTypes.LightGbmEnsemble)
            .OrderByDescending(r => r.SnapshotWeek).FirstOrDefault();
        var comparison = scores.Where(r => r.ModelType == MlModelTypes.LogisticRegression)
            .OrderByDescending(r => r.SnapshotWeek).FirstOrDefault();

        return Ok(new RiskScoreComparisonDto(
            memberId,
            member.Name,
            primary is null ? null : new RiskScoreDto(primary.MemberId, member.Name, primary.SnapshotWeek, primary.PChurn28d, primary.Band, primary.TopReasons, primary.ModelVersion, primary.ModelType),
            comparison is null ? null : new RiskScoreDto(comparison.MemberId, member.Name, comparison.SnapshotWeek, comparison.PChurn28d, comparison.Band, comparison.TopReasons, comparison.ModelVersion, comparison.ModelType)));
    }

    /// <summary>Runs the weekly shadow-mode scoring for the caller's box on demand (also runs Sundays 23:30 via the Worker).
    /// Scores both models (see MlScoringJob); the returned count is the primary (LightGBM ensemble) model's.</summary>
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
