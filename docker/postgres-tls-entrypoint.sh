#!/bin/sh
# US-084: starts PostgreSQL so it only accepts connections over TLS (the "postgres-full" service).
# The certificate and key come from TLS_SOURCE (default /certs, made by make-dev-certs.sh): db.crt and db.key.
# PostgreSQL refuses a key that others can read, and a bind mount from Windows cannot set the owner,
# so the two files are copied to a folder of the container's own, owned by postgres, with the key at mode 0600.
# pg_hba_tls.conf allows TCP connections only with TLS (hostssl); the local socket is used by the health check.
set -eu

src="${TLS_SOURCE:-/certs}"
dst=/var/lib/postgresql-tls

if [ ! -f "$src/db.crt" ] || [ ! -f "$src/db.key" ]; then
    echo "The database certificate is missing: $src/db.crt and $src/db.key (made by the certs service)" >&2
    exit 1
fi

mkdir -p "$dst"
cp "$src/db.crt" "$dst/server.crt"
cp "$src/db.key" "$dst/server.key"
chown -R postgres:postgres "$dst"
chmod 644 "$dst/server.crt"
chmod 600 "$dst/server.key"

# The image's own command is "postgres"; any other arguments are extra settings (e.g. "-c fsync=off").
# "postgres" is left out wherever it is, as it is given once below.
for arg do
    shift
    [ "$arg" = "postgres" ] && continue
    set -- "$@" "$arg"
done

exec docker-entrypoint.sh postgres \
    -c ssl=on \
    -c ssl_cert_file="$dst/server.crt" \
    -c ssl_key_file="$dst/server.key" \
    -c ssl_min_protocol_version=TLSv1.2 \
    -c hba_file=/etc/postgresql/pg_hba_tls.conf \
    "$@"
