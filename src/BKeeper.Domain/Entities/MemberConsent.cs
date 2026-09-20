using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

/// <summary>
/// One row per (member, channel). ponytail: no row = granted (opt-out model) — simpler than the
/// plan's full granted_at/revoked_at/evidence audit trail, since there's no real consent-capture
/// source (import column or form) yet. Upgrade path: switch the default to opt-in once consent is
/// actually captured somewhere, and keep history instead of overwriting this row.
/// </summary>
public class MemberConsent : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public NotificationChannel Channel { get; set; }
    public bool Granted { get; set; } = true;
}
