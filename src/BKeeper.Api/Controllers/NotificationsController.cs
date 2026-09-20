using BKeeper.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BKeeper.Api.Controllers;

[ApiController]
[Route("outreach")]
[Authorize]
public class NotificationsController(OutreachDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sends Queued outreach for the caller's box on demand (also runs every 15 min via the Worker).</summary>
    [HttpPost("dispatch")]
    public async Task<ActionResult<object>> RunDispatch()
    {
        var boxId = Guid.Parse(User.FindFirst("box_id")!.Value);
        var sent = await dispatcher.RunForBoxAsync(boxId);
        return Ok(new { sent });
    }
}
