#!/bin/sh
# US-014 R1: runs backup-db.sh every day at BACKUP_TIME (default 02:00, in the container's TZ).
# Started by the "backup" service in docker-compose.yml. When it starts and today's backup is missing,
# it makes one at once, so a new installation has a backup from the first day.
set -eu

dir=/backups
time="${BACKUP_TIME:-02:00}"

while true; do
    if [ ! -f "$dir/${PGDATABASE}_$(date +%F).dump" ]; then
        sh /scripts/backup-db.sh "$dir" || echo "The backup FAILED; it is tried again at the next start or tomorrow at $time"
    fi

    now=$(date +%s)
    next=$(date -d "$(date +%F) $time" +%s)
    if [ "$next" -le "$now" ]; then
        next=$(date -d "tomorrow $time" +%s)
    fi

    echo "Next backup: $(date -d "@$next" '+%F %H:%M')"
    sleep "$((next - now))"
done
