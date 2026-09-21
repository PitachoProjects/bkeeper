using BKeeper.Application.Dashboards;
using Xunit;

namespace BKeeper.Tests.Unit.Dashboards;

public class CoachAttendanceFilterTests
{
    private static readonly Guid CoachA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CoachB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Member1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Member2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Member3 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void MembersForCoach_ReturnsOnlyMembersWhoAttendedThatCoach()
    {
        var attendances = new List<CoachSessionAttendance>
        {
            new(CoachA, Member1),
            new(CoachA, Member2),
            new(CoachB, Member3),
        };

        var members = CoachAttendanceFilter.MembersForCoach(attendances, CoachA);

        Assert.Equal(2, members.Count);
        Assert.Contains(Member1, members);
        Assert.Contains(Member2, members);
        Assert.DoesNotContain(Member3, members);
    }

    [Fact]
    public void MembersForCoach_DedupesRepeatedAttendance()
    {
        var attendances = new List<CoachSessionAttendance> { new(CoachA, Member1), new(CoachA, Member1), new(CoachA, Member1) };

        var members = CoachAttendanceFilter.MembersForCoach(attendances, CoachA);

        Assert.Single(members);
    }

    [Fact]
    public void MembersForCoach_UnknownCoachReturnsEmptySet()
    {
        var attendances = new List<CoachSessionAttendance> { new(CoachA, Member1) };

        var members = CoachAttendanceFilter.MembersForCoach(attendances, CoachB);

        Assert.Empty(members);
    }

    [Fact]
    public void MembersForCoach_AMemberSeenByBothCoachesCountsForBoth()
    {
        var attendances = new List<CoachSessionAttendance> { new(CoachA, Member1), new(CoachB, Member1) };

        Assert.Contains(Member1, CoachAttendanceFilter.MembersForCoach(attendances, CoachA));
        Assert.Contains(Member1, CoachAttendanceFilter.MembersForCoach(attendances, CoachB));
    }

    /// <summary>The dashboard's coach drill-down (DashboardsController.Retention) restricts CohortAnalysis's
    /// input to just a coach's members before running the existing (unmodified) cohort math — this
    /// integration between the two pure functions is what the endpoint actually does.</summary>
    [Fact]
    public void FilteredMemberSet_FeedsCohortAnalysisCorrectly()
    {
        var attendances = new List<CoachSessionAttendance> { new(CoachA, Member1), new(CoachA, Member2) };
        var coachMemberIds = CoachAttendanceFilter.MembersForCoach(attendances, CoachA);

        var allMembers = new Dictionary<Guid, CohortMember>
        {
            [Member1] = new(new DateOnly(2026, 1, 5), null),
            [Member2] = new(new DateOnly(2026, 1, 20), null),
            [Member3] = new(new DateOnly(2026, 1, 10), null), // not one of CoachA's members
        };

        var filtered = allMembers.Where(kv => coachMemberIds.Contains(kv.Key)).Select(kv => kv.Value).ToList();
        var cohorts = CohortAnalysis.BuildCohorts(filtered, new DateOnly(2026, 3, 1));

        Assert.Equal(2, cohorts[0].CohortSize); // only Member1 and Member2, Member3 excluded
    }
}
