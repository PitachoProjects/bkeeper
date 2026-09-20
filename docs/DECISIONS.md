# Decisions (ADR log)

Extends §2 of [PLAN.md](PLAN.md). Newest first.

## D24 — Week 9 complete: dashboards (retention, alert ops, workouts, my week)
`GET /dashboards/{retention|alerts|workouts|my-week}` plus a retention CSV export:
- **Retention overview**: active/new/churned/net/monthly-churn, computed directly from `Member`
  (no new tables). "Lapsed" is computed at query time (active status, no visit ≥45 days) rather than
  stored, matching plan §5's definition literally.
- **Cohort retention curves** and the **tenure-at-churn histogram** are pure functions
  (`CohortAnalysis`, [src/BKeeper.Application/Dashboards/CohortAnalysis.cs](../src/BKeeper.Application/Dashboards/CohortAnalysis.cs)) — the only genuinely
  algorithmic part of this week; the rest is SQL-shaped aggregation that doesn't benefit from being
  forced into "pure function" form, so it lives directly in `DashboardsController`.
- **Alert operations**: volume by severity/family, SLA compliance %, avg time-to-claim, outcomes mix,
  save rate, and a holdout-vs-treated return-rate comparison (the plan §13 causal check — only
  meaningful once there's enough `Outreach` volume with `IsHoldout` set to compare).
- **Workout mix**: window×type heatmap and class-fill-by-slot, computed live from
  `Booking`/`ClassSession`/`WorkoutTag` over the last 12 weeks. Persona distribution and the R07
  type-abandonment aggregate are **not built** — both depend on `MemberProfile` being populated,
  which is the Week 3 gap already tracked in OPEN_QUESTIONS.md.
- **Coach "my week"**: open alerts assigned to Coach or claimed by the caller, due-this-week and
  resolved-this-week counts. No celebrations feed (R09 milestone rule isn't built).
- **Another real bug found and fixed** while wiring the CSV export: `ActionResult<T>.Value` is `null`
  when the action method returned via `Ok(x)` — the implicit `T -> ActionResult<T>` conversion that
  populates `.Value` only fires on a bare `return dto;`, not through `Ok()` (which returns a plain
  `ActionResult`). The export endpoint called `(await Retention()).Value` and got null every time,
  404-ing unconditionally. Fixed by splitting the DTO-building logic into a private method both the
  GET endpoint and the export call directly — a reusable pattern for any endpoint that wants another
  action's data. Grepped the rest of the controllers for the same mistake; none found.

## D23 — Week 8 complete: ML scoring service, shadow mode (R13)
A Python 3.12 FastAPI service (`ml/`) — D2's own choice — with its own Docker image and Compose
service (`ml`, port 8090), independent of the .NET services:
- **Feature computation lives only in Python** (`ml/app/features.py`), imported by both training and
  serving. This is a deliberate deviation from the plan's literal ask — "same code for training and
  serving" with a cross-language (.NET vs Python) parity test within 1e-6 — because duplicating 16
  features in two languages and testing they agree is more code and more drift risk than just not
  duplicating them: the .NET side ships raw booking facts over HTTP and never computes a feature
  itself. Tradeoff: a network hop at serve time instead of an in-process C# computation; acceptable
  for weekly batch scoring.
- Logistic regression + LightGBM ensemble (plan §8's exact model choice), SHAP explanations mapped
  through a phrase whitelist, isotonic-calibration and monthly-retrain are **not** built (see
  OPEN_QUESTIONS.md) — this pass proves the pipeline shape, not a production training cadence.
- **No real member data exists**, so `ml/app/synthetic.py` generates a synthetic population
  (independent of, not a port of, the plan's own undisclosed `blackkeeper_prototype.py`) purely to
  let the temporal-split backtest run and be sanity-checked end-to-end. Default run: AUC 0.85,
  PR-AUC ~9x base rate, lift ~9x in the top 5% — comparable to the plan's own quoted prototype
  numbers, but **this is an engineering validation, not a performance claim** — plan §8 is explicit
  that real thresholds need a real backtest.
- R13 runs **shadow mode only**: `MlScoringJob` (Sunday 23:30, or `POST /risk-scores/run` on demand)
  stores a `RiskScore` row per eligible member (tenure ≥ 12 weeks) — it never creates an alert. Scores
  are visible only to Manager/Owner (`GET /risk-scores`, and a "Churn risk" card on the member page),
  matching plan §8's shadow-mode requirement literally.
- The service **self-trains a synthetic model on first boot** if the model registry is empty, instead
  of requiring a manual `docker compose run ml python -m app.train` step — good enough until a real
  export exists, at which point retraining on real data (`python -m app.train`) replaces it.
- **Two real bugs found and fixed while building this**, both worth knowing about beyond this feature:
  1. `train.py`'s backtest anchored its horizon on `members[0].join_date` instead of the generator's
     actual `start` date — since member 0's own join week is itself randomized, this silently
     shifted/truncated the horizon and starved the test period of any late-calendar churn events,
     producing a backwards (AUC < 0.5) model. Fixed by anchoring on the real `start` explicitly.
  2. **`User.FindFirst("role")` returned null everywhere**, because ASP.NET Core's JWT bearer handler
     remaps the `"role"` claim (and `"sub"`, `"email"`) to their long-form `ClaimTypes.*` URIs on
     every inbound token by default — a well-known but easy-to-miss .NET quirk. This had been silently
     broken since Week 1 (nothing previously gated on the role claim); it only surfaced once
     `RiskScoresController`'s Manager/Owner check exercised it. Fixed globally with
     `JwtBearerOptions.MapInboundClaims = false` in `Program.cs`, so every claim name in code now
     matches the name `JwtTokenService` actually put in the token.
- **Not built:** real-data retraining/monthly cadence, calibration, drift monitoring, the go-live gate
  evaluation itself (plan §8's AUC/PR-AUC/precision@K comparison against rules-v1 on real data), and
  `{goal_gap, last_eval_score, message_response, class_fill_context, payment_failed}` (the plan's
  "later" feature set, which needs data this pass doesn't have sources for yet).

## D22 — Week 7 complete: goals, evaluation forms, R10/R11
Added `Goal`/`GoalProgress` and `EvaluationForm`/`EvaluationResponse`/`EvaluationFormLink` (plan §4/§9):
- `EvaluationScorer` (pure, [src/BKeeper.Application/Evaluations/EvaluationScorer.cs](../src/BKeeper.Application/Evaluations/EvaluationScorer.cs)) computes flags (scale ≤ 2, NPS ≤ 6, a yes/no health question answered "yes"), per-question numeric scores, an engagement index, and conditional-question visibility. The plan names specific questions for its `engagement_index` formula ("satisfaction, welcome, energy, coach"); this generalises to whatever scale-1-5 questions a form actually has rather than hardcoding those keys.
- `GoalRiskEvaluator` (pure) implements R10 exactly as written: at risk if no progress update in 8 weeks, or if the target date is within 6 weeks and the straight-line pace (progress-so-far vs. elapsed-time-so-far, 20pp tolerance) isn't being met.
- R11 (evaluation overdue) and R10 create alerts via a new `SimpleAlertService` — extracted from what was inline logic in `GoalsEvaluationsJob`, since these are single-hit call sites (unlike `DailyRulePipeline`, which needs `AlertOrchestrator`'s batched multi-hit grouping because several attendance rules can fire on the same member in the same run).
- The 6 seed forms from plan §9 (ONBOARDING, PULSE_D30, PULSE_D90, QUARTERLY, BENCHMARK, EXIT) are seeded per-box at `/auth/bootstrap` time via `EvaluationFormSeeder`.
- Public form flow: `POST /forms/{key}/send` creates a signed, single-use, 14-day `EvaluationFormLink` (a random 32-char token, not a JWT — simpler for something that's checked once and thrown away) and queues the `EVAL_REQUEST` template; `GET/POST /f/{token}` are the only `[AllowAnonymous]` endpoints in the API. A submission that flags negative or health creates/appends an `EVAL_NEG` or `HEALTH` alert (family `Eval`).
- **The public endpoints exposed a real multi-tenancy bug**, fixed here: `CurrentBoxAccessor.BoxId` used to throw when no box scope was active, on the assumption that the `!currentBox.HasBox ||` guard in the EF global query filter would short-circuit before it's evaluated. It doesn't — EF Core's query translator pulls out captured-variable member accesses (like this getter) as parameters *eagerly*, before the SQL `OR` gets a chance to short-circuit, so the getter must never throw. Now returns `Guid.Empty` when unscoped, matching what the filter actually needs (see `CurrentBoxAccessor`'s updated doc comment and the regression test in `CurrentBoxAccessorTests`). This means **every previous anonymous/cross-box query path had this latent bug** — it just hadn't been exercised until the first `[AllowAnonymous]` endpoint (this one) went live.
- Consistency goals (`GoalCategory.Consistency`) get one `GoalProgress` row per ISO week, auto-computed from attendance inside `DailyRulePipeline` (source=`Automatic`) — plan §9's "automatic (consistency goals computed from attendance)".
- Member page gained Goals (add/list/record progress) and Evaluation Forms (list/send) sections; a new unauthenticated `/f/:token` route renders the form, applies conditional-question visibility client-side, and submits.
- **Not built:** the LLM free-text tagging of open answers (injury/price/schedule/coach/motivation "suggested" tags — no LLM integration exists), the Manager-side form/schema editing UI (forms are seeded, not built through a UI), automated reminder-1/reminder-2 *scheduling* beyond the single day-7 reminder + day-21 alert already implemented, and a computed response-rate metric/dashboard.

## D21 — Week 6 complete: consent, templates, policy engine, log-based provider
Added `MemberConsent` and `Outreach` entities (plan §4) plus:
- `NotificationPolicy` (pure function, [src/BKeeper.Application/Notifications/NotificationPolicy.cs](../src/BKeeper.Application/Notifications/NotificationPolicy.cs)) implements the §6.5 flowchart: red never auto-sends (`RequiresHuman`), max 1 automated message per member per 7 days, a deterministic 10% holdout (stable per member via a hash, not a separate assignment table), per-channel consent with a WhatsApp→Push→Email fallback order, and a 21:30-09:00 quiet-hours window that scheduling respects.
- `TemplateCatalog` — the 10 template keys from plan §10, pt-PT/en, with whitelist-only variable substitution (an unrendered `{placeholder}` fails loudly instead of leaking to a member).
- **Two-phase send, not one step:** `OutreachQueueService` decides and writes a `Queued` row; `OutreachDispatcher` (a separate 15-min Hangfire job) actually calls the provider once it's outside quiet hours. This split exists because the daily rule pipeline runs at 05:30 — itself inside quiet hours — so "decide" and "send" can't be the same call.
- `LogNotificationProvider` is the only `INotificationProvider` implementation — logs the message and returns a fake id. **No real WhatsApp/email/push credentials exist**, so nothing is actually delivered to a member yet; this is what lets the whole consent→policy→template→send loop run and be demoed end-to-end today. Upgrade path: implement the interface per real channel and swap the DI registration in `BKeeper.Infrastructure/DependencyInjection.cs` — nothing else changes.
- Wired into `DailyRulePipeline`: a new amber R01/R02/R03 alert auto-queues `MISS_YOU_SOFT` or `SCHEDULE_NUDGE` (per plan §7's `auto_message` column). R04/R08 never auto-send, matching the catalogue.
- Coach one-tap send (`POST /alerts/{id}/outreach`) accepts a template key or free text, skips the frequency-cap/holdout gates (those are system-only), still respects consent and quiet hours.
- Consent model is opt-out, not opt-in (`MemberConsent`'s doc comment explains why — no real consent-capture source exists yet); member page has toggle checkboxes per channel and shows outreach history.
- **Not built:** delivery-status webhooks, inbound-reply handling, Manager template-approval workflow, `{usual_class}`/`{coach}` are static placeholder text since `MemberProfile`/primary-coach data was never populated (a Week 3 gap, see OPEN_QUESTIONS.md).

## D20 — Week 5 complete: SLA escalation, claimed-idle release, auto-resolve, auto-expire
Added to the D16 foundation:
- `EscalationPolicy` (pure function, [src/BKeeper.Application/Alerts/EscalationPolicy.cs](../src/BKeeper.Application/Alerts/EscalationPolicy.cs)) implements the §6.4 SLA table — red escalates Coach→Manager at 24h, →Owner at 48h; amber at 3d/7d; info never escalates but auto-expires (→ `false_positive_no_action`) at 14d.
- Claimed-but-idle release: a `Claimed`/`InProgress` alert with no activity for 48h (red) / 5 days (amber) drops back to `New` so it re-enters the pool (and can be re-escalated on the next sweep).
- Auto-resolve on return: any member with 2+ attended visits in the last 10 days has all their open alerts closed as `AutoResolved`/`returned` — this is the "unassisted return" metric the plan tracks for false-positive rate.
- Snoozed alerts wake to `New` once `SnoozeUntil` passes.
- All of the above runs as `EscalationJob`, scheduled every 15 minutes via Hangfire in the Worker, and is also triggerable on demand via `POST /alerts/escalate/run` (useful for demos/ops, mirrors the dry-run pattern from `/rules/run`).
- **Simplification carried from D16:** alerts now always start `AssignedRole = Coach` (previously red started at Manager) so a single escalation ladder covers both severities — see the note in `EscalationPolicy`'s doc comment for why red's plan-spec "Coach + Manager from the start" dual-visibility doesn't fit a single-assignee-role model.
- Outcome is now a closed vocabulary (`OutcomeTaxonomy`, plan §6.4) enforced server-side; `GET /alerts/outcomes` feeds the frontend's resolve dropdown (replacing an ad-hoc `window.prompt`).
- **Not built in this pass:** push notifications and the 08:00/weekly digest (Week 6 territory — the "Push/digest" column of the SLA table).

## D14 — Rename to BKeeper
Product/repo renamed from BlackKeeper to **BKeeper** at the owner's request. Namespaces, solution
name, Docker Compose project name, and all docs use BKeeper.

## D13 — Docker Compose, not Kubernetes, for local/first deploy
"Run it on a local docker pod called BKeeper" was clarified with the owner to mean a Docker Compose
stack (project name `bkeeper`, containers `bkeeper-api`/`bkeeper-worker`/`bkeeper-web`/`bkeeper-postgres`),
not a Kubernetes pod. No k8s manifests exist yet. If a cluster deploy is needed later, the containers
already build as three independent images so a Helm chart / k8s manifests can wrap them without
changing application code.

## D15 — Frontend and backend are independently deployable
- `web/` has its own `Dockerfile` and build context (`web/`), with **no dependency on the .NET
  solution** — it can be built, containerized and deployed on its own.
- `BKeeper.Api` and `BKeeper.Worker` each have their own Dockerfile (context = repo root, since they
  share project references) and can be deployed to separate hosts/scaling groups.
- The three services only ever talk over HTTP (the SPA calls the API's public REST surface; CORS is
  configured via `Cors__AllowedOrigins`). There is no shared filesystem or in-process coupling.
- The API's base URL is baked into the web image at build time via the `VITE_API_URL` build arg
  (`docker compose build --build-arg` or the `API_ORIGIN` env var read by `docker-compose.yml`).
  **Upgrade path** if the same web image needs to point at different API URLs per environment without
  a rebuild: switch to a runtime `config.js` fetched before the Vue app boots, instead of a Vite
  build-time env var.

## D16 — Scope of this pass: "working foundation" (plan Weeks 0-4, partial Week 5)
Building 100% of the 12-week plan in one pass would produce mostly non-functional stubs (ML scoring,
WhatsApp/push/email, GDPR tooling, dashboards, goals/evaluations, the API connector). Instead this
pass built a smaller slice that **actually runs end-to-end**:
- Multi-tenant domain model + Postgres schema + migrations
- JWT auth (Owner/Manager/Coach/Reception/Member roles), box-scoped via a global EF query filter
- Excel import (Members/Classes/Attendance + a new optional Notes sheet), idempotent by external_id
- Member timeline and **member notes** (new requirement, see D17)
- Rule engine (R01, R02, R03, R04, R08) + alert creation/grouping/cooldown + a bare-bones inbox
  (claim/snooze/resolve, no SLA escalation job yet)
- A `POST /rules/run` endpoint doubling as the plan's Week-4 "dry-run mode" acceptance test
- Vue 3 + TS SPA: login/bootstrap, member list/detail, alert inbox, import upload

**Explicitly not built** (tracked so nothing is silently forgotten — see also OPEN_QUESTIONS.md):
ML scoring service (R13, Week 8), notifications/outreach (WhatsApp/push/email, Week 6 — meaning
"human contact in last 7 days" always evaluates false in the pipeline today), goals & evaluations
(Week 7), dashboards (Week 9), the platform API connector (Week 10), SLA escalation timers and the
08:00 digest job, GDPR/export tooling, R05/R06/R07/R09/R10/R11/R12/R14, and the full onboarding
day-0/3/7/14/30/60/90 track (R08 here is a simplified single "no visit by day 7" check).

## D17 — Member notes (new requirement)
Added `MemberNote` (text, source: Coach/Import/Member/System, author, active flag) — not in the
original plan's data model. Surfaced on the member profile (`GET/POST/DELETE /members/{id}/notes`)
and in the member timeline. The Excel import template gained an **optional** `Notes` sheet
(`member_id`, `note`) so notes like "recovering from a knee injury" can be delivered by the source
system, not just typed by a coach. Not wired into rule suppression yet — `Member.InjuryFlagUntil`
exists on the domain model for that, but nothing sets it from a note automatically (see
OPEN_QUESTIONS.md).

## D18 — Tenant isolation via EF Core global query filter, not Postgres RLS
The plan (§0.4) asks for "Postgres Row Level Security" enforcement. This pass uses an EF Core global
query filter on `BoxId` (scoped through an `AsyncLocal`-backed `ICurrentBoxAccessor`, set from the
JWT's `box_id` claim per request) instead. It is enforced in one place (`BKeeperDbContext`) and
covered by the "box A cannot read box B" acceptance test's *intent*, but it is defense at the ORM
layer, not the database layer — a raw SQL query or a bug that bypasses EF would not be caught.
**Upgrade path:** add `ENABLE ROW LEVEL SECURITY` + policies in a migration and set
`app.current_box_id` per connection, as defense in depth, before handling real member PII in
production.

## D19 — No column-mapping wizard; one fixed Excel template
The importer expects exact sheet/column names as in §4 of the plan (plus the new `Notes` sheet). No
per-box mapping profile UI yet. **Upgrade path:** add once a second box's export uses different
headers.
