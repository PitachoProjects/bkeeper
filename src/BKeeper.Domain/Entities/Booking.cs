using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class Booking : BoxScopedEntity
{
    public string? ExternalId { get; set; }
    public Guid MemberId { get; set; }
    public Guid SessionId { get; set; }
    public DateTimeOffset? BookedAt { get; set; }
    public BookingStatus Status { get; set; }

    public Member? Member { get; set; }
    public ClassSession? Session { get; set; }
}
