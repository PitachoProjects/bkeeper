using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class ClassSession : BoxScopedEntity
{
    public string? ExternalId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public string ClassType { get; set; } = string.Empty;

    /// <summary>Denormalised display fallback from import (kept for sessions imported before Coach
    /// existed, or whose coach name never got linked). Prefer <see cref="Coach"/>/<see cref="CoachId"/>
    /// when set; fall back to this string otherwise — never dropped, since it's the only record for
    /// historical sessions that predate the Coach entity.</summary>
    public string? CoachName { get; set; }
    public Guid? CoachId { get; set; }
    public int? Capacity { get; set; }
    public Guid? WorkoutId { get; set; }
    public ClassWindow Window { get; set; }

    public Workout? Workout { get; set; }
    public Coach? Coach { get; set; }
    public List<Booking> Bookings { get; set; } = new();

    public static ClassWindow WindowFor(TimeOnly time) => time switch
    {
        { Hour: < 8 } => ClassWindow.Early,
        { Hour: < 11 } => ClassWindow.Morning,
        { Hour: < 14 } => ClassWindow.Lunch,
        { Hour: < 17 } => ClassWindow.Afternoon,
        _ => ClassWindow.Evening,
    };
}
