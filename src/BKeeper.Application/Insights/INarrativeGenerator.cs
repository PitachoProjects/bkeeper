namespace BKeeper.Application.Insights;

public enum NarrativeStatus { Generated, NotConfigured, Error }

/// <summary>Everything the model is allowed to see: a scope name and the already-computed,
/// already-validated evidence as JSON. The generator must never be given DB/query access and must
/// never be asked to compute a metric — see the guardrail system prompt in the implementation.</summary>
public record NarrativeRequest(string Scope, string EvidenceJson);

public record NarrativeResult(NarrativeStatus Status, string? Narrative, string? Message);

/// <summary>Turns validated, already-computed analytics into a plain-language narrative. Implementations
/// must never invent a number that isn't in <see cref="NarrativeRequest.EvidenceJson"/> and must fail
/// gracefully (never throw) when the feature isn't configured or the call fails.</summary>
public interface INarrativeGenerator
{
    /// <summary>False when no API key/model is configured — lets callers show a disabled state
    /// without spending a paid API call just to find out.</summary>
    bool IsConfigured { get; }

    Task<NarrativeResult> GenerateNarrativeAsync(NarrativeRequest request, CancellationToken ct = default);
}
