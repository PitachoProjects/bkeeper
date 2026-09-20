using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record GoalProgressDto(Guid Id, DateOnly Date, double Value, GoalProgressSource Source);
public record GoalDto(Guid Id, GoalCategory Category, string Description, string Metric, double? BaselineValue,
    double? TargetValue, string? Unit, DateOnly? TargetDate, GoalStatus Status, List<GoalProgressDto> Progress);
public record CreateGoalRequest(GoalCategory Category, string Description, string Metric,
    double? BaselineValue, double? TargetValue, string? Unit, DateOnly? TargetDate);
public record AddGoalProgressRequest(DateOnly Date, double Value);
public record UpdateGoalStatusRequest(GoalStatus Status);

[ApiController]
[Authorize]
public class GoalsController(BKeeperDbContext db) : ControllerBase
{
    [HttpGet("members/{memberId:guid}/goals")]
    public async Task<ActionResult<List<GoalDto>>> ListForMember(Guid memberId)
    {
        var goals = await db.Goals.Where(g => g.MemberId == memberId).Include(g => g.Progress).ToListAsync();
        return Ok(goals.Select(ToDto).OrderByDescending(g => g.Status == GoalStatus.Active).ToList());
    }

    [HttpPost("members/{memberId:guid}/goals")]
    public async Task<ActionResult<GoalDto>> Create(Guid memberId, CreateGoalRequest request)
    {
        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var goal = new Goal
        {
            BoxId = member.BoxId,
            MemberId = memberId,
            Category = request.Category,
            Description = request.Description,
            Metric = request.Metric,
            BaselineValue = request.BaselineValue,
            TargetValue = request.TargetValue,
            Unit = request.Unit,
            TargetDate = request.TargetDate,
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return Ok(ToDto(goal));
    }

    [HttpPost("goals/{id:guid}/progress")]
    public async Task<IActionResult> AddProgress(Guid id, AddGoalProgressRequest request)
    {
        var goal = await db.Goals.FindAsync(id);
        if (goal is null) return NotFound();

        db.GoalProgresses.Add(new GoalProgress { BoxId = goal.BoxId, GoalId = id, Date = request.Date, Value = request.Value, Source = GoalProgressSource.CoachEntry });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("goals/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateGoalStatusRequest request)
    {
        var goal = await db.Goals.FindAsync(id);
        if (goal is null) return NotFound();

        goal.Status = request.Status;
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static GoalDto ToDto(Goal g) => new(g.Id, g.Category, g.Description, g.Metric, g.BaselineValue, g.TargetValue,
        g.Unit, g.TargetDate, g.Status, g.Progress.OrderBy(p => p.Date).Select(p => new GoalProgressDto(p.Id, p.Date, p.Value, p.Source)).ToList());
}
