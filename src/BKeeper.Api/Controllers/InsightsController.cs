using BKeeper.Application.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BKeeper.Api.Controllers;

public record NarrativeRequestDto(string Scope);
public record NarrativeResponseDto(string Status, string? Narrative, string? Message, object Evidence);

/// <summary>AI narrative layer (product spec: "explain, never compute"). The LLM is never given DB
/// access and never asked to calculate a metric — it only turns the app's own already-validated DTOs
/// (see <see cref="NarrativeInsightsService"/>) into plain-language text. Manager/Owner only, same
/// gate as <see cref="RiskScoresController"/> — this is an analytics feature that costs money per call.</summary>
[ApiController]
[Route("insights")]
[Authorize]
public class InsightsController(NarrativeInsightsService narrativeInsightsService, INarrativeGenerator narrativeGenerator) : ControllerBase
{
    /// <summary>Lets the frontend disable the "Get AI summary" button when nothing is configured,
    /// without spending a paid API call just to find that out.</summary>
    [HttpGet("status")]
    public ActionResult<object> Status()
    {
        if (!IsManagerOrOwner()) return Forbid();

        return Ok(new { configured = narrativeGenerator.IsConfigured });
    }

    [HttpPost("narrative")]
    public async Task<ActionResult<NarrativeResponseDto>> Narrative([FromBody] NarrativeRequestDto request, CancellationToken ct)
    {
        if (!IsManagerOrOwner()) return Forbid();

        var result = await narrativeInsightsService.GenerateAsync(request.Scope, ct);
        if (result is null) return BadRequest(new { error = $"Unsupported narrative scope '{request.Scope}'." });

        return Ok(new NarrativeResponseDto(result.Status.ToString(), result.Narrative, result.Message, result.Evidence));
    }

    private bool IsManagerOrOwner()
    {
        var role = User.FindFirst("role")?.Value;
        return role is "Manager" or "Owner";
    }
}
