using System.Globalization;
using System.Security.Claims;
using System.Text;
using BKeeper.Application.Dashboards;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record CohortCurveDto(string CohortLabel, int CohortSize, List<double?> RetentionByMonth);
public record HistogramBucketDto(string Label, int Count);
public record RetentionOverviewDto(
    int ActiveCount, int NewThisMonth, int ChurnedThisMonth, int NetChange, double MonthlyChurnRatePct,
    int LapsedCount, List<CohortCurveDto> Cohorts, List<HistogramBucketDto> TenureAtChurnHistogram);

public record AlertOperationsDto(
    Dictionary<string, int> VolumeBySeverity, Dictionary<string, int> VolumeByFamily,
    double SlaComplianceRatePct, double? AvgTimeToClaimHours, Dictionary<string, int> OutcomesMix,
    double SaveRatePct, double? HoldoutReturnRatePct, double? TreatedReturnRatePct);

public record HeatmapCellDto(string Day, string Window, string Type, int Count);
public record ClassFillDto(string ClassType, string Window, double AvgFillPct);
public record RecentSessionDto(DateTimeOffset Date, string ClassType, string? WorkoutTitle, string? WorkoutDescription, string Tag, int AttendedCount);
public record WorkoutMixDto(List<HeatmapCellDto> WindowTypeHeatmap, List<ClassFillDto> ClassFillBySlot, List<RecentSessionDto> RecentSessions);

public record MyWeekAlertDto(Guid Id, Guid MemberId, string MemberName, string Severity, string Status, DateTimeOffset DueAt);
public record MyWeekDto(List<MyWeekAlertDto> OpenAlerts, int DueThisWeekCount, int ResolvedThisWeekCount, int CelebrationsThisWeekCount);

[ApiController]
[Route("dashboards")]
[Authorize]
public class DashboardsController(BKeeperDbContext db) : ControllerBase
{
    /// <summary>Plan §9: active/new/churned/net/monthly-churn, cohort retention curves, tenure-at-churn histogram.</summary>
    [HttpGet("retention")]
    public async Task<ActionResult<RetentionOverviewDto>> Retention() => Ok(await BuildRetentionOverviewAsync());

    // ponytail-caught bug: `(await Retention()).Value` is always null when Retention() returns via
    // `Ok(x)` — ActionResult<T>.Value only populates through the implicit T->ActionResult<T>
    // conversion, and Ok() returns a plain ActionResult, so the CSV export always 404'd. Split the
    // data-building out so both callers can get the actual DTO.
    private async Task<RetentionOverviewDto> BuildRetentionOverviewAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var members = await db.Members.Select(m => new { m.JoinDate, m.CancelDate, m.Status }).ToListAsync();

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
            .ToListAsync();
        var lastVisitMap = lastVisitByMember.ToDictionary(x => x.MemberId, x => x.LastVisit);
        var activeMemberIds = await db.Members.Where(m => m.Status == MemberStatus.Active).Select(m => m.Id).ToListAsync();
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

    /// <summary>Plan §9: alert volume/SLA/outcomes/save-rate/holdout-vs-treated.</summary>
    [HttpGet("alerts")]
    public async Task<ActionResult<AlertOperationsDto>> AlertOperations()
    {
        var alerts = await db.Alerts
            .Select(a => new { a.Id, a.Severity, a.Family, a.Status, a.CreatedAt, a.DueAt, a.Outcome, a.ClaimedBy })
            .ToListAsync();

        var volumeBySeverity = alerts.GroupBy(a => a.Severity.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var volumeByFamily = alerts.GroupBy(a => a.Family.ToString()).ToDictionary(g => g.Key, g => g.Count());

        var firstClaimByAlert = (await db.AlertEvents.Where(e => e.Type == AlertEventType.Claimed)
                .Select(e => new { e.AlertId, e.CreatedAt }).ToListAsync())
            .GroupBy(e => e.AlertId).ToDictionary(g => g.Key, g => g.Min(e => e.CreatedAt));

        var claimedAlerts = alerts.Where(a => a.ClaimedBy != null || a.Status is AlertStatus.Resolved or AlertStatus.AutoResolved).ToList();
        var claimedWithinSla = claimedAlerts.Count(a => firstClaimByAlert.TryGetValue(a.Id, out var claimedAt) && claimedAt <= a.DueAt);
        var slaComplianceRate = claimedAlerts.Count == 0 ? 0 : 100.0 * claimedWithinSla / claimedAlerts.Count;

        var claimDurations = alerts.Where(a => firstClaimByAlert.ContainsKey(a.Id))
            .Select(a => (firstClaimByAlert[a.Id] - a.CreatedAt).TotalHours).ToList();
        var avgTimeToClaimHours = claimDurations.Count == 0 ? (double?)null : Math.Round(claimDurations.Average(), 1);

        var outcomesMix = alerts.Where(a => a.Outcome != null).GroupBy(a => a.Outcome!).ToDictionary(g => g.Key, g => g.Count());
        var resolvedOrAuto = alerts.Count(a => a.Status is AlertStatus.Resolved or AlertStatus.AutoResolved);
        var saved = alerts.Count(a => a.Status == AlertStatus.AutoResolved || a.Outcome == "returned");
        var saveRate = resolvedOrAuto == 0 ? 0 : 100.0 * saved / resolvedOrAuto;

        // Holdout vs treated: compare "member returned within 14 days" rate for holdout vs non-holdout system outreach.
        var outreach = await db.Outreaches.Where(o => o.SentBy == OutreachSentBy.System)
            .Select(o => new { o.MemberId, o.CreatedAt, o.IsHoldout }).ToListAsync();
        var visits = await db.Bookings.Where(b => b.Status == BookingStatus.Attended)
            .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { b.MemberId, s.StartsAt })
            .ToListAsync();

        double? ReturnRate(bool holdout)
        {
            var group = outreach.Where(o => o.IsHoldout == holdout).ToList();
            if (group.Count == 0) return null;
            var returned = group.Count(o => visits.Any(v => v.MemberId == o.MemberId && v.StartsAt >= o.CreatedAt && v.StartsAt <= o.CreatedAt.AddDays(14)));
            return Math.Round(100.0 * returned / group.Count, 1);
        }

        return Ok(new AlertOperationsDto(volumeBySeverity, volumeByFamily, Math.Round(slaComplianceRate, 1), avgTimeToClaimHours,
            outcomesMix, Math.Round(saveRate, 1), ReturnRate(true), ReturnRate(false)));
    }

    /// <summary>Plan §9: window x type heatmap, class fill by slot.</summary>
    [HttpGet("workouts")]
    public async Task<ActionResult<WorkoutMixDto>> Workouts()
    {
        var since = DateTime.UtcNow.AddDays(-84);

        var visits = await db.Bookings.Where(b => b.Status == BookingStatus.Attended)
            .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { s.Window, s.WorkoutId, s.StartsAt })
            .Where(v => v.StartsAt >= since)
            .ToListAsync();

        var tags = await db.WorkoutTags.ToListAsync();
        var tagsByWorkout = tags.GroupBy(t => t.WorkoutId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Weight).First().Tag);

        var heatmap = visits
            .Select(v => new { v.StartsAt.DayOfWeek, v.Window, Type = v.WorkoutId.HasValue && tagsByWorkout.TryGetValue(v.WorkoutId.Value, out var t) ? t.ToString() : "untagged" })
            .GroupBy(v => (v.DayOfWeek, v.Window.ToString(), v.Type))
            .Select(g => new HeatmapCellDto(g.Key.DayOfWeek.ToString()[..3], g.Key.Item2, g.Key.Item3, g.Count()))
            .OrderByDescending(c => c.Count)
            .ToList();

        var sessions = await db.ClassSessions.Where(s => s.StartsAt >= since && s.Capacity != null && s.Capacity > 0)
            .Select(s => new { s.ClassType, s.Window, s.Capacity, s.Id }).ToListAsync();
        var bookingCounts = await db.Bookings.Where(b => b.Status != BookingStatus.Cancelled)
            .GroupBy(b => b.SessionId).Select(g => new { SessionId = g.Key, Count = g.Count() }).ToListAsync();
        var bookingCountMap = bookingCounts.ToDictionary(x => x.SessionId, x => x.Count);

        var classFill = sessions
            .GroupBy(s => (s.ClassType, s.Window.ToString()))
            .Select(g => new ClassFillDto(g.Key.ClassType, g.Key.Item2,
                Math.Round(100.0 * g.Average(s => (double)bookingCountMap.GetValueOrDefault(s.Id, 0) / s.Capacity!.Value), 1)))
            .OrderByDescending(c => c.AvgFillPct)
            .ToList();

        var workouts = await db.Workouts.ToDictionaryAsync(w => w.Id);
        var attendedCounts = await db.Bookings.Where(b => b.Status == BookingStatus.Attended)
            .GroupBy(b => b.SessionId).Select(g => new { SessionId = g.Key, Count = g.Count() }).ToListAsync();
        var attendedCountMap = attendedCounts.ToDictionary(x => x.SessionId, x => x.Count);

        var recentSessions = await db.ClassSessions
            .Where(s => s.StartsAt >= since && s.WorkoutId != null)
            .OrderByDescending(s => s.StartsAt)
            .Take(20)
            .Select(s => new { s.Id, s.StartsAt, s.ClassType, s.WorkoutId })
            .ToListAsync();
        var recentSessionDtos = recentSessions.Select(s =>
        {
            var workout = s.WorkoutId.HasValue ? workouts.GetValueOrDefault(s.WorkoutId.Value) : null;
            var tag = s.WorkoutId.HasValue && tagsByWorkout.TryGetValue(s.WorkoutId.Value, out var t) ? t.ToString() : "untagged";
            return new RecentSessionDto(s.StartsAt, s.ClassType, workout?.Title, workout?.Description, tag, attendedCountMap.GetValueOrDefault(s.Id, 0));
        }).ToList();

        return Ok(new WorkoutMixDto(heatmap, classFill, recentSessionDtos));
    }

    /// <summary>Plan §9: coach's "my week" — open alerts assigned to Coach or claimed by me, due this week.</summary>
    [HttpGet("my-week")]
    public async Task<ActionResult<MyWeekDto>> MyWeek()
    {
        var userId = CurrentUserId();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow).DayOfWeek == DayOfWeek.Monday
            ? DateTimeOffset.UtcNow.Date
            : DateTimeOffset.UtcNow.Date.AddDays(-(int)DateTimeOffset.UtcNow.DayOfWeek + 1);
        var weekEnd = weekStart.AddDays(7);

        var open = await db.Alerts.Include(a => a.Member)
            .Where(a => a.Status != AlertStatus.Resolved && a.Status != AlertStatus.AutoResolved)
            .Where(a => a.AssignedRole == UserRole.Coach || a.ClaimedBy == userId)
            .OrderBy(a => a.DueAt)
            .Select(a => new MyWeekAlertDto(a.Id, a.MemberId, a.Member!.Name, a.Severity.ToString(), a.Status.ToString(), a.DueAt))
            .ToListAsync();

        var dueThisWeek = open.Count(a => a.DueAt >= weekStart && a.DueAt < weekEnd);
        var resolvedThisWeek = await db.Alerts.CountAsync(a => a.ResolvedAt != null && a.ResolvedAt >= weekStart && a.ResolvedAt < weekEnd);

        return Ok(new MyWeekDto(open, dueThisWeek, resolvedThisWeek, 0));
    }

    [HttpGet("retention/export")]
    public async Task<IActionResult> ExportRetentionCsv()
    {
        var overview = await BuildRetentionOverviewAsync();

        var sb = new StringBuilder();
        sb.AppendLine("cohort,cohort_size," + string.Join(",", Enumerable.Range(0, overview.Cohorts.FirstOrDefault()?.RetentionByMonth.Count ?? 0).Select(i => $"month_{i}")));
        foreach (var c in overview.Cohorts)
        {
            sb.AppendLine($"{c.CohortLabel},{c.CohortSize}," + string.Join(",", c.RetentionByMonth.Select(v => v.HasValue ? v.Value.ToString("F3", CultureInfo.InvariantCulture) : "")));
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "retention_cohorts.csv");
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;
}
