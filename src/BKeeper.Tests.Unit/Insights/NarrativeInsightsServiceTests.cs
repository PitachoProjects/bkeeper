using System.Text.Json;
using BKeeper.Application.Dashboards;
using BKeeper.Application.Insights;
using Xunit;

namespace BKeeper.Tests.Unit.Insights;

/// <summary>Guardrail tests for the "explain, never compute" narrative layer: the evidence handed to
/// the LLM must be exactly the app's own validated DTO (nothing invented, nothing extra), the request
/// must always go through <see cref="INarrativeGenerator"/> — never a direct API/DB call — and an
/// unrecognised scope must be rejected rather than guessed at.</summary>
public class NarrativeInsightsServiceTests
{
    private static RetentionOverviewDto SampleOverview() => new(
        ActiveCount: 480, NewThisMonth: 12, ChurnedThisMonth: 7, NetChange: 5, MonthlyChurnRatePct: 1.5,
        LapsedCount: 23,
        Cohorts: [new CohortCurveDto("2026-01", 40, [1.0, 0.95, null])],
        TenureAtChurnHistogram: [new HistogramBucketDto("0-1mo", 2), new HistogramBucketDto("1-2mo", 3)]);

    [Fact]
    public async Task GenerateAsync_UnknownScope_ReturnsNull_NeverCallsGenerator()
    {
        var generator = new RecordingNarrativeGenerator();
        var service = new NarrativeInsightsService(new FakeRetentionOverviewService(SampleOverview()), generator);

        var result = await service.GenerateAsync("member-timeline");

        Assert.Null(result);
        Assert.Null(generator.LastRequest); // guardrail: no request ever built for a scope we don't support
    }

    [Fact]
    public async Task GenerateAsync_RetentionOverview_SendsOnlyTheValidatedDtoAsEvidence()
    {
        var overview = SampleOverview();
        var generator = new RecordingNarrativeGenerator();
        var service = new NarrativeInsightsService(new FakeRetentionOverviewService(overview), generator);

        await service.GenerateAsync(NarrativeInsightsService.RetentionOverviewScope);

        Assert.NotNull(generator.LastRequest);
        Assert.Equal(NarrativeInsightsService.RetentionOverviewScope, generator.LastRequest!.Scope);

        // Round-trip the evidence JSON and assert every field is exactly what BuildAsync computed —
        // nothing added, nothing dropped, nothing recomputed by the narrative layer itself.
        var evidence = JsonSerializer.Deserialize<JsonElement>(generator.LastRequest.EvidenceJson);
        Assert.Equal(overview.ActiveCount, evidence.GetProperty("activeCount").GetInt32());
        Assert.Equal(overview.NewThisMonth, evidence.GetProperty("newThisMonth").GetInt32());
        Assert.Equal(overview.ChurnedThisMonth, evidence.GetProperty("churnedThisMonth").GetInt32());
        Assert.Equal(overview.NetChange, evidence.GetProperty("netChange").GetInt32());
        Assert.Equal(overview.MonthlyChurnRatePct, evidence.GetProperty("monthlyChurnRatePct").GetDouble());
        Assert.Equal(overview.LapsedCount, evidence.GetProperty("lapsedCount").GetInt32());
        Assert.Equal(overview.Cohorts.Count, evidence.GetProperty("cohorts").GetArrayLength());
        Assert.Equal(overview.TenureAtChurnHistogram.Count, evidence.GetProperty("tenureAtChurnHistogram").GetArrayLength());

        // Exactly six top-level fields — nothing invented, nothing silently added.
        var propertyNames = evidence.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "activeCount", "churnedThisMonth", "cohorts", "lapsedCount", "monthlyChurnRatePct", "netChange", "newThisMonth", "tenureAtChurnHistogram" }.OrderBy(n => n), propertyNames);
    }

    [Fact]
    public async Task GenerateAsync_PropagatesGeneratorResult_UnchangedAndAlongsideTheEvidence()
    {
        var overview = SampleOverview();
        var generator = new RecordingNarrativeGenerator { Result = new NarrativeResult(NarrativeStatus.Generated, "FACTS: ...", null) };
        var service = new NarrativeInsightsService(new FakeRetentionOverviewService(overview), generator);

        var result = await service.GenerateAsync(NarrativeInsightsService.RetentionOverviewScope);

        Assert.NotNull(result);
        Assert.Equal(NarrativeStatus.Generated, result!.Status);
        Assert.Equal("FACTS: ...", result.Narrative);
        Assert.Same(overview, result.Evidence);
    }

    [Fact]
    public async Task GenerateAsync_WhenGeneratorIsNotConfigured_ReturnsThatStatusInsteadOfThrowing()
    {
        var generator = new RecordingNarrativeGenerator { Result = new NarrativeResult(NarrativeStatus.NotConfigured, null, "AI narrative generation is not configured for this environment.") };
        var service = new NarrativeInsightsService(new FakeRetentionOverviewService(SampleOverview()), generator);

        var result = await service.GenerateAsync(NarrativeInsightsService.RetentionOverviewScope);

        Assert.NotNull(result);
        Assert.Equal(NarrativeStatus.NotConfigured, result!.Status);
        Assert.Null(result.Narrative);
        Assert.NotNull(result.Message);
    }

    private class FakeRetentionOverviewService(RetentionOverviewDto overview) : IRetentionOverviewService
    {
        public Task<RetentionOverviewDto> BuildAsync(CancellationToken ct = default) => Task.FromResult(overview);
    }

    /// <summary>Stands in for the real Anthropic client in every test — the guardrail this whole
    /// suite checks is that nothing here ever needs a live API call.</summary>
    private class RecordingNarrativeGenerator : INarrativeGenerator
    {
        public NarrativeRequest? LastRequest { get; private set; }
        public NarrativeResult Result { get; set; } = new(NarrativeStatus.Generated, "narrative", null);
        public bool IsConfigured => true;

        public Task<NarrativeResult> GenerateNarrativeAsync(NarrativeRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
