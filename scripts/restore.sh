#!/bin/sh
# US-014 R4: restores a backup into the tracker's database, with one command:
#   ./scripts/restore.sh backups/compliance_tracker_2026-10-25.dump
# Everything in the database is replaced by the backup. The web app is stopped first and started again after.
# Add --yes to skip the question (e.g. in a script).
# US-084: the whole stack's database is postgres-full. Add --dev to restore into the development database instead.
set -eu

file=""
yes=""
service="postgres-full"
for arg in "$@"; do
    case "$arg" in
        --yes) yes=1 ;;
        --dev) service="postgres" ;;
        *) file="$arg" ;;
    esac
done

if [ -z "$file" ] || [ ! -f "$file" ]; then
    echo "Usage: ./scripts/restore.sh <backup file> [--yes] [--dev]"
    echo "The backups are in ./backups (the development database's in ./backups/dev)"
    exit 1
fi

if [ -z "$yes" ]; then
    printf 'This replaces ALL data in the database compliance_tracker (%s) with %s. Type yes to go on: ' "$service" "$file"
    read -r answer
    [ "$answer" = "yes" ] || { echo "Nothing was changed"; exit 1; }
fi

cd "$(dirname "$0")/.."

# The web app must not write while the data is replaced
docker compose --profile full stop web 2> /dev/null || true

# Inside the container the restore uses the local socket, so it needs no TLS
docker compose --profile full cp "$file" "$service":/tmp/restore.dump
docker compose --profile full cp docker/restore-db.sh "$service":/tmp/restore-db.sh
docker compose --profile full exec -T -e PGUSER=postgres -e PGDATABASE=compliance_tracker "$service" sh /tmp/restore-db.sh /tmp/restore.dump
docker compose --profile full exec -T "$service" rm -f /tmp/restore.dump /tmp/restore-db.sh

docker compose --profile full start web 2> /dev/null || true
echo "Done"
