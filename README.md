# BKeeper

Retention platform for CrossFit boxes — see [docs/PLAN.md](docs/PLAN.md) for the full product plan,
[docs/DECISIONS.md](docs/DECISIONS.md) for what this codebase actually builds and why, and
[docs/OPEN_QUESTIONS.md](docs/OPEN_QUESTIONS.md) for known gaps and defaults.

## Stack

- **Backend:** .NET 8 (`BKeeper.Api`, `BKeeper.Worker`), EF Core + PostgreSQL, Hangfire
- **Frontend:** Vue 3 + TypeScript + Vite + Pinia + Vue Router (`web/`)
- **Infra:** Docker Compose (project name `bkeeper`) — Postgres, API, Worker, Web as four
  independently-buildable containers (see [docs/DECISIONS.md#d15](docs/DECISIONS.md) for how the
  frontend/backend split works for deployment)

## Run it (Docker Compose)

```bash
docker compose up -d --build
```

- API: http://localhost:5080 (Swagger at `/swagger`, health at `/health`)
- Web: http://localhost:5173
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
```

Sent messages don't go anywhere real yet — there's no WhatsApp/email/push provider wired up, only a
log-based one (see [docs/DECISIONS.md#d21](docs/DECISIONS.md)) — check the `outreach` table or the
member page's "Outreach history" to see what would have been sent.

## Run it locally (without Docker)

```bash
# Postgres only, via compose
docker compose up -d postgres

# Backend
dotnet run --project src/BKeeper.Api
dotnet run --project src/BKeeper.Worker

# Frontend
cd web && npm install && npm run dev
```

## Tests

```bash
dotnet test src/BKeeper.Tests.Unit
```

## Repo layout

```
BKeeper/
  docs/            PLAN.md, DECISIONS.md, OPEN_QUESTIONS.md
  src/
    BKeeper.Domain/         entities, enums, rule contracts — no dependencies
    BKeeper.Application/    metrics, rule implementations, alert orchestration, import contracts
    BKeeper.Infrastructure/ EF Core, Postgres, Excel import, JWT auth, Hangfire pipeline
    BKeeper.Api/            REST API (controllers, auth)
    BKeeper.Worker/         Hangfire host (daily rule run)
    BKeeper.Tests.Unit/     xunit — rules, metrics, alert orchestration, workout classifier
  web/             Vue 3 SPA (its own Dockerfile — deployable independently)
  infra/docker/    Dockerfiles for the API and Worker (repo-root build context)
  docker-compose.yml
```
