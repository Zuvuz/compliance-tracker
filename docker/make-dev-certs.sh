#!/bin/sh
# US-017: certificates for running the whole tracker in Docker over HTTPS (docker compose --profile full).
# Run by the "certs" service. Makes what is missing in the given folder (./certs, never committed):
#   ca.crt / ca.key   a local certificate authority that exists only on this machine
#   tls.crt / tls.key the server certificate for localhost (the web app) and keycloak.localhost (Keycloak)
#   db.crt / db.key   US-084: the certificate for the database, postgres-full
#   <hash>.0          a copy of ca.crt named the way OpenSSL looks it up, so the web app trusts Keycloak
# Files that exist are kept, so an older ./certs only gets the database certificate added.
# Trust ca.crt in your browser or operating system once to avoid the browser warning (see README).
set -eu

dir="$1"

# A certificate signed by the local CA. $1: file name without extension, $2: the subject alternative names,
# $3: the common name
sign() {
    openssl req -newkey rsa:2048 -nodes -keyout "$dir/$1.key" -out "$dir/$1.csr" -subj "/CN=$3"
    # Browsers accept server certificates of at most 398 days
    cat > "$dir/$1.ext" <<EOF
subjectAltName=$2
basicConstraints=CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
EOF
    openssl x509 -req -in "$dir/$1.csr" -CA "$dir/ca.crt" -CAkey "$dir/ca.key" -CAcreateserial \
        -out "$dir/$1.crt" -days 397 -sha256 -extfile "$dir/$1.ext"
    rm -f "$dir/$1.csr" "$dir/$1.ext" "$dir/ca.srl"
    echo "Made $dir/$1.crt"
}

if [ ! -f "$dir/ca.crt" ] || [ ! -f "$dir/ca.key" ]; then
    openssl req -x509 -newkey rsa:3072 -sha256 -days 825 -nodes \
        -keyout "$dir/ca.key" -out "$dir/ca.crt" \
        -subj "/CN=Compliance Tracker local CA" \
        -addext "basicConstraints=critical,CA:TRUE" \
        -addext "keyUsage=critical,keyCertSign,cRLSign"
    # A new CA: the certificates of the old one are no longer trusted
    rm -f "$dir/tls.crt" "$dir/tls.key" "$dir/db.crt" "$dir/db.key" "$dir"/*.0
    echo "Made a new local CA in $dir"
fi

if [ ! -f "$dir/tls.crt" ] || [ ! -f "$dir/tls.key" ]; then
    sign tls "DNS:localhost,DNS:keycloak.localhost" localhost
fi

# US-084: the web app, the migrations and the backup check this name (SSL Mode=VerifyFull)
if [ ! -f "$dir/db.crt" ] || [ ! -f "$dir/db.key" ]; then
    sign db "DNS:postgres-full" postgres-full
fi

cp "$dir/ca.crt" "$dir/$(openssl x509 -hash -noout -in "$dir/ca.crt").0"

# Keycloak (uid 1000) and the web app (uid 1654) must read the server key; the database copies its key
# and makes it private (postgres-tls-entrypoint.sh). They are only for local use.
chmod 644 "$dir/tls.crt" "$dir/tls.key" "$dir/db.crt" "$dir/db.key" "$dir/ca.crt" "$dir"/*.0
chmod 600 "$dir/ca.key"
echo "The certificates in $dir are ready"
