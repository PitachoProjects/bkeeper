#!/usr/bin/env bash
# Plan §11: backup/restore drill. Restores a dump (from backup.sh) into a NEW database on the
# running bkeeper-postgres container, leaving the live "bkeeper" database untouched — the point of
# a drill is proving the backup is usable, not overwriting production with it.
set -euo pipefail

DUMP_FILE="${1:?Usage: restore.sh <dump-file> [target-db-name]}"
TARGET_DB="${2:-bkeeper_restore_test}"

echo "Creating $TARGET_DB..."
docker exec bkeeper-postgres psql -U bkeeper -d postgres -c "DROP DATABASE IF EXISTS $TARGET_DB;"
docker exec bkeeper-postgres psql -U bkeeper -d postgres -c "CREATE DATABASE $TARGET_DB;"

echo "Restoring $DUMP_FILE -> $TARGET_DB..."
docker exec -i bkeeper-postgres pg_restore -U bkeeper -d "$TARGET_DB" --no-owner < "$DUMP_FILE"

echo "Verifying..."
docker exec bkeeper-postgres psql -U bkeeper -d "$TARGET_DB" -c "SELECT count(*) AS members FROM \"Members\";"
echo "Restore drill OK. Drop it with: docker exec bkeeper-postgres psql -U bkeeper -d postgres -c \"DROP DATABASE $TARGET_DB;\""
