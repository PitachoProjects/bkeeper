using System.Net;
using System.Text;
using BKeeper.Application.Insights;
using BKeeper.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BKeeper.Tests.Unit.Insights;

/// <summary>Never call the real Anthropic API in tests — everything here runs against a stub
/// <see cref="HttpMessageHandler"/> that fails the test if it's ever invoked while unconfigured, and
/// otherwise returns a canned response.</summary>
public class AnthropicNarrativeGeneratorTests
{
    [Fact]
    public void IsConfigured_FalseWhenApiKeyMissing()
    {
        var generator = BuildGenerator(apiKey: "", handler: new ThrowingHandler());
        Assert.False(generator.IsConfigured);
    }

    [Fact]
    public async Task GenerateNarrativeAsync_NoApiKey_ReturnsNotConfigured_NeverMakesHttpCall()
    {
        var handler = new ThrowingHandler();
        var generator = BuildGenerator(apiKey: "", handler: handler);

        var result = await generator.GenerateNarrativeAsync(new NarrativeRequest(NarrativeInsightsService.RetentionOverviewScope, "{}"));

        Assert.Equal(NarrativeStatus.NotConfigured, result.Status);
        Assert.Null(result.Narrative);
        Assert.NotNull(result.Message);
        Assert.False(handler.WasInvoked); // the actual crash-prevention guarantee: no live call, ever
    }

    [Fact]
    public async Task GenerateNarrativeAsync_Configured_ParsesTextBlockFromResponse()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"content":[{"type":"text","text":"FACTS: 480 active members.\nSIGNALS: churn steady.\nINSIGHT: nothing alarming."}],"stop_reason":"end_turn"}
            """);
        var generator = BuildGenerator(apiKey: "sk-test-key", handler: handler);

        var result = await generator.GenerateNarrativeAsync(new NarrativeRequest(NarrativeInsightsService.RetentionOverviewScope, """{"activeCount":480}"""));

        Assert.Equal(NarrativeStatus.Generated, result.Status);
        Assert.Contains("480 active members", result.Narrative);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("sk-test-key", handler.LastRequest!.Headers.GetValues("x-api-key").Single());
        // The evidence payload must be the only data reaching the model alongside the fixed system prompt.
        Assert.Contains("activeCount", handler.LastRequestBody);
        Assert.Contains("associated with", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateNarrativeAsync_HttpFailure_ReturnsErrorStatus_NeverThrows()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "{}");
        var generator = BuildGenerator(apiKey: "sk-test-key", handler: handler);

        var result = await generator.GenerateNarrativeAsync(new NarrativeRequest(NarrativeInsightsService.RetentionOverviewScope, "{}"));

        Assert.Equal(NarrativeStatus.Error, result.Status);
        Assert.Null(result.Narrative);
    }

    private static AnthropicNarrativeGenerator BuildGenerator(string apiKey, HttpMessageHandler handler)
    {
        var options = Options.Create(new AnthropicOptions { ApiKey = apiKey, Model = "claude-opus-5", BaseUrl = "https://api.anthropic.test" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
        return new AnthropicNarrativeGenerator(httpClient, options, NullLogger<AnthropicNarrativeGenerator>.Instance);
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        public bool WasInvoked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasInvoked = true;
            throw new InvalidOperationException("The narrative generator must not call the network when it isn't configured.");
        }
    }

    private class StubHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}
