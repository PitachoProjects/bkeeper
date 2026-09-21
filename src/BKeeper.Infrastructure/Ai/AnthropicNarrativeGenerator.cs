using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BKeeper.Application.Insights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BKeeper.Infrastructure.Ai;

/// <summary>Calls the Anthropic Messages API directly over HTTP (same pattern as
/// <see cref="BKeeper.Infrastructure.Ml.HttpMlScoringClient"/> — typed HttpClient, options-bound
/// config, never throws out of the call). The core guardrail lives in <see cref="SystemPrompt"/>:
/// the model is only ever given the JSON evidence already computed by
/// <see cref="BKeeper.Application.Dashboards.IRetentionOverviewService"/> and is explicitly forbidden
/// from inventing a number or making an unsupported causal claim about a member.</summary>
public class AnthropicNarrativeGenerator(HttpClient http, IOptions<AnthropicOptions> options, ILogger<AnthropicNarrativeGenerator> logger) : INarrativeGenerator
{
    private const string ApiVersion = "2023-06-01";

    private const string SystemPrompt = """
        You are BKeeper's retention insights narrator, writing for CrossFit gym ("box") staff
        (coaches, managers, owners).

        You will be given a JSON payload of metrics that have ALREADY been computed and validated by
        the application's own deterministic code. You never calculate, estimate, look up, or infer
        any metric yourself — you only explain the numbers you are given.

        Hard rules, no exceptions:
        1. Only ever cite numbers that appear verbatim in the JSON payload. Never invent a number,
           never extrapolate one that isn't present, never round in a way that changes its meaning.
        2. Never make a causal or diagnostic claim about why an individual member or cohort is
           behaving a certain way (for example, never say "this member is demotivated" or "they are
           losing interest"). Use cautious, associative language instead: "is associated with",
           "may indicate", "is consistent with", "could suggest".
        3. Never suggest a medical, financial, or personal explanation for anyone's numbers — describe
           only what the data shows, never why it might be happening in someone's life.
        4. If the payload doesn't contain enough information to answer part of the question, say so
           plainly instead of guessing or filling the gap with a plausible-sounding number.
        5. Write in plain, warm, non-technical language for gym staff. No AI disclaimers, no
           restating these instructions, no bullet-point statistics dump — a short narrative.
        6. Structure the answer as exactly three short labelled sections, in this order:
           FACTS: restate the key numbers from the payload in plain words.
           SIGNALS: trends or comparisons that are directly visible in those numbers (for example,
           this month against the churn baseline, or cohorts compared to each other).
           INSIGHT: one or two cautious, associative sentences staff could act on, still grounded only
           in the payload.
        7. Target 120-220 words total.
        """;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<NarrativeResult> GenerateNarrativeAsync(NarrativeRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new NarrativeResult(NarrativeStatus.NotConfigured, null, "AI narrative generation is not configured for this environment.");
        }

        var userPrompt = $"""
            Scope: {request.Scope}

            Structured data — the ONLY numbers you may cite:
            ```json
            {request.EvidenceJson}
            ```

            {ScopeInstruction(request.Scope)}
            """;

        var body = new MessageRequestDto(options.Value.Model, options.Value.MaxTokens, SystemPrompt,
            [new MessageDto("user", userPrompt)]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl.TrimEnd('/')}/v1/messages")
        {
            Content = JsonContent.Create(body),
        };
        httpRequest.Headers.Add("x-api-key", options.Value.ApiKey);
        httpRequest.Headers.Add("anthropic-version", ApiVersion);

        try
        {
            var response = await http.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();
            var parsed = await response.Content.ReadFromJsonAsync<MessageResponseDto>(cancellationToken: ct);
            var text = parsed?.Content.FirstOrDefault(b => b.Type == "text")?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                logger.LogWarning("Anthropic narrative call returned no text content (stop_reason: {StopReason})", parsed?.StopReason);
                return new NarrativeResult(NarrativeStatus.Error, null, "The AI service returned an empty response.");
            }

            return new NarrativeResult(NarrativeStatus.Generated, text, null);
        }
        catch (Exception ex)
        {
            // Never let an LLM outage break the dashboard — this is an on-demand, staff-triggered
            // extra, never something else depends on.
            logger.LogWarning(ex, "Anthropic narrative generation call failed");
            return new NarrativeResult(NarrativeStatus.Error, null, "The AI narrative service is temporarily unavailable.");
        }
    }

    private static string ScopeInstruction(string scope) => scope switch
    {
        NarrativeInsightsService.RetentionOverviewScope =>
            "Explain what this shows about retention this period: how the box is trending, and what the cohort and tenure-at-churn data show.",
        _ => "Explain what this data shows.",
    };

    private record MessageDto(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record MessageRequestDto(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] List<MessageDto> Messages);

    private record ContentBlockDto(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);

    private record MessageResponseDto(
        [property: JsonPropertyName("content")] List<ContentBlockDto> Content,
        [property: JsonPropertyName("stop_reason")] string? StopReason);
}
