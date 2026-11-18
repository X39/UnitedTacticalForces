#!/usr/bin/env bash
# Enter all UTF pipeline variables once, on the Mozaic host, as root.
#
# Every value is written through `mozaic-admin pipeline-variable-set`, which can only create or override a
# secret variable and can never read one back. Each variable is a secret, trusted-only and restricted to the
# trusted branch and the release pipeline, so only that pipeline on that branch receives it.
#
# Values are typed (or pasted) without echo, kept in memory until you confirm, and handed to the tool on
# standard input through a shell builtin: they never appear in a process list, a file or the terminal.
# Press Enter on any prompt to skip that variable. Running the script again overrides existing values.
#
#   sudo ./set-pipeline-variables.sh              # prompt for everything, confirm, write
#   sudo ./set-pipeline-variables.sh --list       # show the variable names and what each is for
#   sudo ./set-pipeline-variables.sh --only NAME  # prompt for one variable
#
# How the tool is started (first match wins):
#   MOZAIC_ADMIN_CMD   a command that runs mozaic-admin directly (for tests or a native install)
#   MOZAIC_ADMIN_BIN   path of a mozaic-admin binary on this host; run inside the installed runtime image
#   (default)          /opt/mozaic/bin/mozaic-admin inside the installed runtime image MOZAIC_IMAGE
set -euo pipefail
umask 077

ORG=${UTF_ORG:-01a16590-011f-7d72-91fb-61d57c148fe7}
PROJECT=${UTF_PROJECT:-01a166ad-9024-7b30-a9bc-cd03ac37f2e8}
REPOSITORY=${UTF_REPOSITORY:-01a166ad-93f3-7fa2-a4f0-e57014752726}
RUN_AS=${UTF_RUN_AS:-01a1658e-c4ec-74a1-ac91-89bf2f8ce666}
BRANCH=${UTF_BRANCH:-refs/heads/master}
PIPELINE=${UTF_PIPELINE:-.mozaic/release.yml}
MINIMUM_LENGTH=8

# name|kind|description. kind: key = deploy key file or base64, secret = any text.
VARIABLES=(
  "UTF_DEPLOY_SSH_KEY|key|SSH key the release pipeline logs in to Amsel with (private key file, or its single-line base64)"
  "POSTGRES_PASSWORD|secret|PostgreSQL password of the UTF database"
  "STEAM_API_KEY|secret|Steam Web API key"
  "DISCORD_CLIENT_ID|secret|Discord OAuth client id"
  "DISCORD_CLIENT_SECRET|secret|Discord OAuth client secret"
  "DISCORD_APPLICATION_ID|secret|Discord bot application id"
  "DISCORD_PUBLIC_KEY|secret|Discord bot public key"
  "DISCORD_BOT_TOKEN|secret|Discord bot token"
  "TEAMSPEAK_PASSWORD|secret|TeamSpeak server password"
  "TEAMSPEAK_CHANNEL_PASSWORD|secret|TeamSpeak channel password"
  "HETZNER_TOKEN|secret|Hetzner Cloud API token used to provision game servers"
  "WORKER_REGISTRY_TOKEN|secret|Read-only registry key that lets game-server VMs pull the worker image"
)

fail() { printf 'set-pipeline-variables: %s\n' "$1" >&2; exit 1; }

list_variables() {
  local entry
  for entry in "${VARIABLES[@]}"; do
    IFS='|' read -r name kind description <<<"$entry"
    printf '%-28s %s\n' "$name" "$description"
  done
}

new_operation_id() { cat /proc/sys/kernel/random/uuid; }

# Builds the command that runs mozaic-admin; prints one argument per line (arguments hold no values).
admin_command() {
  if [ -n "${MOZAIC_ADMIN_CMD:-}" ]; then
    # shellcheck disable=SC2086
    printf '%s\n' $MOZAIC_ADMIN_CMD
    return
  fi
  [ -n "${MOZAIC_IMAGE:-}" ] || fail "set MOZAIC_IMAGE to the installed runtime image (for example localhost/mozaic-runtime@sha256:...)"
  local entrypoint=/opt/mozaic/bin/mozaic-admin
  local args=(podman run --rm -i --network "${MOZAIC_NETWORK:-mozaic}"
    --env-file /etc/mozaic/migrator.env --env-file /etc/mozaic/app.env
    --read-only --tmpfs /tmp:rw,noexec,nosuid,nodev,size=67108864
    --cap-drop all --security-opt no-new-privileges)
  [ -r /etc/mozaic/postgres-tls/ca.crt ] \
    && args+=(--volume /etc/mozaic/postgres-tls/ca.crt:/run/mozaic-postgres-ca/ca.crt:ro)
  if [ -n "${MOZAIC_ADMIN_BIN:-}" ]; then
    [ -x "$MOZAIC_ADMIN_BIN" ] || fail "MOZAIC_ADMIN_BIN is not an executable file"
    entrypoint=/opt/mozaic-varset/mozaic-admin
    args+=(--volume "$MOZAIC_ADMIN_BIN:$entrypoint:ro")
  fi
  args+=(--entrypoint "$entrypoint" "$MOZAIC_IMAGE")
  printf '%s\n' "${args[@]}"
}

# Writes one variable; the value arrives on standard input of the tool and nowhere else.
write_variable() {
  local name=$1 value=$2 operation
  operation=$(new_operation_id)
  local command=()
  mapfile -t command < <(admin_command)
  printf '%s' "$value" | "${command[@]}" pipeline-variable-set \
    --organization-id "$ORG" --project-id "$PROJECT" --repository-id "$REPOSITORY" \
    --name "$name" --run-as "$RUN_AS" --operation-id "$operation" \
    --reason "UTF release pipeline variable $name entered by the host operator" \
    --branch "$BRANCH" --pipeline "$PIPELINE" --value-stdin
}

# Reads a key either from a file path or as pasted single-line base64, and normalizes it to base64.
read_key_value() {
  local answer decoded
  IFS= read -r -p "  Path of the key file, or paste its base64 (Enter to skip): " answer || true
  [ -n "$answer" ] || return 0
  if [ -f "$answer" ]; then
    answer=$(base64 -w0 < "$answer")
  fi
  decoded=$(printf '%s' "$answer" | base64 -d 2>/dev/null) || fail "that is neither a readable key file nor valid base64"
  case "$decoded" in
    "-----BEGIN OPENSSH PRIVATE KEY-----"*) ;;
    *) fail "that does not decode to an OpenSSH private key" ;;
  esac
  printf '%s' "$answer"
}

main() {
  local only=""
  case "${1:-}" in
    --list) list_variables; return 0 ;;
    --only) only=${2:?--only needs a variable name} ;;
    "") ;;
    *) fail "unknown argument: $1" ;;
  esac
  # The tool itself enforces host root; this early check is only for a clear message (tests may skip it).
  [ "${UTF_VARS_SKIP_ROOT_CHECK:-}" = 1 ] || [ "$(id -u)" = 0 ] || fail "run as root on the Mozaic host"
  if [ -z "${MOZAIC_ADMIN_CMD:-}" ]; then
    command -v podman >/dev/null || fail "podman is required"
    [ -r /etc/mozaic/migrator.env ] && [ -r /etc/mozaic/app.env ] || fail "/etc/mozaic/migrator.env and app.env must be readable"
  fi

  declare -A values=()
  local entry name kind description value found=false
  printf 'Press Enter to skip a variable. Input is not shown.\n\n'
  for entry in "${VARIABLES[@]}"; do
    IFS='|' read -r name kind description <<<"$entry"
    [ -z "$only" ] || [ "$only" = "$name" ] || continue
    found=true
    printf '%s\n  %s\n' "$name" "$description"
    if [ "$kind" = key ]; then
      value=$(read_key_value)
    else
      IFS= read -rs -p "  Value (Enter to skip): " value || true
      printf '\n'
    fi
    [ -n "$value" ] || { printf '  skipped\n\n'; continue; }
    if [ "${#value}" -lt "$MINIMUM_LENGTH" ]; then
      fail "$name is shorter than $MINIMUM_LENGTH characters; short values would garble pipeline logs when masked"
    fi
    values[$name]=$value
    printf '  accepted (%s characters)\n\n' "${#value}"
  done
  [ -z "$only" ] || [ "$found" = true ] || fail "unknown variable name: $only"
  [ "${#values[@]}" -gt 0 ] || { printf 'Nothing entered; nothing written.\n'; return 0; }

  printf 'About to write these secret variables (names only) for branch %s and pipeline %s:\n' "$BRANCH" "$PIPELINE"
  printf '  %s\n' "${!values[@]}" | sort
  local confirm
  IFS= read -r -p "Write them now? [y/N] " confirm || true
  [ "$confirm" = y ] || [ "$confirm" = Y ] || { printf 'Cancelled; nothing written.\n'; return 0; }

  local failed=0
  for name in $(printf '%s\n' "${!values[@]}" | sort); do
    if write_variable "$name" "${values[$name]}"; then
      printf 'written: %s\n' "$name"
    else
      printf 'FAILED: %s\n' "$name" >&2
      failed=1
    fi
  done
  values=()
  [ "$failed" = 0 ] || fail "at least one variable was not written; rerun for the failed names with --only NAME"
  printf '\nDone. The tool printed metadata only; no value can be read back.\n'
}

main "$@"
