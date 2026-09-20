using BKeeper.Application.Import;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

[ApiController]
[Route("imports")]
[Authorize]
public class ImportsController(IExcelImportService importService, BKeeperDbContext db) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<ImportResult>> Upload(IFormFile file)
    {
        if (file.Length == 0) return BadRequest("Empty file.");

        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        await using var stream = file.OpenReadStream();
        var result = await importService.ImportAsync(boxId, stream, file.FileName);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var run = await db.ImportRuns.Include(r => r.RowErrors).FirstOrDefaultAsync(r => r.Id == id);
        return run is null ? NotFound() : Ok(run);
    }
}
