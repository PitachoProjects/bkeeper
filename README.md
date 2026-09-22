# BKeeper

Retention platform for CrossFit boxes — see [docs/PLAN.md](docs/PLAN.md) for the full product plan,
[docs/DECISIONS.md](docs/DECISIONS.md) for what this codebase actually builds and why,
[docs/OPEN_QUESTIONS.md](docs/OPEN_QUESTIONS.md) for known gaps and defaults, and
[docs/RUNBOOK.md](docs/RUNBOOK.md) for day-to-day operations (backups, scheduled jobs, GDPR requests).

## Stack

- **Backend:** .NET 10 (`BKeeper.Api`, `BKeeper.Worker`), EF Core + PostgreSQL, Hangfire
- **ML:** Python 3.12 + FastAPI + scikit-learn + LightGBM + SHAP (`ml/`) — churn-risk scoring, shadow mode
- **Frontend:** Vue 3 + TypeScript + Vite + Pinia + Vue Router (`web/`)
- **Infra:** Docker Compose (project name `bkeeper`) — Postgres, API, Worker, ML, Web as five
  independently-buildable containers (see [docs/DECISIONS.md#d15](docs/DECISIONS.md) for how the
  frontend/backend split works for deployment)

## Run it (Docker Compose)

```bash
docker compose up -d --build
```

- API: http://localhost:5080 (Swagger at `/swagger`, health at `/health`)
- Web: http://localhost:5173
- ML service: http://localhost:8090 (health at `/health`) — auto-trains a synthetic-data model on first boot
- Postgres: localhost:5432 (`bkeeper`/`bkeeper`/`bkeeper`)

First run: open http://localhost:5173/bootstrap and create your box + Owner account (works once —
refuses if a box already exists).

Then, on the **Import** page, upload an `.xlsx` with `Members`, `Classes`, `Attendance` sheets (and
optionally `Notes` — `member_id`, `note`) per the template in [docs/PLAN.md §4](docs/PLAN.md).

On-demand triggers (all also run automatically via the Worker's Hangfire jobs — daily at 05:30 for
rules, every 15 min for escalation/outreach dispatch):

```bash
curl -X POST http://localhost:5080/rules/run              -H "Authorization: Bearer <token>"  # evaluate rules, create alerts
curl -X POST http://localhost:5080/alerts/escalate/run     -H "Authorization: Bearer <token>"  # SLA escalation, auto-resolve, auto-expire
curl -X POST http://localhost:5080/outreach/dispatch       -H "Authorization: Bearer <token>"  # send Queued messages (outside quiet hours)
curl -X POST http://localhost:5080/risk-scores/run         -H "Authorization: Bearer <token>"  # weekly ML scoring (shadow mode) — Manager/Owner only
curl -X POST http://localhost:5080/health-score/run         -H "Authorization: Bearer <token>"  # recompute the Athlete Health Score for every active member
curl -X POST http://localhost:5080/coaches/backfill         -H "Authorization: Bearer <token>"  # link any still-unlinked ClassSession.CoachName values to Coach records — Owner/Manager only
curl -X POST http://localhost:5080/gdpr/anonymize/run       -H "Authorization: Bearer <token>"  # anonymize members cancelled 24+ months ago
```

Sent messages don't go anywhere real yet — there's no WhatsApp/email/push provider wired up, only a
log-based one (see [docs/DECISIONS.md#d21](docs/DECISIONS.md)) — check the `outreach` table or the
member page's "Outreach history" to see what would have been sent.

Goals and evaluation forms live on the member page: add a goal and record progress against it, or
send one of the 6 seeded forms (ONBOARDING, PULSE_D30, PULSE_D90, QUARTERLY, BENCHMARK, EXIT) — it
queues a link (`/f/{token}`, single-use, expires in 14 days) that opens as a public, no-login page.
A negative or health-flagged answer creates an alert automatically.

Churn risk (plan §8, R13) shows up on the member page as a "Churn risk" card, visible to Manager/Owner
only — it's **shadow mode**: scores are computed and stored weekly (or on demand via the endpoint
above) but never create an alert. Two models run side by side: the original LightGBM ensemble
(Stage C, shown by default) and a standalone, calibrated logistic regression baseline (Stage B —
`GET /risk-scores/members/{id}/compare`, coefficients instead of SHAP for interpretability; see
[docs/DECISIONS.md#d27](docs/DECISIONS.md) and `docs/RUNBOOK.md`'s "Comparing the two churn models"
section). Both were trained on synthetic data (see [docs/DECISIONS.md#d23](docs/DECISIONS.md)) since
no real export exists yet — treat the risk numbers as a pipeline demo, not a real prediction.

The **Dashboards** page has four tabs (retention, alert ops, workouts, my week) — cohort retention
curves, SLA compliance, save rate, holdout-vs-treated, window×type heatmap, class fill, and a coach's
open-alerts-this-week view. Retention cohorts export as CSV from the page. The retention tab can be
filtered by coach and, for Manager/Owner, has a click-triggered "Get AI summary" card that turns the
same numbers into a plain-language narrative via Anthropic's Messages API (see "Turning on AI
retention summaries" in [docs/RUNBOOK.md](docs/RUNBOOK.md) — off by default, never invents a number
not already on the page). Most stats across the dashboard have an "ⓘ What does this mean?" toggle
backed by a formal metric-definition catalog (`GET /metric-definitions`) so the definition, formula,
and limitations of a number are one click away instead of tribal knowledge (see
[docs/DECISIONS.md#d28](docs/DECISIONS.md)).

The **Athlete Health Score** (Settings → Health score weights) is a configurable, versioned composite
of attendance, consistency, booking behaviour, progress, and engagement — shown on each member's page
with a full weight/score/contribution breakdown, distinct from the shadow-mode ML churn risk score
below. Re-weighting creates a new version rather than rewriting history, so a score computed
yesterday still shows what it showed (see [docs/DECISIONS.md#d29](docs/DECISIONS.md)).

**Coaches** get their own page (promoted from the free-text `ClassSession.CoachName` field, with a
one-time backfill migration linking existing sessions) and a coach filter on the retention dashboard
for coach-level drill-down. **Payments** are recorded per member (Owner/Manager/Reception) on the
member page — data-model-only, no payment gateway integration.

GDPR: `GET /members/{id}/gdpr/export` returns every piece of personal data held on a member as one
JSON bundle; `POST /members/{id}/gdpr/anonymize` scrubs their PII immediately (right-to-be-forgotten).
Both are audit-logged. `/auth/login` and `/auth/bootstrap` are rate-limited (10 req/min/IP).
`scripts/backup.sh` / `scripts/restore.sh` do a Postgres backup/restore drill against the running
stack — see [docs/RUNBOOK.md](docs/RUNBOOK.md).

## Run it locally (without Docker)

```bash
# Postgres only, via compose
docker compose up -d postgres

# Backend
dotnet run --project src/BKeeper.Api
dotnet run --project src/BKeeper.Worker

# ML service
cd ml && pip install -r requirements.txt && uvicorn app.serve:app --reload --port 8090

# Frontend
cd web && npm install && npm run dev
```

## Tests

```bash
dotnet test src/BKeeper.Tests.Unit
cd ml && python -m pytest tests/
```

## Deploy (Azure)

Two GitHub Actions workflows push `master` straight to Azure on every relevant change:

- **Frontend** — `.github/workflows/azure-static-web-apps-proud-desert-0db9c9403.yml` builds `web/`
  and deploys it to the Azure Static Web App at
  https://proud-desert-0db9c9403.5.azurestaticapps.net. Requires the
  `AZURE_STATIC_WEB_APPS_API_TOKEN_PROUD_DESERT_0DB9C9403` repo secret (from the Static Web App's
  deployment token). `web/staticwebapp.config.json` adds the SPA fallback rewrite Vue Router's
  history mode needs.
- **API** — `.github/workflows/master_bkeeper-api.yml` (auto-generated by Azure's Deployment
  Center, then fixed up to build/publish `src/BKeeper.Api` specifically instead of the whole
  solution) builds with the .NET 10 SDK and deploys it to the Linux Web App at
  https://bkeeper-api-gqfub3ebf5gyeshr.westeurope-01.azurewebsites.net (App Service runtime stack:
  `.NET 10`). Auth is OIDC federated credentials, not a publish profile — it needs the
  `AZUREAPPSERVICE_CLIENTID_...`, `AZUREAPPSERVICE_TENANTID_...` and
  `AZUREAPPSERVICE_SUBSCRIPTIONID_...` repo secrets that Azure's Deployment Center provisions
  automatically when you connect GitHub Actions deployment from the Portal.

Swagger is served at `/swagger` in every environment, including the deployed Web App, so the live
API contract is browsable at
https://bkeeper-api-gqfub3ebf5gyeshr.westeurope-01.azurewebsites.net/swagger. The Web App also needs
its own app settings for `ConnectionStrings__Postgres`, `Jwt__SigningKey`, etc. — see
`src/BKeeper.Api/appsettings.json` for the full set of keys; `appsettings.Production.json` only pins
`Cors:AllowedOrigins` to the Static Web App's origin.

## Repo layout

```
BKeeper/
  docs/            PLAN.md, DECISIONS.md, OPEN_QUESTIONS.md, RUNBOOK.md
  src/
    BKeeper.Domain/         entities, enums, rule contracts — no dependencies
    BKeeper.Application/    metrics, health scoring, metric-definition catalog, rule implementations,
                             alert orchestration, coach backfill, retention-overview service, import
                             contracts, LLM narrative-generator interface
    BKeeper.Infrastructure/ EF Core, Postgres, Excel import, JWT auth, Hangfire pipeline, Anthropic
                             narrative-generator implementation
    BKeeper.Api/            REST API (controllers, auth) — members, coaches, payments, health score,
                             metric definitions, risk scores, insights narrative, dashboards, alerts
    BKeeper.Worker/         Hangfire host (daily rule run, health score, ML scoring, GDPR sweep, …)
    BKeeper.Tests.Unit/     xunit — rules, metrics, health scoring, alert orchestration, workout
                             classifier, coach backfill, narrative-layer guardrails
  web/             Vue 3 SPA (its own Dockerfile — deployable independently). Sidebar nav is grouped
                   by workflow (Dashboard / Athletes / Classes / Insights / Reports / Configuration)
                   rather than one entry per page — see `src/App.vue`.
  ml/              Python FastAPI scoring service (its own Dockerfile — deployable independently)
    app/           features.py (single source of truth), synthetic.py, train.py (LightGBM ensemble,
                    Stage C), logistic.py (logistic regression baseline, Stage B), metrics.py (shared
                    evaluation), serve.py, explain.py
    backtest_compare.py     side-by-side metrics for both churn models on the same split
    tests/         pytest — feature fixtures, no-leakage checks, both models' metrics/serving
  scripts/         backup.sh / restore.sh — Postgres backup/restore drill
  infra/docker/    Dockerfiles for the API and Worker (repo-root build context)
  docker-compose.yml
```
