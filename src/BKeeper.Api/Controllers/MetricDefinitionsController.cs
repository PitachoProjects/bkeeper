using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record MetricDefinitionSummaryDto(string Key, string Name, string Category, string Unit);
public record MetricDefinitionDetailDto(
    string Key, string Name, string Category, string Unit, string Aggregation, int Version,
    string Definition, string HowCalculated, string Period, string WhyItMatters, string Limitations);

/// <summary>
/// Plan-§5-as-a-catalog: the metric definition registry ("Explain This") read API. The catalog itself
/// is global reference data, seeded via migration (docs/DECISIONS.md), not box-scoped — every box sees
/// the same definitions, and there's no write endpoint yet (see the entity's doc comment).
/// </summary>
[ApiController]
[Route("metric-definitions")]
[Authorize]
public class MetricDefinitionsController(BKeeperDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MetricDefinitionSummaryDto>>> List()
    {
        var definitions = await db.MetricDefinitions
            .Where(m => m.IsActive)
            .OrderBy(m => m.Category).ThenBy(m => m.Name)
            .Select(m => new MetricDefinitionSummaryDto(m.Key, m.Name, m.Category, m.Unit))
            .ToListAsync();
        return Ok(definitions);
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<MetricDefinitionDetailDto>> Get(string key)
    {
        var m = await db.MetricDefinitions.FirstOrDefaultAsync(d => d.Key == key && d.IsActive);
        if (m is null) return NotFound();

        return Ok(new MetricDefinitionDetailDto(
            m.Key, m.Name, m.Category, m.Unit, m.Aggregation, m.Version,
            m.Description, m.Formula, m.TimeWindow, m.WhyItMatters, m.Limitations));
    }
}
