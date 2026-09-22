using BKeeper.Application.HealthScoring;
using BKeeper.Application.Metrics;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Pipeline;

/// <summary>
/// Daily Athlete Health Score calculation — a deterministic, configurable composite score (product
/// spec, not in the original plan's table). Runs after <see cref="DailyRulePipeline"/> and
/// <see cref="GoalsEvaluationsJob"/> so it can reuse the same rule-pipeline day; the plan's own
/// "after MemberWeek is rebuilt" ordering doesn't literally apply here because nothing in this
/// codebase populates MemberWeek yet (see docs/OPEN_QUESTIONS.md) — this job reads booking facts
/// directly, the same way DailyRulePipeline and MlScoringJob already do.
/// </summary>
public class HealthScoreJob(BKeeperDbContext db, CurrentBoxAccessor currentBox, ILogger<HealthScoreJob> logger)
{
    public async Task RunForAllBoxesAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var boxIds = await db.Boxes.Select(b => b.Id).ToListAsync(ct);
        foreach (var boxId in boxIds)
        {
            using (currentBox.Use(boxId))
            {
                await RunForBoxAsync(boxId, asOf, ct: ct);
            }
        }
    }

    /// <summary><paramref name="memberId"/> scopes the run to a single member — the manual "recompute for
    /// this athlete" trigger on their profile — instead of every active member in the box.</summary>
    public async Task<int> RunForBoxAsync(Guid boxId, DateOnly asOf, Guid? memberId = null, CancellationToken ct = default)
    {
        var config = await GetOrCreateActiveConfigAsync(boxId, ct);
        var weights = ToWeights(config);

        var members = await db.Members.Where(m => m.Status == MemberStatus.Active && (memberId == null || m.Id == memberId)).ToListAsync(ct);
        var scored = 0;

        foreach (var member in members)
        {
            var facts = await db.Bookings
                .Where(b => b.MemberId == member.Id)
                .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new BookingFact(DateOnly.FromDateTime(s.StartsAt.Date), b.Status, b.BookedAt))
                .ToListAsync(ct);

            var tenureDays = Math.Max(0, asOf.DayNumber - member.JoinDate.DayNumber);
            var sessionCount = facts.Count(f => f.Status == BookingStatus.Attended && f.SessionDate <= asOf);

            var outcomes = new Dictionary<HealthScoreFactor, FactorOutcome>
            {
                [HealthScoreFactor.Attendance] = AttendanceOutcome(member, facts, asOf, config.AttendanceWindowDays),
                [HealthScoreFactor.Consistency] = ConsistencyOutcome(facts, asOf, config.ConsistencyWindowWeeks),
                [HealthScoreFactor.BookingBehaviour] = BookingBehaviourOutcome(facts, asOf, config.BookingBehaviourWindowDays),
                [HealthScoreFactor.Progress] = await ProgressOutcomeAsync(member.Id, asOf, config.ProgressWindowDays, ct),
                [HealthScoreFactor.Engagement] = await EngagementOutcomeAsync(member.Id, asOf, config.EngagementWindowDays, ct),
            };

            var result = HealthScoreComposer.Compose(weights, outcomes, tenureDays, sessionCount);
            await UpsertHealthScoreAsync(boxId, member.Id, asOf, config.Version, tenureDays, sessionCount, result, ct);
            scored++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Health score job for box {BoxId}: {Scored} members scored (config v{Version})", boxId, scored, config.Version);
        return scored;
    }

    private static FactorOutcome AttendanceOutcome(Member member, List<BookingFact> facts, DateOnly asOf, int windowDays)
    {
        var visitsInWindow = facts.Count(f => f.Status == BookingStatus.Attended && f.SessionDate > asOf.AddDays(-windowDays) && f.SessionDate <= asOf);
        var metrics = MetricsBuilder.Build(member.Id, member.JoinDate, asOf, facts, member.AwayUntil);
        return HealthScoreFactors.AttendanceScore(visitsInWindow, windowDays, metrics.BaselinePerWeek);
    }

    private static FactorOutcome ConsistencyOutcome(List<BookingFact> facts, DateOnly asOf, int windowWeeks)
    {
        var visitDates = facts.Where(f => f.Status == BookingStatus.Attended).Select(f => f.SessionDate).ToHashSet();
        var currentWeekStart = AttendanceMetrics.IsoWeekStart(asOf);

        var weeklyCounts = new List<int>();
        for (var i = windowWeeks - 1; i >= 0; i--)
        {
            var weekStart = currentWeekStart.AddDays(-7 * i);
            var count = Enumerable.Range(0, 7).Count(d => visitDates.Contains(weekStart.AddDays(d)) && weekStart.AddDays(d) <= asOf);
            weeklyCounts.Add(count);
        }

        return HealthScoreFactors.ConsistencyScore(weeklyCounts, minWeeksOfHistory: Math.Min(4, windowWeeks));
    }

    /// <summary>Same no-show + late-cancel composite rate R04 (no-show streak) uses, over the configured window.</summary>
    private static FactorOutcome BookingBehaviourOutcome(List<BookingFact> facts, DateOnly asOf, int windowDays)
    {
        var inWindow = facts.Where(f => f.SessionDate > asOf.AddDays(-windowDays) && f.SessionDate <= asOf).ToList();
        var bookings = inWindow.Count;
        var noShowsAndLateCancels = inWindow.Count(f => f.Status is BookingStatus.NoShow or BookingStatus.LateCancel);
        return HealthScoreFactors.BookingBehaviourScore(noShowsAndLateCancels, bookings, minBookings: 6);
    }

    private async Task<FactorOutcome> ProgressOutcomeAsync(Guid memberId, DateOnly asOf, int windowDays, CancellationToken ct)
    {
        var goals = await db.Goals.Where(g => g.MemberId == memberId && g.Status == GoalStatus.Active)
            .Include(g => g.Progress).ToListAsync(ct);

        var signals = goals.Select(g =>
        {
            var latest = g.Progress.OrderByDescending(p => p.Date).FirstOrDefault();
            return new GoalProgressSignal(DateOnly.FromDateTime(g.CreatedAt.Date), g.TargetDate, g.BaselineValue, g.TargetValue, latest?.Date, latest?.Value);
        }).ToList();

        return HealthScoreFactors.ProgressScore(signals, asOf, windowDays);
    }

    private async Task<FactorOutcome> EngagementOutcomeAsync(Guid memberId, DateOnly asOf, int windowDays, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-windowDays);
        var responses = await db.EvaluationResponses
            .Where(r => r.MemberId == memberId && r.AnsweredAt >= since)
            .Include(r => r.Form)
            .ToListAsync(ct);

        var indices = responses
            .Where(r => r.Form is not null)
            .Select(r => BKeeper.Application.Evaluations.EvaluationScorer.EngagementIndex(r.Form!.Schema, r.Answers))
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();

        return HealthScoreFactors.EngagementScore(indices);
    }

    private async Task UpsertHealthScoreAsync(Guid boxId, Guid memberId, DateOnly asOf, int configVersion,
        int tenureDays, int sessionCount, HealthScoreResult result, CancellationToken ct)
    {
        var factors = result.Factors.ToDictionary(
            kv => kv.Key.ToString(),
            kv => new HealthScoreFactorBreakdown { Score = kv.Value.Score, Weight = kv.Value.Weight, Contribution = kv.Value.Contribution, Included = kv.Value.Included, Reason = kv.Value.Reason });

        var existing = await db.HealthScores.FirstOrDefaultAsync(h => h.MemberId == memberId && h.CalculationDate == asOf, ct);
        if (existing is null)
        {
            db.HealthScores.Add(new HealthScore
            {
                BoxId = boxId,
                MemberId = memberId,
                CalculationDate = asOf,
                ConfigVersion = configVersion,
                OverallScore = result.OverallScore,
                InsufficientData = result.InsufficientData,
                TenureDays = tenureDays,
                SessionCount = sessionCount,
                Factors = factors,
                CalculatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.ConfigVersion = configVersion;
            existing.OverallScore = result.OverallScore;
            existing.InsufficientData = result.InsufficientData;
            existing.TenureDays = tenureDays;
            existing.SessionCount = sessionCount;
            existing.Factors = factors;
            existing.CalculatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>No config yet for this box -> seed the default-weights version 1, active immediately
    /// (mirrors RuleConfig's "absent row = compiled defaults", made explicit here since the whole point
    /// of this table is a visible, versioned config rather than an implicit fallback).</summary>
    private async Task<HealthScoreConfiguration> GetOrCreateActiveConfigAsync(Guid boxId, CancellationToken ct)
    {
        var active = await db.HealthScoreConfigurations.Where(c => c.BoxId == boxId && c.IsActive).FirstOrDefaultAsync(ct);
        if (active is not null) return active;

        var seeded = new HealthScoreConfiguration { BoxId = boxId, Version = 1, IsActive = true };
        db.HealthScoreConfigurations.Add(seeded);
        await db.SaveChangesAsync(ct);
        return seeded;
    }

    public static HealthScoreWeights ToWeights(HealthScoreConfiguration config) => new()
    {
        AttendanceWeight = config.AttendanceWeight,
        ConsistencyWeight = config.ConsistencyWeight,
        BookingBehaviourWeight = config.BookingBehaviourWeight,
        ProgressWeight = config.ProgressWeight,
        EngagementWeight = config.EngagementWeight,
        AttendanceEnabled = config.AttendanceEnabled,
        ConsistencyEnabled = config.ConsistencyEnabled,
        BookingBehaviourEnabled = config.BookingBehaviourEnabled,
        ProgressEnabled = config.ProgressEnabled,
        EngagementEnabled = config.EngagementEnabled,
        MinTenureDays = config.MinTenureDays,
        MinSessions = config.MinSessions,
    };
}
