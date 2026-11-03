#!/bin/sh
# US-017: certificates for running the whole tracker in Docker over HTTPS (docker compose --profile full).
# Run by the "certs" service. Makes them once in the given folder (./certs, never committed):
#   ca.crt / ca.key   a local certificate authority that exists only on this machine
#   tls.crt / tls.key the server certificate for localhost (the web app) and keycloak.localhost (Keycloak)
#   <hash>.0          a copy of ca.crt named the way OpenSSL looks it up, so the web app trusts Keycloak
# Trust ca.crt in your browser or operating system once to avoid the browser warning (see README).
set -eu

dir="$1"
if [ -f "$dir/tls.crt" ] && [ -f "$dir/tls.key" ] && [ -f "$dir/ca.crt" ]; then
    echo "The certificates already exist in $dir"
    exit 0
fi

openssl req -x509 -newkey rsa:3072 -sha256 -days 825 -nodes \
    -keyout "$dir/ca.key" -out "$dir/ca.crt" \
    -subj "/CN=Compliance Tracker local CA" \
    -addext "basicConstraints=critical,CA:TRUE" \
    -addext "keyUsage=critical,keyCertSign,cRLSign"

openssl req -newkey rsa:2048 -nodes -keyout "$dir/tls.key" -out "$dir/tls.csr" -subj "/CN=localhost"

# Browsers accept server certificates of at most 398 days
cat > "$dir/tls.ext" <<EOF
subjectAltName=DNS:localhost,DNS:keycloak.localhost
basicConstraints=CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
EOF
openssl x509 -req -in "$dir/tls.csr" -CA "$dir/ca.crt" -CAkey "$dir/ca.key" -CAcreateserial \
    -out "$dir/tls.crt" -days 397 -sha256 -extfile "$dir/tls.ext"

cp "$dir/ca.crt" "$dir/$(openssl x509 -hash -noout -in "$dir/ca.crt").0"
rm -f "$dir/tls.csr" "$dir/tls.ext" "$dir/ca.srl"

# Keycloak (uid 1000) and the web app (uid 1654) must read the server key. It is only for local use.
chmod 644 "$dir/tls.crt" "$dir/tls.key" "$dir/ca.crt" "$dir"/*.0
chmod 600 "$dir/ca.key"
echo "Made new certificates in $dir"
