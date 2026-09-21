using BKeeper.Application.Ml;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BKeeper.Tests.Unit.Pipeline;

public class MlScoringJobTests
{
    private static BKeeperDbContext NewDb(CurrentBoxAccessor accessor) =>
        new(new DbContextOptionsBuilder<BKeeperDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor);

    private class FakeMlScoringClient(double primaryP, double logisticP) : IMlScoringClient
    {
        public int PrimaryCalls { get; private set; }
        public int LogisticCalls { get; private set; }

        public Task<IReadOnlyList<MlMemberScoreResult>> ScoreAsync(IReadOnlyList<MlMemberScoreRequest> members, CancellationToken ct = default)
        {
            PrimaryCalls++;
            return Task.FromResult<IReadOnlyList<MlMemberScoreResult>>(members
                .Select(m => new MlMemberScoreResult(m.MemberId, primaryP, "red", ["hasn't attended in 20 days"], "v1", MlModelTypes.LightGbmEnsemble))
                .ToList());
        }

        public Task<IReadOnlyList<MlMemberScoreResult>> ScoreLogisticAsync(IReadOnlyList<MlMemberScoreRequest> members, CancellationToken ct = default)
        {
            LogisticCalls++;
            return Task.FromResult<IReadOnlyList<MlMemberScoreResult>>(members
                .Select(m => new MlMemberScoreResult(m.MemberId, logisticP, "amber", ["days_since_att is the top driver"], "v1", MlModelTypes.LogisticRegression))
                .ToList());
        }
    }

    private static Member EligibleMember(Guid boxId, DateOnly snapshotWeek) => new()
    {
        BoxId = boxId,
        Name = "Ana",
        Status = MemberStatus.Active,
        JoinDate = snapshotWeek.AddDays(-365),
    };

    [Fact]
    public async Task RunForBoxAsync_StoresOneRiskScoreRowPerModelType()
    {
        var accessor = new CurrentBoxAccessor();
        using var db = NewDb(accessor);
        var boxId = Guid.NewGuid();
        var snapshotWeek = DateOnly.FromDateTime(DateTime.UtcNow);

        using (accessor.Use(boxId))
        {
            db.Members.Add(EligibleMember(boxId, snapshotWeek));
            await db.SaveChangesAsync();

            var client = new FakeMlScoringClient(primaryP: 0.10, logisticP: 0.05);
            var job = new MlScoringJob(db, accessor, client, NullLogger<MlScoringJob>.Instance);

            var scored = await job.RunForBoxAsync(boxId, snapshotWeek);

            Assert.Equal(1, scored); // the reported count is the primary model's
            Assert.Equal(1, client.PrimaryCalls);
            Assert.Equal(1, client.LogisticCalls);

            var scores = await db.RiskScores.ToListAsync();
            Assert.Equal(2, scores.Count);

            var primary = Assert.Single(scores, s => s.ModelType == MlModelTypes.LightGbmEnsemble);
            Assert.Equal(0.10, primary.PChurn28d);
            Assert.Equal("red", primary.Band);

            var comparison = Assert.Single(scores, s => s.ModelType == MlModelTypes.LogisticRegression);
            Assert.Equal(0.05, comparison.PChurn28d);
            Assert.Equal("amber", comparison.Band);

            // Neither model's row was overwritten by the other's upsert.
            Assert.NotEqual(primary.Id, comparison.Id);
        }
    }

    [Fact]
    public async Task RunForBoxAsync_RunTwice_UpdatesEachModelTypesRowInPlace_NeverDuplicates()
    {
        var accessor = new CurrentBoxAccessor();
        using var db = NewDb(accessor);
        var boxId = Guid.NewGuid();
        var snapshotWeek = DateOnly.FromDateTime(DateTime.UtcNow);

        using (accessor.Use(boxId))
        {
            db.Members.Add(EligibleMember(boxId, snapshotWeek));
            await db.SaveChangesAsync();

            var firstRunClient = new FakeMlScoringClient(primaryP: 0.10, logisticP: 0.05);
            await new MlScoringJob(db, accessor, firstRunClient, NullLogger<MlScoringJob>.Instance).RunForBoxAsync(boxId, snapshotWeek);

            var secondRunClient = new FakeMlScoringClient(primaryP: 0.20, logisticP: 0.15);
            await new MlScoringJob(db, accessor, secondRunClient, NullLogger<MlScoringJob>.Instance).RunForBoxAsync(boxId, snapshotWeek);

            var scores = await db.RiskScores.ToListAsync();
            Assert.Equal(2, scores.Count); // still one row per model type, not four

            Assert.Equal(0.20, scores.Single(s => s.ModelType == MlModelTypes.LightGbmEnsemble).PChurn28d);
            Assert.Equal(0.15, scores.Single(s => s.ModelType == MlModelTypes.LogisticRegression).PChurn28d);
        }
    }
}
