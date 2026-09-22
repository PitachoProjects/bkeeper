# Open questions

Per plan §0.3: every ambiguity gets a documented default here instead of blocking. Newest first.

## From this pass (foundation build)

> The section below describes gaps as of the original Week 0-4 foundation build. Several were closed
> by later weeks (SLA escalation: Week 5/D20; outreach/notifications: Week 6/D21) — cross-check against
> [DECISIONS.md](DECISIONS.md)'s newest-first ADR log (currently through D29) for what's actually true
> today rather than trusting every bullet here at face value.

- **No real platform connector exists.** `IBoxDataConnector` defines the contract (plan §10) but
  nothing implements it — the plan requires reading the chosen platform's real API docs first, and no
  platform was ever named. Excel import remains the only ingestion path. Whoever picks a platform
  needs to implement this interface against that platform's actual API.
- **No automated backup schedule or off-host backup storage.** `scripts/backup.sh`/`restore.sh` work
  (drilled live) but nothing runs them on a schedule or ships the dump off the Docker host.
- **No secrets manager.** All secrets (JWT signing key, ML service token, DB password) are environment
  variables with dev-only defaults in `docker-compose.yml`. Fine for local dev; must be replaced with
  real secret management before any real deployment.
- **Persona distribution and the R07 type-abandonment dashboard aren't built** — both need
  `MemberProfile` populated (usual window/days, type mix, persona), which nothing writes yet (same
  root cause as the earlier `MemberProfile` gap).
- **No celebrations feed on the coach "my week" dashboard** — R09 (milestone alerts: 25/50/100 classes,
  anniversaries, PRs) isn't implemented, so there's nothing to show there yet.
- **The ML model has never seen real data.** Its synthetic-backtest metrics (AUC 0.85 etc.) validate
  that the pipeline runs correctly end-to-end, not that the model will perform well on a real box's
  data. Plan §8 is explicit that the first real backtest (Week 8/12 on real data) decides the actual
  go-live thresholds — that gate isn't evaluated anywhere in this codebase yet.
- **LLM integration exists but only for one narrow use** — `POST /insights/narrative` turns the
  retention-overview dashboard numbers into a plain-language summary (see DECISIONS.md D26), gated
  behind an optional `ANTHROPIC_API_KEY`. Evaluation free-text answers (injury/price/schedule/coach/
  motivation "suggested" tags per plan §9) are still stored as-is and never auto-tagged — that would
  need its own prompt/guardrails built the same way, not a given just because a narrative generator
  now exists.
- **`MemberProfile` (usual window/days, type mix, persona, baseline) is never populated.** The entity
  exists (Week 3) but no job builds it — R05/R06/R07 (window/type-shift rules) can't run without it,
  and Week 6's `{usual_class}`/`{coach}` template variables fall back to static placeholder text
  because of this gap.
- **Injury notes vs. `InjuryFlagUntil` suppression.** The domain model has `Member.InjuryFlagUntil`
  (used by the rule engine to suppress alerts), but member notes are freeform text with no structured
  "until" date. Default: notes are informational only; a coach who wants suppression still has to set
  it separately (not yet exposed in the API/UI). Open question: should tagging a note as "injury"
  prompt for an `until` date and set the flag automatically?
- **"Human contact in last 7 days" still always evaluates to `false`.** Outreach/notifications
  (Week 6/D21) exist now, but `AttendanceRules.cs` never queries `Outreach` — the plan's §6.3 "K:
  human contact -> create as info / delay" branch never fires; every non-cooldown hit still becomes
  a full-severity alert regardless of recent contact. Confirmed still true, not just carried over.
- **Onboarding rule (R08) is simplified** to "no visit within 7 days of joining" (red). The plan's
  full day-0/3/7/14/30/60/90 track (§6.6) is a scheduled workflow with its own state, not a pure
  per-run rule — it needs its own job/table and was out of scope for this pass.
- **R05–R07, R09–R12, R14 are not implemented.** Only the attendance family (R01–R04) plus the
  simplified R08 exist. `RuleConfig` rows for the others can be added later without a schema change.
- ~~SLA escalation (§6.4) is not implemented~~ — **built in Week 5 (D20)**: `escalation-job` runs
  every 15 minutes, promoting unclaimed alerts and releasing idle-claimed ones. Left here struck
  through rather than deleted, as a pointer for anyone still holding the stale assumption.
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
