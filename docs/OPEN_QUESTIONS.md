# Open questions

Per plan §0.3: every ambiguity gets a documented default here instead of blocking. Newest first.

## From this pass (foundation build)

- **No LLM integration exists**, so evaluation free-text answers (injury/price/schedule/coach/motivation
  "suggested" tags per plan §9) are stored as-is but never auto-tagged. Would need an LLM API key/service
  configured — out of scope without one.
- **`MemberProfile` (usual window/days, type mix, persona, baseline) is never populated.** The entity
  exists (Week 3) but no job builds it — R05/R06/R07 (window/type-shift rules) can't run without it,
  and Week 6's `{usual_class}`/`{coach}` template variables fall back to static placeholder text
  because of this gap.
- **Injury notes vs. `InjuryFlagUntil` suppression.** The domain model has `Member.InjuryFlagUntil`
  (used by the rule engine to suppress alerts), but member notes are freeform text with no structured
  "until" date. Default: notes are informational only; a coach who wants suppression still has to set
  it separately (not yet exposed in the API/UI). Open question: should tagging a note as "injury"
  prompt for an `until` date and set the flag automatically?
- **"Human contact in last 7 days" always evaluates to `false`** in the rule pipeline, because
  outreach/notifications (Week 6) aren't built yet. This means the plan's §6.3 "K: human contact ->
  create as info / delay" branch never fires today — every non-cooldown hit becomes a full-severity
  alert. Revisit once outreach exists.
- **Onboarding rule (R08) is simplified** to "no visit within 7 days of joining" (red). The plan's
  full day-0/3/7/14/30/60/90 track (§6.6) is a scheduled workflow with its own state, not a pure
  per-run rule — it needs its own job/table and was out of scope for this pass.
- **R05–R07, R09–R12, R14 are not implemented.** Only the attendance family (R01–R04) plus the
  simplified R08 exist. `RuleConfig` rows for the others can be added later without a schema change.
- **SLA escalation (§6.4) is not implemented** — alerts are created with a `DueAt` and an
  `AssignedRole`, but nothing currently promotes an unclaimed alert to Manager/Owner or reopens an
  idle-claimed one. `AlertStatus.Escalated`/`Reopened` exist on the enum but nothing sets them yet.
- **Timezone handling is simplified.** All dates/times are treated as UTC; the plan's
  `Europe/Lisbon`-by-default, per-box timezone handling (quiet hours, week boundaries) isn't wired up.
  `Box.TimeZone` exists as a column but nothing reads it yet.

## Carried over from the original plan (still unanswered)

- Which platform/API this box exports from, and whether it has a real API (affects Week 10).
- Exact churn definition the owner wants live (cancelled vs. the 45-day "lapsed" default).
- Does the real Excel export include plan/freeze/payment data?
- Are workouts stored per class or per day in the real source data?
- WhatsApp provider choice (Meta Cloud API direct vs. Twilio/360dialog via BSP).
- Who staffs the alert inbox and during what hours (coverage model)?
- Languages needed beyond pt-PT/en?
- Does the box want a member-facing app/PWA in v1?
