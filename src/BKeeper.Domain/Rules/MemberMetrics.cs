namespace BKeeper.Domain.Rules;

/// <summary>
/// Pure input for rule evaluation (§6.3 of the plan): everything a rule needs about one member,
/// computed once per run by the feature builder. Rules never touch the database directly.
/// </summary>
public record MemberMetrics
{
    public required Guid MemberId { get; init; }
    public required DateOnly AsOf { get; init; }
    public required int TenureWeeks { get; init; }
    public required bool IsOnboarding { get; init; }
    public required bool IsAway { get; init; }
    public int DaysSinceJoin { get; init; }
    public int VisitsSinceJoin { get; init; }

    public int? DaysSinceLastVisit { get; init; }
    public int? DaysSinceLastBooking { get; init; }
    public double? MedianGapDays { get; init; }
    public double? MedianBookingGapDays { get; init; }
    public double BaselinePerWeek { get; init; }
    public double Last2WeekRate { get; init; }
    public bool HasUpcomingBooking7d { get; init; }

    public int NoShowsLast14d { get; init; }
    public int NoShowsLast8w { get; init; }
    public int BookingsLast8w { get; init; }

    public double WindowShareDropPp { get; init; }
    public double TypeMixJsDivergence { get; init; }
    public double AbandonedTypeSharePrior12w { get; init; }

    public bool HasHumanContactLast7d { get; init; }
    public double? PChurn28d { get; init; }
}
