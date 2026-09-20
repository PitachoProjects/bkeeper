using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class Member : BoxScopedEntity
{
    public string? ExternalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneE164 { get; set; }
    public int? BirthYear { get; set; }
    public DateOnly JoinDate { get; set; }
    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public DateOnly? CancelDate { get; set; }
    public string? CancelReason { get; set; }
    public string Language { get; set; } = "pt-PT";
    public Guid? PrimaryCoachId { get; set; }
    public DateOnly? AwayUntil { get; set; }
    public DateOnly? InjuryFlagUntil { get; set; }

    public List<Membership> Memberships { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
    public List<MemberNote> Notes { get; set; } = new();
}
