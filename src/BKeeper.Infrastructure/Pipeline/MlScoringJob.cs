using BKeeper.Application.Ml;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Pipeline;

/// <summary>
/// Weekly (Sunday) churn-risk scoring — plan §8/Week8, R13. Shadow mode only: scores are stored in
/// <see cref="RiskScore"/> for Manager/Owner to see, but never create alerts. Going live requires
/// the real-data backtest gate in plan §8 ("AUC >= 0.75, PR-AUC >= 5x base rate, top-10/week
/// precision >= 2x rules-v1") — not evaluated here since there's no real data yet.
/// </summary>
public class MlScoringJob(BKeeperDbContext db, CurrentBoxAccessor currentBox, IMlScoringClient mlClient, ILogger<MlScoringJob> logger)
{
    private const int MinTenureWeeks = 12;

    public async Task RunForAllBoxesAsync(DateOnly snapshotWeek, CancellationToken ct = default)
    {
        var boxIds = await db.Boxes.Select(b => b.Id).ToListAsync(ct);
        foreach (var boxId in boxIds)
        {
            using (currentBox.Use(boxId))
            {
                await RunForBoxAsync(boxId, snapshotWeek, ct);
            }
        }
    }

    public async Task<int> RunForBoxAsync(Guid boxId, DateOnly snapshotWeek, CancellationToken ct = default)
    {
        var members = await db.Members
            .Where(m => m.Status == MemberStatus.Active)
            .Where(m => (m.AwayUntil == null || m.AwayUntil < snapshotWeek))
            .ToListAsync(ct);

        var eligible = members.Where(m => (snapshotWeek.DayNumber - m.JoinDate.DayNumber) / 7 >= MinTenureWeeks).ToList();
        if (eligible.Count == 0) return 0;

        var requests = new List<MlMemberScoreRequest>();
        foreach (var member in eligible)
        {
            var facts = await db.Bookings
                .Where(b => b.MemberId == member.Id)
                .Join(db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { b.Status, b.BookedAt, SessionDate = DateOnly.FromDateTime(s.StartsAt.Date), s.Window })
                .Where(f => f.SessionDate <= snapshotWeek || f.Status == BookingStatus.Booked)
                .ToListAsync(ct);

            var mlFacts = facts.Select(f => new MlBookingFact(
                f.SessionDate, ToMlStatus(f.Status),
                f.BookedAt.HasValue ? DateOnly.FromDateTime(f.BookedAt.Value.Date) : null,
                f.Window.ToString().ToLowerInvariant(), new Dictionary<string, double>())).ToList();

            requests.Add(new MlMemberScoreRequest(member.Id, member.JoinDate, null, snapshotWeek, mlFacts));
        }

        // Stage C (LightGBM ensemble) stays the primary/default view R13 would use if it ever goes
        // live; Stage B (logistic regression) is scored the same shadow-mode way, purely for the
        // Manager/Owner comparison view (RiskScoresController) — see docs/DECISIONS.md.
        var primaryResults = await mlClient.ScoreAsync(requests, ct);
        var comparisonResults = await mlClient.ScoreLogisticAsync(requests, ct);

        var scored = await UpsertRiskScoresAsync(boxId, snapshotWeek, primaryResults, ct);
        await UpsertRiskScoresAsync(boxId, snapshotWeek, comparisonResults, ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "ML scoring (shadow) for box {BoxId}: {Primary} scored by {PrimaryModel}, {Comparison} scored by {ComparisonModel}",
            boxId, primaryResults.Count, MlModelTypes.LightGbmEnsemble, comparisonResults.Count, MlModelTypes.LogisticRegression);
        return scored;
    }

    private async Task<int> UpsertRiskScoresAsync(Guid boxId, DateOnly snapshotWeek, IReadOnlyList<MlMemberScoreResult> results, CancellationToken ct)
    {
        var scored = 0;
        foreach (var result in results)
        {
            var existing = await db.RiskScores.FirstOrDefaultAsync(
                r => r.MemberId == result.MemberId && r.SnapshotWeek == snapshotWeek && r.ModelType == result.ModelType, ct);
            if (existing is null)
            {
                db.RiskScores.Add(new RiskScore
                {
                    BoxId = boxId,
                    MemberId = result.MemberId,
                    SnapshotWeek = snapshotWeek,
                    ModelVersion = result.ModelVersion,
                    ModelType = result.ModelType,
                    PChurn28d = result.PChurn28d,
                    Band = result.Band,
                    TopReasons = result.TopReasons.ToList(),
                });
            }
            else
            {
                existing.ModelVersion = result.ModelVersion;
                existing.PChurn28d = result.PChurn28d;
                existing.Band = result.Band;
                existing.TopReasons = result.TopReasons.ToList();
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            scored++;
        }
        return scored;
    }

    private static string ToMlStatus(BookingStatus status) => status switch
    {
        BookingStatus.NoShow => "no_show",
        BookingStatus.LateCancel => "late_cancel",
        _ => status.ToString().ToLowerInvariant(),
    };
}
