#!/usr/bin/env bash
# Makes a consistent copy of the collector database, checks it, compresses it and keeps the newest 7.
set -euo pipefail
cd "$(dirname "$0")/.."        # the repo root, one folder above ops/
mkdir -p backups
STAMP=$(date +%Y%m%d-%H%M)

# .backup is SQLite's own safe copy: it works while the collector is writing.
sqlite3 data/collector.db ".backup 'backups/collector-$STAMP.db'"

# integrity_check prints "ok" when the copy is healthy.
RESULT=$(sqlite3 "backups/collector-$STAMP.db" "PRAGMA integrity_check;")
if [ "$RESULT" != "ok" ]; then
  echo "Backup $STAMP FAILED integrity check: $RESULT" >&2
  exit 1
fi

gzip "backups/collector-$STAMP.db"
# Delete all but the newest 7 backups.
ls -1t backups/collector-*.db.gz | tail -n +8 | xargs -r rm --
echo "Backup $STAMP ok"
