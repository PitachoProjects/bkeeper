using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class Membership : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int? PlanFreqPerWeek { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateOnly? FreezeStart { get; set; }
    public DateOnly? FreezeEnd { get; set; }

    public Member? Member { get; set; }
}
