using BKeeper.Domain.Common;

namespace BKeeper.Domain.Entities;

/// <summary>
/// Weekly churn-risk snapshot from the ML service (plan §8, R13). Shadow mode: scores are stored
/// and shown to Manager/Owner only — nothing here creates an alert. See docs/OPEN_QUESTIONS.md for
/// the real-data backtest gate that decides whether R13 ever goes live.
/// </summary>
public class RiskScore : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public DateOnly SnapshotWeek { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public double PChurn28d { get; set; }
    public string Band { get; set; } = string.Empty; // red | amber | none
    public List<string> TopReasons { get; set; } = new();
}
