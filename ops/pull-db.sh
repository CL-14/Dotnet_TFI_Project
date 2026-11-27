#!/usr/bin/env bash
# This is my script to run on fedora for local testing: copies a fresh, consistent snapshot of my server collector
# database to this machine, so the local web app shows up-to-date data.

# Usage: ops/pull-db.sh
set -euo pipefail   # stop on the first error, on unset variables, and on failures inside pipes

: "${BOXY_HOST:?Set BOXY_HOST to the ssh host or alias of boxy}"
: "${BOXY_DIR:?Set BOXY_DIR to the project folder on boxy}"
LOCAL_DB="${LOCAL_DB:-$HOME/dev-collector.db}"
TMP_DB="$LOCAL_DB.download"

echo "Snapshotting the database on $BOXY_HOST..."
# On the server: ".backup" makes a safe copy even while the collector is writing (copying the file with cp
# could catch it half-written) The copy is gzip'd to stdout, so it travels compressed over ssh,
# then the temporary copy on the server is deleted. Locally, gunzip unpacks it into TMP_DB.
ssh "$BOXY_HOST" "set -e
  cd '$BOXY_DIR'
  sqlite3 data/collector.db \".backup '/tmp/tfibus-pull.db'\"
  gzip -c /tmp/tfibus-pull.db
  rm -f /tmp/tfibus-pull.db" | gunzip > "$TMP_DB"

# Check the copy before using it, integrity_check prints "ok" when the database is healthy.
RESULT=$(sqlite3 "$TMP_DB" "PRAGMA integrity_check;")
if [ "$RESULT" != "ok" ]; then
  echo "Downloaded copy FAILED integrity check: $RESULT" >&2
  rm -f "$TMP_DB"
  exit 1
fi

# Swap the new copy in. Old -wal/-shm side files belong to the previous database, so remove them.
rm -f "$LOCAL_DB-wal" "$LOCAL_DB-shm"
mv "$TMP_DB" "$LOCAL_DB"

echo "Saved to $LOCAL_DB ($(du -h "$LOCAL_DB" | cut -f1))."
echo "Newest successful poll in the copy (UTC): $(sqlite3 "$LOCAL_DB" "SELECT datetime(MAX(polled_at),'unixepoch') FROM poll_log WHERE ok = 1;")"
echo "Restart the local web app if it is running, so it opens the new file."
