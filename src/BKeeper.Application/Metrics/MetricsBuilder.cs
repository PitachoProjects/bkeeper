using BKeeper.Domain.Enums;
using BKeeper.Domain.Rules;

namespace BKeeper.Application.Metrics;

/// <summary>
/// Builds a member's <see cref="MemberMetrics"/> snapshot from raw booking facts (§5, §Week3).
/// Pure function of its inputs so it is trivially unit-testable against hand-computed fixtures.
/// </summary>
public static class MetricsBuilder
{
    public static MemberMetrics Build(
        Guid memberId,
        DateOnly joinDate,
        DateOnly asOf,
        IReadOnlyList<BookingFact> facts,
        DateOnly? awayUntil = null,
        bool hasHumanContactLast7d = false,
        double? pChurn28d = null)
    {
        var tenureWeeks = (asOf.DayNumber - joinDate.DayNumber) / 7;
        var isOnboarding = tenureWeeks < 4;
        var isAway = awayUntil.HasValue && awayUntil.Value >= asOf;

        var visits = facts.Where(f => f.Status == BookingStatus.Attended).Select(f => f.SessionDate).ToList();
        var lastVisit = visits.Where(d => d <= asOf).OrderByDescending(d => d).FirstOrDefault();
        int? daysSinceLastVisit = visits.Count == 0 ? null : (asOf.DayNumber - lastVisit.DayNumber);

        var bookedDates = facts.Where(f => f.BookedAt.HasValue).Select(f => DateOnly.FromDateTime(f.BookedAt!.Value.Date)).ToList();
        var lastBooked = bookedDates.Where(d => d <= asOf).OrderByDescending(d => d).FirstOrDefault();
        int? daysSinceLastBooking = bookedDates.Count == 0 ? null : (asOf.DayNumber - lastBooked.DayNumber);

        var hasUpcoming = facts.Any(f => f.Status == BookingStatus.Booked && f.SessionDate > asOf && f.SessionDate <= asOf.AddDays(7));

        var last26w = asOf.AddDays(-26 * 7);
        var weeklyCounts = visits.Where(d => d >= last26w && d < asOf.AddDays(-28))
            .GroupBy(d => AttendanceMetrics.IsoWeekStart(d))
            .Select(g => g.Count());
        var baselinePerWeek = AttendanceMetrics.BaselinePerWeek(weeklyCounts);

        var last2w = visits.Count(d => d > asOf.AddDays(-14) && d <= asOf);
        var last2WeekRate = last2w / 2.0;

        var noShows14d = facts.Count(f => f.SessionDate > asOf.AddDays(-14) && f.SessionDate <= asOf &&
            (f.Status == BookingStatus.NoShow || f.Status == BookingStatus.LateCancel));
        var last8wFacts = facts.Where(f => f.SessionDate > asOf.AddDays(-56) && f.SessionDate <= asOf).ToList();
        var noShows8w = last8wFacts.Count(f => f.Status is BookingStatus.NoShow or BookingStatus.LateCancel);
        var bookings8w = last8wFacts.Count;

        var visitsSinceJoin = visits.Count(d => d >= joinDate && d <= asOf);
        var daysSinceJoin = asOf.DayNumber - joinDate.DayNumber;

        return new MemberMetrics
        {
            MemberId = memberId,
            AsOf = asOf,
            TenureWeeks = tenureWeeks,
            IsOnboarding = isOnboarding,
            IsAway = isAway,
            DaysSinceJoin = daysSinceJoin,
            VisitsSinceJoin = visitsSinceJoin,
            DaysSinceLastVisit = daysSinceLastVisit,
            DaysSinceLastBooking = daysSinceLastBooking,
            MedianGapDays = AttendanceMetrics.MedianGapDays(visits.Where(d => d > asOf.AddDays(-84))),
            MedianBookingGapDays = AttendanceMetrics.MedianGapDays(bookedDates.Where(d => d > asOf.AddDays(-84))),
            BaselinePerWeek = baselinePerWeek,
            Last2WeekRate = last2WeekRate,
            HasUpcomingBooking7d = hasUpcoming,
            NoShowsLast14d = noShows14d,
            NoShowsLast8w = noShows8w,
            BookingsLast8w = bookings8w,
            HasHumanContactLast7d = hasHumanContactLast7d,
            PChurn28d = pChurn28d,
        };
    }
}
