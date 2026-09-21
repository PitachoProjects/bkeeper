using BKeeper.Domain.Entities;
using BKeeper.Domain.Rules;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record RulesRunResult(int AlertsCreated);
public record RuleCatalogItem(string Code, string Family, string Description, bool Enabled, int CooldownDaysAmber, int CooldownDaysRed);
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
    };

    /// <summary>Runs the daily rule pipeline for the caller's box on demand (plan §Week4: "dry-run mode").</summary>
    [HttpPost("run")]
    public async Task<ActionResult<RulesRunResult>> Run([FromQuery] DateOnly? asOf)
    {
        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        var created = await pipeline.RunForBoxAsync(boxId, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow));
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
                cfg?.Enabled ?? true, cfg?.CooldownDaysAmber ?? 14, cfg?.CooldownDaysRed ?? 7);
        }).OrderBy(r => r.Code).ToList();
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
