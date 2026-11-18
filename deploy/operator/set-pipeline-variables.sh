#!/usr/bin/env bash
# Enter all UTF pipeline variables once, from your LOCAL terminal. Nothing is run by hand on the server.
#
# The script prompts here (input hidden), then reaches the Mozaic host over SSH and writes each value through
# `mozaic-admin pipeline-variable-set`, which can only create or override a secret variable and can never
# read one back. Every variable is a secret, trusted-only and restricted to the trusted branch and the release
# pipeline. A value travels only on the standard input of that SSH connection: never in an argument, a file,
# a process list or the terminal. Press Enter on any prompt to skip it. Running again overrides values.
#
#   UTF_OPERATOR_SSH=root@amsel ./set-pipeline-variables.sh              # prompt, confirm, write
#   UTF_OPERATOR_SSH=root@amsel ./set-pipeline-variables.sh --only NAME  # one variable
#   ./set-pipeline-variables.sh --list                                    # names and purposes
#
# Settings (environment):
#   UTF_OPERATOR_SSH    ssh login that is root on the Mozaic host (or set UTF_REMOTE_SUDO=sudo for a sudoer)
#   MOZAIC_ADMIN_BIN    local path of a Linux mozaic-admin binary that has pipeline-variable-set. It is
#                       uploaded to a private temporary directory for this run and removed afterwards.
#                       Without it the installed runtime image's own /opt/mozaic/bin/mozaic-admin is used.
#   MOZAIC_IMAGE        installed runtime image; detected from the running containers when unset
#   UTF_SSH             ssh program (on Git Bash for Windows the Windows OpenSSH is picked automatically,
#                       because only it can use the Windows ssh-agent)
set -euo pipefail
umask 077

ORG=${UTF_ORG:-01a16590-011f-7d72-91fb-61d57c148fe7}
PROJECT=${UTF_PROJECT:-01a166ad-9024-7b30-a9bc-cd03ac37f2e8}
REPOSITORY=${UTF_REPOSITORY:-01a166ad-93f3-7fa2-a4f0-e57014752726}
RUN_AS=${UTF_RUN_AS:-01a1658e-c4ec-74a1-ac91-89bf2f8ce666}
BRANCH=${UTF_BRANCH:-refs/heads/master}
PIPELINE=${UTF_PIPELINE:-.mozaic/release.yml}
TARGET=${UTF_OPERATOR_SSH:-}
REMOTE_TMP=${UTF_REMOTE_TMP:-/var/tmp}
MINIMUM_LENGTH=8

# name|kind|description. kind: key = key file or base64, secret = any text.
VARIABLES=(
  "UTF_DEPLOY_SSH_KEY|key|SSH key the release pipeline logs in to Amsel with (path of the private key file, or its single-line base64)"
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
  local entry name kind description
  for entry in "${VARIABLES[@]}"; do
    IFS='|' read -r name kind description <<<"$entry"
    printf '%-28s %s\n' "$name" "$description"
  done
}

pick_ssh() {
  if [ -n "${UTF_SSH:-}" ]; then SSH=$UTF_SSH; return; fi
  SSH=ssh
  # Git Bash's own ssh cannot talk to the Windows ssh-agent; the Windows OpenSSH can.
  if [ "$(uname -o 2>/dev/null)" = Msys ] && [ -x /c/Windows/System32/OpenSSH/ssh.exe ]; then
    SSH=/c/Windows/System32/OpenSSH/ssh.exe
  fi
}

# Runs a command on the host (as root, or through sudo). Standard input is passed through unchanged.
remote() {
  if [ "${UTF_REMOTE_SUDO:-}" = sudo ]; then
    "$SSH" -T -o BatchMode=yes "$TARGET" "sudo sh -c $(printf '%q' "$1")"
  else
    "$SSH" -T -o BatchMode=yes "$TARGET" "$1"
  fi
}

ENTRYPOINT=/opt/mozaic/bin/mozaic-admin
REMOTE_DIR=""
CA_MOUNT=""
IMAGE=""

cleanup_remote() {
  if [ -n "$REMOTE_DIR" ]; then
    remote "rm -f $REMOTE_DIR/mozaic-admin; rmdir $REMOTE_DIR" >/dev/null 2>&1 || \
      printf 'warning: could not remove %s on the host; remove it by hand\n' "$REMOTE_DIR" >&2
    REMOTE_DIR=""
  fi
}

prepare_remote() {
  [ -n "$TARGET" ] || fail "set UTF_OPERATOR_SSH to the ssh login of the Mozaic host operator, for example root@amsel"
  [ "$(remote 'id -u')" = 0 ] || fail "that login is not root on the host (use a root login, or UTF_REMOTE_SUDO=sudo)"
  if [ -n "${MOZAIC_ADMIN_CMD:-}" ]; then return; fi   # tests and native installs: run this command remotely

  IMAGE=${MOZAIC_IMAGE:-}
  if [ -z "$IMAGE" ]; then
    local candidates
    candidates=$(remote "podman ps --format '{{.Image}}'" | grep -i 'mozaic-runtime' | sort -u || true)
    [ "$(printf '%s\n' "$candidates" | grep -c .)" = 1 ] || \
      fail "could not detect the installed runtime image; set MOZAIC_IMAGE. Candidates seen: ${candidates:-none}"
    IMAGE=$candidates
  fi
  if remote 'test -r /etc/mozaic/postgres-tls/ca.crt'; then
    CA_MOUNT="--volume /etc/mozaic/postgres-tls/ca.crt:/run/mozaic-postgres-ca/ca.crt:ro"
  fi
  if [ -n "${MOZAIC_ADMIN_BIN:-}" ]; then
    [ -f "$MOZAIC_ADMIN_BIN" ] || fail "MOZAIC_ADMIN_BIN is not a file: $MOZAIC_ADMIN_BIN"
    REMOTE_DIR=$(remote "umask 077; mktemp -d $REMOTE_TMP/mozaic-varset.XXXXXX")
    case "$REMOTE_DIR" in
      "$REMOTE_TMP"/mozaic-varset.??????) ;;
      *) REMOTE_DIR=""; fail "unexpected temporary directory from the host" ;;
    esac
    trap cleanup_remote EXIT
    remote "cat > $REMOTE_DIR/mozaic-admin && chmod 700 $REMOTE_DIR/mozaic-admin" < "$MOZAIC_ADMIN_BIN"
    ENTRYPOINT=/opt/mozaic-varset/mozaic-admin
  fi
}

# The remote command line holds only fixed names and identifiers, never a value.
remote_command() {
  local name=$1 operation=$2 reason
  reason=$(printf '%q' "UTF release pipeline variable $name entered by the host operator")
  local tail="pipeline-variable-set --organization-id $ORG --project-id $PROJECT --repository-id $REPOSITORY \
--name $name --run-as $RUN_AS --operation-id $operation --reason $reason \
--branch $BRANCH --pipeline $PIPELINE --value-stdin"
  if [ -n "${MOZAIC_ADMIN_CMD:-}" ]; then
    printf '%s %s' "$MOZAIC_ADMIN_CMD" "$tail"
    return
  fi
  local mounts=$CA_MOUNT
  [ -z "$REMOTE_DIR" ] || mounts="$mounts --volume $REMOTE_DIR/mozaic-admin:$ENTRYPOINT:ro"
  printf 'podman run --rm -i --network %s --env-file /etc/mozaic/migrator.env --env-file /etc/mozaic/app.env %s --read-only --tmpfs /tmp:rw,noexec,nosuid,nodev,size=67108864 --cap-drop all --security-opt no-new-privileges --entrypoint %s %s %s' \
    "${MOZAIC_NETWORK:-mozaic}" "$mounts" "$ENTRYPOINT" "$IMAGE" "$tail"
}

write_variable() {
  local name=$1 value=$2
  printf '%s' "$value" | remote "$(remote_command "$name" "$(cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen)")"
}

# Reads a key either from a local file path or as pasted single-line base64, and normalizes it to base64.
read_key_value() {
  local answer decoded
  IFS= read -r -p "  Path of the key file on this computer, or paste its base64 (Enter to skip): " answer || true
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
  pick_ssh

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

  printf 'About to write these secret variables (names only) for branch %s and pipeline %s via %s:\n' \
    "$BRANCH" "$PIPELINE" "${TARGET:-<UTF_OPERATOR_SSH not set>}"
  printf '  %s\n' "${!values[@]}" | sort
  local confirm
  IFS= read -r -p "Write them now? [y/N] " confirm || true
  [ "$confirm" = y ] || [ "$confirm" = Y ] || { printf 'Cancelled; nothing written.\n'; return 0; }

  prepare_remote
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
