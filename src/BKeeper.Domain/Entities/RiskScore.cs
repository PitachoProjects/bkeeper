using BKeeper.Domain.Common;

namespace BKeeper.Domain.Entities;

/// <summary>
/// Weekly churn-risk snapshot from the ML service (plan §8, R13). Shadow mode: scores are stored
/// and shown to Manager/Owner only — nothing here creates an alert. See docs/OPEN_QUESTIONS.md for
/// the real-data backtest gate that decides whether R13 ever goes live.
///
/// One row per (box, member, snapshot week, <see cref="ModelType"/>) — a member can have two rows
/// for the same week, one per model, so the Stage B (logistic regression) comparison view never
/// overwrites Stage C's (LightGBM ensemble) row, and vice versa (see docs/DECISIONS.md).
/// </summary>
public class RiskScore : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public DateOnly SnapshotWeek { get; set; }
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>"lightgbm_ensemble" (Stage C, the shadow-mode default R13 would use) or
    /// "logistic_regression" (Stage B, interpretable comparison) — see BKeeper.Application.Ml.MlModelTypes.</summary>
    public string ModelType { get; set; } = "lightgbm_ensemble";

    public double PChurn28d { get; set; }
    public string Band { get; set; } = string.Empty; // red | amber | none
    public List<string> TopReasons { get; set; } = new();
}
