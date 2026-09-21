#!/usr/bin/env bash
# One-time migration: copies the full bkeeper schema + data from the local
# bkeeper-postgres container into a remote Postgres instance (e.g. Supabase).
# Intended for a FRESH target database — safe to re-run against an empty one,
# not meant to sync an already-populated target.
set -euo pipefail

: "${SUPABASE_HOST:?Set SUPABASE_HOST, e.g. aws-1-eu-west-1.pooler.supabase.com}"
: "${SUPABASE_PORT:=5432}"
: "${SUPABASE_DB:=postgres}"
: "${SUPABASE_USER:?Set SUPABASE_USER, e.g. postgres.<project-ref>}"
: "${PGPASSWORD:?Set PGPASSWORD to the Supabase database password}"

DUMP_FILE=$(mktemp)
trap 'rm -f "$DUMP_FILE"' EXIT

echo "Dumping bkeeper-postgres (schema + data) -> $DUMP_FILE"
docker exec bkeeper-postgres pg_dump -U bkeeper -Fc --no-owner --no-acl bkeeper > "$DUMP_FILE"

echo "Restoring into $SUPABASE_HOST:$SUPABASE_PORT/$SUPABASE_DB as $SUPABASE_USER..."
docker run --rm -i -e PGPASSWORD="$PGPASSWORD" postgres:16-alpine \
  pg_restore --host="$SUPABASE_HOST" --port="$SUPABASE_PORT" --username="$SUPABASE_USER" \
    --dbname="$SUPABASE_DB" --no-owner --no-acl < "$DUMP_FILE"

echo "Done. Verify row counts on both sides, e.g.:"
echo "  PGPASSWORD=\$PGPASSWORD docker run --rm -e PGPASSWORD -i postgres:16-alpine psql --host=$SUPABASE_HOST --port=$SUPABASE_PORT --username=$SUPABASE_USER --dbname=$SUPABASE_DB -c 'SELECT count(*) FROM \"Members\";'"
