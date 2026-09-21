using BKeeper.Domain.Entities;

namespace BKeeper.Application.Metrics;

/// <summary>
/// The seed content for the <see cref="MetricDefinition"/> registry: one row per number BKeeper
/// actually computes today, in <see cref="AttendanceMetrics"/>/<see cref="MetricsBuilder"/>
/// (member-level rule inputs) and <c>DashboardsController</c> (box-level dashboard figures).
/// Formulas here must match the real code exactly — see docs/PLAN.md §5 for the canonical prose
/// definitions this registry makes queryable, and docs/DECISIONS.md D24 for what the dashboards pass
/// actually built. This is the single source of truth both the EF Core migration seed (<c>HasData</c>
/// in <c>BKeeperDbContext</c>) and <c>MetricDefinitionCatalogTests</c> read from — analogous in spirit
/// to <see cref="Evaluations.EvaluationFormSeeder"/>'s DefaultForms, but global (not per-box) and
/// migration-seeded rather than seeded per box at bootstrap, since every box shares one definition
/// catalog (see the entity's doc comment).
/// </summary>
public static class MetricDefinitionCatalog
{
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<MetricDefinition> All { get; } = BuildAll();

    private static List<MetricDefinition> BuildAll()
    {
        var definitions = new List<MetricDefinition>
        {
        new()
        {
            Id = Guid.Parse("f7ebdc5a-d1f9-4678-b10e-c59ec60f3884"),
            Key = "active_members",
            Name = "Active members",
            Category = "Retention",
            Unit = "count",
            Aggregation = "count",
            TimeWindow = "as of today",
            Description = "How many members currently have status Active (plan §5: not frozen, not cancelled).",
            Formula = "COUNT(Members WHERE Status = Active)",
            WhyItMatters = "The denominator behind every other retention rate on the dashboard — new, churned, net change and monthly churn % are all measured against this number.",
            Limitations = "Checks Member.Status only. Plan §5's fuller definition — \"with membership valid on the date\" — would also cross-check Membership start/end and freeze windows, which this count doesn't do yet.",
        },
        new()
        {
            Id = Guid.Parse("609aee5b-e631-4766-a1f0-3e61d70cb42a"),
            Key = "new_members_this_month",
            Name = "New members this month",
            Category = "Retention",
            Unit = "count",
            Aggregation = "count",
            TimeWindow = "current calendar month, to date",
            Description = "Members who joined on or after the 1st of this month.",
            Formula = "COUNT(Members WHERE JoinDate BETWEEN first-of-month AND today)",
            WhyItMatters = "The growth half of Net member change, and the leading indicator for onboarding-track load (plan §6.6).",
            Limitations = "\"Today\" and \"first of month\" use UTC, not the box's own time zone (plan D9 says the box's local time zone should govern week/month boundaries) — this can be off by a day around midnight for boxes far from UTC.",
        },
        new()
        {
            Id = Guid.Parse("72919d59-aa36-488c-98bb-c918b9850745"),
            Key = "churned_members_this_month",
            Name = "Churned members this month",
            Category = "Retention",
            Unit = "count",
            Aggregation = "count",
            TimeWindow = "current calendar month, to date",
            Description = "Members whose CancelDate falls in the current month.",
            Formula = "COUNT(Members WHERE CancelDate BETWEEN first-of-month AND today)",
            WhyItMatters = "Feeds both Net member change and Monthly churn rate.",
            Limitations = "Counts explicit cancellations only. Plan §5's churn definition also includes \"lapsed\" (45+ days with no visit while still marked active) — that group is tracked separately as Lapsed members and is not folded into this count or into the monthly churn rate.",
        },
        new()
        {
            Id = Guid.Parse("5aa46cbe-e1d5-4bd9-a4ae-0540a511daa0"),
            Key = "net_member_change",
            Name = "Net member change",
            Category = "Retention",
            Unit = "count",
            Aggregation = "difference",
            TimeWindow = "current calendar month, to date",
            Description = "New members minus churned members this month.",
            Formula = "new_members_this_month − churned_members_this_month",
            WhyItMatters = "The single number that says whether the box is growing or shrinking this month.",
            Limitations = "Inherits both inputs' limitations: lapsed members reduce actual active headcount without ever counting as \"churned\" here, so a box with a lot of lapsing (but not cancelling) members will show a healthier net change than its real trajectory.",
        },
        new()
        {
            Id = Guid.Parse("09c6aa59-836b-42df-ac21-c3c685161990"),
            Key = "monthly_churn_rate_pct",
            Name = "Monthly churn rate",
            Category = "Retention",
            Unit = "percent",
            Aggregation = "rate",
            TimeWindow = "current calendar month, to date",
            Description = "The share of members who were already active before this month and have since cancelled.",
            Formula = "100 × churned_members_this_month ÷ COUNT(Members WHERE JoinDate < first-of-month AND (CancelDate IS NULL OR CancelDate >= first-of-month))",
            WhyItMatters = "Plan §13's headline success metric — target is −20% relative after 6 months of the pilot, measured against a Week-0 baseline.",
            Limitations = "Same lapsed-vs-cancelled gap as churned_members_this_month: this rate only reflects explicit cancellations, so it understates plan §5's full churn definition in boxes where members go quiet without formally cancelling.",
        },
        new()
        {
            Id = Guid.Parse("03e8ac21-d391-47cf-a5f0-831872d22eab"),
            Key = "lapsed_members",
            Name = "Lapsed members",
            Category = "Retention",
            Unit = "count",
            Aggregation = "count",
            TimeWindow = "as of today, 45-day no-visit threshold",
            Description = "Active members who haven't attended a class in 45+ days (plan §5's \"lapsed\" clause of Churned), including active members with no recorded visit at all.",
            Formula = "COUNT(Members WHERE Status = Active AND (no attended Booking OR days since their most recent attended Booking >= 45))",
            WhyItMatters = "Catches the members who are functionally gone but haven't cancelled — the group monthly_churn_rate_pct misses.",
            Limitations = "Computed live at query time rather than stored on the member (docs/DECISIONS.md D24). The 45-day threshold is hardcoded in the dashboard query today rather than read from rule_config, so changing it per box isn't wired up yet.",
        },
        new()
        {
            Id = Guid.Parse("f8a4c2bf-971e-4b28-9414-bf7cfc46bd22"),
            Key = "cohort_retention_curve",
            Name = "Cohort retention curve",
            Category = "Retention",
            Unit = "percent",
            Aggregation = "curve",
            TimeWindow = "months 0-12 since each cohort's join month",
            Description = "For members who joined in the same calendar month (a \"cohort\"), the share still not cancelled N months later.",
            Formula = "at month N: COUNT(cohort members WHERE CancelDate IS NULL OR CancelDate >= cohort_month + (N+1) months) ÷ cohort size",
            WhyItMatters = "Shows whether retention by tenure is improving release over release, independent of month-to-month join-volume swings — the standard SaaS/membership retention view.",
            Limitations = "\"Retained\" means \"not yet cancelled\", not \"still visiting\" — a frozen or lapsed member who hasn't formally cancelled still counts as retained in this curve. Months with no data yet (not enough time has passed since the cohort joined) show as blank, not zero.",
        },
        new()
        {
            Id = Guid.Parse("0d8bf362-adb0-4200-8b5b-890ce60179a1"),
            Key = "tenure_at_churn_histogram",
            Name = "Tenure-at-churn histogram",
            Category = "Retention",
            Unit = "count",
            Aggregation = "histogram",
            TimeWindow = "all churned members, all-time",
            Description = "How many months members stayed before cancelling, grouped into 1-month buckets up to 24+.",
            Formula = "for each cancelled member: months between their join month and their CancelDate, bucketed 0-1mo, 1-2mo, ... 24mo+",
            WhyItMatters = "Distinguishes an onboarding problem (a spike in the 0-1/1-2 month buckets) from long-tenure churn — very different fixes.",
            Limitations = "Only members with a recorded CancelDate appear here; lapsed members who never formally cancelled are excluded, even though plan §5 treats them as churned too.",
        },
        new()
        {
            Id = Guid.Parse("33500b4f-b904-483c-b4a1-030c69e47c8c"),
            Key = "alert_sla_compliance_rate_pct",
            Name = "Alert SLA compliance",
            Category = "AlertOps",
            Unit = "percent",
            Aggregation = "rate",
            TimeWindow = "all-time (not windowed)",
            Description = "Of the alerts that have been claimed or resolved, the share claimed before their SLA due date.",
            Formula = "100 × COUNT(alerts where the first Claimed event happened on or before Alert.DueAt) ÷ COUNT(alerts that were ever claimed, resolved, or auto-resolved)",
            WhyItMatters = "Plan §13's SLA target is ≥90% — a coverage/staffing signal for the alert inbox (plan §6.4's escalation ladder exists specifically to protect this).",
            Limitations = "An alert that was AutoResolved without ever being claimed lands in the denominator (via its Status) but has no claim timestamp to compare against DueAt, so it can never count as compliant — this slightly understates true SLA performance on boxes with a lot of unassisted returns.",
        },
        new()
        {
            Id = Guid.Parse("c93bf8ac-c140-47e8-a6e3-5d8491cf60d4"),
            Key = "alert_avg_time_to_claim_hours",
            Name = "Average time to claim",
            Category = "AlertOps",
            Unit = "hours",
            Aggregation = "mean",
            TimeWindow = "all-time (not windowed)",
            Description = "On average, how long an alert sits unclaimed before a coach or manager claims it.",
            Formula = "mean(first Claimed AlertEvent.CreatedAt − Alert.CreatedAt) in hours, over alerts claimed at least once",
            WhyItMatters = "A direct read on inbox responsiveness, independent of whether the SLA was technically met.",
            Limitations = "Alerts that were never claimed (e.g. auto-resolved before anyone acted) are excluded entirely rather than counted as a very long or infinite claim time, so this can look better than the SLA compliance rate suggests.",
        },
        new()
        {
            Id = Guid.Parse("42916e16-e0ba-4e8a-a6cc-c4ac99df3332"),
            Key = "alert_save_rate_pct",
            Name = "Save rate",
            Category = "AlertOps",
            Unit = "percent",
            Aggregation = "rate",
            TimeWindow = "all-time (not windowed)",
            Description = "Of the alerts that reached a resolved state, the share where the member came back (returned on their own or after being contacted).",
            Formula = "100 × COUNT(alerts WHERE Status = AutoResolved OR Outcome = 'returned') ÷ COUNT(alerts WHERE Status IN (Resolved, AutoResolved))",
            WhyItMatters = "Plan §13's \"Save rate\" — the headline number for whether the alert/outreach loop is working.",
            Limitations = "This is not a causal measure: a member marked \"saved\" here may well have returned regardless of any alert or outreach. See alert_return_rate_holdout_vs_treated_pct for the actual causal comparison plan §13 asks for.",
        },
        new()
        {
            Id = Guid.Parse("559b3a5b-c7ff-4ee2-b621-f2e2145c6a4c"),
            Key = "alert_return_rate_holdout_vs_treated_pct",
            Name = "Outreach return rate — holdout vs treated",
            Category = "AlertOps",
            Unit = "percent",
            Aggregation = "rate",
            TimeWindow = "per system outreach, next 14 days",
            Description = "Of members who received (or were held out from) an automated outreach message, the share who attended a class within 14 days afterwards — shown separately for the 10% random holdout group and the treated (message actually sent) group.",
            Formula = "100 × COUNT(recipients with an attended Booking within 14 days of the Outreach) ÷ COUNT(recipients), computed once with IsHoldout = true and once with IsHoldout = false",
            WhyItMatters = "Plan §13's causal check: without comparing against a holdout, \"the alert saved this member\" is unprovable, since many flagged members return on their own anyway.",
            Limitations = "Only covers system-sent outreach (Outreach.SentBy = System) — coach-sent messages skip the holdout gate (plan §6.5) entirely and aren't part of this comparison. Returns no value for a group until it has at least one recipient to divide by.",
        },
        new()
        {
            Id = Guid.Parse("6789264c-5dd6-4c68-b1a4-daf9f5535f63"),
            Key = "member_baseline_visits_per_week",
            Name = "Baseline visits per week",
            Category = "Attendance",
            Unit = "visits/week",
            Aggregation = "mean",
            TimeWindow = "weeks −26 to −4 relative to the snapshot date",
            Description = "A member's normal weekly attendance rate, used as the reference point their recent activity is compared against.",
            Formula = "mean(attended visits per ISO week), over weeks that start between 26 weeks and 4 weeks before the snapshot date",
            WhyItMatters = "Plan §5's \"Baseline rate\" — the number R01 (absence gap), R02 (not booking) and R03 (frequency drop) all gate on before evaluating a member, so a naturally low-frequency member isn't flagged for being naturally low-frequency.",
            Limitations = "Plan §5 also requires at least 8 weeks of post-join history before trusting a baseline (\"min 8 weeks\") — the current calculation doesn't enforce that minimum, so a very new member's baseline can be built from very few weeks of data. The seasonal August/Christmas dampening that exists as AttendanceMetrics.ApplySeasonalFactor is not applied inside this calculation.",
        },
        new()
        {
            Id = Guid.Parse("8996b77c-a7dc-4dd1-9457-f74fb327532e"),
            Key = "member_recent_attendance_ratio",
            Name = "Recent attendance ratio (2-week vs baseline)",
            Category = "Attendance",
            Unit = "ratio",
            Aggregation = "ratio",
            TimeWindow = "last 14 days vs the 26-week baseline",
            Description = "How a member's attendance over the last two weeks compares to their own usual rate.",
            Formula = "(attended visits in the last 14 days ÷ 2) ÷ member_baseline_visits_per_week",
            WhyItMatters = "This ratio is R03's frequency-drop trigger — amber below 60% of baseline, red below 30% by default (box-configurable) — the earliest attendance-based warning signal in the rule catalogue.",
            Limitations = "Only computed (and only shown) once BaselinePerWeek clears the rule's configured minimum (1.5/week by default); below that, R03 skips the member entirely rather than showing a misleadingly volatile ratio.",
        },
        new()
        {
            Id = Guid.Parse("5a4427d9-1552-4db7-b1c9-86976cf0beba"),
            Key = "member_no_show_rate_8w",
            Name = "No-show / late-cancel rate (8 weeks)",
            Category = "Attendance",
            Unit = "percent",
            Aggregation = "rate",
            TimeWindow = "last 8 weeks",
            Description = "The share of a member's bookings in the last 8 weeks that ended in a no-show or a too-late cancellation, rather than an attended visit.",
            Formula = "COUNT(Bookings in last 8 weeks WHERE Status IN (NoShow, LateCancel)) ÷ COUNT(Bookings in last 8 weeks), only evaluated when there are at least 6 bookings in that window",
            WhyItMatters = "This is what BKeeper calls the no-show rate (it's also the closest thing to a \"cancellation rate\" the app computes today) — it's R04's rate trigger (>25% by default) and flags members who keep booking but not showing.",
            Limitations = "No-shows and late-cancels are combined into one rate; there is no separately computed figure for late-cancels alone, or for ordinary advance cancellations (BookingStatus.Cancelled), even though the booking model tracks all three statuses separately.",
        },
        new()
        {
            Id = Guid.Parse("622c1ab7-619b-4e9b-87a4-681395725e7d"),
            Key = "member_median_gap_days",
            Name = "Median gap between visits",
            Category = "Attendance",
            Unit = "days",
            Aggregation = "median",
            TimeWindow = "last 12 weeks (84 days)",
            Description = "The typical number of days a member goes between one attended class and the next.",
            Formula = "median(days between consecutive attended-visit dates), over visits in the last 84 days",
            WhyItMatters = "Plan §5's \"Median gap\" exactly — R01's absence-gap threshold is built directly from it (min_days, or this value × a multiplier, whichever is larger).",
            Limitations = "Needs at least 2 visits in the 12-week window to produce a value; with 0 or 1 visits it's null, and R01 falls back to its configured min_days default instead of a member-specific threshold.",
        },
        new()
        {
            Id = Guid.Parse("6ca33336-fcfa-412a-bad0-0fbe58423329"),
            Key = "member_median_booking_gap_days",
            Name = "Median gap between bookings",
            Category = "Attendance",
            Unit = "days",
            Aggregation = "median",
            TimeWindow = "last 12 weeks (84 days)",
            Description = "The typical number of days a member goes between one booking and the next (booking date, not visit date).",
            Formula = "median(days between consecutive Booking.BookedAt dates), over bookings in the last 84 days",
            WhyItMatters = "Feeds R02's not-booking threshold, the same way member_median_gap_days feeds R01.",
            Limitations = "Same as member_median_gap_days: null with fewer than 2 bookings in the window, and R02 falls back to its configured min_days default.",
        },
        new()
        {
            Id = Guid.Parse("6b5ef895-60a2-4982-aba5-11db77b07bd3"),
            Key = "workout_window_type_mix",
            Name = "Window × type mix (box-level)",
            Category = "Workouts",
            Unit = "count",
            Aggregation = "heatmap",
            TimeWindow = "last 12 weeks (84 days)",
            Description = "How the box's attended visits break down by day of week, class time window (Early/Morning/Lunch/Afternoon/Evening) and workout type.",
            Formula = "COUNT(attended Bookings in the last 84 days), grouped by (DayOfWeek, ClassSession.Window, the class's dominant WorkoutTag)",
            WhyItMatters = "Surfaces underused time slots and shifting workout-type popularity across the whole box — feeds the workout-mix dashboard.",
            Limitations = "Plan §5 defines \"Usual window\" and \"Type mix\" per member (the mode of one member's own visit windows in weeks −16..−4, and their own workout-type distribution), which needs MemberProfile — not populated yet (docs/DECISIONS.md D24). This heatmap aggregates every member together as the closest thing currently computed; it is not the per-member figure the plan describes, and it isn't used by any rule (R05/R06/R07, which need the per-member version, aren't built).",
        },
        new()
        {
            Id = Guid.Parse("dceecc84-5d24-4728-a88c-0e633c42f811"),
            Key = "class_fill_rate_pct",
            Name = "Class fill rate",
            Category = "Workouts",
            Unit = "percent",
            Aggregation = "mean",
            TimeWindow = "last 12 weeks (84 days)",
            Description = "How full classes typically run, by class type and time window.",
            Formula = "100 × mean(COUNT(Bookings WHERE Status != Cancelled) ÷ ClassSession.Capacity), grouped by (ClassType, Window), over sessions in the last 84 days with a capacity set",
            WhyItMatters = "Identifies over- or under-booked slots — an input to future class-time-optimisation work (plan §16.5).",
            Limitations = "No-shows and late-cancels still count toward the numerator (only Cancelled bookings are excluded), so this measures booked fill, not who actually showed up. Sessions without a Capacity value are skipped entirely rather than treated as unlimited.",
        },
        };

        foreach (var definition in definitions)
        {
            definition.Version = 1;
            definition.IsActive = true;
            definition.CreatedAt = SeedTimestamp;
            definition.UpdatedAt = SeedTimestamp;
        }

        return definitions;
    }
}
