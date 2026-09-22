using System.Security.Claims;
using BKeeper.Domain.Entities;
using BKeeper.Infrastructure.Audit;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record HealthScoreConfigDto(
    int Version, double AttendanceWeight, double ConsistencyWeight, double BookingBehaviourWeight, double ProgressWeight, double EngagementWeight,
    bool AttendanceEnabled, bool ConsistencyEnabled, bool BookingBehaviourEnabled, bool ProgressEnabled, bool EngagementEnabled,
    int AttendanceWindowDays, int ConsistencyWindowWeeks, int BookingBehaviourWindowDays, int ProgressWindowDays, int EngagementWindowDays,
    int MinTenureDays, int MinSessions, DateTimeOffset ActivatedAt);

public record UpdateHealthScoreConfigRequest(
    double AttendanceWeight, double ConsistencyWeight, double BookingBehaviourWeight, double ProgressWeight, double EngagementWeight,
    bool AttendanceEnabled, bool ConsistencyEnabled, bool BookingBehaviourEnabled, bool ProgressEnabled, bool EngagementEnabled,
    int? AttendanceWindowDays, int? ConsistencyWindowWeeks, int? BookingBehaviourWindowDays, int? ProgressWindowDays, int? EngagementWindowDays,
    int? MinTenureDays, int? MinSessions);

public record HealthScoreFactorDto(string Factor, double? Score, double Weight, double Contribution, bool Included, string? Reason);

public record HealthScoreDto(
    Guid MemberId, DateOnly CalculationDate, int ConfigVersion, double? OverallScore, bool InsufficientData,
    int TenureDays, int SessionCount, List<HealthScoreFactorDto> Factors, DateTimeOffset CalculatedAt);

/// <summary>
/// Athlete Health Score (product spec, scoped to what this codebase has data for — see docs/DECISIONS.md).
/// Config is Owner/Manager-only to change; scores are visible to anyone who can view the member (unlike
/// the shadow-mode ML RiskScore, this is a deterministic, explainable score meant for coaches too).
/// </summary>
[ApiController]
[Authorize]
public class HealthScoreController(BKeeperDbContext db, HealthScoreJob job, AuditLogger audit) : ControllerBase
{
    private const double WeightSumTolerance = 0.01;

    [HttpGet("health-score/config")]
    public async Task<ActionResult<HealthScoreConfigDto>> GetConfig()
    {
        var boxId = CurrentBoxId();
        var config = await db.HealthScoreConfigurations.Where(c => c.BoxId == boxId && c.IsActive).FirstOrDefaultAsync();
        config ??= new HealthScoreConfiguration { BoxId = boxId }; // defaults, not yet persisted — first GET before any run/save
        return Ok(ToDto(config));
    }

    [HttpPut("health-score/config")]
    public async Task<ActionResult<HealthScoreConfigDto>> UpdateConfig(UpdateHealthScoreConfigRequest request)
    {
        if (!IsManagerOrOwner()) return Forbid();

        var weightSum = request.AttendanceWeight + request.ConsistencyWeight + request.BookingBehaviourWeight + request.ProgressWeight + request.EngagementWeight;
        if (Math.Abs(weightSum - 100) > WeightSumTolerance)
            return BadRequest($"Factor weights must sum to 100 (got {weightSum}).");

        var boxId = CurrentBoxId();
        var current = await db.HealthScoreConfigurations.Where(c => c.BoxId == boxId && c.IsActive).FirstOrDefaultAsync();
        var nextVersion = (current?.Version ?? 0) + 1;

        if (current is not null)
        {
            current.IsActive = false;
            current.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var updated = new HealthScoreConfiguration
        {
            BoxId = boxId,
            Version = nextVersion,
            IsActive = true,
            AttendanceWeight = request.AttendanceWeight,
            ConsistencyWeight = request.ConsistencyWeight,
            BookingBehaviourWeight = request.BookingBehaviourWeight,
            ProgressWeight = request.ProgressWeight,
            EngagementWeight = request.EngagementWeight,
            AttendanceEnabled = request.AttendanceEnabled,
            ConsistencyEnabled = request.ConsistencyEnabled,
            BookingBehaviourEnabled = request.BookingBehaviourEnabled,
            ProgressEnabled = request.ProgressEnabled,
            EngagementEnabled = request.EngagementEnabled,
            AttendanceWindowDays = request.AttendanceWindowDays ?? current?.AttendanceWindowDays ?? 28,
            ConsistencyWindowWeeks = request.ConsistencyWindowWeeks ?? current?.ConsistencyWindowWeeks ?? 8,
            BookingBehaviourWindowDays = request.BookingBehaviourWindowDays ?? current?.BookingBehaviourWindowDays ?? 56,
            ProgressWindowDays = request.ProgressWindowDays ?? current?.ProgressWindowDays ?? 90,
            EngagementWindowDays = request.EngagementWindowDays ?? current?.EngagementWindowDays ?? 180,
            MinTenureDays = request.MinTenureDays ?? current?.MinTenureDays ?? 14,
            MinSessions = request.MinSessions ?? current?.MinSessions ?? 3,
            ActivatedAt = DateTimeOffset.UtcNow,
            CreatedBy = CurrentUserId(),
        };
        db.HealthScoreConfigurations.Add(updated);

        audit.Log(boxId, CurrentUserId(), "health_score_config_updated",
            $"v{current?.Version.ToString() ?? "none"}->v{updated.Version}: " +
            $"weights={updated.AttendanceWeight}/{updated.ConsistencyWeight}/{updated.BookingBehaviourWeight}/{updated.ProgressWeight}/{updated.EngagementWeight}");

        await db.SaveChangesAsync();
        return Ok(ToDto(updated));
    }

    /// <summary>Runs the health score calculation for the caller's box on demand (also runs daily via the
    /// Worker). <paramref name="memberId"/> scopes the run to a single member — the manual "recompute for
    /// this athlete" trigger on their profile.</summary>
    [HttpPost("health-score/run")]
    public async Task<ActionResult<object>> Run([FromQuery] Guid? memberId)
    {
        if (!IsManagerOrOwner()) return Forbid();

        var scored = await job.RunForBoxAsync(CurrentBoxId(), DateOnly.FromDateTime(DateTime.UtcNow), memberId);
        return Ok(new { scored });
    }

    [HttpGet("members/{memberId:guid}/health-score")]
    public async Task<ActionResult<HealthScoreDto?>> ForMember(Guid memberId)
    {
        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var latest = await db.HealthScores.Where(h => h.MemberId == memberId)
            .OrderByDescending(h => h.CalculationDate).FirstOrDefaultAsync();
        return Ok(latest is null ? null : ToDto(latest));
    }

    [HttpGet("members/{memberId:guid}/health-score/history")]
    public async Task<ActionResult<List<HealthScoreDto>>> History(Guid memberId, [FromQuery] int days = 180)
    {
        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days);
        var history = await db.HealthScores
            .Where(h => h.MemberId == memberId && h.CalculationDate >= since)
            .OrderBy(h => h.CalculationDate)
            .ToListAsync();
        return Ok(history.Select(ToDto).ToList());
    }

    private static HealthScoreConfigDto ToDto(HealthScoreConfiguration c) => new(
        c.Version, c.AttendanceWeight, c.ConsistencyWeight, c.BookingBehaviourWeight, c.ProgressWeight, c.EngagementWeight,
        c.AttendanceEnabled, c.ConsistencyEnabled, c.BookingBehaviourEnabled, c.ProgressEnabled, c.EngagementEnabled,
        c.AttendanceWindowDays, c.ConsistencyWindowWeeks, c.BookingBehaviourWindowDays, c.ProgressWindowDays, c.EngagementWindowDays,
        c.MinTenureDays, c.MinSessions, c.ActivatedAt);

    private static HealthScoreDto ToDto(HealthScore h) => new(
        h.MemberId, h.CalculationDate, h.ConfigVersion, h.OverallScore, h.InsufficientData, h.TenureDays, h.SessionCount,
        h.Factors.Select(kv => new HealthScoreFactorDto(kv.Key, kv.Value.Score, kv.Value.Weight, kv.Value.Contribution, kv.Value.Included, kv.Value.Reason)).ToList(),
        h.CalculatedAt);

    private Guid CurrentBoxId() => Guid.Parse(User.FindFirst("box_id")!.Value);

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;

    private bool IsManagerOrOwner()
    {
        var role = User.FindFirst("role")?.Value;
        return role is "Manager" or "Owner";
    }
}
