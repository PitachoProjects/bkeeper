using BKeeper.Application.Metrics;
using BKeeper.Domain.Enums;
using Xunit;

namespace BKeeper.Tests.Unit.Metrics;

public class MetricsBuilderTests
{
    [Fact]
    public void Build_ComputesDaysSinceLastVisitAndUpcomingBooking()
    {
        var asOf = new DateOnly(2026, 6, 1);
        var facts = new List<BookingFact>
        {
            new(asOf.AddDays(-5), BookingStatus.Attended, null),
            new(asOf.AddDays(3), BookingStatus.Booked, null),
        };

        var metrics = MetricsBuilder.Build(Guid.NewGuid(), asOf.AddYears(-1), asOf, facts);

        Assert.Equal(5, metrics.DaysSinceLastVisit);
        Assert.True(metrics.HasUpcomingBooking7d);
        Assert.False(metrics.IsOnboarding);
    }

    [Fact]
    public void Build_FlagsAwayMember()
    {
        var asOf = new DateOnly(2026, 6, 1);
        var metrics = MetricsBuilder.Build(Guid.NewGuid(), asOf.AddYears(-1), asOf, [], awayUntil: asOf.AddDays(10));
        Assert.True(metrics.IsAway);
    }

    [Fact]
    public void Build_NewMember_IsOnboarding()
    {
        var asOf = new DateOnly(2026, 6, 1);
        var metrics = MetricsBuilder.Build(Guid.NewGuid(), asOf.AddDays(-10), asOf, []);
        Assert.True(metrics.IsOnboarding);
        Assert.Equal(1, metrics.TenureWeeks);
    }

    [Fact]
    public void Build_CountsNoShowsInLast14Days()
    {
        var asOf = new DateOnly(2026, 6, 1);
        var facts = new List<BookingFact>
        {
            new(asOf.AddDays(-2), BookingStatus.NoShow, null),
            new(asOf.AddDays(-13), BookingStatus.LateCancel, null),
            new(asOf.AddDays(-20), BookingStatus.NoShow, null), // outside 14d window
        };

        var metrics = MetricsBuilder.Build(Guid.NewGuid(), asOf.AddYears(-1), asOf, facts);
        Assert.Equal(2, metrics.NoShowsLast14d);
    }
}
