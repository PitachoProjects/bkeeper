using BKeeper.Domain.Common;

namespace BKeeper.Domain.Entities;

/// <summary>
/// Box-scoped, versioned weights/config for the composite Athlete Health Score. Only one row per box
/// has <see cref="IsActive"/> true at a time; a config update never edits a row in place — it inserts
/// a new version and flips the old one inactive, so a <see cref="HealthScore"/> computed under an old
/// config keeps pointing at the version that was active when it was calculated (see HealthScore.ConfigVersion).
/// Mirrors the immutable-version pattern <see cref="EvaluationForm"/> uses for its schema.
/// </summary>
public class HealthScoreConfiguration : BoxScopedEntity
{
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    // Defaults per spec: Attendance 35 / Consistency 25 / Booking behaviour 15 / Progress 15 / Engagement 10.
    public double AttendanceWeight { get; set; } = 35;
    public double ConsistencyWeight { get; set; } = 25;
    public double BookingBehaviourWeight { get; set; } = 15;
    public double ProgressWeight { get; set; } = 15;
    public double EngagementWeight { get; set; } = 10;

    public bool AttendanceEnabled { get; set; } = true;
    public bool ConsistencyEnabled { get; set; } = true;
    public bool BookingBehaviourEnabled { get; set; } = true;
    public bool ProgressEnabled { get; set; } = true;
    public bool EngagementEnabled { get; set; } = true;

    public int AttendanceWindowDays { get; set; } = 28;
    public int ConsistencyWindowWeeks { get; set; } = 8;
    public int BookingBehaviourWindowDays { get; set; } = 56;
    public int ProgressWindowDays { get; set; } = 90;
    public int EngagementWindowDays { get; set; } = 180;

    /// <summary>Cold-start gate: below either threshold, a member gets no numeric score at all (see HealthScore.InsufficientData).</summary>
    public int MinTenureDays { get; set; } = 14;
    public int MinSessions { get; set; } = 3;

    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
}

/// <summary>One factor's contribution to a calculated <see cref="HealthScore"/>. Weight/contribution are
/// captured at calculation time (after any renormalization), never recomputed from the current config.</summary>
public record HealthScoreFactorBreakdown
{
    /// <summary>0-100, or null when this factor has no usable data (see Reason) — never a fabricated 0.</summary>
    public double? Score { get; init; }
    /// <summary>Renormalized weight (0-100 scale) this factor actually carried in the overall score; 0 when Included is false.</summary>
    public double Weight { get; init; }
    /// <summary>Score * Weight / 100 — what this factor added to OverallScore.</summary>
    public double Contribution { get; init; }
    /// <summary>False if the factor was disabled in config, or lacked enough data to score (see Reason).</summary>
    public bool Included { get; init; }
    /// <summary>Machine-readable reason when Included is false, e.g. "no_goals", "insufficient_history", "disabled".</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// One member's composite health score for one calculation date (plan-adjacent, product-spec feature —
/// not in the original plan §4 table). One row per (box, member, date), like <see cref="MemberWeek"/> is
/// one row per (box, member, week). Historical rows are never rewritten by a later config change.
/// </summary>
public class HealthScore : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public DateOnly CalculationDate { get; set; }
    public int ConfigVersion { get; set; }

    /// <summary>0-100, or null when InsufficientData is true.</summary>
    public double? OverallScore { get; set; }
    /// <summary>True when the member is below the config's cold-start thresholds (tenure/session count) —
    /// no numeric score is produced; TenureDays/SessionCount are still recorded so the UI can show what IS known.</summary>
    public bool InsufficientData { get; set; }
    public int TenureDays { get; set; }
    public int SessionCount { get; set; }

    /// <summary>Keyed by factor name (Attendance/Consistency/BookingBehaviour/Progress/Engagement).</summary>
    public Dictionary<string, HealthScoreFactorBreakdown> Factors { get; set; } = new();

    public DateTimeOffset CalculatedAt { get; set; } = DateTimeOffset.UtcNow;
}
