using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

/// <summary>
/// A coach as a first-class record, promoted out of the free-text <see cref="ClassSession.CoachName"/>
/// string so coach-level analytics/drill-down (retention by coach, attendance trends) is possible.
/// ApplicationUserId links to a login account for coaches who also sign in (role Coach in
/// <see cref="UserRole"/>) — optional, since not every coach in the data has a system login.
/// No navigation property to ApplicationUser here: it lives in BKeeper.Infrastructure and Domain
/// entities have no dependencies (same reasoning as Member.PrimaryCoachId being a bare Guid?).
/// </summary>
public class Coach : BoxScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public CoachStatus Status { get; set; } = CoachStatus.Active;
    public Guid? ApplicationUserId { get; set; }
}
