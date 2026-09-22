namespace BKeeper.Domain.Entities;

/// <summary>
/// A canonical metric definition: the one formula BKeeper uses everywhere a number appears, so
/// dashboards, alerts and rules never silently compute the same-sounding thing differently as the
/// app grows. Global reference data, not per-box (every box shares the same catalog) — unlike
/// <see cref="EvaluationForm"/> it does not derive from <c>BoxScopedEntity</c> and is not subject to
/// the box query filter. Read-only/seeded for this pass (see docs/DECISIONS.md); no admin CRUD UI yet.
/// </summary>
public class MetricDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable id used by the API and the frontend's "Explain This" affordance, e.g. "lapsed_members".</summary>
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Human-readable formula, e.g. "100 × churned this month ÷ active at month start".</summary>
    public string Formula { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Aggregation { get; set; } = string.Empty;
    public string TimeWindow { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public string WhyItMatters { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
