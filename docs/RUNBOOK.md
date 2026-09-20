# Runbook

Operational reference for what's actually built. See [PLAN.md](PLAN.md) for the product spec,
[DECISIONS.md](DECISIONS.md) for what shipped and why, [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md) for gaps.

## Starting the stack

```bash
docker compose up -d --build
```

Five containers: `bkeeper-postgres`, `bkeeper-api`, `bkeeper-worker`, `bkeeper-ml`, `bkeeper-web`.
Check health: `docker compose ps` — all five should show `healthy` (web has no healthcheck defined,
just `Up`). First run: bootstrap a box at `/bootstrap`, then import an `.xlsx`.

## Scheduled jobs (all via Hangfire in `bkeeper-worker`)

| Job | Schedule | What it does |
|---|---|---|
| `daily-rule-pipeline` | 05:30 daily | R01-R08, alert creation, auto-outreach queueing |
| `escalation-job` | every 15 min | SLA escalation, claimed-idle release, auto-resolve, auto-expire |
| `outreach-dispatcher` | every 15 min | Sends `Queued` outreach outside quiet hours |
| `goals-evaluations-job` | 05:45 daily | R10 (goal at risk), R11 (eval overdue) |
| `ml-scoring-job` | Sunday 23:30 | Weekly churn-risk scoring (shadow mode, R13) |
| `anonymization-job` | 06:00 daily | Anonymizes members cancelled 24+ months ago |

Each has an on-demand trigger for testing/ops (see [README.md](../README.md) for the curl commands),
and Hangfire's own dashboard at `/hangfire` (requires a valid JWT) shows run history and failures.

## Backup and restore

```bash
scripts/backup.sh                                    # dumps bkeeper-postgres to ./backups/
scripts/restore.sh ./backups/bkeeper_<ts>.dump        # restores into bkeeper_restore_test, verifies, leaves live DB untouched
```

Drilled live against the running stack while building this (see DECISIONS.md) — both scripts work.
No automated backup schedule exists; this is a manual/cron-it-yourself pair of scripts.

## Common operational tasks

- **A member wants their data**: `GET /members/{id}/gdpr/export` (any authenticated staff) — one JSON
  bundle of everything held on them. Every export is audit-logged (`AuditLogs` table).
- **A member wants to be forgotten now**: `POST /members/{id}/gdpr/anonymize` — scrubs name/email/phone
  immediately, doesn't wait for the 24-month retention sweep.
- **Something looks wrong with alerts**: check `/hangfire` for job failures first, then
  `POST /alerts/escalate/run` to re-run the sweep manually and watch what changes.
- **Onboarding a new box**: `POST /auth/bootstrap` (refuses if any box already exists — this app is
  single-box only; see OPEN_QUESTIONS.md on whether multi-box-per-deployment is ever needed).
- **A password/JWT signing key needs rotating**: set `JWT_SIGNING_KEY` in the environment before
  `docker compose up` — rotating it invalidates all existing sessions immediately (no rolling rotation).

## Known operational gaps (see OPEN_QUESTIONS.md for the full list)

- No automated backup schedule, no off-host backup storage — the scripts are manual.
- No alerting on job failures beyond what's visible in `/hangfire` — no email/Slack/PagerDuty hook.
- Rate limiting is in-memory, per-API-instance — doesn't hold up across multiple API replicas without
  a shared store (see the comment in `Program.cs`).
- No secrets manager integration (Key Vault, etc.) — secrets are environment variables with dev
  defaults baked into `docker-compose.yml`; **change every `*_change_me` default before any real
  deployment**.
