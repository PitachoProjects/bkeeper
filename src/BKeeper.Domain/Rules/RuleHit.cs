using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Rules;

public record RuleHit(string RuleCode, AlertFamily Family, AlertSeverity Severity, IReadOnlyDictionary<string, object> Evidence);

/// <summary>A rule is a pure function: metrics + config in, an optional hit out. No side effects, no I/O.</summary>
public interface IRule
{
    string Code { get; }
    AlertFamily Family { get; }
    RuleHit? Evaluate(MemberMetrics metrics, IReadOnlyDictionary<string, object> config);
}
