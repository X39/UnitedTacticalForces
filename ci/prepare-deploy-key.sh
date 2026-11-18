#!/bin/sh
# Write the deployment SSH key from the trusted-branch pipeline variable UTF_DEPLOY_SSH_KEY and verify it
# before any long build. Accepts the OpenSSH private key either as PEM text (line breaks may have been lost
# by a paste) or as a single line of base64 of that PEM. Prints only the public fingerprint.
set -eu
umask 077
dir=${UTF_PRIVATE_DIR:-/workspace/.private}
expected=${UTF_DEPLOY_PUBLIC_KEY:-deploy/utf-deploy.pub}
fail() { printf 'UTF deploy key: %s\n' "$1" >&2; exit 1; }

[ -n "${UTF_DEPLOY_SSH_KEY:-}" ] || fail "the UTF_DEPLOY_SSH_KEY pipeline variable is missing or empty"
[ -r "$expected" ] || fail "the committed public key is missing"
mkdir -p "$dir"
chmod 700 "$dir"

case "$UTF_DEPLOY_SSH_KEY" in
    *"BEGIN OPENSSH PRIVATE KEY"*) pem=$UTF_DEPLOY_SSH_KEY ;;
    *) pem=$(printf '%s' "$UTF_DEPLOY_SSH_KEY" | base64 -d 2>/dev/null) || fail "the value is neither PEM nor base64 of PEM" ;;
esac
case "$pem" in
    *"BEGIN OPENSSH PRIVATE KEY"*) ;;
    *) fail "the value does not contain an OpenSSH private key" ;;
esac

# Rebuild the file from its base64 body so lost or doubled line breaks cannot break parsing.
body=$(printf '%s' "$pem" | tr -d '\r' \
    | sed -e 's/-----BEGIN OPENSSH PRIVATE KEY-----//' -e 's/-----END OPENSSH PRIVATE KEY-----//' \
    | tr -d ' \n\t')
[ -n "$body" ] || fail "the key body is empty"
key=$dir/deploy_key
{
    printf '%s\n' '-----BEGIN OPENSSH PRIVATE KEY-----'
    printf '%s' "$body" | fold -w 70
    printf '\n%s\n' '-----END OPENSSH PRIVATE KEY-----'
} > "$key"
chmod 600 "$key"

ssh-keygen -y -f "$key" > "$dir/deploy_key.pub" 2>/dev/null || fail "the private key cannot be parsed (wrong format or passphrase)"
derived=$(cut -d ' ' -f 1,2 "$dir/deploy_key.pub")
committed=$(cut -d ' ' -f 1,2 "$expected")
[ "$derived" = "$committed" ] || fail "the variable holds a different key than the committed public key"
printf 'UTF deploy key: ok %s\n' "$(ssh-keygen -lf "$dir/deploy_key.pub" | cut -d ' ' -f 2)"
