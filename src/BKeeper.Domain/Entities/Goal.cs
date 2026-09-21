using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class Goal : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public string? ExternalId { get; set; }
    public GoalCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double? BaselineValue { get; set; }
    public double? TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateOnly? TargetDate { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;

    public Member? Member { get; set; }
    public List<GoalProgress> Progress { get; set; } = new();
}

public class GoalProgress : BoxScopedEntity
{
    public Guid GoalId { get; set; }
    public DateOnly Date { get; set; }
    public double Value { get; set; }
    public GoalProgressSource Source { get; set; }

    public Goal? Goal { get; set; }
}
