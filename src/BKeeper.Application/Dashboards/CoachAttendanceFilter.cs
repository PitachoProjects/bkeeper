namespace BKeeper.Application.Dashboards;

public record CoachSessionAttendance(Guid CoachId, Guid MemberId);

/// <summary>
/// Pure support for the coach drill-down on the retention/cohort dashboard (plan D-adjacent: "coach-level
/// attendance changes" and "retention drill-down by coach"): given the attended bookings for sessions
/// that have a linked <see cref="Domain.Entities.Coach"/>, returns the distinct members who attended that
/// coach's sessions. The caller (DashboardsController) then restricts the existing <see cref="CohortAnalysis"/>
/// input to just those members — additive, the coach-less path is untouched.
/// </summary>
public static class CoachAttendanceFilter
{
    public static IReadOnlySet<Guid> MembersForCoach(IReadOnlyList<CoachSessionAttendance> attendances, Guid coachId) =>
        attendances.Where(a => a.CoachId == coachId).Select(a => a.MemberId).ToHashSet();
}
