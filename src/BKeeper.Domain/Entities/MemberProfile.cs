using BKeeper.Domain.Common;

namespace BKeeper.Domain.Entities;

/// <summary>Derived weekly: usual window/days, type mix, persona, baseline. One row per member (latest snapshot).</summary>
public class MemberProfile : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public string? UsualWindow { get; set; }
    public List<string> UsualDays { get; set; } = new();
    public Dictionary<string, double> TypeMix { get; set; } = new();
    public string Persona { get; set; } = "Balanced";
    public double? MedianGapDays { get; set; }
    public double? Baseline26w { get; set; }
}
