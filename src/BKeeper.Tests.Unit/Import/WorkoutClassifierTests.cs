using BKeeper.Application.Import;
using BKeeper.Domain.Enums;
using Xunit;

namespace BKeeper.Tests.Unit.Import;

public class WorkoutClassifierTests
{
    [Theory]
    [InlineData("Back squat 5x5", null, WorkoutTagType.Strength)]
    [InlineData("21-15-9 AMRAP", "thrusters and pull-ups", WorkoutTagType.Metcon)]
    [InlineData("Skill work", "handstand walk practice", WorkoutTagType.Gymnastics)]
    [InlineData("5k row for time", null, WorkoutTagType.Endurance)]
    [InlineData("Partner WOD", "hero style", WorkoutTagType.Hybrid)]
    public void Classify_MatchesExpectedTag(string title, string? description, WorkoutTagType expected)
    {
        var result = WorkoutClassifier.Classify(title, description);
        Assert.Contains(expected, result.Keys);
    }

    [Fact]
    public void Classify_UnrecognisedText_ReturnsEmpty()
    {
        var result = WorkoutClassifier.Classify("Open gym", "free choice");
        Assert.Empty(result);
    }
}
