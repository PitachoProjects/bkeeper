# BKeeper

Retention platform for CrossFit boxes — see [docs/PLAN.md](docs/PLAN.md) for the full product plan,
[docs/DECISIONS.md](docs/DECISIONS.md) for what this codebase actually builds and why,
[docs/OPEN_QUESTIONS.md](docs/OPEN_QUESTIONS.md) for known gaps and defaults, and
[docs/RUNBOOK.md](docs/RUNBOOK.md) for day-to-day operations (backups, scheduled jobs, GDPR requests).

## Stack

- **Backend:** .NET 8 (`BKeeper.Api`, `BKeeper.Worker`), EF Core + PostgreSQL, Hangfire
- **ML:** Python 3.12 + FastAPI + scikit-learn + LightGBM + SHAP (`ml/`) — churn-risk scoring, shadow mode
- **Frontend:** Vue 3 + TypeScript + Vite + Pinia + Vue Router (`web/`)
- **Infra:** Docker Compose (project name `bkeeper`) — Postgres, API, Worker, ML, Web as five
  independently-buildable containers (see [docs/DECISIONS.md#d15](docs/DECISIONS.md) for how the
  frontend/backend split works for deployment)

## Run it (Docker Compose)

```bash
docker compose up -d --build
```

By default the API/Worker connect to the local `postgres` container. To point at a remote
Postgres instance instead (e.g. Supabase), copy `.env.example` to `.env` and set
`DATABASE_CONNECTION_STRING` — `docker compose` picks up `.env` automatically. See the comments
in `.env.example` for the Supabase-specific gotcha (use the **Session pooler** connection string,
not the direct connection host, which is IPv6-only and unreachable from most networks). `.env` is
gitignored — never commit real credentials.

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
above) but never create an alert. The model was trained on synthetic data (see
[docs/DECISIONS.md#d23](docs/DECISIONS.md)) since no real export exists yet — treat the risk numbers
as a pipeline demo, not a real prediction.

The **Dashboards** page has four tabs (retention, alert ops, workouts, my week) — cohort retention
curves, SLA compliance, save rate, holdout-vs-treated, window×type heatmap, class fill, and a coach's
open-alerts-this-week view. Retention cohorts export as CSV from the page.

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

## Repo layout

```
BKeeper/
  docs/            PLAN.md, DECISIONS.md, OPEN_QUESTIONS.md, RUNBOOK.md
  src/
    BKeeper.Domain/         entities, enums, rule contracts — no dependencies
    BKeeper.Application/    metrics, rule implementations, alert orchestration, import contracts
    BKeeper.Infrastructure/ EF Core, Postgres, Excel import, JWT auth, Hangfire pipeline
    BKeeper.Api/            REST API (controllers, auth)
    BKeeper.Worker/         Hangfire host (daily rule run)
    BKeeper.Tests.Unit/     xunit — rules, metrics, alert orchestration, workout classifier
  web/             Vue 3 SPA (its own Dockerfile — deployable independently)
  ml/              Python FastAPI scoring service (its own Dockerfile — deployable independently)
    app/           features.py (single source of truth), synthetic.py, train.py, serve.py, explain.py
    tests/         pytest — feature fixtures, no-leakage checks
  scripts/         backup.sh / restore.sh — Postgres backup/restore drill
  infra/docker/    Dockerfiles for the API and Worker (repo-root build context)
  docker-compose.yml
```
