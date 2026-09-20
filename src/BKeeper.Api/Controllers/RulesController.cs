using BKeeper.Infrastructure.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BKeeper.Api.Controllers;

public record RulesRunResult(int AlertsCreated);

[ApiController]
[Route("rules")]
[Authorize]
public class RulesController(DailyRulePipeline pipeline) : ControllerBase
{
    /// <summary>Runs the daily rule pipeline for the caller's box on demand (plan §Week4: "dry-run mode").</summary>
    [HttpPost("run")]
    public async Task<ActionResult<RulesRunResult>> Run([FromQuery] DateOnly? asOf)
    {
        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        var created = await pipeline.RunForBoxAsync(boxId, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(new RulesRunResult(created));
    }
}
