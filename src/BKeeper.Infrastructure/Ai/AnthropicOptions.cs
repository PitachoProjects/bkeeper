namespace BKeeper.Infrastructure.Ai;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>Empty by default — the narrative feature is a no-op until an operator sets this
    /// (env var <c>Anthropic__ApiKey</c>). Never crash on a missing key; see
    /// <see cref="AnthropicNarrativeGenerator"/>.</summary>
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-opus-5";
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public int MaxTokens { get; set; } = 1024;
}
