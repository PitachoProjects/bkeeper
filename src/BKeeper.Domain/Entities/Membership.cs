using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class Membership : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int? PlanFreqPerWeek { get; set; }
    public decimal? MonthlyPriceEur { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateOnly? FreezeStart { get; set; }
    public DateOnly? FreezeEnd { get; set; }

    public Member? Member { get; set; }
    public List<MembershipFreeze> Freezes { get; set; } = new();
}

/// <summary>One pause interval within a membership (a member can freeze more than once over time);
/// the single FreezeStart/FreezeEnd pair on Membership only tracks the current/most-recent one.</summary>
public class MembershipFreeze : BoxScopedEntity
{
    public Guid MembershipId { get; set; }
    public string? ExternalId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }

    public Membership? Membership { get; set; }
}
