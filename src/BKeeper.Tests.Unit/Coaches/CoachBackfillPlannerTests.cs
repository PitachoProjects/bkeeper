using BKeeper.Application.Coaches;
using Xunit;

namespace BKeeper.Tests.Unit.Coaches;

public class CoachBackfillPlannerTests
{
    private static readonly Guid BoxA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BoxB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Plan_OneCoachPerDistinctNamePerBox()
    {
        var sessions = new List<ClassSessionCoachName>
        {
            new(BoxA, "Ana Silva"),
            new(BoxA, "Ana Silva"),
            new(BoxA, "Bruno Costa"),
        };

        var plan = CoachBackfillPlanner.Plan(sessions, existingCoaches: []);

        Assert.Equal(2, plan.Count);
        Assert.Contains(plan, c => c.BoxId == BoxA && c.Name == "Ana Silva");
        Assert.Contains(plan, c => c.BoxId == BoxA && c.Name == "Bruno Costa");
    }

    [Fact]
    public void Plan_TrimsWhitespaceBeforeDeduping()
    {
        var sessions = new List<ClassSessionCoachName>
        {
            new(BoxA, "Ana Silva"),
            new(BoxA, "  Ana Silva  "),
            new(BoxA, "Ana Silva\t"),
        };

        var plan = CoachBackfillPlanner.Plan(sessions, existingCoaches: []);

        Assert.Single(plan);
        Assert.Equal("Ana Silva", plan[0].Name);
    }

    [Fact]
    public void Plan_SkipsNullAndBlankCoachNames()
    {
        var sessions = new List<ClassSessionCoachName>
        {
            new(BoxA, null),
            new(BoxA, ""),
            new(BoxA, "   "),
            new(BoxA, "Ana Silva"),
        };

        var plan = CoachBackfillPlanner.Plan(sessions, existingCoaches: []);

        Assert.Single(plan);
        Assert.Equal("Ana Silva", plan[0].Name);
    }

    [Fact]
    public void Plan_SkipsNamesThatAlreadyHaveACoach()
    {
        var sessions = new List<ClassSessionCoachName> { new(BoxA, "Ana Silva"), new(BoxA, "Bruno Costa") };
        var existing = new List<ExistingCoach> { new(BoxA, "Ana Silva") };

        var plan = CoachBackfillPlanner.Plan(sessions, existing);

        Assert.Single(plan);
        Assert.Equal("Bruno Costa", plan[0].Name);
    }

    [Fact]
    public void Plan_IsIdempotent_SecondRunProducesNothing()
    {
        var sessions = new List<ClassSessionCoachName> { new(BoxA, "Ana Silva") };
        var firstPlan = CoachBackfillPlanner.Plan(sessions, existingCoaches: []);
        var alreadyCreated = firstPlan.Select(c => new ExistingCoach(c.BoxId, c.Name)).ToList();

        var secondPlan = CoachBackfillPlanner.Plan(sessions, alreadyCreated);

        Assert.Empty(secondPlan);
    }

    [Fact]
    public void Plan_SameNameInDifferentBoxesProducesTwoCoaches()
    {
        var sessions = new List<ClassSessionCoachName> { new(BoxA, "Ana Silva"), new(BoxB, "Ana Silva") };

        var plan = CoachBackfillPlanner.Plan(sessions, existingCoaches: []);

        Assert.Equal(2, plan.Count);
        Assert.Contains(plan, c => c.BoxId == BoxA);
        Assert.Contains(plan, c => c.BoxId == BoxB);
    }

    [Fact]
    public void Plan_ExistingCoachNameIsMatchedTrimmed()
    {
        var sessions = new List<ClassSessionCoachName> { new(BoxA, "Ana Silva") };
        var existing = new List<ExistingCoach> { new(BoxA, "  Ana Silva  ") };

        var plan = CoachBackfillPlanner.Plan(sessions, existing);

        Assert.Empty(plan);
    }
}
