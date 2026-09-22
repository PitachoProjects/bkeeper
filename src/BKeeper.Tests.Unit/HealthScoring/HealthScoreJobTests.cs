using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKeeper.Tests.Unit.HealthScoring;

public class HealthScoreJobTests
{
    private static BKeeperDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<BKeeperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BKeeperDbContext(options, new CurrentBoxAccessor());
    }

    /// <summary>Seeds a member with a steady 3-visits/week attendance history for 37 weeks, ending at asOf,
    /// so Attendance/Consistency/BookingBehaviour all have enough data to score (rather than "no data").</summary>
    private static async Task<(Guid BoxId, Guid MemberId)> SeedMemberWithHistoryAsync(BKeeperDbContext db, DateOnly asOf)
    {
        var box = new Box { Name = "Test Box" };
        var member = new Member { BoxId = box.Id, Name = "Ana Silva", JoinDate = asOf.AddYears(-1), Status = MemberStatus.Active };
        db.Boxes.Add(box);
        db.Members.Add(member);

        for (var week = 37; week >= 1; week--)
        {
            var weekStart = asOf.AddDays(-7 * week);
            foreach (var offset in new[] { 0, 2, 4 }) // Mon/Wed/Fri-style, 3 visits/week
            {
                var day = weekStart.AddDays(offset);
                if (day > asOf) continue;

                var session = new ClassSession { BoxId = box.Id, StartsAt = day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc), ClassType = "WOD", Window = ClassWindow.Morning };
                db.ClassSessions.Add(session);
                db.Bookings.Add(new Booking { BoxId = box.Id, MemberId = member.Id, SessionId = session.Id, Status = BookingStatus.Attended });
            }
        }

        await db.SaveChangesAsync();
        return (box.Id, member.Id);
    }

    [Fact]
    public async Task RunForBoxAsync_SeedsADefaultConfig_WhenNoneExists()
    {
        using var db = NewDb();
        var asOf = new DateOnly(2026, 6, 1);
        var (boxId, _) = await SeedMemberWithHistoryAsync(db, asOf);

        var job = new HealthScoreJob(db, new CurrentBoxAccessor(), NullLogger<HealthScoreJob>.Instance);
        await job.RunForBoxAsync(boxId, asOf);

        var config = await db.HealthScoreConfigurations.SingleAsync(c => c.BoxId == boxId);
        Assert.Equal(1, config.Version);
        Assert.True(config.IsActive);
    }

    [Fact]
    public async Task RunForBoxAsync_ChangingConfigLater_NeverRewritesAPreviouslyCalculatedScore()
    {
        using var db = NewDb();
        var day1 = new DateOnly(2026, 6, 1);
        var day2 = day1.AddDays(1);
        var (boxId, memberId) = await SeedMemberWithHistoryAsync(db, day2);

        var job = new HealthScoreJob(db, new CurrentBoxAccessor(), NullLogger<HealthScoreJob>.Instance);
        await job.RunForBoxAsync(boxId, day1);

        var day1Row = await db.HealthScores.SingleAsync(h => h.MemberId == memberId && h.CalculationDate == day1);
        Assert.Equal(1, day1Row.ConfigVersion);
        Assert.False(day1Row.InsufficientData);
        var scoreAfterFirstRun = day1Row.OverallScore;

        // Owner/Manager changes the weights: deactivate v1, activate a very different v2.
        var v1 = await db.HealthScoreConfigurations.SingleAsync(c => c.BoxId == boxId && c.IsActive);
        v1.IsActive = false;
        db.HealthScoreConfigurations.Add(new HealthScoreConfiguration
        {
            BoxId = boxId, Version = 2, IsActive = true,
            AttendanceWeight = 10, ConsistencyWeight = 10, BookingBehaviourWeight = 10, ProgressWeight = 10, EngagementWeight = 60,
        });
        await db.SaveChangesAsync();

        await job.RunForBoxAsync(boxId, day2);

        var day2Row = await db.HealthScores.SingleAsync(h => h.MemberId == memberId && h.CalculationDate == day2);
        Assert.Equal(2, day2Row.ConfigVersion);

        // The historical row from before the config change must be untouched.
        var day1RowAfter = await db.HealthScores.SingleAsync(h => h.MemberId == memberId && h.CalculationDate == day1);
        Assert.Equal(1, day1RowAfter.ConfigVersion);
        Assert.Equal(scoreAfterFirstRun, day1RowAfter.OverallScore);
    }

    [Fact]
    public async Task RunForBoxAsync_MemberWithNoBookingHistory_IsFlaggedInsufficientData()
    {
        using var db = NewDb();
        var asOf = new DateOnly(2026, 6, 1);
        var box = new Box { Name = "Test Box" };
        var newMember = new Member { BoxId = box.Id, Name = "Brand New", JoinDate = asOf.AddDays(-2), Status = MemberStatus.Active };
        db.Boxes.Add(box);
        db.Members.Add(newMember);
        await db.SaveChangesAsync();

        var job = new HealthScoreJob(db, new CurrentBoxAccessor(), NullLogger<HealthScoreJob>.Instance);
        await job.RunForBoxAsync(box.Id, asOf);

        var row = await db.HealthScores.SingleAsync(h => h.MemberId == newMember.Id);
        Assert.True(row.InsufficientData);
        Assert.Null(row.OverallScore);
        Assert.Equal(2, row.TenureDays);
        Assert.Equal(0, row.SessionCount);
    }
}
