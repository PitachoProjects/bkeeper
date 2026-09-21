using BKeeper.Application.Dashboards;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Infrastructure.Dashboards;

/// <summary>Moved out of <c>DashboardsController</c> (plan §9) so <c>InsightsController</c>'s AI
/// narrative layer can fetch the exact same validated data instead of duplicating the query.</summary>
public class RetentionOverviewService(BKeeperDbContext db) : IRetentionOverviewService
{
    public async Task<RetentionOverviewDto> BuildAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var members = await db.Members.Select(m => new { m.JoinDate, m.CancelDate, m.Status }).ToListAsync(ct);

        var activeCount = members.Count(m => m.Status == MemberStatus.Active);
        var newThisMonth = members.Count(m => m.JoinDate >= monthStart && m.JoinDate <= today);
        var churnedThisMonth = members.Count(m => m.CancelDate.HasValue && m.CancelDate >= monthStart && m.CancelDate <= today);
        var churnBaseline = members.Count(m => m.JoinDate < monthStart && (m.CancelDate is null || m.CancelDate >= monthStart));
        var monthlyChurnRate = churnBaseline == 0 ? 0 : 100.0 * churnedThisMonth / churnBaseline;

        // D5/§5: "lapsed" is computed, not stored — active status but no visit for >=45 days.
        var lastVisitByMember = await db.Bookings.Where(b => b.Status == BookingStatus.Attended)
            .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { b.MemberId, s.StartsAt })
            .GroupBy(x => x.MemberId)
            .Select(g => new { MemberId = g.Key, LastVisit = g.Max(x => x.StartsAt) })
            .ToListAsync(ct);
        var lastVisitMap = lastVisitByMember.ToDictionary(x => x.MemberId, x => x.LastVisit);
        var activeMemberIds = await db.Members.Where(m => m.Status == MemberStatus.Active).Select(m => m.Id).ToListAsync(ct);
        var lapsedCount = activeMemberIds.Count(id =>
            !lastVisitMap.TryGetValue(id, out var last) || (today.DayNumber - DateOnly.FromDateTime(last.Date).DayNumber) >= 45);

        var cohortInput = members.Select(m => new CohortMember(m.JoinDate, m.CancelDate)).ToList();
        var cohorts = CohortAnalysis.BuildCohorts(cohortInput, today)
            .Select(c => new CohortCurveDto(c.CohortLabel, c.CohortSize, c.RetentionByMonth.ToList())).ToList();

        var churned = members.Where(m => m.CancelDate.HasValue).Select(m => (m.JoinDate, m.CancelDate!.Value)).ToList();
        var histogram = CohortAnalysis.TenureAtChurnHistogram(churned)
            .Select(h => new HistogramBucketDto(h.Label, h.Count)).ToList();

        return new RetentionOverviewDto(activeCount, newThisMonth, churnedThisMonth, newThisMonth - churnedThisMonth,
            Math.Round(monthlyChurnRate, 1), lapsedCount, cohorts, histogram);
    }
}
