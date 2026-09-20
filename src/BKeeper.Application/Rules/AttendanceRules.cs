using BKeeper.Domain.Enums;
using BKeeper.Domain.Rules;

namespace BKeeper.Application.Rules;

/// <summary>R01 — Absence gap (§7). Amber at threshold, red beyond a multiplier or an absolute day cap.</summary>
public class R01AbsenceGap : IRule
{
    public string Code => "R01";
    public AlertFamily Family => AlertFamily.Attendance;

    public RuleHit? Evaluate(MemberMetrics m, IReadOnlyDictionary<string, object> config)
    {
        var minBaseline = config.GetDouble("min_baseline_per_week", 1.0);
        if (m.BaselinePerWeek < minBaseline || m.DaysSinceLastVisit is null) return null;

        var minDays = config.GetInt("min_days", 10);
        var gapMultiplier = config.GetDouble("gap_multiplier", 2.5);
        var capDays = config.GetInt("cap_days", 30);
        var redMultiplier = config.GetDouble("red_multiplier", 1.5);
        var redDays = config.GetInt("red_days", 21);

        var threshold = Math.Min(capDays, Math.Max(minDays, (m.MedianGapDays ?? minDays) * gapMultiplier));
        if (m.DaysSinceLastVisit < threshold) return null;

        var severity = m.DaysSinceLastVisit >= Math.Max(threshold * redMultiplier, redDays)
            ? AlertSeverity.Red
            : AlertSeverity.Amber;

        return new RuleHit(Code, Family, severity, new Dictionary<string, object>
        {
            ["days_since_last_visit"] = m.DaysSinceLastVisit,
            ["threshold_days"] = threshold,
        });
    }
}

/// <summary>R02 — Not booking: no upcoming booking and a stale last-booking gap.</summary>
public class R02NotBooking : IRule
{
    public string Code => "R02";
    public AlertFamily Family => AlertFamily.Attendance;

    public RuleHit? Evaluate(MemberMetrics m, IReadOnlyDictionary<string, object> config)
    {
        var minBaseline = config.GetDouble("min_baseline_per_week", 2.0);
        if (m.BaselinePerWeek < minBaseline || m.HasUpcomingBooking7d || m.DaysSinceLastBooking is null) return null;

        var minDays = config.GetInt("min_days", 5);
        var gapMultiplier = config.GetDouble("gap_multiplier", 1.5);
        var threshold = Math.Max(minDays, (m.MedianBookingGapDays ?? minDays) * gapMultiplier);
        if (m.DaysSinceLastBooking < threshold) return null;

        return new RuleHit(Code, Family, AlertSeverity.Amber, new Dictionary<string, object>
        {
            ["days_since_last_booking"] = m.DaysSinceLastBooking,
            ["threshold_days"] = threshold,
        });
    }
}

/// <summary>R03 — Frequency drop: last-2-week rate vs baseline.</summary>
public class R03FrequencyDrop : IRule
{
    public string Code => "R03";
    public AlertFamily Family => AlertFamily.Attendance;

    public RuleHit? Evaluate(MemberMetrics m, IReadOnlyDictionary<string, object> config)
    {
        var minBaseline = config.GetDouble("min_baseline", 1.5);
        if (m.BaselinePerWeek < minBaseline || m.BaselinePerWeek <= 0) return null;

        var ratio = m.Last2WeekRate / m.BaselinePerWeek;
        var ratioAmber = config.GetDouble("ratio_amber", 0.6);
        var ratioRed = config.GetDouble("ratio_red", 0.3);
        if (ratio >= ratioAmber) return null;

        var severity = ratio < ratioRed ? AlertSeverity.Red : AlertSeverity.Amber;
        return new RuleHit(Code, Family, severity, new Dictionary<string, object>
        {
            ["last_2w_rate"] = m.Last2WeekRate,
            ["baseline_per_week"] = m.BaselinePerWeek,
            ["ratio"] = ratio,
        });
    }
}

/// <summary>R04 — No-show streak: repeated no-shows/late-cancels in a short window, or a high rate over 8 weeks.</summary>
public class R04NoShowStreak : IRule
{
    public string Code => "R04";
    public AlertFamily Family => AlertFamily.Attendance;

    public RuleHit? Evaluate(MemberMetrics m, IReadOnlyDictionary<string, object> config)
    {
        var streakThreshold = config.GetInt("streak_threshold_14d", 2);
        var rateThreshold = config.GetDouble("rate_threshold_8w", 0.25);
        var minBookings8w = config.GetInt("min_bookings_8w", 6);

        var byStreak = m.NoShowsLast14d >= streakThreshold;
        var byRate = m.BookingsLast8w >= minBookings8w && (double)m.NoShowsLast8w / m.BookingsLast8w > rateThreshold;
        if (!byStreak && !byRate) return null;

        return new RuleHit(Code, Family, AlertSeverity.Amber, new Dictionary<string, object>
        {
            ["no_shows_14d"] = m.NoShowsLast14d,
            ["no_shows_8w"] = m.NoShowsLast8w,
            ["bookings_8w"] = m.BookingsLast8w,
        });
    }
}

/// <summary>
/// R08 — Onboarding (simplified): no visit within the first 7 days of joining.
/// The full day-0/3/7/14/30/60/90 scheduled track (§6.6) is a workflow, not a pure per-run rule,
/// and is not built in this pass — see docs/OPEN_QUESTIONS.md.
/// </summary>
public class R08OnboardingNoFirstVisit : IRule
{
    public string Code => "R08";
    public AlertFamily Family => AlertFamily.Onboarding;

    public RuleHit? Evaluate(MemberMetrics m, IReadOnlyDictionary<string, object> config)
    {
        var days = config.GetInt("days", 7);
        if (m.DaysSinceJoin < days || m.VisitsSinceJoin > 0 || m.DaysSinceJoin > days + 3) return null;

        return new RuleHit(Code, Family, AlertSeverity.Red, new Dictionary<string, object>
        {
            ["days_since_join"] = m.DaysSinceJoin,
        });
    }
}
