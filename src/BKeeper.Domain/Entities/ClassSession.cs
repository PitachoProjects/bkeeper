using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class ClassSession : BoxScopedEntity
{
    public string? ExternalId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public string ClassType { get; set; } = string.Empty;
    public string? CoachName { get; set; }
    public int? Capacity { get; set; }
    public Guid? WorkoutId { get; set; }
    public ClassWindow Window { get; set; }

    public Workout? Workout { get; set; }
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
