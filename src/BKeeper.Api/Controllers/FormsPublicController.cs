using BKeeper.Application.Evaluations;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Alerts;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record PublicQuestionDto(string Key, string Label, EvaluationQuestionType Type, string? ShowIfQuestionKey, string? ShowIfEquals);
public record PublicFormDto(string Key, List<PublicQuestionDto> Questions);
public record SubmitFormRequest(Dictionary<string, string> Answers);

/// <summary>
/// Public, unauthenticated form pages (plan Appendix A: GET/POST /f/{token}). No JWT means no box
/// scope is set on the request, so BKeeperDbContext's global filter is a no-op here — safe only
/// because every lookup below goes through an unguessable, single-use token, never a bare id.
/// </summary>
[ApiController]
[Route("f")]
[AllowAnonymous]
public class FormsPublicController(BKeeperDbContext db, SimpleAlertService alertService) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<PublicFormDto>> Get(string token)
    {
        var link = await db.EvaluationFormLinks.FirstOrDefaultAsync(l => l.Token == token);
        if (link is null || link.UsedAt is not null || link.ExpiresAt < DateTimeOffset.UtcNow) return NotFound("This link is invalid, expired, or already used.");

        var form = await db.EvaluationForms.FindAsync(link.FormId);
        if (form is null) return NotFound();

        var questions = form.Schema.Select(q => new PublicQuestionDto(q.Key, q.Label, q.Type, q.ShowIfQuestionKey, q.ShowIfEquals)).ToList();
        return Ok(new PublicFormDto(form.Key, questions));
    }

    [HttpPost("{token}")]
    public async Task<IActionResult> Submit(string token, SubmitFormRequest request)
    {
        var link = await db.EvaluationFormLinks.FirstOrDefaultAsync(l => l.Token == token);
        if (link is null || link.UsedAt is not null || link.ExpiresAt < DateTimeOffset.UtcNow) return BadRequest("This link is invalid, expired, or already used.");

        var form = await db.EvaluationForms.FindAsync(link.FormId);
        if (form is null) return NotFound();

        var flags = EvaluationScorer.ComputeFlags(form.Schema, request.Answers);
        var scores = EvaluationScorer.ComputeScores(form.Schema, request.Answers);

        db.EvaluationResponses.Add(new EvaluationResponse
        {
            BoxId = link.BoxId,
            FormId = form.Id,
            MemberId = link.MemberId,
            AnsweredAt = DateTimeOffset.UtcNow,
            Answers = request.Answers,
            Scores = scores,
        });
        link.UsedAt = DateTimeOffset.UtcNow;

        if (flags.Negative)
            await alertService.CreateOrAppendAsync(link.BoxId, link.MemberId, "EVAL_NEG", AlertFamily.Eval, AlertSeverity.Amber,
                new Dictionary<string, object> { ["form_key"] = form.Key });
        if (flags.Health)
            await alertService.CreateOrAppendAsync(link.BoxId, link.MemberId, "HEALTH", AlertFamily.Eval, AlertSeverity.Amber,
                new Dictionary<string, object> { ["form_key"] = form.Key });

        await db.SaveChangesAsync();
        return Ok(new { message = "Thank you!" });
    }
}
