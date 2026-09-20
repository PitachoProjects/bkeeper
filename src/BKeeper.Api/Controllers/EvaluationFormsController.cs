using System.Security.Cryptography;
using BKeeper.Application.Notifications;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Notifications;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record FormSummaryDto(Guid Id, string Key, int Version, string Cadence, int QuestionCount);
public record SendFormResponse(string Token, string RelativeLink, DateTimeOffset ExpiresAt);

[ApiController]
[Route("forms")]
[Authorize]
public class EvaluationFormsController(BKeeperDbContext db, OutreachQueueService outreachQueue) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FormSummaryDto>>> List()
    {
        var forms = await db.EvaluationForms.ToListAsync();
        return Ok(forms.Select(f => new FormSummaryDto(f.Id, f.Key, f.Version, f.Cadence, f.Schema.Count)).ToList());
    }

    /// <summary>Sends a signed, single-use, 14-day link via the member's preferred channel (EVAL_REQUEST template).</summary>
    [HttpPost("{key}/send")]
    public async Task<ActionResult<SendFormResponse>> Send(string key, [FromQuery] Guid memberId)
    {
        var form = await db.EvaluationForms.FirstOrDefaultAsync(f => f.Key == key);
        if (form is null) return NotFound("Unknown form key.");
        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound("Unknown member.");
        var box = await db.Boxes.FindAsync(member.BoxId);

        var token = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789", 32);
        var link = new EvaluationFormLink { BoxId = member.BoxId, FormId = form.Id, MemberId = memberId, Token = token, ExpiresAt = DateTimeOffset.UtcNow.AddDays(14) };
        db.EvaluationFormLinks.Add(link);

        var variables = new Dictionary<string, string>
        {
            ["first_name"] = member.Name.Split(' ', 2)[0],
            ["box_name"] = box?.Name ?? "your box",
            ["form_link"] = $"/f/{token}",
        };
        await outreachQueue.QueueAsync(member.BoxId, memberId, null, OutreachSentBy.User, CurrentUserId(),
            AlertSeverity.Info, TemplateCatalog.EvalRequest, member.Language, variables);

        await db.SaveChangesAsync();
        return Ok(new SendFormResponse(token, $"/f/{token}", link.ExpiresAt));
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;
}
