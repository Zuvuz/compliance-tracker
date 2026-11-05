#!/bin/sh
# US-014 R4: restores a backup from backup-db.sh into the database in PGDATABASE.
# Usage: restore-db.sh <backup file>. Run inside a PostgreSQL container (scripts/restore.sh does that for you).
# Everything in the database is replaced by the backup, in one transaction: if anything fails, nothing changes.
set -eu

file="$1"

# A file that is not a readable backup is refused before the database is touched
pg_restore --list "$file" > /dev/null

pg_restore --clean --if-exists --no-owner --exit-on-error --single-transaction --dbname="$PGDATABASE" "$file"

echo "Restored $file into the database $PGDATABASE"
