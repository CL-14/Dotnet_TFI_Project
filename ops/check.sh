#!/usr/bin/env bash
# One-command health summary of the collector.
cd "$(dirname "$0")/.." || exit 1
DB=data/collector.db

echo "== Container =="
docker compose ps

echo; echo "== Minutes since the last successful poll =="
sqlite3 "$DB" "SELECT (strftime('%s','now') - MAX(polled_at)) / 60 FROM poll_log WHERE ok = 1;"

echo; echo "== Last 24 hours (about 1,400 polls expected) =="
sqlite3 -header -column "$DB" "
  SELECT feed, COUNT(*) AS polls, SUM(ok) AS ok, COUNT(*) - SUM(ok) AS failed
  FROM poll_log WHERE polled_at > strftime('%s','now','-1 day') GROUP BY feed;"

echo; echo "== Recent failures =="
sqlite3 -header -column "$DB" "
  SELECT datetime(polled_at,'unixepoch') AS time_utc, feed, http_status, substr(error,1,50) AS error
  FROM poll_log WHERE ok = 0 ORDER BY polled_at DESC LIMIT 5;"

echo; echo "== Size and disk =="
du -sh data
df -h / | tail -1
