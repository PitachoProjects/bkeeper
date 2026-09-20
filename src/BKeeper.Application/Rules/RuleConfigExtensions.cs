using System.Text.Json;

namespace BKeeper.Application.Rules;

public static class RuleConfigExtensions
{
    public static double GetDouble(this IReadOnlyDictionary<string, object> config, string key, double fallback) =>
        config.TryGetValue(key, out var v) ? ToDouble(v) : fallback;

    public static int GetInt(this IReadOnlyDictionary<string, object> config, string key, int fallback) =>
        config.TryGetValue(key, out var v) ? (int)ToDouble(v) : fallback;

    private static double ToDouble(object v) => v switch
    {
        JsonElement je => je.GetDouble(),
        IConvertible c => c.ToDouble(null),
        _ => throw new InvalidOperationException($"Cannot convert rule config value '{v}' to a number."),
    };
}
