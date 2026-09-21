using System.Text.Json;
using BKeeper.Application.Dashboards;

namespace BKeeper.Application.Insights;

public record NarrativeInsightResponse(NarrativeStatus Status, string? Narrative, string? Message, object Evidence);

/// <summary>Assembles the structured evidence for a narrative scope from the app's own validated DTOs
/// and hands it to <see cref="INarrativeGenerator"/> — never computes or reshapes a metric itself, so
/// the LLM sees exactly what a human reading the dashboard would see, nothing more.</summary>
public class NarrativeInsightsService(IRetentionOverviewService retentionOverviewService, INarrativeGenerator narrativeGenerator)
{
    public const string RetentionOverviewScope = "retention-overview";

    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Returns null for an unrecognised scope — the caller turns that into a 400, not a
    /// silent best-effort guess at what was meant.</summary>
    public async Task<NarrativeInsightResponse?> GenerateAsync(string scope, CancellationToken ct = default)
    {
        if (scope != RetentionOverviewScope) return null;

        var evidence = await retentionOverviewService.BuildAsync(ct);
        var evidenceJson = JsonSerializer.Serialize(evidence, EvidenceJsonOptions);

        var result = await narrativeGenerator.GenerateNarrativeAsync(new NarrativeRequest(scope, evidenceJson), ct);

        return new NarrativeInsightResponse(result.Status, result.Narrative, result.Message, evidence);
    }
}
