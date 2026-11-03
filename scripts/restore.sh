#!/bin/sh
# US-014 R4: restores a backup into the tracker's database, with one command:
#   ./scripts/restore.sh backups/compliance_tracker_2026-10-25.dump
# Everything in the database is replaced by the backup. The web app is stopped first and started again after.
# Add --yes to skip the question (e.g. in a script).
set -eu

file="${1:-}"
if [ -z "$file" ] || [ ! -f "$file" ]; then
    echo "Usage: ./scripts/restore.sh <backup file> [--yes]"
    echo "The backups are in ./backups"
    exit 1
fi

if [ "${2:-}" != "--yes" ]; then
    printf 'This replaces ALL data in the database compliance_tracker with %s. Type yes to go on: ' "$file"
    read -r answer
    [ "$answer" = "yes" ] || { echo "Nothing was changed"; exit 1; }
fi

cd "$(dirname "$0")/.."

# The web app must not write while the data is replaced
docker compose stop web 2> /dev/null || true

docker compose cp "$file" postgres:/tmp/restore.dump
docker compose cp docker/restore-db.sh postgres:/tmp/restore-db.sh
docker compose exec -T -e PGUSER=postgres -e PGDATABASE=compliance_tracker postgres sh /tmp/restore-db.sh /tmp/restore.dump
docker compose exec -T postgres rm -f /tmp/restore.dump /tmp/restore-db.sh

docker compose --profile full start web 2> /dev/null || true
echo "Done"
