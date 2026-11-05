#!/bin/sh
# US-014: one backup of the database now, then the old backups are deleted.
# Usage: backup-db.sh <folder>. The database comes from the standard PostgreSQL variables
# (PGHOST, PGUSER, PGPASSWORD, PGDATABASE), so no password is written in a file or a command line.
#   R1: <folder>/<database>_<yyyy-mm-dd>.dump in pg_dump's custom format: everything, also the PDFs, evidence
#       files, the activity log and the keys that protect the GitHub tokens (R6)
#   R2: backups older than BACKUP_KEEP_DAYS days (default 30) are deleted
set -eu

dir="$1"
keep="${BACKUP_KEEP_DAYS:-30}"
name="${PGDATABASE}_$(date +%F).dump"

mkdir -p "$dir"

# Written to a temporary name and checked before it replaces anything, so a failed backup never looks finished
pg_dump --format=custom --file="$dir/$name.partial"
pg_restore --list "$dir/$name.partial" > /dev/null
mv "$dir/$name.partial" "$dir/$name"

# Keeps today and the days before it, BACKUP_KEEP_DAYS backups in all
find "$dir" -maxdepth 1 -type f -name "${PGDATABASE}_*.dump" -mtime +"$((keep - 1))" -print -delete

echo "Backup made: $dir/$name"
