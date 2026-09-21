using BKeeper.Application.Insights;
using BKeeper.Infrastructure.Ai;
using Xunit;

namespace BKeeper.Tests.Unit.Insights;

public class NoOpNarrativeGeneratorTests
{
    [Fact]
    public async Task AlwaysReportsNotConfigured_AndNeverThrows()
    {
        var generator = new NoOpNarrativeGenerator();

        Assert.False(generator.IsConfigured);

        var result = await generator.GenerateNarrativeAsync(new NarrativeRequest("retention-overview", "{}"));

        Assert.Equal(NarrativeStatus.NotConfigured, result.Status);
        Assert.Null(result.Narrative);
        Assert.NotNull(result.Message);
    }
}
