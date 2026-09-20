# Decisions (ADR log)

Extends §2 of [PLAN.md](PLAN.md). Newest first.

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
