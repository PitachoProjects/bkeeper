using BKeeper.Application.Insights;

namespace BKeeper.Infrastructure.Ai;

/// <summary>Always-disabled implementation — used in tests (never makes a network call) and as a
/// safe fallback wherever a real LLM client shouldn't be wired up.</summary>
public class NoOpNarrativeGenerator : INarrativeGenerator
{
    public bool IsConfigured => false;

    public Task<NarrativeResult> GenerateNarrativeAsync(NarrativeRequest request, CancellationToken ct = default) =>
        Task.FromResult(new NarrativeResult(NarrativeStatus.NotConfigured, null, "AI narrative generation is not configured for this environment."));
}
