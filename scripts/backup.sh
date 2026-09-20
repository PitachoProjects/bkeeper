#!/usr/bin/env bash
# Plan §11: backup/restore drill. Dumps the running bkeeper-postgres container to a local file.
set -euo pipefail

OUT_DIR="${1:-./backups}"
mkdir -p "$OUT_DIR"
TIMESTAMP=$(date -u +%Y%m%dT%H%M%SZ)
OUT_FILE="$OUT_DIR/bkeeper_${TIMESTAMP}.dump"

echo "Backing up bkeeper-postgres -> $OUT_FILE"
docker exec bkeeper-postgres pg_dump -U bkeeper -Fc bkeeper > "$OUT_FILE"
echo "Done: $(du -h "$OUT_FILE" | cut -f1)"
