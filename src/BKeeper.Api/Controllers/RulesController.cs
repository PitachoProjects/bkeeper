using BKeeper.Domain.Entities;
using BKeeper.Domain.Rules;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record RulesRunResult(int AlertsCreated);
public record RuleCatalogItem(string Code, string Family, string Description, bool Enabled, bool Toggleable, int CooldownDaysAmber, int CooldownDaysRed);
public record SetRuleEnabledRequest(bool Enabled);

[ApiController]
[Route("rules")]
[Authorize]
public class RulesController(DailyRulePipeline pipeline, BKeeperDbContext db, IEnumerable<IRule> rules) : ControllerBase
{
    /// <summary>Rule code -> human description (plan §5's admin view: "check the rules code and description").
    /// No description field is persisted anywhere else, so this is the catalog of record for rule text.</summary>
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        ["R01"] = "Absence gap — flags a member once their gap since last visit passes their own baseline-adjusted threshold.",
        ["R02"] = "Not booking — no upcoming booking and it's been longer than usual since their last one.",
        ["R03"] = "Frequency drop — last two weeks' visit rate has dropped well below their baseline.",
        ["R04"] = "No-show streak — repeated no-shows/late-cancels recently, or a high no-show rate over 8 weeks.",
        ["R08"] = "Onboarding no first visit — joined a week ago and still hasn't shown up.",
        ["R10"] = "Goal at risk — an active goal is due within 6 weeks and off pace, or has had no progress update in 8 weeks.",
        ["R11"] = "Evaluation overdue — an evaluation form link has gone unused for 21+ days after being sent.",
        ["EVAL_NEG"] = "Negative evaluation — a submitted evaluation form flagged a negative response.",
        ["HEALTH"] = "Health flag — a submitted evaluation form flagged a health or injury concern.",
    };

    /// <summary>Codes raised outside the <see cref="IRule"/> pipeline (<see cref="GoalsEvaluationsJob"/>,
    /// <c>FormsPublicController</c>) — real alert-producing rules, but not toggleable here yet since they
    /// aren't gated by <see cref="BKeeper.Domain.Entities.RuleConfig"/>.Enabled the way R01-R08 are. Listed
    /// so Settings' catalog always matches every rule code the Alert Inbox can actually show (was the R10
    /// mismatch bug); the frontend disables the enabled toggle for these.</summary>
    private static readonly (string Code, string Family)[] NonToggleableCodes =
    [
        ("R10", "Goal"),
        ("R11", "Eval"),
        ("EVAL_NEG", "Eval"),
        ("HEALTH", "Eval"),
    ];

    /// <summary>Runs the daily rule pipeline for the caller's box on demand (plan §Week4: "dry-run mode").
    /// <paramref name="memberId"/> scopes the run to a single member (manual "recompute for this athlete"
    /// trigger on their profile) instead of the whole box.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<RulesRunResult>> Run([FromQuery] DateOnly? asOf, [FromQuery] Guid? memberId)
    {
        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        var created = await pipeline.RunForBoxAsync(boxId, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow), memberId);
        return Ok(new RulesRunResult(created));
    }

    [HttpGet]
    public async Task<ActionResult<List<RuleCatalogItem>>> List()
    {
        var configs = await db.RuleConfigs.ToDictionaryAsync(c => c.RuleCode);
        var items = rules.Select(r =>
        {
            configs.TryGetValue(r.Code, out var cfg);
            return new RuleCatalogItem(r.Code, r.Family.ToString(), Descriptions.GetValueOrDefault(r.Code, ""),
                cfg?.Enabled ?? true, true, cfg?.CooldownDaysAmber ?? 14, cfg?.CooldownDaysRed ?? 7);
        }).Concat(NonToggleableCodes.Select(c =>
            new RuleCatalogItem(c.Code, c.Family, Descriptions.GetValueOrDefault(c.Code, ""), true, false, 0, 0)
        )).OrderBy(r => r.Code).ToList();
        return Ok(items);
    }

    [HttpPut("{code}/enabled")]
    public async Task<IActionResult> SetEnabled(string code, SetRuleEnabledRequest request)
    {
        if (rules.All(r => r.Code != code)) return NotFound();

        var config = await db.RuleConfigs.FirstOrDefaultAsync(c => c.RuleCode == code);
        if (config is null)
        {
            var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
            config = new RuleConfig { BoxId = boxId, RuleCode = code };
            db.RuleConfigs.Add(config);
        }
        config.Enabled = request.Enabled;
        config.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
